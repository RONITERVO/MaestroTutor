// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Maestro.Quest.Persistence;
using Maestro.Quest.Rules;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class MetadataOnlyArchiveTests
    {
        string directory,source;
        [SetUp]public void Setup(){directory=Path.Combine(Path.GetTempPath(),"mma-"+Guid.NewGuid().ToString("N"));source=Path.Combine(directory,"source");Directory.CreateDirectory(source);}
        [TearDown]public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        static byte[] Utf8(string text)=>new UTF8Encoding(false,true).GetBytes(text);
        static byte[] Json(object value)=>Utf8(JsonUtility.ToJson(value));
        static byte[] Payload(string kind)=>kind switch{"models"=>ModelFixture.Create(),"audio"=>WaveAudioTests.File(),"images"=>ImageFiles.Png(),_=>throw new ArgumentException()};
        static string Extension(string kind)=>kind switch{"models"=>".glb","audio"=>".wav","images"=>".image",_=>throw new ArgumentException()};
        static int MetadataLimit(string kind)=>kind=="models"?128*1024:1024;
        static string Relative(string kind,string hash,bool metadata=false)=>kind+"/"+hash+(metadata?".txt":Extension(kind));
        static byte[] BomMetadata(string kind){var encoding=Encoding.Unicode;string header="Café original\r\nArtist attribution — keep every character.\r\n";var body=encoding.GetBytes(header+new string('x',(MetadataLimit(kind)-encoding.GetPreamble().Length-encoding.GetByteCount(header))/2));return encoding.GetPreamble().Concat(body).ToArray();}
        static Dictionary<string,byte[]> Documents(string kind=null,string hash=null,bool referenced=false){
            var room=new RoomDocument {version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}}};
            if(referenced)switch(kind){case "models":room.objects[1].modelHash=hash;break;case "audio":room.audioSources=new[]{new RoomAudioDefinition{id=new string('a',32),name="Retained sound",kind="clip",assetHash=hash,seconds=.1f}};break;case "images":room.appearances=new[]{new RoomAppearance{id=new string('b',32),name="Retained image",style=new(){patternMode="image",imageHash=hash}}};break;}
            return new(){[RoomStorage.FileName]=Json(room),["behaviours.v2.json"]=Json(new RuleDocument()),["controls.v2.json"]=Json(new ControllerPreferences()),["avatar-activities.v2.json"]=Json(new AvatarActivityDocument()),["motions/motions.v2.json"]=Utf8("{\"version\":2,\"entries\":[],\"sources\":[]}")};
        }
        static void Save(string kind,string root,byte[] bytes,string name="Original metadata"){
            string path=Path.Combine(root,kind);
            switch(kind){case "models":using(var asset=ModelLibrary.Inspect(name,bytes))new ModelLibrary(path).SaveAsync(asset).GetAwaiter().GetResult();break;case "audio":new AudioLibrary(path).SaveAsync(AudioLibrary.Inspect(name,bytes)).GetAwaiter().GetResult();break;case "images":new ImageLibrary(path).SaveAsync(ImageLibrary.Inspect(name,bytes)).GetAwaiter().GetResult();break;}
        }
        static void Remove(string kind,string root,string hash){
            Task.Run(async()=>{string path=Path.Combine(root,kind);switch(kind){
                case "models":var model=new ModelLibrary(path);var modelReview=await model.InspectFileAsync(hash);await model.RemoveDownloadAsync(hash,modelReview.Revision,()=>Task.FromResult<string>(null));break;
                case "audio":var sound=new AudioLibrary(path);var soundReview=await sound.InspectFileAsync(hash);await sound.RemoveDownloadAsync(hash,soundReview.Revision,()=>Task.FromResult<string>(null));break;
                case "images":var image=new ImageLibrary(path);var imageReview=await image.InspectFileAsync(hash);await image.RemoveDownloadAsync(hash,imageReview.Revision,()=>Task.FromResult<string>(null));break;
            }}).GetAwaiter().GetResult();
        }
        static WorkspaceLibraryCapture Capture(string kind,string root){
            WorkspaceLibraryCapture capture=null;bool accepted=kind switch{"models"=>new ModelLibrary(Path.Combine(root,kind)).TryCaptureArchive(out capture),"audio"=>new AudioLibrary(Path.Combine(root,kind)).TryCaptureArchive(out capture),"images"=>new ImageLibrary(Path.Combine(root,kind)).TryCaptureArchive(out capture),_=>throw new ArgumentException()};Assert.That(accepted,Is.True);return capture;
        }
        static (byte[] Bytes,WorkspaceArchiveReceipt Receipt) Write(Dictionary<string,byte[]> docs,Dictionary<string,Func<Stream>> assets){using var output=new MemoryStream();var receipt=WorkspaceArchive.Write(output,new WorkspaceArchiveSnapshot(docs,assets));return(output.ToArray(),receipt);}
        (byte[] Bytes,WorkspaceArchiveReceipt Receipt) CaptureArchive(string kind,bool referenced=false){
            using var capture=Capture(kind,source);var docs=Documents(kind,ModelLibrary.Hash(Payload(kind)),referenced);var assets=new Dictionary<string,Func<Stream>>();capture.Collect(docs,assets);return Write(docs,assets);
        }
        static int Count(WorkspaceArchiveSummary summary,string kind)=>kind switch{"models"=>summary.Models,"audio"=>summary.Sounds,"images"=>summary.Images,_=>-1};
        static string[] Missing(WorkspaceArchiveSummary summary,string kind)=>kind switch{"models"=>summary.MissingModels,"audio"=>summary.MissingSounds,"images"=>summary.MissingImages,_=>throw new ArgumentException()};
        static Dictionary<string,byte[]> Entries(byte[] bytes){using var input=new MemoryStream(bytes);using var zip=new ZipArchive(input,ZipArchiveMode.Read);return zip.Entries.ToDictionary(x=>x.FullName,x=>{using var data=new MemoryStream();using var entry=x.Open();entry.CopyTo(data);return data.ToArray();});}
        static byte[] Zip(Dictionary<string,byte[]> entries){using var output=new MemoryStream();using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true))foreach(var pair in entries){using var stream=zip.CreateEntry(pair.Key).Open();stream.Write(pair.Value,0,pair.Value.Length);}return output.ToArray();}
        static byte[] WithVersion(byte[] bytes,int version){var entries=Entries(bytes);var manifest=JObject.Parse(Encoding.UTF8.GetString(entries["manifest.json"]));manifest["version"]=version;entries["manifest.json"]=Utf8(manifest.ToString(Formatting.None));return Zip(entries);}
        static byte[] ForgedArchive(Dictionary<string,byte[]> entries,Action<JArray> change=null){
            var list=new JArray(entries.Select(pair=>new JObject{["path"]=pair.Key,["bytes"]=pair.Value.Length,["sha256"]=ModelLibrary.Hash(pair.Value)}));change?.Invoke(list);var copy=entries.ToDictionary(x=>x.Key,x=>x.Value);copy["manifest.json"]=Utf8(new JObject{["format"]="maestro-native-workspace",["version"]=33,["entries"]=list}.ToString(Formatting.None));return Zip(copy);
        }
        void NoStages()=>Assert.That(Directory.GetDirectories(directory,"workspace-import-*"),Is.Empty);
        [TestCase("models")][TestCase("audio")][TestCase("images")]
        public void CleanupCaptureExportStageAndExactReimportKeepCompleteBomMetadata(string kind){
            byte[] payload=Payload(kind),metadata=BomMetadata(kind);string hash=ModelLibrary.Hash(payload),relative=Relative(kind,hash,true),path=Path.Combine(source,relative);Save(kind,source,payload);File.WriteAllBytes(path,metadata);Remove(kind,source,hash);
            Assert.That(File.Exists(Path.Combine(source,Relative(kind,hash))),Is.False);Assert.That(File.ReadAllBytes(path),Is.EqualTo(metadata));var captured=CaptureArchive(kind);Assert.That(Count(captured.Receipt.Summary,kind),Is.Zero);
            Assert.That((int)JObject.Parse(Encoding.UTF8.GetString(Entries(captured.Bytes)["manifest.json"]))["version"],Is.EqualTo(33));
            using(var staged=WorkspaceArchive.Stage(new MemoryStream(captured.Bytes),directory)){
                Assert.That(staged.Receipt.ManifestHash,Is.EqualTo(captured.Receipt.ManifestHash));Assert.That(staged.Receipt.Summary.Files,Is.EqualTo(WorkspaceArchiveMetadata.Required.Length+1));Assert.That(Count(staged.Receipt.Summary,kind),Is.Zero);Assert.That(Missing(staged.Receipt.Summary,kind),Is.Empty);Assert.That(File.ReadAllBytes(Path.Combine(staged.DirectoryPath,relative)),Is.EqualTo(metadata));
                Save(kind,staged.DirectoryPath,payload,"A different picker name");Assert.That(File.ReadAllBytes(Path.Combine(staged.DirectoryPath,Relative(kind,hash))),Is.EqualTo(payload));Assert.That(File.ReadAllBytes(Path.Combine(staged.DirectoryPath,relative)),Is.EqualTo(metadata));Assert.That(Count(staged.Receipt.Summary,kind),Is.Zero,"The earlier archive receipt remains historical");
            }
            Assert.That(File.ReadAllBytes(path),Is.EqualTo(metadata));Assert.That(File.Exists(Path.Combine(source,Relative(kind,hash))),Is.False);NoStages();
        }
        [TestCase("models")][TestCase("audio")][TestCase("images")]
        public void EmptySidecarRefusesPassiveArchiveAndOnlyVerifiedReimportRepairsIt(string kind){
            byte[] payload=Payload(kind);string hash=ModelLibrary.Hash(payload),relative=Relative(kind,hash,true),path=Path.Combine(source,relative),payloadPath=Path.Combine(source,Relative(kind,hash));
            Save(kind,source,payload);File.WriteAllBytes(path,Array.Empty<byte>());
            Assert.Throws<InvalidDataException>(()=>CaptureArchive(kind));Assert.That(File.ReadAllBytes(path),Is.Empty);Assert.That(File.ReadAllBytes(payloadPath),Is.EqualTo(payload));
            Remove(kind,source,hash);Assert.That(File.Exists(payloadPath),Is.False);Assert.That(File.ReadAllBytes(path),Is.Empty,"Cleanup must not repair attribution as a side effect");
            Assert.Throws<InvalidDataException>(()=>CaptureArchive(kind));Assert.That(File.ReadAllBytes(path),Is.Empty);NoStages();
            Save(kind,source,payload,"Verified reimport metadata");byte[] repaired=File.ReadAllBytes(path);
            Assert.That(repaired.Length,Is.InRange(1,MetadataLimit(kind)));Assert.That(File.ReadAllText(path),Does.Contain("Verified reimport metadata"));Assert.That(File.ReadAllBytes(payloadPath),Is.EqualTo(payload));
            var captured=CaptureArchive(kind);Assert.That(Count(captured.Receipt.Summary,kind),Is.EqualTo(1));
            using(var stage=WorkspaceArchive.Stage(new MemoryStream(captured.Bytes),directory)){
                Assert.That(File.ReadAllBytes(Path.Combine(stage.DirectoryPath,relative)),Is.EqualTo(repaired));Assert.That(File.ReadAllBytes(Path.Combine(stage.DirectoryPath,Relative(kind,hash))),Is.EqualTo(payload));
                Save(kind,stage.DirectoryPath,payload,"Later picker name");Assert.That(File.ReadAllBytes(Path.Combine(stage.DirectoryPath,relative)),Is.EqualTo(repaired),"Once healthy, attribution is preserved on exact reimport");
            }
            Assert.That(File.ReadAllBytes(path),Is.EqualTo(repaired));NoStages();
        }
        [TestCase("models")][TestCase("audio")][TestCase("images")]
        public void PairedBomMetadataRoundTripsWithoutTextConversion(string kind){
            byte[] payload=Payload(kind),metadata=BomMetadata(kind);string hash=ModelLibrary.Hash(payload),relative=Relative(kind,hash,true);Save(kind,source,payload);File.WriteAllBytes(Path.Combine(source,relative),metadata);var captured=CaptureArchive(kind,true);
            using var staged=WorkspaceArchive.Stage(new MemoryStream(captured.Bytes),directory);Assert.That(Count(staged.Receipt.Summary,kind),Is.EqualTo(1));Assert.That(Missing(staged.Receipt.Summary,kind),Is.Empty);Assert.That(File.ReadAllBytes(Path.Combine(staged.DirectoryPath,relative)),Is.EqualTo(metadata));Assert.That(File.ReadAllBytes(Path.Combine(staged.DirectoryPath,Relative(kind,hash))),Is.EqualTo(payload));
        }
        [TestCase("models")][TestCase("audio")][TestCase("images")]
        public void MetadataOnlyIdentityDoesNotMasqueradeAsAvailableReferencedPayload(string kind){
            byte[] payload=Payload(kind),metadata=BomMetadata(kind);string hash=ModelLibrary.Hash(payload);Save(kind,source,payload);File.WriteAllBytes(Path.Combine(source,Relative(kind,hash,true)),metadata);Remove(kind,source,hash);var captured=CaptureArchive(kind,true);
            using var staged=WorkspaceArchive.Stage(new MemoryStream(captured.Bytes),directory);Assert.That(Count(staged.Receipt.Summary,kind),Is.Zero);Assert.That(Missing(staged.Receipt.Summary,kind),Is.EqualTo(new[]{hash}));Assert.That(File.ReadAllBytes(Path.Combine(staged.DirectoryPath,Relative(kind,hash,true))),Is.EqualTo(metadata));
        }
        [TestCase("models")][TestCase("audio")][TestCase("images")]
        public void RetainedExportAndDamagedSelectionRecoveryPreserveMetadataWithoutPayload(string kind){
            byte[] payload=Payload(kind),metadata=BomMetadata(kind);string hash=ModelLibrary.Hash(payload),relative=Relative(kind,hash,true);Save(kind,source,payload);File.WriteAllBytes(Path.Combine(source,relative),metadata);Remove(kind,source,hash);var captured=CaptureArchive(kind,true);
            string area=Path.Combine(directory,"store");var store=new WorkspaceGenerationStore(area);var prepared=store.Prepare(new MemoryStream(captured.Bytes));string data=Path.Combine(area,"workspace-generations.v1","generations",prepared.Id,"data");
            string origin=(string)store.InspectRetention()["originHash"];using(var export=store.ExportRetained(origin,prepared.Id,prepared.Receipt.ManifestHash,"original",Path.Combine(directory,"cache"))){
                Assert.That(export.ExcludedFiles,Is.Zero);Assert.That(Count(export.Receipt.Summary,kind),Is.Zero);Assert.That(Missing(export.Receipt.Summary,kind),Is.EqualTo(new[]{hash}));using var input=File.OpenRead(export.Path);using var staged=WorkspaceArchive.Stage(input,directory);Assert.That(File.ReadAllBytes(Path.Combine(staged.DirectoryPath,relative)),Is.EqualTo(metadata));
            }
            string pointer=Path.Combine(area,"workspace-generations.v1","current.v1.json");byte[] damaged={0xff,0xfe,123,0};File.WriteAllBytes(pointer,damaged);File.WriteAllBytes(pointer+".previous",Array.Empty<byte>());string recoveryOrigin=(string)store.InspectRecovery()["originHash"];
            var preview=store.PrepareDamagedRecovery(recoveryOrigin,prepared.Id,prepared.Receipt.ManifestHash);Assert.That(Count(preview.Receipt.Summary,kind),Is.Zero);Assert.That(Missing(preview.Receipt.Summary,kind),Is.EqualTo(new[]{hash}));string clone=Path.Combine(area,"workspace-generations.v1","generations",preview.Id,"data");Assert.That(File.ReadAllBytes(Path.Combine(clone,relative)),Is.EqualTo(metadata));Assert.That(File.Exists(Path.Combine(clone,Relative(kind,hash))),Is.False);
            Assert.That(store.InspectDamagedPreview(preview.Id,preview.Receipt.ManifestHash,recoveryOrigin).Receipt.ManifestHash,Is.EqualTo(preview.Receipt.ManifestHash));Assert.That(File.ReadAllBytes(pointer),Is.EqualTo(damaged));Assert.That(File.ReadAllBytes(Path.Combine(data,relative)),Is.EqualTo(metadata));store.DiscardDamagedPreview(preview.Id,preview.Receipt.ManifestHash);NoStages();
        }
        [TestCase("models")][TestCase("audio")][TestCase("images")]
        public void Version32PairedArchiveRemainsReadableButOrphansAndFutureVersionRefuse(string kind){
            byte[] payload=Payload(kind);string hash=ModelLibrary.Hash(payload),relative=Relative(kind,hash,true);Save(kind,source,payload);byte[] metadata=File.ReadAllBytes(Path.Combine(source,relative));var paired=CaptureArchive(kind);
            byte[] old=WithVersion(paired.Bytes,32);using(var staged=WorkspaceArchive.Stage(new MemoryStream(old),directory)){Assert.That(Count(staged.Receipt.Summary,kind),Is.EqualTo(1));Assert.That(File.ReadAllBytes(Path.Combine(staged.DirectoryPath,relative)),Is.EqualTo(metadata));Assert.That(WorkspaceArchive.VerifyPreparedDirectory(staged.DirectoryPath,staged.ManifestBytes).ManifestHash,Is.EqualTo(staged.Receipt.ManifestHash));}
            var store=new WorkspaceGenerationStore(Path.Combine(directory,"old-store"));var prepared=store.Prepare(new MemoryStream(old));Assert.That(store.InspectPrepared(prepared.Id,prepared.Receipt.ManifestHash).Receipt.ManifestHash,Is.EqualTo(prepared.Receipt.ManifestHash));
            Remove(kind,source,hash);var orphan=CaptureArchive(kind);Assert.Throws<InvalidDataException>(()=>WorkspaceArchive.Stage(new MemoryStream(WithVersion(orphan.Bytes,32)),directory));Assert.Throws<InvalidDataException>(()=>WorkspaceArchive.Stage(new MemoryStream(WithVersion(orphan.Bytes,34)),directory));Assert.That(File.ReadAllBytes(Path.Combine(source,relative)),Is.EqualTo(metadata));NoStages();
        }
        [TestCase("models")][TestCase("audio")][TestCase("images")]
        public void MalformedMetadataNamesCannotBeSilentlyOmittedOrEscapeArchive(string kind){
            Directory.CreateDirectory(Path.Combine(source,kind));string path=Path.Combine(source,kind,"not-an-exact-hash.txt");byte[] original=Utf8("Preserve malformed metadata evidence");File.WriteAllBytes(path,original);Assert.Throws<InvalidDataException>(()=>CaptureArchive(kind));Assert.That(File.ReadAllBytes(path),Is.EqualTo(original));
            var docs=Documents();docs[kind+"/../escape.txt"]=original;Assert.Throws<InvalidDataException>(()=>WorkspaceArchive.Stage(new MemoryStream(ForgedArchive(docs)),directory));Assert.That(File.Exists(Path.Combine(directory,"escape.txt")),Is.False);NoStages();
        }
        [TestCase("models")][TestCase("audio")][TestCase("images")]
        public void OversizedAndUndecodableMetadataFailWithoutTruncatingOrRepairingSource(string kind){
            string hash=new string('a',64),relative=Relative(kind,hash,true),path=Path.Combine(source,relative);Directory.CreateDirectory(Path.GetDirectoryName(path));
            foreach(byte[] invalid in new[]{new byte[MetadataLimit(kind)+1],new byte[]{0xff,0xf0,0xff}}){
                File.WriteAllBytes(path,invalid);Assert.That(()=>CaptureArchive(kind),Throws.Exception);Assert.That(File.ReadAllBytes(path),Is.EqualTo(invalid));var docs=Documents();docs[relative]=invalid;Assert.That(()=>WorkspaceArchive.Stage(new MemoryStream(ForgedArchive(docs)),directory),Throws.Exception);Assert.That(File.ReadAllBytes(path),Is.EqualTo(invalid));NoStages();
            }
        }
        [TestCase("models")][TestCase("audio")][TestCase("images")]
        public void MetadataPathOccupiedByDirectoryIsPreservedAndRefusesCapture(string kind){
            string path=Path.Combine(source,Relative(kind,new string('a',64),true));Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"keep.txt"),"Private obstruction evidence");Assert.Throws<InvalidDataException>(()=>CaptureArchive(kind));Assert.That(File.ReadAllText(Path.Combine(path,"keep.txt")),Is.EqualTo("Private obstruction evidence"));NoStages();
        }
        [TestCase("models")][TestCase("audio")][TestCase("images")]
        public void LinkedMetadataCaptureRefusesAndPreservesExternalAttribution(string kind){
            string hash=new string('a',64),path=Path.Combine(source,Relative(kind,hash,true)),target=Path.Combine(directory,"external-attribution.txt");Directory.CreateDirectory(Path.GetDirectoryName(path));byte[] original=Utf8("External artist attribution");File.WriteAllBytes(target,original);AssetSidecarTestLinks.Create(path,target);
            try{Assert.Throws<InvalidDataException>(()=>CaptureArchive(kind));Assert.That(File.ReadAllBytes(target),Is.EqualTo(original));}finally{File.Delete(path);}NoStages();
        }
        [Test]public void MetadataOnlyEntriesUseTheGlobalArchiveBudgetWithoutClaimingModelSlots(){
            var docs=Documents();for(int i=0;docs.Count<WorkspaceArchive.MaximumEntries;i++)docs["models/"+i.ToString("x64")+".txt"]=Utf8("Retained attribution");var valid=Write(docs,new());Assert.That(valid.Receipt.Summary.Files,Is.EqualTo(WorkspaceArchive.MaximumEntries));Assert.That(valid.Receipt.Summary.Models,Is.Zero);
            using(var stage=WorkspaceArchive.Stage(new MemoryStream(valid.Bytes),directory))Assert.That(stage.Receipt.Summary.Files,Is.EqualTo(WorkspaceArchive.MaximumEntries));docs["audio/"+new string('f',64)+".txt"]=Utf8("One too many");Assert.Throws<InvalidDataException>(()=>new WorkspaceArchiveSnapshot(docs,new Dictionary<string,Func<Stream>>()));Assert.Throws<InvalidDataException>(()=>WorkspaceArchive.Stage(new MemoryStream(ForgedArchive(docs)),directory));NoStages();
        }
        [Test]public void OversizedLiveDirectoryCannotHideEntriesBehindIgnoredExtensions(){
            string models=Path.Combine(source,"models");Directory.CreateDirectory(models);File.WriteAllText(Path.Combine(models,new string('a',64)+".txt"),"Attribution");for(int i=0;i<WorkspaceArchive.MaximumEntries;i++)File.WriteAllText(Path.Combine(models,i.ToString("x64")+".pending"),"Pending evidence "+i);Assert.Throws<InvalidDataException>(()=>CaptureArchive("models"));Assert.That(Directory.GetFiles(models),Has.Length.EqualTo(WorkspaceArchive.MaximumEntries+1));Assert.That(File.ReadAllText(Path.Combine(models,(WorkspaceArchive.MaximumEntries-1).ToString("x64")+".pending")),Is.EqualTo("Pending evidence "+(WorkspaceArchive.MaximumEntries-1)));Assert.That(File.ReadAllText(Path.Combine(models,new string('a',64)+".txt")),Is.EqualTo("Attribution"));NoStages();
        }
        [Test]public void MetadataCannotPushOtherwiseBoundedManifestPastGlobalByteBudget(){
            var docs=Documents();docs["models/"+new string('a',64)+".txt"]=Utf8("Metadata counts too");for(int i=0;i<4;i++){docs["models/"+i.ToString("x64")+".glb"]=new byte[]{0};docs["audio/"+i.ToString("x64")+".wav"]=new byte[]{0};}for(int i=0;i<8;i++)docs["images/"+i.ToString("x64")+".image"]=new byte[]{0};
            var bytes=ForgedArchive(docs,list=>{foreach(var item in list){string path=(string)item["path"];if(WorkspaceArchiveMetadata.IsAsset(path))item["bytes"]=WorkspaceArchiveMetadata.Limit(path);}});var error=Assert.Throws<InvalidDataException>(()=>WorkspaceArchive.Stage(new MemoryStream(bytes),directory));Assert.That(error.Message,Does.Contain("content budget"));NoStages();
        }
    }
}
