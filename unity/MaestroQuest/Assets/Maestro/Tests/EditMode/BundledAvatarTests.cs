// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Quest.Imports;
using Maestro.Quest.Persistence;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class BundledAvatarTests
    {
        string directory;byte[] bytes;
        [SetUp]public void SetUp(){directory=Path.Combine(Path.GetTempPath(),"MaestroIncludedTests-"+Guid.NewGuid().ToString("N"));bytes=ModelFixture.Mixamo();}
        [TearDown]public void TearDown(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        [Test]public void DesktopAndCompressedApkReadTheSameBoundedModelAndRejectChangedContent()
        {
            var included=BundledAvatarFixture.Write(directory,bytes);Assert.That(included.Read().Bytes,Is.EqualTo(bytes));string apk=Path.Combine(directory,"app.apk");
            void Zip(byte[] data,bool duplicate=false){using var file=File.Create(apk);using var zip=new ZipArchive(file,ZipArchiveMode.Create);for(int i=0;i<(duplicate?2:1);i++){var entry=zip.CreateEntry("assets/"+BundledAvatar.RelativePath,CompressionLevel.Optimal);using var output=entry.Open();output.Write(data);}}
            Zip(bytes);var packaged=BundledAvatar.FromApk(BundledAvatarFixture.Manifest(bytes),apk);Assert.That(packaged.Read().Hash,Is.EqualTo(included.Hash));
            Zip(bytes,true);Assert.Throws<ModelImportException>(()=>packaged.Read());Zip(bytes.Take(bytes.Length-1).ToArray());Assert.Throws<ModelImportException>(()=>packaged.Read());
            var changed=(byte[])bytes.Clone();changed[^1]^=1;Zip(changed);Assert.Throws<ModelImportException>(()=>packaged.Read());File.WriteAllBytes(Path.Combine(directory,BundledAvatar.RelativePath),changed);Assert.Throws<ModelImportException>(()=>included.Read());
            var metadata=JObject.Parse(BundledAvatarFixture.Manifest(bytes));metadata["path"]="../outside.glb";Assert.Throws<ModelImportException>(()=>BundledAvatar.FromDirectory(metadata.ToString(),directory));metadata.Remove("path");metadata["bytes"]=ModelInspection.MaximumBytes+1;Assert.Throws<ModelImportException>(()=>BundledAvatar.FromDirectory(metadata.ToString(),directory));
        }
        [Test]public void FirstUseIsAnAcceptedBoundedLibraryWriteThatRetirementDrainsAndExportsPreserve()
        {
            var included=BundledAvatarFixture.Write(Path.Combine(directory,"package"),bytes);var gate=new WorkspaceWriteGate();string models=Path.Combine(directory,"models");var library=new ModelLibrary(models,gate,included);
            Assert.That(library.TryCaptureArchive(out var capture),Is.True);var pending=library.ReadAsync(included.Hash);Assert.That(gate.CanFreeze(out _),Is.False);var retirement=gate.Retire();Assert.That(retirement.IsCompleted,Is.False);
            capture.Dispose();Assert.That(pending.GetAwaiter().GetResult().Bytes,Is.EqualTo(bytes));retirement.GetAwaiter().GetResult();Assert.That(File.ReadAllBytes(Path.Combine(models,included.Hash+".glb")),Is.EqualTo(bytes));Assert.That(File.ReadAllText(Path.Combine(models,included.Hash+".txt")),Does.Contain(included.Attribution));
            File.Delete(Path.Combine(directory,"package",BundledAvatar.RelativePath));Assert.That(new ModelLibrary(models).ReadAsync(included.Hash).GetAwaiter().GetResult().Hash,Is.EqualTo(included.Hash),"An update or missing original package cannot replace a saved exact model");
        }
        [Test]public void DamagedLocalCopiesAndFullLibrariesArePreservedInsteadOfSilentlyRepairedOrEvicted()
        {
            var included=BundledAvatarFixture.Write(Path.Combine(directory,"package"),bytes);string models=Path.Combine(directory,"models");var library=new ModelLibrary(models,null,included);library.ReadAsync(included.Hash).GetAwaiter().GetResult();string path=Path.Combine(models,included.Hash+".glb");
            byte[] damaged=(byte[])bytes.Clone();damaged[^1]^=1;File.WriteAllBytes(path,damaged);Assert.Throws<ModelImportException>(()=>library.ReadAsync(included.Hash).GetAwaiter().GetResult());Assert.That(File.ReadAllBytes(path),Is.EqualTo(damaged));File.Delete(path);
            for(int i=0;i<32;i++)File.WriteAllBytes(Path.Combine(models,i.ToString("x64")+".glb"),bytes);
            Assert.Throws<ModelImportException>(()=>library.ReadAsync(included.Hash).GetAwaiter().GetResult());Assert.That(Directory.GetFiles(models,"*.glb"),Has.Length.EqualTo(32));Assert.That(File.Exists(path),Is.False);
        }
        [Test]public void FreshRecoveryIncludesAnExactPortableAvatarWithNoExternalFileDependency()
        {
            var included=BundledAvatarFixture.Write(Path.Combine(directory,"package"),bytes);var store=new WorkspaceGenerationStore(Path.Combine(directory,"app"));string origin=(string)store.InspectRecovery()["originHash"];
            var preview=store.PrepareFreshRecovery(origin,includedAvatar:included);Assert.That(preview.Receipt.Summary.Models,Is.EqualTo(1));Assert.That(preview.Receipt.Summary.MissingModels,Is.Empty);Assert.That((string)store.InspectRecovery()["originHash"],Is.EqualTo(origin));
            var copy=store.InspectDamagedPreview(preview.Id,preview.Receipt.ManifestHash,origin);Assert.That(copy.Receipt.ManifestHash,Is.EqualTo(preview.Receipt.ManifestHash));
            var snapshot=WorkspaceDefaults.Snapshot(included);Assert.That(JObject.Parse(System.Text.Encoding.UTF8.GetString(snapshot.Documents["room.v10.json"]))["objects"].Single(x=>(string)x["id"]=="maestro")["modelHash"].Value<string>(),Is.EqualTo(included.Hash));
            File.Delete(Path.Combine(directory,"package",BundledAvatar.RelativePath));using var output=new MemoryStream();var receipt=WorkspaceArchive.Write(output,snapshot);Assert.That(receipt.Summary.Models,Is.EqualTo(1));Assert.That(receipt.Summary.MissingModels,Is.Empty);
        }
    }
}
