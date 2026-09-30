// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
 public sealed class ProgramDataTests
 {
  static string Fixture(string name)=>File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/"+name+".json"));
  static ProgramMachine Machine(JObject source){Assert.That(BehaviourProgram.TryParse(source.ToString(),out var program,out var error),Is.True,error);return new ProgramMachine(program,null);}
  [Test] public void SharedStructuredContractsAgree(){foreach(var item in JObject.Parse(Fixture("program-data-contract"))["cases"])Assert.That(BehaviourProgram.TryParse((string)item["source"],out _,out var error),Is.EqualTo((bool)item["valid"]),(string)item["name"]+": "+error);}
  [Test] public void CollectionOfCreatedIdsSurvivesFunctionCallsWithoutAliasingOrLosingAuthority(){
   var machine=Machine(JObject.Parse(Fixture("program-collections")));int creates=0,paints=0,ticks=0;ProgramYield state;
   do {
    state=machine.Advance(out var action);Assert.That(++ticks,Is.LessThan(100),machine.Error);
    if(state!=ProgramYield.Action)continue;
    if(action.Definition.Id=="object.create"){creates++;Assert.That(machine.CompleteAction(new JObject {["objectId"]=creates.ToString("x32")},out var error),Is.True,error);}
    else {Assert.That(action.Definition.Id,Is.EqualTo("object.color.set"));paints++;Assert.That((string)action.Arguments["target"],Is.EqualTo(paints.ToString("x32")));Assert.That((double)action.Arguments["red"],Is.EqualTo(.2));}
   }while(state==ProgramYield.Action||state==ProgramYield.Yield);
   Assert.That(state,Is.EqualTo(ProgramYield.Waiting),machine.Error);Assert.That(creates,Is.EqualTo(2));Assert.That(paints,Is.EqualTo(2));
   Assert.That((double)((JArray)machine.State["items"].Value)[0]["red"],Is.EqualTo(.2));Assert.That((double)((JArray)machine.Locals["copy"].Value)[0]["red"],Is.EqualTo(.9));
   var detached=(JArray)machine.State["items"].Value;detached[0]["red"]=42;Assert.That((double)((JArray)machine.State["items"].Value)[0]["red"],Is.EqualTo(.2));
   machine.Resume(false);Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Completed));
  }
  [Test] public void GuessedIdsInRecordsDoNotAuthorizeEffects(){
   var source=JObject.Parse(Fixture("program-collections"));source["state"][0]["initial"]=new JArray(new JObject {["id"]=new string('a',32),["red"]=.2});((JArray)source["functions"][0]["body"]).RemoveAt(0);
   var machine=Machine(source);ProgramYield result;int ticks=0;do {result=machine.Advance(out _);Assert.That(++ticks,Is.LessThan(20));}while(result==ProgramYield.Yield);
   Assert.That(result,Is.EqualTo(ProgramYield.Failed));Assert.That(machine.Error,Does.Contain("declared or created"));
  }
  [TestCase(-1)] [TestCase(2)] [TestCase(.5)] public void InvalidIndexesFailWithoutChangingTheDestination(double index){
   var source=JObject.Parse((string)JObject.Parse(Fixture("program-data-contract"))["cases"][0]["source"]);source["functions"][0]["body"][0]["value"]["args"][1]["value"]=index;
   var machine=Machine(source);Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Failed));Assert.That(machine.Error,Does.Contain("index"));Assert.That(machine.Locals["item"].Number,Is.Zero);
  }
  [Test] public void RetainedValuesHaveOneBudgetAcrossStateAndFunctionScopes(){
   var source=JObject.Parse((string)JObject.Parse(Fixture("program-data-contract"))["cases"][0]["source"]);var array=new JArray(Enumerable.Repeat(0,32));
   JArray Variables()=>new(Enumerable.Range(0,16).Select(i=>new JObject {["name"]="data"+i,["initial"]=array.DeepClone()}));
   source["state"]=Variables();source["functions"][0]["locals"]=Variables();source["functions"][0]["body"]=new JArray();
   var machine=Machine(source);Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Failed));Assert.That(machine.Error,Does.Contain("retained-value"));
  }
  [Test] public void ValueCopiesFieldsAndListsAreImmutableAndBounded(){
   var value=ProgramValue.Literal(JObject.Parse("{\"id\":\"ball\",\"red\":0.2}"));var changed=value.Operation("withField",new ProgramValue("red"),new ProgramValue(.9));
   Assert.That((double)((JObject)value.Value)["red"],Is.EqualTo(.2));Assert.That((double)((JObject)changed.Value)["red"],Is.EqualTo(.9));
   Assert.That(value.Same(ProgramValue.Literal(JObject.Parse("{\"red\":0.2,\"id\":\"ball\"}"))),Is.True);
   Assert.That(ProgramValue.Literal(new JObject {["n"]=1}).Same(ProgramValue.Literal(new JObject {["n"]=1.0})),Is.True,"Number equality must not depend on JSON integer/float encoding");
   var full=ProgramValue.Literal(new JArray(Enumerable.Range(0,32)));Assert.Throws<ProgramFault>(()=>full.Operation("append",new ProgramValue(33),default));
   var removed=full.Operation("remove",new ProgramValue(0),default);Assert.That(((JArray)removed.Value).Count,Is.EqualTo(31));Assert.That(((JArray)full.Value).Count,Is.EqualTo(32));
  }
 }
}
