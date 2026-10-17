// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        static readonly Vector3 PendingPlacementPosition=new(.25f,1.5f,1.1f);
        bool BeginPlacementTest(bool resize,string id,out RoomEditor.PendingPersistedEdit pending,out string error,Action<string> checkpoint=null)=>
            resize?editor.BeginResize(id,1.4f,out pending,out error,checkpoint):
                editor.BeginMove(id,PendingPlacementPosition,out pending,out error,checkpoint);
        static void PlacementEquals(RoomObjectData actual,RoomObjectData original,bool resize,bool changed)
        {
            Assert.AreEqual(changed&&!resize?PendingPlacementPosition:original.position,actual.position);
            Assert.AreEqual(changed&&resize?1.4f:original.scale,actual.scale);
            Assert.AreEqual(original.rotation,actual.rotation);
        }
        [UnityTest] public IEnumerator PendingPlacementConfirmsSavingPreservesOtherPoseFieldsAndHasOneUndo()
        {
            string id=editor.Identity(block);
            foreach(bool resize in new[]{false,true}){
                // Undo is dirty until saved; each independent preparation starts
                // from a confirmed baseline rather than the preceding iteration.
                Assert.IsTrue(editor.TryFlush(out var flushError),flushError);
                var before=editor.Read(id);
                Assert.IsTrue(BeginPlacementTest(resize,id,out var pending,out var error),error);
                using(pending){
                    yield return Prepared(pending);
                    PlacementEquals(editor.Read(id),before,resize,false);
                    PlacementEquals(new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==id),before,resize,false);
                    yield return Completed(pending);Assert.IsTrue(pending.Published);
                    PlacementEquals(editor.Read(id),before,resize,true);
                    Assert.AreEqual(editor.Read(id).position,editor.Find(id).transform.localPosition);
                    Assert.AreEqual(editor.Read(id).scale,editor.Find(id).transform.localScale.x);
                    PlacementEquals(new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==id),before,resize,true);
                    editor.Undo();PlacementEquals(editor.Read(id),before,resize,false);
                    Assert.IsTrue(pending.Complete(out error),error);
                    PlacementEquals(editor.Read(id),before,resize,false);
                    editor.Redo();PlacementEquals(editor.Read(id),before,resize,true);
                    editor.Undo();
                }
                Assert.IsTrue(editor.WriteGate.CanFreeze(out error),error);
            }
        }
        [UnityTest] public IEnumerator PendingPlacementCancellationAndStaleAuthoredEditsPreserveOriginalPlacement()
        {
            string id=editor.Identity(block);
            foreach(bool resize in new[]{false,true}){
                var before=editor.Read(id);
                Assert.IsTrue(BeginPlacementTest(resize,id,out var cancelled,out var error),error);
                yield return Prepared(cancelled);cancelled.Dispose();yield return Drained(cancelled);
                Assert.IsFalse(cancelled.Complete(out _));Assert.IsFalse(cancelled.Accepted);
                PlacementEquals(editor.Read(id),before,resize,false);
                Assert.IsTrue(BeginPlacementTest(resize,id,out var stale,out error),error);
                using(stale){
                    yield return Prepared(stale);
                    string other=editor.Snapshot().objects.First(x=>!x.IsBuiltIn&&x.id!=id).id;
                    var color=resize?Color.blue:Color.red;
                    Assert.IsTrue(editor.PaintObject(other,color,out error),error);
                    yield return Rejected(stale);Assert.IsFalse(stale.Accepted);
                    PlacementEquals(editor.Read(id),before,resize,false);
                    var saved=new RoomStorage(directory).Load(out _);
                    PlacementEquals(saved.objects.Single(x=>x.id==id),before,resize,false);
                    Assert.AreEqual(color,saved.objects.Single(x=>x.id==other).color);
                }
                yield return Drained(stale);Assert.IsTrue(editor.WriteGate.CanFreeze(out error),error);
            }
        }
        [UnityTest] public IEnumerator PendingPlacementStoppedAfterAcceptanceStillSavesWithoutReplaying()
        {
            string id=editor.Identity(block);string path=Path.Combine(directory,RoomStorage.FileName);
            foreach(bool resize in new[]{false,true}){
                var before=editor.Read(id);var oldBytes=File.ReadAllBytes(path);
                using var pause=new PublicationPause();
                Assert.IsTrue(BeginPlacementTest(resize,id,out var pending,out var error,pause.Check),error);
                try {
                    yield return Prepared(pending);
                    Assert.IsTrue(pending.Poll(out var ready,out error),error);Assert.IsFalse(ready);
                    Assert.IsTrue(pending.Accepted);Assert.IsFalse(pending.Published);
                    PlacementEquals(editor.Read(id),before,resize,true);
                    // Read exact file bytes without acquiring the worker's directory coordinator.
                    CollectionAssert.AreEqual(oldBytes,File.ReadAllBytes(path));
                    pending.Dispose();Assert.IsFalse(pending.Drained);
                    Assert.IsFalse(editor.WriteGate.CanFreeze(out _));
                    Assert.IsFalse(pending.Complete(out _));StringAssert.Contains("accepted",pending.InterruptionStatus);
                } finally {pause.Release();}
                yield return Drained(pending);Assert.IsTrue(pending.Published);
                PlacementEquals(new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==id),before,resize,true);
                Assert.IsTrue(editor.WriteGate.CanFreeze(out error),error);
                editor.Undo();Assert.IsFalse(pending.Complete(out _));
                PlacementEquals(editor.Read(id),before,resize,false);
            }
        }
        IEnumerator TemporaryPlacementSaveDone()
        {
            float end=Time.realtimeSinceStartup+20;
            while(editor.TemporarySavePending){Assert.Less(Time.realtimeSinceStartup,end);yield return null;}
            Assert.IsNull(editor.TemporarySaveError);
        }
        [UnityTest] public IEnumerator PendingPlacementInTemporaryRoomNeedsExplicitSnapshotToBecomeDurable()
        {
            string id=editor.Identity(block);
            foreach(bool resize in new[]{false,true}){
                var before=editor.Read(id);
                Assert.IsTrue(editor.BeginTemporaryRoom(out var error),error);
                yield return TemporaryPlacementSaveDone();
                Assert.IsTrue(BeginPlacementTest(resize,id,out var pending,out error),error);
                using(pending){
                    yield return Completed(pending);Assert.IsTrue(pending.Accepted);Assert.IsFalse(pending.Published);
                    StringAssert.Contains("temporary room edit",pending.InterruptionStatus);
                    PlacementEquals(editor.Read(id),before,resize,true);
                    PlacementEquals(new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==id),before,resize,false);
                    editor.Undo();PlacementEquals(editor.Read(id),before,resize,false);
                    editor.Redo();PlacementEquals(editor.Read(id),before,resize,true);
                    Assert.IsTrue(editor.KeepTemporaryRoom(out error),error);
                    yield return TemporaryPlacementSaveDone();
                    PlacementEquals(new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==id),before,resize,true);
                    Assert.IsTrue(editor.DiscardTemporaryRoom(out error),error);
                    PlacementEquals(editor.Read(id),before,resize,true);
                }
            }
        }
        [UnityTest] public IEnumerator PendingPlacementWaitersCancelIndependentlyAndUseTheLatestSavedBaseline()
        {
            string id=editor.Identity(block);var original=editor.Read(id).position;
            string other=editor.Snapshot().objects.First(x=>!x.IsBuiltIn&&x.id!=id).id;
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.Euler(0,45,0),out var blocker,out var error),error);
            using(blocker){
                yield return Prepared(blocker);
                Assert.IsTrue(runtime.Scheduler.Invoke(NativeMove(id),Time.unscaledTime,out var move,out error),error);
                Assert.IsTrue(runtime.Scheduler.Invoke(ObjectEditCall("object.scale.set",other,("scale",1.4)),Time.unscaledTime,out var resize,out error),error);
                Assert.AreEqual("preparing",(string)runtime.Scheduler.Invocation(move)["phase"]);
                Assert.AreEqual("preparing",(string)runtime.Scheduler.Invocation(resize)["phase"]);
                Assert.IsTrue(runtime.Scheduler.CancelInvocation(move,out error),error);
                blocker.Dispose();yield return Drained(blocker);
                yield return NativeActionDone(resize);
                Assert.AreEqual("cancelled",(string)runtime.Scheduler.Invocation(move)["phase"]);
                Assert.AreEqual(original,editor.Read(id).position);Assert.AreEqual(1.4f,editor.Read(other).scale);
                var saved=new RoomStorage(directory).Load(out _);
                Assert.AreEqual(original,saved.objects.Single(x=>x.id==id).position);
                Assert.AreEqual(1.4f,saved.objects.Single(x=>x.id==other).scale);
            }
        }
        [UnityTest] public IEnumerator PendingPlacementWaiterRevalidatesTargetAndTemporarySessionBeforeCapture()
        {
            string id=editor.Identity(block);var original=editor.Read(id).position;
            foreach(bool temporary in new[]{false,true}){
                Assert.IsTrue(editor.BeginRotation("book",Quaternion.Euler(0,45,0),out var blocker,out var error),error);
                using(blocker){
                    yield return Prepared(blocker);
                    Assert.IsTrue(PersistedEditOperation.Start(editor,id,
                        (out RoomEditor.PendingPersistedEdit edit,out string issue)=>editor.BeginMove(id,PendingPlacementPosition,out edit,out issue),
                        out var waiting,out error),error);
                    try {
                        Assert.AreEqual(RuleActionState.Preparing,waiting.State(out error),error);
                        if(!temporary)Assert.IsTrue(editor.PaintObject(id,Color.red,out error),error);
                        blocker.Dispose();yield return Drained(blocker);
                        if(temporary){Assert.IsTrue(editor.BeginTemporaryRoom(out error),error);yield return TemporaryPlacementSaveDone();}
                        Assert.AreEqual(RuleActionState.Failed,waiting.State(out error));
                        StringAssert.Contains(temporary?"workspace changed":"target changed",error);
                        Assert.IsFalse(waiting.Complete(out _));Assert.AreEqual(original,editor.Read(id).position);
                        if(temporary)Assert.IsTrue(editor.DiscardTemporaryRoom(out error),error);
                    }finally{waiting.Stop(false);}
                }
            }
        }
        [UnityTest] public IEnumerator PendingPlacementRejectsInvalidBoundsAndFrameWithoutLeavingOwnership()
        {
            string id=editor.Identity(block);var before=editor.Read(id);
            var oldBytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));
            Assert.IsFalse(editor.BeginMove(id,new Vector3(float.NaN,1,0),out _,out _));
            Assert.IsFalse(editor.BeginMove(id,new Vector3(25,25,0),out _,out _));
            Assert.IsFalse(editor.BeginResize(id,5,out _,out _));
            Assert.IsFalse(editor.BeginResize("book",2,out _,out _));
            root.transform.localScale=new Vector3(1,2,1);
            Assert.IsFalse(BeginPlacementTest(false,id,out _,out _));
            Assert.IsFalse(BeginPlacementTest(true,id,out _,out _));
            root.transform.localScale=Vector3.one;
            PlacementEquals(editor.Read(id),before,false,false);
            CollectionAssert.AreEqual(oldBytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));
            Assert.IsTrue(editor.WriteGate.CanFreeze(out var error),error);
            yield return null;
        }
    }
}
