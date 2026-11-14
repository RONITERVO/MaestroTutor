// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Imports;
using Maestro.Quest.Persistence;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class ModelSourceOwnershipTests
    {
        string directory,hash;WorkspaceWriteGate workspace;ModelLibrary library;
        const ImportedAssetKind Kind=ImportedAssetKind.Model;
        [SetUp]public void SetUp(){directory=Path.Combine(Path.GetTempPath(),"MaestroModelSource-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);var bytes=ModelFixture.Create();hash=ModelLibrary.Hash(bytes);File.WriteAllBytes(Path.Combine(directory,hash+".glb"),bytes);workspace=new();library=new(directory,workspace);}
        [TearDown]public void TearDown(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        static void Drained(Task value){Assert.That(SpinWait.SpinUntil(()=>value.IsCompleted,5000),Is.True,"Actual worker must drain");}
        [Test]public void ReturnedReadOwnsExactSourceThroughChildHandoffAndWorkspaceClose()
        {
            using var read=library.ReadAsync(hash).GetAwaiter().GetResult();Assert.That(workspace.Assets.Pinned(Kind,hash),Is.EqualTo(1));
            using var child=read.RetainForLoad(library.Assets);read.Dispose();Assert.That(workspace.Assets.Pinned(Kind,hash),Is.EqualTo(1));
            Assert.Throws<ObjectDisposedException>(()=>library.Admit(read));Assert.That(read.Bytes.Length,Is.GreaterThan(28),"A draining importer keeps its validated bytes");
            using(var snapshot=workspace.TryFreeze(out var error))Assert.That(snapshot,Is.Not.Null,error);
            using(var blocked=workspace.Assets.TryRetire(Kind,hash,out _))Assert.That(blocked,Is.Null);
            var closing=workspace.Retire();Assert.That(closing.IsCompleted,Is.False);child.Dispose();Drained(closing);Assert.That(workspace.Assets.Pinned(Kind,hash),Is.Zero);
        }
        [Test]public void NewAdmissionCannotCrossWorkspaceOrRetirementAndDoesNotLeak()
        {
            using var read=library.ReadAsync(hash).GetAwaiter().GetResult();var other=new ModelLibrary(directory,new WorkspaceWriteGate());
            Assert.Throws<InvalidOperationException>(()=>other.Admit(read));Assert.Throws<InvalidOperationException>(()=>read.RetainForLoad(other.Assets));Assert.Throws<InvalidOperationException>(()=>read.RetainForLoad(null));
            Assert.That(workspace.Assets.Pinned(Kind,hash),Is.EqualTo(1));Assert.That(other.Assets.Pinned(Kind,hash),Is.Zero);
            var closing=workspace.Retire();Assert.Throws<InvalidOperationException>(()=>read.RetainForLoad(library.Assets));Assert.Throws<InvalidOperationException>(()=>library.ReadAsync(hash));
            read.Dispose();Drained(closing);
        }
        [Test]public void RawSelectionAdmissionIsIndependentButNativeLoadRequiresBinding()
        {
            using var raw=ModelLibrary.Inspect("choice",ModelFixture.Create());Assert.Throws<InvalidOperationException>(()=>raw.RetainForLoad(library.Assets));
            using var selected=library.Admit(raw);using var accepted=library.Admit(selected);raw.Dispose();selected.Dispose();
            Assert.That(workspace.Assets.Pinned(Kind,hash),Is.EqualTo(1));using var child=accepted.RetainForLoad(library.Assets);accepted.Dispose();Assert.That(workspace.Assets.Pinned(Kind,hash),Is.EqualTo(1));child.Dispose();Assert.That(workspace.Assets.Pinned(Kind,hash),Is.Zero);
        }
        [Test]public void ChangedIdentityCannotBorrowTheOriginalModelsPin()
        {
            using var read=library.ReadAsync(hash).GetAwaiter().GetResult();read.Hash=new string('a',64);
            Assert.Throws<InvalidOperationException>(()=>library.Admit(read));Assert.Throws<InvalidOperationException>(()=>read.RetainForLoad(library.Assets));read.Dispose();Assert.That(workspace.Assets.Pinned(Kind,hash),Is.Zero);
        }
        [Test]public void CancelBeforeDispatchAndAssetRetirementNeverEnterWorker()
        {
            int reads=0;library.ReadCheckpoint=_=>reads++;
            Assert.Catch<OperationCanceledException>(()=>library.ReadAsync(hash,new CancellationToken(true)));
            using var retirement=workspace.Assets.TryRetire(Kind,hash,out var error);Assert.That(retirement,Is.Not.Null,error);
            Assert.Throws<InvalidOperationException>(()=>library.ReadAsync(hash));Assert.That(reads,Is.Zero);Assert.That(workspace.Assets.Pinned(Kind,hash),Is.Zero);
        }
        [Test]public void CancelWhileQueuedDoesNotRetainArchiveOrSource()
        {
            Assert.That(library.TryCaptureArchive(out var capture),Is.True);using var cancel=new CancellationTokenSource();Task<ModelAsset> pending=null;
            try {pending=library.ReadAsync(hash,cancel.Token);Assert.That(workspace.Assets.Pinned(Kind,hash),Is.EqualTo(1));cancel.Cancel();Drained(pending);Assert.Catch<OperationCanceledException>(()=>pending.GetAwaiter().GetResult());Assert.That(workspace.Assets.Pinned(Kind,hash),Is.Zero);}
            finally {cancel.Cancel();capture.Dispose();if(pending!=null)Drained(pending);}
        }
        [Test]public void CancellationKeepsRealInspectedWorkerPinnedUntilDrainWithoutPublishingCache()
        {
            using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();using var cancel=new CancellationTokenSource();
            library.ReadCheckpoint=stage=>{if(stage!="inspected")return;entered.Set();if(!release.Wait(10000))throw new TimeoutException();};var pending=library.ReadAsync(hash,cancel.Token);Task closing=null;
            try {Assert.That(entered.Wait(5000),Is.True);cancel.Cancel();closing=workspace.Retire();Assert.That(closing.IsCompleted,Is.False);Assert.That(pending.IsCompleted,Is.False);Assert.That(workspace.Assets.Pinned(Kind,hash),Is.EqualTo(1));}
            finally {cancel.Cancel();release.Set();Drained(pending);Assert.Catch<OperationCanceledException>(()=>pending.GetAwaiter().GetResult());if(closing!=null)Drained(closing);}
            Assert.That(workspace.Assets.Pinned(Kind,hash),Is.Zero);Assert.That(library.TryReadBounds(hash,out _),Is.False);
        }
        [Test]public void FailedReadsReleasePinAndDoNotRepairPayload()
        {
            File.WriteAllBytes(Path.Combine(directory,hash+".glb"),new byte[30]);Assert.Throws<ModelImportException>(()=>library.ReadAsync(hash).GetAwaiter().GetResult());Assert.That(workspace.Assets.Pinned(Kind,hash),Is.Zero);Assert.That(File.ReadAllBytes(Path.Combine(directory,hash+".glb")),Is.EqualTo(new byte[30]));
            Assert.Throws<ModelImportException>(()=>library.ReadAsync(new string('b',64)).GetAwaiter().GetResult());Assert.That(workspace.Assets.Pinned(Kind,new string('b',64)),Is.Zero);
        }
        [Test]public void SaveOwnsItsAdmissionAfterCallerDisposalUntilQueuedWriteDrains()
        {
            using var raw=ModelLibrary.Inspect("save",ModelFixture.Create());using var selected=library.Admit(raw);
            Assert.That(library.TryCaptureArchive(out var capture),Is.True);Task saving=null;
            try {saving=library.SaveAsync(selected);selected.Dispose();Assert.That(workspace.Assets.Pinned(Kind,hash),Is.EqualTo(1));Assert.That(saving.IsCompleted,Is.False);}
            finally {capture.Dispose();if(saving!=null){Drained(saving);saving.GetAwaiter().GetResult();}}
            Assert.That(workspace.Assets.Pinned(Kind,hash),Is.Zero);using var read=library.ReadAsync(hash).GetAwaiter().GetResult();Assert.That(read.Bytes,Is.EqualTo(raw.Bytes));
        }
    }
}
