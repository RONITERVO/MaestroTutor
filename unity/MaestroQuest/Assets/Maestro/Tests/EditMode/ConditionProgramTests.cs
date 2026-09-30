// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
 public sealed class ConditionProgramTests
 {
  sealed class World:IRuleActions,IProgramFacts,IProgramFactQueries,IProgramEventWorld {
   public Vector3 Position;public bool Exists=true;public int Reads,Starts;
   public bool CanRun(CapabilityCall call,out string error){error=null;return true;}
   public bool Start(string run,CapabilityCall call,out float seconds,out string error){Starts++;seconds=.1f;error=null;return true;}
   public void Stop(string run,bool preserve){}
   public bool TryPosition(string id,out Vector3 value){Reads++;value=Position;return Exists;}
   public bool TryRead(string id,out ProgramValue value)=>BehaviourCatalog.TryRead(id,new BehaviourCatalog.FactContext(world:this),out value);
   public bool TryRead(string id,int version,JObject args,out ProgramValue value)=>BehaviourCatalog.TryRead(id,version,args,new BehaviourCatalog.FactContext(world:this),out value);
  }
  static JObject Fixture()=>JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-conditions.json")));
  static ProgramMachine Machine(JObject p,World w){Assert.That(BehaviourProgram.TryParse(p.ToString(),out var program,out var error),Is.True,error);return new ProgramMachine(program,w);}
  static RuleScheduler Scheduler(JObject p,World w,out string id){id=Guid.NewGuid().ToString("N");var s=new RuleScheduler(w);s.Configure(new RuleDocument {sequences=new[]{new RuleSequence {id=id,name="Watch condition",program=p.ToString()}}});return s;}
  [Test] public void StableEdgesIgnoreBriefChangesAndBaselineSuppressesInitialState(){
   bool current=true;using var watch=new ConditionSubscription(()=>current,"either","baseline",.2f,0);
   Assert.That(watch.Poll(.22f,out _,out _,out _),Is.False);current=false;watch.Poll(.33f,out _,out _,out _);current=true;Assert.That(watch.Poll(.44f,out _,out _,out _),Is.False);
   current=false;Assert.That(watch.Poll(.55f,out _,out _,out _),Is.False);Assert.That(watch.Poll(.77f,out var value,out _,out var error),Is.True);Assert.That(value.Boolean,Is.False);Assert.That(error,Is.Null);
   Assert.That(watch.Poll(.88f,out _,out _,out _),Is.False);current=true;watch.Poll(.99f,out _,out _,out _);Assert.That(watch.Poll(1.21f,out value,out _,out _),Is.True);Assert.That(value.Boolean,Is.True);
  }
  [Test] public void ReportAndGapsRequireFreshStableTimeAndNeverCatchUp(){
   int reads=0;using var watch=new ConditionSubscription(()=>{reads++;return true;},"true","report",.2f,0);
   for(int i=0;i<10;i++)Assert.That(watch.Poll(i*.005f,out _,out _,out _),Is.False);Assert.That(reads,Is.EqualTo(1));
   Assert.That(watch.Poll(.11f,out _,out _,out _),Is.False);Assert.That(watch.Poll(20,out _,out _,out _),Is.False);Assert.That(reads,Is.EqualTo(3));
   Assert.That(watch.Poll(20.11f,out _,out _,out _),Is.False);Assert.That(watch.Poll(20.22f,out _,out _,out _),Is.True);
   watch.Dispose();watch.Poll(30,out _,out _,out _);Assert.That(reads,Is.EqualTo(5));
  }
  [Test] public void SchedulerWaitsWithoutClaimsSamplesBeforeStatementsAndResumesOnce(){
   var w=new World();var s=Scheduler(Fixture(),w,out var id);Assert.That(s.Trigger(id,0),Is.True);Assert.That(s.ObserveRuns()[0].status,Is.EqualTo("Waiting for condition"));Assert.That(s.TargetsBusy(new[]{"book"}),Is.False);
   s.Tick(.11f);w.Position=Vector3.right;s.Tick(.22f);s.Tick(.33f);Assert.That(s.ObserveRuns()[0].nodeId,Is.EqualTo("watch"));s.Tick(.44f);
   var run=s.ObserveRuns()[0];Assert.That(run.nodeId,Is.EqualTo("finish"));Assert.That(run.state.Single(v=>v.name=="matched").value,Is.EqualTo("True"));Assert.That(run.locals.Single(v=>v.name=="conditionValue").value,Is.EqualTo("True"));Assert.That(w.Starts,Is.Zero);
   int reads=w.Reads;s.Tick(.55f);Assert.That(w.Reads,Is.EqualTo(reads));s.Suspend(true);s.Suspend(false);s.Tick(1);Assert.That(s.RunningCount,Is.Zero);
  }
  [Test] public void TimeoutWinsLateSamplesAndKeepsPriorValueWhileMissingReadingsFail(){
   var p=Fixture();p["functions"][0]["locals"][1]["initial"]=true;p["functions"][0]["body"][0]["timeout"]["value"]=.2;
   var w=new World();var s=Scheduler(p,w,out var id);Assert.That(s.Trigger(id,0),Is.True);int reads=w.Reads;w.Position=Vector3.right;s.Tick(.3f);
   Assert.That(w.Reads,Is.EqualTo(reads));var r=s.ObserveRuns()[0];Assert.That(r.state.Single(v=>v.name=="matched").value,Is.EqualTo("False"));Assert.That(r.locals.Single(v=>v.name=="conditionValue").value,Is.EqualTo("True"));
   s=Scheduler(Fixture(),w,out id);Assert.That(s.Trigger(id,0),Is.True);w.Exists=false;s.Tick(.11f);Assert.That(s.RunningCount,Is.Zero);Assert.That(s.Outcomes.Last().status,Does.Contain("unavailable"));
   Assert.That(s.Trigger(id,.2f),Is.False);Assert.That(w.Starts,Is.Zero);
  }
  [Test] public void LongWaitDoesNotExhaustOrRenewStatementsAndExpensiveSamplesFail(){
   var w=new World();var m=Machine(Fixture(),w);Assert.That(m.Advance(out _),Is.EqualTo(ProgramYield.Waiting));int instructions=m.Instructions;using(var watch=m.Wait.Condition(0)){for(int i=1;i<5000;i++)Assert.That(watch.Poll(i*.11f,out _,out _,out var error),Is.False,error);}
   Assert.That(m.Instructions,Is.EqualTo(instructions));
   var p=Fixture();var list=new JArray(Enumerable.Range(0,32));JObject Eq()=>new(){["op"]="eq",["args"]=new JArray(new JObject {["value"]=list.DeepClone()},new JObject {["value"]=list.DeepClone()})};
   p["functions"][0]["body"][0]["test"]=new JObject {["op"]="and",["args"]=new JArray(new JObject {["op"]="and",["args"]=new JArray(Eq(),Eq())},new JObject {["op"]="and",["args"]=new JArray(Eq(),Eq())})};
   m=Machine(p,w);Assert.That(m.Advance(out _),Is.EqualTo(ProgramYield.Waiting));Assert.Throws<ProgramFault>(()=>m.Wait.Condition(0));Assert.That(m.Instructions,Is.LessThan(32));
  }
  [Test] public void ContractRejectsUntypedPoliciesDestinationsAndEvaluatedTiming(){
   foreach(var pair in new[]{("transition",(JToken)new JValue("future")),("initial",new JValue("later")),("received",new JValue("conditionValue")),("value",new JValue("missing")),("test",new JObject {["value"]=1}), ("timeout",new JObject {["value"]=true})}){
    var p=Fixture();p["functions"][0]["body"][0][pair.Item1]=pair.Item2;Assert.That(BehaviourProgram.TryParse(p.ToString(),out _,out _),Is.False,pair.Item1);
   }
   foreach(double bad in new[]{-.1,10.1}){var p=Fixture();p["functions"][0]["body"][0]["stableSeconds"]["value"]=bad;Assert.That(Machine(p,new World()).Advance(out _),Is.EqualTo(ProgramYield.Failed));}
   var source=Fixture();source["version"]=2;source.Remove("state");source.Remove("events");Assert.That(BehaviourProgram.TryParse(source.ToString(),out _,out _),Is.False);
  }
  [Test] public void ImportedConditionExpressionsReadPrivateState(){
   var module=new JObject {["version"]=1,["name"]="Condition",["exports"]=new JArray("main"),["program"]=Fixture()};var caller=Fixture();caller["state"]=new JArray();caller["moduleVersion"]=1;caller["imports"]=new JArray(new JObject {["alias"]="watcher",["hash"]=ProgramModules.Hash(module),["module"]=module,["signals"]=new JObject()});caller["functions"][0]["body"]=new JArray(new JObject {["id"]="call",["op"]="call",["module"]="watcher",["function"]="main",["args"]=new JArray()});
   var w=new World();var s=Scheduler(caller,w,out var id);Assert.That(s.Trigger(id,0),Is.True);w.Position=Vector3.right;s.Tick(.11f);s.Tick(.22f);s.Tick(.33f);Assert.That(s.ObserveRuns()[0].state.Single(v=>v.name=="watcher.matched").value,Is.EqualTo("True"));
  }
 }
}
