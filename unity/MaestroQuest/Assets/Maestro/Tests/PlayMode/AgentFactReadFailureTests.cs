// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        [UnityTest] public IEnumerator AgentImageFactFailureKeepsAcknowledgementSceneAndCompletedReceipt() {
            yield return AgentFactReadFailureFlow("image");
        }
        [UnityTest] public IEnumerator AgentAudioFactFailureKeepsAcknowledgementSceneAndCompletedReceipt() {
            yield return AgentFactReadFailureFlow("audio");
        }
        IEnumerator AgentFactReadFailureFlow(string kind) {
            var agent=root.AddComponent<RoomAgent>();agent.Initialize(editor,null);
            string client=Guid.NewGuid().ToString("N");
            agent.Receive(new JObject {["clientId"]=client}.ToString());string session=agent.Observe().session;
            string target=editor.Identity(block),run=runtime.Scheduler.Receipts.NextId;
            string Wire(int sequence,JObject command,JObject offers=null,bool editing=false) {
                var wire=new JObject {["clientId"]=client,["session"]=session,["request"]=new JObject {
                    ["version"]=2,["session"]=session,["sequence"]=sequence,["sceneRevision"]=editor.Revision,
                    ["conditions"]=editing?new JArray(new JObject {["id"]=target,["revision"]=editor.ObjectRevision(target)}):new JArray(),
                    ["commands"]=new JArray(command)}};
                if(offers!=null)wire[kind=="image"?"images":"sounds"]=offers.DeepClone();
                return wire.ToString(Newtonsoft.Json.Formatting.None);
            }
            var start=new JObject {["action"]="execution",["execution"]=new JObject {
                ["operation"]="start",["runId"]=run,
                ["call"]=ObjectEditCall("object.color.set",target,("red",.6),("green",.3),("blue",.1))}};
            agent.Receive(Wire(1,start,editing:true));var state=agent.Observe();
            Assert.That(state.ack,Is.EqualTo(1));Assert.That(state.ok,Is.True,state.status);
            float until=Time.realtimeSinceStartup+10;
            while((string)runtime.Scheduler.Invocation(run)?["phase"] is "preparing" or "running") {
                Assert.Less(Time.realtimeSinceStartup,until,"Initial edit failed to finish");yield return null;
            }
            var receipt=runtime.Scheduler.Invocation(run);
            Assert.That((string)receipt?["phase"],Is.EqualTo("completed"),receipt?.ToString());
            string scene=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;bool undo=editor.CanUndo;
            var selected=agent.Observe().execution["selected"].DeepClone();
            string fact=kind+".chat.library",hashKey=kind=="image"?"imageHash":"assetHash";
            var offers=new JObject {["offerSet"]=new string('a',32),["offers"]=new JArray(new JObject {
                [hashKey]=new string('b',64),["name"]="Private fixture label",["messageId"]=new string('\u2028',128),
                ["origin"]="generated",["bytes"]=1024})};
            JObject Query(string id,JObject arguments=null) {
                var query=new JObject {["operation"]="inspect",["category"]="facts",["capability"]=id,["version"]=1};
                if(arguments!=null)query["arguments"]=arguments;
                return new JObject {["action"]="catalog",["catalog"]=query};
            }
            var inspect=Query(fact,new JObject {["offset"]=0});string failedWire=Wire(2,inspect,offers);
            LogAssert.Expect(LogType.Error,"Native fact read failed: "+fact+" v1 (invalid typed value).");
            Assert.DoesNotThrow(()=>agent.Receive(failedWire));
            for(int i=0;i<4;i++) {
                state=agent.Observe();Assert.That(state.ack,Is.EqualTo(2));
                Assert.That(state.ok,Is.True,"A passive query was accepted; its fact is explicitly unavailable.");
                Assert.That((bool)state.catalog["available"],Is.False);Assert.That(state.catalog["value"].Type,Is.EqualTo(JTokenType.Null));
                Assert.That((string)state.catalog["status"],Is.EqualTo(RoomCapabilityCatalog.FactReadFailureStatus));
                Assert.That(JToken.DeepEquals(state.catalog["arguments"],new JObject {["offset"]=0}),Is.True);
                Assert.That(JToken.DeepEquals(state.execution["selected"],selected),Is.True);
                Assert.That(JToken.DeepEquals(runtime.Scheduler.Invocation(run),receipt),Is.True);
                Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(scene));Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(editor.CanUndo,Is.EqualTo(undo));
                agent.Receive(failedWire);yield return null;
            }
            agent.Receive(Wire(3,Query("physics.ready")));state=agent.Observe();
            Assert.That(state.ack,Is.EqualTo(3));Assert.That(state.ok,Is.True);
            Assert.That((bool)state.catalog["available"],Is.True);Assert.That((bool)state.catalog["value"],Is.False,"Valid false remains a real value.");
            offers["offerSet"]=new string('c',32);offers["offers"][0]["messageId"]="short-message";
            agent.Receive(Wire(4,inspect,offers));state=agent.Observe();
            Assert.That(state.ack,Is.EqualTo(4));Assert.That((bool)state.catalog["available"],Is.True);
            Assert.That((string)state.catalog["value"]["entries"][0][hashKey],Is.EqualTo(new string('b',64)));
            Assert.That(JToken.DeepEquals(state.execution["selected"],selected),Is.True);
            Assert.That(JToken.DeepEquals(runtime.Scheduler.Invocation(run),receipt),Is.True);
            Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(scene));Assert.That(editor.Revision,Is.EqualTo(revision));
            Assert.That(editor.GetComponent<ImageImportWorkshop>().Chat.Request(),Is.Null);
            Assert.That(editor.GetComponent<AudioImportWorkshop>().Chat.Request(),Is.Null);
        }
    }
}
