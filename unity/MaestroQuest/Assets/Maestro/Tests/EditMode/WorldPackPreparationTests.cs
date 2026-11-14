// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class WorldPackPreparationTests
    {
        static byte[] Bytes(string value)=>new UTF8Encoding(false,true).GetBytes(value);
        static JObject Source()=>JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Resources/WorldPacks/observatory.json")));
        static WorldPackCatalog.Entry Read(JObject source) {
            var bytes=Bytes(source.ToString(Formatting.None));return WorldPackCatalog.Read(bytes,ModelLibrary.Hash(bytes));
        }
        static RoomDocument Room(WorkspaceArchiveSnapshot snapshot) {
            var wire=JObject.Parse(Encoding.UTF8.GetString(snapshot.Documents[RoomStorage.FileName]));
            var room=JsonUtility.FromJson<RoomDocument>(wire.ToString());
            // Match the accepted archive/store read path: Unity materializes null nested classes
            // as known empty instances. Only that representation is normalized back to absence.
            RoomLiquidCodec.Decode(room,wire);RoomStorage.Normalize(room);
            Assert.That(room.Validate(out var error),Is.True,error);return room;
        }
        static void SetFeatures(JObject source)=>source["features"]=new JArray(WorldPackCatalog.RequiredFeatures((JObject)source["creation"]));
        static JObject ResourceSource() {
            var source=Source();var fixture=JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/construction-resources-contract.json")));
            source["creation"]=fixture[0]["arguments"].DeepClone();var blueprint=(JObject)source["creation"]["blueprint"];
            var layer=(JObject)Source()["creation"]["blueprint"]["resources"]["visibilityLayers"][0].DeepClone();
            blueprint["resources"]["version"]=2;blueprint["resources"]["visibilityLayers"]=new JArray(layer);
            foreach(JObject piece in (JArray)blueprint["pieces"]){piece["name"]=(string)layer["id"];piece["source"]["prototype"]["version"]=3;piece["source"]["prototype"]["visibilityLayer"]=(string)layer["id"];}
            SetFeatures(source);return source;
        }
        [Test] public void ActualBundledCatalogHasExactSourceAndFreshDefaultsRemainUntouched() {
            var entry=WorldPackCatalog.Available.Single(e=>e.Id=="maestro.observatory");
            Assert.That(WorldPackCatalog.TryGet(entry.Id,entry.Version,entry.Hash,out var selected),Is.True);Assert.That(selected,Is.SameAs(entry));
            Assert.That(WorldPackCatalog.TryGet(entry.Id,entry.Version+1,entry.Hash,out _),Is.False);
            Assert.That(WorldPackCatalog.TryGet(entry.Id,entry.Version,new string('0',64),out _),Is.False);
            var bytes=File.ReadAllBytes(Path.Combine(Application.dataPath,"Maestro/Resources/WorldPacks/observatory.json"));
            Assert.That(entry.Hash,Is.EqualTo(ModelLibrary.Hash(bytes)));Assert.That(entry.SourceBytes,Is.EqualTo(bytes));
            var defaults=WorkspaceDefaults.Snapshot();Assert.That(Room(defaults).objects.Select(o=>o.id),Is.EqualTo(new[]{"book","maestro"}));
            Assert.That(defaults.Documents.ContainsKey(WorldPackDocument.FileName),Is.False);
        }
        [Test] public void PreviewUsesActualBundledAvatarAndMotionsWithoutNativeOrControlEffects() {
            var avatar=BundledAvatar.FromApplication();var motions=BundledMotions.FromApplication();
            Assert.That(avatar,Is.Not.Null);Assert.That(motions,Is.Not.Null);
            var baseline=WorkspaceDefaults.Snapshot(avatar,motions);var before=baseline.Documents.ToDictionary(p=>p.Key,p=>ModelLibrary.Hash(p.Value));
            int roots=Resources.FindObjectsOfTypeAll<GameObject>().Length;
            var result=WorldPackPreparation.Prepare(WorldPackCatalog.Available.Single(e=>e.Id=="maestro.observatory"),baseline);
            Assert.That(Resources.FindObjectsOfTypeAll<GameObject>().Length,Is.EqualTo(roots));
            var original=Room(baseline);var room=Room(result.Snapshot);
            foreach(var builtin in original.objects)Assert.That(JsonUtility.ToJson(room.objects.Single(o=>o.id==builtin.id)),Is.EqualTo(JsonUtility.ToJson(builtin)));
            foreach(var pair in before){Assert.That(ModelLibrary.Hash(baseline.Documents[pair.Key]),Is.EqualTo(pair.Value));if(pair.Key!=RoomStorage.FileName)Assert.That(ModelLibrary.Hash(result.Snapshot.Documents[pair.Key]),Is.EqualTo(pair.Value));}
            Assert.That(result.Snapshot.Assets.Keys,Is.EquivalentTo(baseline.Assets.Keys));
            Assert.That(room.objects.Single(o=>o.id=="maestro").modelHash,Is.EqualTo(avatar.Hash));
            Assert.That(result.Snapshot.Metadata.Summary.Motions,Is.GreaterThan(0));
            Assert.That(room.objects.Where(o=>!o.IsBuiltIn).All(o=>o.recipe!=null&&!o.recipe.playing&&o.motion==null),Is.True);
            Assert.That(room.objects.Count(o=>o.staticSupport.enabled),Is.EqualTo(2));
            Assert.That(JsonUtility.ToJson(room.viewpoint),Is.EqualTo(JsonUtility.ToJson(original.viewpoint)));
            Assert.That(JsonUtility.ToJson(room.lighting),Is.EqualTo(JsonUtility.ToJson(original.lighting)));
            Assert.That(JsonUtility.ToJson(room.weather),Is.EqualTo(JsonUtility.ToJson(original.weather)));
            Assert.That(result.Summary.CreatedObjects,Is.EqualTo(3));Assert.That(result.Summary.RecipeParts,Is.EqualTo(7));
            Assert.That(result.Summary.CollisionPieces,Is.EqualTo(5));Assert.That(result.Summary.ResourceDefinitions,Is.EqualTo(1));
            Assert.That(result.Summary.ManifestHash,Is.EqualTo(WorkspaceArchive.Fingerprint(result.Snapshot).ManifestHash));
        }
        [Test] public void ExpansionRetainsFreshDisjointExactMappingsAndDoesNotRewriteLiterals() {
            var source=ResourceSource();string original=source.ToString();var entry=Read(source);var baseline=WorkspaceDefaults.Snapshot();
            var a=WorldPackPreparation.Prepare(entry,baseline);var b=WorldPackPreparation.Prepare(entry,baseline);
            Assert.That(a.WorldId,Is.Not.EqualTo(b.WorldId));Assert.That(a.InstanceId,Is.Not.EqualTo(b.InstanceId));
            Assert.That(a.Mapping.Objects.Values.Intersect(b.Mapping.Objects.Values),Is.Empty);
            Assert.That(a.Mapping.Resources.Select(r=>r.Id).Intersect(b.Mapping.Resources.Select(r=>r.Id)),Is.Empty);
            var room=Room(a.Snapshot);var objects=room.objects.Where(o=>!o.IsBuiltIn).ToArray();
            Assert.That(a.Mapping.Objects.Keys,Is.EquivalentTo(new[]{"base","other"}));
            Assert.That(a.Mapping.Resources.Select(r=>r.Kind),Is.EquivalentTo(new[]{"appearance","audio","environment","visibility"}));
            foreach(var mapped in a.Mapping.Objects)Assert.That(objects.Single(o=>o.id==mapped.Value).name,Is.EqualTo(new string('0',31)+"1"));
            Assert.That(objects.Select(o=>o.appearanceBindings.Single().appearanceId).Distinct().Single(),Is.EqualTo(a.Mapping.Resources.Single(r=>r.Kind=="appearance").Id));
            Assert.That(objects.Select(o=>o.audioEmitters.Single().source).Distinct().Single(),Is.EqualTo(a.Mapping.Resources.Single(r=>r.Kind=="audio").Id));
            Assert.That(objects.Select(o=>o.environmentProfile).Distinct().Single(),Is.EqualTo(a.Mapping.Resources.Single(r=>r.Kind=="environment").Id));
            Assert.That(objects.Select(o=>o.visibilityLayer).Distinct().Single(),Is.EqualTo(a.Mapping.Resources.Single(r=>r.Kind=="visibility").Id));
            var provenance=WorldPackDocument.Decode(a.Snapshot.Documents[WorldPackDocument.FileName]);provenance.ValidateWorld(a.WorldId);
            var instance=provenance.instances.Single();Assert.That(instance.sourceHash,Is.EqualTo(entry.Hash));Assert.That(instance.instanceId,Is.EqualTo(a.InstanceId));
            Assert.That(instance.objects.ToDictionary(o=>o.slot,o=>o.id),Is.EqualTo(a.Mapping.Objects));
            foreach(var map in a.Mapping.Resources)Assert.That(instance.resources.Single(r=>r.kind==map.Kind&&r.slot==map.Slot).id,Is.EqualTo(map.Id));
            Assert.That(source.ToString(),Is.EqualTo(original));
            Assert.Throws<NotSupportedException>(()=>((IDictionary<string,string>)a.Mapping.Objects).Add("illegal",new string('f',32)));
            var detached=entry.Creation;detached["scale"]=4;var copy=entry.SourceBytes;copy[0]=0;
            Assert.That(entry.Creation["scale"].Value<int>(),Is.EqualTo(1));Assert.That(entry.SourceBytes[0],Is.Not.EqualTo(0));
        }
        [Test] public void PreparedArchiveKeepsPreviewIdentitiesAndDeletionNeverReinstallsPackObjects() {
            var result=WorldPackPreparation.Prepare(Read(Source()),WorkspaceDefaults.Snapshot());string directory=Path.Combine(Path.GetTempPath(),"MaestroPackTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try {
                using var zip=new MemoryStream();var receipt=WorkspaceArchive.Write(zip,result.Snapshot);Assert.That(receipt.ManifestHash,Is.EqualTo(result.Summary.ManifestHash));zip.Position=0;
                using(var prepared=WorkspaceArchive.Stage(zip,directory)){
                    var room=new RoomStorage(prepared.DirectoryPath).Load(out var error);Assert.That(room,Is.Not.Null,error);
                    Assert.That(room.objects.Where(o=>!o.IsBuiltIn).Select(o=>o.id),Is.EquivalentTo(result.Mapping.Objects.Values));
                    Assert.That(File.ReadAllBytes(Path.Combine(prepared.DirectoryPath,WorldPackDocument.FileName)),Is.EqualTo(result.Snapshot.Documents[WorldPackDocument.FileName]));
                }
                var changed=Room(result.Snapshot);changed.objects=changed.objects.Where(o=>o.IsBuiltIn).ToArray();
                var docs=result.Snapshot.Documents.ToDictionary(p=>p.Key,p=>p.Value);docs[RoomStorage.FileName]=Bytes(RoomLiquidCodec.Encode(changed));
                var deleted=new WorkspaceArchiveSnapshot(docs,result.Snapshot.Assets);using var editedZip=new MemoryStream();WorkspaceArchive.Write(editedZip,deleted);editedZip.Position=0;
                using var reopened=WorkspaceArchive.Stage(editedZip,directory);var accepted=new RoomStorage(reopened.DirectoryPath).Load(out var issue);
                Assert.That(accepted,Is.Not.Null,issue);Assert.That(accepted.objects.Length,Is.EqualTo(2));
                Assert.That(WorldPackDocument.Decode(File.ReadAllBytes(Path.Combine(reopened.DirectoryPath,WorldPackDocument.FileName))).instances.Single().objects.Length,Is.EqualTo(3));
                Assert.Throws<InvalidDataException>(()=>WorldPackPreparation.Prepare(Read(Source()),deleted));
            } finally {Directory.Delete(directory,true);}
        }
        [TestCase("format")][TestCase("version")][TestCase("blueprint-version")][TestCase("unknown")][TestCase("dependency")][TestCase("asset")]
        [TestCase("missing-feature")][TestCase("unknown-feature")][TestCase("duplicate-feature")][TestCase("compact-label")]
        public void DescriptorRejectsUnsupportedWireBeforePreparation(string kind) {
            var source=Source();switch(kind){
                case "format":source["format"]="another-world";break;case "version":source["version"]=2;break;case "blueprint-version":source["creation"]["blueprint"]["version"]=2;break;case "unknown":source["autoInstall"]=true;break;
                case "dependency":((JArray)source["dependencies"]).Add(new JObject());break;case "asset":((JArray)source["assets"]).Add(new JObject());break;
                case "missing-feature":((JArray)source["features"]).RemoveAt(0);break;case "unknown-feature":((JArray)source["features"]).Add("future.v999");break;
                case "duplicate-feature":((JArray)source["features"]).Add(source["features"][0].DeepClone());break;case "compact-label":source["name"]="A\u2028B";break;
            }
            Assert.Throws<InvalidDataException>(()=>Read(source));
        }
        [Test] public void ExactBytesRejectTamperingDuplicatesTrailingContentAndOversize() {
            var bytes=Bytes(Source().ToString());string hash=ModelLibrary.Hash(bytes);bytes[bytes.Length-1]=(byte)' ';
            Assert.Throws<InvalidDataException>(()=>WorldPackCatalog.Read(bytes,hash));
            string original=Source().ToString(Formatting.None);
            foreach(string text in new[]{original+"{}",original.Insert(1,"\"version\":1,")}){
                var invalid=Bytes(text);Assert.Catch(()=>WorldPackCatalog.Read(invalid,ModelLibrary.Hash(invalid)));
            }
            bytes=new byte[WorldPackCatalog.MaximumBytes+1];Assert.Throws<InvalidDataException>(()=>WorldPackCatalog.Read(bytes,ModelLibrary.Hash(bytes)));
        }
        [TestCase("model")][TestCase("template")][TestCase("image")][TestCase("clip")][TestCase("missing-local")][TestCase("extra-local")]
        public void SourceDependencyClosureCannotBeBypassed(string kind) {
            var source=ResourceSource();var creation=(JObject)source["creation"];var resources=(JObject)creation["blueprint"]["resources"];
            switch(kind){
                case "model":creation["blueprint"]["pieces"][0]["source"]["prototype"]["geometry"]=new JObject{["kind"]="model",["modelHash"]=new string('a',64)};break;
                case "template":creation["blueprint"]["pieces"][0]["source"]=new JObject{["kind"]="template",["templateHash"]=new string('a',64)};break;
                case "image":resources["appearances"][0]["style"]["patternMode"]="image";resources["appearances"][0]["style"]["imageHash"]=new string('a',64);break;
                case "clip":resources["audioSources"][0]["kind"]="clip";resources["audioSources"][0]["assetHash"]=new string('b',64);break;
                case "missing-local":((JArray)resources["audioSources"]).Clear();break;
                case "extra-local":var extra=resources["audioSources"][0].DeepClone();extra["id"]=new string('e',32);((JArray)resources["audioSources"]).Add(extra);break;
            }
            SetFeatures(source);Assert.Throws<InvalidDataException>(()=>Read(source));
        }
        [Test] public void FreshPreparationRefusesOccupiedBaselineAndCancellation() {
            var entry=Read(Source());var baseline=WorkspaceDefaults.Snapshot();var room=Room(baseline);
            room.objects=room.objects.Append(new RoomObjectData{id=new string('d',32),kind=RoomObjectKind.Block}).ToArray();
            var docs=baseline.Documents.ToDictionary(p=>p.Key,p=>p.Value);docs[RoomStorage.FileName]=Bytes(RoomLiquidCodec.Encode(room));
            var occupied=new WorkspaceArchiveSnapshot(docs,baseline.Assets);Assert.Throws<InvalidDataException>(()=>WorldPackPreparation.Prepare(entry,occupied));
            Assert.Throws<OperationCanceledException>(()=>WorldPackPreparation.Prepare(entry,baseline,new CancellationToken(true)));
            Assert.That(Room(baseline).objects.Length,Is.EqualTo(2));
        }
        [Test] public void BundledClosureRejectsMissingAndChangedActualModelBytes() {
            var baseline=WorkspaceDefaults.Snapshot();var room=Room(baseline);var payload=ModelFixture.Mixamo();string hash=ModelLibrary.Hash(payload),path="models/"+hash+".glb";
            room.objects.Single(o=>o.id=="maestro").modelHash=hash;var docs=baseline.Documents.ToDictionary(p=>p.Key,p=>p.Value);docs[RoomStorage.FileName]=Bytes(RoomLiquidCodec.Encode(room));
            var missing=new WorkspaceArchiveSnapshot(docs,new Dictionary<string,Func<Stream>>());Assert.That(missing.Metadata.Summary.MissingModels,Does.Contain(hash));
            Assert.Throws<InvalidDataException>(()=>WorldPackPreparation.Prepare(Read(Source()),missing));
            var supplied=new WorkspaceArchiveSnapshot(docs,new Dictionary<string,Func<Stream>>{{path,()=>new MemoryStream(payload,false)}});
            payload[0]^=1;Assert.Catch(()=>WorldPackPreparation.Prepare(Read(Source()),supplied));
            Assert.That(Room(supplied).objects.Length,Is.EqualTo(2));
        }
    }
}
