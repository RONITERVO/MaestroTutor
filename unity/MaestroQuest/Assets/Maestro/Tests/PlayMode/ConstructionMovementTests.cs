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
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        RoomGroupTransform GroupMove(params string[] ids)=>new(){members=ids.Select(id=>new TransformMember {target=id,revision=editor.ObjectRevision(id)}).ToArray(),position=new Vector3(0,2,1),rotation=Quaternion.Euler(0,90,0),scale=1.5f};
        RoomAgentRequest GroupRequest(RoomGroupTransform value){var request=ObjectEditRequest(new JObject {["id"]="object.layout.transform",["version"]=1,["arguments"]=JObject.Parse(JsonUtility.ToJson(value))});request.conditions=value.members.Select(m=>new RoomObjectCondition {id=m.target,revision=editor.ObjectRevision(m.target)}).ToArray();return request;}
        [UnityTest] public IEnumerator GroupTransformUsesPivotRoomAxesScaleOneUndoAndExactReceipts(){
            var a=LayoutObject(new Vector3(2,1,0));var b=LayoutObject(new Vector3(2.2f,1,0));root.transform.SetPositionAndRotation(new Vector3(8,0,5),Quaternion.Euler(0,30,0));var value=GroupMove(a,b);var request=GroupRequest(value);var executor=new RoomAgentExecutor(editor);
            Assert.That(executor.Execute(request,out var error,out var created),Is.True,error);Assert.That(created,Is.Empty);Assert.That(Vector3.Distance(editor.Find(a).transform.localPosition,new Vector3(0,2,1)),Is.LessThan(.001f));Assert.That(Vector3.Distance(editor.Find(b).transform.localPosition,new Vector3(0,2,.7f)),Is.LessThan(.001f));
            foreach(var id in new[]{a,b}){Assert.That(editor.Find(id).transform.localScale.x,Is.EqualTo(1.5f));Assert.That(Quaternion.Angle(editor.Find(id).transform.localRotation,value.rotation),Is.LessThan(.01f));}
            Assert.That(new RoomStorage(directory).Load(out _).objects.Single(o=>o.id==b).scale,Is.EqualTo(1.5f));editor.Undo();Assert.That(editor.Find(a).transform.localPosition,Is.EqualTo(new Vector3(2,1,0)));Assert.That(editor.Find(b).transform.localPosition,Is.EqualTo(new Vector3(2.2f,1,0)));Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(editor.Find(a).transform.localPosition.x,Is.EqualTo(2));yield return null;
        }
        [UnityTest] public IEnumerator GroupTransformRefusesStaleLimitsPhysicsAndFailedSaveBeforeMovingAnyMember(){
            var a=LayoutObject(new Vector3(2,1,0));var b=LayoutObject(new Vector3(2.2f,1,0));var before=JsonUtility.ToJson(editor.Snapshot());var value=GroupMove(a,b);value.members[1].revision--;Assert.That(editor.TransformGroup(value,out _),Is.False);
            value=GroupMove(a,b);value.scale=4;Assert.That(editor.ResizeObject(b,2,out var error),Is.True,error);value.members[1].revision=editor.ObjectRevision(b);var prior=editor.Find(a).transform.localPosition;Assert.That(editor.TransformGroup(value,out error),Is.False);Assert.That(editor.Find(a).transform.localPosition,Is.EqualTo(prior));
            value=GroupMove(a,b);physics.SetSurfaces(true,"Ready");physics.StartPhysics();Assert.That(editor.TransformGroup(value,out error),Is.False);physics.PausePhysics();
            value.position=new Vector3(25,0,0);value.rotation=Quaternion.identity;Assert.That(editor.TransformGroup(value,out error),Is.False);value=GroupMove(a,b);
            var obstacle=Path.Combine(directory,"room.v6.json.pending");Directory.CreateDirectory(obstacle);before=JsonUtility.ToJson(editor.Snapshot());try{Assert.That(editor.TransformGroup(value,out error),Is.False);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));Assert.That(editor.Find(a).transform.localPosition,Is.EqualTo(prior));}finally{Directory.Delete(obstacle);}yield return null;
        }
        [UnityTest] public IEnumerator GroupTransformRejectsExternalHingesAndPreservesCompleteInternalFrames(){
            var ex=new RoomAgentExecutor(editor);Assert.That(ex.Execute(TemplateRequest(LeverCall()),out var creationError,out _),Is.True,creationError);var ids=((JArray)ex.Executions.Observe()["selected"]["output"]["objectIds"]).Values<string>().ToArray();var before=editor.Read(ids[1]).hinges[0].Copy();Assert.That(editor.TransformGroup(GroupMove(ids[1]),out var error),Is.False);Assert.That(error,Does.Contain("both ends"));
            Assert.That(editor.TransformGroup(GroupMove(ids),out error),Is.True,error);Assert.That(JsonUtility.ToJson(editor.Read(ids[1]).hinges[0]),Is.EqualTo(JsonUtility.ToJson(before)));Assert.That(before.Aligned(editor.Find(ids[1]).transform,editor.Find(ids[0]).transform,out error),Is.True,error);yield return null;
        }
        ConstructionManipulator Mover(string a,string b){Assert.That(editor.SetConstructionSelection(editor.ObserveConstructionSelection().stateId,new[]{a,b},false,out var error),Is.True,error);Assert.That(ConstructionManipulationCapability.RunManual(editor,true,out error),Is.True,error);return editor.GetComponent<ConstructionManipulator>();}
        void MoveHandle(ConstructionManipulator tool,Vector3 delta){tool.Handle.transform.localPosition+=delta;Assert.That(tool.Preview(out var error),Is.True,error);}
        [UnityTest] public IEnumerator SolidConstructionHandlePreviewsAndReleasesThroughTheSameGroupAction(){
            var a=LayoutObject(new Vector3(2,1,0));var b=LayoutObject(new Vector3(2.2f,1,0));var tool=Mover(a,b);CaptureConstructionHandle(tool);var hand=Hand(1,tool.Handle.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,tool.Handle.Grab);Assert.That(tool.Holding,Is.True);
            Assert.That(editor.Find(a).Grab.enabled,Is.False);Assert.That(editor.Find(b).Grab.enabled,Is.False);Assert.That(editor.Ownership.Observe().owners.Single(o=>o.id=="construction-handle").claims.Length,Is.EqualTo(2));MoveHandle(tool,new Vector3(0,.3f,.4f));
            Assert.That(editor.Read(a).position,Is.EqualTo(new Vector3(2,1,0)),"Preview must not save");Assert.That(Vector3.Distance(editor.Find(b).transform.localPosition,new Vector3(2.2f,1.3f,.4f)),Is.LessThan(.001f));Assert.That(editor.SetConstructionSelection(editor.ObserveConstructionSelection().stateId,Array.Empty<string>(),false,out _),Is.False);
            var moveId=runtime.Scheduler.Receipts.NextId;hand.selectInput.manualPerformed=false;hand.selectInput.manualValue=0;manager.SelectExit((IXRSelectInteractor)hand,tool.Handle.Grab);Assert.That(tool.Holding,Is.False);Assert.That(editor.Find(a).Grab.enabled,Is.True);Assert.That(editor.Find(b).Grab.enabled,Is.True);
            Assert.That(Vector3.Distance(editor.Read(a).position,new Vector3(2,1.3f,.4f)),Is.LessThan(.001f));var receipt=runtime.Scheduler.Invocation(moveId);Assert.That(tool.Error,Is.Empty);
            Assert.That((string)receipt["call"]["id"],Is.EqualTo("object.layout.transform"));Assert.That((string)receipt["phase"],Is.EqualTo("completed"),receipt.ToString());editor.Undo();Assert.That(editor.Find(a).transform.localPosition,Is.EqualTo(new Vector3(2,1,0)));Assert.That(editor.Find(b).transform.localPosition,Is.EqualTo(new Vector3(2.2f,1,0)));yield return null;
        }
        [UnityTest] public IEnumerator CancelledGroupGripRestoresEveryPieceAndReleasesOwnershipWithoutSaving(){
            var a=LayoutObject(new Vector3(2,1,0));var b=LayoutObject(new Vector3(2.2f,1,0));var tool=Mover(a,b);var before=JsonUtility.ToJson(editor.Snapshot());var hand=Hand(1,tool.Handle.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,tool.Handle.Grab);MoveHandle(tool,Vector3.up*.2f);
            using(editor.RuntimeGate.Hold("Pause test")){Assert.That(tool.Holding,Is.False);Assert.That(tool.Visible,Is.False);Assert.That(editor.Find(a).transform.localPosition,Is.EqualTo(new Vector3(2,1,0)));Assert.That(editor.Find(b).transform.localPosition,Is.EqualTo(new Vector3(2.2f,1,0)));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));}
            Assert.That(editor.Ownership.Observe().owners.Any(o=>o.id=="construction-handle"),Is.False);Assert.That(editor.Find(a).Grab.enabled,Is.True);Assert.That(editor.Find(b).GetComponent<RigidRoomItem>().AnimationOwned,Is.False);yield return null;
        }
        [UnityTest] public IEnumerator GroupHandleBoundsAndSaveFailureRestoreTheWholeConstruction(){
            var a=LayoutObject(new Vector3(2,1,0));var b=LayoutObject(new Vector3(2.2f,1,0));var tool=Mover(a,b);var hand=Hand(1,tool.Handle.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,tool.Handle.Grab);MoveHandle(tool,Vector3.up*.2f);var accepted=tool.Handle.transform.localPosition;
            tool.Handle.transform.localPosition=new Vector3(40,0,0);Assert.That(tool.Preview(out _),Is.False);Assert.That(tool.Handle.transform.localPosition,Is.EqualTo(accepted));
            var obstacle=Path.Combine(directory,"room.v6.json.pending");Directory.CreateDirectory(obstacle);try{hand.selectInput.manualPerformed=false;hand.selectInput.manualValue=0;manager.SelectExit((IXRSelectInteractor)hand,tool.Handle.Grab);Assert.That(tool.Error,Does.Contain("not saved"));Assert.That(editor.Find(a).transform.localPosition,Is.EqualTo(new Vector3(2,1,0)));Assert.That(editor.Find(b).transform.localPosition,Is.EqualTo(new Vector3(2.2f,1,0)));Assert.That(tool.Holding,Is.False);}finally{Directory.Delete(obstacle);}yield return null;
        }
        [UnityTest] public IEnumerator TwoRealXrGripsResizeBothMembersAndOnlyTheLastReleaseCommits(){
            var a=LayoutObject(new Vector3(2,1,0));var b=LayoutObject(new Vector3(2.2f,1,0));var tool=Mover(a,b);var center=tool.Handle.transform.position;
            var left=Hand(1,center-Vector3.right*.08f);var right=Hand(2,center+Vector3.right*.08f);manager.SelectEnter((IXRSelectInteractor)left,tool.Handle.Grab);manager.SelectEnter((IXRSelectInteractor)right,tool.Handle.Grab);for(int i=0;i<3;i++)yield return null;
            left.transform.position-=Vector3.right*.04f;right.transform.position+=Vector3.right*.04f;for(int i=0;i<6;i++)yield return null;
            Assert.That(tool.Handle.transform.localScale.x,Is.GreaterThan(1.2f));Assert.That(editor.Find(a).transform.localScale.x,Is.EqualTo(editor.Find(b).transform.localScale.x).Within(.001f));Assert.That(editor.Read(a).scale,Is.EqualTo(1));
            left.selectInput.manualPerformed=false;left.selectInput.manualValue=0;manager.SelectExit((IXRSelectInteractor)left,tool.Handle.Grab);Assert.That(tool.Holding,Is.True);Assert.That(editor.Read(a).scale,Is.EqualTo(1));
            right.selectInput.manualPerformed=false;right.selectInput.manualValue=0;manager.SelectExit((IXRSelectInteractor)right,tool.Handle.Grab);Assert.That(tool.Holding,Is.False);Assert.That(editor.Read(a).scale,Is.GreaterThan(1.2f));editor.Undo();Assert.That(editor.Read(a).scale,Is.EqualTo(1));Assert.That(editor.Read(b).scale,Is.EqualTo(1));
        }
        void CaptureConstructionHandle(ConstructionManipulator tool){
            string output=Environment.GetEnvironmentVariable("MAESTRO_GROUP_EVIDENCE");if(string.IsNullOrEmpty(output))return;Directory.CreateDirectory(output);
            var view=new GameObject("Construction evidence",typeof(Camera));var camera=view.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.88f,.91f,.92f);camera.nearClipPlane=.01f;camera.farClipPlane=10;camera.fieldOfView=42;
            var focus=tool.Handle.transform.position+new Vector3(.1f,-.1f,0);view.transform.position=focus+new Vector3(.45f,.3f,-1.2f);view.transform.LookAt(focus);
            var render=new RenderTexture(1200,900,24);var pixels=new Texture2D(1200,900,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try{camera.targetTexture=render;camera.Render();RenderTexture.active=render;pixels.ReadPixels(new Rect(0,0,1200,900),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(output,"construction-handle.png"),pixels.EncodeToPNG());}
            finally{RenderTexture.active=previous;camera.targetTexture=null;render.Release();UnityEngine.Object.Destroy(render);UnityEngine.Object.Destroy(pixels);UnityEngine.Object.Destroy(view);}
        }
        [UnityTest] public IEnumerator StartingPhysicsCancelsHeldGroupBeforeSimulation(){
            var a=LayoutObject(new Vector3(2,1,0));var b=LayoutObject(new Vector3(2.2f,1,0));var tool=Mover(a,b);var hand=Hand(1,tool.Handle.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,tool.Handle.Grab);MoveHandle(tool,Vector3.up*.2f);
            physics.SetSurfaces(true,"Ready");physics.StartPhysics();Assert.That(tool.Holding,Is.False);Assert.That(tool.Visible,Is.False);Assert.That(editor.Find(a).transform.localPosition,Is.EqualTo(new Vector3(2,1,0)));Assert.That(editor.Find(b).transform.localPosition,Is.EqualTo(new Vector3(2.2f,1,0)));Assert.That(editor.Find(a).Grab.enabled,Is.True);Assert.That(editor.Find(a).GetComponent<RigidRoomItem>().AnimationOwned,Is.False);Assert.That(editor.Ownership.Observe().owners.Any(o=>o.id=="construction-handle"),Is.False);yield return null;
        }
        [UnityTest] public IEnumerator SelectionChangesHideIdleHandleAndTemporaryGroupTransformsStayInTheFork(){
            var a=LayoutObject(new Vector3(2,1,0));var b=LayoutObject(new Vector3(2.2f,1,0));var tool=Mover(a,b);Assert.That(editor.SetConstructionSelection(editor.ObserveConstructionSelection().stateId,new[]{b,a},false,out var error),Is.True,error);Assert.That(tool.Visible,Is.False);
            Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);float deadline=Time.realtimeSinceStartup+5;while(editor.TemporarySavePending&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(editor.TemporarySavePending,Is.False);Assert.That(editor.TransformGroup(GroupMove(a,b),out error),Is.True,error);Assert.That(new RoomStorage(directory).Load(out _).objects.Single(o=>o.id==a).position,Is.EqualTo(new Vector3(2,1,0)));
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.Find(a).transform.localPosition,Is.EqualTo(new Vector3(2,1,0)));Assert.That(tool.Visible,Is.False);
        }
    }
}
