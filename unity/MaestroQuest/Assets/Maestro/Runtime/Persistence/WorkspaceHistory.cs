// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Persistence
{
    // Owner-thread coordination; workers handle bounded I/O only. Native action receipts own replay.
    internal sealed class WorkspaceHistory
    {
        readonly WorkspaceHost host;readonly WorkspaceHistoryArchive archive;Job latest;
        bool disposed,paused,focused=true;
        internal Action<string> Fault;
        internal sealed class Job
        {
            internal readonly string Id=Guid.NewGuid().ToString("N");internal string Target,Phase,Error,EvidenceId="";
            internal WorkspaceHistoryArchive.Snapshot Snapshot;internal bool Pending;internal CancellationTokenSource Cancellation;
            internal Task<Outcome> Worker;
            internal JObject Result=>new() {["requestId"]=Id,["target"]=Target,["fingerprint"]=Snapshot.Fingerprint,["evidenceId"]=EvidenceId,["summary"]=new JObject {["files"]=Snapshot.Files,["sizeKiB"]=(int)((Snapshot.Bytes+1023)/1024)}};
        }
        internal sealed class Outcome {internal WorkspaceHistoryArchive.Snapshot Snapshot;internal string EvidenceId="",Error;internal bool Cancelled;}
        internal WorkspaceHistory(WorkspaceHost host,string applicationData){this.host=host;archive=new WorkspaceHistoryArchive(applicationData);archive.Fault=point=>Fault?.Invoke(point);}
        internal bool Busy=>latest?.Pending==true;
        bool Unavailable(string target)=>target switch {"activation"=>host.Activation.HistoryUnavailable,"review"=>host.Review.HistoryUnavailable,"recovery"=>host.Recovery.HistoryUnavailable,_=>false};
        byte[] Accepted(string target)=>WorkspaceHistoryArchive.Accepted(target switch {"activation"=>host.Activation.HistorySnapshot(),"review"=>host.Review.HistorySnapshot(),"recovery"=>host.Recovery.HistorySnapshot(),_=>throw new ArgumentException()});
        internal bool CanInspect(string target,out string error)
        {
            error=null;
            if(disposed||paused||!focused||!host||!host.isActiveAndEnabled||!host.Ready||host.Retiring){error="Resume Maestro and wait for previous owners to finish.";return false;}
            if(host.Evidence?.Busy==true||Busy||host.Switching||host.Activation.Busy||host.Review.Busy||host.Recovery.Busy||host.Import.Occupied||host.Export.Busy){error="Finish the current workspace operation first. Restart if its outcome remains uncertain.";return false;}
            if(!WorkspaceHistoryArchive.ValidTarget(target)||!Unavailable(target)){error="Only unavailable operation history can be repaired.";return false;}
            return true;
        }
        internal bool CanReset(string id,string fingerprint,out string error)
        {
            error="Inspect the latest unavailable history before resetting it.";
            if(latest==null||latest.Pending||latest.Phase!="inspected"||latest.Id!=id||latest.Snapshot.Fingerprint!=fingerprint)return false;
            return CanInspect(latest.Target,out error);
        }
        internal Job Inspect(string target)
        {
            if(!CanInspect(target,out var error))throw new InvalidOperationException(error);return Start(target,null);
        }
        internal Job Reset(string id,string fingerprint)
        {
            if(!CanReset(id,fingerprint,out var error))throw new InvalidOperationException(error);return Start(latest.Target,latest.Snapshot);
        }
        Job Start(string target,WorkspaceHistoryArchive.Snapshot snapshot)
        {
            byte[] accepted=Accepted(target);var job=new Job {Target=target,Phase=snapshot==null?"inspecting":"resetting",Pending=true,Cancellation=new CancellationTokenSource(),Snapshot=snapshot};
            latest=job;var token=job.Cancellation.Token;
            job.Worker=Task.Run(()=>{
                try {
                    Fault?.Invoke("history.worker");token.ThrowIfCancellationRequested();
                    if(snapshot==null)return new Outcome {Snapshot=archive.Inspect(target,accepted,token)};
                    return new Outcome {Snapshot=snapshot,EvidenceId=archive.Reset(snapshot,accepted,token)};
                }catch(OperationCanceledException){return new Outcome {Error="History repair cancelled before reset. Existing files remain preserved.",Cancelled=true};}
                catch(Exception){return new Outcome {Error="History could not be inspected or preserved. Original files remain; inspect again before retrying."};}
            });return job;
        }
        internal void Poll()
        {
            var job=latest;if(disposed||job?.Pending!=true||!job.Worker.IsCompleted)return;
            var result=job.Worker.GetAwaiter().GetResult();job.Cancellation.Dispose();job.Cancellation=null;job.Error=result.Error;
            if(result.Error!=null)job.Phase=result.Cancelled?"cancelled":"failed";
            else {
                job.Snapshot=result.Snapshot;job.EvidenceId=result.EvidenceId;
                if(result.EvidenceId!=""){
                    // Only tracking state is cleared. The selected workspace and review lease remain.
                    switch(job.Target){case "activation":host.Activation.ClearHistory();break;case "review":host.Review.ClearHistory();break;case "recovery":host.Recovery.ClearHistory();break;}
                    job.Phase="reset";
                }else job.Phase="inspected";
            }
            job.Pending=false;
        }
        internal void Cancel(Job job){if(job?.Pending==true)job.Cancellation?.Cancel();}
        internal JObject Status()
        {
            var job=latest;return new JObject {["requestId"]=job?.Id??"",["target"]=job?.Target??"",["phase"]=job?.Phase??"idle",["status"]=job?.Error??(job?.Phase=="reset"?"History preserved and tracking reset. Inspect current workspace before continuing.":"Inspect unavailable history before requesting its reset."),["fingerprint"]=job?.Snapshot?.Fingerprint??"",["evidenceId"]=job?.EvidenceId??"",["summary"]=new JObject {["files"]=job?.Snapshot?.Files??0,["sizeKiB"]=(int)(((job?.Snapshot?.Bytes??0)+1023)/1024)},["acceptedAvailable"]=job?.Snapshot!=null&&System.Text.Encoding.UTF8.GetString(job.Snapshot.Accepted)!="null"};
        }
        internal void Pause(bool value){paused=value;if(value)Cancel(latest);}
        internal void Focus(bool value){focused=value;if(!value)Cancel(latest);}
        internal void Disable()=>Cancel(latest);
        internal async Task Dispose(){disposed=true;Cancel(latest);if(latest?.Worker!=null)await latest.Worker;if(latest!=null){latest.Cancellation?.Dispose();latest.Cancellation=null;latest.Pending=false;}}
    }
}
