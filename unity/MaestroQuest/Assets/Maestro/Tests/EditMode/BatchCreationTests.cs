// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.IO;
using System.Linq;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class BatchCreationTests
    {
        static JObject Source()=>JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-batch-create.json")));
        static ProgramMachine Machine(JObject source) {Assert.That(BehaviourProgram.TryParse(source.ToString(Newtonsoft.Json.Formatting.None),out var program,out var error),Is.True,error);return new ProgramMachine(program,null);}
        [Test] public void BatchResultListGrantsExactlyTheCreatedIdsToSubsequentProgramActions() {
            var machine=Machine(Source());Assert.That(machine.Advance(out var action),Is.EqualTo(ProgramYield.Action),machine.Error);Assert.That(action.Resources,Is.Empty);
            var ids=new JArray(Enumerable.Range(1,6).Select(i=>i.ToString("x32")));var slots=new JArray(Enumerable.Range(1,6).Select(i=>"brick_"+i));
            Assert.That(machine.CompleteAction(new JObject { ["objectIds"]=ids,["slots"]=slots,["temporary"]=false},out var error),Is.True,error);ids[0]=new string('f',32);
            int paints=0;ProgramYield state;do {state=machine.Advance(out action);if(state==ProgramYield.Action){paints++;Assert.That((string)action.Arguments["target"],Is.EqualTo(paints.ToString("x32")));Assert.That(machine.CompleteAction(new JObject(),out error),Is.True,error);}}while(state==ProgramYield.Action||state==ProgramYield.Yield);
            Assert.That(state,Is.EqualTo(ProgramYield.Completed),machine.Error);Assert.That(paints,Is.EqualTo(6));
        }
        [Test] public void WholeBatchBudgetIsReservedBeforeTheCallAndExistingTargetResultsCostNoCreations() {
            var source=Source();var create=(JObject)source["functions"][0]["body"][0];var first=create["arguments"]["blueprint"]["pieces"][0];
            create["arguments"]["blueprint"]["pieces"]=new JArray(Enumerable.Range(0,16).Select(i=>{var p=first.DeepClone();p["slot"]="piece_"+i;return p;}));
            var settings=new JObject { ["id"]="settings",["op"]="invoke",["capability"]="object.physics.configure",["version"]=1,["arguments"]=new JObject { ["target"]=new string('0',32),["revision"]=1,["mode"]="solid",["shape"]="automatic",["mass"]=.5},["bindings"]=JObject.Parse("{\"target\":{\"op\":\"at\",\"args\":[{\"var\":\"pieces\"},{\"value\":15}]}}")};
            var extra=new JObject { ["id"]="too_many",["op"]="invoke",["capability"]="object.create",["version"]=1,["arguments"]=BehaviourCatalog.Action("object.create").Example,["bindings"]=new JObject()};source["functions"][0]["body"]=new JArray(create,settings,extra);var machine=Machine(source);
            Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Action));var ids=new JArray(Enumerable.Range(1,16).Select(i=>i.ToString("x32")));
            Assert.That(machine.CompleteAction(new JObject { ["objectIds"]=ids,["slots"]=new JArray(Enumerable.Range(0,16).Select(i=>"piece_"+i)),["temporary"]=false},out var error),Is.True,error);
            Assert.That(machine.Advance(out var call),Is.EqualTo(ProgramYield.Action),machine.Error);Assert.That(call.Definition.Id,Is.EqualTo("object.physics.configure"));
            Assert.That(machine.CompleteAction(new JObject { ["target"]=ids[15].DeepClone(),["revision"]=2,["temporary"]=false},out error),Is.True,error);
            Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Failed));Assert.That(machine.Error,Does.Contain("16 created"));
            source["functions"][0]["body"]=new JArray(extra.DeepClone(),create.DeepClone());machine=Machine(source);
            Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Action));Assert.That(machine.CompleteAction(new JObject { ["objectId"]=new string('f',32)},out error),Is.True,error);
            Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Failed),"A 16-piece batch must reserve all 16 before executing, even when only one earlier object exists");
        }
        [Test] public void TypedEmptyOutputListsWorkAndInvalidBoundValuesDoNotPartiallyReplaceLocals() {
            var source=Source();var entryType=JObject.Parse("{\"list\":{\"record\":{\"modelHash\":\"text\",\"name\":\"text\",\"bytes\":\"number\"}}}");
            source["functions"][0]["locals"]=new JArray(new JObject { ["name"]="entries",["initial"]=new JArray(),["type"]=entryType});
            source["functions"][0]["body"]=new JArray(new JObject { ["id"]="read",["op"]="invoke",["capability"]="model.library.inspect",["version"]=1,["arguments"]=new JObject { ["offset"]=0},["bindings"]=new JObject(),["results"]=new JObject { ["entries"]="entries"}});
            var machine=Machine(source);machine.Advance(out _);
            var tooLarge=new JObject { ["total"]=8,["offset"]=0,["entries"]=new JArray(Enumerable.Range(0,8).Select(i=>new JObject { ["modelHash"]=new string('a',64),["name"]=new string('n',120),["bytes"]=2000000}))};
            Assert.That(machine.CompleteAction(tooLarge,out _),Is.False);Assert.That(((JArray)machine.Locals["entries"].Value).Count,Is.EqualTo(0));
            Assert.That(machine.CompleteAction(new JObject { ["total"]=0,["offset"]=0,["entries"]=new JArray()},out var error),Is.True,error);Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Completed));
        }
        [Test] public void BlueprintRejectsDuplicateSlotsPlayingPiecesAndInvalidComposedPlacement() {
            var args=(JObject)Source()["functions"][0]["body"][0]["arguments"];Assert.That(BehaviourCatalog.TryCall("object.batch.create",1,args,out _,out var error),Is.True,error);
            args["blueprint"]["pieces"][1]["slot"]=args["blueprint"]["pieces"][0]["slot"].DeepClone();Assert.That(BehaviourCatalog.TryCall("object.batch.create",1,args,out _,out _),Is.False);
            args=(JObject)Source()["functions"][0]["body"][0]["arguments"];args["position"]["x"]=25;Assert.That(BehaviourCatalog.TryCall("object.batch.create",1,args,out _,out _),Is.False);
            args=(JObject)Source()["functions"][0]["body"][0]["arguments"];args["scale"]=4;args["blueprint"]["pieces"][0]["scale"]=2;Assert.That(BehaviourCatalog.TryCall("object.batch.create",1,args,out _,out _),Is.False);
            args=(JObject)Source()["functions"][0]["body"][0]["arguments"];var recipe=Maestro.Quest.Creation.RecipeTemplates.BoxRobot(true);args["blueprint"]["pieces"][0]["source"]=new JObject { ["kind"]="recipe",["recipe"]=JObject.Parse(JsonUtility.ToJson(recipe))};Assert.That(BehaviourCatalog.TryCall("object.batch.create",1,args,out _,out _),Is.False);
        }
    }
}
