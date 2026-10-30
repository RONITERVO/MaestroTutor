// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class AnimationWorkshopTests
    {
        IEnumerator RecordingSettled(bool success=true)
        {
            float end=Time.realtimeSinceStartup+20;
            while(workshop.IsSavingRecording){Assert.Less(Time.realtimeSinceStartup,end,"Recording publication timed out");yield return null;}
            Assert.AreEqual(!success,workshop.HasUnsavedRecording,workshop.Status);
            yield return RecordingDrained();
        }
        IEnumerator RecordingDrained()
        {
            float end=Time.realtimeSinceStartup+20;
            while(editor.PendingEditSave){Assert.Less(Time.realtimeSinceStartup,end,"Recording worker did not drain");yield return null;}
        }
        IEnumerator StartPhysicalTake()
        {
            editor.Select(avatarItem);workshop.ToggleRecord();Assert.IsTrue(workshop.IsRecording,workshop.Status);
            yield return new WaitForSeconds(.12f);avatarItem.transform.localPosition+=Vector3.right*.2f;
        }
        [UnityTest] public IEnumerator RecordingCompletionSharesPhysicalFinishAndSurvivesReceiptCancellation()
        {
            AuthorRuntime();yield return StartPhysicalTake();
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.Euler(0,10,0),out var blocker,out var error),error);
            try{
                workshop.ToggleRecord();Assert.IsFalse(workshop.IsRecording);Assert.IsTrue(workshop.IsSavingRecording);
                Assert.AreEqual("saving",(string)RecordingFact()["phase"]);var session=workshop.RecordingSessionId;
                Assert.IsTrue(workshop.BeginFinishRecording(session,"maestro",out var attempt,out error),error);
                var call=RecordingCall("finish");
                Assert.IsTrue(BehaviourCatalog.Action("animation.record").Module.Start(new CapabilityContext(editor,workshop),"observe", (Newtonsoft.Json.Linq.JObject)call["arguments"],out var op,out error),error);
                Assert.AreEqual(RuleActionState.Preparing,op.State(out error),error);op.Stop(false);
                Assert.That(op.InterruptionStatus,Does.Contain("continues saving"));
                Assert.IsTrue(workshop.IsSavingRecording);Assert.IsNull(editor.Read("maestro").motion);
                blocker.Dispose();yield return RecordingSettled();
                Assert.IsTrue(attempt.Poll(out var ready,out error),error);Assert.IsTrue(ready);
                int revision=(int)attempt.Result["revision"];var receipt=attempt.Result;receipt["frames"]=301;
                Assert.AreNotEqual(301,(int)attempt.Result["frames"]);
                Assert.IsTrue(editor.SaveAnimation("maestro",null,null,false));
                Assert.Greater(editor.ObjectRevision("maestro"),revision);Assert.AreEqual(revision,(int)attempt.Result["revision"]);
                Assert.AreEqual(RuleActionState.Failed,op.State(out _));Assert.IsFalse(op.Complete(out _));
            }finally{blocker.Dispose();}
        }
        [UnityTest] public IEnumerator RecordingDiscardCancelsQueuedSaveAndOldObserverCannotTouchTheNextTake()
        {
            yield return StartPhysicalTake();
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);
            try{
                int revision=editor.ObjectRevision("maestro");string session=workshop.RecordingSessionId;
                Assert.IsTrue(workshop.BeginFinishRecording(session,"maestro",out var attempt,out error),error);
                Assert.IsTrue(workshop.DiscardRecording(session,out _,out error),error);
                Assert.IsFalse(attempt.Poll(out _,out error));Assert.IsNull(editor.Read("maestro").motion);Assert.AreEqual(revision,editor.ObjectRevision("maestro"));
                blocker.Dispose();yield return RecordingDrained();
                yield return StartPhysicalTake();string next=workshop.RecordingSessionId;
                Assert.AreNotEqual(session,next);Assert.IsFalse(attempt.Poll(out _,out _));
                Assert.IsTrue(workshop.IsRecording);Assert.AreEqual(next,workshop.RecordingSessionId);workshop.DiscardTake();
            }finally{blocker.Dispose();}
        }
        sealed class RecordingPublicationPause:IDisposable
        {
            readonly ManualResetEventSlim release=new();
            internal readonly ManualResetEventSlim Entered=new();
            internal int Worker;
            internal void Check(string phase){
                if(phase!="publishing")return;
                Worker=Thread.CurrentThread.ManagedThreadId;Entered.Set();
                if(!release.Wait(10000))throw new TimeoutException("Recording publication barrier timed out");
            }
            internal void Release()=>release.Set();
            public void Dispose(){release.Dispose();Entered.Dispose();}
        }
        [UnityTest] public IEnumerator RecordingAcceptedSaveWaitsForDiskKeepsFrozenFramesAndRejectsPrematureDiscard()
        {
            yield return StartPhysicalTake();using var pause=new RecordingPublicationPause();int ownerThread=Thread.CurrentThread.ManagedThreadId;
            Assert.IsTrue(workshop.BeginFinishRecording(workshop.RecordingSessionId,"maestro",out var attempt,out var error,pause.Check),error);
            try{
                float end=Time.realtimeSinceStartup+20;
                while(!pause.Entered.IsSet){Assert.Less(Time.realtimeSinceStartup,end);yield return null;}
                Assert.IsTrue(attempt.Accepted);Assert.IsFalse(attempt.Ended);Assert.AreNotEqual(ownerThread,pause.Worker);
                var frozen=RecordingFact();Assert.AreEqual("saving",(string)frozen["phase"]);
                Assert.IsFalse(workshop.DiscardRecording(workshop.RecordingSessionId,out _,out error));Assert.That(error,Does.Contain("still saving"));
                Assert.IsTrue(workshop.BeginFinishRecording(workshop.RecordingSessionId,"maestro",out var same,out error),error);Assert.AreSame(attempt,same);
                avatarItem.transform.localPosition+=Vector3.right;yield return null;Assert.AreEqual(frozen["frames"],RecordingFact()["frames"]);
                Assert.IsNull(attempt.Result);
                using(editor.RuntimeGate.Hold("Synthetic focus boundary after acceptance")){
                    Assert.IsTrue(attempt.Accepted);Assert.IsFalse(attempt.Ended);
                    pause.Release();yield return RecordingSettled();
                }
                Assert.AreEqual((int)frozen["frames"],(int)attempt.Result["frames"]);
                var saved=new RoomStorage(directory).Load(out error).objects.Single(x=>x.id=="maestro").motion;
                Assert.AreEqual((int)frozen["frames"],saved.frames.Length);Assert.Less(saved.frames[^1].position.x-saved.frames[0].position.x,.3f);
                Assert.IsFalse(workshop.IsPlaying);
            }finally{pause.Release();}
        }
        [UnityTest] public IEnumerator RecordingPublicationFailureRetainsAppliedBufferWithoutReplayingIt()
        {
            yield return StartPhysicalTake();editor.SaveNow();yield return null;
            var bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));int revision=editor.ObjectRevision("maestro");string session=workshop.RecordingSessionId;
            Assert.IsTrue(workshop.BeginFinishRecording(session,"maestro",out var attempt,out var error,
                phase=>{if(phase=="publishing")throw new IOException("Synthetic recording publication failure");}),error);
            yield return RecordingSettled(false);Assert.IsTrue(attempt.Accepted);Assert.IsFalse(attempt.Poll(out _,out error));
            Assert.That(error,Does.Contain("applied"));Assert.That(error,Does.Contain("unconfirmed"));
            Assert.Greater(editor.ObjectRevision("maestro"),revision);Assert.IsNotNull(editor.Read("maestro").motion);
            Assert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));
            Assert.IsFalse(workshop.BeginFinishRecording(session,"maestro",out _,out _));Assert.IsFalse(editor.CanSaveRoom);
            Assert.IsTrue(workshop.DiscardRecording(session,out _,out error),error);
            Assert.IsNotNull(editor.Read("maestro").motion);Assert.That(workshop.Status,Does.Contain("applied animation remains"));
        }
        [UnityTest] public IEnumerator RecordingDisableRetainsUnacceptedTakeForRetryAndDoesNotLeakAdmission()
        {
            yield return StartPhysicalTake();
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);
            try{
                Assert.IsTrue(workshop.BeginFinishRecording(workshop.RecordingSessionId,"maestro",out var attempt,out error),error);
                workshop.enabled=false;Assert.IsTrue(attempt.Ended);Assert.IsFalse(attempt.Accepted);Assert.IsTrue(workshop.HasUnsavedRecording);
                Assert.IsNull(editor.Read("maestro").motion);Assert.IsFalse(workshop.IsRecording);
                blocker.Dispose();yield return RecordingDrained();workshop.enabled=true;
                Assert.IsTrue(workshop.BeginFinishRecording(workshop.RecordingSessionId,"maestro",out var retry,out error),error);
                Assert.AreNotSame(attempt,retry);yield return RecordingSettled();
                Assert.IsNotNull(editor.Read("maestro").motion);Assert.IsFalse(attempt.Poll(out _,out _));
            }finally{blocker.Dispose();workshop.enabled=true;}
        }
        [UnityTest] public IEnumerator RecordingSuspensionRetiresPreparedCandidateAndRejoinsAfterResume()
        {
            yield return StartPhysicalTake();
            Assert.IsTrue(workshop.BeginFinishRecording(workshop.RecordingSessionId,"maestro",out var attempt,out var error),error);
            Assert.IsTrue(attempt.Poll(out var ready,out error),error);Assert.IsFalse(ready);Assert.IsFalse(attempt.Accepted);
            using(editor.RuntimeGate.Hold("Synthetic suspension during recording preparation")){
                yield return RecordingDrained();yield return null;
                Assert.IsTrue(workshop.IsSavingRecording);Assert.IsFalse(attempt.Ended);Assert.IsNull(editor.Read("maestro").motion);
            }
            yield return RecordingSettled();Assert.IsTrue(attempt.Accepted);Assert.IsNotNull(attempt.Result);
        }
        [UnityTest] public IEnumerator RecordingSaveRefusesANewerActorWithoutInterruptingIt()
        {
            foreach(bool gesture in new[]{true,false}){
                string id=gesture?"maestro":editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block).id;
                var item=editor.Find(id);editor.Select(item);workshop.ToggleRecord();Assert.IsTrue(workshop.IsRecording,workshop.Status);
                yield return new WaitForSeconds(.12f);int revision=editor.ObjectRevision(id);
                Assert.IsTrue(workshop.BeginFinishRecording(workshop.RecordingSessionId,id,out var attempt,out var error),error);
                if(!gesture){Assert.IsTrue(attempt.Poll(out var ready,out error),error);Assert.IsFalse(ready);}
                var body=item.GetComponent<RigidRoomItem>();var actor=new object();
                try{
                    if(gesture)Assert.IsTrue(avatar.BeginUpperBody("new-actor","Greeting"));else{Assert.IsNotNull(body);body.SetAnimationOwner(actor,true);}
                    Assert.IsFalse(attempt.Poll(out _,out error));Assert.IsFalse(attempt.Accepted);Assert.IsTrue(workshop.HasUnsavedRecording);
                    Assert.AreEqual(revision,editor.ObjectRevision(id));
                    if(gesture)Assert.IsTrue(avatar.UpperBodyOwnedBy("new-actor"));else Assert.IsTrue(body.AnimationOwned);
                }finally{
                    if(gesture)avatar.EndUpperBody("new-actor");else if(body)body.SetAnimationOwner(actor,false);
                    workshop.DiscardTake();
                }
                yield return RecordingDrained();
            }
        }
        [UnityTest] public IEnumerator RecordingQueuedSaveRefusesNewAuthoredRevisionAndRetainsTake()
        {
            yield return StartPhysicalTake();string session=workshop.RecordingSessionId;
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);
            try{
                Assert.IsTrue(workshop.BeginFinishRecording(session,"maestro",out var attempt,out error),error);
                blocker.Dispose();yield return RecordingDrained();
                // No workshop tick between releasing the drained writer and this newer physical edit.
                Assert.IsTrue(editor.MoveObject("maestro",Vector3.right,out error),error);
                yield return RecordingSettled(false);Assert.IsFalse(attempt.Accepted);Assert.IsNull(editor.Read("maestro").motion);
                Assert.AreEqual(session,workshop.RecordingSessionId);workshop.DiscardTake();
            }finally{blocker.Dispose();}
        }
    }
}
