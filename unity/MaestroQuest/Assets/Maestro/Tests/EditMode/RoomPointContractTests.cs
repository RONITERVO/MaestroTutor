// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.IO;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class RoomPointContractTests
    {
        [TestCase("program-contact.json")] [TestCase("program-physical-catch.json")] [TestCase("program-anchor-zone.json")]
        public void OldOrMissingPointEventVersionsArePreservedButRefused(string file)
        {
            var source=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures",file)));
            Assert.That(BehaviourProgram.TryParse(source.ToString(),out _,out var error),Is.True,error);
            var node=file=="program-contact.json"?source["functions"][0]["body"][0]["body"][0]:source["functions"][0]["body"][0];
            foreach(JToken version in new JToken[]{new JValue(1),new JValue(3),new JValue("2"),JValue.CreateNull()}) {
                node["version"]=version;string saved=source.ToString();
                Assert.That(BehaviourProgram.TryParse(saved,out _,out error),Is.False);
                Assert.That(error,Does.Contain("version"));Assert.That(source.ToString(),Is.EqualTo(saved));
            }
            ((JObject)node).Remove("version");Assert.That(BehaviourProgram.TryParse(source.ToString(),out _,out error),Is.False);Assert.That(error,Does.Contain("version"));
        }
        [TestCase("object.position")] [TestCase("object.anchor")] [TestCase("object.recipe.pose")] [TestCase("object.physics.trajectory")]
        public void SpatialFactsRequireTheirCurrentCoordinateContract(string id)
        {
            var fact=BehaviourCatalog.Fact(id);Assert.That(fact.Version,Is.EqualTo(2));var args=(JObject)fact.ToJson()["example"];
            Assert.That(fact.ValidArguments(1,args,out _),Is.False);Assert.That(fact.ValidArguments(2,args,out var error),Is.True,error);
        }
        [Test] public void OldResettingViewContractRequiresExplicitReview()
        {
            var action=BehaviourCatalog.Action("controller.mode.set");Assert.That(action.Version,Is.EqualTo(2));
            Assert.That(BehaviourCatalog.TryCall(action.Id,1,action.Example,out _,out _),Is.False);
            Assert.That(BehaviourCatalog.TryCall(action.Id,2,action.Example,out _,out var error),Is.True,error);
        }
        [Test] public void OldPhysicalLaunchCannotSilentlyUseAuthoredCoordinates()
        {
            var action=BehaviourCatalog.Action("object.physics.launch");Assert.That(action.Version,Is.EqualTo(2));
            Assert.That(BehaviourCatalog.TryCall(action.Id,1,action.Example,out _,out _),Is.False);
            Assert.That(BehaviourCatalog.TryCall(action.Id,2,action.Example,out _,out var error),Is.True,error);
        }
    }
}
