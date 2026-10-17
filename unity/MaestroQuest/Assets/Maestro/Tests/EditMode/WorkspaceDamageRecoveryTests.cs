// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Threading;
using Maestro.Quest.Imports;
using Maestro.Quest.Persistence;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed partial class WorkspaceGenerationTests
    {
        static JObject RecoveryAccepted()=>new() {["version"]=1,["available"]=new JObject {["room"]=false,["behaviours"]=false,["controls"]=false,["activities"]=false},["room"]=new JObject(),["behaviours"]=new JObject(),["controls"]=new JObject(),["activities"]=new JObject(),["temporaryRoom"]=JValue.CreateNull()};
        string DamageOrigin()=>(string)store.InspectRecovery()["originHash"];
        void DamageSelection(){File.WriteAllBytes(Pointer,new byte[]{0xff,0xfe,123,0});File.WriteAllBytes(Pointer+".previous",Array.Empty<byte>());}
        CapturedRecoveryEvidence PreserveFor(PreparedWorkspaceGeneration preview,string origin)=>WorkspaceRecoveryEvidence.Write(Path.Combine(directory,"room"),store.RecoveryEvidenceDirectory(preview.Id,preview.Receipt.ManifestHash,origin),WorkspaceRecoveryEvidence.EncodeAccepted(RecoveryAccepted()));
        [Test] public void RecoveryInspectionIsReadOnlyAndReportsDamagedCandidateMetadataWithoutHidingOtherChoices()
        {
            string empty=Path.Combine(directory,"unused-recovery-read");var clean=new WorkspaceGenerationStore(empty).InspectRecovery();Assert.That((bool)clean["selectionReadable"],Is.True);Assert.That((JArray)clean["candidates"],Is.Empty);Assert.That(Directory.Exists(empty),Is.False);
            var good=Prepare();var bad=Prepare();File.WriteAllText(Path.Combine(Generation(bad.Id),"generation.v1.json"),"broken");DamageSelection();byte[] bytes=File.ReadAllBytes(Pointer);
            var inspected=store.InspectRecovery();Assert.That((bool)inspected["selectionReadable"],Is.False);Assert.That(((JArray)inspected["candidates"]).Count,Is.EqualTo(2));Assert.That((bool)inspected["candidates"].Single(x=>(string)x["generationId"]==good.Id)["available"],Is.True);Assert.That((bool)inspected["candidates"].Single(x=>(string)x["generationId"]==bad.Id)["available"],Is.False);Assert.That(File.ReadAllBytes(Pointer),Is.EqualTo(bytes));
            File.WriteAllText(Pointer+".previous","different damaged backup");Assert.That(DamageOrigin(),Is.Not.EqualTo((string)inspected["originHash"]));
        }
        [Test] public void RecoveryPreviewPreservesExactUnreadableSelectionBytesAndCopiesVerifiedCandidateWithoutOrdinaryActivation()
        {
            var source=Prepare();DamageSelection();string origin=DamageOrigin();byte[] current=File.ReadAllBytes(Pointer),backup=File.ReadAllBytes(Pointer+".previous");var preview=store.PrepareDamagedRecovery(origin,source.Id,source.Receipt.ManifestHash);
            Assert.That(preview.Id,Is.Not.EqualTo(source.Id));Assert.That(preview.Receipt.Summary.Models,Is.EqualTo(1));Assert.That(File.ReadAllBytes(Pointer),Is.EqualTo(current));Assert.That(File.ReadAllBytes(Pointer+".previous"),Is.EqualTo(backup));
            Assert.That(File.ReadAllBytes(Path.Combine(Generation(preview.Id),"origin-current.bin")),Is.EqualTo(current));Assert.That(File.ReadAllBytes(Path.Combine(Generation(preview.Id),"origin-previous.bin")),Is.EqualTo(backup));Assert.That(store.InspectDamagedPreview(preview.Id,preview.Receipt.ManifestHash,origin).Receipt.Summary.Models,Is.EqualTo(1));
            string cloned=Path.Combine(Generation(preview.Id),"data","models",modelHash+".glb");Assert.That(File.ReadAllBytes(cloned),Is.EqualTo(File.ReadAllBytes(Path.Combine(Generation(source.Id),"data","models",modelHash+".glb"))));
            Assert.Throws<InvalidDataException>(()=>store.InspectPrepared(preview.Id,preview.Receipt.ManifestHash));
            store.DiscardDamagedPreview(preview.Id,preview.Receipt.ManifestHash);Assert.That(Directory.Exists(Generation(preview.Id)),Is.False);Assert.That(Directory.Exists(Generation(source.Id)),Is.True);Assert.That(File.ReadAllBytes(Pointer),Is.EqualTo(current));
        }
        [Test] public void MissingSelectionRecoveryNeverInitializesAFallbackAndCanCommitOnlyAfterPrivateEvidenceIsPreserved()
        {
            var source=Prepare();var selected=Activate(store,source.Id,source.Receipt.ManifestHash,"initial");File.Delete(Pointer);Assert.Throws<InvalidDataException>(()=>store.Load());string origin=DamageOrigin();var preview=store.PrepareDamagedRecovery(origin,selected.Previous.Generation,source.Receipt.ManifestHash);Assert.That(File.Exists(Pointer),Is.False);
            Assert.Throws<InvalidDataException>(()=>store.CommitDamagedRecovery(preview.Id,preview.Receipt.ManifestHash,origin,null));Assert.That(File.Exists(Pointer),Is.False);var evidence=PreserveFor(preview,origin);var next=store.CommitDamagedRecovery(preview.Id,preview.Receipt.ManifestHash,origin,evidence);
            Assert.That(next.Active.Generation,Is.EqualTo(preview.Id));Assert.That(next.Active.ReviewRequired,Is.True);Assert.That(next.Active.ReceiptEpoch,Is.Not.EqualTo(selected.Active.ReceiptEpoch));Assert.That(next.Previous,Is.Null,"Damaged roots remain protected evidence, not an implicitly valid previous snapshot");Assert.That(new WorkspaceGenerationStore(directory).Load().Revision,Is.EqualTo(next.Revision));Assert.That(File.Exists(evidence.Path),Is.True);Assert.That(File.ReadAllText(Path.Combine(directory,"room","keep.txt")),Is.EqualTo("Original workspace"));
            Assert.That(store.CommitDamagedRecovery(preview.Id,preview.Receipt.ManifestHash,origin,evidence).Revision,Is.EqualTo(next.Revision));Assert.That(store.CommittedDamagedRecovery(preview.Id,preview.Receipt.ManifestHash,origin).Revision,Is.EqualTo(next.Revision));Assert.Throws<InvalidDataException>(()=>store.DiscardPrepared(source.Id,source.Receipt.ManifestHash));
            string export=Environment.GetEnvironmentVariable("MAESTRO_DAMAGE_RECOVERY_EVIDENCE");if(!string.IsNullOrEmpty(export)){Directory.CreateDirectory(export);File.WriteAllText(Path.Combine(export,"selection.json"),next.Json().ToString());File.Copy(Path.Combine(Generation(preview.Id),"damaged-recovery.v1.json"),Path.Combine(export,"proof.json"),true);File.Copy(Path.Combine(Generation(preview.Id),"damaged-commit.v1.json"),Path.Combine(export,"commit.json"),true);File.Copy(evidence.Path,Path.Combine(export,"preserved.zip"),true);File.Copy(Path.Combine(Generation(preview.Id),"origin-previous.bin"),Path.Combine(export,"origin-previous.bin"),true);}
        }
        [TestCase("current")] [TestCase("previous")]
        public void ChangedSelectionEvidenceRejectsBothPreparationAndCommitWithoutOverwritingOriginals(string part)
        {
            var source=Prepare();DamageSelection();string origin=DamageOrigin();var preview=store.PrepareDamagedRecovery(origin,source.Id,source.Receipt.ManifestHash);var evidence=PreserveFor(preview,origin);string path=part=="current"?Pointer:Pointer+".previous";File.WriteAllText(path,"Changed after inspection");
            Assert.Throws<InvalidDataException>(()=>store.PrepareDamagedRecovery(origin,source.Id,source.Receipt.ManifestHash));Assert.Throws<InvalidDataException>(()=>store.InspectDamagedPreview(preview.Id,preview.Receipt.ManifestHash,origin));Assert.Throws<InvalidDataException>(()=>store.CommitDamagedRecovery(preview.Id,preview.Receipt.ManifestHash,origin,evidence));Assert.That(File.ReadAllText(path),Is.EqualTo("Changed after inspection"));Assert.That(File.Exists(Path.Combine(Generation(preview.Id),"damaged-commit.v1.json")),Is.False);
        }
        [Test] public void ForeignOrChangedEvidenceCannotBeSubstitutedIntoARecoveryCommit()
        {
            var source=Prepare();DamageSelection();string origin=DamageOrigin();var preview=store.PrepareDamagedRecovery(origin,source.Id,source.Receipt.ManifestHash);var foreign=WorkspaceRecoveryEvidence.Write(Path.Combine(directory,"room"),Path.Combine(directory,"foreign-evidence"),WorkspaceRecoveryEvidence.EncodeAccepted(RecoveryAccepted()));
            Assert.Throws<InvalidDataException>(()=>store.CommitDamagedRecovery(preview.Id,preview.Receipt.ManifestHash,origin,foreign));var evidence=PreserveFor(preview,origin);File.AppendAllText(evidence.Path,"changed");Assert.Throws<InvalidDataException>(()=>store.CommitDamagedRecovery(preview.Id,preview.Receipt.ManifestHash,origin,evidence));Assert.That(File.Exists(Path.Combine(Generation(preview.Id),"damaged-commit.v1.json")),Is.False);Assert.That(DamageOrigin(),Is.EqualTo(origin));
        }
        [TestCase("damage.copied")] [TestCase("damage.ready")]
        public void InterruptedRecoveryPreparationRemovesOnlyItsPrivateCopy(string point)
        {
            var source=Prepare();DamageSelection();string origin=DamageOrigin();int count=Directory.GetDirectories(Path.Combine(root,"generations")).Length;var failing=new WorkspaceGenerationStore(directory,phase=>{if(phase==point)throw new IOException("Injected preparation fault");});
            Assert.Throws<IOException>(()=>failing.PrepareDamagedRecovery(origin,source.Id,source.Receipt.ManifestHash));Assert.That(Directory.GetDirectories(Path.Combine(root,"generations")).Length,Is.EqualTo(count));Assert.That(DamageOrigin(),Is.EqualTo(origin));Assert.That(Directory.Exists(Generation(source.Id)),Is.True);
        }
        [Test] public void CancelledRecoveryPreparationAndChangedSourceRetainTheSourceAndRawSelection()
        {
            var source=Prepare();DamageSelection();string origin=DamageOrigin();using var cancel=new CancellationTokenSource();var cancelling=new WorkspaceGenerationStore(directory,phase=>{if(phase=="damage.copied")cancel.Cancel();});Assert.Throws<OperationCanceledException>(()=>cancelling.PrepareDamagedRecovery(origin,source.Id,source.Receipt.ManifestHash,cancel.Token));
            string payload=Path.Combine(Generation(source.Id),"data","models",modelHash+".glb");File.AppendAllText(payload,"damage");Assert.Throws<InvalidDataException>(()=>store.PrepareDamagedRecovery(origin,source.Id,source.Receipt.ManifestHash));Assert.That(Directory.GetDirectories(Path.Combine(root,"generations")).Length,Is.EqualTo(1));Assert.That(DamageOrigin(),Is.EqualTo(origin));
        }
        [TestCase("damage.reserved",false)] [TestCase("pointer.beforeCommit",false)] [TestCase("pointer.afterCommit",true)]
        public void RecoveryCommitReconcilesExactIdentityAcrossInterruptionAndPreservesBothOriginFiles(string point,bool committed)
        {
            var source=Prepare();DamageSelection();byte[] original=File.ReadAllBytes(Pointer),backup=File.ReadAllBytes(Pointer+".previous");string origin=DamageOrigin();var preview=store.PrepareDamagedRecovery(origin,source.Id,source.Receipt.ManifestHash);var evidence=PreserveFor(preview,origin);var failing=new WorkspaceGenerationStore(directory,phase=>{if(phase==point)throw new IOException("Injected commit interruption");});
            Assert.Throws<IOException>(()=>failing.CommitDamagedRecovery(preview.Id,preview.Receipt.ManifestHash,origin,evidence));Assert.That(store.CommittedDamagedRecovery(preview.Id,preview.Receipt.ManifestHash,origin)!=null,Is.EqualTo(committed));Assert.Throws<InvalidDataException>(()=>store.DiscardDamagedPreview(preview.Id,preview.Receipt.ManifestHash));
            var next=store.CommitDamagedRecovery(preview.Id,preview.Receipt.ManifestHash,origin,evidence);Assert.That(store.CommittedDamagedRecovery(preview.Id,preview.Receipt.ManifestHash,origin).Revision,Is.EqualTo(next.Revision));Assert.That(File.ReadAllBytes(Path.Combine(Generation(preview.Id),"origin-current.bin")),Is.EqualTo(original));Assert.That(File.ReadAllBytes(Path.Combine(Generation(preview.Id),"origin-previous.bin")),Is.EqualTo(backup));Assert.That(File.ReadAllBytes(Pointer+".previous"),Is.EqualTo(original));
            Assert.Throws<InvalidDataException>(()=>store.CommittedDamagedRecovery(preview.Id,preview.Receipt.ManifestHash,new string('a',64)));Assert.Throws<InvalidDataException>(()=>store.DiscardPrepared(source.Id,source.Receipt.ManifestHash));
            var another=PreserveFor(preview,origin);Assert.Throws<InvalidDataException>(()=>store.CommitDamagedRecovery(preview.Id,preview.Receipt.ManifestHash,origin,another));
        }
        [Test] public void CancelledCommitKeepsOriginalSelectionAndPreservedEvidenceWithoutClaimingCompletion()
        {
            var source=Prepare();DamageSelection();string origin=DamageOrigin();var preview=store.PrepareDamagedRecovery(origin,source.Id,source.Receipt.ManifestHash);var evidence=PreserveFor(preview,origin);using var cancel=new CancellationTokenSource();var cancelling=new WorkspaceGenerationStore(directory,phase=>{if(phase=="damage.reserved")cancel.Cancel();});
            Assert.Throws<OperationCanceledException>(()=>cancelling.CommitDamagedRecovery(preview.Id,preview.Receipt.ManifestHash,origin,evidence,cancel.Token));Assert.That(DamageOrigin(),Is.EqualTo(origin));Assert.That(store.CommittedDamagedRecovery(preview.Id,preview.Receipt.ManifestHash,origin),Is.Null);Assert.That(File.Exists(evidence.Path),Is.True);Assert.Throws<InvalidDataException>(()=>store.DiscardDamagedPreview(preview.Id,preview.Receipt.ManifestHash));
        }
        [TestCase("proof")] [TestCase("origin")]
        public void MissingOrAlteredRecoveryProvenanceCannotBeUsedAsAnOrdinaryPreview(string change)
        {
            var source=Prepare();string origin=DamageOrigin();var preview=store.PrepareDamagedRecovery(origin,source.Id,source.Receipt.ManifestHash);
            if(change=="proof")File.Delete(Path.Combine(Generation(preview.Id),"damaged-recovery.v1.json"));else File.WriteAllText(Path.Combine(Generation(preview.Id),"origin-current.bin"),"changed");
            Assert.Catch(()=>store.InspectDamagedPreview(preview.Id,preview.Receipt.ManifestHash,origin));Assert.Catch(()=>store.InspectPrepared(preview.Id,preview.Receipt.ManifestHash));Assert.That(store.Load().Revision,Is.EqualTo("initial"));
        }
        [Test] public void SelectedRecoveryCannotBeDiscardedAfterCommitRecordIsLostAndReconciliationNeverReplaysAfterReview()
        {
            var source=Prepare();DamageSelection();string origin=DamageOrigin();var preview=store.PrepareDamagedRecovery(origin,source.Id,source.Receipt.ManifestHash);var evidence=PreserveFor(preview,origin);var next=store.CommitDamagedRecovery(preview.Id,preview.Receipt.ManifestHash,origin,evidence);
            var approved=store.CompleteReview(Guid.NewGuid().ToString("N"),preview.Id,preview.Receipt.ManifestHash,next.Revision,preview.Receipt.ManifestHash);Assert.That(store.CommittedDamagedRecovery(preview.Id,preview.Receipt.ManifestHash,origin),Is.Null);Assert.That(store.Load().Revision,Is.EqualTo(approved.Revision));
            File.Delete(Path.Combine(Generation(preview.Id),"damaged-commit.v1.json"));Assert.Throws<InvalidDataException>(()=>store.CommittedDamagedRecovery(preview.Id,preview.Receipt.ManifestHash,origin));Assert.Throws<InvalidDataException>(()=>store.DiscardDamagedPreview(preview.Id,preview.Receipt.ManifestHash));Assert.That(Directory.Exists(Generation(preview.Id)),Is.True);
        }
        [Test] public void DamagedEvidenceWithoutACommitRemainsAvailableForExplicitMaintenance()
        {
            var source=Prepare();DamageSelection();string origin=DamageOrigin();var preview=store.PrepareDamagedRecovery(origin,source.Id,source.Receipt.ManifestHash);var evidence=PreserveFor(preview,origin);Assert.Throws<InvalidDataException>(()=>store.DiscardDamagedPreview(preview.Id,preview.Receipt.ManifestHash));Assert.That(File.Exists(evidence.Path),Is.True);Assert.That(DamageOrigin(),Is.EqualTo(origin));
        }
    }
}
