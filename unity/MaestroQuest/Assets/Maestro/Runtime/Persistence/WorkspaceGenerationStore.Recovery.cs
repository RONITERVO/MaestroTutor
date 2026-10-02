// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Maestro.Quest.Imports;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Persistence
{
    /// <summary>Explicit recovery storage path. Never repairs an origin or falls back to another
    /// workspace. The host must preserve live owners under WorkspaceRecoveryHold before commit.
    /// A committed selection alone neither replaces content nor releases its review hold.</summary>
    internal sealed partial class WorkspaceGenerationStore
    {
        const int MaximumOriginBytes=64*1024;
        const string DamageProof="damaged-recovery.v1.json",DamageCommit="damaged-commit.v1.json";
        sealed class Origin
        {
            internal readonly byte[] Current,Previous;
            internal Origin(byte[] current,byte[] previous){Current=current;Previous=previous;}
            static JObject File(byte[] bytes)=>new() {["present"]=bytes!=null,["bytes"]=bytes?.Length??0,["sha256"]=bytes==null?"":ModelLibrary.Hash(bytes)};
            internal JObject Json()=>new() {["current"]=File(Current),["previous"]=File(Previous)};
            internal string Hash=>ModelLibrary.Hash(WorkspaceGenerationStore.Json(Json()));
        }
        static byte[] OriginFile(string path)
        {
            if(Directory.Exists(path))throw Invalid("A recovery selection path is a directory. Its contents are preserved.");
            if(!File.Exists(path))return null;
            WorkspaceArchive.NoLink(path);using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(input.Length>MaximumOriginBytes)throw Invalid("Selection evidence exceeds its recovery limit. Original files are preserved.");
            var bytes=new byte[(int)input.Length];int offset=0;
            while(offset<bytes.Length){int n=input.Read(bytes,offset,bytes.Length-offset);if(n==0)throw Invalid("Selection evidence changed while reading.");offset+=n;}
            if(input.ReadByte()!=-1)throw Invalid("Selection evidence grew while reading.");return bytes;
        }
        Origin ObserveOrigin(){CheckRoots();return new Origin(OriginFile(pointer),OriginFile(pointer+".previous"));}
        static void ExpectedOrigin(Origin current,string hash){if(!ModelLibrary.ValidHash(hash)||current.Hash!=hash)throw Invalid("Workspace selection evidence changed. Inspect recovery choices again.");}
        string[] GenerationIds()
        {
            if(!Directory.Exists(generations))return Array.Empty<string>();var ids=new List<string>();
            foreach(string path in Directory.EnumerateFileSystemEntries(generations)){
                if(ids.Count>=MaximumGenerations)throw Invalid("Workspace retention is full or contains unexpected paths.");WorkspaceArchive.NoLink(path);
                string id=Path.GetFileName(path);if(!Directory.Exists(path)||!Id(id))throw Invalid("An unrecognized retained workspace path needs inspection; its data is preserved.");ids.Add(id);
            }
            ids.Sort(StringComparer.Ordinal);return ids.ToArray();
        }
        // Metadata only. Selection fully verifies content on a worker before producing a preview.
        // Reading an empty installation does not initialize a pointer, lock or directory.
        internal JObject InspectRecovery()
        {
            var origin=ObserveOrigin();var candidates=new JArray();bool readable;
            try{Load();readable=true;}catch(Exception){readable=false;}
            foreach(string id in GenerationIds()){
                string hash="",error="";try{hash=(string)Metadata(id)["manifestHash"];}catch(Exception){error="This retained workspace has unavailable metadata. Original files are preserved.";}
                candidates.Add(new JObject {["generationId"]=id,["manifestHash"]=hash,["available"]=error=="",["error"]=error});
            }
            return new JObject {["originHash"]=origin.Hash,["selectionReadable"]=readable,["candidates"]=candidates};
        }
        internal PreparedWorkspaceGeneration PrepareDamagedRecovery(string originHash,string sourceId,string hash,CancellationToken cancellation=default)=>PrepareRecovery(originHash,sourceId,hash,null,cancellation);
        internal PreparedWorkspaceGeneration PrepareFreshRecovery(string originHash,CancellationToken cancellation=default,BundledAvatar includedAvatar=null)=>PrepareRecovery(originHash,"","",WorkspaceDefaults.Snapshot(includedAvatar),cancellation);
        PreparedWorkspaceGeneration PrepareRecovery(string originHash,string sourceId,string hash,WorkspaceArchiveSnapshot fresh,CancellationToken cancellation)
        {
            using var lease=Lease(initialize:false);cancellation.ThrowIfCancellationRequested();var origin=ObserveOrigin();ExpectedOrigin(origin,originHash);
            if(fresh==null&&(!ModelLibrary.ValidHash(hash)||(string)Metadata(sourceId)["manifestHash"]!=hash))throw Invalid("The recovery candidate identity changed. Inspect it again.");
            var protectedIds=GenerationIds();if(protectedIds.Length>=MaximumGenerations)throw Invalid("Workspace retention is full. Preserve and review retained workspaces before recovery.");
            string id=Guid.NewGuid().ToString("N"),target=GenerationPath(id);bool created=false;
            try {
                Directory.CreateDirectory(target);created=true;string data=Path.Combine(target,"data");byte[] manifest;WorkspaceArchiveReceipt receipt;
                if(fresh!=null){
                    using var archive=new MemoryStream();WorkspaceArchive.Write(archive,fresh,cancellation);archive.Position=0;
                    using var staged=WorkspaceArchive.Stage(archive,staging,cancellation);Directory.Move(staged.DirectoryPath,data);receipt=staged.Receipt;manifest=staged.ManifestBytes;hash=receipt.ManifestHash;
                }else{
                    string source=GenerationPath(sourceId);manifest=Bytes(Path.Combine(source,"manifest.json"),WorkspaceArchive.MaximumManifestBytes);if(ModelLibrary.Hash(manifest)!=hash)throw Invalid("The recovery candidate manifest changed. Its files are preserved.");
                    Directory.CreateDirectory(data);
                    void Copy(string name,byte[] bytes){cancellation.ThrowIfCancellationRequested();string output=Path.GetFullPath(Path.Combine(data,name.Replace('/',Path.DirectorySeparatorChar)));if(!output.StartsWith(data+Path.DirectorySeparatorChar,StringComparison.Ordinal))throw Invalid("Unsafe recovery path.");Directory.CreateDirectory(Path.GetDirectoryName(output));WriteNew(output,bytes);}
                    receipt=WorkspaceArchive.VerifyPreparedDirectory(Path.Combine(source,"data"),manifest,cancellation,Copy);
                }
                fault?.Invoke("damage.copied");cancellation.ThrowIfCancellationRequested();ExpectedOrigin(ObserveOrigin(),originHash);WriteNew(Path.Combine(target,"manifest.json"),manifest);
                if(origin.Current!=null)WriteNew(Path.Combine(target,"origin-current.bin"),origin.Current);
                if(origin.Previous!=null)WriteNew(Path.Combine(target,"origin-previous.bin"),origin.Previous);
                var sourceChoice=fresh!=null?new JObject {["kind"]="fresh"}:new JObject {["kind"]="retained",["generationId"]=sourceId};
                WriteNew(Path.Combine(target,DamageProof),Json(new JObject {["version"]=1,["originHash"]=originHash,["origin"]=origin.Json(),["source"]=sourceChoice,["manifestHash"]=hash,["protectedGenerations"]=new JArray(protectedIds)}));
                WriteNew(Path.Combine(target,"generation.v1.json"),Json(new JObject {["version"]=1,["id"]=id,["manifestHash"]=hash,["damagedRecovery"]=true}));
                fault?.Invoke("damage.ready");cancellation.ThrowIfCancellationRequested();return new PreparedWorkspaceGeneration(id,receipt);
            }catch{if(created)DeleteOwned(target);throw;}
        }
        static bool ValidRecoverySource(JObject source,JArray saved)
        {
            if(source==null)return false;
            return (string)source["kind"]=="fresh"?Exact(source,"kind"):Exact(source,"kind","generationId")&&(string)source["kind"]=="retained"&&TextId(source["generationId"])&&saved.Any(x=>(string)x==(string)source["generationId"]);
        }
        static bool ValidOrigin(JObject origin)
        {
            if(!Exact(origin,"current","previous"))return false;
            foreach(string key in new[]{"current","previous"}){
                if(origin[key] is not JObject file||!Exact(file,"present","bytes","sha256")||file["present"]?.Type!=JTokenType.Boolean||file["bytes"]?.Type!=JTokenType.Integer||file["sha256"]?.Type!=JTokenType.String)return false;
                long count=(long)file["bytes"];string hash=(string)file["sha256"];
                if(count<0||count>MaximumOriginBytes||((bool)file["present"]?!ModelLibrary.ValidHash(hash):count!=0||hash!=""))return false;
            }return true;
        }
        JObject ReadDamageProof(string id)
        {
            var metadata=Metadata(id);if((bool?)metadata["damagedRecovery"]!=true)throw Invalid("This generation is not a damaged-workspace recovery preview.");
            string folder=GenerationPath(id);var proof=Read(Path.Combine(folder,DamageProof),16384);
            if(!Exact(proof,"version","originHash","origin","source","manifestHash","protectedGenerations")||proof["version"]?.Type!=JTokenType.Integer||(int)proof["version"]!=1||proof["originHash"]?.Type!=JTokenType.String||!ModelLibrary.ValidHash((string)proof["originHash"])||!ValidOrigin(proof["origin"] as JObject)||proof["manifestHash"]?.Type!=JTokenType.String||(string)proof["manifestHash"]!=(string)metadata["manifestHash"]||proof["protectedGenerations"] is not JArray saved||saved.Count>=MaximumGenerations||saved.Any(x=>!TextId(x))||saved.Select(x=>(string)x).Distinct().Count()!=saved.Count||saved.Any(x=>(string)x==id)||!ValidRecoverySource(proof["source"] as JObject,saved))throw Invalid("Invalid damaged-workspace recovery identity.");
            var captured=new Origin(OriginFile(Path.Combine(folder,"origin-current.bin")),OriginFile(Path.Combine(folder,"origin-previous.bin")));
            if(captured.Hash!=(string)proof["originHash"]||!JToken.DeepEquals(captured.Json(),proof["origin"]))throw Invalid("Recovery selection evidence is missing or changed. Originals are preserved.");return proof;
        }
        internal PreparedWorkspaceGeneration InspectDamagedPreview(string id,string hash,string originHash,CancellationToken cancellation=default)
        {
            using var lease=Lease(initialize:false);var proof=ReadDamageProof(id);if((string)proof["originHash"]!=originHash||(string)proof["manifestHash"]!=hash)throw Invalid("Recovery preview belongs to a different inspected choice.");
            if(File.Exists(Path.Combine(GenerationPath(id),DamageCommit))||Directory.Exists(Path.Combine(GenerationPath(id),DamageCommit)))throw Invalid("This recovery preview is already reserved or unavailable. Inspect its recorded outcome.");
            ExpectedOrigin(ObserveOrigin(),originHash);return new PreparedWorkspaceGeneration(id,Verify(id,hash,cancellation));
        }
        internal string RecoveryEvidenceDirectory(string id,string hash,string originHash)
        {
            var proof=ReadDamageProof(id);if((string)proof["manifestHash"]!=hash||(string)proof["originHash"]!=originHash)throw Invalid("Recovery evidence belongs to a different preview.");
            string path=Path.Combine(GenerationPath(id),"preservation");if(File.Exists(path))throw Invalid("Recovery preservation directory is unavailable.");if(Directory.Exists(path))WorkspaceArchive.NoLink(path);return path;
        }
        static (long Bytes,string Hash) DigestFile(string path,long maximum,CancellationToken token)
        {
            WorkspaceArchive.NoLink(path);using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);long expected=input.Length;if(expected<1||expected>maximum)throw Invalid("Recovery evidence exceeds its limit.");
            using var hash=SHA256.Create();var buffer=new byte[65536];long total=0;int count;
            while((count=input.Read(buffer,0,buffer.Length))!=0){token.ThrowIfCancellationRequested();total+=count;if(total>expected)throw Invalid("Recovery evidence grew while reading.");hash.TransformBlock(buffer,0,count,null,0);}
            token.ThrowIfCancellationRequested();if(total!=expected)throw Invalid("Recovery evidence changed while reading.");hash.TransformFinalBlock(Array.Empty<byte>(),0,0);return(total,BitConverter.ToString(hash.Hash).Replace("-","").ToLowerInvariant());
        }
        JObject EvidenceIdentity(string id,string hash,string originHash,CapturedRecoveryEvidence evidence,CancellationToken token)
        {
            if(evidence==null||!ModelLibrary.ValidHash(evidence.Sha256)||!ModelLibrary.ValidHash(evidence.AcceptedHash))throw Invalid("Preserve current accepted data before committing recovery.");
            string directory=RecoveryEvidenceDirectory(id,hash,originHash),path=Path.GetFullPath(evidence.Path);
            if(!string.Equals(Path.GetDirectoryName(path),directory,Path.DirectorySeparatorChar=='\\'?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))throw Invalid("Recovery evidence is outside this preview's private preservation directory.");
            string name=Path.GetFileName(path);if(!name.StartsWith("maestro-recovery-",StringComparison.Ordinal)||!name.EndsWith(".zip",StringComparison.Ordinal)||!Id(name.Substring(17,name.Length-21)))throw Invalid("Unexpected recovery evidence identity.");
            WorkspaceArchive.NoLink(directory);var digest=DigestFile(path,WorkspaceRecoveryEvidence.MaximumBytes+8L*1024*1024,token);if(digest.Hash!=evidence.Sha256)throw Invalid("Recovery evidence changed after capture.");
            return new JObject {["file"]=name,["bytes"]=digest.Bytes,["sha256"]=digest.Hash,["acceptedHash"]=evidence.AcceptedHash};
        }
        JObject ReadDamageCommit(string id,JObject proof)
        {
            var record=Read(Path.Combine(GenerationPath(id),DamageCommit),16384);
            if(!Exact(record,"version","originHash","manifestHash","evidence","next")||record["version"]?.Type!=JTokenType.Integer||(int)record["version"]!=1||record["originHash"]?.Type!=JTokenType.String||(string)record["originHash"]!=(string)proof["originHash"]||record["manifestHash"]?.Type!=JTokenType.String||(string)record["manifestHash"]!=(string)proof["manifestHash"]||record["evidence"] is not JObject evidence||!Exact(evidence,"file","bytes","sha256","acceptedHash")||evidence["file"]?.Type!=JTokenType.String||evidence["bytes"]?.Type!=JTokenType.Integer||(long)evidence["bytes"]<1||(long)evidence["bytes"]>WorkspaceRecoveryEvidence.MaximumBytes+8L*1024*1024||evidence["sha256"]?.Type!=JTokenType.String||!ModelLibrary.ValidHash((string)evidence["sha256"])||evidence["acceptedHash"]?.Type!=JTokenType.String||!ModelLibrary.ValidHash((string)evidence["acceptedHash"]))throw Invalid("Invalid damaged-workspace commit identity.");
            string name=(string)evidence["file"];if(name.Length!=53||!name.StartsWith("maestro-recovery-",StringComparison.Ordinal)||!name.EndsWith(".zip",StringComparison.Ordinal)||!Id(name.Substring(17,32)))throw Invalid("Invalid preserved recovery evidence path.");
            var next=Selection(record["next"] as JObject);if(next.Active.Generation!=id||!next.Active.ReviewRequired||next.Previous!=null||next.Revision==Initial)throw Invalid("Invalid damaged-workspace selection boundary.");return record;
        }
        // The coordinator must retain its preservation hold throughout this call. After releasing
        // ownership it may only reconcile; it must never retry an old evidence/preview pair.
        internal WorkspaceSelection CommitDamagedRecovery(string id,string hash,string originHash,CapturedRecoveryEvidence evidence,CancellationToken cancellation=default)
        {
            using var lease=Lease(initialize:false);cancellation.ThrowIfCancellationRequested();var proof=ReadDamageProof(id);
            if((string)proof["manifestHash"]!=hash||(string)proof["originHash"]!=originHash)throw Invalid("Recovery commit belongs to a different preview.");
            var preserved=EvidenceIdentity(id,hash,originHash,evidence,cancellation);string path=Path.Combine(GenerationPath(id),DamageCommit);WorkspaceSelection next;
            if(File.Exists(path)){
                var record=ReadDamageCommit(id,proof);if(!JToken.DeepEquals(record["evidence"],preserved))throw Invalid("Recovery is reserved with different preserved evidence.");next=Selection(record["next"] as JObject);
                try{var current=Load();if(JToken.DeepEquals(current.Json(),next.Json()))return current;}catch(Exception){/* Damaged origin is expected; match its exact raw bytes below. */}
                ExpectedOrigin(ObserveOrigin(),originHash);Verify(id,hash,cancellation);
            }else {
                ExpectedOrigin(ObserveOrigin(),originHash);Verify(id,hash,cancellation);
                next=new WorkspaceSelection(Guid.NewGuid().ToString("N"),new WorkspaceLocation(id,Guid.NewGuid().ToString("N"),true),null);
                WriteNew(path,Json(new JObject {["version"]=1,["originHash"]=originHash,["manifestHash"]=hash,["evidence"]=preserved,["next"]=next.Json()}));fault?.Invoke("damage.reserved");
            }
            cancellation.ThrowIfCancellationRequested();ExpectedOrigin(ObserveOrigin(),originHash);Commit(next);return next;
        }
        // No fallback, new selection, worker dispatch or retry. A missing/damaged current pointer
        // after an uncertain attempt remains an error instead of pretending that no commit happened.
        internal WorkspaceSelection CommittedDamagedRecovery(string id,string hash,string originHash)
        {
            using var lease=Lease(initialize:false);var proof=ReadDamageProof(id);if((string)proof["manifestHash"]!=hash||(string)proof["originHash"]!=originHash)throw Invalid("Recovery outcome belongs to a different preview.");
            string path=Path.Combine(GenerationPath(id),DamageCommit);if(Directory.Exists(path))throw Invalid("Recovery outcome record is unavailable.");if(!File.Exists(path)){ExpectedOrigin(ObserveOrigin(),originHash);return null;}var record=ReadDamageCommit(id,proof);
            var observed=ObserveOrigin();if(observed.Hash==originHash)return null;
            var current=Load();var next=Selection(record["next"] as JObject);return JToken.DeepEquals(current.Json(),next.Json())?current:null;
        }
        internal void DiscardDamagedPreview(string id,string hash)
        {
            using var lease=Lease(initialize:false);var proof=ReadDamageProof(id);if((string)proof["manifestHash"]!=hash)throw Invalid("Recovery preview identity changed.");
            string folder=GenerationPath(id);if(File.Exists(Path.Combine(folder,DamageCommit))||Directory.Exists(Path.Combine(folder,DamageCommit)))throw Invalid("Reserved recovery evidence cannot be discarded as an unused preview.");
            WorkspaceSelection current=null;try{current=Load();}catch(Exception){ExpectedOrigin(ObserveOrigin(),(string)proof["originHash"]);}
            if(current?.Active.Generation==id||current?.Previous?.Generation==id||Reserved(id,except:id))throw Invalid("A selected or protected recovery generation cannot be discarded.");
            // Even evidence from a cancelled/failed attempt remains available for explicit maintenance.
            if(Directory.Exists(Path.Combine(folder,"preservation"))&&Directory.EnumerateFileSystemEntries(Path.Combine(folder,"preservation")).Any())throw Invalid("This preview contains preserved recovery evidence. Keep it for explicit maintenance.");
            DeleteOwned(folder);
        }
        bool DamageReferences(string folder,string id)
        {
            string metadata=Path.Combine(folder,"generation.v1.json");bool marked=File.Exists(metadata)&&(bool?)Read(metadata)["damagedRecovery"]==true;
            if(!marked&&!File.Exists(Path.Combine(folder,DamageProof))&&!File.Exists(Path.Combine(folder,DamageCommit)))return false;
            var proof=ReadDamageProof(Path.GetFileName(folder));return Path.GetFileName(folder)==id||((JArray)proof["protectedGenerations"]).Any(x=>(string)x==id);
        }
    }
}
