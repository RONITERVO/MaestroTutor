// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {public sealed partial class RoomRulesTests {
    IEnumerator FinishSculptSave(SpatialSculpting capture){
        float deadline=Time.realtimeSinceStartup+10;
        while((capture.Resolving||editor.PendingEditSave)&&Time.realtimeSinceStartup<deadline)yield return null;
        Assert.IsFalse(capture.Resolving,editor.Status);Assert.IsFalse(editor.PendingEditSave,"Physical save did not drain");
    }
    IEnumerator PhysicalAccepted(SpatialSculpting capture){
        float deadline=Time.realtimeSinceStartup+10;
        while(capture.ResolvingEdit?.Accepted!=true&&capture.Resolving&&Time.realtimeSinceStartup<deadline)yield return null;
        Assert.IsTrue(capture.ResolvingEdit?.Accepted==true,editor.Status);
    }
    [UnityTest] public IEnumerator PendingPhysicalSculptConfirmsOnceAndKeepsUnrelatedMotionRunning(){
        var(id,view,capture)=SculptStage();capture.Begin(0,SculptRay(view));capture.Cancel(0);string session=capture.SessionId;
        using var pause=new PublicationPause();var body=block.GetComponent<Rigidbody>();MovingLiquidBody(body);
        Assert.IsTrue(capture.BeginResolve(session,false,out var operation,out var error,pause.Check),error);
        try {
            Assert.IsEmpty(operation.Result);Assert.AreEqual("saving",(string)SculptFact("object.field.capture")["phase"]);
            Assert.IsFalse(capture.CanResolve(session,true,out _));yield return PhysicalAccepted(capture);
            Assert.AreEqual(.05f,view.Accepted.heights[144],.000001f);
            Assert.AreSame(view.Surface.GetComponent<MeshFilter>().sharedMesh,((MeshCollider)view.Collision).sharedMesh);
            var position=body.position;for(int i=0;i<3;i++)yield return new WaitForFixedUpdate();
            Assert.Greater(Vector3.Distance(position,body.position),.001f);Assert.IsTrue(pause.Entered.IsSet);
            Assert.IsEmpty(operation.Result);Assert.IsFalse(operation.Complete(out _));
            pause.Release();yield return FinishSculptSave(capture);Assert.IsTrue(operation.Complete(out error),error);
            Assert.AreEqual(session,(string)operation.Result["sessionId"]);Assert.AreEqual("saved",(string)operation.Result["phase"]);
            var result=operation.Result;result["changedVertices"]=999;Assert.Less((int)operation.Result["changedVertices"],289);
            Assert.AreEqual(.05f,new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==id).heightFields[0].heights[144],.000001f,error);
            capture.Begin(0,SculptRay(view,.3f));Assert.IsTrue(capture.Active);operation.Stop(false);Assert.IsTrue(capture.Active,"Old receipt must not stop a later gesture");
            capture.Cancel(0);capture.ResolveManual(true);editor.Undo();Assert.AreEqual(.08f,editor.Read(id).heightFields[0].heights[144]);
        } finally {pause.Release();operation.Stop(false);}
    }
    [UnityTest] public IEnumerator PendingPhysicalSculptStopAfterAcceptanceDrainsWithoutRetryableDraft(){
        var(id,view,capture)=SculptStage();capture.Begin(0,SculptRay(view));capture.Cancel(0);string session=capture.SessionId;
        using var pause=new PublicationPause();Assert.IsTrue(capture.BeginResolve(session,false,out var operation,out var error,pause.Check),error);
        RoomEditor.PendingPersistedEdit pending=null;
        try {
            yield return PhysicalAccepted(capture);pending=capture.ResolvingEdit;operation.Stop(false);
            Assert.IsFalse(capture.Busy);Assert.IsFalse(capture.CanResolve(session,false,out _));Assert.IsEmpty(operation.Result);
            StringAssert.Contains("Saving is still pending",operation.InterruptionStatus);
            Assert.AreEqual(.05f,editor.Read(id).heightFields[0].heights[144],.000001f);
        } finally {pause.Release();operation.Stop(false);}
        yield return Drained(pending);Assert.IsTrue(pending.Published);
        Assert.AreEqual(.05f,new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==id).heightFields[0].heights[144],.000001f,error);
        editor.Undo();Assert.AreEqual(.08f,editor.Read(id).heightFields[0].heights[144]);
    }
    [UnityTest] public IEnumerator PendingPhysicalScoopPausedInQueueRetainsBothBalancesAndExplicitRetrySavesOnce(){
        var(field,view,capture)=SculptStage();var(tool,contact)=MaterialScoopProp();var before=editor.Read(field).heightFields[0];
        PlaceMaterialScoop(tool,view);contact.Sample(true,RoomActorRole.Control);capture.Cancel(-2);string session=capture.SessionId;
        Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);
        CapabilityOperation operation=null;
        try {
            Assert.IsTrue(capture.BeginResolve(session,false,out operation,out error),error);
            Assert.IsNull(capture.ResolvingEdit);Assert.AreEqual("saving",(string)SculptFact("object.material.capture")["phase"]);
            capture.SendMessage("OnApplicationPause",true);Assert.AreEqual(RuleActionState.Failed,operation.State(out _));
            Assert.IsTrue(capture.Retained);Assert.IsFalse(capture.Resolving);Assert.AreEqual(session,capture.SessionId);
            Assert.AreEqual(0,MaterialAmount(tool));Assert.AreEqual(before.heights,editor.Read(field).heightFields[0].heights);
            capture.SendMessage("OnApplicationPause",false);yield return null;Assert.IsTrue(capture.Retained);
        } finally {blocker.Dispose();operation?.Stop(false);}
        yield return Drained(blocker);
        yield return ContainerAction(new RoomAgentExecutor(editor),SculptResolve(capture));
        double amount=MaterialAmount(tool);Assert.Greater(amount,0);Assert.AreEqual(amount,before.VolumeLitres-editor.Read(field).heightFields[0].VolumeLitres,.000001);
        Assert.IsFalse(capture.CanResolve(session,false,out _));editor.Undo();Assert.AreEqual(0,MaterialAmount(tool));Assert.AreEqual(before.heights,editor.Read(field).heightFields[0].heights);
    }
    [UnityTest] public IEnumerator PendingPhysicalScoopReleasePreservesAuthoredResizeAndRequiresFreshRetry()
    {
        var(field,view,capture)=SculptStage();var(tool,contact)=MaterialScoopProp();var before=editor.Read(field).heightFields[0];
        PlaceMaterialScoop(tool,view);var item=editor.Find(tool);var hand=Hand(1,item.transform.position);
        manager.SelectEnter((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)hand,item.Grab);contact.SendMessage("LateUpdate");Assert.IsTrue(capture.Active);
        PlaceMaterialScoop(tool,view,false,.15f);contact.SendMessage("LateUpdate");Assert.IsTrue(capture.Resolving);
        item.transform.localScale=UnityEngine.Vector3.one*1.2f;
        hand.selectInput.manualPerformed=false;hand.selectInput.manualValue=0;
        manager.SelectExit((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)hand,item.Grab);contact.SendMessage("LateUpdate");
        yield return FinishSculptSave(capture);Assert.IsTrue(capture.Retained);Assert.AreEqual(1.2f,editor.Read(tool).scale);
        Assert.AreEqual(0,MaterialAmount(tool));Assert.AreEqual(before.heights,editor.Read(field).heightFields[0].heights);
        yield return ContainerAction(new RoomAgentExecutor(editor),SculptResolve(capture));
        Assert.Greater(MaterialAmount(tool),0);Assert.AreEqual(1.2f,editor.Read(tool).scale);
        editor.Undo();Assert.AreEqual(0,MaterialAmount(tool));Assert.AreEqual(1.2f,editor.Read(tool).scale);Assert.AreEqual(UnityEngine.Vector3.one*1.2f,item.transform.localScale);
    }
    [UnityTest] public IEnumerator PendingPhysicalPackingAcceptedFailureIsNotASecondPackAndPreservesSavedFile(){
        var(id,view,capture)=PackingStage();var before=editor.Read(id).heightFields[0];int count=editor.Snapshot().objects.Length;
        var original=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));capture.Begin(0,SculptRay(view));capture.Cancel(0);string session=capture.SessionId;
        using var pause=new PublicationPause();Assert.IsTrue(capture.BeginResolve(session,false,out var operation,out var error,phase=>{if(phase=="publishing"){pause.Check(phase);throw new IOException("Injected physical packing publication failure");}}),error);
        try {
            yield return PhysicalAccepted(capture);Assert.AreEqual(count+1,editor.Snapshot().objects.Length);Assert.IsEmpty(PackedId());
            var ball=editor.Snapshot().objects.Single(x=>x.name=="Packed "+before.material);
            Assert.AreEqual(ball.materialStores[0].amountLitres,before.VolumeLitres-editor.Read(id).heightFields[0].VolumeLitres,.000001);
            pause.Release();yield return FinishSculptSave(capture);Assert.AreEqual(RuleActionState.Failed,operation.State(out error));
            Assert.IsEmpty(operation.Result);Assert.IsFalse(capture.Busy);Assert.IsFalse(capture.CanResolve(session,false,out _));
            StringAssert.Contains(ball.id,operation.InterruptionStatus);Assert.AreEqual(count+1,editor.Snapshot().objects.Length);
            Assert.AreEqual(original,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));
            Assert.IsFalse(editor.BeginMove("book",Vector3.one,out _,out _),"Unconfirmed save must block subsequent writes");
        } finally {pause.Release();operation.Stop(false);}
    }
    [UnityTest] public IEnumerator PendingPhysicalPackingQueueRechecksClearanceWithoutConsumingMaterial(){
        var(id,view,capture)=PackingStage();var before=editor.Read(id).heightFields[0];int count=editor.Snapshot().objects.Length;
        capture.Begin(0,SculptRay(view));capture.Cancel(0);
        Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);
        CapabilityOperation operation=null;GameObject wall=null;
        try {
            Assert.IsTrue(capture.BeginResolve(capture.SessionId,false,out operation,out error),error);
            wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform,false);
            wall.transform.position=editor.transform.TransformPoint(JsonUtility.FromJson<Vector3>(SculptFact("material.pack.capture")["position"].ToString()));wall.transform.localScale=Vector3.one*.02f;
            Assert.AreEqual(RuleActionState.Failed,operation.State(out error));StringAssert.Contains("clear space",error);
            Assert.IsTrue(capture.Retained);Assert.AreEqual(count,editor.Snapshot().objects.Length);Assert.AreEqual(before.heights,editor.Read(id).heightFields[0].heights);
        } finally {if(wall)UnityEngine.Object.DestroyImmediate(wall);operation?.Stop(false);blocker.Dispose();}
        yield return Drained(blocker);capture.ResolveManual(true);
    }
}}
