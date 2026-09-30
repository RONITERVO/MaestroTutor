// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Maestro.Quest.Persistence;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class WorkspaceGenerationTests
    {
        string directory,root;WorkspaceGenerationStore store;byte[] archive;string modelHash;
        static byte[] Json(object value)=>new UTF8Encoding(false,true).GetBytes(JsonUtility.ToJson(value));
        [SetUp] public void Setup()
        {
            directory=Path.Combine(Path.GetTempPath(),"MaestroGeneration-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);root=Path.Combine(directory,"workspace-generations.v1");store=new WorkspaceGenerationStore(directory);
            Directory.CreateDirectory(Path.Combine(directory,"room"));File.WriteAllText(Path.Combine(directory,"room","keep.txt"),"Original workspace");File.WriteAllText(Path.Combine(directory,"room","action-receipts.v1.json"),"Original receipts stay private");
            var model=ModelFixture.Mixamo();modelHash=ModelLibrary.Hash(model);
            var docs=new Dictionary<string,byte[]> {
                ["room.v2.json"]=Json(new RoomDocument {version=2,objects=new[]{new RoomObjectData {id="book",kind=RoomObjectKind.Book},new RoomObjectData {id="maestro",kind=RoomObjectKind.Maestro,modelHash=modelHash}}}),
                ["behaviours.v2.json"]=Json(new RuleDocument()),["controls.v2.json"]=Json(new ControllerPreferences()),["avatar-activities.v2.json"]=Json(new AvatarActivityDocument()),
                ["motions/motions.v2.json"]=Encoding.UTF8.GetBytes("{\"version\":2,\"entries\":[],\"sources\":[]}")};
            var assets=new Dictionary<string,Func<Stream>> {["models/"+modelHash+".glb"]=()=>new MemoryStream(model,false)};
            using var output=new MemoryStream();WorkspaceArchive.Write(output,new WorkspaceArchiveSnapshot(docs,assets));archive=output.ToArray();
        }
        [TearDown] public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        PreparedWorkspaceGeneration Prepare()=>store.Prepare(new MemoryStream(archive,false));
        string Generation(string id)=>Path.Combine(root,"generations",id);
        string Pointer=>Path.Combine(root,"current.v1.json");
        [Test] public void PrepareDoesNotChangeOriginalAndActivationOpensCompleteNativeStoresWithFreshReceipts()
        {
            var initial=store.Load();Assert.That(initial.Revision,Is.EqualTo("initial"));var prepared=Prepare();Assert.That(store.Load().Revision,Is.EqualTo(initial.Revision));Assert.That(File.Exists(Pointer),Is.True,"The original selection is persisted before a switch can be attempted");
            Assert.That(store.InspectPrepared(prepared.Id,prepared.Receipt.ManifestHash).Receipt.Summary.Models,Is.EqualTo(1));
            var active=store.Activate(prepared.Id,prepared.Receipt.ManifestHash,initial.Revision);Assert.That(active.Active.ReviewRequired,Is.True);Assert.That(active.Previous.Generation,Is.EqualTo("original"));
            var reopened=new WorkspaceGenerationStore(directory);var selected=reopened.Load();Assert.That(selected.Revision,Is.EqualTo(active.Revision));Assert.That(selected.Active.ReviewRequired,Is.True);
            string data=reopened.DataDirectory(selected.Active);var room=new RoomStorage(data).Load(out var error);Assert.That(error,Is.Null);Assert.That(room.objects.Single(x=>x.id=="maestro").modelHash,Is.EqualTo(modelHash));Assert.That(new RuleStorage(data).Load(out error).sequences,Is.Empty);Assert.That(error,Is.Null);
            Assert.That(new ModelLibrary(Path.Combine(data,"models")).ReadAsync(modelHash).GetAwaiter().GetResult().Hash,Is.EqualTo(modelHash));
            Assert.That(reopened.ReceiptDirectory(selected.Active),Does.Contain(selected.Active.ReceiptEpoch));Assert.That(File.Exists(Path.Combine(data,"action-receipts.v1.json")),Is.False);Assert.That(File.ReadAllText(Path.Combine(directory,"room","keep.txt")),Is.EqualTo("Original workspace"));
            Assert.That(File.ReadAllText(Path.Combine(directory,"room","action-receipts.v1.json")),Is.EqualTo("Original receipts stay private"));
            Assert.That(store.Activate(prepared.Id,prepared.Receipt.ManifestHash,initial.Revision).Revision,Is.EqualTo(active.Revision),"Exact activation retry reconciles without switching twice");
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_WORKSPACE_GENERATION_EVIDENCE");if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"selection.json"),active.Json().ToString());File.WriteAllText(Path.Combine(evidence,"prepared-manifest.json"),File.ReadAllText(Path.Combine(Generation(prepared.Id),"manifest.json")));}
        }
        [Test] public void ReviewAndRecoveryPersistWithoutRewritingProgramsOrReusingReceipts()
        {
            var p=Prepare();var selected=store.Activate(p.Id,p.Receipt.ManifestHash,"initial");string data=store.DataDirectory(selected.Active);var original=File.ReadAllBytes(Path.Combine(data,"behaviours.v2.json"));
            var reviewed=store.CompleteReview(selected.Revision);Assert.That(reviewed.Active.ReviewRequired,Is.False);Assert.That(reviewed.Active.ReceiptEpoch,Is.EqualTo(selected.Active.ReceiptEpoch));Assert.That(store.Load().Active.ReviewRequired,Is.False);
            File.WriteAllText(Path.Combine(data,"accepted-later.txt"),"Preserve authored changes");var back=store.RestorePrevious(reviewed.Revision);Assert.That(back.Active.Generation,Is.EqualTo("original"));Assert.That(back.Active.ReviewRequired,Is.True);Assert.That(back.Active.ReceiptEpoch,Is.Not.EqualTo("original"));
            var forward=store.RestorePrevious(back.Revision);Assert.That(forward.Active.Generation,Is.EqualTo(p.Id));Assert.That(forward.Active.ReviewRequired,Is.True);Assert.That(forward.Active.ReceiptEpoch,Is.Not.EqualTo(selected.Active.ReceiptEpoch));Assert.That(File.ReadAllText(Path.Combine(data,"accepted-later.txt")),Is.EqualTo("Preserve authored changes"));Assert.That(File.ReadAllBytes(Path.Combine(data,"behaviours.v2.json")),Is.EqualTo(original));
            Assert.Throws<InvalidDataException>(()=>store.InspectPrepared(p.Id,p.Receipt.ManifestHash),"An authored workspace cannot be reused as an untouched import preview");
        }
        [TestCase("activation.reserved",false)] [TestCase("pointer.beforeCommit",false)] [TestCase("pointer.afterCommit",true)]
        public void InterruptedActivationLeavesExactlyOldOrNewSelectionAndRetryUsesSameIdentity(string point,bool committed)
        {
            var p=Prepare();var interrupted=new WorkspaceGenerationStore(directory,phase=>{if(phase==point)throw new IOException("Simulated interruption");});Assert.Throws<IOException>(()=>interrupted.Activate(p.Id,p.Receipt.ManifestHash,"initial"));
            var after=new WorkspaceGenerationStore(directory).Load();Assert.That(after.Active.Generation,Is.EqualTo(committed?p.Id:"original"));
            var saved=JObject.Parse(File.ReadAllText(Path.Combine(Generation(p.Id),"activation.v1.json")));var result=store.Activate(p.Id,p.Receipt.ManifestHash,"initial");Assert.That(result.Revision,Is.EqualTo((string)saved["next"]["revision"]));Assert.That(result.Active.ReviewRequired,Is.True);Assert.That(File.Exists(Path.Combine(directory,"room","keep.txt")),Is.True);
        }
        [TestCase("prepare.verified")] [TestCase("prepare.contentMoved")] [TestCase("prepare.ready")]
        public void FailedPreparationCleansOnlyItsGenerationAndNeverChangesSelection(string point)
        {
            var interrupted=new WorkspaceGenerationStore(directory,phase=>{if(phase==point)throw new IOException("Simulated preparation failure");});Assert.Throws<IOException>(()=>interrupted.Prepare(new MemoryStream(archive,false)));Assert.That(store.Load().Revision,Is.EqualTo("initial"));Assert.That(Directory.GetDirectories(Path.Combine(root,"generations")),Is.Empty);Assert.That(Directory.GetDirectories(Path.Combine(root,"staging")),Is.Empty);Assert.That(File.Exists(Path.Combine(directory,"room","keep.txt")),Is.True);
        }
        [TestCase("content")] [TestCase("inventory")] [TestCase("manifest")]
        public void ChangedPreviewCannotBecomeActive(string part)
        {
            var p=Prepare();string target=Generation(p.Id);
            if(part=="content")File.WriteAllText(Path.Combine(target,"data","room.v2.json"),"{}");
            if(part=="inventory")File.WriteAllText(Path.Combine(target,"data","foreign.txt"),"not in preview");
            if(part=="manifest")File.AppendAllText(Path.Combine(target,"manifest.json")," ");
            Assert.Throws<InvalidDataException>(()=>store.Activate(p.Id,p.Receipt.ManifestHash,"initial"));Assert.That(store.Load().Revision,Is.EqualTo("initial"));Assert.That(File.Exists(Path.Combine(target,"activation.v1.json")),Is.False);
        }
        [Test] public void StaleReviewOrPreviewCannotOverwriteANewerSelection()
        {
            var first=Prepare();var second=Prepare();var active=store.Activate(first.Id,first.Receipt.ManifestHash,"initial");
            Assert.Throws<InvalidDataException>(()=>store.Activate(second.Id,second.Receipt.ManifestHash,"initial"));Assert.Throws<InvalidDataException>(()=>store.CompleteReview("initial"));Assert.Throws<InvalidDataException>(()=>store.RestorePrevious("initial"));
            Assert.That(store.Load().Revision,Is.EqualTo(active.Revision));Assert.That(store.InspectPrepared(second.Id,second.Receipt.ManifestHash).Id,Is.EqualTo(second.Id));
            store.DiscardPrepared(second.Id,second.Receipt.ManifestHash);Assert.That(Directory.Exists(Generation(second.Id)),Is.False);Assert.Throws<InvalidDataException>(()=>store.DiscardPrepared(first.Id,first.Receipt.ManifestHash));
        }
        [Test] public void CorruptPointerCannotSilentlyFallBackToOriginalOrBackup()
        {
            var p=Prepare();var selected=store.Activate(p.Id,p.Receipt.ManifestHash,"initial");store.CompleteReview(selected.Revision);Assert.That(File.Exists(Pointer+".previous"),Is.True);
            File.WriteAllText(Pointer,"{\"version\":1,\"version\":2}");Assert.That(()=>new WorkspaceGenerationStore(directory).Load(),Throws.Exception);Assert.That(File.Exists(Pointer+".previous"),Is.True);
            File.Delete(Pointer);Assert.Throws<InvalidDataException>(()=>store.Load());Assert.That(File.Exists(Path.Combine(directory,"room","keep.txt")),Is.True);
        }
        [TestCase(false)] [TestCase(true)] public void LostFirstSelectionNeverLooksLikeAFreshInstall(bool removeBackup)
        {
            var p=Prepare();store.Activate(p.Id,p.Receipt.ManifestHash,"initial");Assert.That(File.Exists(Pointer+".previous"),Is.True);File.Delete(Pointer);if(removeBackup)File.Delete(Pointer+".previous");
            Assert.Throws<InvalidDataException>(()=>new WorkspaceGenerationStore(directory).Load());Assert.Throws<InvalidDataException>(()=>store.CompleteReview("initial"));Assert.That(File.Exists(Pointer),Is.False);Assert.That(File.Exists(Path.Combine(directory,"room","keep.txt")),Is.True);
        }
        [Test] public void MissingPreviousDoesNotHideHealthyActiveWorkspaceButCannotBeRestoredAsEmpty()
        {
            var p=Prepare();var selected=store.Activate(p.Id,p.Receipt.ManifestHash,"initial");Directory.Delete(Path.Combine(directory,"room"),true);Assert.That(store.Load().Active.Generation,Is.EqualTo(p.Id));Assert.Throws<InvalidDataException>(()=>store.RestorePrevious(selected.Revision));Assert.That(store.Load().Revision,Is.EqualTo(selected.Revision));
        }
        [Test] public void AlteredActivationIdentityCannotMasqueradeAsACompletedSwitch()
        {
            var first=Prepare();var active=store.Activate(first.Id,first.Receipt.ManifestHash,"initial");var second=Prepare();
            var interrupted=new WorkspaceGenerationStore(directory,phase=>{if(phase=="activation.reserved")throw new IOException("Interrupted");});Assert.Throws<IOException>(()=>interrupted.Activate(second.Id,second.Receipt.ManifestHash,active.Revision));
            string path=Path.Combine(Generation(second.Id),"activation.v1.json");var record=JObject.Parse(File.ReadAllText(path));record["next"]["revision"]=active.Revision;File.WriteAllText(path,record.ToString());
            Assert.Throws<InvalidDataException>(()=>store.Activate(second.Id,second.Receipt.ManifestHash,active.Revision));Assert.That(store.Load().Active.Generation,Is.EqualTo(first.Id));
        }
        [TestCase("pointer.beforeCommit",false)] [TestCase("pointer.afterCommit",true)]
        public void InterruptedRecoveryKeepsCompleteSelectedWorkspaceAndFreshReviewBoundary(string point,bool committed)
        {
            var p=Prepare();var active=store.Activate(p.Id,p.Receipt.ManifestHash,"initial");var interrupted=new WorkspaceGenerationStore(directory,phase=>{if(phase==point)throw new IOException("Interrupted recovery");});Assert.Throws<IOException>(()=>interrupted.RestorePrevious(active.Revision));
            var selected=store.Load();Assert.That(selected.Active.Generation,Is.EqualTo(committed?"original":p.Id));Assert.That(selected.Active.ReviewRequired,Is.True);Assert.That(File.Exists(Path.Combine(store.DataDirectory(selected.Active),committed?"keep.txt":"room.v2.json")),Is.True);
            if(committed)Assert.That(selected.Active.ReceiptEpoch,Is.Not.EqualTo("original"));else Assert.That(selected.Revision,Is.EqualTo(active.Revision));
        }
        [Test] public void WriterExclusionAndCancelledPreparationLeaveSelectionUnchanged()
        {
            var p=Prepare();using(var held=new FileStream(Path.Combine(root,"writer.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None)){Assert.Throws<IOException>(()=>store.Activate(p.Id,p.Receipt.ManifestHash,"initial"));}
            using var cancellation=new CancellationTokenSource();cancellation.Cancel();Assert.Throws<OperationCanceledException>(()=>store.Prepare(new MemoryStream(archive,false),cancellation.Token));Assert.That(store.Load().Revision,Is.EqualTo("initial"));
        }
        [Test] public void NewerOrInvalidSelectionPathsAreRefusedWithoutTouchingData()
        {
            Prepare();File.WriteAllText(Path.Combine(root,"current.v2.json"),"{}");Assert.Throws<InvalidDataException>(()=>store.Load());File.Delete(Path.Combine(root,"current.v2.json"));
            File.Delete(Pointer);Directory.CreateDirectory(Pointer);Assert.Throws<InvalidDataException>(()=>store.Load());Directory.Delete(Pointer);
            var pointer=new JObject {["version"]=1,["revision"]=Guid.NewGuid().ToString("N"),["active"]=new JObject {["generation"]="../escape",["receiptEpoch"]="original",["reviewRequired"]=false},["previous"]=null};File.WriteAllText(Pointer,pointer.ToString());Assert.Throws<InvalidDataException>(()=>store.Load());
        }
    }
}
