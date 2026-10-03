// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
    public sealed class WorkspaceArchiveTests
    {
        string directory;readonly Dictionary<string,byte[]> documents=new(),payloads=new();
        string modelHash,motionPath,moduleHash,motionId;
        static byte[] Bytes(string text)=>new UTF8Encoding(false,true).GetBytes(text);
        static byte[] Document(object value)=>Bytes(JsonUtility.ToJson(value));
        [SetUp] public void Setup()
        {
            directory=Path.Combine(Path.GetTempPath(),"MaestroArchiveTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);documents.Clear();payloads.Clear();
            byte[] model=ModelFixture.Mixamo();modelHash=ModelLibrary.Hash(model);payloads["models/"+modelHash+".glb"]=model;
            using var motions=new MotionLibrary(Path.Combine(directory,"source-motions"));var motion=motions.ImportAsync("Wave.glb",model,"gestures").GetAwaiter().GetResult().Single();motionId=motion.id;motionPath="motions/"+motion.hash+".motion.glb";
            payloads[motionPath]=File.ReadAllBytes(Path.Combine(directory,"source-motions",motion.hash+".motion.glb"));documents["motions/motions.v2.json"]=File.ReadAllBytes(Path.Combine(directory,"source-motions","motions.v2.json"));
            documents["room.v11.json"]=Document(new RoomDocument {version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData {id="book",kind=RoomObjectKind.Book},new RoomObjectData {id="maestro",kind=RoomObjectKind.Maestro,modelHash=modelHash,walkMotionId=motion.id}}});
            documents["behaviours.v2.json"]=Document(new RuleDocument());documents["controls.v2.json"]=Document(new ControllerPreferences());documents["avatar-activities.v2.json"]=Document(new AvatarActivityDocument());
            documents["models/"+modelHash+".txt"]=Bytes("Maestro äö\nOriginal attribution kept");
            var module=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-modules-nested.json")))["imports"][0]["module"] as JObject;moduleHash=ProgramModules.Hash(module);documents["program-modules.v1/"+moduleHash+".json"]=Bytes(module.ToString(Formatting.None));
        }
        [Test] public void StructureBaselinesAndMissingMemberIdsSurvivePortableArchiveRoundTrip()
        {
            var room=JsonUtility.FromJson<RoomDocument>(Encoding.UTF8.GetString(documents[RoomStorage.FileName]));room.structures=new[]{RoomStructureTests.Structure()};documents[RoomStorage.FileName]=Document(room);
            using var input=new MemoryStream(Archive());using var staged=WorkspaceArchive.Stage(input,directory);
            var restored=new RoomStorage(staged.DirectoryPath).Load(out var loadError);Assert.That(restored,Is.Not.Null,loadError);
            Assert.That(restored.structures.Single().slots.Single().placement.target,Is.EqualTo(new string('a',32)));Assert.That(restored.Validate(out var error),Is.True,error);
        }
        [Test] public void AttachedInkAndConfiguredTipSurvivePortableArchiveWithExactIdentityAndPoints()
        {
            var room=JsonUtility.FromJson<RoomDocument>(Encoding.UTF8.GetString(documents[RoomStorage.FileName]));var entry=CreationTemplates.All.First(x=>x.Id=="chalkboard");
            var board=new RoomObjectData {id=new string('d',32),kind=RoomObjectKind.Assembly,recipe=entry.Recipe,surfaces=entry.Surfaces,drawingTips=new[]{new DrawingTip{part="Board",enabled=false,color=Color.yellow}}};board.surfaces[0].strokes=new[]{new SurfaceStroke {id=new string('e',32),color=Color.cyan,points=new[]{Vector3.left*.05f,Vector3.up*.03f,Vector3.right*.05f}}};room.objects=room.objects.Append(board).ToArray();documents[RoomStorage.FileName]=Document(room);
            using var input=new MemoryStream(Archive());using var staged=WorkspaceArchive.Stage(input,directory);var restored=new RoomStorage(staged.DirectoryPath).Load(out var error);Assert.That(restored,Is.Not.Null,error);var ink=restored.objects.Single(o=>o.id==board.id).surfaces[0].strokes.Single();Assert.That(ink.id,Is.EqualTo(new string('e',32)));Assert.That(ink.color,Is.EqualTo(Color.cyan));Assert.That(ink.points,Is.EqualTo(board.surfaces[0].strokes[0].points));Assert.That(JsonUtility.ToJson(restored.objects.Single(o=>o.id==board.id).drawingTips.Single()),Is.EqualTo(JsonUtility.ToJson(board.drawingTips.Single())));
        }
        [Test] public void IncludedModuleCopiesAndEmbeddedWatcherSurviveWithoutAnyInstalledDefaults()
        {
            var module=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Resources/Programs/Modules/StructureWatch.json")));string hash=ProgramModules.Hash(module);
            documents["program-modules.v1/"+hash+".json"]=Bytes(module.ToString(Formatting.None));
            string source=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-structure-watch.json"))).ToString(Formatting.None);
            documents["behaviours.v2.json"]=Document(new RuleDocument {sequences=new[]{new RuleSequence {id=new string('b',32),name="Portable watcher",program=source}}});
            using var input=new MemoryStream(Archive());using var staged=WorkspaceArchive.Stage(input,directory);
            var library=new ProgramModuleLibrary(staged.DirectoryPath);library.Flush();Assert.That(library.Inspect(hash).Included,Is.False);Assert.That(JToken.DeepEquals(library.Inspect(hash).ReadDefinition(),module),Is.True);
            var rules=new RuleStorage(staged.DirectoryPath).Load(out var error);Assert.That(rules,Is.Not.Null,error);Assert.That(rules.sequences.Single().program,Is.EqualTo(source));Assert.That(rules.sequences.Single().Compile(out error),Is.Not.Null,error);
        }
        [TearDown] public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        WorkspaceArchiveSnapshot Snapshot()=>new(documents,payloads.ToDictionary(x=>x.Key,x=>(Func<Stream>)(()=>new MemoryStream(x.Value,false))));
        byte[] Archive(){using var stream=new MemoryStream();WorkspaceArchive.Write(stream,Snapshot());return stream.ToArray();}
        Dictionary<string,byte[]> Entries(byte[] archive){using var stream=new MemoryStream(archive);using var zip=new ZipArchive(stream,ZipArchiveMode.Read);return zip.Entries.ToDictionary(x=>x.FullName,x=>{using var output=new MemoryStream();using var input=x.Open();input.CopyTo(output);return output.ToArray();});}
        static byte[] Zip(IEnumerable<KeyValuePair<string,byte[]>> entries){using var output=new MemoryStream();using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true)){foreach(var entry in entries){using var stream=zip.CreateEntry(entry.Key).Open();stream.Write(entry.Value,0,entry.Value.Length);}}return output.ToArray();}
        void NoStages()=>Assert.That(Directory.GetDirectories(directory,"workspace-import-*"),Is.Empty);
        [Test] public void RememberedValuesChangeFingerprintRoundTripAndRejectDamage()
        {
            string program=new string('a',32),cell=new string('c',32);var baseline=WorkspaceArchive.Fingerprint(Snapshot()).ManifestHash;
            var memory=ProgramMemoryDocument.Empty().WithValues(program,new Dictionary<string,ProgramMemoryDocument.Cell>{[cell]=new("count",new ProgramValue(7d))});
            documents[ProgramMemoryStore.FileName]=memory.Encode();Assert.That(WorkspaceArchive.Fingerprint(Snapshot()).ManifestHash,Is.Not.EqualTo(baseline));
            using(var input=new MemoryStream(Archive()))using(var staged=WorkspaceArchive.Stage(input,directory)){
                var restored=ProgramMemoryDocument.Decode(File.ReadAllBytes(Path.Combine(staged.DirectoryPath,ProgramMemoryStore.FileName)));Assert.That(restored.Identity,Is.EqualTo(memory.Identity));
            }
            documents[ProgramMemoryStore.FileName]=Bytes("broken");Assert.That(()=>Snapshot(),Throws.Exception);documents.Remove(ProgramMemoryStore.FileName);Assert.That(WorkspaceArchive.Fingerprint(Snapshot()).ManifestHash,Is.EqualTo(baseline));
        }
        [Test] public void ReviewFingerprintIsTheExactArchiveManifestWithoutWritingAZip()
        {
            var first=WorkspaceArchive.Fingerprint(Snapshot());using var zip=new MemoryStream();var exported=WorkspaceArchive.Write(zip,Snapshot());
            Assert.That(first.ManifestHash,Is.EqualTo(exported.ManifestHash));Assert.That(first.Summary.Bytes,Is.EqualTo(exported.Summary.Bytes));Assert.That(first.Summary.Files,Is.EqualTo(exported.Summary.Files));
            var shuffled=new WorkspaceArchiveSnapshot(documents.Reverse().ToDictionary(x=>x.Key,x=>x.Value),payloads.Reverse().ToDictionary(x=>x.Key,x=>(Func<Stream>)(()=>new MemoryStream(x.Value,false))));
            Assert.That(WorkspaceArchive.Fingerprint(shuffled).ManifestHash,Is.EqualTo(first.ManifestHash));
            var preferences=JsonUtility.FromJson<ControllerPreferences>(Encoding.UTF8.GetString(documents["controls.v2.json"]));preferences.deadZone=.35f;documents["controls.v2.json"]=Document(preferences);
            Assert.That(WorkspaceArchive.Fingerprint(Snapshot()).ManifestHash,Is.Not.EqualTo(first.ManifestHash));
        }
        [Test] public void ReviewFingerprintAppliesAssetValidationAndCancellationInsteadOfTrustingNames()
        {
            using var cancelled=new CancellationTokenSource();cancelled.Cancel();Assert.Throws<OperationCanceledException>(()=>WorkspaceArchive.Fingerprint(Snapshot(),cancelled.Token));
            payloads["models/"+modelHash+".glb"][0]^=1;Assert.That(()=>WorkspaceArchive.Fingerprint(Snapshot()),Throws.Exception);
        }
        [Test] public void RoundTripPreservesEveryIdentityPayloadAndNativeStoreWithoutActivating()
        {
            string sentinel=Path.Combine(directory,"existing-room.txt");File.WriteAllText(sentinel,"current live room");using var output=new MemoryStream();var written=WorkspaceArchive.Write(output,Snapshot());Assert.That(output.CanWrite,Is.True);
            using(var stage=WorkspaceArchive.Stage(new MemoryStream(output.ToArray()),directory)){
                Assert.That(stage.Receipt.ManifestHash,Is.EqualTo(written.ManifestHash));Assert.That(stage.Receipt.Summary.Models,Is.EqualTo(1));Assert.That(stage.Receipt.Summary.Motions,Is.EqualTo(1));Assert.That(stage.Receipt.Summary.Modules,Is.EqualTo(1));Assert.That(stage.Receipt.Summary.MissingModels,Is.Empty);
                foreach(var pair in documents.Concat(payloads))Assert.That(File.ReadAllBytes(Path.Combine(stage.DirectoryPath,pair.Key)),Is.EqualTo(pair.Value),pair.Key);
                var room=new RoomStorage(stage.DirectoryPath).Load(out var error);Assert.That(error,Is.Null);Assert.That(room.objects.Single(x=>x.id=="maestro").walkMotionId,Is.EqualTo(motionId));
                using var loaded=new MotionLibrary(Path.Combine(stage.DirectoryPath,"motions"));Assert.That(loaded.Downloaded(motionId),Is.True);Assert.That(loaded.Inspect(motionId).hash+".motion.glb",Is.EqualTo(Path.GetFileName(motionPath)));
                var modules=new ProgramModuleLibrary(stage.DirectoryPath);modules.Flush();Assert.That(modules.Inspect(moduleHash).Error,Is.Null);Assert.That(File.ReadAllText(sentinel),Is.EqualTo("current live room"));
            }NoStages();Assert.That(File.ReadAllText(sentinel),Is.EqualTo("current live room"));
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_WORKSPACE_ARCHIVE_EVIDENCE");if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.WriteAllBytes(Path.Combine(evidence,"workspace.zip"),output.ToArray());File.WriteAllText(Path.Combine(evidence,"receipt.json"),JsonConvert.SerializeObject(written,Formatting.Indented));}
        }
        [Test] public void SnapshotDetachesDocumentsAndDoesNotRebindMissingModels()
        {
            string missing=new string('c',64);var room=JObject.Parse(Encoding.UTF8.GetString(documents["room.v11.json"]));room["objects"][1]["modelHash"]=missing;documents["room.v11.json"]=Bytes(room.ToString(Formatting.None));
            var snapshot=Snapshot();Array.Fill(documents["room.v11.json"],(byte)'x');documents.Clear();using var output=new MemoryStream();var receipt=WorkspaceArchive.Write(output,snapshot);
            Assert.That(receipt.Summary.MissingModels,Is.EqualTo(new[]{missing}));using var staged=WorkspaceArchive.Stage(new MemoryStream(output.ToArray()),directory);Assert.That(staged.Receipt.Summary.MissingModels,Is.EqualTo(new[]{missing}));
            Assert.That(JObject.Parse(File.ReadAllText(Path.Combine(staged.DirectoryPath,"room.v11.json")))["objects"][1]["modelHash"].Value<string>(),Is.EqualTo(missing));
        }
        [Test] public void UnavailableBehaviourSourceSurvivesWithoutExecutingOrBlockingOtherStores()
        {
            var rules=new RuleDocument {sequences=new[]{new RuleSequence {id=new string('b',32),name="Future program",program="{\"version\":99,\"kept\":true}"}}};documents["behaviours.v2.json"]=Document(rules);
            using var stage=WorkspaceArchive.Stage(new MemoryStream(Archive()),directory);Assert.That(stage.Receipt.Summary.UnavailablePrograms,Is.EqualTo(1));
            var storage=new RuleStorage(stage.DirectoryPath);var restored=storage.Load(out _);Assert.That(storage.ReadOnly,Is.False);Assert.That(restored.sequences[0].program,Is.EqualTo(rules.sequences[0].program));Assert.That(restored.ProgramError(restored.sequences[0]),Is.Not.Null);
        }
        [Test] public void MotionTombstonesKeepTheirIdentityAndRequireExactPayloadInventory()
        {
            payloads.Remove(motionPath);Assert.Throws<InvalidDataException>(()=>Snapshot());var motions=JObject.Parse(Encoding.UTF8.GetString(documents["motions/motions.v2.json"]));motions["entries"][0]["removed"]=true;motions["entries"][0]["archived"]=true;documents["motions/motions.v2.json"]=Bytes(motions.ToString());
            using var stage=WorkspaceArchive.Stage(new MemoryStream(Archive()),directory);using var restored=new MotionLibrary(Path.Combine(stage.DirectoryPath,"motions"));Assert.That(restored.Inspect(motionId).removed,Is.True);Assert.That(restored.Inspect(motionId).id,Is.EqualTo(motionId));Assert.That(restored.PayloadPresent(motionId),Is.False);Assert.That(stage.Receipt.Summary.MissingMotions,Is.EqualTo(new[]{motionId}));
        }
        [TestCase("../outside.json")][TestCase("/room.v11.json")][TestCase("models/../../outside.glb")][TestCase("room.v11.json.backup")][TestCase("action-receipts.v1.json")][TestCase("MODELS/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.glb")]
        public void UnknownOrUnsafePathsAreRejectedBeforeStaging(string path)
        {
            var entries=Entries(Archive());entries[path]=Bytes("not accepted");Assert.Throws<InvalidDataException>(()=>WorkspaceArchive.Stage(new MemoryStream(Zip(entries)),directory));NoStages();
        }
        [Test] public void TamperedMissingExtraAndDuplicateEntriesNeverProduceAReadyStage()
        {
            var entries=Entries(Archive());entries["controls.v2.json"][0]=(byte)'!';Assert.Throws<InvalidDataException>(()=>WorkspaceArchive.Stage(new MemoryStream(Zip(entries)),directory));NoStages();
            entries=Entries(Archive());entries.Remove(motionPath);Assert.Throws<InvalidDataException>(()=>WorkspaceArchive.Stage(new MemoryStream(Zip(entries)),directory));NoStages();
            entries=Entries(Archive());entries["models/"+new string('d',64)+".glb"]=payloads.Values.First();Assert.Throws<InvalidDataException>(()=>WorkspaceArchive.Stage(new MemoryStream(Zip(entries)),directory));NoStages();
            entries=Entries(Archive());Assert.Throws<InvalidDataException>(()=>WorkspaceArchive.Stage(new MemoryStream(Zip(entries.Concat(new[]{entries.First()}))),directory));NoStages();
        }
        [Test] public void ValidManifestHashesCannotHideAnInvalidModelAndPartialStageIsRemoved()
        {
            var entries=Entries(Archive());string old="models/"+modelHash+".glb";byte[] bad=Bytes("Not a GLB but with a matching cryptographic digest");string changed="models/"+ModelLibrary.Hash(bad)+".glb";
            entries.Remove(old);entries[changed]=bad;entries.Remove("models/"+modelHash+".txt");
            var manifest=JObject.Parse(Encoding.UTF8.GetString(entries["manifest.json"]));var list=(JArray)manifest["entries"];list.Single(x=>(string)x["path"]=="models/"+modelHash+".txt").Remove();var item=list.Single(x=>(string)x["path"]==old);item["path"]=changed;item["sha256"]=ModelLibrary.Hash(bad);item["bytes"]=bad.Length;entries["manifest.json"]=Bytes(manifest.ToString(Formatting.None));
            Assert.Throws<ModelImportException>(()=>WorkspaceArchive.Stage(new MemoryStream(Zip(entries)),directory));NoStages();
        }
        [Test] public void ManifestDuplicatesVersionsLengthsAndDirectoryCountsAreBounded()
        {
            byte[] good=Archive();var entries=Entries(good);string original=Encoding.UTF8.GetString(entries["manifest.json"]);
            entries["manifest.json"]=Bytes(original.Replace("\"version\":10","\"version\":10,\"version\":10"));Assert.Throws<JsonReaderException>(()=>WorkspaceArchive.Stage(new MemoryStream(Zip(entries)),directory));NoStages();
            entries["manifest.json"]=Bytes(original.Replace("\"version\":10","\"version\":99"));Assert.Throws<InvalidDataException>(()=>WorkspaceArchive.Stage(new MemoryStream(Zip(entries)),directory));NoStages();
            var manifest=JObject.Parse(original);manifest["entries"][0]["bytes"]=long.MaxValue;entries["manifest.json"]=Bytes(manifest.ToString());Assert.Throws<InvalidDataException>(()=>WorkspaceArchive.Stage(new MemoryStream(Zip(entries)),directory));NoStages();
            good[good.Length-22+10]=0xff;good[good.Length-22+11]=0xff;Assert.Throws<InvalidDataException>(()=>WorkspaceArchive.Stage(new MemoryStream(good),directory));NoStages();
        }
        [Test] public void InvalidDocumentsAndMotionMetadataFailBeforeAReceipt()
        {
            var old=documents["controls.v2.json"];documents["controls.v2.json"]=new byte[]{0xff};Assert.Throws<DecoderFallbackException>(()=>Snapshot());documents["controls.v2.json"]=old;
            var motions=JObject.Parse(Encoding.UTF8.GetString(documents["motions/motions.v2.json"]));motions["entries"][0]["rigHash"]=new string('a',64);documents["motions/motions.v2.json"]=Bytes(motions.ToString());
            using var output=new MemoryStream();Assert.Throws<InvalidDataException>(()=>WorkspaceArchive.Write(output,Snapshot()));Assert.That(output.CanWrite,Is.True);
        }
        [Test] public void CancellationAndOutputFailureNeverPublishOrActivate()
        {
            byte[] archive=Archive();using var cancelled=new CancellationTokenSource();cancelled.Cancel();using var output=new MemoryStream();Assert.Throws<OperationCanceledException>(()=>WorkspaceArchive.Write(output,Snapshot(),cancelled.Token));Assert.That(output.Length,Is.Zero);
            Assert.Throws<OperationCanceledException>(()=>WorkspaceArchive.Stage(new MemoryStream(archive),directory,cancelled.Token));NoStages();
            using var broken=new FailingStream();Assert.Throws<IOException>(()=>WorkspaceArchive.Write(broken,Snapshot()));Assert.That(broken.CanWrite,Is.True);
        }
        [Test] public async Task ModelCaptureHoldsImportsUntilItsExactAssetReadersHaveFinished()
        {
            var library=new ModelLibrary(Path.Combine(directory,"models"));await library.SaveAsync(ModelLibrary.Inspect("Original",payloads["models/"+modelHash+".glb"]));
            Assert.That(library.TryCaptureArchive(out var capture),Is.True);
            var changed=ModelLibrary.Inspect("Later",ModelFixture.Mixamo(json=>json["nodes"][5]["translation"][1]=.15));
            var queued=library.SaveAsync(changed);Assert.That(queued.IsCompleted,Is.False);Assert.That(library.TryCaptureArchive(out _),Is.False);
            try{
                var docs=new Dictionary<string,byte[]>();var assets=new Dictionary<string,Func<Stream>>();capture.Collect(docs,assets);Assert.That(assets.Count,Is.EqualTo(1));Assert.That(assets.ContainsKey("models/"+modelHash+".glb"),Is.True);
                using var stream=assets.Values.Single()();using var bytes=new MemoryStream();stream.CopyTo(bytes);Assert.That(ModelLibrary.Hash(bytes.ToArray()),Is.EqualTo(modelHash));
            }finally{capture.Dispose();capture.Dispose();}
            await queued;Assert.That(library.TryCaptureArchive(out var after),Is.True);using(after){var docs=new Dictionary<string,byte[]>();var assets=new Dictionary<string,Func<Stream>>();after.Collect(docs,assets);Assert.That(assets.Count,Is.EqualTo(2));}
        }
        [Test] public async Task MotionCaptureHoldsRemovalUntilArchiveCompletionAndKeepsOriginalIds()
        {
            using var library=new MotionLibrary(Path.Combine(directory,"source-motions"));await library.ArchiveAsync(motionId,true);Assert.That(library.TryCaptureArchive(out var capture),Is.True);
            var removal=library.RemoveDownloadAsync(motionId,()=>Task.FromResult<string>(null));Assert.That(removal.IsCompleted,Is.False);Assert.That(library.PayloadPresent(motionId),Is.True);
            try{
                var docs=documents.Where(x=>x.Key!="motions/motions.v2.json").ToDictionary(x=>x.Key,x=>x.Value);var assets=payloads.Where(x=>x.Key!=motionPath).ToDictionary(x=>x.Key,x=>(Func<Stream>)(()=>new MemoryStream(x.Value,false)));
                capture.Collect(docs,assets);using var archive=new MemoryStream();WorkspaceArchive.Write(archive,new WorkspaceArchiveSnapshot(docs,assets));
                using var stage=WorkspaceArchive.Stage(new MemoryStream(archive.ToArray()),directory);using var restored=new MotionLibrary(Path.Combine(stage.DirectoryPath,"motions"));Assert.That(restored.Inspect(motionId).archived,Is.True);Assert.That(restored.Downloaded(motionId),Is.True);
            }finally{capture.Dispose();}
            await removal;Assert.That(library.PayloadPresent(motionId),Is.False);Assert.That(library.Inspect(motionId).removed,Is.True);
        }
        sealed class FailingStream:MemoryStream {public override void Write(byte[] bytes,int offset,int count){if(Length+count>100)throw new IOException("Injected storage failure");base.Write(bytes,offset,count);}}
    }
}
