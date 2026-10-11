// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class ConnectedBlueprintTests
    {
        static JObject Source()=>JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-spring-lever.json")));
        [Test] public void SharedCasesAgreeOnGraphFramesLimitsAndSchemaBoundaries()
        {
            var cases=JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/connected-blueprints-contract.json")));
            foreach(var c in cases)Assert.That(BehaviourCatalog.TryCall("object.batch.create",1,(JObject)c["arguments"],out _,out var error),Is.EqualTo((bool)c["valid"]),(string)c["name"]+": "+error);
        }
        [Test] public void IncludedLeverUsesPinnedOrdinaryProgramSourceAndGrantsExactlyItsNewMembers()
        {
            var source=Source();var module=(JObject)source["imports"][0]["module"];ProgramModuleLibrary.Validate(module);
            Assert.That((string)source["imports"][0]["hash"],Is.EqualTo(ProgramModules.Hash(module)));
            Assert.That(JToken.DeepEquals(module,JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Resources/Programs/Modules/SpringLever.json")))),Is.True);
            Assert.That(BehaviourProgram.TryParse(source.ToString(),out var program,out var error),Is.True,error);var machine=new ProgramMachine(program,null);Assert.That(machine.Advance(out var call),Is.EqualTo(ProgramYield.Action),machine.Error);
            Assert.That(call.Definition.Id,Is.EqualTo("object.batch.create"));Assert.That(call.Resources,Is.Empty);
            var ids=new JArray(new string('a',32),new string('b',32));Assert.That(machine.CompleteAction(new JObject{["objectIds"]=ids,["slots"]=new JArray("mount","handle"),["temporary"]=false},out error),Is.True,error);
            Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Completed),machine.Error);Assert.That((JArray)machine.Locals["pieces"].Value,Is.EqualTo(ids));
        }
        [Test] public void DataExpansionBindsFreshIdentitiesAndRoundTripsWithoutLeakingRoomIdsIntoSource()
        {
            var args=(JObject)Source()["imports"][0]["module"]["program"]["functions"][1]["body"][0]["arguments"];var batch=JsonUtility.FromJson<CreationBatch>(args.ToString());
            Assert.That(batch.Prepare(out var first,out var error),Is.True,error);Assert.That(batch.Prepare(out var second,out error),Is.True,error);Assert.That(first[1].connections.Single().connected,Is.EqualTo(first[0].id));Assert.That(second[1].connections.Single().connected,Is.EqualTo(second[0].id));Assert.That(first.Select(x=>x.id).Intersect(second.Select(x=>x.id)),Is.Empty);
            var wire=JObject.Parse(JsonUtility.ToJson(batch));Assert.That(((JObject)wire["blueprint"]["connections"][0]["definition"]).Properties().Select(p=>p.Name),Is.EquivalentTo(new[]{"enabled","kind","breakForce","breakTorque","ownerFrame","connectedFrame","limits","drive","slide"}));
            Assert.That(JsonUtility.FromJson<CreationBatch>(wire.ToString()).Prepare(out _,out error),Is.True,error);
            first[1].connections[0].drive.spring=99;Assert.That(batch.blueprint.connections[0].definition.drive.spring,Is.EqualTo(8));
        }
    }
}
