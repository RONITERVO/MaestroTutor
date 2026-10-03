// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Maestro.Quest.Programs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
 public sealed class ProgramModuleTests
 {
  static string Fixture(string name)=>File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/"+name+".json"));
  [Test] public void SharedContractsAndIndependentHashesAgree(){
   var contract=JObject.Parse(Fixture("program-module-contract"));foreach(var c in contract["cases"])Assert.That(BehaviourProgram.TryParse((string)c["source"],out _,out var error),Is.EqualTo((bool)c["valid"]),(string)c["name"]+": "+error);
   foreach(var v in contract["hashes"])Assert.That(ProgramModules.Hash(v["value"]),Is.EqualTo((string)v["hash"]));
  }
  [TestCase("program-modules","first.count")] [TestCase("program-modules-nested","first.inner.count")]
  public void ImportedStateSignalsCallsAndPinsShareOneMachine(string fixture,string firstState){
   string source=Fixture(fixture);Assert.That(BehaviourProgram.TryParse(source,out var program,out var error),Is.True,error);
   Assert.That(JToken.DeepEquals(JObject.Parse(source),JObject.Parse(program.Source)),Is.True,"Source retains frozen snapshots, not the generated implementation");
   var machine=new ProgramMachine(program,null);var signals=new List<string>();ProgramYield result;int ticks=0;
   do {result=machine.Advance(out _);Assert.That(++ticks,Is.LessThan(100),machine.Error);if(result==ProgramYield.Signal)signals.Add(machine.Signal.Event+":"+machine.Signal.Value.Number);}while(result==ProgramYield.Signal||result==ProgramYield.Yield);
   Assert.That(result,Is.EqualTo(ProgramYield.Waiting),machine.Error);Assert.That(signals,Is.EqualTo(new[]{"user.first:1","user.first:3","user.second:5"}));
   Assert.That(machine.State[firstState].Number,Is.EqualTo(3));Assert.That(machine.State["second.count"].Number,Is.EqualTo(5));
   Assert.That(machine.Locals["a"].Number,Is.EqualTo(1));Assert.That(machine.Locals["b"].Number,Is.EqualTo(3));Assert.That(machine.Locals["c"].Number,Is.EqualTo(5));
   Assert.That(new ProgramMachine(program,null).State[firstState].Number,Is.Zero,"New runs initialize import state independently; module entry is never auto-run");
   machine.Resume(false);Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Completed));
  }
  [Test] public void ImportedBusyLoopSharesInstructionBudgetAndDiagnostics(){
   var source=JObject.Parse(Fixture("program-modules"));var imp=(JObject)source["imports"][0];var module=(JObject)imp["module"];
   module["program"]["functions"][2]["body"]=new JArray(new JObject { ["id"]="busy",["op"]="forever",["body"]=new JArray() });imp["hash"]=ProgramModules.Hash(module);
   Assert.That(BehaviourProgram.TryParse(source.ToString(),out var program,out var error),Is.True,error);var machine=new ProgramMachine(program,null);ProgramYield result;int ticks=0;
   do {result=machine.Advance(out _);Assert.That(++ticks,Is.LessThan(3000));}while(result==ProgramYield.Yield);
   Assert.That(result,Is.EqualTo(ProgramYield.Failed));Assert.That(machine.Error,Does.Contain("instruction"));Assert.That(machine.Function,Is.EqualTo("first.privateAdd"));Assert.That(machine.NodeId,Is.EqualTo("first.busy"));
  }
  [TestCase(false)] [TestCase(true)] public void ImportedCollectionsKeepOneCreationAuthority(bool guessed){
   var child=JObject.Parse(Fixture("program-collections"));if(guessed){child["state"][0]["initial"]=new JArray(new JObject {["id"]=new string('a',32),["red"]=.2});((JArray)child["functions"][0]["body"]).RemoveAt(0);}
   var module=new JObject {["version"]=1,["name"]="Create and paint",["exports"]=new JArray("main"),["program"]=child};
   var source=JObject.Parse(@"{'version':3,'dataVersion':1,'moduleVersion':1,'entry':'main','resources':[],'state':[],'events':[],'functions':[{'name':'main','returns':'void','parameters':[],'locals':[],'body':[{'id':'run','op':'call','module':'objects','function':'main','args':[]}]}]}");
   source["imports"]=new JArray(new JObject {["alias"]="objects",["hash"]=ProgramModules.Hash(module),["module"]=module,["signals"]=new JObject()});Assert.That(BehaviourProgram.TryParse(source.ToString(),out var program,out var error),Is.True,error);
   var machine=new ProgramMachine(program,null);int creates=0,paints=0,ticks=0;ProgramYield result;
   do {result=machine.Advance(out var action);Assert.That(++ticks,Is.LessThan(100),machine.Error);if(result!=ProgramYield.Action)continue;
    if(action.Definition.Id=="object.create"){creates++;Assert.That(machine.CompleteAction(new JObject {["objectId"]=creates.ToString("x32")},out error),Is.True,error);}
    else {Assert.That(action.Definition.Id,Is.EqualTo("object.color.set"));paints++;Assert.That((string)action.Arguments["target"],Is.EqualTo(paints.ToString("x32")));}
   }while(result==ProgramYield.Action||result==ProgramYield.Yield);
   if(guessed){Assert.That(result,Is.EqualTo(ProgramYield.Failed));Assert.That(machine.Error,Does.Contain("declared or created"));Assert.That(paints,Is.Zero);}
   else {Assert.That(result,Is.EqualTo(ProgramYield.Waiting),machine.Error);Assert.That(creates,Is.EqualTo(2));Assert.That(paints,Is.EqualTo(2));Assert.That(machine.State.ContainsKey("objects.items"),Is.True);}
  }
  [Test] public void ReorderedMetadataDoesNotUpgradeOrGrantAuthority(){
   var source=JObject.Parse(Fixture("program-modules"));var imp=(JObject)source["imports"][0];var module=(JObject)imp["module"];string pin=(string)imp["hash"];
   imp["module"]=new JObject(module.Properties().Reverse().Select(p=>new JProperty(p.Name,p.Value.DeepClone())));Assert.That(ProgramModules.Hash(imp["module"]),Is.EqualTo(pin));
   Assert.That(BehaviourProgram.TryParse(source.ToString(),out _,out var error),Is.True,error);
   imp["module"]["program"]["resources"]=new JArray("book");imp["hash"]=ProgramModules.Hash(imp["module"]);
   Assert.That(BehaviourProgram.TryParse(source.ToString(),out _,out error),Is.False);Assert.That(error,Does.Contain("resource"));
  }
 }
}
