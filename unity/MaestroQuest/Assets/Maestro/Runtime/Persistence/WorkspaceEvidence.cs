// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Persistence
{
    internal sealed class WorkspaceEvidence
    {
        readonly WorkspaceHost host;readonly WorkspaceEvidenceStore store;Job latest;WorkspaceEvidenceStore.Entry[] entries;
        string inspectionId="";bool disposed,paused,focused=true;internal Action<string> Fault;
        internal sealed class Job
        {
            internal readonly string Id;internal Job(string id){Id=id??Guid.NewGuid().ToString("N");}internal string Mode,Phase,Error;internal bool Pending;
            internal WorkspaceEvidenceStore.Entry Source;
            internal JObject Result;internal Task<Outcome> Worker;internal CancellationTokenSource Cancellation;
        }
        internal sealed class Outcome {internal JObject Result;internal WorkspaceEvidenceStore.Entry[] Entries;internal string Error;internal bool Cancelled;}
        internal WorkspaceEvidence(WorkspaceHost host,string applicationData){this.host=host;store=new WorkspaceEvidenceStore(applicationData);store.Fault=p=>Fault?.Invoke(p);}
        internal bool Busy=>latest?.Pending==true;
        internal bool Ready(out string error)
        {
            error=null;if(disposed||paused||!focused||!host||!host.isActiveAndEnabled||!host.Ready||host.Retiring){error="Resume Maestro and wait for previous owners to finish.";return false;}
            if(host.Retention?.Busy==true||Busy||host.Switching||host.Activation.Busy||host.Review.Busy||host.Recovery.BlocksOtherOperations||host.History.Busy||host.Import.Occupied||host.Export.Busy){error="Finish or cancel the current workspace operation first.";return false;}return true;
        }
        WorkspaceEvidenceStore.Entry Find(JObject args)=>entries!=null&&(string)args["inspectionId"]==inspectionId?entries.FirstOrDefault(e=>e.Id==(string)args["evidenceId"]&&e.Fingerprint==(string)args["fingerprint"]):null;
        internal bool CanStart(string mode,JObject args,out string error)
        {
            if(!Ready(out error))return false;if(mode=="inspect")return true;
            if(Find(args)==null){error="Inspect this exact evidence entry before exporting or removing it.";return false;}
            if(mode=="export")return host.Export.TryPublisher(out _,out _,out error);
            if(mode!="remove"){error="Unsupported evidence operation.";return false;}
            var receipts=host.Runtime.Scheduler.Receipts;var proof=receipts.Error==null?receipts.Find((string)args["exportRunId"]):null;
            if((string)proof?["phase"]!="completed"||(string)proof?["capability"]!="workspace.evidence.export"||(string)proof?["output"]?["evidenceId"]!=(string)args["evidenceId"]||(string)proof?["output"]?["fingerprint"]!=(string)args["fingerprint"]){error="A retained completed export receipt for these exact bytes is required. Export again if its receipt expired.";return false;}
            return true;
        }
        internal Job Start(string mode,JObject args,string runId=null)
        {
            if(!CanStart(mode,args,out var error))throw new InvalidOperationException(error);var source=Find(args);string cache=null;Func<string,string> publish=null;
            if(mode=="export"&&!host.Export.TryPublisher(out cache,out publish,out error))throw new InvalidOperationException(error);
            var job=new Job(runId) {Source=source,Mode=mode,Phase=mode=="inspect"?"inspecting":mode=="export"?"exporting":"removing",Pending=true,Cancellation=new CancellationTokenSource()};latest=job;
            if(mode=="inspect"){entries=null;inspectionId="";}var token=job.Cancellation.Token;
            job.Worker=Task.Run(()=>{
                try {
                    Fault?.Invoke("evidence.worker");token.ThrowIfCancellationRequested();
                    if(mode=="inspect"){
                        var found=store.List(token);return new Outcome {Entries=found,Result=new JObject {["inspectionId"]=job.Id,["count"]=found.Length,["sizeKiB"]=(found.Sum(e=>e.Bytes)+1023)/1024}};
                    }
                    if(mode=="export"){
                        using var bundle=store.Capture(source,cache,token);Fault?.Invoke("evidence.beforePublish");token.ThrowIfCancellationRequested();string location=publish(bundle.Path);
                        if(!WorkspaceExport.ValidLocation(location))throw new System.IO.IOException();
                        return new Outcome {Result=new JObject {["evidenceId"]=source.Id,["fingerprint"]=source.Fingerprint,["location"]=location,["archiveHash"]=bundle.Hash,["sizeKiB"]=bundle.Bytes/1024d}};
                    }
                    store.Remove(source,token);return new Outcome {Result=new JObject {["evidenceId"]=source.Id,["fingerprint"]=source.Fingerprint,["removed"]=true}};
                }catch(OperationCanceledException){return new Outcome {Cancelled=true,Error="Evidence operation cancelled before publication or removal. Inspect its current state before continuing."};}
                catch(Exception){return new Outcome {Error=mode=="remove"?"Removal may be partial. Check the exported copy and inspect remaining evidence before continuing.":"Evidence could not be inspected or exported. Check Downloads after uncertain publication before retrying."};}
            });return job;
        }
        internal void Poll()
        {
            var job=latest;if(disposed||job?.Pending!=true||!job.Worker.IsCompleted)return;var result=job.Worker.GetAwaiter().GetResult();job.Error=result.Error;job.Result=result.Result;
            job.Phase=result.Error!=null?result.Cancelled?"cancelled":"failed":job.Mode=="inspect"?"inspected":job.Mode=="export"?"exported":"removed";
            if(result.Entries!=null){entries=result.Entries;inspectionId=job.Id;}
            if(job.Mode=="remove"){entries=null;inspectionId="";} // Changed inventory or partial deletion requires inspection.
            job.Cancellation.Dispose();job.Cancellation=null;job.Pending=false;
        }
        internal JObject Entry(string request,int index)=>request==inspectionId&&entries!=null&&index>=0&&index<entries.Length?entries[index].View():null;
        internal JObject Status()=>new() {["requestId"]=latest?.Id??"",["phase"]=latest?.Phase??"idle",["status"]=latest?.Error??"Evidence operations never recover or approve workspace content.",["inspectionId"]=inspectionId,["evidenceId"]=(string)latest?.Result?["evidenceId"]??latest?.Source?.Id??"",["fingerprint"]=(string)latest?.Result?["fingerprint"]??latest?.Source?.Fingerprint??"",["location"]=(string)latest?.Result?["location"]??"",["archiveHash"]=(string)latest?.Result?["archiveHash"]??""};
        internal void Cancel(Job job){if(job?.Pending==true)job.Cancellation?.Cancel();}
        internal void Pause(bool value){paused=value;if(value)Cancel(latest);}
        internal void Focus(bool value){focused=value;if(!value)Cancel(latest);}
        internal void Disable()=>Cancel(latest);
        internal async Task Dispose(){disposed=true;Cancel(latest);if(latest?.Worker!=null)await latest.Worker;if(latest!=null){latest.Cancellation?.Dispose();latest.Cancellation=null;latest.Pending=false;}}
    }
}
