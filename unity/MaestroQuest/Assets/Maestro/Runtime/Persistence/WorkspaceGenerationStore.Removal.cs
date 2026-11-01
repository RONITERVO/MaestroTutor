// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Persistence
{
    internal sealed partial class WorkspaceGenerationStore
    {
        internal sealed class RemovalPreview
        {
            internal string Id,OriginHash,ContextHash,Fingerprint,Reason;
            internal bool Eligible;internal JArray Nodes;internal long Bytes;internal int Files;
            internal JObject View(string previewId)=>new() {["previewId"]=previewId,["generationId"]=Id,["fingerprint"]=Fingerprint??"",["eligible"]=Eligible,["reason"]=Reason??"",["files"]=Files,["sizeMiB"]=Bytes/(1024d*1024)};
        }
        internal const long MaximumRemovalBytes=2L*1024*1024*1024;
        internal const int MaximumRemovalEntries=16384;
        sealed class RemovalContext
        {
            internal string Hash,OriginHash;internal readonly Dictionary<string,string> Protected=new(StringComparer.Ordinal);
        }
        // Historical generation reservations are retained evidence, not perpetual references to
        // live content. Selection pointers, live owners and tracked operations are dependencies.
        // Uncertain/changed history never permits disposal. No tracking record is cleared here.
        RemovalContext RemovalDependencies(JObject accepted,JObject live,CancellationToken token)
        {
            WorkspaceFileInventory.Parents(root);var origin=ObserveOrigin();var selected=origin.Current==null?Load():Selection(Read(origin.Current));var result=new RemovalContext {OriginHash=origin.Hash};
            void Protect(string id,string why){if(id!=null&&Id(id))result.Protected.TryAdd(id,why);}
            void SelectionRoots(WorkspaceSelection value,string why){if(value==null)return;Protect(value.Active.Generation,why);Protect(value.Previous?.Generation,why);}
            SelectionRoots(selected,"This is a current or previous workspace.");
            if(origin.Previous!=null)SelectionRoots(Selection(Read(origin.Previous)),"The selection backup still needs this workspace.");
            if(live!=null)SelectionRoots(Selection(live),"Live workspace owners still reference this generation.");
            var identities=new JObject {["origin"]=origin.Hash,["live"]=live?.DeepClone()??JValue.CreateNull()};
            foreach(string key in new[]{"activation","review","recovery"}){
                token.ThrowIfCancellationRequested();string folder=Path.Combine(appRoot,"workspace-"+key+".v1"),file=Path.Combine(folder,"latest.json");WorkspaceFileInventory.Parents(folder);
                JObject stored=null;string kind=WorkspaceFileInventory.Kind(file);if(kind=="directory")throw Invalid("Workspace operation history is unavailable.");
                if(kind=="file"){
                    byte[] bytes=Bytes(file,key=="recovery"?65536:8192);stored=Read(bytes);
                    bool valid=key=="activation"?WorkspaceActivation.ValidRecord(stored):key=="review"?WorkspaceReview.Valid(stored):WorkspaceRecovery.Valid(stored);
                    if(!valid)throw Invalid("Workspace operation history is unavailable.");identities[key]=WorkspaceFileInventory.Hash(bytes);
                }else identities[key]="";
                if(accepted==null||!accepted.ContainsKey(key)||!JToken.DeepEquals(stored??(JToken)JValue.CreateNull(),accepted[key]))throw Invalid("Workspace operation history changed. Inspect removal again.");
                if(stored==null)continue;string phase=(string)stored["phase"];
                if(phase is "inspecting" or "preparing" or "preserving" or "activating" or "completing" or "committed" or "cancelling" or "unavailable"||key=="recovery"&&phase=="prepared")throw Invalid("Finish or recover the pending workspace operation before disposal.");
                foreach(string field in key=="activation"?new[]{"generationId","retainedId"}:key=="review"?new[]{"generationId"}:new[]{"previewId","sourceId"})Protect((string)stored[field],"Tracked "+key+" history still references this workspace.");
            }
            result.Hash=WorkspaceFileInventory.Hash(Encoding.UTF8.GetBytes(identities.ToString(Formatting.None)));return result;
        }
        internal RemovalPreview PreviewRemoval(string originHash,string id,JObject accepted,JObject live,CancellationToken token=default)
        {
            // Read-only preview must not initialize selection or consume a generation slot.
            var context=RemovalDependencies(accepted,live,token);if(context.OriginHash!=originHash)throw Invalid("Workspace selection changed. Inspect retained workspaces again.");
            string source=GenerationPath(id);WorkspaceFileInventory.Parents(source);if(WorkspaceFileInventory.Kind(source)!="directory")throw Invalid("This generation no longer exists.");
            var preview=new RemovalPreview {Id=id,OriginHash=originHash,ContextHash=context.Hash};
            if(context.Protected.TryGetValue(id,out var reason)){preview.Reason=reason;return preview;}
            preview.Nodes=new JArray();WorkspaceFileInventory.Scan(source,"",preview.Nodes,ref preview.Files,ref preview.Bytes,MaximumRemovalBytes,MaximumRemovalEntries,true,token,maxDepth:12);
            var after=RemovalDependencies(accepted,live,token);if(after.Hash!=context.Hash)throw Invalid("Workspace dependencies changed during inspection.");
            preview.Fingerprint=WorkspaceFileInventory.Hash(Encoding.UTF8.GetBytes(new JObject {["generationId"]=id,["context"]=context.Hash,["entries"]=preview.Nodes}.ToString(Formatting.None)));
            preview.Eligible=true;preview.Reason="Permanently discards this generation, including its private recovery evidence and action history. No Undo.";return preview;
        }
        internal void RemoveRetained(RemovalPreview expected,JObject accepted,JObject live,CancellationToken token=default)
        {
            if(expected==null||!expected.Eligible)throw Invalid("Review an eligible generation before permanently discarding it.");
            WorkspaceFileInventory.Parents(root);using var lease=Lease(initialize:false);
            void Same(){var current=PreviewRemoval(expected.OriginHash,expected.Id,accepted,live,token);if(!current.Eligible||current.Fingerprint!=expected.Fingerprint)throw Invalid("Files or dependencies changed. Inspect removal again before confirming.");}
            Same();fault?.Invoke("retention.beforeRemove");Same();token.ThrowIfCancellationRequested();
            // After the first deletion Stop cannot roll back disposal. Nonrecursive directory
            // deletion refuses new/uninspected children; a fault leaves a visible partial generation
            // that requires a new preview/confirmation. Native receipts prevent automatic replay.
            string source=GenerationPath(expected.Id);
            foreach(JObject node in expected.Nodes.OrderByDescending(n=>((string)n["path"]).Length)){
                string path=Path.GetFullPath(Path.Combine(source,((string)node["path"]).Replace('/',Path.DirectorySeparatorChar)));
                if(path!=source&&!path.StartsWith(source+Path.DirectorySeparatorChar,StringComparison.Ordinal))throw Invalid("Unsafe generation disposal path.");
                WorkspaceFileInventory.Parents(Path.GetDirectoryName(path));if(WorkspaceFileInventory.Kind(path)!=(string)node["kind"])throw new IOException("Generation changed during disposal.");
                if((string)node["kind"]=="file"){
                    var actual=new JArray();int files=0;long bytes=0;WorkspaceFileInventory.Scan(path,(string)node["path"],actual,ref files,ref bytes,MaximumRemovalBytes,1,true,CancellationToken.None);
                    if(!JToken.DeepEquals(actual[0],node))throw new IOException("File changed during disposal.");File.Delete(path);
                }else Directory.Delete(path,false);
                if(path!=source)fault?.Invoke("retention.removing");
            }
            try{fault?.Invoke("retention.removed");}catch(Exception){}
        }
    }
}
