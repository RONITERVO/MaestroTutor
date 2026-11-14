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
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        bool BeginContentTest(bool delete,string id,out RoomEditor.PendingPersistedEdit pending,out string error,Action<string> checkpoint=null)=>
            delete?editor.BeginDelete(id,out pending,out error,checkpoint):editor.BeginPaint(id,Color.blue,out pending,out error,checkpoint);
        [UnityTest] public IEnumerator PendingContentCancellationAndAuthoredChangesNeverPublishTheCandidate()
        {
            string id=editor.Identity(block),other=editor.Snapshot().objects.First(x=>!x.IsBuiltIn&&x.id!=id).id;
            foreach(bool delete in new[]{false,true}){
                Assert.IsTrue(editor.TryFlush(out var error),error);string before=JsonUtility.ToJson(editor.Read(id));
                Assert.IsTrue(BeginContentTest(delete,id,out var pending,out error),error);
                yield return Prepared(pending);pending.Dispose();yield return Drained(pending);
                Assert.IsFalse(pending.Accepted);Assert.IsFalse(pending.Complete(out _));Assert.AreEqual(before,JsonUtility.ToJson(editor.Read(id)));
                Assert.IsTrue(BeginContentTest(delete,id,out var stale,out error),error);
                using(stale){
                    yield return Prepared(stale);Assert.IsTrue(editor.PaintObject(other,delete?Color.red:Color.green,out error),error);
                    yield return Rejected(stale);Assert.IsFalse(stale.Accepted);
                    Assert.AreEqual(before,JsonUtility.ToJson(editor.Read(id)));
                    Assert.AreEqual(before,JsonUtility.ToJson(new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==id)));
                }
                yield return Drained(stale);Assert.IsTrue(editor.WriteGate.CanFreeze(out error),error);
            }
        }
        [UnityTest] public IEnumerator PendingPaintKeepsMovingBodyAndLocalAppearanceWithoutChangingSharedStyle()
        {
            string appearance=NewAppearance();
            Assert.IsTrue(editor.CreatePrimitive(RoomObjectKind.Ball,"Paint in motion",new Vector3(0,1.5f,1),1,Color.white,out var id,out var error),error);
            Assert.IsTrue(AssignAppearance(id,appearance,out error),error);
            var style=JsonUtility.ToJson(editor.ReadAppearance(appearance));
            physics.SetSurfaces(true,"Ready");physics.StartPhysics();
            var item=editor.Find(id);var body=item.GetComponent<Rigidbody>();
            yield return new WaitForFixedUpdate();Assert.IsFalse(body.isKinematic);
            var original=editor.Read(id);Assert.IsTrue(editor.BeginPaint(id,Color.blue,out var pending,out error),error);
            using(pending){
                yield return Prepared(pending);Assert.IsFalse(body.isKinematic);
                var position=item.transform.position;
                body.linearVelocity=Vector3.right;body.angularVelocity=Vector3.up;
                Assert.IsTrue(pending.Poll(out _,out error),error);Assert.IsTrue(pending.Accepted);
                Assert.AreEqual(position,item.transform.position);Assert.AreEqual(Vector3.right,body.linearVelocity);Assert.AreEqual(Vector3.up,body.angularVelocity);
                Assert.AreEqual("#0000FF",editor.Read(id).appearanceBindings[0].tint);
                Assert.AreEqual(style,JsonUtility.ToJson(editor.ReadAppearance(appearance)));
                yield return Completed(pending);
                Assert.AreEqual("#0000FF",new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==id).appearanceBindings[0].tint);
                physics.PausePhysics();editor.Undo();Assert.AreEqual(original.appearanceBindings[0].tint,editor.Read(id).appearanceBindings[0].tint);
                Assert.IsTrue(pending.Complete(out error),error);Assert.AreEqual(original.appearanceBindings[0].tint,editor.Read(id).appearanceBindings[0].tint);
            }
        }
        [UnityTest] public IEnumerator PendingContentCanEditWithoutPlacementFrameAndDeleteWithoutNativeView()
        {
            string id=editor.Identity(block),lost=editor.Snapshot().objects.First(x=>!x.IsBuiltIn&&x.id!=id).id;
            UnityEngine.Object.DestroyImmediate(editor.Find(lost).gameObject);
            root.transform.localScale=new Vector3(1,2,1);
            try {
                Assert.IsFalse(editor.Frame.Valid);Assert.IsFalse(editor.BeginMove(id,Vector3.one,out _,out _));
                Assert.IsTrue(editor.BeginPaint(id,Color.blue,out var paint,out var error),error);
                using(paint){yield return Completed(paint);}
                Assert.IsTrue(editor.BeginDelete(id,out var deletion,out error),error);
                using(deletion){yield return Completed(deletion);}
                Assert.IsFalse(editor.HasSavedObject(id));Assert.IsNull(editor.Find(lost));Assert.IsTrue(editor.HasSavedObject(lost));
                Assert.IsTrue(editor.BeginDelete(lost,out var unavailable,out error),error);
                using(unavailable){yield return Completed(unavailable);}
                Assert.IsFalse(editor.HasSavedObject(lost));Assert.IsFalse(editor.NativeActivationPending);
            }finally{root.transform.localScale=Vector3.one;}
        }
        [UnityTest] public IEnumerator PendingContentStopAfterAcceptanceStillPublishesAndUndoIsNotReplayed()
        {
            string id=editor.Identity(block),path=Path.Combine(directory,RoomStorage.FileName);
            foreach(bool delete in new[]{false,true}){
                Assert.IsTrue(editor.TryFlush(out var error),error);string before=JsonUtility.ToJson(editor.Read(id));var bytes=File.ReadAllBytes(path);
                using var pause=new PublicationPause();
                Assert.IsTrue(BeginContentTest(delete,id,out var pending,out error,pause.Check),error);
                try {
                    yield return Prepared(pending);Assert.AreEqual(before,JsonUtility.ToJson(editor.Read(id)));
                    Assert.IsTrue(pending.Poll(out var ready,out error),error);Assert.IsFalse(ready);Assert.IsTrue(pending.Accepted);
                    CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));pending.Dispose();
                    Assert.IsFalse(pending.Drained);Assert.IsFalse(pending.Complete(out _));Assert.IsFalse(editor.WriteGate.CanFreeze(out _));
                }finally{pause.Release();}
                yield return Drained(pending);Assert.IsTrue(pending.Published);
                var saved=new RoomStorage(directory).Load(out _).objects.SingleOrDefault(x=>x.id==id);
                if(delete)Assert.IsNull(saved);else Assert.AreEqual(Color.blue,saved.color);
                editor.Undo();Assert.AreEqual(before,JsonUtility.ToJson(editor.Read(id)));Assert.IsFalse(pending.Complete(out _));
                Assert.AreEqual(before,JsonUtility.ToJson(editor.Read(id)));Assert.IsTrue(editor.WriteGate.CanFreeze(out error),error);
            }
        }
        [UnityTest] public IEnumerator PendingContentTemporaryChangesRequireKeepAndPreserveUndo()
        {
            string id=editor.Identity(block);
            foreach(bool delete in new[]{false,true}){
                string before=JsonUtility.ToJson(editor.Read(id));
                Assert.IsTrue(editor.BeginTemporaryRoom(out var error),error);yield return TemporaryPlacementSaveDone();
                Assert.IsTrue(BeginContentTest(delete,id,out var pending,out error),error);
                using(pending){
                    yield return Completed(pending);Assert.IsFalse(pending.Published);Assert.IsTrue(pending.Accepted);
                    Assert.AreEqual(before,JsonUtility.ToJson(new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==id)));
                    editor.Undo();Assert.AreEqual(before,JsonUtility.ToJson(editor.Read(id)));editor.Redo();
                    Assert.IsTrue(editor.KeepTemporaryRoom(out error),error);yield return TemporaryPlacementSaveDone();
                    var saved=new RoomStorage(directory).Load(out _).objects.SingleOrDefault(x=>x.id==id);
                    if(delete)Assert.IsNull(saved);else Assert.AreEqual(Color.blue,saved.color);
                    Assert.IsTrue(editor.DiscardTemporaryRoom(out error),error);
                    if(delete)Assert.IsFalse(editor.HasSavedObject(id));else Assert.AreEqual(Color.blue,editor.Read(id).color);
                }
            }
        }
        [UnityTest] public IEnumerator PendingContentStorageRefusalKeepsSourceUntilPreparationCanRetry()
        {
            string id=editor.Identity(block),obstacle=Path.Combine(directory,RoomStorage.FileName+".pending");
            foreach(bool delete in new[]{false,true}){
                string before=JsonUtility.ToJson(editor.Read(id));int revision=editor.Revision;
                Directory.CreateDirectory(obstacle);
                try{
                    Assert.IsTrue(BeginContentTest(delete,id,out var failed,out var error),error);
                    using(failed){yield return Rejected(failed);Assert.IsFalse(failed.Accepted);}
                    yield return Drained(failed);Assert.AreEqual(revision,editor.Revision);
                    Assert.AreEqual(before,JsonUtility.ToJson(editor.Read(id)));
                }finally{Directory.Delete(obstacle);}
                Assert.IsTrue(BeginContentTest(delete,id,out var retry,out var retryError),retryError);
                using(retry){yield return Completed(retry);Assert.IsTrue(retry.Published);}
                editor.Undo();Assert.AreEqual(before,JsonUtility.ToJson(editor.Read(id)));
                Assert.IsTrue(editor.TryFlush(out retryError),retryError);
            }
        }
        [UnityTest] public IEnumerator PendingContentRefusesAHandTakingTheTargetBeforeAcceptance()
        {
            string id=editor.Identity(block);
            foreach(bool delete in new[]{false,true}){
                string before=JsonUtility.ToJson(editor.Read(id));
                Assert.IsTrue(BeginContentTest(delete,id,out var pending,out var error),error);
                using(pending){
                    yield return Prepared(pending);var hand=Hand(1,block.transform.position);
                    manager.SelectEnter((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)hand,block.Grab);
                    try{
                        yield return Rejected(pending);Assert.IsFalse(pending.Accepted);
                        Assert.AreEqual(before,JsonUtility.ToJson(editor.Read(id)));Assert.IsTrue(block.Grab.isSelected);
                    }finally{manager.SelectExit((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)hand,block.Grab);}
                }
                yield return Drained(pending);
            }
        }
        [UnityTest] public IEnumerator PendingPaintNeverRepairsANativeTargetLostDuringPreparation()
        {
            string id=editor.Identity(block),before=JsonUtility.ToJson(editor.Read(id));
            Assert.IsTrue(editor.BeginPaint(id,Color.blue,out var pending,out var error),error);
            using(pending){
                yield return Prepared(pending);UnityEngine.Object.DestroyImmediate(block.gameObject);
                yield return Rejected(pending);Assert.IsFalse(pending.Accepted);Assert.IsFalse((bool)editor.Find(id));
                Assert.AreEqual(before,JsonUtility.ToJson(editor.Read(id)));Assert.IsFalse(editor.NativeActivationPending);
            }
            yield return Drained(pending);Assert.IsTrue(editor.WriteGate.CanFreeze(out error),error);
        }
        [UnityTest] public IEnumerator PendingContentSharesPlacementQueueWithoutAnUnpolledWaiterBeingOvertaken()
        {
            string id=editor.Identity(block),other=editor.Snapshot().objects.First(x=>!x.IsBuiltIn&&x.id!=id).id;
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.Euler(0,45,0),out var blocker,out var error),error);
            CapabilityOperation first=null,later=null;
            try{
                yield return Prepared(blocker);
                Assert.IsTrue(PersistedEditOperation.Start(editor,id,
                    (out RoomEditor.PendingPersistedEdit edit,out string issue)=>editor.BeginPaint(id,Color.blue,out edit,out issue),
                    out first,out error,(out string issue)=>editor.CanEditObject(id,true,out issue)),error);
                blocker.Dispose();yield return Drained(blocker);
                UnityEngine.Object.DestroyImmediate(editor.Find(other).gameObject);
                Assert.IsTrue(PersistedEditOperation.Start(editor,other,
                    (out RoomEditor.PendingPersistedEdit edit,out string issue)=>editor.BeginDelete(other,out edit,out issue),
                    out later,out error,(out string issue)=>editor.CanDeleteObject(other,out issue)),error);
                Assert.AreEqual(RuleActionState.Preparing,later.State(out error),error);Assert.IsTrue(editor.HasSavedObject(other));
                yield return PlacementOperationDone(first);Assert.IsTrue(editor.HasSavedObject(other));
                yield return PlacementOperationDone(later);Assert.IsFalse(editor.HasSavedObject(other));Assert.AreEqual(Color.blue,editor.Read(id).color);
            }finally{first?.Stop(false);later?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingContentWaitingDeleteRevalidatesItsSavedIdentityBeforePreparation()
        {
            string id=editor.Identity(block);CapabilityOperation waiting=null;
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.Euler(0,45,0),out var blocker,out var error),error);
            try {
                yield return Prepared(blocker);
                Assert.IsTrue(PersistedEditOperation.Start(editor,id,
                    (out RoomEditor.PendingPersistedEdit edit,out string issue)=>editor.BeginDelete(id,out edit,out issue),
                    out waiting,out error,(out string issue)=>editor.CanDeleteObject(id,out issue)),error);
                Assert.IsTrue(editor.PaintObject(id,Color.red,out error),error);
                blocker.Dispose();yield return Drained(blocker);
                Assert.AreEqual(RuleActionState.Failed,waiting.State(out error));StringAssert.Contains("target changed",error);
                Assert.IsTrue(editor.HasSavedObject(id));Assert.AreEqual(Color.red,editor.Read(id).color);
            }finally{waiting?.Stop(false);blocker.Dispose();}
        }
    }
}
