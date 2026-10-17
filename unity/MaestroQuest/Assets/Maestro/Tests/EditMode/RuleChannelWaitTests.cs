// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
 public sealed class RuleChannelWaitTests
 {
  sealed class Actions:IRuleActions {
   public readonly List<CapabilityCall> Starts=new();public readonly HashSet<string> Active=new();public bool Ready=true;public int Checks;
   public bool CanRun(CapabilityCall call,out string error){Checks++;error=Ready?null:"The observed revision changed";return Ready;}
   public bool Start(string id,CapabilityCall call,out float seconds,out string error){Starts.Add(call);Active.Add(id);seconds=call.Instant?0:(float?)call.Arguments["seconds"]??.1f;error=null;return true;}
   public void Stop(string id,bool preserve){Active.Remove(id);}
  }
  static RuleSequence Sequence(string target="book",double? wait=3,double seconds=.1){
   var p=JObject.Parse(BehaviourProgram.FromSteps(new RuleStep {action=RuleActionKind.RecordedAnimation,targetId=target,seconds=(float)seconds}));p["version"]=3;p["state"]=new JArray();p["events"]=new JArray();
   if(wait!=null)p["functions"][0]["body"][0]["waitForChannels"]=new JObject {["value"]=wait};return Sequence(p);
  }
  static RuleSequence Sequence(JObject program)=>new(){id=Guid.NewGuid().ToString("N"),name="Wait for channels",program=program.ToString(Newtonsoft.Json.Formatting.None)};
  static RoomOwnership.Lease Hold(RuleScheduler scheduler,string target="book",RoomActorRole role=RoomActorRole.Control){
   Assert.That(scheduler.Ownership.TryAcquire(Guid.NewGuid().ToString("N"),"User control",role,new[]{new BehaviourCatalog.Claim(target,"wholeTarget")},null,out var lease,out var error),Is.True,error);return lease;
  }
  [Test] public void OverlappingWaitsUseArrivalOrderWithoutOwningAnythingAndDisjointWorkStillRuns(){
   var a=new Actions();var s=new RuleScheduler(a);RuleSequence first=Sequence(),second=Sequence(),other=Sequence("maestro",null,2),immediate=Sequence(wait:null);
   s.Configure(new RuleDocument {sequences=new[]{first,second,other,immediate}});var held=Hold(s);
   Assert.That(s.Trigger(first.id,10000),Is.True,s.LastError);Assert.That(s.Trigger(second.id,10000),Is.True,s.LastError);Assert.That(s.Trigger(other.id,10000),Is.True,s.LastError);
   Assert.That(a.Starts.Count,Is.EqualTo(1));Assert.That(s.Ownership.Observe().owners.Length,Is.EqualTo(2));Assert.That(s.ObserveRuns().Single(r=>r.sequenceId==first.id).waitSeconds,Is.EqualTo(3));
   held.Dispose();Assert.That(s.Trigger(immediate.id,10000.01f),Is.False);s.Tick(10000.06f);Assert.That(a.Starts.Count,Is.EqualTo(2));Assert.That(s.ObserveRuns().Single(r=>r.sequenceId==second.id).waiting,Is.True);
   s.Tick(10000.2f);Assert.That(a.Starts.Count,Is.EqualTo(3));Assert.That(s.Outcomes.Single(o=>o.sequenceId==first.id).phase,Is.EqualTo("completed"));s.StopAll();
  }
  [Test] public void EarlierWaitsPreventLegacyRestartsFromCancellingOthersAndLegacyQueuesRemainPending(){
   var a=new Actions();var s=new RuleScheduler(a);var first=Sequence(seconds:.5);var waiter=Sequence();var p=JObject.Parse(Sequence(wait:null).program);p["version"]=2;p.Remove("state");p.Remove("events");var restart=Sequence(p);var queued=Sequence(p);queued.interruption=RuleInterruption.QueueLatest;
   s.Configure(new RuleDocument {sequences=new[]{first,waiter,restart,queued}});Assert.That(s.Trigger(first.id,0),Is.True);Assert.That(s.Trigger(waiter.id,.01f),Is.True);
   Assert.That(s.Trigger(restart.id,.02f),Is.False);Assert.That(s.Outcomes,Is.Empty);Assert.That(a.Active.Count,Is.EqualTo(1));Assert.That(s.Trigger(queued.id,.03f),Is.True);Assert.That(s.QueuedCount,Is.EqualTo(1));
   s.Tick(.6f);Assert.That(s.QueuedCount,Is.EqualTo(1));Assert.That(a.Starts.Count,Is.EqualTo(2));s.Tick(.8f);Assert.That(s.QueuedCount,Is.Zero);Assert.That(a.Starts.Count,Is.EqualTo(3));s.StopAll();
  }
  [Test] public void PinnedModuleWaitTimeoutUsesItsOwnScopedState(){
   var child=JObject.Parse(Sequence().program);child["state"]=new JArray(new JObject {["name"]="patience",["initial"]=2});child["functions"][0]["body"][0]["waitForChannels"]=new JObject {["state"]="patience"};
   var module=new JObject {["version"]=1,["name"]="Queued motion",["exports"]=new JArray("main"),["program"]=child};var source=JObject.Parse(Sequence().program);source["moduleVersion"]=1;source["imports"]=new JArray(new JObject {["alias"]="motion",["hash"]=ProgramModules.Hash(module),["module"]=module,["signals"]=new JObject()});source["functions"][0]["body"]=new JArray(new JObject {["id"]="call",["op"]="call",["module"]="motion",["function"]="main",["args"]=new JArray()});
   Assert.That(BehaviourProgram.TryParse(source.ToString(),out var compiled,out var error),Is.True,error);var machine=new ProgramMachine(compiled,null);Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Action));Assert.That(machine.ChannelWaitSeconds,Is.EqualTo(2));
  }
  [Test] public void AFreeChannelAtTheDeadlineCannotStartAnExpiredAction(){
   var a=new Actions();var s=new RuleScheduler(a);var q=Sequence(wait:.2);s.Configure(new RuleDocument {sequences=new[]{q}});var held=Hold(s);Assert.That(s.Trigger(q.id,0),Is.True);
   held.Dispose();s.Tick(.2f);Assert.That(a.Starts,Is.Empty);Assert.That(s.Outcomes.Single().phase,Is.EqualTo("failed"));Assert.That(s.LastError,Does.Contain("Timed out"));s.Tick(99);Assert.That(a.Starts,Is.Empty);
  }
  [TestCase("stop")] [TestCase("pause")] [TestCase("target")] [TestCase("edit")]
  public void CancelledWaitsNeverReplayAfterTheOwnerReleases(string reason){
   var a=new Actions();var s=new RuleScheduler(a);var q=Sequence();var doc=new RuleDocument {sequences=new[]{q}};s.Configure(doc);var held=Hold(s);Assert.That(s.Trigger(q.id,0),Is.True);
   if(reason=="stop")s.StopSequence(q.id);if(reason=="pause"){s.Suspend(true);s.Suspend(false);}if(reason=="target")s.StopTarget("book",true);if(reason=="edit")s.Configure(doc);
   held.Dispose();s.Tick(1);Assert.That(a.Starts,Is.Empty);Assert.That(s.RunningCount,Is.Zero);Assert.That(s.Ownership.Observe().owners,Is.Empty);
  }
  [Test] public void ReadinessIsCheckedAfterWaitingAndAStartedActionStillCancelsOnTakeover(){
   var a=new Actions();var s=new RuleScheduler(a);var q=Sequence();s.Configure(new RuleDocument {sequences=new[]{q}});var held=Hold(s);Assert.That(s.Trigger(q.id,0),Is.True);Assert.That(a.Checks,Is.Zero);
   a.Ready=false;held.Dispose();s.Tick(.06f);Assert.That(a.Starts,Is.Empty);Assert.That(s.LastError,Does.Contain("revision"));Assert.That(s.Ownership.Observe().owners,Is.Empty);
   a.Ready=true;Assert.That(s.Trigger(q.id,1),Is.True);Assert.That(a.Starts.Count,Is.EqualTo(1));held=Hold(s);Assert.That(a.Active,Is.Empty);held.Dispose();s.Tick(2);Assert.That(a.Starts.Count,Is.EqualTo(1));
  }
  [Test] public void ABlockedMultiChannelWaitDoesNotPartiallyCancelAnAmbientActor(){
   string prop=Guid.NewGuid().ToString("N");var p=JObject.Parse(BehaviourProgram.FromSteps(new RuleStep {action=RuleActionKind.Gesture,targetId="maestro",propId=prop,seconds=.2f}));p["version"]=3;p["state"]=new JArray();p["events"]=new JArray();p["functions"][0]["body"][0]["waitForChannels"]=new JObject {["value"]=3};
   var a=new Actions();var s=new RuleScheduler(a);var q=Sequence(p);s.Configure(new RuleDocument {sequences=new[]{q}});int interrupted=0;
   Assert.That(s.Ownership.TryAcquire("ambient","Tutor activity",RoomActorRole.Ambient,new[]{new BehaviourCatalog.Claim("maestro","wholeTarget")},_=>interrupted++,out var ambient,out _),Is.True);var held=Hold(s,prop);
   Assert.That(s.Trigger(q.id,0),Is.True,s.LastError);Assert.That(ambient.Held,Is.True);Assert.That(interrupted,Is.Zero);held.Dispose();s.Tick(.06f);Assert.That(interrupted,Is.EqualTo(1));Assert.That(a.Starts.Count,Is.EqualTo(1));s.StopAll();
  }
  [Test] public void ParallelBranchesCanSerializeTheSameChannelWithoutRestartingEitherBranch(){
   var p=JObject.Parse(Sequence().program);var worker=(JObject)p["functions"][0].DeepClone();worker["name"]="work";p["parallelVersion"]=1;p["functions"]=new JArray(new JObject {["name"]="main",["returns"]="void",["parameters"]=new JArray(),["locals"]=new JArray(),["body"]=new JArray(new JObject {["id"]="together",["op"]="parallel",["branches"]=new JArray(new JObject {["function"]="work",["args"]=new JArray()},new JObject {["function"]="work",["args"]=new JArray()})})},worker);
   var a=new Actions();var s=new RuleScheduler(a);var q=Sequence(p);s.Configure(new RuleDocument {sequences=new[]{q}});Assert.That(s.Trigger(q.id,0),Is.True);s.Tick(.01f);Assert.That(a.Starts.Count,Is.EqualTo(1));Assert.That(s.ObserveRuns().Count(r=>r.waiting),Is.EqualTo(1));
   s.Tick(.2f);s.Tick(.4f);s.Tick(.5f);Assert.That(a.Starts.Count,Is.EqualTo(2));Assert.That(s.RunningCount,Is.Zero);Assert.That(s.Outcomes.Single().phase,Is.EqualTo("completed"));
  }
  [Test] public void WaitingForEveryInstantEffectDoesNotRenewInstructionBudgets(){
   string target=Guid.NewGuid().ToString("N");var p=JObject.Parse(BehaviourProgram.FromSteps(new RuleStep {action=RuleActionKind.PhysicsStop,targetId=target}));p["version"]=3;p["state"]=new JArray();p["events"]=new JArray();var body=(JArray)p["functions"][0]["body"].DeepClone();body[0]["waitForChannels"]=new JObject {["value"]=3};p["functions"][0]["body"]=new JArray(new JObject {["id"]="loop",["op"]="forever",["body"]=body});
   var a=new Actions();var s=new RuleScheduler(a);var q=Sequence(p);s.Configure(new RuleDocument {sequences=new[]{q}});var hold=Hold(s,target);Assert.That(s.Trigger(q.id,0),Is.True);hold.Dispose();float time=0;
   for(int i=0;i<40000&&s.RunningCount>0;i++){s.Tick(time+=.06f);using var next=Hold(s,target);s.Tick(time+=.01f);}
   Assert.That(s.RunningCount,Is.Zero);Assert.That(s.LastError,Does.Contain("instruction budget"));Assert.That(a.Starts.Count,Is.GreaterThan(1));
  }
  [Test] public void SharedSyntaxRequiresVersionThreeAndRejectsInvalidComputedTimeoutBeforeEffects(){
   var p=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-channel-wait.json")));Assert.That(BehaviourProgram.TryParse(p.ToString(),out _,out var error),Is.True,error);
   p["version"]=2;((JObject)p).Remove("state");((JObject)p).Remove("events");Assert.That(BehaviourProgram.TryParse(p.ToString(),out _,out _),Is.False);
   foreach(double wait in new[]{0,.01,31}){var a=new Actions();var s=new RuleScheduler(a);var q=Sequence(wait:wait);s.Configure(new RuleDocument {sequences=new[]{q}});Assert.That(s.Trigger(q.id,0),Is.False);Assert.That(a.Starts,Is.Empty);Assert.That(s.LastError,Does.Contain("timeout"));}
  }
 }
}
