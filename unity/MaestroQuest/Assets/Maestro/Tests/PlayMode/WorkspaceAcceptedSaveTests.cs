// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class WorkspaceHostTests
    {
        sealed class DelayedAcceptedSave:IDisposable
        {
            readonly TaskCompletionSource<string> pending=new(TaskCreationOptions.RunContinuationsAsynchronously);
            readonly Timer watchdog;
            internal bool Released=>pending.Task.IsCompleted;
            internal DelayedAcceptedSave(MonoBehaviour owner)
            {
                // Simulate a slow already-dispatched writer, not a Unity-thread delay.
                // The watchdog makes a regression fail instead of deadlocking the suite.
                owner.GetType().GetField("saveTask",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(owner,pending.Task);
                watchdog=new Timer(_=>pending.TrySetResult("Watchdog released a blocked test writer"),null,TimeSpan.FromSeconds(10),Timeout.InfiniteTimeSpan);
            }
            internal void Release(string error=null)=>pending.TrySetResult(error);
            public void Dispose(){Release();watchdog.Dispose();}
        }
        void DisableAcceptedSaveUpdates(){host.Current.Editor.enabled=false;host.Current.Rules.enabled=false;}
        static Task<string> AcceptedWorker(MonoBehaviour owner)=>(Task<string>)owner.GetType().GetField("saveTask",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);
        static IEnumerator WaitAcceptedWorker(MonoBehaviour owner)
        {
            float end=Time.realtimeSinceStartup+10;while(AcceptedWorker(owner)?.IsCompleted==false&&Time.realtimeSinceStartup<end)yield return null;
            Assert.That(AcceptedWorker(owner)?.IsCompleted,Is.True);
        }
        static void AssertSavedEdit(RoomEditor editor,RoomObjectData changed,int sequences)
        {
            var saved=new RoomStorage(editor.SaveDirectory).Load(out var error);Assert.That(saved,Is.Not.Null,error);Assert.That(saved.objects.Single(x=>x.id==changed.id).name,Is.EqualTo(changed.name));
            var rules=new RuleStorage(editor.SaveDirectory).Load(out error);Assert.That(error,Is.Null);Assert.That(rules.sequences.Length,Is.EqualTo(sequences));
        }
        [UnityTest] public IEnumerator ReviewCompletionDoesNotBlockAndSavesTheLatestAcceptedDocumentsAfterOlderWriters()
        {
            yield return ReadyForReview();DisableAcceptedSaveUpdates();var editor=host.Current.Editor;var rules=host.Current.Rules;
            var changed=AcceptedEdit("Accepted while an earlier save was running");rules.NewSequence();int count=rules.Snapshot().sequences.Length;
            string id=BeginReview();yield return FinishReview(id);string revision=host.Selection.Revision;
            using var roomSave=new DelayedAcceptedSave(editor);using var ruleSave=new DelayedAcceptedSave(rules);
            var capture=new JObject();
            ApproveReview(id);
            Assert.That(roomSave.Released,Is.False,"Review completion blocked the Unity owner thread");Assert.That(ruleSave.Released,Is.False);
            Assert.That(editor.WriteGate.Frozen,Is.True);Assert.That(host.Review.WorkerPending,Is.True);
            Assert.That(editor.TryFlush(out _),Is.False,"Lifecycle flush must not wait on this held writer");Assert.That(rules.TryFlush(out _),Is.False);
            for(int i=0;i<3;i++)yield return null;
            Assert.That(host.Selection.Revision,Is.EqualTo(revision));Assert.That(host.ReviewRequired,Is.True);
            capture["saving"]=host.Review.Read(id);
            roomSave.Release("An older room snapshot failed");ruleSave.Release("An older behaviour snapshot failed");
            yield return FinishReview(id);Assert.That((string)host.Review.Read(id)["phase"],Is.EqualTo("completed"));
            Assert.That(editor.WriteGate.Frozen,Is.False);Assert.That(host.ReviewRequired,Is.False);AssertSavedEdit(editor,changed,count);
            Assert.That(editor.TryFlush(out var error),Is.True,error);Assert.That(rules.TryFlush(out error),Is.True,error);
            Assert.That(editor.HasUnsavedChanges,Is.False);Assert.That(rules.HasUnsavedChanges,Is.False);
            capture["completed"]=host.Review.Read(id);capture["savedName"]=changed.name;capture["savedBehaviours"]=count;
            string folder=Environment.GetEnvironmentVariable("MAESTRO_REVIEW_SAVE_EVIDENCE");if(!string.IsNullOrEmpty(folder)){Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"accepted-save.json"),capture.ToString());}
        }
        [UnityTest] public IEnumerator CancellingReviewDrainsBothAcceptedWritersWithoutAbandoningTheirSnapshots()
        {
            yield return ReadyForReview();DisableAcceptedSaveUpdates();var editor=host.Current.Editor;var rules=host.Current.Rules;
            var changed=AcceptedEdit("Keep accepted edits despite cancelled approval");rules.NewSequence();int count=rules.Snapshot().sequences.Length;
            string id=BeginReview();yield return FinishReview(id);
            using var roomSave=new DelayedAcceptedSave(editor);using var ruleSave=new DelayedAcceptedSave(rules);
            ApproveReview(id);Assert.That(roomSave.Released,Is.False);host.Review.Cancel(id);roomSave.Release();yield return WaitAcceptedWorker(editor);
            for(int i=0;i<3;i++)yield return null;
            Assert.That(ruleSave.Released,Is.False);Assert.That(host.Review.Busy,Is.True);Assert.That(editor.WriteGate.Frozen,Is.True);
            ruleSave.Release();yield return FinishReview(id);
            Assert.That((string)host.Review.Read(id)["phase"],Is.EqualTo("cancelled"));Assert.That(host.ReviewRequired,Is.True);Assert.That(editor.WriteGate.Frozen,Is.False);Assert.That(editor.RuntimeGate.Held,Is.True);
            AssertSavedEdit(editor,changed,count);
        }
        [UnityTest] public IEnumerator FailureToStartCaptureStillDrainsAcceptedSavesBeforeReleasingOwners()
        {
            yield return ReadyForReview();DisableAcceptedSaveUpdates();var editor=host.Current.Editor;var rules=host.Current.Rules;
            var changed=AcceptedEdit("Capture failure cannot drop this accepted edit");string id=BeginReview();yield return FinishReview(id);
            using var roomSave=new DelayedAcceptedSave(editor);
            host.Review.Fault=point=>{if(point=="review.savesStarted")throw new IOException("Injected capture-start failure");};
            ApproveReview(id);Assert.That(roomSave.Released,Is.False);yield return null;
            Assert.That(editor.WriteGate.Frozen,Is.True);Assert.That(host.Review.Busy,Is.True);roomSave.Release();yield return FinishReview(id);
            Assert.That((string)host.Review.Read(id)["phase"],Is.EqualTo("failed"));Assert.That(editor.WriteGate.Frozen,Is.False);Assert.That(host.ReviewRequired,Is.True);
            AssertSavedEdit(editor,changed,rules.Snapshot().sequences.Length);
        }
        [UnityTest] public IEnumerator AFailedAcceptedSaveCannotReleaseTheOtherWriterOrApproveTheReview()
        {
            yield return ReadyForReview();DisableAcceptedSaveUpdates();var editor=host.Current.Editor;var rules=host.Current.Rules;
            var changed=AcceptedEdit("Failure keeps the live accepted document");string id=BeginReview();yield return FinishReview(id);
            using var ruleSave=new DelayedAcceptedSave(rules);string blocked=Path.Combine(editor.SaveDirectory,RoomStorage.FileName+".pending");Directory.CreateDirectory(blocked);
            try {
                ApproveReview(id);yield return WaitAcceptedWorker(editor);Assert.That(AcceptedWorker(editor).Result,Is.Not.Null);
                for(int i=0;i<3;i++)yield return null;Assert.That(editor.WriteGate.Frozen,Is.True);Assert.That(host.Review.WorkerPending,Is.True);Assert.That(ruleSave.Released,Is.False);
                ruleSave.Release();yield return FinishReview(id);Assert.That((string)host.Review.Read(id)["phase"],Is.EqualTo("failed"));Assert.That(host.ReviewRequired,Is.True);Assert.That(editor.WriteGate.Frozen,Is.False);
                editor.SendMessage("Update");Assert.That(editor.HasUnsavedChanges,Is.True);Assert.That(editor.Read(changed.id).name,Is.EqualTo(changed.name));Assert.That(editor.CanUndo,Is.True);
            }finally{Directory.Delete(blocked);}
            Assert.That(editor.TryFlush(out var error),Is.True,error);
        }
        [UnityTest] public IEnumerator ARefusedAcceptedSaveStillDrainsPreviouslyDispatchedWork()
        {
            yield return ReadyForReview();DisableAcceptedSaveUpdates();var editor=host.Current.Editor;
            string id=BeginReview();yield return FinishReview(id);using var earlier=new DelayedAcceptedSave(editor);
            host.Review.Fault=point=>{
                if(point!="review.beforeCapture")return;
                // A newer save discovered after preflight makes this store read-only.
                // The earlier writer remains owned until it reports completion.
                File.WriteAllText(Path.Combine(editor.SaveDirectory,"room.v14.json"),"{}");
                var storage=(RoomStorage)typeof(RoomEditor).GetField("storage",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(editor);
                storage.Load(out _);Assert.That(storage.ReadOnly,Is.True);
            };
            ApproveReview(id);Assert.That(earlier.Released,Is.False);
            for(int i=0;i<3;i++)yield return null;
            Assert.That(editor.WriteGate.Frozen,Is.True);Assert.That(host.Review.WorkerPending,Is.True);
            earlier.Release("Earlier writer detected unavailable storage");yield return FinishReview(id);
            Assert.That((string)host.Review.Read(id)["phase"],Is.EqualTo("failed"));Assert.That(host.ReviewRequired,Is.True);Assert.That(editor.WriteGate.Frozen,Is.False);
            Assert.That(File.ReadAllText(Path.Combine(editor.SaveDirectory,"room.v14.json")),Is.EqualTo("{}"));
        }
        [UnityTest] public IEnumerator ClosingTheHostWaitsForAcceptedWritesBeforeAnotherOwnerOpensTheWorkspace()
        {
            yield return ReadyForReview();DisableAcceptedSaveUpdates();var editor=host.Current.Editor;var rules=host.Current.Rules;
            var changed=AcceptedEdit("Persist before retiring these owners");rules.NewSequence();int count=rules.Snapshot().sequences.Length;
            string id=BeginReview();yield return FinishReview(id);string path=directory;var retiring=host;
            using var roomSave=new DelayedAcceptedSave(editor);using var ruleSave=new DelayedAcceptedSave(rules);
            ApproveReview(id);Assert.That(roomSave.Released,Is.False);UnityEngine.Object.Destroy(root);yield return null;
            Assert.That(retiring.Retirement.IsCompleted,Is.False);BuildShell(path);Open();Assert.That(host.Current,Is.Null,"New owners must wait for previous accepted writes");
            roomSave.Release();ruleSave.Release();yield return ReadyHost();yield return ReadyReviewOwners();
            Assert.That(host.ReviewRequired,Is.True);Assert.That(host.Current.Editor.RuntimeGate.Held,Is.True);AssertSavedEdit(host.Current.Editor,changed,count);
            Assert.That((string)host.Review.Read(id)["phase"],Is.EqualTo("cancelled").Or.EqualTo("interrupted"));Assert.That(host.Current.Rules.Runtime.Scheduler.RunningCount,Is.Zero);
        }
    }
}
