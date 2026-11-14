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
 public sealed class StructureWatchProgramTests
 {
  static JObject Fixture()=>JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-structure-watch.json")));
  static string Source()=>File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Resources/Programs/Modules/StructureWatch.json"));
  sealed class World:IRuleActions,IProgramFacts,IProgramFactQueries {
   public int Displaced,Missing,Held,Revision=1,Starts,Reads;public bool Available=true,Exists=true;
   public bool CanRun(CapabilityCall call,out string error){error=null;return true;}
   public bool Start(string run,CapabilityCall call,out float seconds,out string error){Starts++;seconds=0;error=null;return true;}
   public void Stop(string run,bool preserve){}
   public bool TryRead(string id,out ProgramValue value){value=default;return false;}
   public bool TryRead(string id,int version,JObject args,out ProgramValue value){Reads++;value=default;if(id!="structure.state"||!Exists)return false;
    value=ProgramValue.Literal(new JObject {["id"]=(string)args["id"],["revision"]=Revision,["total"]=6,["present"]=6-Missing,["displaced"]=Displaced,["missing"]=Missing,["held"]=Held,["available"]=Available});return true;}
  }
  static RuleScheduler Start(World w,JObject fixture=null){var p=fixture??Fixture();Assert.That(BehaviourProgram.TryParse(p.ToString(Newtonsoft.Json.Formatting.None),out _,out var error),Is.True,error);var s=new RuleScheduler(w);string id=new string('a',32);s.Configure(new RuleDocument {sequences=new[]{new RuleSequence {id=id,name="Structure watcher",program=p.ToString(Newtonsoft.Json.Formatting.None)}}});Assert.That(s.Trigger(id,0),Is.True,s.LastError);return s;}
  static void Step(RuleScheduler s,ref float time,int count=3){for(int i=0;i<count;i++){time+=.11f;s.Tick(time);}}
  static string State(RuleScheduler s,string name)=>s.ObserveRuns().Single().state.Single(v=>v.name==name).value;
  static JObject Once(bool disturbed,float timeout=0,float threshold=1){var p=Fixture();var call=(JObject)p["functions"][0]["body"][0]["body"][1].DeepClone();call["args"][2]["value"]=disturbed;call["args"][3]=new JObject {["value"]=threshold};call["args"][5]["value"]=timeout;
   p["functions"][0]["body"]=new JArray(call,new JObject {["id"]="result",["op"]="setState",["variable"]="phase",["value"]=new JObject {["var"]="status"}},new JObject {["id"]="show",["op"]="sleep",["seconds"]=new JObject {["value"]=30}});return p;}
  [Test] public void IncludedDefinitionIsPinnedInspectableAndNeverWritesOrStartsOnLoad(){
   var module=JObject.Parse(Source());ProgramModuleLibrary.Validate(module);var fixture=Fixture();Assert.That(JToken.DeepEquals(fixture["imports"][0]["module"],module),Is.True);Assert.That((string)fixture["imports"][0]["hash"],Is.EqualTo(ProgramModules.Hash(module)));
   string directory=Path.Combine(Path.GetTempPath(),"maestro-included-"+Guid.NewGuid().ToString("N"));var library=new ProgramModuleLibrary(directory,includedDefinitions:ProgramModuleLibrary.IncludedFromApplication());library.Flush();
   Assert.That(library.Error,Is.Null);var entry=library.Search("Structure state waits").Single();Assert.That(entry.Included,Is.True);Assert.That(entry.ReadDefinition()["program"]["resources"].Count(),Is.Zero);Assert.That(library.CanRemove(entry.Hash,out _),Is.False);Assert.Throws<ProgramFault>(()=>library.Remove(entry.Hash));
   Assert.That(library.Revision,Is.EqualTo(1));Assert.That(Directory.Exists(directory),Is.False);
   var changed=entry.ReadDefinition();changed["name"]="My copy";Assert.That(library.Inspect(entry.Hash).Name,Is.EqualTo("Structure state waits"));
   // An explicit file import/publication must remain durable even if today's app
   // already includes those bytes. A future app may ship a different example.
   try{var write=library.Publish(entry.ReadDefinition());library.Flush();Assert.That(write.Error,Is.Null);Assert.That(write.Changed,Is.True);Assert.That(library.Inspect(entry.Hash).Included,Is.False);var later=new ProgramModuleLibrary(directory);later.Flush();Assert.That(later.Inspect(entry.Hash),Is.Not.Null);Assert.That(later.Count,Is.EqualTo(1));}
   finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
  }
  [Test] public void RemovingADamagedPrivateDuplicateRevealsTheIncludedSource(){
   string directory=Path.Combine(Path.GetTempPath(),"maestro-included-"+Guid.NewGuid().ToString("N"));string source=Source(),hash=ProgramModules.Hash(JObject.Parse(source));var files=Path.Combine(directory,"program-modules.v1");Directory.CreateDirectory(files);string path=Path.Combine(files,hash+".json");File.WriteAllText(path,"{broken");
   try{var library=new ProgramModuleLibrary(directory,includedDefinitions:new[]{source});library.Flush();Assert.That(library.Count,Is.EqualTo(1));Assert.That(library.Inspect(hash).Included,Is.False);Assert.That(library.Inspect(hash).Error,Is.Not.Null);Assert.That(File.ReadAllText(path),Is.EqualTo("{broken"));var removed=library.Remove(hash);library.Flush();Assert.That(removed.Error,Is.Null);Assert.That(removed.Changed,Is.True);Assert.That(library.Inspect(hash).Included,Is.True);Assert.That(library.Count,Is.EqualTo(1));Assert.That(File.Exists(path),Is.False);}
   finally{Directory.Delete(directory,true);}
  }
  [Test] public void WatcherArmsOnRebuildIgnoresHeldPiecesAndRequiresRearming(){
   var w=new World {Displaced=1};var s=Start(w);float t=0;Step(s,ref t,5);Assert.That(State(s,"cycles"),Is.EqualTo("0"));Assert.That(State(s,"phase"),Is.EqualTo("waitingForRebuild"));
   w.Displaced=0;Step(s,ref t,4);Assert.That(State(s,"phase"),Is.EqualTo("waitingForDisturbance"));w.Displaced=1;w.Held=1;Step(s,ref t,4);Assert.That(State(s,"cycles"),Is.EqualTo("0"));w.Held=0;Step(s,ref t,4);Assert.That(State(s,"cycles"),Is.EqualTo("1"));Step(s,ref t,6);Assert.That(State(s,"cycles"),Is.EqualTo("1"));
   w.Displaced=0;Step(s,ref t,4);w.Missing=1;Step(s,ref t,4);Assert.That(State(s,"cycles"),Is.EqualTo("2"));Assert.That(w.Starts,Is.Zero);Assert.That(s.TargetsBusy(new[]{"book","maestro"}),Is.False);
  }
  [Test] public void UnavailableIntervalsAndSamplingGapsRequireFreshStableTime(){
   var w=new World {Displaced=2};var s=Start(w,Once(true));float t=0;Step(s,ref t,1);w.Available=false;Step(s,ref t,3);Assert.That(State(s,"phase"),Is.EqualTo("idle"));w.Available=true;Step(s,ref t,1);t+=1;s.Tick(t);Assert.That(State(s,"phase"),Is.EqualTo("idle"));Step(s,ref t,1);Assert.That(State(s,"phase"),Is.EqualTo("idle"));Step(s,ref t,2);Assert.That(State(s,"phase"),Is.EqualTo("matched"));Assert.That(w.Starts,Is.Zero);
  }
  [Test] public void DefinitionChangesTimeoutAndBadThresholdReturnExplicitStatuses(){
   var w=new World();var s=Start(w,Once(true));float t=0;w.Revision=2;Step(s,ref t,4);Assert.That(State(s,"phase"),Is.EqualTo("definitionChanged"));
   w=new World();s=Start(w,Once(true,.2f));t=0;Step(s,ref t,6);Assert.That(State(s,"phase"),Is.EqualTo("timeout"));s=Start(w,Once(true,0,0));Assert.That(State(s,"phase"),Is.EqualTo("invalidThreshold"));
   s=Start(w);t=0;Step(s,ref t,4);w.Revision++;Step(s,ref t,4);Assert.That(s.RunningCount,Is.Zero);Assert.That(s.Outcomes.Last().nodeId,Is.EqualTo("stop_disturbed"));
  }
  [Test] public void MissingDefinitionAndStopDoNotFabricateAChangeOrResume(){
   var w=new World();var s=Start(w);float t=0;Step(s,ref t,4);w.Exists=false;Step(s,ref t,1);Assert.That(s.RunningCount,Is.Zero);Assert.That(s.Outcomes.Last().status,Does.Contain("unavailable"));
   w=new World();s=Start(w);s.Suspend(true);s.Suspend(false);w.Displaced=2;Step(s,ref t,4);Assert.That(s.RunningCount,Is.Zero);Assert.That(w.Starts,Is.Zero);
  }
 }
}
