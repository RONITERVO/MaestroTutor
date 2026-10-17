// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Persistence;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class PreparedRoomPublicationTests
    {
        string directory,primary;RoomStorage storage;RoomDocument room;
        [SetUp] public void Before(){
            directory=Path.Combine(Path.GetTempPath(),"MaestroPublication-"+Guid.NewGuid().ToString("N"));
            storage=new RoomStorage(directory);
            room=new RoomJournal(new RoomDocument{version=1,objects=new[]{
                new RoomObjectData{id="book",kind=RoomObjectKind.Book,name="before"},
                new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}
            }}).Snapshot();
            Assert.IsTrue(storage.Save(room,out var error),error);Assert.IsTrue(storage.Save(room,out error),error);
            primary=Path.Combine(directory,RoomStorage.FileName);
        }
        [TearDown] public void After(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        RoomDocument Changed(string name="after"){var value=room.Copy();value.objects[0].name=name;return value;}
        string[] Stages()=>Directory.GetFiles(directory,RoomStorage.FileName+".prepare.*");
        sealed class Barrier:IDisposable
        {
            readonly string phase;readonly ManualResetEventSlim release=new();
            internal readonly TaskCompletionSource<int> Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal SynchronizationContext Context;
            internal Barrier(string phase="queued"){this.phase=phase;}
            internal void Check(string value){
                if(value!=phase)return;Context=SynchronizationContext.Current;
                Entered.TrySetResult(Thread.CurrentThread.ManagedThreadId);
                if(!release.Wait(10000))throw new TimeoutException("Publication test barrier timed out");
            }
            internal async Task Wait(){Assert.AreSame(Entered.Task,await Task.WhenAny(Entered.Task,Task.Delay(5000)),"Worker did not reach publication barrier");}
            internal void Release()=>release.Set();
            public void Dispose()=>release.Dispose();
        }
        [Test] public async Task WorkerOwnsPublicationAndCleanupWithoutUsingTheCallingThread(){
            int caller=Thread.CurrentThread.ManagedThreadId;
            using var prepared=await storage.PrepareSaveAsync(Changed());using var barrier=new Barrier();
            string before=File.ReadAllText(primary);var job=storage.StartPreparedPublication(prepared,barrier.Check);
            try{
                await barrier.Wait();Assert.AreNotEqual(caller,barrier.Entered.Task.Result);Assert.IsNull(barrier.Context);
                Assert.IsFalse(job.Completion.IsCompleted);Assert.IsFalse(job.Started);Assert.AreEqual(before,File.ReadAllText(primary));
                prepared.Dispose();Assert.AreEqual(1,Stages().Length,"Caller disposal cannot steal transferred bytes");
                Assert.IsFalse(storage.PublishPrepared(prepared,out _));
                var duplicate=await storage.StartPreparedPublication(prepared).Completion;
                Assert.IsFalse(duplicate.Saved);Assert.IsFalse(duplicate.MayHavePublished);
                Assert.ThrowsAsync<InvalidOperationException>(async()=>{using var next=await storage.PrepareSaveAsync(room);});
            }finally{barrier.Release();await job.Completion;}
            var result=await job.Completion;Assert.IsTrue(result.Saved,result.Error);Assert.IsTrue(result.MayHavePublished);Assert.IsTrue(job.Started);
            Assert.AreEqual("after",storage.Load(out _).objects[0].name);Assert.AreEqual(before,File.ReadAllText(primary+".backup"));Assert.IsEmpty(Stages());
            using var again=await storage.PrepareSaveAsync(Changed("next"));
            Assert.IsTrue((await storage.StartPreparedPublication(again).Completion).Saved);
        }
        [Test] public async Task CancelBeforeBoundaryPreservesAcceptedFilesAndConsumesTheCandidate(){
            string before=File.ReadAllText(primary),backup=File.ReadAllText(primary+".backup");
            using var prepared=await storage.PrepareSaveAsync(Changed());using var barrier=new Barrier();
            var job=storage.StartPreparedPublication(prepared,barrier.Check);
            try{await barrier.Wait();Assert.IsTrue(job.CancelBeforeStart());Assert.IsFalse(job.CancelBeforeStart());}
            finally{barrier.Release();await job.Completion;}
            var result=await job.Completion;Assert.IsFalse(result.Saved);Assert.IsFalse(result.MayHavePublished);Assert.IsFalse(job.Started);
            Assert.AreEqual(before,File.ReadAllText(primary));Assert.AreEqual(backup,File.ReadAllText(primary+".backup"));Assert.IsEmpty(Stages());
            Assert.IsFalse((await storage.StartPreparedPublication(prepared).Completion).MayHavePublished);
            using var next=await storage.PrepareSaveAsync(room);
        }
        [Test] public async Task CancellationAfterBoundaryCannotRetractPublication(){
            using var prepared=await storage.PrepareSaveAsync(Changed());using var barrier=new Barrier("publishing");
            var job=storage.StartPreparedPublication(prepared,barrier.Check);
            try{await barrier.Wait();Assert.IsTrue(job.Started);Assert.IsFalse(job.CancelBeforeStart());Assert.IsFalse(job.Completion.IsCompleted);}
            finally{barrier.Release();await job.Completion;}
            Assert.IsTrue((await job.Completion).Saved);Assert.AreEqual("after",storage.Load(out _).objects[0].name);
            Assert.IsFalse(job.CancelBeforeStart());Assert.IsFalse((await storage.StartPreparedPublication(prepared).Completion).Saved);
        }
        [Test] public async Task ForeignStoreCannotConsumeAnotherStoresPreparedPublication(){
            using var prepared=await storage.PrepareSaveAsync(Changed());
            var foreign=new RoomStorage(directory);var refused=foreign.StartPreparedPublication(prepared);
            Assert.IsFalse(refused.CancelBeforeStart());Assert.IsFalse((await refused.Completion).MayHavePublished);Assert.AreEqual(1,Stages().Length);
            Assert.IsTrue((await storage.StartPreparedPublication(prepared).Completion).Saved);
        }
        [Test] public async Task InterveningAcceptedSaveIsNotOverwrittenByQueuedPublication(){
            using var prepared=await storage.PrepareSaveAsync(Changed());using var barrier=new Barrier();
            var job=storage.StartPreparedPublication(prepared,barrier.Check);string before=null,backup=null;
            try{
                await barrier.Wait();Assert.IsTrue(storage.Save(Changed("newer accepted"),out var error),error);
                before=File.ReadAllText(primary);backup=File.ReadAllText(primary+".backup");
            }finally{barrier.Release();await job.Completion;}
            var result=await job.Completion;Assert.IsFalse(result.Saved);Assert.IsTrue(result.MayHavePublished);
            Assert.AreEqual(before,File.ReadAllText(primary));Assert.AreEqual(backup,File.ReadAllText(primary+".backup"));Assert.IsEmpty(Stages());
            Assert.IsFalse((await storage.StartPreparedPublication(prepared).Completion).Saved);
        }
        [Test] public async Task BusyCoordinatorFailsOnceAndReleasesPublicationAdmission(){
            using var prepared=await storage.PrepareSaveAsync(Changed());
            var entered=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);using var release=new ManualResetEventSlim();
            var writer=Task.Run(()=>{using var held=RoomSnapshotTransaction.Enter(directory);entered.SetResult(true);if(!release.Wait(10000))throw new TimeoutException();});
            try{
                Assert.AreSame(entered.Task,await Task.WhenAny(entered.Task,Task.Delay(5000)));
                var job=storage.StartPreparedPublication(prepared);
                Assert.AreSame(job.Completion,await Task.WhenAny(job.Completion,Task.Delay(5000)),"Publication must not wait indefinitely for a coordinator");
                var result=await job.Completion;Assert.IsFalse(result.Saved);Assert.IsTrue(result.MayHavePublished);Assert.IsFalse(writer.IsCompleted);
            }finally{release.Set();await writer;}
            Assert.AreEqual("before",storage.Load(out _).objects[0].name);Assert.IsEmpty(Stages());
            using var next=await storage.PrepareSaveAsync(Changed());
            Assert.IsTrue((await storage.StartPreparedPublication(next).Completion).Saved);
        }
        [TestCase("queued",false,false)] [TestCase("publishing",true,false)] [TestCase("published",true,true)]
        public async Task FailedObservationDoesNotLieAboutPossibleDurableEffects(string phase,bool attempted,bool savedOnDisk){
            using var prepared=await storage.PrepareSaveAsync(Changed());
            var job=storage.StartPreparedPublication(prepared,value=>{if(value==phase)throw new IOException("Injected observation failure");});
            var result=await job.Completion;Assert.IsFalse(result.Saved);Assert.AreEqual(attempted,result.MayHavePublished);
            Assert.AreEqual(savedOnDisk?"after":"before",storage.Load(out _).objects[0].name);
            Assert.IsFalse(job.CancelBeforeStart());Assert.IsFalse((await storage.StartPreparedPublication(prepared).Completion).Saved);Assert.IsEmpty(Stages());
            using var next=await storage.PrepareSaveAsync(Changed("next"));
        }
        [Test] public async Task TwoStoresCannotBothPublishFromTheSameCapturedBaseline(){
            var other=new RoomStorage(directory);
            using var first=await storage.PrepareSaveAsync(Changed("first"));using var second=await other.PrepareSaveAsync(Changed("second"));
            var one=storage.StartPreparedPublication(first);var two=other.StartPreparedPublication(second);
            var results=await Task.WhenAll(one.Completion,two.Completion);
            Assert.AreEqual(1,results.Count(x=>x.Saved));Assert.That(storage.Load(out _).objects[0].name,Is.EqualTo("first").Or.EqualTo("second"));Assert.IsEmpty(Stages());
        }
#if UNITY_EDITOR_WIN
        [Test] public async Task WindowsReaderFailureIsUnconfirmedAndCannotReplayAfterItCloses(){
            string before=File.ReadAllText(primary);using var prepared=await storage.PrepareSaveAsync(Changed());
            using(var reader=new FileStream(primary,FileMode.Open,FileAccess.Read,FileShare.Read)){
                var result=await storage.StartPreparedPublication(prepared).Completion;
                Assert.IsFalse(result.Saved);Assert.IsTrue(result.MayHavePublished);Assert.AreEqual(before,File.ReadAllText(primary));
            }
            Assert.IsFalse((await storage.StartPreparedPublication(prepared).Completion).Saved);Assert.AreEqual(before,File.ReadAllText(primary));Assert.IsEmpty(Stages());
        }
#endif
    }
}
