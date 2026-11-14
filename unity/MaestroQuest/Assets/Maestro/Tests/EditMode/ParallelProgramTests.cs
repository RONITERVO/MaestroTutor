// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
 public sealed class ParallelProgramTests
 {
  sealed class Actions:IRuleActions,IRuleReadiness,IRuleResults {
   public readonly Dictionary<string,CapabilityCall> Active=new();public readonly List<string> Starts=new();
   public readonly HashSet<string> Fail=new();public bool Preparing,ThrowOnStop;
   public bool CanRun(CapabilityCall call,out string error){error=null;return true;}
   public bool Start(string id,CapabilityCall call,out float seconds,out string error){Active[id]=call;Starts.Add((string)call.Arguments["target"]??call.Definition.Id);seconds=call.Instant?0:(float?)call.Arguments["seconds"]??1;error=null;return true;}
   public void Stop(string id,bool preserve){if(Active.Remove(id)&&ThrowOnStop)throw new InvalidOperationException("Stop failed");}
   public RuleActionState State(string id,out string error){error=Fail.Contains(id)?"Target disappeared":null;return error!=null?RuleActionState.Failed:Preparing?RuleActionState.Preparing:RuleActionState.Ready;}
   public JObject TakeResult(string id)=>new();
  }
  static JObject Source()=>JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-parallel.json")));
  static RuleSequence Sequence(JObject p)=>new(){id=Guid.NewGuid().ToString("N"),name="Parallel motion",program=p.ToString(Newtonsoft.Json.Formatting.None)};
  static RuleScheduler Scheduler(JObject p,Actions actions,out RuleSequence sequence){sequence=Sequence(p);var s=new RuleScheduler(actions);s.Configure(new RuleDocument{sequences=new[]{sequence}});return s;}
  static JArray Body(JObject p,int index=0)=>(JArray)p["functions"][index]["body"];
  static string Count(RuleScheduler s)=>s.ObserveRuns().Single(r=>string.IsNullOrEmpty(r.parentRunId)).state.Single(v=>v.name=="count").value;
  [Test] public void ParallelCallsWaitForAllAndMergeExplicitResultsWithoutLeakingBranchState(){
   var actions=new Actions();var s=Scheduler(Source(),actions,out var sequence);Assert.That(s.Trigger(sequence.id,0),Is.True);Assert.That(s.RunningCount,Is.EqualTo(3));Assert.That(actions.Starts,Is.Empty);
   s.Tick(.01f);Assert.That(actions.Starts,Is.EquivalentTo(new[]{"book","maestro"}));Assert.That(s.ObserveRuns().Count(r=>!string.IsNullOrEmpty(r.parentRunId)),Is.EqualTo(2));
   s.Tick(.5f);Assert.That(s.Outcomes,Is.Empty,"One completed branch must never report the behaviour completed");Assert.That(Count(s),Is.EqualTo("7"));Assert.That(s.RunningCount,Is.EqualTo(2));Assert.That(actions.Active.Count,Is.EqualTo(1));
   s.Tick(.9f);Assert.That(Count(s),Is.EqualTo("7"));s.Tick(1);Assert.That(Count(s),Is.EqualTo("17"));Assert.That(s.RunningCount,Is.EqualTo(1));Assert.That(s.Outcomes,Is.Empty,"Joined children are not terminal program outcomes");
   Assert.That(s.ObserveRuns().Single().locals.Select(v=>v.value),Is.EqualTo(new[]{"8","9"}));s.StopAll();Assert.That(actions.Active,Is.Empty);
  }
  [Test] public void ChildFailureCancelsSiblingBeforeItsNextSnapshotStepAndLeavesUnrelatedRunsAlone(){
   var actions=new Actions();var p=Source();var sequence=Sequence(p);var other=Sequence(JObject.Parse("{version:3,entry:'main',resources:[],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[{id:'sleep',op:'sleep',seconds:{value:30}}]}]}"));
   var s=new RuleScheduler(actions);s.Configure(new RuleDocument{sequences=new[]{sequence,other}});s.Trigger(other.id,0);s.Trigger(sequence.id,0);s.Tick(.01f);
   var failing=s.ObserveRuns().First(r=>!string.IsNullOrEmpty(r.parentRunId));actions.Fail.Add(failing.id);s.Tick(.1f);
   Assert.That(actions.Active,Is.Empty);Assert.That(s.ObserveRuns().Single().sequenceId,Is.EqualTo(other.id));Assert.That(s.Outcomes.Count(o=>o.sequenceId==sequence.id&&o.phase=="failed"),Is.EqualTo(1));Assert.That(s.Outcomes.Last().nodeId,Is.EqualTo("motion"));
   Assert.That(s.Outcomes.Last().status,Does.Contain("disappeared"));
  }
  [Test] public void ConflictStopPauseAndPreparationCancelTheWholeGroupWithoutRestart(){
   foreach(string mode in new[]{"conflict","target","sequence","pause"}){
    var p=Source();if(mode=="conflict")Body(p)[0]["branches"][1]["args"][0]["value"]="book";
    var a=new Actions{Preparing=mode=="pause"};var s=Scheduler(p,a,out var sequence);s.Trigger(sequence.id,0);s.Tick(.01f);
    if(mode=="target")s.StopTarget("book",true);if(mode=="sequence")s.StopSequence(sequence.id);if(mode=="pause"){s.Suspend(true);s.Suspend(false);}
    Assert.That(s.RunningCount,Is.Zero,mode);Assert.That(a.Active,Is.Empty,mode);int starts=a.Starts.Count;s.Tick(100);Assert.That(a.Starts.Count,Is.EqualTo(starts),mode);
   }
  }
  [Test] public void AllBranchSlotsAreReservedBeforeAnyBranchEffectAndNestedGroupsStayBounded(){
   var p=Source();var branches=(JArray)Body(p)[0]["branches"];branches.Add(branches[0].DeepClone());branches[2]["result"]="third";((JArray)p["functions"][0]["locals"]).Add(JObject.Parse("{name:'third',initial:0}"));
   var worker=(JObject)p["functions"][1].DeepClone();worker["name"]="inner";((JArray)p["functions"]).Add(worker);Body(p,1).Clear();
   Body(p,1).Add(JObject.Parse("{id:'nested',op:'parallel',branches:[{function:'inner',args:[{var:'target'},{var:'seconds'},{var:'amount'}]},{function:'inner',args:[{var:'target'},{var:'seconds'},{var:'amount'}]}]}"));Body(p,1).Add(JObject.Parse("{id:'done',op:'return',value:{value:0}}"));
   var a=new Actions();var s=Scheduler(p,a,out var sequence);s.Trigger(sequence.id,0);s.Tick(.01f);Assert.That(s.RunningCount,Is.Zero);Assert.That(a.Starts,Is.Empty);Assert.That(s.LastError,Does.Contain("slots"));
  }
  [Test] public void PureForkJoinCannotRenewInstructionBudgetAndActualTimersCan(){
   var p=Source();Body(p,1).RemoveAt(0);var parallel=Body(p)[0].DeepClone();Body(p).Clear();Body(p).Add(new JObject{["id"]="forever",["op"]="forever",["body"]=new JArray(parallel)});
   var a=new Actions();var s=Scheduler(p,a,out var sequence);s.Trigger(sequence.id,0);for(int i=0;i<20000&&s.RunningCount>0;i++)s.Tick(i*.01f);
   Assert.That(s.RunningCount,Is.Zero);Assert.That(s.LastError,Does.Contain("instruction budget"));
   Body(p,1).Insert(0,JObject.Parse("{id:'delay',op:'sleep',seconds:{value:0.1}}"));s=Scheduler(p,a,out sequence);s.Trigger(sequence.id,0);for(int i=1;i<30000;i++)s.Tick(i*.2f);
   Assert.That(s.RunningCount,Is.GreaterThan(0));Assert.That(s.LastError,Is.Null);s.StopAll();
  }
  [Test] public void ParallelValidationRejectsAmbiguousResultsRecursionBadTypesAndUnsupportedVersion(){
   Assert.That(BehaviourProgram.TryParse(Source().ToString(),out _,out var error),Is.True,error);
   var cases=new Action<JObject>[] {p=>p.Remove("parallelVersion"),p=>p["parallelVersion"]=2,p=>p["parallelVersion"]=1.5,p=>p["version"]=2,p=>((JArray)Body(p)[0]["branches"]).RemoveAt(0),p=>Body(p)[0]["branches"][1]["result"]="first",p=>Body(p)[0]["branches"][0]["function"]="main",p=>Body(p)[0]["branches"][0]["args"][0]=new JObject{["value"]=3},p=>Body(p)[0]["branches"][0]["extra"]=true};
   foreach(var change in cases){var p=Source();change(p);Assert.That(BehaviourProgram.TryParse(p.ToString(),out _,out _),Is.False,p.ToString());}
  }
  [Test] public void RetainedMemoryLimitAppliesToTheEntireGroupBeforeAnyEffects(){
   var p=Source();p["dataVersion"]=1;var text=new string('x',120);for(int i=0;i<15;i++)((JArray)p["state"]).Add(new JObject{["name"]="s"+i,["initial"]=text});
   var branches=(JArray)Body(p)[0]["branches"];branches.Add(JObject.Parse("{function:'move',args:[{value:'book'},{value:1},{value:1}]}"));branches.Add(branches.Last.DeepClone());
   var a=new Actions();var s=Scheduler(p,a,out var sequence);Assert.That(s.Trigger(sequence.id,0),Is.False);Assert.That(s.LastError,Does.Contain("retained-value"));Assert.That(a.Starts,Is.Empty);
  }

  [Test] public void ParallelEventWaitsUseIndependentSubscriptionsAndStopDropsQueuedDeliveries(){
   var p=Source();p["events"]=JArray.Parse("[{name:'user.go',type:'text'}]");p["functions"][1]["locals"]=JArray.Parse("[{name:'received',initial:false},{name:'payload',initial:''}]");
   Body(p,1)[0]=JObject.Parse("{id:'wait',op:'awaitEvent',event:'user.go',source:'',timeout:{value:0},received:'received',value:'payload'}");
   var a=new Actions();var s=Scheduler(p,a,out var sequence);s.Trigger(sequence.id,0);s.Tick(.01f);Assert.That(s.ObserveRuns().Count(r=>r.waiting),Is.EqualTo(2));
   Assert.That(s.Signal("user.go",new ProgramValue("go"),.1f,out var status),Is.True);Assert.That(status,Does.Contain("2 waiting"));s.Tick(.2f);s.Tick(.3f);Assert.That(Count(s),Is.EqualTo("17"));
   s.StopAll();s.Trigger(sequence.id,1);s.Tick(1.1f);s.Signal("user.go",new ProgramValue("old"),1.2f,out _);s.StopSequence(sequence.id);s.Trigger(sequence.id,2);s.Tick(2.1f);Assert.That(Count(s),Is.EqualTo("7"));Assert.That(s.ObserveRuns().Count(r=>r.waiting),Is.EqualTo(2));s.StopAll();
  }
  [Test] public void EveryBranchSharesTheSixteenCreationLimitIncludingReservedResults(){
   var p=Source();p["resources"]=new JArray();var create=new JObject{["id"]="create",["op"]="invoke",["capability"]="object.create",["version"]=1,["arguments"]=BehaviourCatalog.Action("object.create").Example,["bindings"]=new JObject()};
   Body(p,1).Clear();Body(p,1).Add(new JObject{["id"]="nine",["op"]="repeat",["count"]=new JObject{["value"]=9},["body"]=new JArray(create)});Body(p,1).Add(JObject.Parse("{id:'done',op:'return',value:{value:1}}"));
   Assert.That(BehaviourProgram.TryParse(p.ToString(),out var program,out var error),Is.True,error);var machine=new ProgramMachine(program,null);Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Parallel));
   for(int i=0;i<8;i++)foreach(var branch in machine.Branches){Assert.That(branch.Advance(out _),Is.EqualTo(ProgramYield.Action));Assert.That(branch.CompleteAction(new JObject{["objectId"]=Guid.NewGuid().ToString("N")},out error),Is.True,error);}
   Assert.That(machine.Branches[0].Advance(out _),Is.EqualTo(ProgramYield.Failed));Assert.That(machine.Branches[0].Error,Does.Contain("16 created objects"));
  }
  [Test] public void PinnedParallelModulesRemainImportableAndRequireTheirVersionInTheCaller(){
   var p=Source();var definition=ProgramModuleLibrary.Definition(p.ToString(),"Parallel",new[]{"move"});Assert.DoesNotThrow(()=>ProgramModuleLibrary.Validate(definition));
   p["moduleVersion"]=1;p["imports"]=new JArray(new JObject{["alias"]="motion",["hash"]=ProgramModules.Hash(definition),["module"]=definition,["signals"]=new JObject()});Body(p)[0]["branches"][0]["module"]="motion";
   Assert.That(BehaviourProgram.TryParse(p.ToString(),out _,out var error),Is.True,error);p.Remove("parallelVersion");Assert.That(BehaviourProgram.TryParse(p.ToString(),out _,out error),Is.False);Assert.That(error,Does.Contain("parallel"));
  }

  [Test] public void OneCleanupFailureCannotStrandOtherBranchesOrTheirClaims(){
   var a=new Actions();var s=Scheduler(Source(),a,out var sequence);s.Trigger(sequence.id,0);s.Tick(.01f);a.ThrowOnStop=true;
   Assert.Throws<InvalidOperationException>(()=>s.StopSequence(sequence.id));Assert.That(s.RunningCount,Is.Zero);Assert.That(a.Active,Is.Empty);Assert.That(s.LastError,Does.Contain("could not stop cleanly"));Assert.That(s.TargetsBusy(new[]{"book","maestro"}),Is.False);Assert.That(s.Outcomes.Last().phase,Is.EqualTo("failed"));
  }
 }
}
