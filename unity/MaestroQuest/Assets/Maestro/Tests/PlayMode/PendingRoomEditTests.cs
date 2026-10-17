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
                Assert.IsTrue(pending.Prepare(out var ready,out var error),error);
                if(ready)yield break;
                Assert.Less(Time.realtimeSinceStartup,end,"Background room preparation timed out");yield return null;
            }
        }
        IEnumerator Completed(RoomEditor.PendingPersistedEdit pending)
        {
            float end=Time.realtimeSinceStartup+20;
            while(true){
                Assert.IsTrue(pending.Poll(out var ready,out var error),error);
                if(ready){Assert.IsTrue(pending.Complete(out error),error);yield break;}
                Assert.Less(Time.realtimeSinceStartup,end,"Background publication timed out");yield return null;
            }
        }
        IEnumerator Rejected(RoomEditor.PendingPersistedEdit pending)
        {
            float end=Time.realtimeSinceStartup+20;
            while(pending.Poll(out var ready,out var error)){
                Assert.IsFalse(ready,"Expected publication refusal");
                Assert.Less(Time.realtimeSinceStartup,end,"Background refusal timed out");yield return null;
            }
            Assert.IsFalse(pending.Complete(out _));
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
                yield return Completed(pending);Assert.IsTrue(pending.Published);
                Assert.Less(Quaternion.Angle(expected,block.transform.localRotation),.01);
                var disk=new RoomStorage(directory).Load(out _);Assert.Less(Quaternion.Angle(expected,disk.objects.Single(x=>x.id==id).rotation),.01);
                Assert.GreaterOrEqual(editor.WorldSecond,now);Assert.AreEqual(Vector3.left,editor.Viewpoint.position);
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
        [UnityTest] public IEnumerator ANewerPrimaryAfterPreflightCannotBeOverwrittenByRotation()
        {
            string id=editor.Identity(block);var original=editor.Read(id).rotation;
            Assert.IsTrue(editor.BeginRotation(id,Quaternion.Euler(0,90,0),out var pending,out var error),error);
            using(pending){
                yield return Prepared(pending);
                var path=Path.Combine(directory,"room.v999.json");File.WriteAllText(path,"preserve");
                yield return Rejected(pending);Assert.IsFalse(pending.Published);
                Assert.IsTrue(pending.Accepted,"A change after preflight is a post-acceptance save conflict");
                Assert.Greater(Quaternion.Angle(original,editor.Read(id).rotation),1);
                Assert.IsFalse(editor.CanSaveRoom,"No autosave may overwrite the unconfirmed saved baseline");
                Assert.AreEqual("preserve",File.ReadAllText(path));File.Delete(path);
                Assert.IsFalse(editor.TryFlush(out error),"Removing the external marker must not silently resume writes");
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
                try {yield return Completed(pending);}
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
                try {yield return Completed(pending);}
                finally {editor.Changed-=callback;}
                Assert.IsTrue(callbackRan);Assert.IsTrue(pending.Published);Assert.IsTrue(pending.Drained);
                Assert.IsTrue(pending.Complete(out error),error);Assert.IsNull(error);
                Assert.Less(Quaternion.Angle(expected,new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==id).rotation),.01);
                editor.Undo();Assert.Greater(Quaternion.Angle(expected,editor.Read(id).rotation),1);
                Assert.IsTrue(pending.Complete(out error),error);Assert.IsNull(error);Assert.Greater(Quaternion.Angle(expected,editor.Read(id).rotation),1);
            }
        }
        sealed class PublicationPause:IDisposable
        {
            readonly System.Threading.ManualResetEventSlim release=new();
            internal readonly System.Threading.ManualResetEventSlim Entered=new();
            internal int Worker;
            internal void Check(string phase){
                if(phase!="publishing")return;
                Worker=System.Threading.Thread.CurrentThread.ManagedThreadId;Entered.Set();
                if(!release.Wait(10000))throw new TimeoutException("Native publication test barrier timed out");
            }
            internal void Release()=>release.Set();
            public void Dispose(){release.Dispose();Entered.Dispose();}
        }
        [UnityTest] public IEnumerator PendingRotationWaitsForSavingWithoutFreezingOtherPhysicsOrWorldTime()
        {
            int caller=System.Threading.Thread.CurrentThread.ManagedThreadId;
            Assert.IsTrue(editor.CreatePrimitive(RoomObjectKind.Ball,"Independent ball",new Vector3(0,1.8f,1),1,Color.white,out var moving,out var error),error);
            Assert.IsTrue(editor.ConfigureWorldTime(editor.WorldTimeRevision,new WorldTimeSettings{running=true},out error),error);
            physics.SetSurfaces(true,"Ready");physics.StartPhysics();
            using var pause=new PublicationPause();
            Assert.IsTrue(editor.BeginRotation(editor.Identity(block),Quaternion.Euler(0,77,0),out var pending,out error,pause.Check),error);
            try {
                yield return Prepared(pending);
                Assert.IsTrue(pending.Poll(out var ready,out error),error);Assert.IsFalse(ready);
                Assert.IsTrue(pending.Accepted);Assert.IsFalse(pending.Published);Assert.IsFalse(editor.WriteGate.Frozen);
                Assert.IsFalse(pending.Complete(out _),"No successful receipt until the actual save finishes");
                float y=editor.Find(moving).transform.position.y;double clock=editor.WorldSecond;
                for(int i=0;i<4;i++)yield return new WaitForFixedUpdate();
                Assert.IsTrue(pause.Entered.IsSet);Assert.AreNotEqual(caller,pause.Worker);
                Assert.Less(editor.Find(moving).transform.position.y,y);Assert.Greater(editor.WorldSecond,clock);
                Assert.Less(Quaternion.Angle(Quaternion.Euler(0,77,0),block.transform.localRotation),.01);
                editor.RememberViewpoint(new RoomViewpoint{active=true,position=Vector3.forward});
            }finally{pause.Release();}
            yield return Completed(pending);Assert.IsTrue(pending.Published);Assert.IsTrue(editor.HasUnsavedChanges);
            Assert.AreEqual(Vector3.forward,editor.Viewpoint.position);
        }
        [UnityTest] public IEnumerator PendingRotationStopAfterAcceptanceKeepsItsSaveAndDirectoryOwner()
        {
            using var pause=new PublicationPause();string id=editor.Identity(block);var target=Quaternion.Euler(0,62,0);
            Assert.IsTrue(editor.BeginRotation(id,target,out var pending,out var error,pause.Check),error);
            try {
                yield return Prepared(pending);Assert.IsTrue(pending.Poll(out _,out error),error);
                Assert.IsTrue(pending.Accepted);
                Assert.IsFalse(editor.BeginTemporaryRoom(out error),"A paired snapshot must not bypass this publication");
                StringAssert.Contains("pending room edit",error);pending.Dispose();
                Assert.IsFalse(pending.Drained);Assert.IsFalse(editor.WriteGate.CanFreeze(out _));
                Assert.IsFalse(pending.Complete(out _));StringAssert.Contains("accepted",pending.InterruptionStatus);
                Assert.Less(Quaternion.Angle(target,editor.Read(id).rotation),.01);
            }finally{pause.Release();}
            yield return Drained(pending);Assert.IsTrue(pending.Published);
            Assert.Less(Quaternion.Angle(target,new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==id).rotation),.01);
            Assert.IsTrue(editor.WriteGate.CanFreeze(out error),error);Assert.IsFalse(pending.Complete(out _),"Stopped observation must not replay");
        }
        [UnityTest] public IEnumerator PendingRotationDisableAfterAcceptanceLetsDetachedSavingDrain()
        {
            using var pause=new PublicationPause();string id=editor.Identity(block);var target=Quaternion.Euler(0,51,0);
            Assert.IsTrue(editor.BeginRotation(id,target,out var pending,out var error,pause.Check),error);
            try {
                yield return Prepared(pending);Assert.IsTrue(pending.Poll(out _,out error),error);
                editor.enabled=false;Assert.IsFalse(pending.Drained);
                Assert.Less(Quaternion.Angle(target,editor.Read(id).rotation),.01);
            }finally{pause.Release();}
            yield return Drained(pending);Assert.IsTrue(pending.Published);
            Assert.Less(Quaternion.Angle(target,new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==id).rotation),.01);
            editor.enabled=true;
        }
        [UnityTest] public IEnumerator PendingRotationNativeCallbackCanFlushNewerStateWithoutDeadlockingPublication()
        {
            string id=editor.Identity(block);var target=Quaternion.Euler(0,48,0);bool flushed=false;
            Assert.IsTrue(editor.BeginRotation(id,target,out var pending,out var error),error);
            Action callback=()=>{
                if(flushed||!pending.Accepted)return;
                flushed=true;editor.RememberViewpoint(new RoomViewpoint{active=true,position=Vector3.down});
                Assert.IsTrue(editor.TryFlush(out var issue),issue);
            };
            editor.Changed+=callback;
            try{yield return Completed(pending);}finally{editor.Changed-=callback;pending.Dispose();}
            Assert.IsTrue(flushed);Assert.IsTrue(pending.Published);
            var saved=new RoomStorage(directory).Load(out _);Assert.AreEqual(Vector3.down,saved.viewpoint.position);
            Assert.Less(Quaternion.Angle(target,saved.objects.Single(x=>x.id==id).rotation),.01);
        }
        [UnityTest] public IEnumerator PendingRotationLateDiskConflictCannotBeRetriedByAutosaveOrFlush()
        {
            using var pause=new PublicationPause();string id=editor.Identity(block);string primary=Path.Combine(directory,RoomStorage.FileName);
            var target=Quaternion.Euler(0,39,0);string accepted=null,backup=null;
            Assert.IsTrue(editor.BeginRotation(id,target,out var pending,out var error,pause.Check),error);
            try {
                yield return Prepared(pending);Assert.IsTrue(pending.Poll(out _,out error),error);Assert.IsTrue(pending.Accepted);
                var other=new RoomStorage(directory);var newer=other.Load(out _);newer.objects.Single(x=>x.id==id).name="External accepted edit";
                Assert.IsTrue(other.Save(newer,out error),error);accepted=File.ReadAllText(primary);backup=File.ReadAllText(primary+".backup");
            }finally{pause.Release();}
            yield return Rejected(pending);yield return Drained(pending);
            Assert.IsFalse(editor.CanSaveRoom);Assert.IsFalse(editor.TryFlush(out error));StringAssert.Contains("save was not confirmed",error);
            Assert.IsFalse(editor.PaintObject(id,Color.red,out error));StringAssert.Contains("save was not confirmed",error);
            editor.SaveNow();yield return null;yield return null;
            Assert.AreEqual(accepted,File.ReadAllText(primary));Assert.AreEqual(backup,File.ReadAllText(primary+".backup"));
            Assert.Less(Quaternion.Angle(target,editor.Read(id).rotation),.01,"Keep the accepted live edit inspectable");
        }
        [UnityTest] public IEnumerator PendingRotationReleasesItsReservationWithoutAnotherObserverPoll()
        {
            using var pause=new PublicationPause();string id=editor.Identity(block);
            Assert.IsTrue(editor.BeginRotation(id,Quaternion.Euler(0,29,0),out var pending,out var error,pause.Check),error);
            try {
                yield return Prepared(pending);Assert.IsTrue(pending.Poll(out var ready,out error),error);Assert.IsFalse(ready);
                Assert.IsTrue(pending.Accepted);Assert.IsFalse(pending.Drained);
            }finally{pause.Release();}
            // No further Poll, Complete or Dispose call drives this cleanup.
            yield return Drained(pending);
            Assert.IsTrue(editor.WriteGate.CanFreeze(out error),error);Assert.IsTrue(pending.Published);
            Assert.IsTrue(pending.Complete(out error),error,"Late observation may acknowledge, never replay, the completed effect");
        }
    }
}
