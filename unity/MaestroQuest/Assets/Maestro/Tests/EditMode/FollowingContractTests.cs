// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.IO;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Tests {
    public sealed class FollowingContractTests {
        static IEnumerable Cases(){
            var cases=JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/following-contract.json")));
            foreach(var value in cases)yield return new TestCaseData(value).SetName("FollowingContract_"+(string)value["name"]);
        }
        [TestCaseSource(nameof(Cases))]public void SharedFollowingContract(JToken value){
            var args=(JObject)value["arguments"];var original=args.ToString();
            Assert.That(CapabilityArguments.Validate(args,new FollowObjectCapability().InputSchema,out _),Is.EqualTo((bool?)(value["schemaValid"]??value["valid"])));
            Assert.That(BehaviourCatalog.TryCall("object.follow",1,args,out _,out var error),Is.EqualTo((bool)value["valid"]),error);
            Assert.That(args.ToString(),Is.EqualTo(original),"Resolving a branch never rewrites a saved invocation");
        }
        [Test]public void OptionalSelectorRequiresOneUnambiguousDeclaredBranch(){
            JObject Branch(string mode,bool optional)=>Object(new JObject{["mode"]=Choice(mode)},optional?new[]{"mode"}:System.Array.Empty<string>());
            var schema=new JObject{["oneOf"]=new JArray(Branch("walk",true),Branch("swim",false)),["x-discriminators"]=new JArray("mode")};
            Assert.That(Resolve(schema,new JObject()),Is.SameAs(schema["oneOf"][0]));
            foreach(var invalid in new JToken[]{JValue.CreateNull(),new JValue("fly"),new JValue(1)})
                Assert.That(Resolve(schema,new JObject{["mode"]=invalid}),Is.Null);
            ((JArray)schema["oneOf"]).Add(Branch("surface",true));Assert.That(Resolve(schema,new JObject()),Is.Null);
        }
    }
}
