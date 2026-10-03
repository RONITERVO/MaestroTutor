// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        string LayoutObject(Vector3 position) {Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Layout piece",position,1,Color.white,out var id,out var error),Is.True,error);return id;}
        RoomLayout Layout(params string[] ids)=>new() {placements=ids.Select((id,i)=>new ObjectPlacement {target=id,position=new Vector3(2+i*.2f,1,0),rotation=Quaternion.Euler(0,i*30,0),scale=1}).ToArray()};
        JObject LayoutCall(RoomLayout layout)=>new() { ["id"]="object.layout.apply",["version"]=1,["arguments"]=JObject.Parse(JsonUtility.ToJson(layout))};
        RoomAgentRequest LayoutRequest(RoomLayout layout) {var request=ObjectEditRequest(LayoutCall(layout));request.conditions=layout.placements.Select(p=>new RoomObjectCondition {id=p.target,revision=editor.ObjectRevision(p.target)}).ToArray();return request;}
        [UnityTest] public IEnumerator LayoutSavesAllMembersOnceAndUndoRestoresLiveBeforePosesWithoutReceiptReplay() {
            string a=LayoutObject(new Vector3(2,1,0)),b=LayoutObject(new Vector3(2.2f,1,0));var layout=Layout(a,b);layout.placements[1].rotation=Quaternion.identity;
            // Simulate motion between physics snapshot ticks, leaving the saved baseline unchanged.
            editor.Find(a).transform.localPosition=new Vector3(3,2,1);editor.Find(b).transform.localPosition=new Vector3(3.2f,2,1);
            var executor=new RoomAgentExecutor(editor);var request=LayoutRequest(layout);
            Assert.That(executor.Execute(request,out var error,out _),Is.True,error);
            Assert.That((int)executor.Executions.Observe()["selected"]["output"]["count"],Is.EqualTo(2));
            var disk=new RoomStorage(directory).Load(out error);foreach(var p in layout.placements) {Assert.That(editor.Read(p.target).position,Is.EqualTo(p.position));Assert.That(disk.objects.Single(d=>d.id==p.target).position,Is.EqualTo(p.position));}
            editor.Undo();Assert.That(editor.Find(a).transform.localPosition,Is.EqualTo(new Vector3(3,2,1)));Assert.That(editor.Find(b).transform.localPosition,Is.EqualTo(new Vector3(3.2f,2,1)));
            Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(editor.Find(a).transform.localPosition,Is.EqualTo(new Vector3(3,2,1)),"Replay cannot reset again after Undo");
            editor.Redo();Assert.That(editor.Find(a).transform.localPosition,Is.EqualTo(layout.placements[0].position));
            yield return null;
        }
        [UnityTest] public IEnumerator LayoutRefusesStaleHeldConflictingMissingAndFailedSaveWithoutMovingEarlierMembers() {
            string a=LayoutObject(new Vector3(3,2,1)),b=editor.Identity(block);var layout=Layout(a,b);var executor=new RoomAgentExecutor(editor);var before=JsonUtility.ToJson(editor.Snapshot());var request=LayoutRequest(layout);
            request.conditions[1].revision--;Assert.That(executor.Execute(request,out _,out _),Is.False);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));
            var hand=Hand(1,block.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,block.Grab);Assert.That(executor.Execute(LayoutRequest(layout),out _,out _),Is.False);manager.SelectExit((IXRSelectInteractor)hand,block.Grab);yield return null;
            Assert.That(runtime.Trigger(sequenceId),Is.True);Assert.That(executor.Execute(LayoutRequest(layout),out _,out _),Is.False);runtime.Scheduler.StopAll();
            Assert.That(editor.Find(a).transform.localPosition,Is.EqualTo(new Vector3(3,2,1)));
            var missing=Layout(a,new string('e',32));Assert.That(editor.ApplyLayout(missing,out _),Is.False);Assert.That(editor.Read(a).position,Is.EqualTo(new Vector3(3,2,1)));
            string obstacle=Path.Combine(directory,"room.v6.json.pending");Directory.CreateDirectory(obstacle);before=JsonUtility.ToJson(editor.Snapshot());
            try {Assert.That(executor.Execute(LayoutRequest(layout),out _,out _),Is.False);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));Assert.That(editor.Find(a).transform.localPosition,Is.EqualTo(new Vector3(3,2,1)));}finally{Directory.Delete(obstacle);}
        }
        [UnityTest] public IEnumerator LivePlacementUsesRoomAxesAndAlreadyMatchingLayoutUpdatesSavedPoseWithoutEmptyUndo() {
            string id=LayoutObject(new Vector3(3,2,1));var layout=Layout(id);root.transform.position=new Vector3(10,0,0);editor.Find(id).transform.localPosition=layout.placements[0].position;
            var actions=new Maestro.Quest.Rules.RoomRuleActions(editor,animations);Assert.That(actions.TryRead("object.placement",1,new JObject { ["target"]=id},out var fact),Is.True);
            Assert.That((float)((JObject)fact.Value)["position"]["x"],Is.EqualTo(2));Assert.That(editor.Read(id).position.x,Is.EqualTo(3));
            Assert.That(editor.ApplyLayout(layout,out var error),Is.True,error);Assert.That(editor.Read(id).position.x,Is.EqualTo(2));Assert.That(new RoomStorage(directory).Load(out _).objects.Single(d=>d.id==id).position.x,Is.EqualTo(2));
            editor.Undo();Assert.That(editor.Read(id),Is.Null,"An identical live placement must not add an empty Undo before creation");
            using(editor.RuntimeGate.Hold("Test paused room"))Assert.That(actions.TryRead("object.placement",1,new JObject { ["target"]=editor.Identity(block)},out _),Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator LayoutInTemporaryPlayDoesNotChangeSavedRoomAndDiscardRestoresBothMembers() {
            string a=LayoutObject(new Vector3(3,2,1)),b=LayoutObject(new Vector3(3.2f,2,1));
            yield return null;Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);
            float end=Time.realtimeSinceStartup+5;while(editor.TemporarySavePending&&Time.realtimeSinceStartup<end)yield return null;Assert.That(editor.TemporarySavePending,Is.False);
            var executor=new RoomAgentExecutor(editor);Assert.That(executor.Execute(LayoutRequest(Layout(a,b)),out error,out _),Is.True,error);Assert.That((bool)executor.Executions.Observe()["selected"]["output"]["temporary"],Is.True);
            Assert.That(new RoomStorage(directory).Load(out _).objects.Single(d=>d.id==a).position.x,Is.EqualTo(3));
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.Find(a).transform.localPosition.x,Is.EqualTo(3));Assert.That(editor.Find(b).transform.localPosition.x,Is.EqualTo(3.2f));
        }
        [UnityTest] public IEnumerator LooseBrickStructureCanSettleBeKnockedDownAndResetThroughSharedLayout() {
            RoomPhysicsLayers.Configure();var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform,false);floor.transform.position=new Vector3(2,-.1f,0);floor.transform.localScale=new Vector3(3,.2f,3);floor.layer=RoomPhysicsLayers.Scanned;
            var executor=new RoomAgentExecutor(editor);var entry=CreationTemplates.All.First(e=>e.Id=="brick");var ids=new string[6];
            for(int i=0;i<ids.Length;i++) {Assert.That(editor.CreateRecipe("Castle brick",new Vector3(2+(i%2)*.25f,.04f+(i/2)*.08f,0),1,entry.Recipe,entry.Collision,entry.Physics,out ids[i],out var error),Is.True,error);}
            var baseline=new RoomLayout {placements=ids.Select(id=>ObjectPlacement.Capture(id,editor.Find(id).transform)).ToArray()};Physics.SyncTransforms();physics.SetSurfaces(true,"Synthetic castle floor");physics.StartPhysics();
            for(int i=0;i<100;i++)yield return new WaitForFixedUpdate();
            Assert.That(editor.Find(ids[4]).transform.localPosition.y,Is.GreaterThan(.12f),"The initial top brick must settle on its stack");
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Ball,"Knockdown ball",new Vector3(2,.16f,-.6f),.7f,Color.white,out var ballId,out var createError),Is.True,createError);
            // Reset the projectile too: leaving it inside the rebuilding stack makes
            // subsequent stability a different physical question than layout reset.
            baseline.placements=baseline.placements.Append(ObjectPlacement.Capture(ballId,editor.Find(ballId).transform)).ToArray();
            yield return new WaitForFixedUpdate();var ballBody=editor.Find(ballId).GetComponent<Rigidbody>();ballBody.linearVelocity=Vector3.forward*3;
            for(int i=0;i<100;i++)yield return new WaitForFixedUpdate();
            Assert.That(ids.Any(id=>Vector3.Distance(editor.Find(id).transform.localPosition,baseline.placements.Single(p=>p.target==id).position)>.1f),Is.True,"A real ball contact must displace at least one brick");
            Assert.That(executor.Execute(LayoutRequest(baseline),out var error2,out _),Is.True,error2);
            foreach(var p in baseline.placements) {var item=editor.Find(p.target);Assert.That(Vector3.Distance(item.transform.localPosition,p.position),Is.LessThan(.001f));Assert.That(item.GetComponent<Rigidbody>().linearVelocity,Is.EqualTo(Vector3.zero));}
            Assert.That(physics.Running,Is.True);for(int i=0;i<100;i++)yield return new WaitForFixedUpdate();Assert.That(editor.Find(ids[4]).transform.localPosition.y,Is.GreaterThan(.12f),"Rebuilt top brick: "+editor.Find(ids[4]).transform.localPosition+"; reset ball: "+editor.Find(ballId).transform.localPosition);
        }
    }
}
