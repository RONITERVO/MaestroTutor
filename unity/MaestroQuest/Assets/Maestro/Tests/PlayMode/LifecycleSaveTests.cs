// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Rules;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class SaveRecoveryTests
    {
        JObject Checkpoint(string name)
        {
            var fact=BehaviourCatalog.Fact("room.persistence");
            Assert.IsTrue(fact.TryRead(new BehaviourCatalog.FactContext(editor:editor),1,new JObject(),out var value));
            var data=JObject.FromObject(value.Value);
            string output=Environment.GetEnvironmentVariable("MAESTRO_CHECKPOINT_EVIDENCE");
            if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,name+".json"),data.ToString());}
            return data;
        }
        sealed class SlowWriter:IDisposable
        {
            readonly TaskCompletionSource<string> pending=new(TaskCreationOptions.RunContinuationsAsynchronously);
            readonly Timer watchdog;
            internal bool Released=>pending.Task.IsCompleted;
            internal SlowWriter(MonoBehaviour owner){
                Pending(owner,pending.Task);
                watchdog=new Timer(_=>pending.TrySetResult("Watchdog released a blocked writer"),null,10000,Timeout.Infinite);
            }
            internal void Release(string error=null)=>pending.TrySetResult(error);
            public void Dispose(){Release();watchdog.Dispose();}
        }
        static void Lifecycle(MonoBehaviour owner,string method,object value=null)=>
            owner.GetType().GetMethod(method,Private).Invoke(owner,value==null?null:new[]{value});
        static void ObserveCheckpoint(MonoBehaviour owner)=>
            owner.GetType().GetMethod("CompleteSave",Private).Invoke(owner,new object[]{false});
        static IEnumerator WaitLifecycle(Task pending){
            float end=Time.realtimeSinceStartup+10;
            while(!pending.IsCompleted&&Time.realtimeSinceStartup<end)yield return null;
            Assert.That(pending.IsCompleted,Is.True,"Lifecycle writer did not drain");Assert.That(pending.IsFaulted,Is.False,pending.Exception?.ToString());
        }
        IEnumerator FinishTemporaryWorker(){
            float end=Time.realtimeSinceStartup+10;
            while(editor.TemporarySavePending&&Time.realtimeSinceStartup<end){editor.PollTemporarySave();yield return null;}
            Assert.IsFalse(editor.TemporarySavePending);
        }
        [UnityTest] public IEnumerator PauseAndFocusCoalesceLatestAcceptedStateWithoutWaitingForDisk()
        {
            Rename("Captured on pause");rules.NewSequence();
            Assert.AreEqual("unsaved",(string)Checkpoint("unsaved")["behaviours"]["phase"]);
            using var roomSave=new SlowWriter(editor);using var ruleSave=new SlowWriter(rules);
            var timer=Stopwatch.StartNew();
            Lifecycle(editor,"OnApplicationPause",true);Lifecycle(rules,"OnApplicationPause",true);
            var first=editor.LifecycleCompletion;var behaviours=rules.LifecycleCompletion;
            var saving=Checkpoint("saving");Assert.AreEqual("saving",(string)saving["room"]["phase"]);
            Assert.AreEqual("saving",(string)saving["behaviours"]["phase"]);
            Assert.IsFalse(roomSave.Released,"Pause blocked on room IO");Assert.IsFalse(ruleSave.Released,"Pause blocked on behaviour IO");
            Rename("Newest captured on focus");rules.NewSequence();
            Lifecycle(editor,"OnApplicationFocus",false);Lifecycle(rules,"OnApplicationFocus",false);
            Assert.AreSame(first,editor.LifecycleCompletion);Assert.AreSame(behaviours,rules.LifecycleCompletion);
            Assert.IsTrue(editor.HasUnsavedChanges);Assert.IsTrue(rules.HasUnsavedChanges);
            TestContext.WriteLine("Four lifecycle callbacks dispatched in "+timer.Elapsed.TotalMilliseconds.ToString("F2")+" ms; both injected workers still blocked.");
            roomSave.Release("Earlier room save failed");ruleSave.Release("Earlier rule save failed");
            yield return WaitLifecycle(Task.WhenAll(first,behaviours));ObserveCheckpoint(editor);ObserveCheckpoint(rules);
            Assert.IsFalse(editor.HasUnsavedChanges);Assert.IsFalse(rules.HasUnsavedChanges);AssertSaved("Newest captured on focus",3);
            var saved=Checkpoint("saved");Assert.AreEqual("idle",(string)saved["room"]["phase"]);
            Assert.AreEqual("idle",(string)saved["behaviours"]["phase"]);
        }
        [UnityTest] public IEnumerator OlderLifecycleCompletionCannotMarkLaterUnsavedWorkAsSaved()
        {
            Rename("Captured");using var older=new SlowWriter(editor);
            var captured=editor.QueueLifecycleSave();Rename("Edited after capture");
            older.Release();yield return WaitLifecycle(captured);ObserveCheckpoint(editor);
            Assert.IsTrue(editor.HasUnsavedChanges);AssertSaved("Captured",1);
            Assert.AreEqual("unsaved",(string)Checkpoint("newer")["room"]["phase"]);
            yield return WaitLifecycle(editor.QueueLifecycleSave());ObserveCheckpoint(editor);
            Assert.IsFalse(editor.HasUnsavedChanges);AssertSaved("Edited after capture",1);
        }
        [UnityTest] public IEnumerator PauseOnlyDrainsTheTemporaryBaselineAndNeverPersistsItsLaterFork()
        {
            Rename("Starting accepted room");using var older=new SlowWriter(editor);
            Assert.IsTrue(editor.BeginTemporaryRoom(out var error),error);Rename("Only in temporary fork");
            var captured=editor.QueueLifecycleSave();Assert.IsFalse(older.Released);Assert.IsFalse(captured.IsCompleted);
            older.Release();yield return WaitLifecycle(captured);yield return FinishTemporaryWorker();
            AssertSaved("Starting accepted room",1);Assert.IsTrue(editor.TemporaryRoom);
            Assert.AreEqual("temporary",(string)Checkpoint("temporary")["room"]["phase"]);
            Lifecycle(editor,"OnApplicationFocus",false);AssertSaved("Starting accepted room",1);
            Assert.IsTrue(editor.DiscardTemporaryRoom(out error),error);
        }
        [UnityTest] public IEnumerator DestroyDispatchesDetachedAcceptedSnapshotsAndDoesNotWaitForTheirWriters()
        {
            Rename("Accepted before closing");rules.NewSequence();
            using var earlierRoom=new SlowWriter(editor);using var earlierRules=new SlowWriter(rules);
            UnityEngine.Object.Destroy(root);yield return null;
            Assert.IsFalse(earlierRoom.Released);Assert.IsFalse(earlierRules.Released);
            var drain=Task.WhenAll(editor.LifecycleCompletion,rules.LifecycleCompletion,rules.Modules.Retire());
            Assert.IsFalse(drain.IsCompleted);earlierRoom.Release();earlierRules.Release();
            yield return WaitLifecycle(drain);AssertSaved("Accepted before closing",2);
        }
        [UnityTest] public IEnumerator LifecycleCheckpointRespectsRecoverySaveHolds()
        {
            Rename("Held accepted room");rules.NewSequence();
            using var roomHold=editor.HoldSaveDispatch(out _,out var error);Assert.IsNotNull(roomHold,error);
            using var ruleHold=rules.HoldSaveDispatch(out _,out error);Assert.IsNotNull(ruleHold,error);
            var before=File.ReadAllBytes(RoomPath);var rulesBefore=File.ReadAllBytes(RulesPath);
            var held=Checkpoint("held");Assert.IsTrue((bool)held["room"]["dispatchHeld"]);
            Assert.IsTrue((bool)held["behaviours"]["dispatchHeld"]);
            Assert.IsTrue(editor.QueueLifecycleSave().IsCompleted);Assert.IsTrue(rules.QueueLifecycleSave().IsCompleted);
            Assert.IsTrue(editor.HasUnsavedChanges);Assert.IsTrue(rules.HasUnsavedChanges);
            CollectionAssert.AreEqual(before,File.ReadAllBytes(RoomPath));CollectionAssert.AreEqual(rulesBefore,File.ReadAllBytes(RulesPath));
            roomHold.Dispose();ruleHold.Dispose();yield return WaitLifecycle(Task.WhenAll(editor.QueueLifecycleSave(),rules.QueueLifecycleSave()));
            AssertSaved("Held accepted room",2);
        }
        [UnityTest] public IEnumerator SharedCheckpointFactPreservesFailureAndDoesNotRetryOnRead()
        {
            Rename("Unconfirmed accepted room");rules.NewSequence();Block();
            yield return WaitLifecycle(Task.WhenAll(editor.QueueLifecycleSave(),rules.QueueLifecycleSave()));
            ObserveCheckpoint(editor);ObserveCheckpoint(rules);
            var failed=Checkpoint("failed");
            foreach(string store in new[]{"room","behaviours"}){
                Assert.AreEqual("unconfirmed",(string)failed[store]["phase"]);
                Assert.IsFalse((bool)failed[store]["pending"]);Assert.IsTrue((bool)failed[store]["dirty"]);
                Assert.IsNotEmpty((string)failed[store]["error"]);
            }
            Assert.IsNull(Pending(editor));Assert.IsNull(Pending(rules));
            Assert.IsFalse(BehaviourCatalog.Fact("room.persistence").TryRead(default,1,new JObject(),out _));
            Unblock();yield return WaitLifecycle(Task.WhenAll(editor.QueueLifecycleSave(),rules.QueueLifecycleSave()));
            ObserveCheckpoint(editor);ObserveCheckpoint(rules);
            Assert.AreEqual("idle",(string)Checkpoint("recovered")["room"]["phase"]);
            AssertSaved("Unconfirmed accepted room",2);
        }
    }
}
