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
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Maestro.Quest.Tests
{
    public sealed class WorldPackDocumentTests
    {
        static readonly string World=new('a',32);
        string directory;
        [SetUp] public void Setup(){directory=Path.Combine(Path.GetTempPath(),"MaestroWorldPack-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);}
        [TearDown] public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        static string Id(int n)=>n.ToString("x32");
        static byte[] Utf8(string text)=>new UTF8Encoding(false,true).GetBytes(text);
        static WorldPackDocument Pack()=>new(){worldId=World,instances=new[]{new WorldPackInstance {
            instanceId=Id(1),packId="maestro.starter-world",packVersion=1,sourceHash=new string('f',64),entryId="garden",
            objects=new[]{new WorldPackSlot{slot="AppleTree_1",id=Id(2)}},
            resources=new[]{new WorldPackResourceSlot{kind="appearance",slot=Id(3),id=Id(4)}}
        }}};
        static RoomDocument Room(float x=0)=>new(){version=RoomDocument.CurrentVersion,world=new(){worldId=World,regionId=Id(20)},objects=new[]{
            new RoomObjectData{id="book",kind=RoomObjectKind.Book,position=new Vector3(x,1,0)},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}
        }};
        static Dictionary<string,byte[]> Documents(bool legacy=false,bool provenance=true)
        {
            var room=JObject.Parse(RoomLiquidCodec.Encode(Room()));if(legacy){room["version"]=46;foreach(JObject item in room["objects"])item.Remove("staticSupport");}
            byte[] Json(object value)=>Utf8(JsonUtility.ToJson(value));
            var docs=new Dictionary<string,byte[]> {
                [legacy?WorkspaceArchiveMetadata.LegacyRoomFileName:RoomStorage.FileName]=Utf8(room.ToString(Formatting.None)),
                ["behaviours.v2.json"]=Json(new RuleDocument()),["controls.v2.json"]=Json(new ControllerPreferences()),
                ["avatar-activities.v2.json"]=Json(new AvatarActivityDocument()),["motions/motions.v2.json"]=Utf8("{\"version\":2,\"entries\":[],\"sources\":[]}")
            };
            if(provenance)docs.Add(WorldPackDocument.FileName,Utf8(JObject.Parse(Encoding.UTF8.GetString(Pack().Encode())).ToString(Formatting.Indented)+"\n"));
            return docs;
        }
        static byte[] Archive(Dictionary<string,byte[]> docs,int version)
        {
            // Deliberately bypass snapshot validation to test untrusted manifests/documents.
            var manifest=new JObject {["format"]="maestro-native-workspace",["version"]=version,["entries"]=new JArray(docs.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>new JObject {["path"]=p.Key,["bytes"]=p.Value.Length,["sha256"]=ModelLibrary.Hash(p.Value)}))};
            using var bytes=new MemoryStream();using(var zip=new ZipArchive(bytes,ZipArchiveMode.Create,true)){
                void Add(string path,byte[] data){using var output=zip.CreateEntry(path).Open();output.Write(data,0,data.Length);}
                Add("manifest.json",Utf8(manifest.ToString(Formatting.None)));foreach(var pair in docs)Add(pair.Key,pair.Value);
            }return bytes.ToArray();
        }
        static Dictionary<string,byte[]> Entries(string path)
        {
            using var input=File.OpenRead(path);using var zip=new ZipArchive(input,ZipArchiveMode.Read);
            return zip.Entries.ToDictionary(e=>e.FullName,e=>{using var input=e.Open();using var output=new MemoryStream();input.CopyTo(output);return output.ToArray();});
        }
        [Test] public void CodecPreservesExactIdentitiesAndDetachesValuesWithoutRequiringLiveMappings()
        {
            var original=Pack();var bytes=original.Encode();var copy=WorldPackDocument.Decode(bytes);copy.ValidateWorld(World);
            Assert.That(copy.Encode(),Is.EqualTo(bytes));Assert.That(copy.instances[0].objects[0].slot,Is.EqualTo("AppleTree_1"));
            original.instances[0].objects[0].id=Id(99);Assert.That(copy.instances[0].objects[0].id,Is.EqualTo(Id(2)));
            bytes[0]=0;Assert.That(copy.Encode()[0],Is.EqualTo((byte)'{'));
            var docs=Documents();Assert.That(Room().objects.Any(o=>o.id==Id(2)),Is.False);
            Assert.DoesNotThrow(()=>new WorkspaceArchiveSnapshot(docs,new Dictionary<string,Func<Stream>>()));
        }
        [TestCase("missing-field")][TestCase("instance-null")][TestCase("objects-null")][TestCase("resources-null")][TestCase("extra-root")][TestCase("version")][TestCase("version-float")][TestCase("world")][TestCase("instances-null")]
        [TestCase("pack-newline")][TestCase("entry-newline")][TestCase("slot-newline")][TestCase("extra-instance")][TestCase("pack-version")][TestCase("pack-version-float")][TestCase("pack-id")][TestCase("entry-id")][TestCase("source-hash")]
        [TestCase("object-slot")][TestCase("object-id")][TestCase("extra-object")][TestCase("resource-kind")][TestCase("resource-slot")][TestCase("extra-resource")]
        public void StrictCodecRefusesMalformedFieldsAndFutureSchemas(string change)
        {
            var json=JObject.Parse(Encoding.UTF8.GetString(Pack().Encode()));var entry=json["instances"][0];
            switch(change){
                case "missing-field":((JObject)entry).Remove("sourceHash");break;case "instance-null":json["instances"][0]=null;break;case "objects-null":entry["objects"]=null;break;case "resources-null":entry["resources"]=null;break;
                case "extra-root":json["extra"]=1;break;case "version":json["version"]=2;break;case "version-float":json["version"]=1.0;break;
                case "world":json["worldId"]="book";break;case "instances-null":json["instances"]=null;break;
                case "extra-instance":entry["extra"]=1;break;case "pack-version":entry["packVersion"]=0;break;case "pack-version-float":entry["packVersion"]=1.0;break;
                case "pack-newline":entry["packId"]="maestro.test\n";break;case "entry-newline":entry["entryId"]="garden\n";break;case "slot-newline":entry["objects"][0]["slot"]="Garden\n";break;
                case "pack-id":entry["packId"]="../maestro";break;case "entry-id":entry["entryId"]="Garden";break;case "source-hash":entry["sourceHash"]=new string('F',64);break;
                case "object-slot":entry["objects"][0]["slot"]="not a slot";break;case "object-id":entry["objects"][0]["id"]="maestro";break;case "extra-object":entry["objects"][0]["owner"]=true;break;
                case "resource-kind":entry["resources"][0]["kind"]="unknown";break;case "resource-slot":entry["resources"][0]["slot"]="surface";break;case "extra-resource":entry["resources"][0]["owner"]=true;break;
            }
            Assert.Throws<InvalidDataException>(()=>WorldPackDocument.Decode(Utf8(json.ToString(Formatting.None))));
        }
        [Test] public void DuplicateKeysTrailingContentInvalidUtf8AndByteOverflowAreRefused()
        {
            string text=Encoding.UTF8.GetString(Pack().Encode());
            Assert.Throws<InvalidDataException>(()=>WorldPackDocument.Decode(Utf8(text.Replace("\"version\":1","\"version\":1,\"version\":1"))));
            Assert.Throws<InvalidDataException>(()=>WorldPackDocument.Decode(Utf8(text+"{}")));
            Assert.Throws<InvalidDataException>(()=>WorldPackDocument.Decode(new byte[]{0xc3,0x28}));
            Assert.Throws<InvalidDataException>(()=>WorldPackDocument.Decode(new byte[WorldPackDocument.MaximumBytes+1]));
            Assert.Throws<InvalidDataException>(()=>WorldPackDocument.Decode(Array.Empty<byte>()));
        }
        [TestCase("instance")][TestCase("object-id")][TestCase("object-slot")][TestCase("resource-id")][TestCase("resource-slot")]
        public void DuplicateMappingsAreRefusedWithinAndAcrossInstances(string change)
        {
            var doc=Pack();var next=Pack().instances[0];next.instanceId=Id(5);next.objects[0].id=Id(6);next.resources[0].id=Id(7);doc.instances=doc.instances.Append(next).ToArray();
            switch(change){
                case "instance":next.instanceId=Id(1);break;case "object-id":next.objects[0].id=Id(2);break;
                case "object-slot":next.objects=next.objects.Append(new WorldPackSlot{slot=next.objects[0].slot,id=Id(8)}).ToArray();break;
                case "resource-id":next.resources[0].id=Id(4);break;
                case "resource-slot":next.resources=next.resources.Append(new WorldPackResourceSlot{kind="appearance",slot=next.resources[0].slot,id=Id(8)}).ToArray();break;
            }
            Assert.Throws<InvalidDataException>(()=>doc.Encode());
        }
        [Test] public void LimitsApplyAcrossTheWholeHistoricalDocumentAndResourcesKeepKindNamespaces()
        {
            var doc=Pack();var first=doc.instances[0];first.objects=Enumerable.Range(100,256).Select(n=>new WorldPackSlot{slot="s"+n,id=Id(n)}).ToArray();
            var second=Pack().instances[0];second.instanceId=Id(5);second.resources=Array.Empty<WorldPackResourceSlot>();second.objects=Array.Empty<WorldPackSlot>();doc.instances=new[]{first,second};Assert.DoesNotThrow(()=>doc.Encode());
            second.objects=new[]{new WorldPackSlot{slot="extra",id=Id(999)}};Assert.Throws<InvalidDataException>(()=>doc.Encode());
            doc=Pack();doc.instances=Enumerable.Range(1,16).Select(n=>new WorldPackInstance{instanceId=Id(n),packId="maestro.test",packVersion=1,sourceHash=new string('f',64),entryId="empty"}).ToArray();Assert.DoesNotThrow(()=>doc.Encode());
            doc.instances=doc.instances.Append(new WorldPackInstance{instanceId=Id(17)}).ToArray();Assert.Throws<InvalidDataException>(()=>doc.Encode());
            doc=Pack();doc.instances[0].resources=new[]{new WorldPackResourceSlot{kind="appearance",slot=Id(3),id=Id(4)},new WorldPackResourceSlot{kind="audio",slot=Id(3),id=Id(4)}};Assert.DoesNotThrow(()=>doc.Encode());
        }
        [TestCase("appearance",64)][TestCase("audio",32)][TestCase("environment",16)][TestCase("visibility",16)]
        public void ResourceLimitsAreGlobalPerKind(string kind,int limit)
        {
            var doc=Pack();var first=doc.instances[0];first.resources=Enumerable.Range(100,limit).Select(n=>new WorldPackResourceSlot{kind=kind,slot=Id(n),id=Id(n)}).ToArray();Assert.DoesNotThrow(()=>doc.Encode());
            var next=Pack().instances[0];next.instanceId=Id(7);next.objects=Array.Empty<WorldPackSlot>();next.resources=new[]{new WorldPackResourceSlot{kind=kind,slot=Id(999),id=Id(999)}};doc.instances=new[]{first,next};Assert.Throws<InvalidDataException>(()=>doc.Encode());
        }
        [Test] public void CurrentArchiveIncludesExactProvenanceAndVerifiesItsRoomIdentity()
        {
            var docs=Documents();using var output=new MemoryStream();var receipt=WorkspaceArchive.Write(output,new WorkspaceArchiveSnapshot(docs,new Dictionary<string,Func<Stream>>()));
            output.Position=0;using var stage=WorkspaceArchive.Stage(output,directory);
            Assert.That((int)JObject.Parse(Encoding.UTF8.GetString(stage.ManifestBytes))["version"],Is.EqualTo(35));
            Assert.That(stage.Receipt.ManifestHash,Is.EqualTo(receipt.ManifestHash));Assert.That(File.ReadAllBytes(Path.Combine(stage.DirectoryPath,WorldPackDocument.FileName)),Is.EqualTo(docs[WorldPackDocument.FileName]));
            Assert.That(WorkspaceArchive.VerifyPreparedDirectory(stage.DirectoryPath,stage.ManifestBytes).ManifestHash,Is.EqualTo(receipt.ManifestHash));
            var detached=new WorkspaceArchiveSnapshot(docs,new Dictionary<string,Func<Stream>>());docs[WorldPackDocument.FileName][0]=0;
            using var another=new MemoryStream();Assert.DoesNotThrow(()=>WorkspaceArchive.Write(another,detached));
        }
        [Test] public void MismatchedWorldRefusesSnapshotAndUntrustedStageWithoutChangingBytes()
        {
            var docs=Documents();var pack=Pack();pack.worldId=Id(99);docs[WorldPackDocument.FileName]=pack.Encode();var bytes=(byte[])docs[WorldPackDocument.FileName].Clone();
            Assert.Throws<InvalidDataException>(()=>pack.ValidateWorld(World));Assert.Throws<InvalidDataException>(()=>new WorkspaceArchiveSnapshot(docs,new Dictionary<string,Func<Stream>>()));
            Assert.Throws<InvalidDataException>(()=>WorkspaceArchive.Stage(new MemoryStream(Archive(docs,35)),directory));
            Assert.That(docs[WorldPackDocument.FileName],Is.EqualTo(bytes));Assert.That(Directory.GetDirectories(directory,"workspace-import-*"),Is.Empty);
        }
        [TestCase(32)][TestCase(33)][TestCase(34)]
        public void PriorFormatsRemainReadableButCannotSmuggleProvenance(int version)
        {
            var docs=Documents(version<34,false);using(var stage=WorkspaceArchive.Stage(new MemoryStream(Archive(docs,version)),directory)){
                Assert.That(File.Exists(Path.Combine(stage.DirectoryPath,WorldPackDocument.FileName)),Is.False);Assert.That(WorkspaceArchive.VerifyPreparedDirectory(stage.DirectoryPath,stage.ManifestBytes),Is.Not.Null);
            }
            docs[WorldPackDocument.FileName]=Pack().Encode();Assert.Throws<InvalidDataException>(()=>WorkspaceArchive.Stage(new MemoryStream(Archive(docs,version)),directory));
        }
        [Test] public void OptionalAbsenceAndFutureArchiveAreDistinct()
        {
            using(var stage=WorkspaceArchive.Stage(new MemoryStream(Archive(Documents(provenance:false),35)),directory))Assert.That(WorldPackDocument.ReadOptional(stage.DirectoryPath),Is.Null);
            Assert.Throws<InvalidDataException>(()=>WorkspaceArchive.Stage(new MemoryStream(Archive(Documents(),36)),directory));
        }
        [TestCase("world-packs.v2.json")][TestCase("world-packs.v1.json.backup")]
        public void UnrecognizedSavedEvidenceIsNeverOmittedOrRepaired(string name)
        {
            string path=Path.Combine(directory,name);byte[] bytes=Pack().Encode();File.WriteAllBytes(path,bytes);
            Assert.Throws<InvalidDataException>(()=>WorldPackDocument.ReadOptional(directory));Assert.That(File.ReadAllBytes(path),Is.EqualTo(bytes));
        }
        [Test] public void MalformedOrNonregularSavedDocumentIsNotAbsence()
        {
            string path=Path.Combine(directory,WorldPackDocument.FileName);File.WriteAllText(path,"malformed");Assert.Throws<InvalidDataException>(()=>WorldPackDocument.ReadOptional(directory));Assert.That(File.ReadAllText(path),Is.EqualTo("malformed"));
            File.Delete(path);Directory.CreateDirectory(path);Assert.Throws<InvalidDataException>(()=>WorldPackDocument.ReadOptional(directory));
        }
        [TestCase("prepared",false)][TestCase("committed",true)]
        public void OrdinarySavesAndPairedSnapshotRecoveryLeaveProvenanceBytesUntouched(string fault,bool committed)
        {
            var storage=new RoomStorage(directory);Assert.That(storage.Save(Room(),out var error),Is.True,error);byte[] bytes=Pack().Encode();File.WriteAllBytes(Path.Combine(directory,WorldPackDocument.FileName),bytes);
            Assert.That(storage.Save(Room(1),out error),Is.True,error);Assert.That(WorldPackDocument.ReadOptional(directory),Is.EqualTo(bytes));
            var before=RoomSnapshotTransaction.Capture(directory);var after=RoomSnapshotTransaction.FromDocuments(Room(2),ProgramMemoryDocument.Empty());
            Assert.Throws<IOException>(()=>RoomSnapshotTransaction.Publish(directory,before,after,stage=>{if(stage==fault)throw new IOException("Injected stop");}));
            var recovered=RoomSnapshotTransaction.Capture(directory);Assert.That(recovered.Identity,Is.EqualTo((committed?after:before).Identity));Assert.That(WorldPackDocument.ReadOptional(directory),Is.EqualTo(bytes));
        }
        [Test] public void RetainedExportAndRecoveryPreserveProvenanceAfterOrdinaryEditsAndRefuseNewerEvidence()
        {
            var docs=Documents();string storeRoot=Path.Combine(directory,"store");var store=new WorkspaceGenerationStore(storeRoot);var prepared=store.Prepare(new MemoryStream(Archive(docs,35)));
            string data=Path.Combine(storeRoot,"workspace-generations.v1","generations",prepared.Id,"data");var storage=new RoomStorage(data);
            var edited=storage.Load(out var error);Assert.That(error,Is.Null);Assert.That(edited,Is.Not.Null);Assert.That(edited.world.worldId,Is.EqualTo(World));
            edited.objects.Single(item=>item.id=="book").position=new Vector3(1,1,0);Assert.That(storage.Save(edited,out error),Is.True,error);
            Assert.That(WorldPackDocument.ReadOptional(data),Is.EqualTo(docs[WorldPackDocument.FileName]));
            string Origin()=>(string)store.InspectRetention()["originHash"];
            using(var exported=store.ExportRetained(Origin(),prepared.Id,prepared.Receipt.ManifestHash,"original",Path.Combine(directory,"cache"))){
                Assert.That(Entries(exported.Path)[WorldPackDocument.FileName],Is.EqualTo(docs[WorldPackDocument.FileName]));using var input=File.OpenRead(exported.Path);using var stage=WorkspaceArchive.Stage(input,directory);Assert.That(WorldPackDocument.ReadOptional(stage.DirectoryPath),Is.EqualTo(docs[WorldPackDocument.FileName]));
                var exportedRoom=new RoomStorage(stage.DirectoryPath).Load(out error);Assert.That(error,Is.Null);Assert.That(exportedRoom.objects.Single(item=>item.id=="book").position,Is.EqualTo(new Vector3(1,1,0)));
            }
            var recovery=store.PrepareDamagedRecovery(Origin(),prepared.Id,prepared.Receipt.ManifestHash);string recoveryData=Path.Combine(storeRoot,"workspace-generations.v1","generations",recovery.Id,"data");
            Assert.That(WorldPackDocument.ReadOptional(recoveryData),Is.EqualTo(docs[WorldPackDocument.FileName]));
            var recoveredRoom=new RoomStorage(recoveryData).Load(out error);Assert.That(error,Is.Null);Assert.That(recoveredRoom.objects.Single(item=>item.id=="book").position,Is.EqualTo(new Vector3(1,1,0)));
            string newer=Path.Combine(data,"world-packs.v2.json");File.WriteAllBytes(newer,docs[WorldPackDocument.FileName]);
            Assert.Throws<InvalidDataException>(()=>store.ExportRetained(Origin(),prepared.Id,prepared.Receipt.ManifestHash,"original",Path.Combine(directory,"cache")));
            Assert.That(File.ReadAllBytes(newer),Is.EqualTo(docs[WorldPackDocument.FileName]));Assert.That(WorldPackDocument.ReadOptional(recoveryData),Is.EqualTo(docs[WorldPackDocument.FileName]));
        }
    }
}
