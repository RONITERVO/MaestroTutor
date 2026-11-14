// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
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
    public sealed class StaticSupportArchiveTests
    {
        string directory;
        [SetUp] public void Setup(){directory=Path.Combine(Path.GetTempPath(),"MaestroSupportArchive-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);}
        [TearDown] public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        static byte[] Utf8(string value)=>new UTF8Encoding(false,true).GetBytes(value);
        static byte[] Json(object value)=>Utf8(JsonUtility.ToJson(value));
        static string Legacy=>WorkspaceArchiveMetadata.LegacyRoomFileName;
        static Dictionary<string,byte[]> Documents(bool legacy=false,bool support=false)
        {
            var room=new RoomDocument{version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},new RoomObjectData{id=new string('a',32),kind=RoomObjectKind.Block,physics=ItemPhysics.Fixed,staticSupport=new(){enabled=support}}}};
            var wire=JObject.Parse(JsonUtility.ToJson(room));if(legacy){wire["version"]=46;foreach(var item in wire["objects"].Cast<JObject>())item.Remove("staticSupport");}
            return new(){[legacy?Legacy:RoomStorage.FileName]=Utf8(wire.ToString(Formatting.Indented)),["behaviours.v2.json"]=Json(new RuleDocument()),["controls.v2.json"]=Json(new ControllerPreferences()),["avatar-activities.v2.json"]=Json(new AvatarActivityDocument()),["motions/motions.v2.json"]=Utf8("{\"version\":2,\"entries\":[],\"sources\":[]}")};
        }
        static void AddHistoricalModel(Dictionary<string,byte[]> docs){var payload=ModelFixture.Create();string hash=ModelLibrary.Hash(payload);docs["models/"+hash+".glb"]=payload;docs["models/"+hash+".txt"]=Utf8("Original Café attribution\r\nPreserve every byte");}
        // Fixture writer deliberately bypasses snapshot validation to exercise the untrusted reader.
        static byte[] Archive(Dictionary<string,byte[]> files,int version,Action<JObject> corruptManifest=null)
        {
            var manifest=new JObject{["format"]="maestro-native-workspace",["version"]=version,["entries"]=new JArray(files.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>new JObject{["path"]=p.Key,["bytes"]=p.Value.Length,["sha256"]=ModelLibrary.Hash(p.Value)}))};corruptManifest?.Invoke(manifest);
            using var output=new MemoryStream();using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true)){
                void Write(string name,byte[] bytes){using var stream=zip.CreateEntry(name).Open();stream.Write(bytes,0,bytes.Length);}
                Write("manifest.json",Utf8(manifest.ToString(Formatting.Indented)));foreach(var entry in files)Write(entry.Key,entry.Value);
            }return output.ToArray();
        }
        static Dictionary<string,byte[]> Entries(string path){using var input=File.OpenRead(path);using var zip=new ZipArchive(input,ZipArchiveMode.Read);return zip.Entries.ToDictionary(e=>e.FullName,e=>{using var output=new MemoryStream();using var stream=e.Open();stream.CopyTo(output);return output.ToArray();});}
        [TestCase(32)] [TestCase(33)] public void HistoricalArchivePreservesOriginalRoomAndManifestBytesThroughStageVerification(int version)
        {
            var docs=Documents(true);AddHistoricalModel(docs);byte[] bytes=Archive(docs,version);
            using var stage=WorkspaceArchive.Stage(new MemoryStream(bytes),directory);var manifest=stage.ManifestBytes;var receipt=WorkspaceArchive.VerifyPreparedDirectory(stage.DirectoryPath,manifest);
            Assert.That(receipt.ManifestHash,Is.EqualTo(ModelLibrary.Hash(manifest)));Assert.That(receipt.ManifestHash,Is.EqualTo(stage.Receipt.ManifestHash));
            foreach(var entry in docs)Assert.That(File.ReadAllBytes(Path.Combine(stage.DirectoryPath,entry.Key)),Is.EqualTo(entry.Value),entry.Key);
            var loaded=new RoomStorage(stage.DirectoryPath).Load(out var error);Assert.That(loaded,Is.Not.Null,error);Assert.That(loaded.objects.All(o=>o.staticSupport.Default),Is.True);Assert.That(File.Exists(Path.Combine(stage.DirectoryPath,RoomStorage.FileName)),Is.False);
            Assert.That(WorkspaceArchive.VerifyPreparedDirectory(stage.DirectoryPath,manifest).ManifestHash,Is.EqualTo(receipt.ManifestHash));Assert.That(stage.ManifestBytes,Is.EqualTo(manifest));
        }
        [TestCase(32)] [TestCase(33)] public void HistoricalRetainedExportUsesNewManifestWithoutRewritingLegacyDocuments(int version)
        {
            var docs=Documents(true);AddHistoricalModel(docs);var store=new WorkspaceGenerationStore(Path.Combine(directory,"store"));var prepared=store.Prepare(new MemoryStream(Archive(docs,version)));string data=Path.Combine(directory,"store","workspace-generations.v1","generations",prepared.Id,"data");
            using var export=store.ExportRetained((string)store.InspectRetention()["originHash"],prepared.Id,prepared.Receipt.ManifestHash,"original",Path.Combine(directory,"cache"));
            var entries=Entries(export.Path);Assert.That((int)JObject.Parse(Encoding.UTF8.GetString(entries["manifest.json"]))["version"],Is.EqualTo(WorkspaceArchive.FormatVersion));Assert.That(entries.ContainsKey(Legacy),Is.True);Assert.That(entries.ContainsKey(RoomStorage.FileName),Is.False);Assert.That(export.ExcludedFiles,Is.Zero);
            using var input=File.OpenRead(export.Path);using var stage=WorkspaceArchive.Stage(input,directory);Assert.That(stage.Receipt.ManifestHash,Is.EqualTo(export.Receipt.ManifestHash));
            foreach(var entry in docs){Assert.That(entries[entry.Key],Is.EqualTo(entry.Value));Assert.That(File.ReadAllBytes(Path.Combine(data,entry.Key)),Is.EqualTo(entry.Value));Assert.That(File.ReadAllBytes(Path.Combine(stage.DirectoryPath,entry.Key)),Is.EqualTo(entry.Value));}
            Assert.That(store.InspectPrepared(prepared.Id,prepared.Receipt.ManifestHash).Receipt.ManifestHash,Is.EqualTo(prepared.Receipt.ManifestHash),"Original archived receipt remains verifiable");
        }
        [Test] public void CurrentArchiveRoundTripsSupportAndMetadataWithoutNormalizingBytes()
        {
            var docs=Documents(support:true);string metadata="models/"+new string('c',64)+".txt";docs[metadata]=Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("Original attribution \r\nCafé")).ToArray();
            using var output=new MemoryStream();var receipt=WorkspaceArchive.Write(output,new WorkspaceArchiveSnapshot(docs,new Dictionary<string,Func<Stream>>()));
            using var stage=WorkspaceArchive.Stage(new MemoryStream(output.ToArray()),directory);Assert.That(stage.Receipt.ManifestHash,Is.EqualTo(receipt.ManifestHash));var loaded=new RoomStorage(stage.DirectoryPath).Load(out var error);Assert.That(loaded,Is.Not.Null,error);Assert.That(loaded.objects[2].staticSupport.enabled,Is.True);
            foreach(var entry in docs)Assert.That(File.ReadAllBytes(Path.Combine(stage.DirectoryPath,entry.Key)),Is.EqualTo(entry.Value));Assert.That(WorkspaceArchive.VerifyPreparedDirectory(stage.DirectoryPath,stage.ManifestBytes).ManifestHash,Is.EqualTo(receipt.ManifestHash));
        }
        [TestCase(32)] [TestCase(33)] public void OldArchiveFormatCannotClaimCurrentRoom(int version)=>Assert.Throws<InvalidDataException>(()=>WorkspaceArchive.Stage(new MemoryStream(Archive(Documents(support:true),version)),directory));
        [Test] public void ArchiveRefusesBothRoomPrimariesUnknownNewerRoomAndMalformedCurrentSupport()
        {
            var docs=Documents(true);docs[RoomStorage.FileName]=Documents(support:true)[RoomStorage.FileName];Assert.Throws<InvalidDataException>(()=>WorkspaceArchive.Stage(new MemoryStream(Archive(docs,34)),directory));
            docs=Documents();docs["room.v48.json"]=docs[RoomStorage.FileName];docs.Remove(RoomStorage.FileName);Assert.Throws<InvalidDataException>(()=>WorkspaceArchive.Stage(new MemoryStream(Archive(docs,34)),directory));
            docs=Documents();var bad=JObject.Parse(Encoding.UTF8.GetString(docs[RoomStorage.FileName]));((JObject)bad["objects"][2]).Remove("staticSupport");docs[RoomStorage.FileName]=Utf8(bad.ToString());Assert.Throws<InvalidDataException>(()=>WorkspaceArchive.Stage(new MemoryStream(Archive(docs,34)),directory));
            Assert.That(Directory.GetDirectories(directory,"workspace-import-*"),Is.Empty);
        }
        [Test] public void RetainedCaptureSelectsExistingCurrentPrimaryAndNeverFallsBackAfterDamage()
        {
            var old=Documents(true);var store=new WorkspaceGenerationStore(Path.Combine(directory,"store"));var prepared=store.Prepare(new MemoryStream(Archive(old,33)));string data=Path.Combine(directory,"store","workspace-generations.v1","generations",prepared.Id,"data"),current=Path.Combine(data,RoomStorage.FileName);var fresh=Documents(support:true);File.WriteAllBytes(current,fresh[RoomStorage.FileName]);
            string Origin()=>(string)store.InspectRetention()["originHash"];
            using(var export=store.ExportRetained(Origin(),prepared.Id,prepared.Receipt.ManifestHash,"original",Path.Combine(directory,"cache"))){var entries=Entries(export.Path);Assert.That(entries.ContainsKey(Legacy),Is.False);Assert.That(entries[RoomStorage.FileName],Is.EqualTo(fresh[RoomStorage.FileName]));Assert.That(export.ExcludedFiles,Is.EqualTo(1));using var input=File.OpenRead(export.Path);using var stage=WorkspaceArchive.Stage(input,directory);Assert.That(new RoomStorage(stage.DirectoryPath).Load(out _).objects[2].staticSupport.enabled,Is.True);}
            byte[] broken=Utf8("malformed current primary");File.WriteAllBytes(current,broken);Assert.That(()=>store.ExportRetained(Origin(),prepared.Id,prepared.Receipt.ManifestHash,"original",Path.Combine(directory,"cache")),Throws.Exception);Assert.That(File.ReadAllBytes(current),Is.EqualTo(broken));Assert.That(File.ReadAllBytes(Path.Combine(data,Legacy)),Is.EqualTo(old[Legacy]));
        }
        [Test] public void OriginalManifestHashIsCheckedBeforeLegacyDecoding()
        {
            var docs=Documents(true);var bytes=Archive(docs,33,m=>m["entries"][0]["sha256"]=new string('0',64));Assert.Throws<InvalidDataException>(()=>WorkspaceArchive.Stage(new MemoryStream(bytes),directory));Assert.That(Directory.GetDirectories(directory,"workspace-import-*"),Is.Empty);
        }
    }
}
