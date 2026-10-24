// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.IO;
using Maestro.Quest.Programs;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
 public sealed class ParameterizedFactTests
 {
  sealed class World:IProgramEventWorld,IProgramRoomSpace,IProgramFacts,IProgramFactQueries {
   public Vector3 Position=new(1,2,3);public bool Exists=true;public int Reads;public string Target;
   public bool TryPosition(string id,out Vector3 value)=>TryRoomPosition(id,out value);
   public bool TryRoomPosition(string id,out Vector3 value){Reads++;Target=id;value=Position;return Exists;}
   public bool TryRead(string id,out ProgramValue value)=>BehaviourCatalog.TryRead(id,new BehaviourCatalog.FactContext(world:this),out value);
   public bool TryRead(string id,int version,JObject args,out ProgramValue value)=>BehaviourCatalog.TryRead(id,version,args,new BehaviourCatalog.FactContext(world:this),out value);
  }
  static JObject Fixture()=>JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-object-facts.json")));
  static ProgramMachine Machine(JObject source,World world){Assert.That(BehaviourProgram.TryParse(source.ToString(),out var program,out var error),Is.True,error);return new ProgramMachine(program,world);}
  [Test] public void AStoredSnapshotStaysDetachedAndTheNextEvaluationReadsTheLiveTarget(){
   var world=new World();var machine=Machine(Fixture(),world);Assert.That(machine.Advance(out _,256),Is.EqualTo(ProgramYield.Waiting));Assert.That(world.Target,Is.EqualTo("book"));Assert.That(world.Reads,Is.EqualTo(1));
   world.Position=new Vector3(4,2,3);machine.Resume(false);Assert.That(machine.Advance(out _,256),Is.EqualTo(ProgramYield.Waiting));Assert.That(world.Reads,Is.EqualTo(2));Assert.That(machine.State["firstX"].Number,Is.EqualTo(1));Assert.That(machine.State["lastX"].Number,Is.EqualTo(4));Assert.That(machine.State["moved"].Boolean,Is.True);
  }
  [Test] public void MissingAndInvalidReadingsAreUnavailableInsteadOfTheOrigin(){
   var w=new World {Exists=false};Assert.That(Machine(Fixture(),w).Advance(out _),Is.EqualTo(ProgramYield.Failed));
   w.Exists=true;w.Position=new Vector3(float.NaN,0,0);Assert.That(w.TryRead("object.position",2,new JObject {["target"]="book"},out _),Is.False);
   w.Position=new Vector3(1000001,0,0);Assert.That(w.TryRead("object.position",2,new JObject {["target"]="book"},out _),Is.False);
   w.Position=Vector3.zero;Assert.That(w.TryRead("object.position",2,new JObject {["target"]="book"},out var zero),Is.True);Assert.That((double)((JObject)zero.Value)["x"],Is.Zero);
  }
  [Test] public void InvalidComputedTargetsFailBeforeReadingAndReadOnlyTargetsGrantNoAuthority(){
   var source=Fixture();source["state"][0]["initial"]="not an object";var world=new World();var machine=Machine(source,world);Assert.That(machine.Advance(out _,256),Is.EqualTo(ProgramYield.Failed));Assert.That(world.Reads,Is.Zero);
   source=Fixture();source["state"][0]["initial"]=new string('a',32);machine=Machine(source,world);Assert.That(machine.Advance(out _,256),Is.EqualTo(ProgramYield.Waiting));Assert.That(world.Target,Is.EqualTo(new string('a',32)));Assert.That(((JArray)source["resources"]).Count,Is.Zero);
   ((JArray)source["functions"][0]["body"]).Insert(1,new JObject {["id"]="edit",["op"]="invoke",["capability"]="object.color.set",["version"]=1,["arguments"]=new JObject {["target"]=new string('a',32),["red"]=1,["green"]=0,["blue"]=0},["bindings"]=new JObject()});Assert.That(BehaviourProgram.TryParse(source.ToString(),out _,out _),Is.False);
  }
  [Test] public void QuerySourceRejectsVersionArgumentTypeAndStructuredValueMismatch(){
   foreach(string key in new[]{"version","arguments","bindings"}){var p=Fixture();((JObject)p["functions"][0]["body"][0]["value"]).Remove(key);Assert.That(BehaviourProgram.TryParse(p.ToString(),out _,out _),Is.False,key);}
   var source=Fixture();source["functions"][0]["body"][0]["value"]["bindings"]["target"]=new JObject {["value"]=4};Assert.That(BehaviourProgram.TryParse(source.ToString(),out _,out _),Is.False);
   source=Fixture();source.Remove("dataVersion");Assert.That(BehaviourProgram.TryParse(source.ToString(),out _,out _),Is.False);
   source=Fixture();source["functions"][0]["body"][0]["value"]["version"]=1;Assert.That(BehaviourProgram.TryParse(source.ToString(),out _,out _),Is.False);
  }
  [Test] public void ImportedFactBindingsResolvePrivateModuleState(){
   var module=new JObject {["version"]=1,["name"]="Position observer",["exports"]=new JArray("main"),["program"]=Fixture()};
   var caller=JObject.Parse("{\"version\":3,\"dataVersion\":1,\"moduleVersion\":1,\"entry\":\"main\",\"resources\":[],\"state\":[],\"events\":[],\"functions\":[{\"name\":\"main\",\"returns\":\"void\",\"parameters\":[],\"locals\":[],\"body\":[{\"id\":\"call\",\"op\":\"call\",\"module\":\"probe\",\"function\":\"main\",\"args\":[]}]}]}");
   caller["imports"]=new JArray(new JObject {["alias"]= "probe",["hash"]=ProgramModules.Hash(module),["module"]=module,["signals"]=new JObject()});var w=new World();var machine=Machine(caller,w);Assert.That(machine.Advance(out _,256),Is.EqualTo(ProgramYield.Waiting));Assert.That(w.Target,Is.EqualTo("book"));Assert.That(machine.State["probe.firstX"].Number,Is.EqualTo(1));
  }
  [Test] public void CatalogInputsAreBoundedAndOnlyFactInspectionAcceptsThem(){
   var query=JObject.Parse("{\"operation\":\"inspect\",\"category\":\"facts\",\"capability\":\"object.position\",\"version\":2,\"arguments\":{\"target\":\"book\"}}");Assert.That(RoomCapabilityCatalog.ValidRequest(query),Is.True);
   foreach(string category in new[]{"actions","events","modules"}){query["category"]=category;Assert.That(RoomCapabilityCatalog.ValidRequest(query),Is.False);}
   foreach(JToken category in new JToken[]{new JObject(),new JArray("facts"),new JValue(1),JValue.CreateNull()}){query["category"]=category;Assert.That(RoomCapabilityCatalog.ValidRequest(query),Is.False);}
   query["category"]="facts";query["arguments"]=new JObject {["target"]=new string('x',129)};Assert.That(RoomCapabilityCatalog.ValidRequest(query),Is.False);query.Remove("capability");Assert.That(RoomCapabilityCatalog.ValidRequest(query),Is.False);
  }
 }
}
