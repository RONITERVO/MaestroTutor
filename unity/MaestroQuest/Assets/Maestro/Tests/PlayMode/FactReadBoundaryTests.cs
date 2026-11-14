// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        static JObject FactBoundaryQuery(string id)=>new(){["operation"]="inspect",["category"]="facts",["capability"]=id,["version"]=1,["arguments"]=new JObject{["offset"]=0}};
        static void AssertFailedFact(JObject observed)
        {
            Assert.That((bool)observed["available"],Is.False);Assert.That(observed["value"].Type,Is.EqualTo(JTokenType.Null));
            Assert.That((string)observed["status"],Is.EqualTo(RoomCapabilityCatalog.FactReadFailureStatus));
            Assert.That((string)observed["capability"],Is.EqualTo("image.chat.library"));Assert.That((int)observed["version"],Is.EqualTo(1));
            Assert.That((int)observed["arguments"]["offset"],Is.Zero);Assert.That(observed["definition"].Type,Is.EqualTo(JTokenType.Object));
        }
        [UnityTest] public IEnumerator PassiveFactFailureKeepsPublishingAndRecoversWithoutEditingTheRoom()
        {
            var imports=editor.GetComponent<ImageImportWorkshop>();var wire=ChatOffers(ImageFiles.Png());imports.SyncChat("fact-boundary",wire);
            var catalog=new RoomCapabilityCatalog(editor);var query=FactBoundaryQuery("image.chat.library");
            Assert.That(catalog.Execute(query,out var status),Is.True,status);Assert.That((bool)catalog.Observe()["available"],Is.True);
            int revision=editor.Revision;string roomBefore=JsonUtility.ToJson(editor.Snapshot());
            wire["offerSet"]=new string('b',32);wire["offers"][0]["messageId"]=new string('\u2028',128);wire["offers"][0]["name"]=new string('\u2028',80);
            var original=wire.DeepClone();imports.SyncChat("fact-boundary",wire);
            LogAssert.Expect(LogType.Error,"Native fact read failed: image.chat.library v1 (invalid typed value).");
            for(int i=0;i<4;i++){AssertFailedFact(catalog.Observe());yield return null;}
            Assert.That(catalog.Execute(query,out status),Is.True);Assert.That(status,Is.EqualTo(RoomCapabilityCatalog.FactReadFailureStatus));
            AssertFailedFact(catalog.Observe());
            Assert.That(JToken.DeepEquals(wire,original),Is.True);Assert.That(imports.Chat.OfferSet,Is.EqualTo((string)wire["offerSet"]));
            Assert.That(imports.Chat.CanSelect((string)wire["offerSet"],(string)wire["offers"][0]["imageHash"],out _),Is.True);
            Assert.That(imports.Chat.Request(),Is.Null,"Reading must not start a transfer");
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(roomBefore));
            wire["offerSet"]=new string('c',32);wire["offers"][0]["messageId"]="restored-message";wire["offers"][0]["name"]="Restored image";imports.SyncChat("fact-boundary",wire);
            var recovered=catalog.Observe();Assert.That((bool)recovered["available"],Is.True,recovered.ToString());
            Assert.That((string)recovered["value"]["entries"][0]["messageId"],Is.EqualTo("restored-message"));
            Assert.That(catalog.Execute(new JObject{["operation"]="inspect",["category"]="facts",["capability"]="physics.ready",["version"]=1},out status),Is.True,status);
            var healthy=catalog.Observe();Assert.That((bool)healthy["available"],Is.True);
            Assert.That((bool)healthy["value"],Is.False,"The fixture has no accepted ground; reliable false is still available.");
            // Diagnostics are bounded by registered identity/version, not queries or recovery cycles.
            imports.SyncChat("fact-boundary",(JObject)original);Assert.That(catalog.Execute(query,out status),Is.True);AssertFailedFact(catalog.Observe());
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest] public IEnumerator PassiveFactFailureBoundaryDoesNotBypassRuntimeFocusGating()
        {
            var imports=editor.GetComponent<ImageImportWorkshop>();var wire=ChatOffers(ImageFiles.Png());
            wire["offers"][0]["messageId"]=new string('\u2028',128);imports.SyncChat("fact-gating",wire);
            var catalog=new RoomCapabilityCatalog(editor);runtime.SendMessage("OnApplicationFocus",false);
            try {
                Assert.That(catalog.Execute(FactBoundaryQuery("image.chat.library"),out _),Is.True);
                var unavailable=catalog.Observe();Assert.That((bool)unavailable["available"],Is.False);Assert.That(unavailable["value"].Type,Is.EqualTo(JTokenType.Null));
                Assert.That((string)unavailable["status"],Does.Contain("currently unavailable"));LogAssert.NoUnexpectedReceived();
            }finally{runtime.SendMessage("OnApplicationFocus",true);}
            // Focus loss clears the chat offers; reshare them as the browser would on resume.
            // The selected passive request is retained and now reports its genuine failure.
            imports.SyncChat("fact-gating",wire);
            LogAssert.Expect(LogType.Error,"Native fact read failed: image.chat.library v1 (invalid typed value).");
            AssertFailedFact(catalog.Observe());yield return null;
        }
    }
}
