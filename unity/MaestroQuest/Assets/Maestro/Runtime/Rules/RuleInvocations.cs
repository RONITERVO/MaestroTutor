// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Rules
{
    public sealed partial class RuleScheduler
    {
        // One-off invocations share Run, ProgramMachine, native handlers and the
        // same terminal history. They never enter RuleDocument or its Undo stack.
        public bool Invoke(JObject call,float now,out string runId,out string error,string issuedId=null)
        {
            runId=null;error=null;LastError=null;
            if(!RoomCapabilityCatalog.ValidCall(call)||!BehaviourCatalog.TryCall((string)call["id"],(int)call["version"],(JObject)call["arguments"],out var step,out error))
            {error??="Invalid capability invocation";return false;}
            if(suspended||!float.IsFinite(now)) {error="Actions are paused";return false;}
            string source=BehaviourProgram.FromInvocation(call);
            if(!BehaviourProgram.TryParse(source,out var program,out error))return false;
            if(ActionBusy(step)) {error=step.RequiresQuietRoom?"Stop other room actions before this room-wide action":"A running action owns a required animation channel or object";return false;}
            if(!HasCapacity) {error="All action slots are currently in use";return false;}
            var sequence=new RuleSequence {id=Guid.NewGuid().ToString("N"),name="One-off action",interruption=RuleInterruption.Ignore,program=source};
            var identity=issuedId??Receipts?.NextId??Guid.NewGuid().ToString("N");
            if(Receipts!=null&&!Receipts.Reserve(identity,call,program.Resources.ToArray(),out error))return false;
            var run=new Run {Id=identity,Sequence=sequence,Targets=program.Resources.ToHashSet(),Claims=step.Claims,Invocation=(JObject)call.DeepClone(),Machine=new ProgramMachine(program,this)};
            runId=run.Id;running.Add(run);bool accepted=StartStep(run,now);
            if(!accepted)error=LastError??"Action failed";return accepted;
        }
        static JObject Summary(string id,JObject call,string[] resources,string phase,string status)=>new() {
            ["id"]=id,["capability"]=call["id"].DeepClone(),["version"]=call["version"].DeepClone(),["resources"]=new JArray(resources),["phase"]=phase,["status"]=status==null?"":status.Length<=2048?status:status.Substring(0,2048)
        };
        JObject Summary(Run run)=>Summary(run.Id,run.Invocation,run.Targets.ToArray(),run.Acquiring||run.Preparing?"preparing":"running",run.Acquiring?"Loading required objects":run.Preparing?run.Active?.AwaitCompletion==true?"Waiting for action completion":"Loading action animation":"Action running");
        static JObject Summary(FinishedRun run) {
            var summary=Summary(run.Outcome.id,run.Invocation,run.Resources,run.Outcome.phase,run.Outcome.status);
            if(run.Output!=null)summary["output"]=run.Output.DeepClone();return summary;
        }
        public JObject Invocation(string runId)
        {
            if(Receipts==null)return LiveInvocation(runId);
            var retained=Receipts.Find(runId);return retained==null?null:LiveInvocation(runId)??retained;
        }
        JObject LiveInvocation(string runId)=>LiveInvocation(runId,true);
        JObject LiveInvocation(string runId,bool includeCall)
        {
            var active=running.FirstOrDefault(x=>x.Id==runId&&x.Invocation!=null);
            if(active!=null) {var value=Summary(active);if(includeCall)value["call"]=active.Invocation.DeepClone();return value;}
            var done=outcomes.FirstOrDefault(x=>x.Outcome.id==runId&&x.Invocation!=null);
            if(done==null)return null;var result=Summary(done);if(includeCall)result["call"]=done.Invocation.DeepClone();return result;
        }
        public JObject ObserveInvocations(string selectedId)=>Receipts?.Observe(selectedId,LiveInvocation)??new JObject {
            ["selected"]=Invocation(selectedId)??(JToken)JValue.CreateNull(),
            ["running"]=new JArray(running.Where(x=>x.Invocation!=null).Select(Summary)),
            ["outcomes"]=new JArray(outcomes.Where(x=>x.Invocation!=null).Select(Summary))
        };
        public bool RecoverInvocations(string id,out string error)
        {
            error="Action history is unavailable";if(Receipts==null||!Receipts.CanRecover(id,out error))return false;
            // A duplicate recovery cannot stop or erase actions started afterwards.
            if(Receipts.Error==null)return Receipts.Recover(id,out error);
            // Validate the observed recovery token before stopping anything.
            foreach(var run in running.Where(x=>x.Invocation!=null).ToArray())Stop(run,false);
            if(!Receipts.Recover(id,out error))return false;
            var retained=outcomes.Where(x=>x.Invocation==null).ToArray();outcomes.Clear();foreach(var item in retained)outcomes.Enqueue(item);
            return true;
        }
        public bool CancelInvocation(string runId,out string error)
        {
            error=null;var active=running.FirstOrDefault(x=>x.Id==runId&&x.Invocation!=null);
            if(active!=null) {Stop(active,false);return true;}
            if(outcomes.Any(x=>x.Outcome.id==runId&&x.Invocation!=null)||Receipts?.Find(runId)!=null)return true;
            error="This action outcome is unknown or no longer retained";return false;
        }
    }
}
