// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Threading;
using Maestro.Quest.Persistence;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed partial class WorkspaceGenerationTests
    {
        [Test] public void ExplicitFreshRecoveryWorksWithoutCandidatesAndKeepsOriginalBytesUnderReview()
        {
            Directory.CreateDirectory(root);DamageSelection();string origin=DamageOrigin();byte[] original=File.ReadAllBytes(Pointer);
            Assert.That((JArray)store.InspectRecovery()["candidates"],Is.Empty);var preview=store.PrepareFreshRecovery(origin);
            Assert.That(File.ReadAllBytes(Pointer),Is.EqualTo(original));Assert.That(preview.Receipt.Summary.Files,Is.EqualTo(5));Assert.That(preview.Receipt.Summary.Models,Is.Zero);
            var proof=JObject.Parse(File.ReadAllText(Path.Combine(Generation(preview.Id),"damaged-recovery.v1.json")));Assert.That((string)proof["source"]["kind"],Is.EqualTo("fresh"));Assert.That((JArray)proof["protectedGenerations"],Is.Empty);
            var selected=store.CommitDamagedRecovery(preview.Id,preview.Receipt.ManifestHash,origin,PreserveFor(preview,origin));Assert.That(selected.Active.ReviewRequired,Is.True);Assert.That(selected.Previous,Is.Null);
            var data=store.DataDirectory(selected.Active);var room=JObject.Parse(File.ReadAllText(Path.Combine(data,"room.v15.json")));Assert.That(room["objects"].Select(x=>(string)x["id"]),Is.EquivalentTo(new[]{"book","maestro"}));
            Assert.That(room["objects"].All(x=>string.IsNullOrEmpty((string)x["modelHash"])),Is.True);Assert.That(File.ReadAllText(Path.Combine(directory,"room","keep.txt")),Is.EqualTo("Original workspace"));
            Assert.That(File.ReadAllBytes(Path.Combine(Generation(preview.Id),"origin-current.bin")),Is.EqualTo(original));Assert.That(store.CommittedDamagedRecovery(preview.Id,preview.Receipt.ManifestHash,origin).Revision,Is.EqualTo(selected.Revision));
        }
        [Test] public void MissingBothPointersAfterFreshCommitCannotSilentlyReopenTheOriginalWorkspace()
        {
            Directory.CreateDirectory(root);DamageSelection();string origin=DamageOrigin();var preview=store.PrepareFreshRecovery(origin);store.CommitDamagedRecovery(preview.Id,preview.Receipt.ManifestHash,origin,PreserveFor(preview,origin));
            File.Delete(Pointer);File.Delete(Pointer+".previous");Assert.Throws<InvalidDataException>(()=>store.Load());
            Assert.That((bool)store.InspectRecovery()["selectionReadable"],Is.False);Assert.That(File.ReadAllText(Path.Combine(directory,"room","keep.txt")),Is.EqualTo("Original workspace"));
        }
        [Test] public void FreshRecoveryRejectsChangedOriginBeforePreparationOrCommit()
        {
            Directory.CreateDirectory(root);DamageSelection();string origin=DamageOrigin();var preview=store.PrepareFreshRecovery(origin);var evidence=PreserveFor(preview,origin);File.WriteAllText(Pointer,"Changed origin");
            Assert.Throws<InvalidDataException>(()=>store.PrepareFreshRecovery(origin));Assert.Throws<InvalidDataException>(()=>store.CommitDamagedRecovery(preview.Id,preview.Receipt.ManifestHash,origin,evidence));Assert.That(File.ReadAllText(Pointer),Is.EqualTo("Changed origin"));
        }
        [Test] public void CancelledFreshPreparationRemovesOnlyItsUncommittedCopy()
        {
            Directory.CreateDirectory(root);DamageSelection();string origin=DamageOrigin();using var cancellation=new CancellationTokenSource();
            var cancelled=new WorkspaceGenerationStore(directory,phase=>{if(phase=="damage.copied")cancellation.Cancel();});
            Assert.Throws<OperationCanceledException>(()=>cancelled.PrepareFreshRecovery(origin,cancellation.Token));Assert.That(Directory.GetDirectories(Path.Combine(root,"generations")),Is.Empty);Assert.That(DamageOrigin(),Is.EqualTo(origin));
        }
        [Test] public void FreshProofCannotClaimAnUninspectedRetainedSource()
        {
            Directory.CreateDirectory(root);DamageSelection();string origin=DamageOrigin();var preview=store.PrepareFreshRecovery(origin);string path=Path.Combine(Generation(preview.Id),"damaged-recovery.v1.json");var proof=JObject.Parse(File.ReadAllText(path));
            proof["source"]=new JObject {["kind"]="retained",["generationId"]=new string('a',32)};File.WriteAllText(path,proof.ToString());Assert.Throws<InvalidDataException>(()=>store.InspectDamagedPreview(preview.Id,preview.Receipt.ManifestHash,origin));
        }
        [Test] public void ExternalArchivePreparationPreservesAMissingPointerAndItsDamagedBackup()
        {
            Directory.CreateDirectory(root);File.WriteAllText(Pointer+".previous","Backup needing recovery");string origin=DamageOrigin();
            var imported=store.PrepareImport(new MemoryStream(archive,false));Assert.That(File.Exists(Pointer),Is.False);Assert.That(DamageOrigin(),Is.EqualTo(origin));Assert.That(imported.Receipt.Summary.Models,Is.EqualTo(1));
            var recovery=store.PrepareDamagedRecovery(origin,imported.Id,imported.Receipt.ManifestHash);Assert.That(recovery.Id,Is.Not.EqualTo(imported.Id));Assert.That(File.Exists(Pointer),Is.False);
            var selected=store.CommitDamagedRecovery(recovery.Id,recovery.Receipt.ManifestHash,origin,PreserveFor(recovery,origin));Assert.That(selected.Active.Generation,Is.EqualTo(recovery.Id));Assert.That(File.ReadAllText(Path.Combine(Generation(recovery.Id),"origin-previous.bin")),Is.EqualTo("Backup needing recovery"));
        }
    }
}
