// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.IO;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
    public sealed class CatalogCallBoundsTests {
        static IEnumerable Cases() {
            var cases=JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/catalog-call-bounds.json")));
            foreach(var value in cases)yield return new TestCaseData(value).SetName("CatalogCallBounds_"+(string)value["name"]);
        }
        [TestCaseSource(nameof(Cases))] public void SharedTransportAndDomainBounds(JToken value) {
            var call=(JObject)value["call"];bool bounded=(bool)value["bounded"],valid=(bool)value["valid"];
            Assert.That(RoomCapabilityCatalog.ValidCall(call),Is.EqualTo(bounded),"Structural transport bounds");
            Assert.That(RoomCapabilityCatalog.ValidRequest(new JObject{["operation"]="check",["call"]=call.DeepClone()}),Is.EqualTo(bounded));
            Assert.That(RoomExecutions.ValidRequest(new JObject{["operation"]="start",["call"]=call.DeepClone()}),Is.EqualTo(bounded));
            Assert.That(BehaviourCatalog.TryCall((string)call["id"],(int)call["version"],(JObject)call["arguments"],out _,out var error),Is.EqualTo(valid),error);
        }
    }
}
