// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Persistence;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Maestro.Quest.Tests
{
    public sealed class PreparedRoomSaveTests
    {
        [Serializable] public sealed class Probe {public int version=1,value=1;public string name="one";}
        string directory,primary;
        VersionedRoomFile<Probe> file;
        Action<Probe> observe;
        VersionedRoomFile<Probe> Store(string name="probe",bool pure=true)=>new(directory,name,8192,
            x=>{observe?.Invoke(x);return x.value>=0&&x.value<10;},
            x=>new Probe{version=x.version,value=x.value,name=x.name},null,x=>x.version=1,version:1,
            validWire:x=>x.Count==3&&x["name"]?.Type==JTokenType.String&&(string)x["name"]!="unreadable",
            cachePrimaryInspection:pure);
        [SetUp] public void SetUp()
        {
            directory=Path.Combine(Path.GetTempPath(),"MaestroPreparedRoom-"+Guid.NewGuid().ToString("N"));
            primary=Path.Combine(directory,"probe.v1.json");file=Store();
            Assert.That(file.Save(new Probe(),out var error),Is.True,error);
            Assert.That(file.Save(new Probe(),out error),Is.True,error);
        }
        [TearDown] public void TearDown(){observe=null;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        VersionedRoomFile<Probe>.PreparedSave Prepare(Probe value=null,bool staged=false)
        {
            Assert.That(file.PrepareSave(value??new Probe{value=2},file.CaptureForPreparation(),out var prepared,out var error,stageFile:staged),Is.True,error);
            return prepared;
        }
        [Test] public void PreparationIsPrivateAndPublicationIsOneShot()
        {
            string original=File.ReadAllText(primary),backup=File.ReadAllText(primary+".backup");
            var input=new Probe{value=2};using var prepared=Prepare(input);input.value=3;
            Assert.That(File.ReadAllText(primary),Is.EqualTo(original));
            Assert.That(File.ReadAllText(primary+".backup"),Is.EqualTo(backup));
            Assert.That(File.Exists(primary+".pending"),Is.False);
            Assert.That(file.PublishPrepared(prepared,out var error),Is.True,error);
            Assert.That(file.Load(out error).value,Is.EqualTo(2),error);
            Assert.That(File.ReadAllText(primary+".backup"),Is.EqualTo(original));
            Assert.That(file.PublishPrepared(prepared,out _),Is.False);
        }
        [Test] public void SameMetadataReplacementRejectsStalePreparation()
        {
            using var prepared=Prepare();string backup=File.ReadAllText(primary+".backup");
            var stamp=File.GetLastWriteTimeUtc(primary);
            string replacement=File.ReadAllText(primary).Replace("\"value\":1","\"value\":3");
            File.WriteAllText(primary,replacement);File.SetLastWriteTimeUtc(primary,stamp);
            Assert.That(file.PublishPrepared(prepared,out _),Is.False);
            Assert.That(File.ReadAllText(primary),Is.EqualTo(replacement));
            Assert.That(File.ReadAllText(primary+".backup"),Is.EqualTo(backup));
            Assert.That(File.Exists(primary+".pending"),Is.False);
        }
        [Test] public void ConcurrentPreparationsCannotOverwriteEachOther()
        {
            using var first=Prepare(new Probe{value=2});using var second=Prepare(new Probe{value=3});
            Assert.That(file.PublishPrepared(first,out var error),Is.True,error);
            Assert.That(file.PublishPrepared(second,out _),Is.False);
            Assert.That(file.Load(out _).value,Is.EqualTo(2));
        }
        [Test] public void AnotherStoreCannotClaimThePreparation()
        {
            using var prepared=Prepare();
            Assert.That(Store().PublishPrepared(prepared,out _),Is.False);
            Assert.That(file.PublishPrepared(prepared,out var error),Is.True,error);
        }
        [Test] public void DisposedPreparationCannotWrite()
        {
            var prepared=Prepare();string before=File.ReadAllText(primary);prepared.Dispose();
            Assert.That(file.PublishPrepared(prepared,out _),Is.False);
            Assert.That(File.ReadAllText(primary),Is.EqualTo(before));
        }
        [Test] public void NewerFormatAfterPreparationPreservesEveryOriginal()
        {
            using var prepared=Prepare();string before=File.ReadAllText(primary),backup=File.ReadAllText(primary+".backup");
            string newer=Path.Combine(directory,"probe.v2.json");File.WriteAllText(newer,"future");
            Assert.That(file.PublishPrepared(prepared,out _),Is.False);
            Assert.That(File.ReadAllText(primary),Is.EqualTo(before));
            Assert.That(File.ReadAllText(primary+".backup"),Is.EqualTo(backup));
            Assert.That(File.ReadAllText(newer),Is.EqualTo("future"));
        }
        [Test] public void DamagedBaselineRemainsUnreadableEvidence()
        {
            string backup=File.ReadAllText(primary+".backup");File.WriteAllText(primary,"{damaged");
            using var prepared=Prepare();
            Assert.That(file.PublishPrepared(prepared,out var error),Is.True,error);
            Assert.That(File.ReadAllText(primary+".unreadable"),Is.EqualTo("{damaged"));
            Assert.That(File.ReadAllText(primary+".backup"),Is.EqualTo(backup));
            Assert.That(file.Load(out _).value,Is.EqualTo(2));
        }
        [Test] public void UnownedPendingFileIsNeverReplacedOrDeleted()
        {
            using var prepared=Prepare();string before=File.ReadAllText(primary);
            File.WriteAllText(primary+".pending","another writer");
            Assert.That(file.PublishPrepared(prepared,out _),Is.False);
            Assert.That(File.ReadAllText(primary+".pending"),Is.EqualTo("another writer"));
            Assert.That(File.ReadAllText(primary),Is.EqualTo(before));
        }
        [Test] public void FailedPublicationConsumesOnlyItsOwnCandidate()
        {
            using var prepared=Prepare();string before=File.ReadAllText(primary);
            File.Delete(primary+".backup");Directory.CreateDirectory(primary+".backup");
            Assert.That(file.PublishPrepared(prepared,out _),Is.False);
            Assert.That(File.ReadAllText(primary),Is.EqualTo(before));
            Assert.That(Directory.Exists(primary+".backup"),Is.True);
            Assert.That(File.Exists(primary+".pending"),Is.False);
            Directory.Delete(primary+".backup");
            Assert.That(file.PublishPrepared(prepared,out _),Is.False);
        }
        [Test] public void GeneratedBytesMustPassTheReadContract()
        {
            Assert.That(file.PrepareSave(new Probe{name="unreadable"},file.CaptureForPreparation(),out var prepared,out _),Is.False);
            Assert.That(prepared,Is.Null);Assert.That(file.Load(out _).value,Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(()=>Store("contextual",pure:false).CaptureForPreparation());
        }
        [Test] public void NewerPrimaryCannotBecomeAPreparedBaseline()
        {
            string newer=File.ReadAllText(primary).Replace("\"version\":1","\"version\":2");
            File.WriteAllText(primary,newer);
            Assert.That(file.PrepareSave(new Probe{value=2},file.CaptureForPreparation(),out var prepared,out _),Is.False);
            Assert.That(prepared,Is.Null);Assert.That(File.ReadAllText(primary),Is.EqualTo(newer));
        }
        [Test] public void OversizedCandidateIsRejectedBeforePublication()
        {
            string before=File.ReadAllText(primary);
            Assert.That(file.PrepareSave(new Probe{name=new string('a',9000)},file.CaptureForPreparation(),out var prepared,out _),Is.False);
            Assert.That(prepared,Is.Null);Assert.That(File.ReadAllText(primary),Is.EqualTo(before));
        }
        [TestCase(0)] [TestCase(8193)] public void UncapturablePrimaryIsPreserved(int length)
        {
            string before=new string('a',length);File.WriteAllText(primary,before);
            Assert.Throws<IOException>(()=>file.CaptureForPreparation());
            Assert.That(File.ReadAllText(primary),Is.EqualTo(before));
        }
        [Test] public void CreationAppearingAfterAnAbsentBaselineIsPreserved()
        {
            var fresh=Store("fresh");var capture=fresh.CaptureForPreparation();
            Assert.That(fresh.PrepareSave(new Probe{value=2},capture,out var prepared,out var error),Is.True,error);
            using(prepared) {
                Assert.That(fresh.Save(new Probe{value=3},out error),Is.True,error);
                Assert.That(fresh.PublishPrepared(prepared,out _),Is.False);
                Assert.That(fresh.Load(out _).value,Is.EqualTo(3));
            }
        }
        [Test] public async Task ExpensivePreparationDoesNotHoldThePublicationLock()
        {
            int caller=Environment.CurrentManagedThreadId,worker=caller;
            var entered=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var release=new ManualResetEventSlim();
            observe=x=>{if(x.value!=2)return;worker=Environment.CurrentManagedThreadId;entered.TrySetResult(true);if(!release.Wait(5000))throw new TimeoutException("Preparation was not released.");};
            var captured=file.CaptureForPreparation();
            var task=Task.Run(()=>{Assert.That(file.PrepareSave(new Probe{value=2},captured,out var value,out var error),Is.True,error);return value;});
            try {
                Assert.That(await Task.WhenAny(entered.Task,Task.Delay(5000)),Is.SameAs(entered.Task));
                Assert.That(worker,Is.Not.EqualTo(caller));
                Assert.That(file.Save(new Probe{value=3},out var error),Is.True,error);
            } finally {release.Set();}
            using var prepared=await task;
            Assert.That(file.PublishPrepared(prepared,out _),Is.False);
            Assert.That(file.Load(out _).value,Is.EqualTo(3));
        }
        string[] Stages()=>Directory.GetFiles(directory,"*.prepare.*");
        [Test] public void WorkerStagingIsPrivatePinnedAndPublishedOnlyOnce()
        {
            string before=File.ReadAllText(primary),backup=File.ReadAllText(primary+".backup");
            using var prepared=Prepare(staged:true);
            Assert.That(Stages().Length,Is.EqualTo(1));
            string stage=Stages()[0];StringAssert.Contains("\"value\":2",File.ReadAllText(stage));
            Assert.That(File.ReadAllText(primary),Is.EqualTo(before));
            Assert.That(File.ReadAllText(primary+".backup"),Is.EqualTo(backup));
            Assert.That(file.Load(out _).value,Is.EqualTo(1),"Loading never adopts private prepared bytes");
            Assert.Throws<IOException>(()=>{using var writer=new FileStream(stage,FileMode.Open,FileAccess.Write,FileShare.ReadWrite);});
            Assert.That(file.PublishPrepared(prepared,out var error),Is.True,error);
            Assert.That(Stages(),Is.Empty);Assert.That(File.Exists(primary+".pending"),Is.False);
            Assert.That(file.Load(out _).value,Is.EqualTo(2));
            Assert.That(File.ReadAllText(primary+".backup"),Is.EqualTo(before));
            Assert.That(file.PublishPrepared(prepared,out _),Is.False);
        }
        [Test] public void DisposingStagedPreparationDeletesOnlyItsPrivateFile()
        {
            string before=File.ReadAllText(primary);
            string foreign=primary+".prepare.foreign";File.WriteAllText(foreign,"preserve");
            var prepared=Prepare(staged:true);Assert.That(Stages().Length,Is.EqualTo(2));
            prepared.Dispose();prepared.Dispose();
            Assert.That(Stages(),Is.EqualTo(new[]{foreign}));
            Assert.That(File.ReadAllText(foreign),Is.EqualTo("preserve"));
            Assert.That(File.ReadAllText(primary),Is.EqualTo(before));
            Assert.That(file.PublishPrepared(prepared,out _),Is.False);
        }
        [TestCase(false)] [TestCase(true)]
        public void StagedPublicationPreservesAnInterveningWriterAndItsBackup(bool newer)
        {
            using var prepared=Prepare(staged:true);
            string future=Path.Combine(directory,"probe.v2.json");
            if(newer)File.WriteAllText(future,"future");
            else Assert.That(file.Save(new Probe{value=3},out _),Is.True);
            string before=File.ReadAllText(primary),backup=File.ReadAllText(primary+".backup");
            Assert.That(file.PublishPrepared(prepared,out _),Is.False);
            Assert.That(File.ReadAllText(primary),Is.EqualTo(before));
            Assert.That(File.ReadAllText(primary+".backup"),Is.EqualTo(backup));
            Assert.That(Stages(),Is.Empty);
            if(newer)Assert.That(File.ReadAllText(future),Is.EqualTo("future"));
        }
        [TestCase(false)] [TestCase(true)]
        public void StagedPublicationNeverClaimsAnUnfinishedWriter(bool directoryPending)
        {
            using var prepared=Prepare(staged:true);
            string before=File.ReadAllText(primary),backup=File.ReadAllText(primary+".backup"),pending=primary+".pending";
            if(directoryPending)Directory.CreateDirectory(pending);else File.WriteAllText(pending,"foreign");
            Assert.That(file.PublishPrepared(prepared,out _),Is.False);
            Assert.That(File.ReadAllText(primary),Is.EqualTo(before));
            Assert.That(File.ReadAllText(primary+".backup"),Is.EqualTo(backup));
            if(directoryPending)Assert.That(Directory.Exists(pending),Is.True);else Assert.That(File.ReadAllText(pending),Is.EqualTo("foreign"));
            Assert.That(Stages(),Is.Empty);
        }
        [Test] public void StagedDamagedBaselineIsRetainedAndNotPromotedToBackup()
        {
            var backup=File.ReadAllText(primary+".backup");File.WriteAllText(primary,"{damaged");
            using var prepared=Prepare(staged:true);
            Assert.That(file.PublishPrepared(prepared,out var error),Is.True,error);
            Assert.That(File.ReadAllText(primary+".unreadable"),Is.EqualTo("{damaged"));
            Assert.That(File.ReadAllText(primary+".backup"),Is.EqualTo(backup));
            Assert.That(file.Load(out _).value,Is.EqualTo(2));Assert.That(Stages(),Is.Empty);
        }
        [Test] public void AnOrphanPrivateStageIsNeverRecoveredAsARoom()
        {
            var orphan=primary+".prepare.orphan";File.WriteAllText(orphan,File.ReadAllText(primary).Replace("\"value\":1","\"value\":7"));
            Assert.That(Store().Load(out _).value,Is.EqualTo(1));
            File.Delete(primary);File.Delete(primary+".backup");
            Assert.That(Store().Load(out _),Is.Null);
            Assert.That(File.Exists(orphan),Is.True);
            using var prepared=Prepare(staged:true);
            Assert.That(file.PublishPrepared(prepared,out var error),Is.True,error);
            Assert.That(file.Load(out _).value,Is.EqualTo(2));
            Assert.That(Stages(),Is.EqualTo(new[]{orphan}));
        }
        [Test] public void StagedPreparationCannotBeConsumedByADifferentStore()
        {
            using var prepared=Prepare(staged:true);
            Assert.That(Store().PublishPrepared(prepared,out _),Is.False);
            Assert.That(Stages().Length,Is.EqualTo(1));
            Assert.That(file.PublishPrepared(prepared,out var error),Is.True,error);
            Assert.That(Stages(),Is.Empty);
        }
        [Test] public void AReplacedPrivateStageIsPreservedAfterFailedClaim()
        {
            string pending=primary+".pending";Directory.CreateDirectory(pending);
            var bytes=System.Text.Encoding.UTF8.GetBytes("original");
            using var stage=PreparedFileStage.Create(primary,bytes,default);
            string path=Stages()[0];
            Assert.Throws<IOException>(()=>stage.MoveTo(pending));
            var stamp=File.GetLastWriteTimeUtc(path);
            File.WriteAllText(path,"replaced");File.SetLastWriteTimeUtc(path,stamp);
            stage.Dispose();
            Assert.That(File.ReadAllText(path),Is.EqualTo("replaced"));
            Assert.That(Directory.Exists(pending),Is.True);
        }
        [Test] public async Task BusyRetentionReaderDoesNotBlockStagedPublication()
        {
            using var prepared=Prepare(staged:true);
            string before=File.ReadAllText(primary);
            var entered=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var release=new ManualResetEventSlim();
            observe=x=>{entered.TrySetResult(true);if(!release.Wait(5000))throw new TimeoutException("Retention reader was not released");};
            var reader=Task.Run(()=>file.Retains(x=>new[]{x.name},"one",out _,force:true));
            try {
                Assert.That(await Task.WhenAny(entered.Task,Task.Delay(5000)),Is.SameAs(entered.Task));
                Assert.That(file.PublishPrepared(prepared,out var error),Is.False);
                StringAssert.Contains("busy",error);
                Assert.That(reader.IsCompleted,Is.False,"Publication returned while the reader still owns its lock");
                Assert.That(File.ReadAllText(primary),Is.EqualTo(before));
                Assert.That(Stages(),Is.Empty);
            } finally {release.Set();await reader;observe=null;}
        }
#if UNITY_EDITOR_WIN
        [Test] public void StagedPublicationDoesNotRetryAWindowsReaderOrReplayAfterItLeaves()
        {
            using var prepared=Prepare(staged:true);
            string before=File.ReadAllText(primary);
            using(var reader=new FileStream(primary,FileMode.Open,FileAccess.Read,FileShare.Read)){
                Assert.That(file.PublishPrepared(prepared,out _),Is.False);
                Assert.That(File.ReadAllText(primary),Is.EqualTo(before));
            }
            Assert.That(file.PublishPrepared(prepared,out _),Is.False);
            Assert.That(File.ReadAllText(primary),Is.EqualTo(before));
            Assert.That(Stages(),Is.Empty);Assert.That(File.Exists(primary+".pending"),Is.False);
        }
#endif
        static RoomDocument Room()=>new RoomJournal(new RoomDocument {version=1,objects=new[]{
            new RoomObjectData{id="book",kind=RoomObjectKind.Book},
            new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}
        }}).Snapshot();
        [Test] public async Task RoomPreparationOwnsTheCallerSnapshot()
        {
            var storage=new RoomStorage(directory);var room=Room();
            Assert.That(storage.Save(room,out var error),Is.True,error);
            room.objects[0].name="prepared";
            var task=storage.PrepareSaveAsync(room);room.objects[0].name="later";
            using var prepared=await task;
            Assert.That(Stages().Length,Is.EqualTo(1),"PrepareSaveAsync returns only after writing the private stage");
            Assert.That(storage.Load(out _).objects[0].name,Is.Not.EqualTo("prepared"));
            Assert.That(storage.PublishPrepared(prepared,out error),Is.True,error);
            Assert.That(storage.Load(out _).objects[0].name,Is.EqualTo("prepared"));Assert.That(Stages(),Is.Empty);
        }
        [Test] public async Task CancelledRoomPreparationDoesNotPublish()
        {
            var storage=new RoomStorage(directory);var room=Room();Assert.That(storage.Save(room,out var error),Is.True,error);
            string path=Path.Combine(directory,RoomStorage.FileName),before=File.ReadAllText(path);
            using var cancel=new CancellationTokenSource();cancel.Cancel();
            try {using var result=await storage.PrepareSaveAsync(room,cancel.Token);Assert.Fail("Cancelled preparation completed.");}
            catch(OperationCanceledException) {}
            Assert.That(File.ReadAllText(path),Is.EqualTo(before));
        }
        [Test] public async Task RoomPreparationAdmissionLastsUntilConsumedAndCanRecover()
        {
            var storage=new RoomStorage(directory);var room=Room();Assert.That(storage.Save(room,out var error),Is.True,error);
            var first=await storage.PrepareSaveAsync(room);
            try {using var unexpected=await storage.PrepareSaveAsync(room);Assert.Fail("A second room snapshot was admitted.");}
            catch(InvalidOperationException) {}
            first.Dispose();first.Dispose();
            using var next=await storage.PrepareSaveAsync(room);
            Assert.That(new RoomStorage(directory).PublishPrepared(next,out _),Is.False);
            Assert.That(storage.PublishPrepared(next,out error),Is.True,error);
            using var after=await storage.PrepareSaveAsync(room);
            Assert.That(storage.PublishPrepared(after,out error),Is.True,error);
        }
        [Test] public async Task FailedRoomPreparationReleasesAdmission()
        {
            var storage=new RoomStorage(directory);var room=Room();Assert.That(storage.Save(room,out var error),Is.True,error);
            try {using var unexpected=await storage.PrepareSaveAsync(null);Assert.Fail("Invalid room was prepared.");}
            catch(ArgumentException) {}
            using var after=await storage.PrepareSaveAsync(room);
            Assert.That(storage.PublishPrepared(after,out error),Is.True,error);
        }
        [Test] public async Task BusyDiskCoordinatorRefusesPreparationWithoutBlockingCaller()
        {
            var storage=new RoomStorage(directory);var room=Room();Assert.That(storage.Save(room,out var error),Is.True,error);
            var entered=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var release=new ManualResetEventSlim();
            var writer=Task.Run(()=>{using var held=RoomSnapshotTransaction.Enter(directory);entered.SetResult(true);if(!release.Wait(5000))throw new TimeoutException("Writer was not released.");});
            try {
                Assert.That(await Task.WhenAny(entered.Task,Task.Delay(5000)),Is.SameAs(entered.Task));
                var preparation=storage.PrepareSaveAsync(room);
                Assert.That(preparation.IsCompleted,Is.True,"Busy capture must not queue a blocking worker or block the caller.");
                try {using var unexpected=await preparation;Assert.Fail("Busy snapshot was captured.");}
                catch(IOException) {}
            } finally {release.Set();await writer;}
            using var after=await storage.PrepareSaveAsync(room);
            Assert.That(storage.PublishPrepared(after,out error),Is.True,error);
        }
        [Test] public async Task BusyDiskCoordinatorRefusesStagedPublicationAndReleasesAdmission()
        {
            var storage=new RoomStorage(directory);var room=Room();Assert.That(storage.Save(room,out var error),Is.True,error);
            using var prepared=await storage.PrepareSaveAsync(room);
            var entered=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var release=new ManualResetEventSlim();
            var writer=Task.Run(()=>{using var held=RoomSnapshotTransaction.Enter(directory);entered.SetResult(true);if(!release.Wait(5000))throw new TimeoutException("Writer was not released.");});
            try {
                Assert.That(await Task.WhenAny(entered.Task,Task.Delay(5000)),Is.SameAs(entered.Task));
                Assert.That(storage.PublishPrepared(prepared,out _),Is.False);
                Assert.That(writer.IsCompleted,Is.False,"Owner-thread publication must not wait for the coordinator");
                Assert.That(Stages(),Is.Empty);
            } finally {release.Set();await writer;}
            using var after=await storage.PrepareSaveAsync(room);
            Assert.That(storage.PublishPrepared(after,out error),Is.True,error);
        }
        [Test] public async Task PreparedRoomRejectsAnInterveningAcceptedSave()
        {
            var storage=new RoomStorage(directory);var room=Room();Assert.That(storage.Save(room,out var error),Is.True,error);
            var pending=storage.PrepareSaveAsync(room);
            room.objects[0].name="accepted later";Assert.That(storage.Save(room,out error),Is.True,error);
            using var prepared=await pending;
            Assert.That(storage.PublishPrepared(prepared,out _),Is.False);
            Assert.That(storage.Load(out _).objects[0].name,Is.EqualTo("accepted later"));
        }
    }
}
