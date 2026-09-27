// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.IO;
using System.Linq;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class CreationVocabularyTests
    {
        static JArray Cases()=>JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/creation-contract.json")));
        [Test] public void CreationKindsValidateWithoutInventingResourcesAndKeepOneResultContract() {
            foreach(var entry in Cases()) {
                var source=entry["call"];bool valid=BehaviourCatalog.TryCall((string)source["id"],1,(JObject)source["arguments"],out var call,out var error);
                Assert.That(valid,Is.EqualTo((bool)entry["valid"]),(string)entry["name"]+": "+error);
                if(valid) {Assert.That(call.Resources,Is.Empty);Assert.That(call.Claims,Is.Empty);Assert.That(call.Instant,Is.True);Assert.That(JToken.DeepEquals(call.Arguments,source["arguments"]),Is.True);}
            }
            var definition=BehaviourCatalog.Action("object.create");Assert.That((string)definition.OutputSchema["properties"]["objectId"]["x-resource"],Is.EqualTo("object"));
            foreach(string old in new[]{"object.create.primitive","object.create.recipe"})Assert.That(BehaviourCatalog.Action(old),Is.Null);
        }
        [Test] public void VariantExamplesAreCompleteDetachedAndDiscoverable() {
            var definition=BehaviourCatalog.Action("object.create");var shape=definition.InputSchema;
            foreach(var variant in (JArray)shape["oneOf"]) {
                var example=(JObject)variant["examples"][0];Assert.That(BehaviourCatalog.TryCall(definition.Id,1,example,out var call,out var error),Is.True,error);
                Assert.That(call.TryStep(out var step,out error),Is.True,error);
                Assert.That(LegacyCapabilityAdapters.TryCall(step,out var restored,out error),Is.True,error);// Numeric JSON integer/float representation changes through Unity's legacy float fields.
                JObject Numbers(JObject value) {var copy=(JObject)value.DeepClone();foreach(var number in copy.Descendants().OfType<JValue>().Where(v=>v.Type==JTokenType.Integer||v.Type==JTokenType.Float).ToArray())number.Replace(new JValue(number.Value<double>()));return copy;}
                Assert.That(JToken.DeepEquals(Numbers(restored.Arguments),Numbers(example)),Is.True,restored.Arguments+" compared with "+example);
                example["kind"]="changed";
            }
            Assert.That((string)definition.InputSchema["oneOf"][0]["examples"][0]["kind"],Is.EqualTo("primitive"));
            var catalog=new Maestro.Quest.Creation.RoomCapabilityCatalog(null);
            foreach(string query in new[]{"shape","recipe","storage.writable"}) {Assert.That(catalog.Execute(new JObject {["operation"]="search",["query"]=query,["offset"]=0},out _),Is.True);Assert.That(catalog.Observe()["entries"].Any(x=>(string)x["id"]==definition.Id),Is.True,query);}
        }
        [Test] public void CreationKindIsStaticButCoordinatesRemainTypedExpressions() {
            var source=JObject.Parse(BehaviourProgram.FromInvocation((JObject)Cases()[0]["call"]));var node=source["functions"][0]["body"][0];
            node["bindings"]["kind"]=new JObject {["value"]="primitive"};Assert.That(BehaviourProgram.TryParse(source.ToString(),out _,out var error),Is.False);Assert.That(error,Does.Contain("literal"));
            node["bindings"]=new JObject {["x"]=new JObject {["value"]=.4}};Assert.That(BehaviourProgram.TryParse(source.ToString(),out var program,out error),Is.True,error);
            var machine=new ProgramMachine(program,null);Assert.That(machine.Advance(out var call),Is.EqualTo(ProgramYield.Action));Assert.That((double)call.Arguments["x"],Is.EqualTo(.4));
            node["bindings"]["x"]["value"]="wrong type";Assert.That(BehaviourProgram.TryParse(source.ToString(),out _,out _),Is.False);
        }
    }
}
