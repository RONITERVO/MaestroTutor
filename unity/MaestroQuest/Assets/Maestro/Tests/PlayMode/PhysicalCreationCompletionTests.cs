// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    internal static class PhysicalCreationTestWait
    {
        internal static IEnumerator Until(Func<bool> ready,string label)
        {
            float deadline=Time.realtimeSinceStartup+20;
            while(!ready()&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(ready(),Is.True,label);
        }
        internal static IEnumerator Finish(RoomEditor editor)=>Until(()=>!editor.ManualCreationPending,"Physical creation edit did not finish");
    }
    public sealed partial class RoomEditorTests
    {
        sealed class HeldManualPublication:IDisposable
        {
            readonly ManualResetEventSlim release=new(false);
            readonly TaskCompletionSource<bool> entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal bool Entered=>entered.Task.IsCompleted;
            internal int ThreadId {get;private set;}
            internal void Check(string phase)
            {
                if(phase!="publishing")return;
                ThreadId=Thread.CurrentThread.ManagedThreadId;entered.TrySetResult(true);
                if(!release.Wait(TimeSpan.FromSeconds(20)))throw new TimeoutException("Physical tool publication was not released");
            }
            internal void Release()=>release.Set();
            public void Dispose()=>Release();
        }
        [UnityTest] public IEnumerator PhysicalCreationWaitsForMemoryStartupWithoutBlockingOrReplaying()
        {
            Assert.That(editor.TryFlush(out var error),Is.True,error);
            int count=editor.Snapshot().objects.Length,revision=editor.Revision;
            using var release=new ManualResetEventSlim();
            var entered=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var writer=Task.Run(()=>{
                using var held=Maestro.Quest.Persistence.RoomSnapshotTransaction.Enter(directory);
                entered.TrySetResult(true);
                if(!release.Wait(TimeSpan.FromSeconds(20)))throw new TimeoutException("Startup coordinator was not released");
            });
            try {
                yield return PhysicalCreationTestWait.Until(()=>entered.Task.IsCompleted,"Coordinator was not held");
                var workshop=root.AddComponent<Maestro.Quest.Rules.RuleWorkshop>();workshop.Initialize(editor,directory);
                Assert.That(workshop.Memory.Initialization.IsCompleted,Is.False);
                editor.Create(RoomObjectKind.Ball);
                for(int i=0;i<5;i++)yield return null;
                Assert.That(editor.ManualCreationPending,Is.True,editor.Status);
                Assert.That(editor.PendingEditSave,Is.False,"Waiting startup must not allocate a prepared edit");
                Assert.That(editor.Revision,Is.EqualTo(revision));
                Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));
                release.Set();
                yield return PhysicalCreationTestWait.Finish(editor);
                Assert.That(workshop.Memory.Ready,Is.True);Assert.That(workshop.Memory.Error,Is.Null);
                Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+1),editor.Status);
                Assert.That(editor.Find(editor.SelectedId),Is.Not.Null);
                Assert.That(new RoomStorage(directory).Load(out _).objects.Length,Is.EqualTo(count+1));
                editor.Undo();Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));
            }finally{release.Set();}
            yield return PhysicalCreationTestWait.Until(()=>writer.IsCompleted,"Coordinator did not drain");
            Assert.That(writer.Exception,Is.Null);
        }
        [UnityTest] public IEnumerator PhysicalCreationSavesOffThreadAndDoesNotStealLaterSelection()
        {
            int count=editor.Snapshot().objects.Length,thread=Thread.CurrentThread.ManagedThreadId;
            editor.Select(editor.Find("maestro"));
            using var hold=new HeldManualPublication();editor.ManualCreationCheckpoint=hold.Check;
            try {
                editor.Create(RoomObjectKind.Block);Assert.That(editor.ManualCreationPending,Is.True);
                Assert.That(editor.SelectedId,Is.EqualTo("maestro"));
                yield return PhysicalCreationTestWait.Until(()=>hold.Entered,"Creation did not reach publication");
                Assert.That(hold.ThreadId,Is.Not.EqualTo(thread));
                Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+1));
                Assert.That(editor.CanUndo,Is.False);Assert.That(editor.WriteGate.TryFreeze(out _),Is.Null);
                editor.Create(RoomObjectKind.Ball);
                Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+1),"A second pending tap created another object");
                for(int i=0;i<8;i++)yield return null;
                editor.Select(editor.Find("book"));hold.Release();
                yield return PhysicalCreationTestWait.Finish(editor);
                Assert.That(editor.SelectedId,Is.EqualTo("book"),"Completion replaced the learner's newer selection");
                Assert.That(new RoomStorage(directory).Load(out _).objects.Length,Is.EqualTo(count+1));
                editor.Undo();Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));
                editor.Redo();Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+1));
            } finally {hold.Release();editor.ManualCreationCheckpoint=null;}
        }
        [UnityTest] public IEnumerator PhysicalCreationCancellationBeforeAdmissionCreatesNothing()
        {
            int count=editor.Snapshot().objects.Length,revision=editor.Revision;
            Assert.That(editor.BeginCreatePrimitive(RoomObjectKind.Ball,"blocker",Vector3.up,1,Color.white,out _,out var blocker,out var error),Is.True,error);
            using(blocker) {
                editor.Create(RoomObjectKind.Block);Assert.That(editor.ManualCreationPending,Is.True);
                editor.CancelManualCreation(true);
                Assert.That(editor.ManualCreationPending,Is.False);
                Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));
            }
            yield return PhysicalCreationTestWait.Until(()=>!editor.PendingEditSave,"Blocker did not drain");
            Assert.That(editor.WriteGate.CanFreeze(out error),Is.True,error);
        }
        [UnityTest] public IEnumerator PhysicalPaintCopiesIntentAndKeepsHumanPriorityThroughSaving()
        {
            var target=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block);editor.Select(editor.Find(target.id));
            bool interrupted=false;
            Assert.That(editor.Ownership.TryAcquire("program-test","Program",RoomActorRole.Program,new[]{new BehaviourCatalog.Claim(target.id,"wholeTarget")},
                _=>interrupted=true,out var program,out var error),Is.True,error);
            using(program)using(var hold=new HeldManualPublication()){
                editor.ManualCreationCheckpoint=hold.Check;
                try {
                    editor.ChoosePaint(Color.red);Assert.That(interrupted,Is.True);Assert.That(program.Held,Is.False);
                    yield return PhysicalCreationTestWait.Until(()=>hold.Entered,"Paint did not reach publication");
                    editor.ChoosePaint(Color.blue); // future tool preference only; pending intent stays red
                    Assert.That(editor.Paint,Is.EqualTo(Color.blue));
                    Assert.That(editor.Ownership.CanAcquire("other-program",RoomActorRole.Program,new[]{new BehaviourCatalog.Claim(target.id,"wholeTarget")},out _),Is.False);
                    hold.Release();yield return PhysicalCreationTestWait.Finish(editor);
                    Assert.That(editor.Read(target.id).color,Is.EqualTo(Color.red));
                    Assert.That(new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==target.id).color,Is.EqualTo(Color.red));
                    Assert.That(editor.Ownership.CanAcquire("other-program",RoomActorRole.Program,new[]{new BehaviourCatalog.Claim(target.id,"wholeTarget")},out error),Is.True,error);
                    editor.Undo();Assert.That(editor.Read(target.id).color,Is.EqualTo(target.color));
                }finally{hold.Release();editor.ManualCreationCheckpoint=null;}
            }
        }
        [UnityTest] public IEnumerator PhysicalAcceptedCreationKeepsSavingAfterDisableWithoutRepeating()
        {
            int count=editor.Snapshot().objects.Length;
            using var hold=new HeldManualPublication();editor.ManualCreationCheckpoint=hold.Check;
            try {
                editor.Create(RoomObjectKind.Ball);
                yield return PhysicalCreationTestWait.Until(()=>hold.Entered,"Creation did not reach publication");
                editor.enabled=false;Assert.That(editor.ManualCreationPending,Is.False);
                Assert.That(editor.WriteGate.TryFreeze(out _),Is.Null,"Accepted save must retain workspace lifetime");
                hold.Release();yield return PhysicalCreationTestWait.Until(()=>!editor.PendingEditSave,"Accepted save did not drain");
                Assert.That(new RoomStorage(directory).Load(out _).objects.Length,Is.EqualTo(count+1));
                editor.enabled=true;yield return null;yield return null;
                Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+1));
            }finally{hold.Release();editor.ManualCreationCheckpoint=null;editor.enabled=true;}
        }
        [UnityTest] public IEnumerator PhysicalAcceptedCreationFailureIdentifiesObjectAndNeverReplays()
        {
            Assert.That(editor.TryFlush(out var error),Is.True,error);
            var before=editor.Snapshot().objects.Select(x=>x.id).ToArray();
            var path=Path.Combine(directory,RoomStorage.FileName);var saved=File.ReadAllBytes(path);
            editor.ManualCreationCheckpoint=phase=>{if(phase=="publishing")throw new IOException("Injected physical publication failure");};
            try {
                editor.Create(RoomObjectKind.Ball);yield return PhysicalCreationTestWait.Finish(editor);
                yield return PhysicalCreationTestWait.Until(()=>!editor.PendingEditSave,"Failed accepted edit did not drain");
                var created=editor.Snapshot().objects.Single(x=>!before.Contains(x.id));
                Assert.That(editor.Find(created.id),Is.Not.Null);
                Assert.That(editor.Status,Does.Contain(created.id));
                Assert.That(editor.Status,Does.Contain("accepted"));
                Assert.That(editor.CanSaveRoom,Is.False,"Unconfirmed accepted save must preserve files");
                Assert.That(File.ReadAllBytes(path),Is.EqualTo(saved));
                Assert.That(editor.Ownership.Observe().owners.Any(x=>x.label=="Creation tools"),Is.False);
                for(int i=0;i<8;i++)yield return null;
                Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(before.Length+1));
                Assert.That(File.ReadAllBytes(path),Is.EqualTo(saved));
            }finally{editor.ManualCreationCheckpoint=null;}
        }
        [UnityTest] public IEnumerator PhysicalCreationFailureReleasesItsOwnerAndAllowsExplicitRetry()
        {
            Assert.That(editor.TryFlush(out var error),Is.True,error);int count=editor.Snapshot().objects.Length;
            string obstacle=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(obstacle);
            try {
                editor.Create(RoomObjectKind.Block);yield return PhysicalCreationTestWait.Finish(editor);
                yield return PhysicalCreationTestWait.Until(()=>!editor.PendingEditSave,"Failed preparation did not drain");
                Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));
                Assert.That(editor.Ownership.Observe().owners.Any(x=>x.label=="Creation tools"),Is.False);
            }finally{Directory.Delete(obstacle);}
            editor.Create(RoomObjectKind.Block);yield return PhysicalCreationTestWait.Finish(editor);
            Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+1));
        }
        [UnityTest] public IEnumerator PhysicalQueuedPaintRejectsChangedTargetAndDoesNotWriteStaleColour()
        {
            var target=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block);editor.Select(editor.Find(target.id));
            Assert.That(editor.BeginCreatePrimitive(RoomObjectKind.Ball,"blocker",Vector3.up,1,Color.white,out _,out var blocker,out var error),Is.True,error);
            using(blocker) {
                editor.ChoosePaint(Color.red);Assert.That(editor.ManualCreationPending,Is.True);
                Assert.That(editor.PaintObject(target.id,Color.blue,out error),Is.True,error);
            }
            yield return PhysicalCreationTestWait.Finish(editor);
            Assert.That(editor.Read(target.id).color,Is.EqualTo(Color.blue));
            Assert.That(editor.Status,Does.Contain("changed"));
        }
        [UnityTest] public IEnumerator PhysicalTemporaryToolsChangeLocalHistoryWithoutPublishingOrdinaryRoom()
        {
            Assert.That(editor.TryFlush(out var error),Is.True,error);
            var path=Path.Combine(directory,RoomStorage.FileName);
            Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);
            yield return PhysicalCreationTestWait.Until(()=>!editor.TemporarySavePending,"Temporary baseline did not save");
            var before=File.ReadAllBytes(path);
            int count=editor.Snapshot().objects.Length;
            var tray=new GameObject("Physical temporary tools");tray.transform.SetParent(root.transform,false);
            tray.AddComponent<RoomToolTray>().Build(editor,room);
            PhysicalRoomAction Button(RoomTool tool)=>tray.GetComponentsInChildren<PhysicalRoomAction>().First(x=>x.Tool==tool);
            Button(RoomTool.Cylinder).Activate();yield return PhysicalCreationTestWait.Finish(editor);
            string created=editor.SelectedId;Assert.That(editor.Read(created).kind,Is.EqualTo(RoomObjectKind.Cylinder));
            var paint=Button(RoomTool.Paint);paint.Activate();yield return PhysicalCreationTestWait.Finish(editor);
            Button(RoomTool.Duplicate).Activate();yield return PhysicalCreationTestWait.Finish(editor);
            Assert.That(editor.SelectedId,Is.Not.EqualTo(created));Assert.That(editor.Read(editor.SelectedId).color,Is.EqualTo(paint.Paint));
            Button(RoomTool.Erase).Activate();yield return PhysicalCreationTestWait.Finish(editor);
            Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+1));
            Assert.That(File.ReadAllBytes(path),Is.EqualTo(before));
            editor.Undo();Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+2));
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);
            Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));
        }
    }
}
