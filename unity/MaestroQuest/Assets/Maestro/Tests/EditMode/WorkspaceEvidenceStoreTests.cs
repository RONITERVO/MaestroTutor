// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using Maestro.Quest.Persistence;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class WorkspaceEvidenceStoreTests
    {
        string root,evidence,id,folder,cache;WorkspaceEvidenceStore store;
        [SetUp] public void Setup(){root=Path.Combine(Path.GetTempPath(),"mwe-"+Guid.NewGuid().ToString("N"));evidence=Path.Combine(root,"workspace-history.v1","evidence");id=Guid.NewGuid().ToString("N");folder=Path.Combine(evidence,id);cache=Path.Combine(root,"cache");store=new WorkspaceEvidenceStore(root);}
        [TearDown] public void Cleanup(){if(Directory.Exists(root))Directory.Delete(root,true);}
        void Seed(){Directory.CreateDirectory(Path.Combine(folder,"empty"));File.WriteAllBytes(Path.Combine(folder,"raw status [] ü.json"),new byte[]{0,255,1,17});File.WriteAllText(Path.Combine(folder,"accepted.json"),"{ malformed accepted");}
        WorkspaceEvidenceStore.Entry Inspect()=>store.Inspect(id,CancellationToken.None);
        [Test] public void MissingEvidenceInspectionDoesNotCreateStorage(){Assert.That(store.List(CancellationToken.None),Is.Empty);Assert.That(Directory.Exists(root),Is.False);}
        [Test] public void BundlePreservesRawBytesAndEmptyDirectoriesWithSafePayloadNames()
        {
            Seed();var value=Inspect();using var bundle=store.Capture(value,cache,CancellationToken.None);Assert.That(Path.GetFileName(bundle.Path),Does.StartWith("maestro-evidence-"));byte[] raw=File.ReadAllBytes(bundle.Path);Assert.That(WorkspaceFileInventory.Hash(raw),Is.EqualTo(bundle.Hash));
            using var file=File.OpenRead(bundle.Path);using var zip=new ZipArchive(file,ZipArchiveMode.Read);using var reader=new StreamReader(zip.GetEntry("evidence-manifest.json").Open());var manifest=JObject.Parse(reader.ReadToEnd());
            Assert.That((string)manifest["format"],Is.EqualTo("maestro-evidence"));Assert.That((string)manifest["fingerprint"],Is.EqualTo(value.Fingerprint));Assert.That(((JArray)manifest["entries"]).Any(n=>(string)n["path"]=="empty"&&(string)n["kind"]=="directory"),Is.True);
            foreach(JObject node in manifest["entries"]){if((string)node["kind"]!="file")continue;string payload=(string)node["payload"];Assert.That(System.Text.RegularExpressions.Regex.IsMatch(payload,"^payload/[0-9]{4}$"),Is.True);using var input=zip.GetEntry(payload).Open();using var output=new MemoryStream();input.CopyTo(output);Assert.That(output.ToArray(),Is.EqualTo(File.ReadAllBytes(Path.Combine(folder,(string)node["path"]))));Assert.That(WorkspaceFileInventory.Hash(output.ToArray()),Is.EqualTo((string)node["sha256"]));}
            Assert.That(zip.Entries.All(e=>e.FullName=="evidence-manifest.json"||e.FullName.StartsWith("payload/",StringComparison.Ordinal)),Is.True);Assert.That(Inspect().Fingerprint,Is.EqualTo(value.Fingerprint));
            manifest.Remove("fingerprint");foreach(JObject node in manifest["entries"])node.Remove("payload");Assert.That(WorkspaceFileInventory.Hash(Encoding.UTF8.GetBytes(manifest.ToString(Newtonsoft.Json.Formatting.None))),Is.EqualTo(value.Fingerprint));
            using var unplayable=File.OpenRead(bundle.Path);Assert.Throws<InvalidDataException>(()=>WorkspaceArchive.Stage(unplayable,Path.Combine(root,"restore")));

        }
        [Test] public void StaleBytesPreventExportAndRemovalWithoutDeletingAnything()
        {
            Seed();var value=Inspect();File.AppendAllText(Path.Combine(folder,"accepted.json"),"changed");Assert.Throws<InvalidOperationException>(()=>store.Capture(value,cache,CancellationToken.None));Assert.Throws<InvalidOperationException>(()=>store.Remove(value,CancellationToken.None));Assert.That(Directory.GetFiles(folder),Has.Length.EqualTo(2));Assert.That(Directory.Exists(cache),Is.False);
        }
        [Test] public void MutationDuringCaptureDeletesOnlyItsPrivateIncompleteArchive()
        {
            Seed();var value=Inspect();store.Fault=point=>{if(point=="evidence.beforeCapture")File.AppendAllText(Path.Combine(folder,"accepted.json"),"changed");};
            Assert.Throws<IOException>(()=>store.Capture(value,cache,CancellationToken.None));Assert.That(Directory.GetFiles(cache),Is.Empty);Assert.That(File.ReadAllText(Path.Combine(folder,"accepted.json")),Does.EndWith("changed"));
        }
        [TestCase(false)] [TestCase(true)] public void CancellationBeforeRemovalPreservesEverythingButAfterItStartsCannotRollBack(bool late)
        {
            Seed();var value=Inspect();using var cancellation=new CancellationTokenSource();store.Fault=point=>{if(point==(late?"evidence.removing":"evidence.beforeRemove"))cancellation.Cancel();};
            if(late){store.Remove(value,cancellation.Token);Assert.That(Directory.Exists(folder),Is.False);}
            else{Assert.Throws<OperationCanceledException>(()=>store.Remove(value,cancellation.Token));Assert.That(Inspect().Fingerprint,Is.EqualTo(value.Fingerprint));}
        }
        [Test] public void PartialRemovalFailureLeavesExportedCopyIntactAndRemainingFilesInspectable()
        {
            Seed();var value=Inspect();using var bundle=store.Capture(value,cache,CancellationToken.None);string hash=bundle.Hash;store.Fault=point=>{if(point=="evidence.removing")throw new IOException("Simulated delete failure");};
            Assert.Throws<IOException>(()=>store.Remove(value,CancellationToken.None));Assert.That(Directory.Exists(folder),Is.True);Assert.That(Inspect().Fingerprint,Is.Not.EqualTo(value.Fingerprint));Assert.That(WorkspaceFileInventory.Hash(File.ReadAllBytes(bundle.Path)),Is.EqualTo(hash));
        }
        [Test] public void LostAcknowledgementCannotUndoKnownCompletedRemoval()
        {
            Seed();var value=Inspect();store.Fault=point=>{if(point=="evidence.removed")throw new IOException();};Assert.DoesNotThrow(()=>store.Remove(value,CancellationToken.None));Assert.That(Directory.Exists(folder),Is.False);
        }
        [Test] public void UninspectedNewChildrenRefuseRemovalAndPreserveThem()
        {
            Seed();var value=Inspect();store.Fault=point=>{if(point=="evidence.beforeRemove")File.WriteAllText(Path.Combine(folder,"new"),"preserved");};Assert.Throws<InvalidOperationException>(()=>store.Remove(value,CancellationToken.None));Assert.That(File.ReadAllText(Path.Combine(folder,"new")),Is.EqualTo("preserved"));Assert.That(File.Exists(Path.Combine(folder,"accepted.json")),Is.True);
        }
        [Test] public void InventoryAndCacheCannotSelectUnownedPaths()
        {
            Seed();Assert.Throws<InvalidDataException>(()=>store.Inspect("../other",CancellationToken.None));Assert.Throws<IOException>(()=>store.Capture(Inspect(),folder,CancellationToken.None));File.WriteAllText(Path.Combine(evidence,"unknown"),"unowned");Assert.Throws<InvalidDataException>(()=>store.List(CancellationToken.None));Assert.That(File.ReadAllText(Path.Combine(evidence,"unknown")),Is.EqualTo("unowned"));
        }
        [Test] public void RemovingAnExportableFullQuotaEntryMakesHistoryPreservationAvailableAgain()
        {
            Directory.CreateDirectory(folder);using(var file=File.Create(Path.Combine(folder,"original")))file.SetLength(WorkspaceEvidenceStore.MaximumBytes);
            string parent=Path.Combine(root,"workspace-recovery.v1");Directory.CreateDirectory(parent);File.WriteAllText(Path.Combine(parent,"latest.json"),"broken");var histories=new WorkspaceHistoryArchive(root);var accepted=Encoding.UTF8.GetBytes("null");var snapshot=histories.Inspect("recovery",accepted,CancellationToken.None);
            Assert.Throws<IOException>(()=>histories.Reset(snapshot,accepted,CancellationToken.None));var value=Inspect();using var bundle=store.Capture(value,cache,CancellationToken.None);Assert.That(bundle.Bytes,Is.GreaterThan(0));store.Remove(value,CancellationToken.None);Assert.DoesNotThrow(()=>histories.Reset(snapshot,accepted,CancellationToken.None));
        }
        [Test] public void InventoryBoundsPreserveOversizedEvidence()
        {
            Directory.CreateDirectory(folder);using(var file=File.Create(Path.Combine(folder,"original")))file.SetLength(WorkspaceEvidenceStore.MaximumBytes+1);
            Assert.Throws<InvalidDataException>(()=>store.List(CancellationToken.None));Assert.That(new FileInfo(Path.Combine(folder,"original")).Length,Is.EqualTo(WorkspaceEvidenceStore.MaximumBytes+1));
        }
    }
}
