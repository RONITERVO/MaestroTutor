// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using Maestro.Quest.Persistence;
namespace Maestro.Quest.Imports
{
    internal sealed class ImportedAssetInventory<TEntry>
    {
        internal TEntry[] Entries {get;}
        // Library-specific observation generation, stamped while its file lease is held.
        internal long SourceRevision {get;set;}
        internal bool Ready {get;}
        internal bool Complete {get;}
        internal bool EnumerationComplete {get;}
        internal int IssueCount {get;}
        internal string Error {get;}
        readonly Exception failure;
        internal ImportedAssetInventory(TEntry[] entries,bool ready,bool enumerated,int issues,Exception failure)
        {
            Entries=entries;Ready=ready;EnumerationComplete=enumerated;IssueCount=issues;this.failure=failure;
            Complete=ready&&enumerated&&issues==0;
            Error=!ready?"Library unavailable; its directory could not be read safely.":Complete?"":
                "Library incomplete: some files could not be checked or inspection limits were reached. Existing files are preserved.";
        }
        // Array-only callers cannot represent partiality. Preserve their strict
        // failure contract, including the first original format/access exception.
        internal TEntry[] RequireComplete()
        {
            if(!Complete)ExceptionDispatchInfo.Capture(failure??new InvalidDataException(Error)).Throw();
            return Entries;
        }
    }
    internal sealed class ImportedAssetReadBudget
    {
        long payload,metadata;
        internal ImportedAssetReadBudget(long payloadBytes,long metadataBytes){payload=payloadBytes;metadata=metadataBytes;}
        internal void Payload(long bytes)=>Reserve(ref payload,bytes);
        internal void Metadata(long bytes)=>Reserve(ref metadata,bytes);
        static void Reserve(ref long remaining,long bytes)
        {
            if(bytes<0||bytes>remaining)throw new InvalidDataException("The library inspection byte budget was reached.");
            remaining-=bytes;
        }
        internal byte[] ReadMetadata(string path,int maximum,CancellationToken cancel,Func<Exception> oversized=null)
        {
            cancel.ThrowIfCancellationRequested();FileAttributes attributes;
            try{attributes=File.GetAttributes(path);}
            catch(FileNotFoundException){return null;}
            catch(DirectoryNotFoundException){return null;}
            if((attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Linked asset metadata is unavailable.");
            if((attributes&FileAttributes.Directory)!=0)throw new IOException("Asset metadata is not a regular file.");
            using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            long length=input.Length;
            if(length>maximum)throw oversized?.Invoke()??new InvalidDataException("The asset information file exceeds its limit.");
            // Include the final change-detection byte in the reservation. Failed
            // entry reads still consume their reservation; retries cannot evade it.
            Metadata(length+1);var bytes=new byte[(int)length];int at=0;
            while(at<bytes.Length){cancel.ThrowIfCancellationRequested();int read=input.Read(bytes,at,bytes.Length-at);if(read==0)throw new EndOfStreamException("Asset metadata changed during inspection.");at+=read;}
            if(input.ReadByte()!=-1)throw new IOException("Asset metadata changed during inspection.");
            cancel.ThrowIfCancellationRequested();return bytes;
        }
    }
    internal static class ImportedAssetInventory
    {
        // A normal full library has 32 payloads and 32 sidecars. Inspect at most
        // 256 directory entries of ANY kind (plus one end probe), never recurse,
        // retain at most 32 entries, and sort only that bounded candidate set.
        // Metadata budgets allow 32 maximum-size model sidecars or 64 media
        // sidecars, plus their EOF probes. Payload budgets use each library's
        // existing byte limit; model discovery charges sizes without reading GLBs.
        internal const int MaximumVisitedEntries=256,MaximumRetainedEntries=32;
        internal const long ModelMetadataBytes=4L*1024*1024+32,MediaMetadataBytes=64L*1024+64;
        static bool Expected(Exception error)=>error is IOException||error is UnauthorizedAccessException||error is InvalidDataException||
            error is DecoderFallbackException||error is ModelImportException||error is System.Security.SecurityException;
        internal static ImportedAssetInventory<TEntry> Scan<TEntry>(string directory,string extension,ImportedAssetReadBudget budget,
            Func<string,ImportedAssetReadBudget,CancellationToken,TEntry> inspect,CancellationToken cancel,Func<IEnumerable<string>> enumerate=null)
        {
            cancel.ThrowIfCancellationRequested();
            try{
                FileAttributes attributes;
                try{attributes=File.GetAttributes(directory);}
                catch(FileNotFoundException){return new(Array.Empty<TEntry>(),true,true,0,null);}
                catch(DirectoryNotFoundException){return new(Array.Empty<TEntry>(),true,true,0,null);}
                WorkspaceArchive.NoLink(directory);
                if((attributes&FileAttributes.Directory)==0)throw new IOException("The asset library path is not a directory.");
            }catch(Exception error) when(Expected(error)){return new(Array.Empty<TEntry>(),false,false,1,error);}
            var paths=new List<string>();var entries=new List<TEntry>();Exception first=null;int issues=0;bool enumerated=false;
            void Issue(Exception error){issues++;first??=error;}
            try{
                using var iterator=(enumerate?.Invoke()??Directory.EnumerateFileSystemEntries(directory)).GetEnumerator();
                for(int visited=0;;visited++){
                    cancel.ThrowIfCancellationRequested();
                    if(!iterator.MoveNext()){enumerated=true;break;}
                    if(visited==MaximumVisitedEntries){Issue(new InvalidDataException("The library directory inspection limit was reached."));break;}
                    string path=iterator.Current;
                    if(string.Equals(Path.GetExtension(path),extension,StringComparison.OrdinalIgnoreCase))paths.Add(path);
                }
            }catch(Exception error) when(Expected(error)){Issue(error);}
            foreach(string path in paths.OrderBy(Path.GetFileName,StringComparer.Ordinal)){
                cancel.ThrowIfCancellationRequested();
                if(entries.Count==MaximumRetainedEntries){Issue(new InvalidDataException("The library result limit was reached."));break;}
                try{
                    // Recognize extension aliases as issues, but never pass them
                    // to adapters that address exact hashes with canonical paths.
                    if(!string.Equals(Path.GetExtension(path),extension,StringComparison.Ordinal))throw new InvalidDataException("The asset filename is not canonical.");
                    var entry=inspect(path,budget,cancel);if(entry is null)throw new InvalidOperationException("An asset inspector returned no entry.");entries.Add(entry);
                }
                catch(Exception error) when(Expected(error)){Issue(error);}
            }
            cancel.ThrowIfCancellationRequested();
            // Enumeration failure before finding any candidate is unavailable,
            // not evidence that the library is empty. A bounded prefix can still
            // provide usable entries, with explicit incomplete enumeration.
            return new(entries.ToArray(),enumerated||paths.Count>0,enumerated,issues,first);
        }
    }
}
