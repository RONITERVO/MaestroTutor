// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Threading;
using Maestro.Quest.Imports;
using Maestro.Quest.Persistence;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class ModelLibraryDiscoveryTests
    {
        string directory;ModelLibrary library;
        [SetUp]public void SetUp(){directory=Path.Combine(Path.GetTempPath(),"MaestroModelDiscovery-"+Guid.NewGuid().ToString("N"));library=new ModelLibrary(directory);}
        [TearDown]public void TearDown(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        [Test]public void ListsExactSortedIdentitiesWithBoundedNamesWithoutLoadingOrAlteringModels()
        {
            Assert.That(library.ListAsync().GetAwaiter().GetResult(),Is.Empty);
            var assets=Enumerable.Range(0,9).Select(i=>ModelLibrary.Inspect("<Chosen>\n"+i+new string('x',110),ModelFixture.Mixamo(json=>json["animations"][0]["name"]="Clip "+i))).ToArray();
            foreach(var asset in assets)library.SaveAsync(asset).GetAwaiter().GetResult();
            var entries=library.ListAsync().GetAwaiter().GetResult();Assert.That(entries.Select(e=>e.Hash),Is.EqualTo(assets.Select(a=>a.Hash).OrderBy(h=>h,StringComparer.Ordinal)));Assert.That(entries.All(e=>e.Name.Length==100&&!e.Name.Contains('<')&&!e.Name.Contains('\n')),Is.True);
            foreach(var entry in entries){Assert.That((library.ReadAsync(entry.Hash).GetAwaiter().GetResult()).Hash,Is.EqualTo(entry.Hash));Assert.That(entry.Bytes,Is.EqualTo(assets.Single(a=>a.Hash==entry.Hash).Bytes.Length));}
        }
        [Test]public void InvalidInventoryFailsInsteadOfInventingAnEmptyLibrary()
        {
            var asset=ModelLibrary.Inspect("Model",ModelFixture.Create());library.SaveAsync(asset).GetAwaiter().GetResult();File.WriteAllText(Path.Combine(directory,asset.Hash+".txt"),new string('x',128*1024+1));
            Assert.Throws<ModelImportException>(()=>library.ListAsync().GetAwaiter().GetResult());File.Delete(Path.Combine(directory,asset.Hash+".txt"));Assert.That((library.ListAsync().GetAwaiter().GetResult()).Single().Name,Is.EqualTo(asset.Hash));
            File.WriteAllBytes(Path.Combine(directory,"invalid.glb"),asset.Bytes);Assert.Throws<ModelImportException>(()=>library.ListAsync().GetAwaiter().GetResult());
        }
        [Test]public void PartialDiscoveryKeepsHealthyMetadataWithoutLoadingOrChangingAnyModel()
        {
            var asset=ModelLibrary.Inspect("Healthy",ModelFixture.Create());library.SaveAsync(asset).GetAwaiter().GetResult();string invalid=Path.Combine(directory,"invalid.glb");File.WriteAllBytes(invalid,new byte[28]);
            string broken=new string('b',64);File.WriteAllBytes(Path.Combine(directory,broken+".glb"),new byte[28]);File.WriteAllBytes(Path.Combine(directory,broken+".txt"),new byte[128*1024+1]);
            var before=Directory.GetFiles(directory).ToDictionary(Path.GetFileName,File.ReadAllBytes);
            using(var locked=new FileStream(Path.Combine(directory,asset.Hash+".glb"),FileMode.Open,FileAccess.Read,FileShare.None)){
                var result=library.ScanAsync().GetAwaiter().GetResult();Assert.That(result.Ready&&result.EnumerationComplete,Is.True);Assert.That(result.Complete,Is.False);Assert.That(result.IssueCount,Is.EqualTo(2));Assert.That(result.Entries.Select(e=>e.Hash),Is.EqualTo(new[]{asset.Hash}));Assert.That(result.Entries[0].Name,Is.EqualTo("Healthy"));Assert.That(result.Entries[0].Bytes,Is.EqualTo(asset.Bytes.Length));
            }
            foreach(var file in before)Assert.That(File.ReadAllBytes(Path.Combine(directory,file.Key)),Is.EqualTo(file.Value));Assert.That(Directory.GetFiles(directory).Length,Is.EqualTo(before.Count));Assert.Throws<ModelImportException>(()=>library.ListAsync().GetAwaiter().GetResult());
        }
        [Test]public void MetadataDiscoveryDoesNotPromiseMatchingPayloadHashOrHumanoidValidity()
        {
            Directory.CreateDirectory(directory);string hash=new string('c',64);File.WriteAllBytes(Path.Combine(directory,hash+".glb"),new byte[28]);var result=library.ScanAsync().GetAwaiter().GetResult();Assert.That(result.Complete,Is.True);Assert.That(result.Entries.Single().Hash,Is.EqualTo(hash));Assert.Throws<ModelImportException>(()=>library.ReadAsync(hash).GetAwaiter().GetResult());
        }
        [Test]public void CancelledDiscoveryWaitingForArchiveReleasesItsWorkspaceLease()
        {
            var gate=new WorkspaceWriteGate();library=new ModelLibrary(directory,gate);Assert.That(library.TryCaptureArchive(out var capture),Is.True);using var stop=new CancellationTokenSource();var pending=library.ScanAsync(stop.Token);
            try{Assert.That(pending.IsCompleted,Is.False);stop.Cancel();Assert.That(SpinWait.SpinUntil(()=>pending.IsCompleted,3000),Is.True,"Cancellation must finish while archive ownership remains held");Assert.Catch<OperationCanceledException>(()=>pending.GetAwaiter().GetResult());Assert.That(gate.CanFreeze(out _),Is.True);}
            finally{capture.Dispose();}Assert.That(Directory.Exists(directory),Is.False);
        }
    }
}
