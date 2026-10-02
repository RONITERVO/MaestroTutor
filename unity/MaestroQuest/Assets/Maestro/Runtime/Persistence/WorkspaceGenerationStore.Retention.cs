// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Maestro.Quest.Imports;
using Maestro.Quest.Creation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Persistence
{
    internal sealed partial class WorkspaceGenerationStore
    {
        // Metadata inspection is intentionally distinct from content verification. Neither this
        // list nor a portable export proves that all private recovery material can be removed.
        internal JObject InspectRetention(CancellationToken token=default)
        {
            WorkspaceFileInventory.Parents(root);var origin=ObserveOrigin();WorkspaceSelection selected=null,backup=null;
            try{selected=Load();}catch(Exception){}
            bool backupKnown=origin.Previous==null;
            if(origin.Previous!=null){try{backup=Selection(Read(pointer+".previous"));backupKnown=true;}catch(Exception){}}
            var entries=new JArray();
            foreach(string id in GenerationIds()){
                token.ThrowIfCancellationRequested();string hash="",reservation="unknown";bool available=false;
                try{hash=(string)Metadata(id)["manifestHash"];available=true;}catch(Exception){}
                try{reservation=Reserved(id)?"reserved":"unreserved";}catch(Exception){}
                string role=selected==null?"unknown":selected.Active.Generation==id?"active":selected.Previous?.Generation==id?"previous":!backupKnown?"unknown":backup?.Active.Generation==id||backup?.Previous?.Generation==id?"pointer-backup":"inactive";
                entries.Add(new JObject {["generationId"]=id,["originalManifestHash"]=hash,["metadataAvailable"]=available,["role"]=role,["reservation"]=reservation});
            }
            ExpectedOrigin(ObserveOrigin(),origin.Hash);
            return new JObject {["originHash"]=origin.Hash,["selectionReadable"]=selected!=null,["entries"]=entries};
        }
        internal sealed class RetainedExport:IDisposable
        {
            internal string Path,ArchiveHash,SourceFingerprint;internal int ExcludedFiles;internal long Bytes;internal WorkspaceArchiveReceipt Receipt;
            public void Dispose(){try{File.Delete(Path);}catch(IOException){}catch(UnauthorizedAccessException){}}
        }
        static JArray RetainedInventory(string data,CancellationToken token)
        {
            WorkspaceFileInventory.Parents(data);if(WorkspaceFileInventory.Kind(data)!="directory")throw Invalid("Retained content is unavailable.");
            var nodes=new JArray();int files=0;long bytes=0;
            WorkspaceFileInventory.Scan(data,"",nodes,ref files,ref bytes,MaximumRetainedBytes,4096,true,token,maxDepth:8);return nodes;
        }
        const long MaximumRetainedBytes=WorkspaceArchive.MaximumArchiveBytes+32L*1024*1024;
        static string InventoryHash(JArray nodes)=>WorkspaceFileInventory.Hash(Encoding.UTF8.GetBytes(nodes.ToString(Formatting.None)));
        // Inactive data has no live writers under the host's path lease. Read its actual saved
        // documents, not its possibly obsolete original manifest. Never recover/default a bad file.
        internal RetainedExport ExportRetained(string originHash,string id,string originalHash,string liveGeneration,string cache,CancellationToken token=default)
        {
            WorkspaceFileInventory.Parents(root);using var lease=Lease(initialize:false);token.ThrowIfCancellationRequested();ExpectedOrigin(ObserveOrigin(),originHash);var current=Load();
            if(current.Active.Generation==id||liveGeneration==id)throw Invalid("Export the live workspace through its accepted owners instead.");
            if(!ModelLibrary.ValidHash(originalHash)||(string)Metadata(id)["manifestHash"]!=originalHash)throw Invalid("Retained workspace identity changed. Inspect again.");
            string data=Path.Combine(GenerationPath(id),"data");var inventory=RetainedInventory(data,token);string fingerprint=InventoryHash(inventory);
            var files=inventory.OfType<JObject>().Where(n=>(string)n["kind"]=="file").ToDictionary(n=>(string)n["path"],StringComparer.Ordinal);
            var documents=new Dictionary<string,byte[]>(StringComparer.Ordinal);var assets=new Dictionary<string,Func<Stream>>(StringComparer.Ordinal);
            byte[] ReadDocument(string name){if(!files.ContainsKey(name))throw Invalid("A required retained document is missing.");var bytes=WorkspaceLibraryCapture.ReadDocument(Path.Combine(data,name.Replace('/',Path.DirectorySeparatorChar)),WorkspaceArchiveMetadata.Limit(name));if(WorkspaceFileInventory.Hash(bytes)!=(string)files[name]["sha256"])throw Invalid("Retained document changed during capture.");return bytes;}
            foreach(string name in WorkspaceArchiveMetadata.Required){
                string source=Path.Combine(data,name.Replace('/',Path.DirectorySeparatorChar)),file=Path.GetFileName(source),stem=file.Substring(0,file.LastIndexOf(".v",StringComparison.Ordinal));
                if(VersionedRoomFile<RoomDocument>.HasNewerFiles(Path.GetDirectoryName(source),stem,2))throw Invalid("Retained content needs a newer app version; original files remain preserved.");
                documents.Add(name,ReadDocument(name));
            }
            // Optional memory is validated even when absent, so unknown formats or
            // backup-only evidence cannot silently disappear from portable exports.
            _=Programs.ProgramMemoryStore.ReadSaved(data);
            var motionNames=MotionLibrary.DecodeSnapshot(documents["motions/motions.v2.json"]).entries.Where(x=>!x.removed).Select(x=>"motions/"+x.hash+".motion.glb").ToHashSet(StringComparer.Ordinal);
            foreach(string name in files.Keys){
                token.ThrowIfCancellationRequested();if(documents.ContainsKey(name))continue;
                try{_=WorkspaceArchiveMetadata.Limit(name);}catch(InvalidDataException){continue;}
                if(name.StartsWith("motions/",StringComparison.Ordinal)&&!motionNames.Contains(name))continue;
                if(name.StartsWith("models/",StringComparison.Ordinal)&&name.EndsWith(".txt",StringComparison.Ordinal)&&!files.ContainsKey(name.Substring(0,name.Length-4)+".glb"))continue;
                if(WorkspaceArchiveMetadata.IsAsset(name)){string source=Path.Combine(data,name.Replace('/',Path.DirectorySeparatorChar));assets.Add(name,()=>{WorkspaceFileInventory.Parents(Path.GetDirectoryName(source));return WorkspaceLibraryCapture.Open(source);});}
                else documents.Add(name,ReadDocument(name));
            }
            var snapshot=new WorkspaceArchiveSnapshot(documents,assets);
            cache=Path.GetFullPath(cache);var comparison=Path.DirectorySeparatorChar=='\\'?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
            if(cache.Equals(appRoot,comparison)||cache.Equals(root,comparison)||cache.StartsWith(root+Path.DirectorySeparatorChar,comparison)||cache.Equals(Path.Combine(appRoot,"room"),comparison)||cache.StartsWith(Path.Combine(appRoot,"room")+Path.DirectorySeparatorChar,comparison))throw Invalid("Use a separate private export cache.");
            WorkspaceFileInventory.Parents(cache);Directory.CreateDirectory(cache);WorkspaceFileInventory.Parents(cache);
            var result=new RetainedExport {Path=Path.Combine(cache,"maestro-workspace-"+Guid.NewGuid().ToString("N")+".zip"),SourceFingerprint=fingerprint,ExcludedFiles=files.Count-documents.Count-assets.Count};
            try {
                using(var output=new FileStream(result.Path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){result.Receipt=WorkspaceArchive.Write(output,snapshot,token);output.Flush(true);}
                fault?.Invoke("retention.captured");token.ThrowIfCancellationRequested();
                if(InventoryHash(RetainedInventory(data,token))!=fingerprint)throw Invalid("Retained workspace changed while exporting. Inspect again.");
                ExpectedOrigin(ObserveOrigin(),originHash);if((string)Metadata(id)["manifestHash"]!=originalHash)throw Invalid("Retained workspace identity changed while exporting.");
                var digest=DigestFile(result.Path,WorkspaceArchive.MaximumArchiveBytes,token);result.ArchiveHash=digest.Hash;result.Bytes=digest.Bytes;return result;
            }catch{result.Dispose();throw;}
        }
    }
}
