// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Imports;
using Maestro.Quest.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class BundledMotionsTests
    {
        string directory;byte[] a,b;
        string Package=>Path.Combine(directory,"package");string Library=>Path.Combine(directory,"library");
        [SetUp]public void Setup(){directory=Path.Combine(Path.GetTempPath(),"MIM-"+Guid.NewGuid().ToString("N"));a=ModelFixture.TranslationMotion("LINEAR");b=ModelFixture.TranslationMotion("LINEAR",2);}
        [TearDown]public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        void ExistingEmpty(){Directory.CreateDirectory(Library);File.WriteAllText(Path.Combine(Library,"motions.v2.json"),JsonConvert.SerializeObject(new MotionCatalogue()));}
        [Test]public void DirectoryAndCompressedApkReadsValidateTheExactManifestAndEveryPayload()
        {
            var pack=BundledMotionsFixture.Write(Package,a,b);pack.Verify();string apk=Path.Combine(directory,"test.apk");
            using(var zip=ZipFile.Open(apk,ZipArchiveMode.Create))foreach(var entry in pack.Catalogue.entries){using var target=zip.CreateEntry("assets/"+BundledMotions.RelativeDirectory+entry.hash+".motion",CompressionLevel.Optimal).Open();var bytes=pack.Read(entry.hash);target.Write(bytes,0,bytes.Length);}
            var loaded=BundledMotions.FromApk(BundledMotionsFixture.Manifest(a,b),apk);loaded.Verify();Assert.That(loaded.Hash,Is.EqualTo(pack.Hash));Assert.That(loaded.Count,Is.EqualTo(2));
            var json=JObject.Parse(BundledMotionsFixture.Manifest(a,b));json["catalogue"]["entries"][0]["rigHash"]=new string('f',64);Assert.Throws<ModelImportException>(()=>BundledMotions.FromDirectory(json.ToString(),Package));
            string path=Path.Combine(Package,BundledMotions.RelativeDirectory+pack.Catalogue.entries[0].hash+".motion");var damaged=File.ReadAllBytes(path);damaged[^1]^=1;File.WriteAllBytes(path,damaged);Assert.Throws<ModelImportException>(()=>pack.Verify());
        }
        [Test]public void FreshLibraryUsesStableIdsButExistingLibraryDoesNotSilentlyInstallLaterContent()
        {
            var first=BundledMotionsFixture.Write(Package,a);string id=first.Catalogue.entries[0].id;
            using(var library=new MotionLibrary(Library,includedMotions:first)){library.IncludedInitialization.GetAwaiter().GetResult();Assert.That(library.Find(id),Is.Not.Null);library.UpdateAsync(id,"My favourite",new[]{"mine"},true).GetAwaiter().GetResult();}
            var later=BundledMotionsFixture.Write(Package,a,b);using var reopened=new MotionLibrary(Library,includedMotions:later);Assert.That(reopened.List(),Has.Length.EqualTo(1));Assert.That(reopened.Find(id).name,Is.EqualTo("My favourite"));
            var result=reopened.InstallIncludedAsync(later.Hash).GetAwaiter().GetResult();Assert.That(result.Added,Is.EqualTo(1));Assert.That(result.Preserved,Is.EqualTo(1));Assert.That(reopened.Find(id).favourite,Is.True);Assert.That(reopened.Find(id).tags,Is.EqualTo(new[]{"mine"}));Assert.That(reopened.List(),Has.Length.EqualTo(2));
        }
        [Test]public async Task ExistingImportedIdsAndRemovalChoicesSurviveAddAndExplicitRestoreKeepsTheirMetadata()
        {
            string id;using(var original=new MotionLibrary(Library)){id=original.ImportAsync("mine.glb",a).GetAwaiter().GetResult().Single().id;original.UpdateAsync(id,"Personal name",new[]{"personal"},true).GetAwaiter().GetResult();original.ArchiveAsync(id,true).GetAwaiter().GetResult();await original.RemoveDownloadAsync(id,()=>Task.FromResult<string>(null));}
            var pack=BundledMotionsFixture.Write(Package,a,b);using var library=new MotionLibrary(Library,includedMotions:pack);var add=library.InstallIncludedAsync(pack.Hash).GetAwaiter().GetResult();Assert.That(add.Added,Is.EqualTo(1));Assert.That(library.Inspect(id).removed,Is.True);Assert.That(library.PayloadPresent(id),Is.False);
            Assert.Throws<ModelImportException>(()=>library.InstallIncludedAsync(new string('f',64),id).GetAwaiter().GetResult());var restore=library.InstallIncludedAsync(pack.Hash,id).GetAwaiter().GetResult();Assert.That(restore.Restored,Is.EqualTo(1));var entry=library.Inspect(id);Assert.That(entry.removed,Is.False);Assert.That(entry.archived,Is.True);Assert.That(entry.favourite,Is.True);Assert.That(entry.name,Is.EqualTo("Personal name"));Assert.That(library.Downloaded(id),Is.True);
        }
        [Test]public void CancellationDoesNotPublishAPartialCatalogueAndRetryReusesVerifiedCopies()
        {
            ExistingEmpty();var pack=BundledMotionsFixture.Write(Package,a,b);using var stop=new CancellationTokenSource();bool cancel=true;int reads=0;
            var controlled=new BundledMotions(BundledMotionsFixture.Manifest(a,b),(hash,size)=>{reads++;var data=pack.Read(hash);if(cancel){cancel=false;stop.Cancel();}return data;});
            using var library=new MotionLibrary(Library,includedMotions:controlled);Assert.Catch<OperationCanceledException>(()=>library.InstallIncludedAsync(controlled.Hash,cancellation:stop.Token).GetAwaiter().GetResult());Assert.That(library.List(),Is.Empty);Assert.That(reads,Is.EqualTo(1));
            Assert.That(Directory.GetFiles(Library,"*.motion.glb"),Has.Length.EqualTo(1));var retry=library.InstallIncludedAsync(controlled.Hash).GetAwaiter().GetResult();Assert.That(retry.Added,Is.EqualTo(2));using var reopened=new MotionLibrary(Library);Assert.That(reopened.List().Select(x=>x.id).OrderBy(x=>x),Is.EqualTo(pack.Catalogue.entries.Select(x=>x.id).OrderBy(x=>x)));
        }
        [Test]public void FirstUseHoldsPreservationUntilAcceptedCopyDrainsAndDoesNotCompileResidentClips()
        {
            var pack=BundledMotionsFixture.Write(Package,a);using var entered=new ManualResetEventSlim();using var finish=new ManualResetEventSlim();var gate=new WorkspaceWriteGate();
            var slow=new BundledMotions(BundledMotionsFixture.Manifest(a),(hash,size)=>{entered.Set();if(!finish.Wait(TimeSpan.FromSeconds(10)))throw new IOException("Test stalled");return pack.Read(hash);});
            using var library=new MotionLibrary(Library,gate,slow);try{Assert.That(entered.Wait(TimeSpan.FromSeconds(3)),Is.True);Assert.That(gate.CanFreeze(out _),Is.False);Assert.That(library.InstallingIncluded,Is.True);Assert.That(library.List(),Is.Empty);}finally{finish.Set();}
            library.IncludedInitialization.GetAwaiter().GetResult();Assert.That(gate.CanFreeze(out _),Is.True);Assert.That(library.ResidentClipCount,Is.Zero);Assert.That(library.List(),Has.Length.EqualTo(1));
        }
        [Test]public void RetryingFailedFirstUseClearsItsStaleFailureNotice()
        {
            var pack=BundledMotionsFixture.Write(Package,a);bool fail=true;
            var flaky=new BundledMotions(BundledMotionsFixture.Manifest(a),(hash,size)=>{if(fail)throw new IOException("Unavailable test package");return pack.Read(hash);});
            using var library=new MotionLibrary(Library,includedMotions:flaky);library.IncludedInitialization.GetAwaiter().GetResult();Assert.That(library.Notice,Is.Not.Null);Assert.That(library.List(),Is.Empty);
            fail=false;library.InstallIncludedAsync(flaky.Hash).GetAwaiter().GetResult();Assert.That(library.Notice,Is.Null);Assert.That(library.List(),Has.Length.EqualTo(1));
        }
        [Test]public void FullCatalogueBudgetRefusesNewPackWithoutEvictionOrMetadataChanges()
        {
            var pack=BundledMotionsFixture.Write(Package,a);var catalog=pack.Catalogue;var entry=catalog.entries[0];catalog.entries=Enumerable.Range(0,16).Select(i=>{var copy=entry.Copy();copy.id=Guid.NewGuid().ToString("N");copy.hash=i.ToString("x64");copy.bytes=MotionPack.MaximumBytes;return copy;}).ToArray();Directory.CreateDirectory(Library);string path=Path.Combine(Library,"motions.v2.json"),before=JsonConvert.SerializeObject(catalog);File.WriteAllText(path,before);
            using var library=new MotionLibrary(Library,includedMotions:pack);Assert.That(library.ReadOnly,Is.False);Assert.Throws<ModelImportException>(()=>library.InstallIncludedAsync(pack.Hash).GetAwaiter().GetResult());Assert.That(File.ReadAllText(path),Is.EqualTo(before));Assert.That(library.List(),Has.Length.EqualTo(16));Assert.That(Directory.GetFiles(Library,"*.motion.glb"),Is.Empty);
        }
        [Test]public void FreshRecoveryContainsPortablePayloadsWithTheSameExactIds()
        {
            var pack=BundledMotionsFixture.Write(Package,a,b);var store=new WorkspaceGenerationStore(Path.Combine(directory,"app"));string origin=(string)store.InspectRecovery()["originHash"];var prepared=store.PrepareFreshRecovery(origin,includedMotions:pack);
            Assert.That(prepared.Receipt.Summary.Motions,Is.EqualTo(2));Assert.That(prepared.Receipt.Summary.MissingMotions,Is.Empty);
            var snapshot=WorkspaceDefaults.Snapshot(includedMotions:pack);var catalogue=MotionLibrary.DecodeSnapshot(snapshot.Documents["motions/motions.v2.json"]);Assert.That(catalogue.entries.Select(x=>x.id),Is.EqualTo(pack.Catalogue.entries.Select(x=>x.id)));
            using var output=new MemoryStream();var receipt=WorkspaceArchive.Write(output,snapshot);Assert.That(receipt.Summary.Motions,Is.EqualTo(2));Assert.That(receipt.Summary.MissingMotions,Is.Empty);
        }
    }
}
