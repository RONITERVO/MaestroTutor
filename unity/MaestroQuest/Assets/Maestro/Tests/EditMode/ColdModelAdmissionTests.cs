// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Persistence;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class ColdModelAdmissionTests
    {
        const ImportedAssetKind Kind=ImportedAssetKind.Model;
        string directory,hash;WorkspaceWriteGate workspace;ModelLibrary library;
        [SetUp] public void SetUp()
        {
            directory=Path.Combine(Path.GetTempPath(),"MaestroColdModelAdmission-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            byte[] bytes=StaticModelFixture.Create();hash=ModelLibrary.Hash(bytes);File.WriteAllBytes(Path.Combine(directory,hash+".glb"),bytes);
            workspace=new WorkspaceWriteGate();library=new ModelLibrary(directory,workspace);
        }
        [TearDown] public void TearDown(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Task Prepare(string mode,CancellationToken cancellation=default,ModelLibrary source=null)
        {
            source??=library;
            if(mode=="acoustics")return source.PrepareAcousticsAsync(hash,cancellation);
            return mode=="rest"?source.PrepareRestBoundsAsync(hash,cancellation):source.PrepareBoundsAsync(hash,cancellation);
        }
        static void Complete(Task task)
        {
            Assert.That(SpinWait.SpinUntil(()=>task.IsCompleted,5000),Is.True,"The bounded worker did not drain");
            task.GetAwaiter().GetResult();
        }
        static void Cancelled(Task task)
        {
            Assert.That(SpinWait.SpinUntil(()=>task.IsCompleted,5000),Is.True,"The cancelled worker did not drain");
            Assert.Catch<OperationCanceledException>(()=>task.GetAwaiter().GetResult());
        }
        [TestCase("bounds")][TestCase("rest")][TestCase("acoustics")]
        public void RetirementRejectsFreshPreparationBeforeAnyWorkerOrCachePublication(string mode)
        {
            int reads=0;library.ColdReadCheckpoint=_=>Interlocked.Increment(ref reads);
            using(var retirement=workspace.Assets.TryRetire(Kind,hash,out var error)){
                Assert.That(retirement,Is.Not.Null,error);
                Assert.Throws<InvalidOperationException>(()=>Prepare(mode).GetAwaiter().GetResult());
                Assert.That(reads,Is.Zero);Assert.That(library.TryReadBounds(hash,out _),Is.False);Assert.That(workspace.Assets.Pinned(Kind,hash),Is.Zero);
            }
            Complete(Prepare(mode));Assert.That(reads,Is.EqualTo(2));Assert.That(workspace.Assets.Pinned(Kind,hash),Is.Zero);
            Assert.That(library.TryReadBounds(hash,out _),Is.True);Assert.That(Directory.GetFiles(directory).Length,Is.EqualTo(1));
        }
        [TestCase("bounds")][TestCase("rest")]
        public void CachedPreparationNeedsFreshAdmissionButPassiveMetadataDoesNotReadOrPin(string mode)
        {
            Complete(Prepare(mode));int reads=0;library.ColdReadCheckpoint=_=>Interlocked.Increment(ref reads);
            using(var locked=new FileStream(Path.Combine(directory,hash+".glb"),FileMode.Open,FileAccess.Read,FileShare.None)){
                using(var retirement=workspace.Assets.TryRetire(Kind,hash,out var error)){
                    Assert.That(retirement,Is.Not.Null,error);
                    Assert.Throws<InvalidOperationException>(()=>Prepare(mode).GetAwaiter().GetResult());
                    Assert.That(library.TryReadBounds(hash,out var cached,mode=="rest"),Is.True);Assert.That(cached.Known,Is.True);
                    Assert.That(library.TryReadClips(hash,out _),Is.True);Assert.That(workspace.Assets.Pinned(Kind,hash),Is.Zero);
                }
                Complete(Prepare(mode));Assert.Catch<OperationCanceledException>(()=>Prepare(mode,new CancellationToken(true)).GetAwaiter().GetResult());
                Assert.That(reads,Is.Zero,"Cached metadata must not reopen the locked payload");
                Complete(workspace.Retire());
                Assert.Throws<InvalidOperationException>(()=>Prepare(mode).GetAwaiter().GetResult());
                Assert.That(library.TryReadBounds(hash,out _),Is.True,"Detached metadata remains readable after workspace retirement");
            }
        }
        [TestCase("bounds")][TestCase("rest")][TestCase("acoustics")]
        public void CancelledRealInspectionKeepsExactWorkspacePinUntilWorkerDrains(string mode)
        {
            using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();using var cancellation=new CancellationTokenSource();
            library.ColdReadCheckpoint=phase=>{if(phase!="inspected")return;entered.Set();if(!release.Wait(10000))throw new TimeoutException("Fixture did not release real inspected bytes");};
            var pending=Prepare(mode,cancellation.Token);Task closing=null;
            try{
                Assert.That(entered.Wait(5000),Is.True,"The real file inspection never reached its publication boundary");
                Assert.That(workspace.Assets.Pinned(Kind,hash),Is.EqualTo(1));
                using var blocked=workspace.Assets.TryRetire(Kind,hash,out var error);Assert.That(blocked,Is.Null);Assert.That(error,Does.Contain("in use"));
                var otherWorkspace=new WorkspaceWriteGate();using var independent=otherWorkspace.Assets.TryRetire(Kind,hash,out error);Assert.That(independent,Is.Not.Null,error);
                using(var preservation=workspace.TryFreeze(out error))Assert.That(preservation,Is.Not.Null,error);
                cancellation.Cancel();closing=workspace.Retire();
                Assert.That(pending.IsCompleted,Is.False);Assert.That(closing.IsCompleted,Is.False);Assert.That(workspace.Assets.Pinned(Kind,hash),Is.EqualTo(1));
                var sameWorkspace=new ModelLibrary(directory,workspace);
                Assert.Throws<InvalidOperationException>(()=>Prepare(mode,source:sameWorkspace).GetAwaiter().GetResult());
                Assert.That(library.TryReadBounds(hash,out _),Is.False,"Cancelled inspection must not publish metadata");
            }finally{cancellation.Cancel();release.Set();Cancelled(pending);if(closing!=null)Complete(closing);}
            Assert.That(workspace.Assets.Pinned(Kind,hash),Is.Zero);Assert.That(library.TryReadBounds(hash,out _),Is.False);
        }
        [TestCase("bounds")][TestCase("acoustics")]
        public void CancellationWhileArchiveOwnsFileLaneReleasesQueuedPinWithoutReading(string mode)
        {
            int reads=0;library.ColdReadCheckpoint=_=>Interlocked.Increment(ref reads);
            Assert.That(library.TryCaptureArchive(out var capture),Is.True);using var cancellation=new CancellationTokenSource();Task pending=null;
            try{
                pending=Prepare(mode,cancellation.Token);Assert.That(pending.IsCompleted,Is.False);Assert.That(workspace.Assets.Pinned(Kind,hash),Is.EqualTo(1));
                cancellation.Cancel();Cancelled(pending);Assert.That(reads,Is.Zero);Assert.That(workspace.Assets.Pinned(Kind,hash),Is.Zero);
                using var retirement=workspace.Assets.TryRetire(Kind,hash,out var error);Assert.That(retirement,Is.Not.Null,error);
            }finally{cancellation.Cancel();capture.Dispose();if(pending!=null&&!pending.IsCompleted)Cancelled(pending);}
            Complete(Prepare(mode));Assert.That(reads,Is.EqualTo(2));
        }
        [Test] public void CancellingQueuedColdOwnerDoesNotReleaseTheRunningOwnersPin()
        {
            using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();using var cancellation=new CancellationTokenSource();
            library.ColdReadCheckpoint=phase=>{if(phase!="inspected")return;entered.Set();if(!release.Wait(10000))throw new TimeoutException("Fixture did not release real inspected bytes");};
            var running=Prepare("bounds");Task queued=null;
            try{
                Assert.That(entered.Wait(5000),Is.True);queued=Prepare("acoustics",cancellation.Token);
                Assert.That(workspace.Assets.Pinned(Kind,hash),Is.EqualTo(2));cancellation.Cancel();Cancelled(queued);
                Assert.That(workspace.Assets.Pinned(Kind,hash),Is.EqualTo(1));Assert.That(running.IsCompleted,Is.False);
                using var refused=workspace.Assets.TryRetire(Kind,hash,out _);Assert.That(refused,Is.Null);
            }finally{cancellation.Cancel();release.Set();Complete(running);if(queued!=null&&!queued.IsCompleted)Cancelled(queued);}
            Assert.That(workspace.Assets.Pinned(Kind,hash),Is.Zero);Assert.That(library.TryReadBounds(hash,out _),Is.True);
        }
        [Test] public void SpatialIndexDisposalDoesNotReleaseItsStillRunningInspection()
        {
            using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
            library.ColdReadCheckpoint=phase=>{if(phase!="inspected")return;entered.Set();if(!release.Wait(10000))throw new TimeoutException("Fixture did not release real inspected bytes");};
            using var index=new RoomSpatialIndex(library);var data=new RoomObjectData{id=new string('a',32),kind=RoomObjectKind.ImportedModel,modelHash=hash};
            Task closing=null;
            try{
                index.Synchronize(new RoomDocument{objects=new[]{data}});index.Tick();Assert.That(entered.Wait(5000),Is.True);
                index.Dispose();closing=workspace.Retire();Assert.That(closing.IsCompleted,Is.False);Assert.That(workspace.Assets.Pinned(Kind,hash),Is.EqualTo(1));
                Assert.That(index.TryRead(data.id,RoomEditor.SpatialSource(data.Copy()),out _),Is.False);
            }finally{index.Dispose();release.Set();closing??=workspace.Retire();Complete(closing);}
            Assert.That(workspace.Assets.Pinned(Kind,hash),Is.Zero);Assert.That(library.TryReadBounds(hash,out _),Is.False);
        }
        [TestCase("bounds")][TestCase("acoustics")]
        public void DamagedColdReadReleasesAdmissionWithoutPublishingMetadata(string mode)
        {
            File.WriteAllBytes(Path.Combine(directory,hash+".glb"),new byte[30]);
            Assert.Throws<ModelImportException>(()=>Prepare(mode).GetAwaiter().GetResult());
            Assert.That(workspace.Assets.Pinned(Kind,hash),Is.Zero);Assert.That(library.TryReadBounds(hash,out _),Is.False);
            using var retirement=workspace.Assets.TryRetire(Kind,hash,out var error);Assert.That(retirement,Is.Not.Null,error);
        }
        [TestCase("bounds")][TestCase("acoustics")]
        public void IncludedColdInspectionIsOwnedButNeverMaterializesPrivatePayload(string mode)
        {
            string bundleDirectory=Path.Combine(directory,"bundle"),privateDirectory=Path.Combine(directory,"private");
            var included=BundledAvatarFixture.Write(bundleDirectory,StaticModelFixture.Create());hash=included.Hash;
            library=new ModelLibrary(privateDirectory,workspace,included);int inspections=0;
            library.ColdReadCheckpoint=phase=>{if(phase!="inspected")return;Assert.That(workspace.Assets.Pinned(Kind,hash),Is.EqualTo(1));Interlocked.Increment(ref inspections);};
            Complete(Prepare(mode));Assert.That(inspections,Is.EqualTo(1));Assert.That(Directory.Exists(privateDirectory),Is.False);
            Assert.That(workspace.Assets.Pinned(Kind,hash),Is.Zero);Assert.That(library.TryReadBounds(hash,out _),Is.True);
        }
    }
}
