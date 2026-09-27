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
        public bool Invoke(JObject call,float now,out string runId,out string error)
        {
            runId=null;error=null;LastError=null;
            if(!RoomCapabilityCatalog.ValidCall(call)||!BehaviourCatalog.TryInvocation((string)call["id"],(int)call["version"],(JObject)call["arguments"],out var step,out error))
            {error??="Invalid capability invocation";return false;}
            if(suspended||!float.IsFinite(now)) {error="Actions are paused";return false;}
            string source=BehaviourProgram.FromInvocation(call);
            if(!BehaviourProgram.TryParse(source,out var program,out error))return false;
            if(ActionBusy(step)) {error="A running action owns a required animation channel or object";return false;}
            if(!HasCapacity) {error="All action slots are currently in use";return false;}
            var sequence=new RuleSequence {id=Guid.NewGuid().ToString("N"),name="One-off action",interruption=RuleInterruption.Ignore,program=source};
            var run=new Run {Id=Guid.NewGuid().ToString("N"),Sequence=sequence,Targets=program.Resources.ToHashSet(),Claims=BehaviourCatalog.Claims(step),Invocation=(JObject)call.DeepClone(),Machine=new ProgramMachine(program,this)};
            runId=run.Id;running.Add(run);bool accepted=StartStep(run,now);
            if(!accepted)error=LastError??"Action failed";return accepted;
        }
        static JObject Summary(string id,JObject call,string[] resources,string phase,string status)=>new() {
            ["id"]=id,["capability"]=call["id"].DeepClone(),["version"]=call["version"].DeepClone(),["resources"]=new JArray(resources),["phase"]=phase,["status"]=status==null?"":status.Length<=2048?status:status.Substring(0,2048)
        };
        JObject Summary(Run run)=>Summary(run.Id,run.Invocation,run.Targets.ToArray(),run.Preparing?"preparing":"running",run.Preparing?"Loading action animation":"Action running");
        static JObject Summary(FinishedRun run)=>Summary(run.Outcome.id,run.Invocation,run.Resources,run.Outcome.phase,run.Outcome.status);
        public JObject Invocation(string runId)
        {
            var active=running.FirstOrDefault(x=>x.Id==runId&&x.Invocation!=null);
            if(active!=null) {var value=Summary(active);value["call"]=active.Invocation.DeepClone();return value;}
            var done=outcomes.FirstOrDefault(x=>x.Outcome.id==runId&&x.Invocation!=null);
            if(done==null)return null;var result=Summary(done);result["call"]=done.Invocation.DeepClone();return result;
        }
        public JObject ObserveInvocations(string selectedId)=>new() {
            ["selected"]=Invocation(selectedId)??(JToken)JValue.CreateNull(),
            ["running"]=new JArray(running.Where(x=>x.Invocation!=null).Select(Summary)),
            ["outcomes"]=new JArray(outcomes.Where(x=>x.Invocation!=null).Select(Summary))
        };
        public bool CancelInvocation(string runId,out string error)
        {
            error=null;var active=running.FirstOrDefault(x=>x.Id==runId&&x.Invocation!=null);
            if(active!=null) {Stop(active,false);return true;}
            if(outcomes.Any(x=>x.Outcome.id==runId&&x.Invocation!=null))return true;
            error="This action outcome is unknown or no longer retained";return false;
        }
    }
}
