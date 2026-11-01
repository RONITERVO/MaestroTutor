// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Imports;
using Maestro.Quest.Persistence;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class AudioMaintenanceTests
    {
        string directory;WorkspaceWriteGate gate;AudioLibrary library;AudioAsset asset;
        string Payload=>Path.Combine(directory,asset.Hash+".wav");string Metadata=>Path.Combine(directory,asset.Hash+".txt");
        [SetUp]public void Setup(){directory=Path.Combine(Path.GetTempPath(),"MaestroAudioMaintenance-"+Guid.NewGuid().ToString("N"));gate=new();library=new(directory,gate);asset=AudioLibrary.Inspect("Original",WaveAudioTests.File());library.SaveAsync(asset).GetAwaiter().GetResult();}
        [TearDown]public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        AudioFileReview Inspect()=>library.InspectFileAsync(asset.Hash).GetAwaiter().GetResult();
        Task<AudioFileReview> Remove(string revision,CancellationToken cancel=default,Func<Task<string>> protection=null)=>Task.Run(()=>library.RemoveDownloadAsync(asset.Hash,revision,protection??(()=>Task.FromResult<string>(null)),cancel));
        [Test]public void ReviewedRemovalKeepsCompleteMetadataAndExactReimportRestoresIdentity(){
            var other=AudioLibrary.Inspect("Other",WaveAudioTests.File(sample:(i,c)=>.5));library.SaveAsync(other).GetAwaiter().GetResult();
            byte[] metadata=Encoding.Unicode.GetPreamble().AsSpan().ToArray();var tail=Encoding.Unicode.GetBytes("Original\r\nAttribution stays here\r\n");var whole=new byte[metadata.Length+tail.Length];metadata.CopyTo(whole,0);tail.CopyTo(whole,metadata.Length);File.WriteAllBytes(Metadata,whole);
            var before=Inspect();Assert.That(before.PayloadPresent&&before.ContentValid&&before.MetadataPresent,Is.True);Assert.That(before.Name,Is.EqualTo("Original"));
            var after=Remove(before.Revision).GetAwaiter().GetResult();Assert.That(after.PayloadPresent,Is.False);Assert.That(after.Revision,Is.Not.EqualTo(before.Revision));Assert.That(File.ReadAllBytes(Metadata),Is.EqualTo(whole));Assert.That(File.Exists(Path.Combine(directory,other.Hash+".wav")),Is.True);Assert.That(gate.Frozen,Is.False);
            library.SaveAsync(asset).GetAwaiter().GetResult();Assert.That(library.Review(asset.Hash),Is.Null);Assert.That(library.ReadAsync(asset.Hash).GetAwaiter().GetResult().Hash,Is.EqualTo(asset.Hash));Assert.That(File.ReadAllBytes(Metadata),Is.EqualTo(whole));
        }
        [TestCase(true)][TestCase(false)]public void ContentOrMetadataChangesInvalidateReviewedRemoval(bool payload){
            var before=Inspect();string path=payload?Payload:Metadata;byte[] replacement=payload?new byte[]{1,2,3}:Encoding.UTF8.GetBytes("New reviewed name");File.WriteAllBytes(path,replacement);
            Assert.Throws<InvalidOperationException>(()=>Remove(before.Revision).GetAwaiter().GetResult());Assert.That(File.ReadAllBytes(path),Is.EqualTo(replacement));Assert.That(File.Exists(Payload),Is.True);Assert.That(gate.CanFreeze(out _),Is.True);
        }
        [Test]public void DamagedSmallPayloadCanBeReviewedWithoutPretendingItIsValid(){
            File.WriteAllBytes(Payload,new byte[]{0});var before=Inspect();Assert.That(before.ContentValid,Is.False);Assert.That(before.PayloadPresent,Is.True);Assert.That(before.Bytes,Is.EqualTo(1));
            Assert.That(Remove(before.Revision).GetAwaiter().GetResult().PayloadPresent,Is.False);
        }
        [Test]public void UncertainReferenceProofAndLivePinsPreserveBytesAndReopenAdmission(){
            var before=Inspect();Assert.Throws<InvalidOperationException>(()=>Remove(before.Revision,protection:()=>Task.FromResult("Retained evidence unavailable")).GetAwaiter().GetResult());Assert.That(File.Exists(Payload),Is.True);
            using(var pin=gate.Assets.Pin(ImportedAssetKind.Audio,asset.Hash))Assert.Throws<InvalidOperationException>(()=>Remove(before.Revision).GetAwaiter().GetResult());
            using var reopened=gate.Assets.Pin(ImportedAssetKind.Audio,asset.Hash);Assert.That(reopened,Is.Not.Null);Assert.That(gate.CanFreeze(out _),Is.True);
        }
        [Test]public void CancellationBeforeDispatchLeavesFilesAndReleasesAllHolds(){
            var before=Inspect();using var cancel=new CancellationTokenSource();library.MaintenanceCheckpoint=phase=>{if(phase=="ready")cancel.Cancel();};
            Assert.Catch<OperationCanceledException>(()=>Remove(before.Revision,cancel.Token).GetAwaiter().GetResult());Assert.That(File.ReadAllBytes(Payload),Is.EqualTo(asset.Content));Assert.That(gate.Frozen,Is.False);using var pin=gate.Assets.Pin(ImportedAssetKind.Audio,asset.Hash);
        }
        [Test]public void CancellationAfterDispatchKeepsFenceUntilActualDeletionDrains(){
            var before=Inspect();using var cancel=new CancellationTokenSource();using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
            library.MaintenanceCheckpoint=phase=>{if(phase=="deleting"){entered.Set();if(!release.Wait(10000))throw new TimeoutException();}};
            var task=Remove(before.Revision,cancel.Token);
            try{Assert.That(entered.Wait(8000),Is.True);cancel.Cancel();Assert.That(task.IsCompleted,Is.False);Assert.That(gate.Frozen,Is.True);Assert.That(gate.Assets.TryPin(ImportedAssetKind.Audio,asset.Hash,out _),Is.Null);Assert.That(File.Exists(Payload),Is.True);}
            finally{release.Set();}
            Assert.That(task.GetAwaiter().GetResult().PayloadPresent,Is.False);Assert.That(gate.Frozen,Is.False);
        }
        [Test]public void LastFileCheckRejectsChangesAfterRetainedProof(){
            var before=Inspect();byte[] replacement={9,8,7};library.MaintenanceCheckpoint=phase=>{if(phase=="deleting")File.WriteAllBytes(Payload,replacement);};
            Assert.Throws<InvalidOperationException>(()=>Remove(before.Revision).GetAwaiter().GetResult());Assert.That(File.ReadAllBytes(Payload),Is.EqualTo(replacement));Assert.That(gate.Frozen,Is.False);
        }
        [Test]public void InspectionAndRemovalNeverTraverseDirectoryEntries(){
            var before=Inspect();File.Delete(Payload);Directory.CreateDirectory(Payload);Assert.Throws<InvalidDataException>(()=>Inspect());Assert.Throws<InvalidDataException>(()=>Remove(before.Revision).GetAwaiter().GetResult());Assert.That(Directory.Exists(Payload),Is.True);
        }
        [Test]public void DamagedPayloadStillRequiresCertainUnusedProof(){
            File.WriteAllBytes(Payload,new byte[]{0});var before=Inspect();Assert.That(before.ContentValid,Is.False);
            foreach(string reason in new[]{"A saved clip source still uses this sound","Retained sound evidence is unavailable"}){
                Assert.Throws<InvalidOperationException>(()=>Remove(before.Revision,protection:()=>Task.FromResult(reason)).GetAwaiter().GetResult());Assert.That(File.ReadAllBytes(Payload),Is.EqualTo(new byte[]{0}));Assert.That(gate.CanFreeze(out _),Is.True);
            }
        }
        [TestCase(true)][TestCase(false)]public void OversizedOrLockedEvidenceRefusesInspectionAndRemoval(bool oversized){
            var before=Inspect();FileStream held=null;
            try{
                if(oversized){using var payload=new FileStream(Payload,FileMode.Open,FileAccess.Write,FileShare.None);payload.SetLength(WaveAudio.MaximumBytes+1L);}
                else held=new FileStream(Metadata,FileMode.Open,FileAccess.Read,FileShare.None);
                if(oversized){Assert.Throws<InvalidDataException>(()=>Inspect());Assert.Throws<InvalidDataException>(()=>Remove(before.Revision).GetAwaiter().GetResult());}
                else {Assert.Throws<IOException>(()=>Inspect());Assert.Throws<IOException>(()=>Remove(before.Revision).GetAwaiter().GetResult());}
                Assert.That(File.Exists(Payload),Is.True);Assert.That(gate.CanFreeze(out _),Is.True);
            }finally{held?.Dispose();}
        }
        [Test]public void PassiveReviewDoesNotReadFilesAndInvalidationIsVisibleToInventory(){
            var scan=library.ScanAsync().GetAwaiter().GetResult();Assert.That(scan.SourceRevision,Is.EqualTo(library.InventoryRevision));var before=Inspect();
            using(var held=new FileStream(Payload,FileMode.Open,FileAccess.Read,FileShare.None))Assert.That(library.Review(asset.Hash),Is.SameAs(before),"Passive review must not reopen its payload");
            Remove(before.Revision).GetAwaiter().GetResult();Assert.That(library.InventoryRevision,Is.GreaterThan(scan.SourceRevision));Assert.That(library.Review(asset.Hash).PayloadPresent,Is.False);
            library.SaveAsync(asset).GetAwaiter().GetResult();Assert.That(library.Review(asset.Hash),Is.Null);Assert.That(Inspect().Revision,Is.EqualTo(before.Revision));
        }
    }
}
