// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Creation;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Quest.Imports;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class BundledAvatarTests
    {
        string directory;byte[] bytes;
        [SetUp]public void SetUp(){directory=Path.Combine(Path.GetTempPath(),"MaestroIncludedTests-"+Guid.NewGuid().ToString("N"));bytes=ModelFixture.Mixamo();}
        [TearDown]public void TearDown(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        [TestCase("maximum")][TestCase("multiline")][TestCase("escaped")][TestCase("unicode")]
        public void IncludedAvatarFactTextProjectionBoundsUnsafeDisplayValuesAndSignalsSummaries(string kind)
        {
            string original=kind switch {
                "maximum"=>new string('a',2048),
                "multiline"=>"First author line\r\nSecond\tline\0Full terms retained",
                "escaped"=>string.Concat(Enumerable.Repeat("\\\"",1024)),
                _=>string.Concat(Enumerable.Repeat("Dog \U0001F436 ",300))+"\ud800"
            };
            var source=new JObject {["name"]=original,["attribution"]=original};var before=source.DeepClone();
            string name=AvatarModelCapability.IncludedMetadataText((string)source["name"]),attribution=AvatarModelCapability.IncludedMetadataText((string)source["attribution"],true);
            Assert.That(name,Does.Contain("[shortened]"));Assert.That(attribution,Does.Contain("[summary; full attribution retained]"));
            foreach(string text in new[]{name,attribution}){
                Assert.That(text.Length,Is.LessThanOrEqualTo(128));Assert.That(JsonConvert.ToString(text).Length,Is.LessThanOrEqualTo(128));Assert.That(text.Any(char.IsControl),Is.False);
                for(int i=0;i<text.Length;i++)if(char.IsHighSurrogate(text[i])){
                    Assert.That(i+1,Is.LessThan(text.Length));Assert.That(char.IsLowSurrogate(text[++i]),Is.True);
                }else Assert.That(char.IsLowSurrogate(text[i]),Is.False,"Display text must not contain a split Unicode character");
            }
            var value=new JObject {["modelHash"]=new string('a',64),["name"]=name,["bundled"]=true,["attribution"]=attribution,["walkClipIndex"]=31};
            var definition=BehaviourCatalog.Fact("avatar.included");var typed=ProgramValue.Literal(value,definition.Type);
            Assert.That(definition.Version,Is.EqualTo(1));Assert.That(definition.ValidValue(typed),Is.True);Assert.That(typed.Characters,Is.LessThanOrEqualTo(1024));
            Assert.That(JToken.DeepEquals(source,before),Is.True,"The display projection must not overwrite complete metadata");
        }
        [Test]public void IncludedAvatarFactTextProjectionPreservesOrdinaryMetadataAndCanonicalParserRules()
        {
            const string name="Maestro",attribution="Original project artwork — full use terms";
            Assert.That(AvatarModelCapability.IncludedMetadataText(name),Is.EqualTo(name));Assert.That(AvatarModelCapability.IncludedMetadataText(attribution,true),Is.EqualTo(attribution));
            Assert.That(AvatarModelCapability.IncludedMetadataText(null),Is.Empty);Assert.That(AvatarModelCapability.IncludedMetadataText("",true),Is.Empty);
            var manifest=JObject.Parse(BundledAvatarFixture.Manifest(bytes));manifest["attribution"]="First line\nSecond line";
            Assert.Throws<ModelImportException>(()=>BundledAvatar.FromDirectory(manifest.ToString(),directory),"Display sanitization must not silently broaden the canonical package format");
            Assert.That((string)manifest["attribution"],Is.EqualTo("First line\nSecond line"));
        }
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
            capture.Dispose();using(var first=pending.GetAwaiter().GetResult()){Assert.That(first.Bytes,Is.EqualTo(bytes));Assert.That(retirement.IsCompleted,Is.False,"Returned model owns its source until disposed");}retirement.GetAwaiter().GetResult();Assert.That(File.ReadAllBytes(Path.Combine(models,included.Hash+".glb")),Is.EqualTo(bytes));Assert.That(File.ReadAllText(Path.Combine(models,included.Hash+".txt")),Does.Contain(included.Attribution));
            File.Delete(Path.Combine(directory,"package",BundledAvatar.RelativePath));using var restored=new ModelLibrary(models).ReadAsync(included.Hash).GetAwaiter().GetResult();Assert.That(restored.Hash,Is.EqualTo(included.Hash),"An update or missing original package cannot replace a saved exact model");
        }
        [Test]public void DamagedLocalCopiesAndFullLibrariesArePreservedInsteadOfSilentlyRepairedOrEvicted()
        {
            var included=BundledAvatarFixture.Write(Path.Combine(directory,"package"),bytes);string models=Path.Combine(directory,"models");var library=new ModelLibrary(models,null,included);library.ReadAsync(included.Hash).GetAwaiter().GetResult().Dispose();string path=Path.Combine(models,included.Hash+".glb");
            byte[] damaged=(byte[])bytes.Clone();damaged[^1]^=1;File.WriteAllBytes(path,damaged);Assert.Throws<ModelImportException>(()=>library.ReadAsync(included.Hash).GetAwaiter().GetResult());Assert.That(File.ReadAllBytes(path),Is.EqualTo(damaged));File.Delete(path);
            for(int i=0;i<32;i++)File.WriteAllBytes(Path.Combine(models,i.ToString("x64")+".glb"),bytes);
            Assert.Throws<ModelImportException>(()=>library.ReadAsync(included.Hash).GetAwaiter().GetResult());Assert.That(Directory.GetFiles(models,"*.glb"),Has.Length.EqualTo(32));Assert.That(File.Exists(path),Is.False);
        }
        [Test]public void FreshRecoveryIncludesAnExactPortableAvatarWithNoExternalFileDependency()
        {
            var included=BundledAvatarFixture.Write(Path.Combine(directory,"package"),bytes);var store=new WorkspaceGenerationStore(Path.Combine(directory,"app"));string origin=(string)store.InspectRecovery()["originHash"];
            var preview=store.PrepareFreshRecovery(origin,includedAvatar:included);Assert.That(preview.Receipt.Summary.Models,Is.EqualTo(1));Assert.That(preview.Receipt.Summary.MissingModels,Is.Empty);Assert.That((string)store.InspectRecovery()["originHash"],Is.EqualTo(origin));
            var copy=store.InspectDamagedPreview(preview.Id,preview.Receipt.ManifestHash,origin);Assert.That(copy.Receipt.ManifestHash,Is.EqualTo(preview.Receipt.ManifestHash));
            var snapshot=WorkspaceDefaults.Snapshot(included);Assert.That(JObject.Parse(System.Text.Encoding.UTF8.GetString(snapshot.Documents[RoomStorage.FileName]))["objects"].Single(x=>(string)x["id"]=="maestro")["modelHash"].Value<string>(),Is.EqualTo(included.Hash));
            File.Delete(Path.Combine(directory,"package",BundledAvatar.RelativePath));using var output=new MemoryStream();var receipt=WorkspaceArchive.Write(output,snapshot);Assert.That(receipt.Summary.Models,Is.EqualTo(1));Assert.That(receipt.Summary.MissingModels,Is.Empty);
        }
    }
}
