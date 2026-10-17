// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Persistence;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed partial class WorkspaceGenerationTests
    {
        PreparedWorkspaceGeneration[] FillOrdinaryCapacity()=>Enumerable.Range(0,64).Select(_=>Prepare()).ToArray();
        [TestCase(false)] [TestCase(true)]
        public void ExplicitRecoveryAtCapacityPreservesAllOriginalsAndCancellationReleasesOnlyItsReserve(bool fresh)
        {
            var saved=FillOrdinaryCapacity();DamageSelection();string origin=DamageOrigin();var original=File.ReadAllBytes(Pointer);
            Assert.Throws<InvalidDataException>(()=>Prepare());
            var preview=fresh?store.PrepareFreshRecovery(origin):store.PrepareDamagedRecovery(origin,saved[0].Id,saved[0].Receipt.ManifestHash);
            Assert.That(store.InspectRecovery()["candidates"].Count(),Is.EqualTo(65));Assert.That(store.InspectRetention()["entries"].Count(),Is.EqualTo(65));
            Assert.That(store.InspectDamagedPreview(preview.Id,preview.Receipt.ManifestHash,origin),Is.Not.Null);
            var proof=JObject.Parse(File.ReadAllText(Path.Combine(Generation(preview.Id),"damaged-recovery.v1.json")));
            Assert.That(proof["protectedGenerations"].Select(x=>(string)x),Is.EquivalentTo(saved.Select(x=>x.Id)));
            Assert.That(Assert.Throws<InvalidDataException>(()=>store.PrepareFreshRecovery(origin)).Message,Is.EqualTo(WorkspaceGenerationStore.RecoveryCapacityError));
            Assert.That(WorkspaceGenerationStore.RecoveryCapacityError.Length,Is.LessThanOrEqualTo(128));Assert.Throws<InvalidDataException>(()=>Prepare());
            store.DiscardDamagedPreview(preview.Id,preview.Receipt.ManifestHash);
            Assert.That(Directory.GetDirectories(Path.Combine(root,"generations")).Select(Path.GetFileName),Is.EquivalentTo(saved.Select(x=>x.Id)));
            foreach(var item in saved)Assert.That(store.InspectPrepared(item.Id,item.Receipt.ManifestHash),Is.Not.Null);
            Assert.That(File.ReadAllBytes(Pointer),Is.EqualTo(original));Assert.That(DamageOrigin(),Is.EqualTo(origin));
            Assert.That(store.PrepareFreshRecovery(origin),Is.Not.Null);
        }
        [TestCase("damage.copied")] [TestCase("damage.ready")]
        public void FailedReservePreparationFreesItsOwnSlotAndLeavesEveryOriginalVerifiable(string point)
        {
            var saved=FillOrdinaryCapacity();DamageSelection();string origin=DamageOrigin();
            var failing=new WorkspaceGenerationStore(directory,phase=>{if(phase==point)throw new IOException("Injected reserve failure");});
            Assert.Throws<IOException>(()=>failing.PrepareDamagedRecovery(origin,saved[0].Id,saved[0].Receipt.ManifestHash));
            Assert.That(Directory.GetDirectories(Path.Combine(root,"generations")).Select(Path.GetFileName),Is.EquivalentTo(saved.Select(x=>x.Id)));
            Assert.That(DamageOrigin(),Is.EqualTo(origin));foreach(var item in saved)Assert.That(store.InspectPrepared(item.Id,item.Receipt.ManifestHash),Is.Not.Null);
            Assert.That(store.PrepareFreshRecovery(origin),Is.Not.Null);
        }
        [Test] public void RecoveryReviewAtCapacityEnablesExplicitCleanupWithoutWeakeningSelectionProtection()
        {
            var saved=FillOrdinaryCapacity();DamageSelection();string origin=DamageOrigin();var original=File.ReadAllBytes(Pointer);
            var preview=store.PrepareDamagedRecovery(origin,saved[0].Id,saved[0].Receipt.ManifestHash);
            var selected=store.CommitDamagedRecovery(preview.Id,preview.Receipt.ManifestHash,origin,PreserveFor(preview,origin));
            Assert.That(()=>Removal(saved[1].Id),Throws.Exception,"The unreadable backup is protected until review commits a valid selection.");
            store.CompleteReview(Guid.NewGuid().ToString("N"),preview.Id,preview.Receipt.ManifestHash,selected.Revision,preview.Receipt.ManifestHash);
            Assert.That(Removal(preview.Id).Eligible,Is.False);Assert.Throws<InvalidDataException>(()=>Prepare());
            foreach(var item in saved.Skip(1).Take(2)){var removal=Removal(item.Id);Assert.That(removal.Eligible,Is.True);store.RemoveRetained(removal,EmptyHistory(),null);}
            Assert.That(Directory.GetDirectories(Path.Combine(root,"generations")),Has.Length.EqualTo(63));Assert.That(Prepare(),Is.Not.Null);
            Assert.That(File.ReadAllBytes(Path.Combine(Generation(preview.Id),"origin-current.bin")),Is.EqualTo(original));
            foreach(var item in saved.Skip(3))Assert.That(WorkspaceArchive.VerifyPreparedDirectory(Path.Combine(Generation(item.Id),"data"),File.ReadAllBytes(Path.Combine(Generation(item.Id),"manifest.json"))).ManifestHash,Is.EqualTo(item.Receipt.ManifestHash));
        }
        [Test] public void RecoveryInventoryRejectsUnexpectedOverflowWithoutDeletingAnyPath()
        {
            FillOrdinaryCapacity();DamageSelection();store.PrepareFreshRecovery(DamageOrigin());
            string extra=Generation(Guid.NewGuid().ToString("N"));Directory.CreateDirectory(extra);File.WriteAllText(Path.Combine(extra,"evidence.txt"),"Preserve unexpected data");
            Assert.Throws<InvalidDataException>(()=>store.InspectRecovery());Assert.Throws<InvalidDataException>(()=>store.InspectRetention());
            Assert.That(Directory.GetDirectories(Path.Combine(root,"generations")),Has.Length.EqualTo(66));Assert.That(File.ReadAllText(Path.Combine(extra,"evidence.txt")),Is.EqualTo("Preserve unexpected data"));
        }
    }
}
