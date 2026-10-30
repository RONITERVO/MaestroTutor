// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class WorkspaceHostTests
    {
        [UnityTest] public IEnumerator WorkspaceRetirementOwnsOrdinaryCheckpointsAfterItsUnityObjectsAreDestroyed()=>RetireCheckpoints(false);
        [UnityTest] public IEnumerator LateRetirementReadsDestroyedOwnersWithoutCallingTheirUnityApis()=>RetireCheckpoints(true);
        sealed class RetirementPublicationHold:System.IDisposable
        {
            readonly ManualResetEventSlim release=new(false);
            readonly TaskCompletionSource<bool> entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal Task Worker {get;}
            internal Task Ready=>entered.Task;
            internal RetirementPublicationHold(object gate){Worker=Task.Run(()=>{lock(gate){entered.TrySetResult(true);release.Wait(10000);}});}
            internal void Release()=>release.Set();
            public void Dispose(){Release();Worker.Wait(5000);release.Dispose();}
        }
        [UnityTest] public IEnumerator RetirementWaitsForSaveFailureRetriesDispatchedDuringDestruction()
        {
            yield return ReadyForReview();DisableAcceptedSaveUpdates();
            var content=host.Current;var editor=content.Editor;var rules=content.Rules;
            yield return TestLifecycleDrain.Wait(Task.WhenAll(editor.LifecycleCompletion,rules.LifecycleCompletion));
            var changed=AcceptedEdit("Retry belongs to the retiring generation");rules.NewSequence();int count=rules.Snapshot().sequences.Length;
            string roomBlock=Path.Combine(editor.SaveDirectory,Maestro.Quest.Creation.RoomStorage.FileName+".pending");
            string ruleBlock=Path.Combine(editor.SaveDirectory,"behaviours.v2.json.pending");
            RetirementPublicationHold roomHold=null,ruleHold=null;
            try{
                Directory.CreateDirectory(roomBlock);Directory.CreateDirectory(ruleBlock);
                var retired=content.Retire();var firstRoom=AcceptedWorker(editor);var firstRules=AcceptedWorker(rules);
                // Only the test waits: force detached IO to fail before Unity
                // reaches OnDestroy, which must then capture and retry the save.
                Assert.IsTrue(Task.WhenAll(firstRoom,firstRules).Wait(5000),"Initial detached checkpoints did not finish");
                Assert.IsNotNull(firstRoom.Result);Assert.IsNotNull(firstRules.Result);
                object StorageLock(object owner){
                    const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                    var storage=owner.GetType().GetField("storage",flags).GetValue(owner);
                    var file=storage.GetType().GetField("file",flags).GetValue(storage);
                    return file.GetType().GetField("retainedGate",flags).GetValue(file);
                }
                // Hold actual storage publication, not a replacement fake task.
                // Owner-side lifecycle callbacks do not enter these IO locks.
                roomHold=new RetirementPublicationHold(StorageLock(editor));ruleHold=new RetirementPublicationHold(StorageLock(rules));
                Assert.IsTrue(Task.WhenAll(roomHold.Ready,ruleHold.Ready).Wait(5000),"Storage holds did not start");
                for(int i=0;i<3;i++)yield return null;
                Assert.IsFalse(editor);Assert.IsFalse(rules);
                Assert.IsFalse(roomHold.Worker.IsCompleted,"Room lifecycle waited on IO");Assert.IsFalse(ruleHold.Worker.IsCompleted,"Behaviour lifecycle waited on IO");
                Assert.AreNotSame(firstRoom,editor.LifecycleCompletion);Assert.AreNotSame(firstRules,rules.LifecycleCompletion);
                Assert.IsFalse(editor.LifecycleCompletion.IsCompleted);Assert.IsFalse(rules.LifecycleCompletion.IsCompleted);
                Assert.IsFalse(retired.IsCompleted,"Retirement released the generation before its final lifecycle retries");
                Directory.Delete(roomBlock);Directory.Delete(ruleBlock);
                roomHold.Release();
                yield return TestLifecycleDrain.Wait(editor.LifecycleCompletion);
                Assert.IsFalse(retired.IsCompleted,"Behaviour checkpoint was not retained");
                ruleHold.Release();yield return TestLifecycleDrain.Wait(retired);
                AssertSavedEdit(editor,changed,count);
            }finally{
                roomHold?.Dispose();ruleHold?.Dispose();
                if(Directory.Exists(roomBlock))Directory.Delete(roomBlock);
                if(Directory.Exists(ruleBlock))Directory.Delete(ruleBlock);
            }
        }
        IEnumerator RetireCheckpoints(bool destroyFirst)
        {
            yield return ReadyForReview();DisableAcceptedSaveUpdates();
            var content=host.Current;var editor=content.Editor;var rules=content.Rules;
            var changed=AcceptedEdit("Latest accepted state before retirement");rules.NewSequence();int count=rules.Snapshot().sequences.Length;
            using var oldRoom=new DelayedAcceptedSave(editor);using var oldRules=new DelayedAcceptedSave(rules);
            Task retired;
            if(destroyFirst){Object.Destroy(content.gameObject);yield return null;retired=content.Retire();}
            else{retired=content.Retire();yield return null;}
            Assert.IsFalse(oldRoom.Released,"Retirement blocked on room IO");Assert.IsFalse(oldRules.Released,"Retirement blocked on behaviour IO");
            Assert.IsFalse(retired.IsCompleted,"Generation released before its detached snapshots completed");
            Assert.IsNull(editor.WriteGate.TryWrite(out _));Assert.IsFalse(rules.Modules.CanWrite(out _));
            oldRoom.Release();oldRules.Release();
            float end=Time.realtimeSinceStartup+10;while(!retired.IsCompleted&&Time.realtimeSinceStartup<end)yield return null;
            Assert.IsTrue(retired.IsCompleted,"Detached retirement did not drain");Assert.IsFalse(retired.IsFaulted,retired.Exception?.ToString());
            AssertSavedEdit(editor,changed,count);
        }
    }
}
