// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Persistence;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    internal sealed class RoomStagingMaintenance
    {
        readonly RoomEditor editor;
        PreparedStageInventory inventory;
        Job latest;
        internal RoomStagingMaintenance(RoomEditor editor){this.editor=editor;}
        internal sealed class Outcome {internal PreparedStageInventory Inventory;internal JObject Result;internal string Error;}
        internal sealed class Job
        {
            internal readonly string Id,Mode;
            readonly object sync=new();
            readonly CancellationTokenSource cancellation=new();
            internal readonly CancellationToken Token;
            internal Task<Outcome> Worker;internal JObject Result;internal string Error;internal bool Finished;
            bool closed,cancelled;
            internal Job(string id,string mode){Id=id;Mode=mode;Token=cancellation.Token;}
            internal bool Cancelled {get{lock(sync)return cancelled;}}
            internal void Cancel(){lock(sync){cancelled=true;if(!closed)cancellation.Cancel();}}
            internal void Close(){lock(sync){closed=true;cancellation.Dispose();}}
        }
        internal bool CanStart(string mode,JObject args,out string error)
        {
            Poll();error="Resume the current workspace before inspecting saved preparations.";
            if(!editor||!editor.isActiveAndEnabled||editor.Ownership.Suspended)return false;
            if(latest!=null&&!latest.Finished){error="Wait for the current preparation maintenance to finish.";return false;}
            if(mode=="discard"){
                error="Inspect and explicitly confirm the exact eligible preparation first.";
                if(inventory==null||!inventory.Matches((string)args["inspectionId"],(string)args["entryId"],(string)args["fingerprint"],out _)||
                    (string)args["confirmation"]!="discard "+(string)args["entryId"])return false;
                if(!editor.WriteGate.CanFreeze(out error))return false;
            }
            error=null;return true;
        }
        internal bool Start(string mode,string runId,JObject args,out Job job,out string error)
        {
            job=null;if(!CanStart(mode,args,out error))return false;
            IDisposable held=null,reader=null;
            try {
                if(mode=="discard"){held=editor.WriteGate.TryFreeze(out error);if(held==null)return false;}
                // Freeze alone is not a retirement owner. This pure reader lease
                // keeps the directory owned until the worker actually drains.
                reader=editor.WriteGate.RetainRead();
                string primary=Path.Combine(editor.SaveDirectory,RoomStorage.FileName);
                var reviewed=inventory;var values=(JObject)args.DeepClone();
                inventory=null;job=latest=new Job(runId,mode);
                var captured=job;var freeze=held;var lifetime=reader;
                captured.Worker=Task.Run(()=>{
                    try {
                        captured.Token.ThrowIfCancellationRequested();
                        if(mode=="inspect"){
                            var found=PreparedStageInventory.Inspect(primary,captured.Token);
                            captured.Token.ThrowIfCancellationRequested();
                            return new Outcome{Inventory=found,Result=new JObject{["inspectionId"]=found.Id,["count"]=found.Entries.Length,["more"]=found.More}};
                        }
                        reviewed.Discard((string)values["inspectionId"],(string)values["entryId"],(string)values["fingerprint"],(string)values["confirmation"],captured.Token);
                        return new Outcome{Result=new JObject{["entryId"]=(string)values["entryId"],["fingerprint"]=(string)values["fingerprint"],["removed"]=true}};
                    } catch(OperationCanceledException){return new Outcome{Error="Preparation maintenance stopped. Inspect current saved preparations before another request."};}
                    catch(Exception){return new Outcome{Error="Preparation maintenance could not be confirmed. Its remaining files are preserved; inspect again before another request."};}
                    finally {try{freeze?.Dispose();}finally{lifetime.Dispose();captured.Close();}}
                });
                held=null;reader=null;return true;
            }catch(Exception){
                if(job?.Worker==null){job?.Close();latest=null;}
                error="Preparation maintenance could not start.";return false;
            }
            finally{held?.Dispose();reader?.Dispose();}
        }
        internal void Poll()
        {
            var job=latest;if(job==null||job.Finished||job.Worker==null||!job.Worker.IsCompleted)return;
            var outcome=job.Worker.GetAwaiter().GetResult();
            job.Error=outcome.Error;job.Result=outcome.Result;job.Finished=true;
            if(job.Mode=="inspect"&&job.Cancelled){job.Error="Preparation inspection was cancelled.";job.Result=null;}
            else if(outcome.Error==null&&outcome.Inventory!=null)inventory=outcome.Inventory;
        }
        internal void Cancel(Job job=null){
            if(job==null||ReferenceEquals(job,latest)&&job.Mode=="inspect")inventory=null;
            (job??latest)?.Cancel();
        }
        internal JObject Status()
        {
            Poll();return new JObject{["runId"]=latest?.Id??"",["phase"]=latest==null?"idle":!latest.Finished?"working":latest.Error!=null?"failed":"completed",
                ["inspectionId"]=inventory?.Id??"",["count"]=inventory?.Entries.Length??0,["more"]=inventory?.More??false,["error"]=latest?.Error??""};
        }
        internal JObject Entry(string inspectionId,int index)
        {
            Poll();if(inventory==null||inventory.Id!=inspectionId||index<0||index>=inventory.Entries.Length)return null;
            var entry=inventory.Entries[index];
            return new JObject{["entryId"]=entry.Id,["fingerprint"]=entry.Hash??"",["bytes"]=entry.Bytes,["eligible"]=entry.Eligible,["reason"]=entry.Reason};
        }
    }
    public sealed partial class RoomEditor
    {
        RoomStagingMaintenance stagingMaintenance;
        internal RoomStagingMaintenance StagingMaintenance=>stagingMaintenance??=new RoomStagingMaintenance(this);
        void CancelStagingMaintenance()=>stagingMaintenance?.Cancel();
    }
}
