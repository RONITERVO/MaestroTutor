// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed partial class WorkspaceGenerationTests
    {
        static void ChangeSavedName(string data,string name){string path=Path.Combine(data,"room.v16.json");var room=JObject.Parse(File.ReadAllText(path));room["objects"][1]["name"]=name;File.WriteAllText(path,room.ToString());}
        [Test] public void SavedRecoveryCapturesLatestRoomAndMemoryAndKeepsOriginalManifestAndPrivateEvidence()
        {
            var source=Prepare();string data=Path.Combine(Generation(source.Id),"data");byte[] originalManifest=File.ReadAllBytes(Path.Combine(Generation(source.Id),"manifest.json"));
            ChangeSavedName(data,"Saved after import");string program=new string('a',32),cell=new string('b',32);
            var memory=ProgramMemoryDocument.Empty().WithValues(program,new Dictionary<string,ProgramMemoryDocument.Cell>{[cell]=new("count",new ProgramValue(17d))});File.WriteAllBytes(Path.Combine(data,ProgramMemoryStore.FileName),memory.Encode());
            File.WriteAllText(Path.Combine(data,"private-receipts.json"),"Keep private history");using(RoomSnapshotTransaction.Enter(data)){}
            DamageSelection();string origin=DamageOrigin();var preview=store.PrepareDamagedRecovery(origin,source.Id,source.Receipt.ManifestHash);string copied=Path.Combine(Generation(preview.Id),"data");
            Assert.That(preview.Receipt.ManifestHash,Is.Not.EqualTo(source.Receipt.ManifestHash));Assert.That(store.InspectDamagedPreview(preview.Id,preview.Receipt.ManifestHash,origin),Is.Not.Null);
            Assert.That(File.ReadAllText(Path.Combine(copied,"room.v16.json")),Does.Contain("Saved after import"));Assert.That(ProgramMemoryStore.ReadSaved(copied).Identity,Is.EqualTo(memory.Identity));Assert.That(File.Exists(Path.Combine(copied,"private-receipts.json")),Is.False);
            var proof=JObject.Parse(File.ReadAllText(Path.Combine(Generation(preview.Id),"damaged-recovery.v1.json")));Assert.That((int)proof["version"],Is.EqualTo(2));Assert.That((string)proof["source"]["originalManifestHash"],Is.EqualTo(source.Receipt.ManifestHash));Assert.That((string)proof["source"]["fingerprint"],Has.Length.EqualTo(64));Assert.That((int)proof["source"]["excludedFiles"],Is.EqualTo(2));
            ChangeSavedName(data,"Edited after preview");var selected=store.CommitDamagedRecovery(preview.Id,preview.Receipt.ManifestHash,origin,PreserveFor(preview,origin));
            Assert.That(selected.Active.ReviewRequired,Is.True);Assert.That(File.ReadAllText(Path.Combine(copied,"room.v16.json")),Does.Contain("Saved after import"));Assert.That(File.ReadAllText(Path.Combine(data,"room.v16.json")),Does.Contain("Edited after preview"));Assert.That(File.ReadAllBytes(Path.Combine(Generation(source.Id),"manifest.json")),Is.EqualTo(originalManifest));Assert.That(File.ReadAllText(Path.Combine(data,"private-receipts.json")),Is.EqualTo("Keep private history"));
        }
        [TestCase("room.v16.json")] [TestCase("room.v17.json")] [TestCase("program-memory.v1.json.backup")] [TestCase("room-snapshot.v15.json")]
        public void SavedRecoveryNeverDefaultsDamagedMissingOrUnfinishedSavedContent(string name)
        {
            var source=Prepare();string data=Path.Combine(Generation(source.Id),"data"),path=Path.Combine(data,name);File.WriteAllText(path,"Preserve unavailable content");DamageSelection();string origin=DamageOrigin();
            Assert.That(()=>store.PrepareDamagedRecovery(origin,source.Id,source.Receipt.ManifestHash),Throws.Exception);Assert.That(File.ReadAllText(path),Is.EqualTo("Preserve unavailable content"));Assert.That(DamageOrigin(),Is.EqualTo(origin));Assert.That(Directory.GetDirectories(Path.Combine(root,"generations")),Has.Length.EqualTo(1));
        }
        [TestCase(false)] [TestCase(true)] public void ChangedOrCancelledSavedCaptureRemovesOnlyItsOwnPreview(bool cancel)
        {
            var source=Prepare();string data=Path.Combine(Generation(source.Id),"data");DamageSelection();string origin=DamageOrigin();using var cancellation=new CancellationTokenSource();
            var capturing=new WorkspaceGenerationStore(directory,point=>{if(point!="damage.saved")return;if(cancel)cancellation.Cancel();else ChangeSavedName(data,"Changed during capture");});
            if(cancel)Assert.Throws<OperationCanceledException>(()=>capturing.PrepareDamagedRecovery(origin,source.Id,source.Receipt.ManifestHash,cancellation.Token));
            else Assert.Throws<InvalidDataException>(()=>capturing.PrepareDamagedRecovery(origin,source.Id,source.Receipt.ManifestHash));
            Assert.That(Directory.GetDirectories(Path.Combine(root,"generations")),Has.Length.EqualTo(1));Assert.That(DamageOrigin(),Is.EqualTo(origin));Assert.That(Directory.Exists(Generation(source.Id)),Is.True);
        }
        [Test] public void SavedCaptureHoldsThePairAgainstConcurrentWritersWhileReadingItsExistingLock()
        {
            var source=Prepare();string data=Path.Combine(Generation(source.Id),"data");using(RoomSnapshotTransaction.Enter(data)){}ChangeSavedName(data,"Captured together");DamageSelection();string origin=DamageOrigin();
            using var started=new ManualResetEventSlim();using var entered=new ManualResetEventSlim();Task writer=null;
            var capturing=new WorkspaceGenerationStore(directory,point=>{if(point!="damage.saved")return;
                writer=Task.Run(()=>{started.Set();using var owner=RoomSnapshotTransaction.Enter(data);entered.Set();ChangeSavedName(data,"Later writer");});
                Assert.That(started.Wait(5000),Is.True);Assert.That(entered.Wait(100),Is.False,"A saved-room writer must wait for the complete captured pair.");
            });
            PreparedWorkspaceGeneration preview=null;try{preview=capturing.PrepareDamagedRecovery(origin,source.Id,source.Receipt.ManifestHash);}finally{if(writer!=null)Assert.That(writer.Wait(5000),Is.True);}
            Assert.That(File.ReadAllText(Path.Combine(Generation(preview.Id),"data","room.v16.json")),Does.Contain("Captured together"));Assert.That(File.ReadAllText(Path.Combine(data,"room.v16.json")),Does.Contain("Later writer"));
        }
        [TestCase("fingerprint")] [TestCase("originalManifestHash")] [TestCase("excludedFiles")]
        public void SavedRecoveryProofRejectsInvalidCaptureProvenance(string field)
        {
            var source=Prepare();DamageSelection();string origin=DamageOrigin();var preview=store.PrepareDamagedRecovery(origin,source.Id,source.Receipt.ManifestHash);string path=Path.Combine(Generation(preview.Id),"damaged-recovery.v1.json");var proof=JObject.Parse(File.ReadAllText(path));proof["source"][field]=field=="excludedFiles"?new JValue(4097):new JValue("invalid");File.WriteAllText(path,proof.ToString());Assert.Throws<InvalidDataException>(()=>store.InspectDamagedPreview(preview.Id,preview.Receipt.ManifestHash,origin));
        }
        [Test] public void HistoricalImmutableRecoveryProofRemainsInspectableWithoutRecapturingItsSource()
        {
            var source=Prepare();DamageSelection();string origin=DamageOrigin();var preview=store.PrepareDamagedRecovery(origin,source.Id,source.Receipt.ManifestHash);string path=Path.Combine(Generation(preview.Id),"damaged-recovery.v1.json");var proof=JObject.Parse(File.ReadAllText(path));proof["version"]=1;proof["source"]=new JObject {["kind"]="retained",["generationId"]=source.Id};File.WriteAllText(path,proof.ToString());ChangeSavedName(Path.Combine(Generation(source.Id),"data"),"Later original edit");Assert.That(store.InspectDamagedPreview(preview.Id,preview.Receipt.ManifestHash,origin),Is.Not.Null);
        }
    }
}
