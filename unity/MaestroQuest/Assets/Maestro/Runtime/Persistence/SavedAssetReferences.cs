// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Maestro.Quest.Creation;
using Maestro.Quest.Avatar;
using Maestro.Quest.Imports;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json;
namespace Maestro.Quest.Persistence
{
    internal sealed class SavedAssetReferenceProof
    {
        internal bool Ready {get;}
        internal bool Retained {get;}
        internal bool Uncertain {get;}
        internal IReadOnlyList<string> Reasons {get;}
        internal SavedAssetReferenceProof(bool ready,bool retained,bool uncertain,IEnumerable<string> reasons)
        {Ready=ready;Retained=retained;Uncertain=uncertain;Reasons=Array.AsReadOnly(reasons.ToArray());}
    }
    // Worker-only proof for one exact identity. The caller must exclude authored
    // writes and merge live/Undo/runtime references; this is not deletion authority
    // by itself. Every read is fresh and bounded, with no Load/recovery or repair.
    internal static class SavedAssetReferences
    {
        // Count ALL entries visited, including unrelated files and final directory
        // stability checks. 64 MiB accommodates current room/memory backups plus
        // the 256-module library; larger retained histories fail closed.
        internal const int MaximumVisitedEntries=1024;
        internal const long MaximumReadBytes=64L*1024*1024;
        static bool Expected(Exception error)=>error is IOException||error is UnauthorizedAccessException||error is InvalidDataException||
            error is JsonException||error is DecoderFallbackException||error is ProgramFault||error is OverflowException||error is System.Security.SecurityException;
        sealed class Audit
        {
            internal bool Retained,Uncertain;internal int Visited;internal long Bytes;
            readonly HashSet<string> reasons=new(StringComparer.Ordinal);
            readonly Dictionary<string,string[]> directories=new(StringComparer.Ordinal);
            internal void Found(string reason){Retained=true;reasons.Add(reason);}
            internal void Unknown(){Uncertain=true;reasons.Add("Some retained reference evidence is unavailable or unsupported.");}
            internal SavedAssetReferenceProof Result(bool ready)=>new(ready,Retained,Uncertain,reasons.OrderBy(x=>x,StringComparer.Ordinal));
            string[] Enumerate(string directory,CancellationToken token)
            {
                var paths=new List<string>();
                foreach(string path in System.IO.Directory.EnumerateFileSystemEntries(directory)){
                    token.ThrowIfCancellationRequested();if(++Visited>MaximumVisitedEntries)throw new InvalidDataException("Retained reference enumeration exceeds its limit.");paths.Add(path);
                }
                return paths.OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            }
            internal string[] Directory(string directory,CancellationToken token)
            {
                string kind=WorkspaceFileInventory.Kind(directory);
                if(kind=="absent"){directories[directory]=Array.Empty<string>();return Array.Empty<string>();}
                if(kind!="directory")throw new IOException("Retained reference directory is unavailable.");
                var paths=Enumerate(directory,token);directories[directory]=paths;return paths;
            }
            internal void VerifyDirectories(CancellationToken token)
            {
                foreach(var pair in directories){token.ThrowIfCancellationRequested();string kind=WorkspaceFileInventory.Kind(pair.Key);
                    var now=kind=="absent"?Array.Empty<string>():kind=="directory"?Enumerate(pair.Key,token):throw new IOException("Retained reference directory changed.");
                    if(!now.SequenceEqual(pair.Value,StringComparer.Ordinal))throw new IOException("Retained reference files changed during inspection.");
                }
            }
            internal byte[] Read(string path,int limit,CancellationToken token)
            {
                token.ThrowIfCancellationRequested();if(WorkspaceFileInventory.Kind(path)!="file")throw new IOException("Retained reference is not a regular file.");
                using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);long length=stream.Length;
                if(length<1||length>limit||length+1>MaximumReadBytes-Bytes)throw new InvalidDataException("Retained reference bytes exceed their limit.");
                Bytes+=length+1;var bytes=new byte[(int)length];int at=0;
                while(at<bytes.Length){token.ThrowIfCancellationRequested();int read=stream.Read(bytes,at,Math.Min(65536,bytes.Length-at));if(read==0)throw new EndOfStreamException();at+=read;}
                if(stream.ReadByte()!=-1)throw new IOException("Retained reference changed during inspection.");token.ThrowIfCancellationRequested();return bytes;
            }
        }
        static bool Versioned(string name,string stem,out int version,out string suffix)
        {
            version=0;suffix=null;string prefix=stem+".v";
            if(!name.StartsWith(prefix,StringComparison.Ordinal))return false;int end=name.IndexOf(".json",prefix.Length,StringComparison.Ordinal);if(end<0)return false;
            string digits=name.Substring(prefix.Length,end-prefix.Length);
            if(!int.TryParse(digits,NumberStyles.None,CultureInfo.InvariantCulture,out version)||version<1||digits!=version.ToString(CultureInfo.InvariantCulture))return false;
            suffix=name.Substring(end+5);return true;
        }
        static bool DocumentSuffix(string suffix)=>suffix is "" or ".backup" or ".pending" or ".unreadable"||
            suffix!=null&&suffix.StartsWith(".prepare.",StringComparison.Ordinal)&&Guid.TryParseExact(suffix.Substring(9),"N",out _);
        static bool Family(string name,string stem)=>name.StartsWith(stem+".",StringComparison.OrdinalIgnoreCase);
        delegate bool RoomReferences(RoomStorage storage,byte[] bytes,int version,string identity,out bool uncertain);
        internal static SavedAssetReferenceProof ReadImages(string saveDirectory,string imageHash,CancellationToken token=default)
        {
            if(!ModelLibrary.ValidHash(imageHash))throw new ArgumentException("Choose an exact image identity.",nameof(imageHash));
            return Read(saveDirectory,imageHash,static (RoomStorage storage,byte[] bytes,int version,string identity,out bool uncertain)=>
                storage.RetainsImageDocument(bytes,version,identity,out uncertain),token);
        }
        internal static SavedAssetReferenceProof ReadAudio(string saveDirectory,string assetHash,CancellationToken token=default)
        {
            if(!ModelLibrary.ValidHash(assetHash))throw new ArgumentException("Choose an exact audio identity.",nameof(assetHash));
            return Read(saveDirectory,assetHash,static (RoomStorage storage,byte[] bytes,int version,string identity,out bool uncertain)=>
                storage.RetainsAudioDocument(bytes,version,identity,out uncertain),token);
        }
        internal static SavedAssetReferenceProof ReadModels(string saveDirectory,string modelHash,CancellationToken token=default)
        {
            if(!ModelLibrary.ValidHash(modelHash))throw new ArgumentException("Choose an exact model identity.",nameof(modelHash));
            return Read(saveDirectory,modelHash,static (RoomStorage storage,byte[] bytes,int version,string identity,out bool uncertain)=>
                storage.RetainsModelDocument(bytes,version,identity,out uncertain),token,avatarProfiles:true);
        }
        // The validated room selector differs by kind; models also have saved
        // avatar-profile roots. Stored programs,
        // modules and remembered values conservatively retain exact identities
        // regardless of the eventual action that may consume them.
        static SavedAssetReferenceProof Read(string saveDirectory,string identity,RoomReferences roomReferences,CancellationToken token,bool avatarProfiles=false)
        {
            string directory=Path.GetFullPath(saveDirectory);var audit=new Audit();
            try{
                token.ThrowIfCancellationRequested();WorkspaceFileInventory.Parents(directory);
                // Bound even unrelated entries before the snapshot coordinator's
                // format checks. The final pass detects changed directory sets.
                var paths=audit.Directory(directory,token);
                using var owner=RoomSnapshotTransaction.Inspect(directory,token);
                var rooms=new RoomStorage(directory);var rules=new RuleStorage(directory);
                foreach(string path in paths){
                    token.ThrowIfCancellationRequested();string name=Path.GetFileName(path);
                    try{
                        if(name=="room-snapshot.writer.lock"){
                            if(WorkspaceFileInventory.Kind(path)!="file")audit.Unknown();
                            continue;
                        }
                        if(Family(name,"room-snapshot")){audit.Unknown();continue;}
                        if(avatarProfiles&&Family(name,AvatarActivityProfiles.FileStem)){
                            if(!Versioned(name,AvatarActivityProfiles.FileStem,out int version,out string suffix)||!DocumentSuffix(suffix)){audit.Unknown();continue;}
                            var bytes=audit.Read(path,AvatarActivityProfiles.MaximumBytes,token);
                            if(AvatarActivityProfiles.RetainsModelDocument(directory,bytes,version,identity,out bool uncertain))audit.Found("Saved avatar activity profile or retained profile history");
                            if(uncertain)audit.Unknown();continue;
                        }
                        if(Family(name,"room")||Family(name,"behaviours")||Family(name,"rules")){
                            bool room=Family(name,"room");string stem=room?"room":"behaviours";
                            if(!Versioned(name,stem,out int version,out string suffix)||!DocumentSuffix(suffix)){audit.Unknown();continue;}
                            byte[] bytes=audit.Read(path,room?4*1024*1024:512*1024,token);bool uncertain;
                            bool retained=room?roomReferences(rooms,bytes,version,identity,out uncertain):rules.RetainsAssetDocument(bytes,version,identity,out uncertain);
                            if(retained)audit.Found(room?"Saved room or retained room history":"Saved behaviour or retained behaviour history");if(uncertain)audit.Unknown();continue;
                        }
                        if(Family(name,"program-memory")){
                            if(!Versioned(name,"program-memory",out int version,out string suffix)||version!=1||!(suffix is "" or ".backup"||suffix.StartsWith(".pending.",StringComparison.Ordinal)&&Guid.TryParseExact(suffix.Substring(9),"N",out _))){audit.Unknown();continue;}
                            if(ProgramMemoryDocument.Decode(audit.Read(path,ProgramMemoryDocument.MaximumBytes,token)).Retains(identity))audit.Found("Saved remembered behaviour value");continue;
                        }
                        if(Family(name,"program-modules")){
                            if(name!="program-modules.v1"){audit.Unknown();continue;}
                            ReadModules(audit,path,identity,token);
                        }
                    }catch(Exception error) when(Expected(error)){audit.Unknown();}
                }
                // A missing module directory is still a stability root: a newly
                // appeared library must not be mistaken for an empty reference set.
                if(!paths.Contains(Path.Combine(directory,"program-modules.v1"),StringComparer.Ordinal))audit.Directory(Path.Combine(directory,"program-modules.v1"),token);
                audit.VerifyDirectories(token);token.ThrowIfCancellationRequested();return audit.Result(true);
            }catch(OperationCanceledException){throw;}
            catch(Exception error) when(Expected(error)){audit.Unknown();return audit.Result(false);}
        }
        static void ReadModules(Audit audit,string directory,string identity,CancellationToken token)
        {
            foreach(string path in audit.Directory(directory,token)){
                token.ThrowIfCancellationRequested();string name=Path.GetFileName(path),hash=null;
                if(name.EndsWith(".json",StringComparison.Ordinal)&&ModelLibrary.ValidHash(name.Substring(0,name.Length-5)))hash=name.Substring(0,name.Length-5);
                else if(!(name.EndsWith(".tmp",StringComparison.Ordinal)&&Guid.TryParseExact(name.Substring(0,name.Length-4),"N",out _))){audit.Unknown();continue;}
                try{if(ProgramModuleLibrary.RetainedDefinitionReferences(audit.Read(path,ProgramModuleLibrary.MaximumBytes,token),hash,identity))audit.Found("Saved reusable behaviour module");}
                catch(Exception error) when(Expected(error)){audit.Unknown();}
            }
        }
    }
}
