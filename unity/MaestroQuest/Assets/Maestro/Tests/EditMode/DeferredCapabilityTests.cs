// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
 public sealed class DeferredCapabilityTests
 {
  sealed class Actions:IRuleActions,IRuleReadiness,IRuleCompletion,IRuleResults,IRuleInterruptionInfo {
   public int Starts,Stops,Completions;public bool Hold,Failed,first;
   public bool CanRun(CapabilityCall call,out string error){error=null;return true;}
   public bool Start(string id,CapabilityCall call,out float seconds,out string error){Starts++;first=true;seconds=0;error=null;return true;}
   public RuleActionState State(string id,out string error){error=Failed?"write failed":null;if(Failed)return RuleActionState.Failed;bool pending=Hold||first;first=false;return pending?RuleActionState.Preparing:RuleActionState.Ready;}
   public bool Complete(string id,out string error){Completions++;error=null;return true;}
   public JObject TakeResult(string id)=>new() {["sessionId"]=new string('a',32),["saveId"]=new string('b',32),["savedRevision"]=Completions};
   public string InterruptionStatus(string id)=>"Stopped waiting; the dispatched write may still finish";
   public void Stop(string id,bool preserve){Stops++;}
  }
  static JObject Call()=>new() {["id"]="room.session",["version"]=1,["arguments"]=new JObject {["operation"]="keep",["sessionId"]=new string('a',32)}};
  [Test] public void CompletionWaitsForTheOperationAndStopDoesNotClaimRollback()
  {
   var actions=new Actions {Hold=true};var scheduler=new RuleScheduler(actions);
   Assert.That(scheduler.Invoke(Call(),0,out var id,out var error),Is.True,error);
   Assert.That((string)scheduler.Invocation(id)["phase"],Is.EqualTo("preparing"));scheduler.Tick(1);Assert.That(actions.Completions,Is.Zero);
   actions.Hold=false;scheduler.Tick(2);Assert.That(actions.Completions,Is.EqualTo(1));Assert.That((string)scheduler.Invocation(id)["phase"],Is.EqualTo("completed"));
   Assert.That((string)scheduler.Invocation(id)["output"]["saveId"],Is.EqualTo(new string('b',32)));
   actions.Hold=true;Assert.That(scheduler.Invoke(Call(),3,out var cancelled,out _),Is.True);
   Assert.That(scheduler.CancelInvocation(cancelled,out _),Is.True);
   Assert.That((string)scheduler.Invocation(cancelled)["phase"],Is.EqualTo("cancelled"));Assert.That((string)scheduler.Invocation(cancelled)["status"],Does.Contain("may still finish"));
   actions.Hold=false;scheduler.Tick(4);Assert.That(actions.Completions,Is.EqualTo(1),"No continuation after Stop");Assert.That(actions.Starts,Is.EqualTo(2));
  }
  [TestCase(false)] [TestCase(true)] public void FailureAndTimeoutRetainDispatchedEffectUncertainty(bool fail)
  {
   var actions=new Actions {Hold=true};var scheduler=new RuleScheduler(actions);scheduler.Invoke(Call(),0,out var id,out _);
   actions.Failed=fail;scheduler.Tick(fail?1:31);
   Assert.That((string)scheduler.Invocation(id)["phase"],Is.EqualTo("failed"));Assert.That((string)scheduler.Invocation(id)["status"],Does.Contain("may still finish"));
   Assert.That(actions.Completions,Is.Zero);Assert.That(actions.Stops,Is.EqualTo(1));
  }
  [Test] public void WorkspacePublicationHasABoundedNativeDeadlineWithoutChangingOtherActions()
  {
   var actions=new Actions {Hold=true};var scheduler=new RuleScheduler(actions);
   var call=new JObject {["id"]="workspace.archive.export",["version"]=1,["arguments"]=new JObject()};
   Assert.That(scheduler.Invoke(call,0,out var id,out var error),Is.True,error);
   scheduler.Tick(31);Assert.That((string)scheduler.Invocation(id)["phase"],Is.EqualTo("preparing"));
   scheduler.Tick(599);Assert.That((string)scheduler.Invocation(id)["phase"],Is.EqualTo("preparing"));
   scheduler.Tick(600);Assert.That((string)scheduler.Invocation(id)["phase"],Is.EqualTo("failed"));Assert.That(actions.Completions,Is.Zero);Assert.That(actions.Stops,Is.EqualTo(1));
  }
  [Test] public void IoCompletionNeverRenewsAnInstantForeverProgramsInstructionBudget()
  {
   var source=JObject.Parse(BehaviourProgram.FromInvocation(Call()));source["version"]=3;source["state"]=new JArray();source["events"]=new JArray();
   var body=(JArray)source["functions"][0]["body"];var action=body[0].DeepClone();body.Clear();body.Add(new JObject {["id"]="loop",["op"]="forever",["body"]=new JArray(action)});
   var sequence=new RuleSequence {id=Guid.NewGuid().ToString("N"),name="Repeated save",program=source.ToString()};
   var actions=new Actions();var scheduler=new RuleScheduler(actions);scheduler.Configure(new RuleDocument {sequences=new[]{sequence}});
   Assert.That(scheduler.Trigger(sequence.id,0),Is.True,scheduler.LastError);
   for(int i=1;i<70000&&scheduler.RunningCount>0;i++)scheduler.Tick(i*.01f);
   Assert.That(scheduler.RunningCount,Is.Zero);Assert.That(scheduler.LastError,Does.Contain("budget"));Assert.That(actions.Completions,Is.GreaterThan(1000));
  }
 }
}
