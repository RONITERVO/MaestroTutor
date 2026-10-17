// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        IEnumerator Prepared(RoomEditor.PendingPersistedEdit pending)
        {
            float end=Time.realtimeSinceStartup+20;
            while(true){
                Assert.IsTrue(pending.Poll(out var ready,out var error),error);
                if(ready)yield break;
                Assert.Less(Time.realtimeSinceStartup,end,"Background room preparation timed out");yield return null;
            }
        }
        IEnumerator Drained(RoomEditor.PendingPersistedEdit pending)
        {
            float end=Time.realtimeSinceStartup+20;
            while(!pending.Drained){Assert.Less(Time.realtimeSinceStartup,end,"Cancelled worker did not drain");yield return null;}
        }
        [UnityTest] public IEnumerator PendingRotationPreservesRunningClockAndViewpointAndSavesBeforeSuccess()
        {
            string id=editor.Identity(block);var original=editor.Read(id).rotation;var expected=Quaternion.Euler(0,83,0);
            Assert.IsTrue(editor.ConfigureWorldTime(editor.WorldTimeRevision,new WorldTimeSettings{running=true},out var error),error);
            Assert.IsTrue(editor.BeginRotation(id,expected,out var pending,out error),error);
            using(pending){
                Assert.AreEqual(original,editor.Read(id).rotation);Assert.IsFalse(pending.Published);
                Assert.IsFalse(editor.WriteGate.CanFreeze(out _),"Backup must not retire a pending edit's owner");
                yield return Prepared(pending);
                Assert.AreEqual(original,editor.Read(id).rotation);
                Assert.AreEqual(original,new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==id).rotation);
                editor.TickWorldTime(.2);editor.TickWorldTime(.2);
                editor.RememberViewpoint(new RoomViewpoint{active=true,position=Vector3.left});
                double now=editor.WorldSecond;
                Assert.IsTrue(pending.Complete(out error),error);Assert.IsTrue(pending.Published);
                Assert.Less(Quaternion.Angle(expected,block.transform.localRotation),.01);
                var disk=new RoomStorage(directory).Load(out _);Assert.Less(Quaternion.Angle(expected,disk.objects.Single(x=>x.id==id).rotation),.01);
                Assert.AreEqual(now,editor.WorldSecond);Assert.AreEqual(Vector3.left,editor.Viewpoint.position);
                Assert.IsTrue(editor.HasUnsavedChanges,"The newer runtime checkpoint must still be saved");
                editor.Undo();Assert.Less(Quaternion.Angle(original,editor.Read(id).rotation),.01);
                Assert.IsTrue(pending.Complete(out error),error);Assert.Less(Quaternion.Angle(original,editor.Read(id).rotation),.01,"Completing twice must not replay");
            }
            Assert.IsTrue(editor.WriteGate.CanFreeze(out error),error);
        }
        [UnityTest] public IEnumerator NewManualEditInvalidatesReadyRotationWithoutOverwritingIt()
        {
            string id=editor.Identity(block);var original=editor.Read(id).rotation;
            Assert.IsTrue(editor.BeginRotation(id,Quaternion.Euler(0,90,0),out var pending,out var error),error);
            using(pending){
                yield return Prepared(pending);
                string other=editor.Snapshot().objects.First(x=>!x.IsBuiltIn&&x.id!=id).id;
                Assert.IsTrue(editor.PaintObject(other,Color.red,out error),error);
                Assert.IsFalse(pending.Complete(out error));StringAssert.Contains("changed",error);
                Assert.IsFalse(pending.Published);Assert.AreEqual(original,editor.Read(id).rotation);
                Assert.AreEqual(Color.red,new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==other).color);
            }
            yield return Drained(pending);
        }
        [UnityTest] public IEnumerator PendingRotationCancellationAndDisableReleaseOwnershipWithoutPublishing()
        {
            string id=editor.Identity(block);var original=editor.Read(id).rotation;
            for(int mode=0;mode<2;mode++){
                Assert.IsTrue(editor.BeginRotation(id,Quaternion.Euler(0,90,0),out var pending,out var error),error);
                Assert.IsFalse(editor.BeginRotation(id,Quaternion.identity,out _,out _),"Admission is bounded before any additional copy");
                Assert.IsTrue(pending.Poll(out _,out error),error);
                if(mode==0)pending.Dispose();else editor.enabled=false;
                yield return Drained(pending);
                Assert.IsFalse(pending.Complete(out _));Assert.IsFalse(pending.Published);
                Assert.AreEqual(original,editor.Read(id).rotation);
                Assert.AreEqual(original,new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==id).rotation);
                editor.enabled=true;Assert.IsTrue(editor.WriteGate.CanFreeze(out error),error);
            }
        }
        [UnityTest] public IEnumerator ANewerPrimaryDuringPreparationCannotBeOverwrittenByRotation()
        {
            string id=editor.Identity(block);var original=editor.Read(id).rotation;
            Assert.IsTrue(editor.BeginRotation(id,Quaternion.Euler(0,90,0),out var pending,out var error),error);
            using(pending){
                yield return Prepared(pending);
                var path=Path.Combine(directory,"room.v999.json");File.WriteAllText(path,"preserve");
                Assert.IsFalse(pending.Complete(out error));Assert.IsFalse(pending.Published);
                Assert.AreEqual(original,editor.Read(id).rotation);Assert.AreEqual("preserve",File.ReadAllText(path));
                File.Delete(path);
            }
        }
        [UnityTest] public IEnumerator CancelledPendingRotationReleasesOnlyItsOwnBodyAndPreservesItsTrajectory()
        {
            Assert.IsTrue(editor.CreatePrimitive(RoomObjectKind.Ball,"Moving rotation target",new Vector3(0,1.6f,1),1,Color.white,out var movingId,out var createError),createError);
            var moving=editor.Find(movingId);
            physics.SetSurfaces(true,"Ready");physics.StartPhysics();yield return new WaitForFixedUpdate();
            var rigid=moving.GetComponent<Maestro.Quest.Interaction.RigidRoomItem>();
            Assert.IsTrue(rigid.Launch(Vector3.right,Vector3.up));yield return new WaitForFixedUpdate();
            var motion=rigid.FlowMotion;var start=moving.transform.position;
            Assert.IsTrue(editor.BeginRotation(movingId,Quaternion.Euler(0,90,0),out var pending,out var error),error);
            using(pending){
                Assert.IsTrue(rigid.AnimationOwned);yield return new WaitForFixedUpdate();
                Assert.AreEqual(start,moving.transform.position);Assert.IsTrue(physics.Running);
                pending.Dispose();Assert.IsFalse(rigid.AnimationOwned);
                Assert.Less((rigid.FlowMotion.Linear-motion.Linear).magnitude,.001f);
            }
            yield return Drained(pending);
        }
        [UnityTest] public IEnumerator PendingPoseCancellationDoesNotReplayMomentumAfterPhysicsPause()
        {
            Assert.IsTrue(editor.CreatePrimitive(RoomObjectKind.Ball,"Moving rotation target",new Vector3(0,1.6f,1),1,Color.white,out var movingId,out var createError),createError);
            var moving=editor.Find(movingId);
            physics.SetSurfaces(true,"Ready");physics.StartPhysics();yield return new WaitForFixedUpdate();
            var rigid=moving.GetComponent<Maestro.Quest.Interaction.RigidRoomItem>();
            Assert.IsTrue(rigid.Launch(Vector3.right,Vector3.up));yield return new WaitForFixedUpdate();
            Assert.IsTrue(editor.BeginRotation(movingId,Quaternion.Euler(0,90,0),out var pending,out var error),error);
            physics.PausePhysics();pending.Dispose();yield return Drained(pending);
            Assert.IsFalse(rigid.AnimationOwned);Assert.AreEqual(Vector3.zero,rigid.FlowMotion.Linear);
        }
        [UnityTest] public IEnumerator PendingRotationKeepsRuntimeCaptureFromItsNativeChangedCallbackDirty()
        {
            var expected=Quaternion.Euler(0,80,0);string id=editor.Identity(block);int revision=editor.Revision;bool captured=false;
            Assert.IsTrue(editor.BeginRotation(id,expected,out var pending,out var error),error);
            using(pending){
                yield return Prepared(pending);
                Action callback=()=>{
                    if(captured||Quaternion.Angle(editor.Read(id).rotation,expected)>.01)return;
                    captured=true;Assert.Greater(editor.Revision,revision,"Observers must see the accepted revision");
                    editor.RememberViewpoint(new RoomViewpoint{active=true,position=Vector3.back});
                };
                editor.Changed+=callback;
                try {Assert.IsTrue(pending.Complete(out error),error);}
                finally {editor.Changed-=callback;}
                Assert.IsTrue(captured);Assert.AreEqual(Vector3.back,editor.Viewpoint.position);
                Assert.IsTrue(editor.HasUnsavedChanges,"A saved earlier checkpoint cannot clear a newer callback capture");
                Assert.AreNotEqual(Vector3.back,new RoomStorage(directory).Load(out _).viewpoint.position);
            }
        }
        [UnityTest] public IEnumerator PendingRotationStopFromCompletionObserverCannotRetractOrReplayTheSavedEdit()
        {
            string id=editor.Identity(block);var expected=Quaternion.Euler(0,64,0);bool callbackRan=false;
            Assert.IsTrue(editor.BeginRotation(id,expected,out var pending,out var error),error);
            using(pending){
                yield return Prepared(pending);
                Action callback=()=>{
                    if(callbackRan||Quaternion.Angle(editor.Read(id).rotation,expected)>.01)return;
                    callbackRan=true;pending.Dispose();
                    Assert.IsFalse(pending.Complete(out _),"A completion callback cannot recursively publish");
                };
                editor.Changed+=callback;
                try {Assert.IsTrue(pending.Complete(out error),error);}
                finally {editor.Changed-=callback;}
                Assert.IsTrue(callbackRan);Assert.IsTrue(pending.Published);Assert.IsTrue(pending.Drained);
                Assert.IsTrue(pending.Complete(out error),error);Assert.IsNull(error);
                Assert.Less(Quaternion.Angle(expected,new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==id).rotation),.01);
                editor.Undo();Assert.Greater(Quaternion.Angle(expected,editor.Read(id).rotation),1);
                Assert.IsTrue(pending.Complete(out error),error);Assert.IsNull(error);Assert.Greater(Quaternion.Angle(expected,editor.Read(id).rotation),1);
            }
        }
    }
}
