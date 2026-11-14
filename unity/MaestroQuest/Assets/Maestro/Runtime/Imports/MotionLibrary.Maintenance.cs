// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Maestro.Quest.Imports
{
    internal sealed class MotionMaintenanceRequest
    {
        public string operation,motionId,entryRevision,name;
        public string[] tags;
        public bool favourite;
        internal bool Destructive=>operation is "removeDownload" or "forgetMotion";
        internal static bool Maintenance(string operation)=>operation is "save" or "archive" or "restore" or "removeDownload" or "forgetMotion";
        internal bool Valid()=>Maintenance(operation)&&Guid.TryParseExact(motionId,"N",out _)&&ModelLibrary.ValidHash(entryRevision)&&
            (operation!="save"||name!=null&&!string.IsNullOrWhiteSpace(name)&&name.Length<=100&&!name.Any(char.IsControl)&&tags!=null&&tags.Length<=16&&tags.All(t=>t!=null&&!string.IsNullOrWhiteSpace(t)&&t.Length<=32&&!t.Any(char.IsControl))&&tags.Distinct(StringComparer.OrdinalIgnoreCase).Count()==tags.Length);
        internal MotionMaintenanceRequest Copy()=>new(){operation=operation,motionId=motionId,entryRevision=entryRevision,name=name,tags=tags?.ToArray(),favourite=favourite};
    }
    internal sealed class MotionMaintenanceResult
    {
        internal string Operation,MotionId,PreviousRevision,EntryRevision;
        internal MotionEntry Entry;
        internal bool Downloaded,PayloadPresent;
    }
    public sealed partial class MotionLibrary
    {
        // Content revision covers all saved entry fields (including origins), not
        // transient playback/payload presence. No catalogue migration or ID rewrite.
        public static string EntryRevision(MotionEntry entry)=>entry==null?"":ModelLibrary.Hash(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(entry,Formatting.None)));
        public string EntryRevision(string id){lock(gate)return EntryRevision(catalogue.entries.FirstOrDefault(e=>e.id==id));}
        internal bool Maintaining(string id){lock(gate)return removing.Contains(id);}
        internal Action<string> MaintenanceCheckpoint;

        internal async Task<MotionMaintenanceResult> EditAsync(MotionMaintenanceRequest request,Func<Task<string>> protection=null,CancellationToken cancellation=default)
        {
            if(request?.Valid()!=true)throw new ModelImportException("Inspect the motion and provide its current entry revision and valid details.");
            request=request.Copy();
            if(request.Destructive&&protection==null)throw new ArgumentNullException(nameof(protection));
            // Reference checks must stay authoritative through deletion: generic
            // program/module/memory writes can contain IDs without acquiring a clip.
            // Freeze authored writes rather than relying only on the hidden entry.
            using var write=request.Destructive?FreezeMaintenance():workspaceWrites.Write();
            using var lifetime=request.Destructive?workspaceWrites.RetainRead():null;
            // Resume on the calling Unity context for lease/retention checks. The
            // final worker owns only files and immutable data; it never touches Unity.
            await writes.WaitAsync(cancellation);
            bool hiding=false;
            try {
                cancellation.ThrowIfCancellationRequested();
                if(disposed)throw new ObjectDisposedException(nameof(MotionLibrary));
                if(readOnly)throw new ModelImportException(Notice??"The motion library is read-only.");
                MotionCatalogue candidate;lock(gate)candidate=catalogue.Copy();
                var entry=candidate.entries.FirstOrDefault(e=>e.id==request.motionId)??throw new ModelImportException("This motion is missing from the catalogue.");
                if(EntryRevision(entry)!=request.entryRevision)throw new ModelImportException("This motion changed. Inspect its current entry before editing it.");
                if(request.operation=="restore"&&entry.removed)throw new ModelImportException("Import its original export again to restore this motion and its existing identity.");
                if(request.Destructive){
                    if(request.operation=="forgetMotion"&&(!entry.removed||PayloadPresent(entry.id)))throw new ModelImportException("Remove the local download before forgetting its details.");
                    if(!entry.archived)throw new ModelImportException("Archive this motion before removing its download.");
                    if(Pinned(entry.id))throw new ModelImportException("Stop this motion and wait for loading to finish before removing its download.");
                    lock(gate)removing.Add(entry.id);hiding=true;
                }
                // The shared handler also checks lifecycle/storage immediately
                // before dispatch. Destructive work re-reads retained files here.
                if(protection!=null){string reason=await protection();if(reason!=null)throw new ModelImportException(reason);}
                cancellation.ThrowIfCancellationRequested();
                if(disposed)throw new ObjectDisposedException(nameof(MotionLibrary));
                if(readOnly)throw new ModelImportException(Notice??"The motion library is read-only.");
                if(request.Destructive&&Pinned(entry.id))throw new ModelImportException("Stop this motion before removing its download.");
                if(request.operation=="save"){
                    // Exact reviewed metadata survives a favourite-only edit.
                    // Validation rejects unsupported data; never silently trim,
                    // strip punctuation, deduplicate or rewrite existing labels.
                    entry.name=request.name;entry.tags=request.tags.ToArray();entry.favourite=request.favourite;
                }else if(request.operation is "archive" or "restore")entry.archived=request.operation=="archive";
                else if(request.operation=="removeDownload")entry.removed=true;
                else {
                    candidate.entries=candidate.entries.Where(e=>e.id!=entry.id).ToArray();
                    var needed=candidate.entries.SelectMany(e=>e.origins).Select(o=>o.sourceHash).ToHashSet();
                    candidate.sources=candidate.sources.Where(s=>needed.Contains(s.hash)).ToArray();
                }
                Validate(candidate);
                if(request.Destructive&&cache.TryGetValue(entry.id,out var cached))Remove(cached);
                MaintenanceCheckpoint?.Invoke("ready");cancellation.ThrowIfCancellationRequested();
                // After dispatch, Stop cannot roll back publication or deletion.
                // The write lease and hidden entry remain owned until it drains.
                return await Task.Run(()=>{
                    MaintenanceCheckpoint?.Invoke("publishing");
                    try{Save(candidate);}catch(Exception error){
                        readOnly=true;Notice="Motion metadata publication was not confirmed. The library is read-only; restart and inspect its saved state before another edit.";
                        throw new ModelImportException(Notice+" "+(error is ModelImportException?error.Message:"Existing files are preserved for recovery."));
                    }
                    lock(gate)catalogue=candidate;
                    MaintenanceCheckpoint?.Invoke("published");
                    if(request.operation=="removeDownload"){
                        try{string path=PayloadPath(entry.hash);if(System.IO.File.Exists(path))System.IO.File.Delete(path);}
                        catch{throw new ModelImportException("The removed-download state was saved, but local file removal was not confirmed. Inspect this motion before an explicit retry.");}
                    }
                    var accepted=request.operation=="forgetMotion"?null:entry.Copy();
                    bool present=accepted!=null&&System.IO.File.Exists(PayloadPath(accepted.hash));
                    return new MotionMaintenanceResult {Operation=request.operation,MotionId=entry.id,PreviousRevision=request.entryRevision,EntryRevision=EntryRevision(accepted),Entry=accepted,PayloadPresent=present,
                        Downloaded=accepted!=null&&!accepted.removed&&present};
                }).ConfigureAwait(false);
            }finally{if(hiding)lock(gate)removing.Remove(request.motionId);writes.Release();}
        }
        IDisposable FreezeMaintenance()=>workspaceWrites.TryFreeze(out var error)??throw new ModelImportException(error);
    }
}
