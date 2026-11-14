// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Imports;
using Maestro.Quest.Persistence;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class ModelMaintenanceTests
    {
        string directory;WorkspaceWriteGate gate;ModelLibrary library;ModelAsset asset;
        const ImportedAssetKind Kind=ImportedAssetKind.Model;
        string Payload=>Path.Combine(directory,asset.Hash+".glb");
        string Metadata=>Path.Combine(directory,asset.Hash+".txt");
        [SetUp]public void Setup(){directory=Path.Combine(Path.GetTempPath(),"MaestroModelMaintenance-"+Guid.NewGuid().ToString("N"));gate=new();library=new(directory,gate);asset=ModelLibrary.Inspect("Original model",ModelFixture.Create());library.SaveAsync(asset).GetAwaiter().GetResult();}
        [TearDown]public void Cleanup(){asset?.Dispose();if(Directory.Exists(directory))Directory.Delete(directory,true);}
        ModelFileReview Inspect()=>library.InspectFileAsync(asset.Hash).GetAwaiter().GetResult();
        Task<ModelFileReview> Remove(string revision,CancellationToken token=default,Func<Task<string>> protection=null)=>Task.Run(()=>library.RemoveDownloadAsync(asset.Hash,revision,protection??(()=>Task.FromResult<string>(null)),token));
        static void Drained(Task task){Assert.That(SpinWait.SpinUntil(()=>task.IsCompleted,10000),Is.True,"Actual model worker did not drain");}
        [TestCase(false)][TestCase(true)]public void ReviewedRemovalKeepsFullAttributionAndExactReimportRestoresIdentity(bool unicode){
            using var other=ModelLibrary.Inspect("Other model",StaticModelFixture.Create());library.SaveAsync(other).GetAwaiter().GetResult();
            string header="Original model\r\nSHA256: "+asset.Hash+"\r\n\r\nArtist licence and attribution\r\n";
            var encoding=unicode?Encoding.Unicode:new UTF8Encoding(false);var preamble=encoding.GetPreamble();string text=header+new string('x',(128*1024-preamble.Length-encoding.GetByteCount(header))/encoding.GetByteCount("x"));var body=encoding.GetBytes(text);var metadata=new byte[preamble.Length+body.Length];preamble.CopyTo(metadata,0);body.CopyTo(metadata,preamble.Length);Assert.That(metadata.Length,Is.EqualTo(128*1024));File.WriteAllBytes(Metadata,metadata);
            var before=Inspect();Assert.That(before.PayloadPresent&&before.MetadataPresent&&before.ContentValid,Is.True);Assert.That(before.Included,Is.False);Assert.That(before.Name,Is.EqualTo("Original model"));
            var after=Remove(before.Revision).GetAwaiter().GetResult();Assert.That(after.PayloadPresent,Is.False);Assert.That(after.Revision,Is.Not.EqualTo(before.Revision));Assert.That(File.ReadAllBytes(Metadata),Is.EqualTo(metadata));Assert.That(File.ReadAllBytes(Path.Combine(directory,other.Hash+".glb")),Is.EqualTo(other.Bytes));Assert.That(gate.Frozen,Is.False);
            using var renamed=ModelLibrary.Inspect("New picker display name",asset.Bytes);library.SaveAsync(renamed).GetAwaiter().GetResult();
            Assert.That(library.Review(asset.Hash),Is.Null);using var read=library.ReadAsync(asset.Hash).GetAwaiter().GetResult();Assert.That(read.Bytes,Is.EqualTo(asset.Bytes));Assert.That(File.ReadAllBytes(Metadata),Is.EqualTo(metadata));Assert.That(Inspect().Revision,Is.EqualTo(before.Revision));
        }
        [TestCase(true)][TestCase(false)]public void ChangedPayloadOrAttributionRefusesTheReviewedRevision(bool payload){
            var before=Inspect();string path=payload?Payload:Metadata;byte[] replacement=payload?new byte[]{1,2,3}:Encoding.UTF8.GetBytes("Changed attribution");File.WriteAllBytes(path,replacement);
            Assert.Throws<InvalidOperationException>(()=>Remove(before.Revision).GetAwaiter().GetResult());Assert.That(File.ReadAllBytes(path),Is.EqualTo(replacement));Assert.That(File.Exists(Payload),Is.True);Assert.That(gate.CanFreeze(out _),Is.True);
        }
        [Test]public void DamagedPayloadIsReviewableButStillNeedsCertainUnusedProof(){
            File.WriteAllBytes(Payload,new byte[]{0});var before=Inspect();Assert.That(before.PayloadPresent,Is.True);Assert.That(before.ContentValid,Is.False);Assert.That(before.Bytes,Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(()=>Remove(before.Revision,protection:()=>Task.FromResult("Retained model evidence is unavailable")).GetAwaiter().GetResult());Assert.That(File.ReadAllBytes(Payload),Is.EqualTo(new byte[]{0}));
            Assert.That(Remove(before.Revision).GetAwaiter().GetResult().PayloadPresent,Is.False);
        }
        [Test]public void ReadAndNativeChildOwnersBothProtectThePayloadAndReopenAdmissionOnRelease(){
            var before=Inspect();using var read=library.ReadAsync(asset.Hash).GetAwaiter().GetResult();using var child=read.RetainForLoad(library.Assets);
            read.Dispose();Assert.That(gate.Assets.Pinned(Kind,asset.Hash),Is.EqualTo(1));Assert.Throws<InvalidOperationException>(()=>Remove(before.Revision).GetAwaiter().GetResult());Assert.That(File.ReadAllBytes(Payload),Is.EqualTo(asset.Bytes));
            child.Dispose();Assert.That(gate.Assets.Pinned(Kind,asset.Hash),Is.Zero);Assert.That(Remove(before.Revision).GetAwaiter().GetResult().PayloadPresent,Is.False);using var reopened=gate.Assets.Pin(Kind,asset.Hash);Assert.That(reopened,Is.Not.Null);
        }
        [Test]public void CancellationBeforeDispatchPreservesPayloadMetadataAndReleasesAllHolds(){
            var before=Inspect();var metadata=File.ReadAllBytes(Metadata);using var cancel=new CancellationTokenSource();library.MaintenanceCheckpoint=phase=>{if(phase=="ready")cancel.Cancel();};
            Assert.Catch<OperationCanceledException>(()=>Remove(before.Revision,cancel.Token).GetAwaiter().GetResult());Assert.That(File.ReadAllBytes(Payload),Is.EqualTo(asset.Bytes));Assert.That(File.ReadAllBytes(Metadata),Is.EqualTo(metadata));Assert.That(gate.Frozen,Is.False);using var pin=gate.Assets.Pin(Kind,asset.Hash);
        }
        [Test]public void CancellationAfterDispatchKeepsFileAndAdmissionHoldsUntilTheWorkerDrains(){
            var before=Inspect();using var cancel=new CancellationTokenSource();using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
            library.MaintenanceCheckpoint=phase=>{if(phase=="deleting"){entered.Set();if(!release.Wait(10000))throw new TimeoutException();}};var task=Remove(before.Revision,cancel.Token);
            try{Assert.That(entered.Wait(8000),Is.True);cancel.Cancel();Assert.That(task.IsCompleted,Is.False);Assert.That(gate.Frozen,Is.True);Assert.That(gate.Assets.TryPin(Kind,asset.Hash,out _),Is.Null);Assert.Throws<InvalidOperationException>(()=>library.ReadAsync(asset.Hash));Assert.That(File.Exists(Payload),Is.True);}
            finally{release.Set();Drained(task);}
            Assert.That(task.GetAwaiter().GetResult().PayloadPresent,Is.False);Assert.That(gate.Frozen,Is.False);Assert.That(gate.Assets.Pinned(Kind,asset.Hash),Is.Zero);
        }
        [Test]public void FinalFileCheckRejectsAttributionChangedAfterReferenceProof(){
            var before=Inspect();byte[] replacement=Encoding.UTF8.GetBytes("New attribution after review");library.MaintenanceCheckpoint=phase=>{if(phase=="deleting")File.WriteAllBytes(Metadata,replacement);};
            Assert.Throws<InvalidOperationException>(()=>Remove(before.Revision).GetAwaiter().GetResult());Assert.That(File.ReadAllBytes(Payload),Is.EqualTo(asset.Bytes));Assert.That(File.ReadAllBytes(Metadata),Is.EqualTo(replacement));Assert.That(gate.Frozen,Is.False);
        }
        [TestCase(true)][TestCase(false)]public void DirectoryEntriesAreNeverMistakenForMissingFiles(bool payload){
            var before=Inspect();string path=payload?Payload:Metadata;File.Delete(path);Directory.CreateDirectory(path);
            Assert.Throws<InvalidDataException>(()=>Inspect());Assert.Throws<InvalidDataException>(()=>Remove(before.Revision).GetAwaiter().GetResult());Assert.That(Directory.Exists(path),Is.True);Assert.That(gate.CanFreeze(out _),Is.True);
        }
        [TestCase(true)][TestCase(false)]public void OversizedEvidenceIsPreservedWithoutInspectionOrRemoval(bool payload){
            var before=Inspect();string path=payload?Payload:Metadata;using(var file=new FileStream(path,FileMode.Open,FileAccess.Write,FileShare.None))file.SetLength(payload?ModelInspection.MaximumBytes+1L:128*1024+1L);
            Assert.Throws<InvalidDataException>(()=>Inspect());Assert.Throws<InvalidDataException>(()=>Remove(before.Revision).GetAwaiter().GetResult());Assert.That(File.Exists(Payload),Is.True);Assert.That(gate.CanFreeze(out _),Is.True);
        }
        [Test]public void LockedMetadataCannotBecomeUnusedOrAbsentEvidence(){
            var before=Inspect();using(var locked=new FileStream(Metadata,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){
                Assert.Throws<IOException>(()=>Inspect());Assert.Throws<IOException>(()=>Remove(before.Revision).GetAwaiter().GetResult());Assert.That(File.Exists(Payload),Is.True);Assert.That(gate.CanFreeze(out _),Is.True);
            }
        }
        [Test]public void PassiveReviewAndGeometryCachesAreInvalidatedByRemovalAndExplicitReimport(){
            var before=Inspect();long revision=library.InventoryRevision;Assert.That(library.TryReadBounds(asset.Hash,out _),Is.True);Assert.That(library.TryReadClips(asset.Hash,out _),Is.True);
            using(var locked=new FileStream(Payload,FileMode.Open,FileAccess.Read,FileShare.None))Assert.That(library.Review(asset.Hash),Is.SameAs(before),"Passive review cannot reopen the file");
            Remove(before.Revision).GetAwaiter().GetResult();Assert.That(library.InventoryRevision,Is.GreaterThan(revision));Assert.That(library.Review(asset.Hash).PayloadPresent,Is.False);Assert.That(library.TryReadBounds(asset.Hash,out _),Is.False);Assert.That(library.TryReadBounds(asset.Hash,out _,true),Is.False);Assert.That(library.TryReadClips(asset.Hash,out _),Is.False);
            library.SaveAsync(asset).GetAwaiter().GetResult();Assert.That(library.Review(asset.Hash),Is.Null);Assert.That(library.TryReadBounds(asset.Hash,out _),Is.True);Assert.That(library.TryReadClips(asset.Hash,out _),Is.True);Assert.That(Inspect().Revision,Is.EqualTo(before.Revision));
        }
        [TestCase(false)][TestCase(true)]public void IncludedAvatarIsProtectedEvenWithoutAPrivateCopy(bool copied){
            var included=BundledAvatarFixture.Write(Path.Combine(directory,"package"),ModelFixture.Mixamo());string models=Path.Combine(directory,"included-models");var source=new ModelLibrary(models,new WorkspaceWriteGate(),included);
            if(copied)source.ReadAsync(included.Hash).GetAwaiter().GetResult().Dispose();var before=source.InspectFileAsync(included.Hash).GetAwaiter().GetResult();Assert.That(before.Included,Is.True);Assert.That(before.PayloadPresent,Is.EqualTo(copied));
            bool checkedReferences=false;Assert.Throws<InvalidOperationException>(()=>Task.Run(()=>source.RemoveDownloadAsync(included.Hash,before.Revision,()=>{checkedReferences=true;return Task.FromResult<string>(null);})).GetAwaiter().GetResult());
            Assert.That(checkedReferences,Is.False,"Included content is protected independently of retained-reference state");Assert.That(File.Exists(Path.Combine(models,included.Hash+".glb")),Is.EqualTo(copied));
        }
        [TestCase(false)][TestCase(true)]public void ExplicitReimportRepairsInvalidMetadataButPassiveInspectionNeverDoes(bool oversized){
            byte[] invalid=oversized?new byte[128*1024+1]:new byte[]{0xff,0xff,0};File.WriteAllBytes(Metadata,invalid);if(oversized)Assert.Throws<InvalidDataException>(()=>Inspect());else Inspect();Assert.That(File.ReadAllBytes(Metadata),Is.EqualTo(invalid));
            library.SaveAsync(asset).GetAwaiter().GetResult();var repaired=File.ReadAllBytes(Metadata);Assert.That(repaired,Is.Not.EqualTo(invalid));Assert.That(File.ReadAllText(Metadata),Does.Contain("Original model"));Assert.That(File.ReadAllBytes(Payload),Is.EqualTo(asset.Bytes));
            using var renamed=ModelLibrary.Inspect("Other name",asset.Bytes);library.SaveAsync(renamed).GetAwaiter().GetResult();Assert.That(File.ReadAllBytes(Metadata),Is.EqualTo(repaired));Assert.That(Directory.GetFiles(directory,"*.part"),Is.Empty);
        }
        [Test]public void CancellingRemovalQueuedBehindRealColdInspectionPreservesBothOwners(){
            var cold=new ModelLibrary(directory,gate);var before=cold.InspectFileAsync(asset.Hash).GetAwaiter().GetResult();using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();using var stop=new CancellationTokenSource();
            cold.ColdReadCheckpoint=phase=>{if(phase=="inspected"){entered.Set();if(!release.Wait(10000))throw new TimeoutException();}};var prepare=cold.PrepareRestBoundsAsync(asset.Hash,CancellationToken.None);Task<ModelFileReview> remove=null;
            try{Assert.That(entered.Wait(8000),Is.True);remove=Task.Run(()=>cold.RemoveDownloadAsync(asset.Hash,before.Revision,()=>Task.FromResult<string>(null),stop.Token));Assert.That(SpinWait.SpinUntil(()=>gate.Frozen,5000),Is.True);Assert.That(remove.IsCompleted,Is.False);stop.Cancel();Drained(remove);Assert.Catch<OperationCanceledException>(()=>remove.GetAwaiter().GetResult());Assert.That(gate.Frozen,Is.False);Assert.That(prepare.IsCompleted,Is.False);Assert.That(gate.Assets.Pinned(Kind,asset.Hash),Is.EqualTo(1));Assert.That(File.ReadAllBytes(Payload),Is.EqualTo(asset.Bytes));}
            finally{stop.Cancel();release.Set();Drained(prepare);if(remove!=null)Drained(remove);}
            prepare.GetAwaiter().GetResult();Assert.That(gate.Assets.Pinned(Kind,asset.Hash),Is.Zero);Assert.That(cold.TryReadBounds(asset.Hash,out _,true),Is.True);
        }
        [TestCase(false)][TestCase(true)]public void FullMetadataRepairHonoursCancellationAndAConcurrentValidPublication(bool cancel){
            byte[] invalid={0xff,0xff,0};File.WriteAllBytes(Metadata,invalid);using var stop=new CancellationTokenSource();var replacement=Encoding.UTF8.GetBytes("Another accepted attribution\n"+new string('z',4000));bool staged=false;
            void BeforePublish(){staged=true;Assert.That(File.ReadAllBytes(Metadata),Is.EqualTo(invalid));if(cancel)stop.Cancel();else File.WriteAllBytes(Metadata,replacement);}
            void Save()=>ImportedAssetSidecar.SaveAcceptedDocument(Metadata,"New valid metadata\n"+new string('x',6000),128*1024,stop.Token,BeforePublish);
            if(cancel)Assert.Catch<OperationCanceledException>(()=>Save());else Save();Assert.That(staged,Is.True);Assert.That(File.ReadAllBytes(Metadata),Is.EqualTo(cancel?invalid:replacement));Assert.That(File.ReadAllBytes(Payload),Is.EqualTo(asset.Bytes));Assert.That(Directory.GetFiles(directory,"*.part"),Is.Empty);
        }
        [Test]public void MissingOrdinaryLibraryInspectionDoesNotCreateDirectoriesOrPayloads(){
            string missing=Path.Combine(directory,"ordinary","missing-models");var source=new ModelLibrary(missing);var review=source.InspectFileAsync(asset.Hash).GetAwaiter().GetResult();
            Assert.That(review.PayloadPresent||review.MetadataPresent||review.ContentValid||review.Included,Is.False);Assert.That(review.Bytes,Is.Zero);Assert.That(Directory.Exists(Path.Combine(directory,"ordinary")),Is.False);
        }
        [TestCase(false)][TestCase(true)]public void LinkedAncestorRefusesInspectionRemovalAndImportWithoutTouchingTarget(bool populated){
            string target=Path.Combine(directory,"target"),models=Path.Combine(target,"models"),alias=Path.Combine(directory,"linked-parent");Directory.CreateDirectory(target);
            var direct=new ModelLibrary(models);if(populated)direct.SaveAsync(asset).GetAwaiter().GetResult();var before=direct.InspectFileAsync(asset.Hash).GetAwaiter().GetResult();
            byte[] metadata=populated?File.ReadAllBytes(Path.Combine(models,asset.Hash+".txt")):null;CreateDirectoryLink(alias,target);
            try{
                var linked=new ModelLibrary(Path.Combine(alias,"models"));Assert.Throws<InvalidDataException>(()=>linked.InspectFileAsync(asset.Hash).GetAwaiter().GetResult());
                bool proof=false;Assert.Throws<InvalidDataException>(()=>Task.Run(()=>linked.RemoveDownloadAsync(asset.Hash,before.Revision,()=>{proof=true;return Task.FromResult<string>(null);})).GetAwaiter().GetResult());Assert.That(proof,Is.False);
                Assert.Throws<InvalidDataException>(()=>linked.SaveAsync(asset).GetAwaiter().GetResult());using var other=ModelLibrary.Inspect("Different new model",StaticModelFixture.Create());Assert.Throws<InvalidDataException>(()=>linked.SaveAsync(other).GetAwaiter().GetResult());
                Assert.That(Directory.Exists(models),Is.EqualTo(populated));Assert.That(File.Exists(Path.Combine(models,other.Hash+".glb")),Is.False);
                if(populated){Assert.That(File.ReadAllBytes(Path.Combine(models,asset.Hash+".glb")),Is.EqualTo(asset.Bytes));Assert.That(File.ReadAllBytes(Path.Combine(models,asset.Hash+".txt")),Is.EqualTo(metadata));Assert.That(Directory.GetFiles(models),Has.Length.EqualTo(2));}
            }finally{Directory.Delete(alias);}
        }
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        [DllImport("kernel32.dll",EntryPoint="CreateSymbolicLinkW",CharSet=CharSet.Unicode,SetLastError=true)]
        [return:MarshalAs(UnmanagedType.I1)]static extern bool DirectoryLink(string path,string target,uint flags);
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
        [DllImport("libc",EntryPoint="symlink",SetLastError=true)]static extern int DirectoryLink(string target,string path);
#endif
        static void CreateDirectoryLink(string path,string target){
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if(!DirectoryLink(path,target,3))Assert.Ignore("This host cannot create the directory symlink fixture: "+Marshal.GetLastWin32Error());
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
            if(DirectoryLink(target,path)!=0)Assert.Ignore("This host cannot create the directory symlink fixture: "+Marshal.GetLastWin32Error());
#else
            Assert.Ignore("This test host has no directory-link fixture adapter.");
#endif
        }
        [Test]public void LinkedMetadataIsNotFollowedDuringInspectionRemovalOrImport(){
            var before=Inspect();string target=Path.Combine(directory,"attribution-original.txt");byte[] bytes=Encoding.UTF8.GetBytes("Original external attribution");File.WriteAllBytes(target,bytes);File.Delete(Metadata);AssetSidecarTestLinks.Create(Metadata,target);
            try{Assert.Catch<Exception>(()=>Inspect());Assert.Catch<Exception>(()=>Remove(before.Revision).GetAwaiter().GetResult());Assert.Catch<Exception>(()=>library.SaveAsync(asset).GetAwaiter().GetResult());Assert.That(File.ReadAllBytes(target),Is.EqualTo(bytes));Assert.That(File.ReadAllBytes(Payload),Is.EqualTo(asset.Bytes));}
            finally{File.Delete(Metadata);}
        }
    }
}
