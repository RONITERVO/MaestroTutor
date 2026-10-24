// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
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
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        RoomSnapPlacement PendingSnapPair(out string moving,out string mount,bool join=true){
            (moving,mount)=FixedPieces();SnapPoint(moving,"Bottom",-.05f);SnapPoint(mount,"Top",.05f);
            var request=SnapTo(mount,moving);request.mode=join?"join":"place";request.breakForce=45;request.breakTorque=3;return request;
        }
        static JObject PendingSnapArgs(RoomSnapPlacement request){
            var a=JObject.Parse(JsonUtility.ToJson(request));if(request.mode=="place"){a.Remove("breakForce");a.Remove("breakTorque");}return a;
        }
        [UnityTest] public IEnumerator PendingSnapPointCapturesIntentAndKeepsLiveMotionAndUndo(){
            string id=editor.Identity(block);var args=(JObject)BehaviourCatalog.Action("object.snapPoint.edit").Module.Example.DeepClone();
            args["target"]=id;args["revision"]=editor.ObjectRevision(id);
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation operation=null;
            try{
                yield return Prepared(blocker);var module=BehaviourCatalog.Action("object.snapPoint.edit").Module;Assert.AreEqual("completion",module.Duration);
                Assert.IsTrue(module.Start(new CapabilityContext(editor,animations),"point",args,out operation,out error),error);
                Assert.AreEqual(RuleActionState.Preparing,operation.State(out error),error);Assert.IsEmpty(editor.Read(id).snapPoints);
                Assert.IsFalse(block.GetComponent<RigidRoomItem>().AnimationOwned);args.RemoveAll();
                var moved=new Vector3(3,2,2);block.transform.localPosition=moved;
                blocker.Dispose();yield return Drained(blocker);yield return GeometryReady(operation);Assert.IsTrue(operation.Complete(out error),error);
                Assert.AreEqual("Top",editor.Read(id).snapPoints.Single().id);
                Assert.AreEqual("Brick",new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==id).snapPoints.Single().family,error);
                Assert.AreEqual(moved,block.transform.localPosition);block.transform.localPosition+=Vector3.right;var latest=block.transform.localPosition;
                editor.Undo();Assert.IsEmpty(editor.Read(id).snapPoints);Assert.AreEqual(latest,block.transform.localPosition);
            }finally{operation?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingSnapSamplesAdmittedFramesAndCapturesQueuedRequest(){
            var request=PendingSnapPair(out var moving,out var mount);var args=PendingSnapArgs(request);
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation operation=null;
            try{
                yield return Prepared(blocker);var module=BehaviourCatalog.Action("object.layout.snap").Module;Assert.AreEqual("completion",module.Duration);
                Assert.IsTrue(module.Start(new CapabilityContext(editor,animations),"snap",args,out operation,out error),error);
                Assert.AreEqual(RuleActionState.Preparing,operation.State(out error),error);Assert.IsEmpty(editor.Read(moving).connections);
                Assert.IsFalse(editor.Find(moving).GetComponent<RigidRoomItem>().AnimationOwned);Assert.IsFalse(editor.Find(mount).GetComponent<RigidRoomItem>().AnimationOwned);
                args.RemoveAll();editor.Find(mount).transform.localPosition+=Vector3.up;var destination=editor.Find(mount).transform.localPosition;var before=editor.Find(moving).transform.localPosition;
                blocker.Dispose();yield return Drained(blocker);yield return GeometryReady(operation);Assert.IsTrue(operation.Complete(out error),error);
                Assert.AreEqual(1,(int)operation.Result["count"]);Assert.IsTrue((bool)operation.Result["joined"]);
                Assert.AreEqual(destination+Vector3.up*.1f,editor.Find(moving).transform.localPosition);
                var saved=new RoomStorage(directory).Load(out error);Assert.AreEqual(destination,saved.objects.Single(x=>x.id==mount).position);
                Assert.AreEqual(mount,saved.objects.Single(x=>x.id==moving).connections.Single().connected);
                editor.Undo();Assert.IsEmpty(editor.Read(moving).connections);Assert.AreEqual(before,editor.Find(moving).transform.localPosition);Assert.AreEqual(destination,editor.Find(mount).transform.localPosition);
                Assert.IsTrue((bool)operation.Result["joined"],"A later Undo must not rewrite the accepted result");
            }finally{operation?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingSnapStaleDestinationCannotOverwriteNewerHumanEdit(){
            var request=PendingSnapPair(out var moving,out var mount);var args=PendingSnapArgs(request);
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation operation=null;
            try{
                yield return Prepared(blocker);Assert.IsTrue(BehaviourCatalog.Action("object.layout.snap").Module.Start(new CapabilityContext(editor,animations),"snap",args,out operation,out error),error);
                blocker.Dispose();yield return Drained(blocker);SnapPoint(mount,"Top",.08f);
                Assert.AreEqual(RuleActionState.Failed,operation.State(out error));StringAssert.Contains("destination changed",error);
                Assert.IsEmpty(editor.Read(moving).connections);Assert.AreEqual(.08f,editor.Read(mount).snapPoints.Single().frame.position.y);
                Assert.IsFalse(editor.Find(moving).GetComponent<RigidRoomItem>().AnimationOwned);
            }finally{operation?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingSnapCancellationReleasesAllBodiesWithoutMovingOrSaving(){
            var request=PendingSnapPair(out var moving,out var mount);var before=JsonUtility.ToJson(editor.Snapshot());var bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));
            Assert.IsTrue(editor.BeginSnap(request,out var pending,out var error),error);
            try{
                yield return Prepared(pending);Assert.IsTrue(editor.Find(moving).GetComponent<RigidRoomItem>().AnimationOwned);Assert.IsTrue(editor.Find(mount).GetComponent<RigidRoomItem>().AnimationOwned);
                Assert.IsFalse(block.GetComponent<RigidRoomItem>().AnimationOwned);Assert.IsFalse(pending.Accepted);
                pending.Dispose();yield return Drained(pending);
                Assert.AreEqual(before,JsonUtility.ToJson(editor.Snapshot()));Assert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));
                Assert.IsFalse(editor.Find(moving).GetComponent<RigidRoomItem>().AnimationOwned);Assert.IsFalse(editor.Find(mount).GetComponent<RigidRoomItem>().AnimationOwned);
            }finally{pending.Dispose();}
        }
        [UnityTest] public IEnumerator PendingSnapChangedLiveFrameRejectsPreparedJointWithoutTeleporting(){
            var request=PendingSnapPair(out var moving,out var mount);var bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));var before=editor.Find(moving).transform.localPosition;
            Assert.IsTrue(editor.BeginSnap(request,out var pending,out var error),error);
            try{
                yield return Prepared(pending);editor.Find(mount).transform.localPosition+=Vector3.right;var changed=editor.Find(mount).transform.localPosition;
                Assert.IsFalse(pending.Poll(out _,out error));StringAssert.Contains("moved during preparation",error);Assert.IsFalse(pending.Accepted);
                yield return Drained(pending);Assert.IsEmpty(editor.Read(moving).connections);Assert.AreEqual(before,editor.Find(moving).transform.localPosition);Assert.AreEqual(changed,editor.Find(mount).transform.localPosition);
                Assert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));
            }finally{pending.Dispose();}
        }
        [UnityTest] public IEnumerator PendingSnapAcceptedPublicationFailureRetainsUnconfirmedJoinedArrangement(){
            var request=PendingSnapPair(out var moving,out var mount);var bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));using var pause=new PublicationPause();
            Assert.IsTrue(editor.BeginSnap(request,out var pending,out var error,phase=>{if(phase=="publishing"){pause.Check(phase);throw new IOException("Injected snap publication failure");}}),error);
            try{
                yield return Prepared(pending);Assert.IsTrue(pending.Poll(out var ready,out error),error);Assert.IsFalse(ready);Assert.IsTrue(pending.Accepted);
                Assert.AreEqual(mount,editor.Read(moving).connections.Single().connected);Assert.IsFalse(editor.Find(moving).GetComponent<RigidRoomItem>().AnimationOwned);
                pause.Release();yield return Rejected(pending);Assert.IsFalse(pending.Complete(out _));Assert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));
                Assert.AreEqual(mount,editor.Read(moving).connections.Single().connected);Assert.IsFalse(editor.BeginSnap(SnapTo(mount,moving),out _,out _));
            }finally{pause.Release();pending.Dispose();}
            yield return Drained(pending);
        }
        [UnityTest] public IEnumerator PendingSnapStopAfterAcceptanceDrainsSavedJoinAndReleasesBodies(){
            var request=PendingSnapPair(out var moving,out var mount);using var pause=new PublicationPause();
            Assert.IsTrue(editor.BeginSnap(request,out var pending,out var error,pause.Check),error);
            try{
                yield return Prepared(pending);Assert.IsTrue(pending.Poll(out var ready,out error),error);Assert.IsFalse(ready);Assert.IsTrue(pending.Accepted);
                pending.Dispose();Assert.IsFalse(editor.Find(moving).GetComponent<RigidRoomItem>().AnimationOwned);Assert.IsFalse(editor.Find(mount).GetComponent<RigidRoomItem>().AnimationOwned);
                pause.Release();yield return Drained(pending);Assert.IsTrue(pending.Published);Assert.IsFalse(pending.Complete(out _));
                Assert.AreEqual(mount,new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==moving).connections.Single().connected,error);
                physics.SetSurfaces(true,"Synthetic accepted join");physics.StartPhysics();yield return new WaitForFixedUpdate();
                var view=editor.Find(moving).GetComponent<RoomConnectionView>();Assert.IsTrue(view.Active,view.Error);physics.PausePhysics();
            }finally{pause.Release();pending.Dispose();}
        }
        [UnityTest] public IEnumerator PendingSnapTemporaryCompletionKeepsSavedBaselineAndOneUndo(){
            var request=PendingSnapPair(out var moving,out var mount);var before=editor.Find(moving).transform.localPosition;
            Assert.IsTrue(editor.BeginTemporaryRoom(out var error),error);float end=Time.realtimeSinceStartup+10;
            while(editor.TemporarySavePending&&Time.realtimeSinceStartup<end)yield return null;
            Assert.IsTrue(editor.TemporaryRoom);Assert.IsFalse(editor.TemporarySavePending);var bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));
            var module=BehaviourCatalog.Action("object.layout.snap").Module;Assert.IsTrue(module.Start(new CapabilityContext(editor,animations),"temporary snap",PendingSnapArgs(request),out var operation,out error),error);
            try{
                yield return GeometryReady(operation);Assert.IsTrue(operation.Complete(out error),error);Assert.IsTrue((bool)operation.Result["temporary"]);
                Assert.AreEqual(mount,editor.Read(moving).connections.Single().connected);Assert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));
                editor.Undo();Assert.AreEqual(before,editor.Find(moving).transform.localPosition);Assert.IsEmpty(editor.Read(moving).connections);Assert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));
            }finally{operation.Stop(false);}
        }
    }
}
