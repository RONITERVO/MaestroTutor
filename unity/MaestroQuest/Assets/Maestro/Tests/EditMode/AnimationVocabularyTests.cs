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
    public sealed class AnimationVocabularyTests
    {
        static JArray Cases()=>JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/animation-contract.json")));
        [Test] public void SharedAnimationVariantsValidateExactSourcesAndOwnOnlyTheirSelectedChannels() {
            foreach(var entry in Cases()) {
                var source=entry["call"];bool valid=BehaviourCatalog.TryCall((string)source["id"],1,(JObject)source["arguments"],out var call,out var error);
                Assert.That(valid,Is.EqualTo((bool)entry["valid"]),(string)entry["name"]+": "+error);
                if(valid) {Assert.That(call.Claims.Select(x=>x.Channel),Is.EqualTo(entry["channels"].Values<string>()),(string)entry["name"]);Assert.That(JToken.DeepEquals(call.Arguments,source["arguments"]),Is.True);}
            }
            Assert.That(BehaviourCatalog.Actions.Count(x=>x.Id=="animation.play"),Is.EqualTo(1));
            foreach(string retired in new[]{"animation.library.play","animation.embedded.play","avatar.gesture.play","avatar.gesture.upperBody","animation.recording.play","animation.recipe.play"})Assert.That(BehaviourCatalog.Action(retired),Is.Null,"Prototype names are not competing public APIs");
        }
        [Test] public void SourceNamesAndPrerequisitesStayDiscoverableUnderOneVerb() {
            var catalog=new Maestro.Quest.Creation.RoomCapabilityCatalog(null);
            foreach(string term in new[]{"recording","library","embedded","recipe","upper","rig.compatible"}) {
                Assert.That(catalog.Execute(new JObject {["operation"]="search",["query"]=term,["offset"]=0},out _),Is.True);
                Assert.That(catalog.Observe()["entries"].Any(x=>(string)x["id"]=="animation.play"),Is.True,term);
            }
        }
        [TestCase("source.kind")] [TestCase("channel")]
        public void SourceAndChannelSelectorsCannotChangeThroughExpressions(string selector) {
            var source=JObject.Parse(BehaviourProgram.FromInvocation((JObject)Cases()[0]["call"]));
            source["functions"][0]["body"][0]["bindings"][selector]=new JObject {["value"]=selector=="channel"?"upperBody":"recording"};
            Assert.That(BehaviourProgram.TryParse(source.ToString(),out _,out var error),Is.False);Assert.That(error,Does.Contain("literal"));
        }
        [Test] public void NestedResourceBindingRetainsNativeAuthorityAndNeverEditsItsLiteralSource() {
            var source=JObject.Parse(BehaviourProgram.FromInvocation((JObject)Cases().Last()["call"]));source["version"]=3;source["state"]=new JArray();source["events"]=new JArray();source["resources"]=new JArray("maestro");
            string target=new string('d',32);var node=source["functions"][0]["body"][0];node["bindings"]["prop.objectId"]=new JObject {["value"]=target};
            Assert.That(BehaviourProgram.TryParse(source.ToString(),out var program,out var error),Is.True,error);
            var machine=new ProgramMachine(program,null);Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Failed));Assert.That(machine.Error,Does.Contain("resource"));
            ((JArray)source["resources"]).Add(target);Assert.That(BehaviourProgram.TryParse(source.ToString(),out program,out error),Is.True,error);
            machine=new ProgramMachine(program,null);Assert.That(machine.Advance(out var call),Is.EqualTo(ProgramYield.Action),machine.Error);
            Assert.That((string)call.Arguments["prop"]["objectId"],Is.EqualTo(target));Assert.That(call.Resources,Is.EquivalentTo(new[]{"maestro",target}));
            Assert.That((string)node["arguments"]["prop"]["objectId"],Is.EqualTo(new string('c',32)));
            ((JObject)node["arguments"]).Remove("prop");Assert.That(BehaviourProgram.TryParse(source.ToString(),out _,out error),Is.False);Assert.That(error,Does.Contain("placeholder"));
        }
    }
}
