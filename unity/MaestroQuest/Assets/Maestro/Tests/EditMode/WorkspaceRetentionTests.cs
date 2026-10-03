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
        string RetentionCache=>Path.Combine(directory,"retention-cache");
        string Origin()=> (string)store.InspectRetention()["originHash"];
        WorkspaceGenerationStore.RetainedExport Export(PreparedWorkspaceGeneration value,WorkspaceGenerationStore from=null)=>
            (from??store).ExportRetained(Origin(),value.Id,value.Receipt.ManifestHash,"original",RetentionCache);
        [Test] public void RetentionInspectionDoesNotInitializeAMissingStore()
        {
            string path=Path.Combine(directory,"absent");var result=new WorkspaceGenerationStore(path).InspectRetention();Assert.That((bool)result["selectionReadable"],Is.True);Assert.That((JArray)result["entries"],Is.Empty);Assert.That(Directory.Exists(path),Is.False);
        }
        [Test] public void RetentionInventoryDistinguishesSelectedPreviousBackupAndReservations()
        {
            var first=Prepare();var one=Activate(store,first.Id,first.Receipt.ManifestHash,"initial");var second=Prepare();var two=Activate(store,second.Id,second.Receipt.ManifestHash,one.Revision);var spare=Prepare();
            var entries=((JArray)store.InspectRetention()["entries"]).Cast<JObject>().ToDictionary(x=>(string)x["generationId"]);
            Assert.That((string)entries[second.Id]["role"],Is.EqualTo("active"));Assert.That((string)entries[two.Previous.Generation]["role"],Is.EqualTo("previous"));Assert.That((string)entries[first.Id]["role"],Is.EqualTo("pointer-backup"));Assert.That((string)entries[one.Previous.Generation]["role"],Is.EqualTo("pointer-backup"));Assert.That((string)entries[spare.Id]["reservation"],Is.EqualTo("unreserved"));Assert.That((string)entries[first.Id]["reservation"],Is.EqualTo("reserved"));
            File.WriteAllText(Pointer+".previous","broken");entries=((JArray)store.InspectRetention()["entries"]).Cast<JObject>().ToDictionary(x=>(string)x["generationId"]);Assert.That((string)entries[first.Id]["role"],Is.EqualTo("unknown"));Assert.That((string)entries[second.Id]["role"],Is.EqualTo("active"));
        }
        [Test] public void RetainedExportUsesNewerSavedContentAndLeavesExcludedRecoveryMaterialAlone()
        {
            var p=Prepare();string data=Path.Combine(Generation(p.Id),"data"),room=Path.Combine(data,"room.v7.json");var doc=JObject.Parse(File.ReadAllText(room));doc["objects"][1]["name"]="Latest saved Maestro";File.WriteAllText(room,doc.ToString());File.WriteAllText(Path.Combine(data,"action-receipts.v1.json"),"private evidence");File.WriteAllText(Path.Combine(Generation(p.Id),"preserved.bin"),"private outer evidence");
            Assert.Throws<InvalidDataException>(()=>store.InspectPrepared(p.Id,p.Receipt.ManifestHash));
            using var export=Export(p);Assert.That(export.Receipt.ManifestHash,Is.Not.EqualTo(p.Receipt.ManifestHash));Assert.That(export.ExcludedFiles,Is.EqualTo(1));
            Directory.CreateDirectory(Path.Combine(directory,"portable-check"));
            using var input=File.OpenRead(export.Path);using var imported=WorkspaceArchive.Stage(input,Path.Combine(directory,"portable-check"));Assert.That(imported.Receipt.ManifestHash,Is.EqualTo(export.Receipt.ManifestHash));Assert.That(File.ReadAllText(Path.Combine(imported.DirectoryPath,"room.v7.json")),Does.Contain("Latest saved Maestro"));Assert.That(File.Exists(Path.Combine(imported.DirectoryPath,"action-receipts.v1.json")),Is.False);Assert.That(File.ReadAllText(Path.Combine(data,"action-receipts.v1.json")),Is.EqualTo("private evidence"));Assert.That(File.ReadAllText(Path.Combine(Generation(p.Id),"preserved.bin")),Is.EqualTo("private outer evidence"));Assert.That(store.Load().Revision,Is.EqualTo("initial"));
        }
        [Test] public void UntouchedRetainedExportHasTheSamePortableManifestAndNeverConsumesARetentionSlot()
        {
            var p=Prepare();for(int i=1;i<64;i++)Prepare();Assert.Throws<InvalidDataException>(()=>Prepare());using var exported=Export(p);Assert.That(exported.Receipt.ManifestHash,Is.EqualTo(p.Receipt.ManifestHash));Assert.That(exported.ExcludedFiles,Is.Zero);Assert.That(Directory.GetDirectories(Path.Combine(root,"generations")),Has.Length.EqualTo(64));
        }
        [TestCase("document")] [TestCase("asset")] public void InvalidRetainedBytesCannotBeReplacedByOldOrEmptyContent(string damage)
        {
            var p=Prepare();string path=Path.Combine(Generation(p.Id),"data",damage=="document"?"room.v7.json":"models/"+modelHash+".glb");File.WriteAllText(path,"broken");Assert.That(()=>Export(p),Throws.Exception);Assert.That(File.ReadAllText(path),Is.EqualTo("broken"));if(Directory.Exists(RetentionCache))Assert.That(Directory.GetFiles(RetentionCache),Is.Empty);
        }
        [Test] public void RetainedExportRejectsActiveLiveAndStaleSelectionIdentities()
        {
            var p=Prepare();string before=Origin();var selection=Activate(store,p.Id,p.Receipt.ManifestHash,"initial");Assert.Throws<InvalidDataException>(()=>Export(p));var q=Prepare();
            Assert.Throws<InvalidDataException>(()=>store.ExportRetained(before,q.Id,q.Receipt.ManifestHash,"original",RetentionCache));Assert.Throws<InvalidDataException>(()=>store.ExportRetained(Origin(),q.Id,q.Receipt.ManifestHash,q.Id,RetentionCache));Assert.Throws<InvalidDataException>(()=>store.ExportRetained(Origin(),q.Id,new string('a',64),"original",RetentionCache));Assert.That(store.Load().Revision,Is.EqualTo(selection.Revision));
        }
        [Test] public void RetainedExportDetectsChangesAndCleansOnlyItsPrivateOutput()
        {
            var p=Prepare();string path=Path.Combine(Generation(p.Id),"data","room.v7.json");var changed=new WorkspaceGenerationStore(directory,point=>{if(point=="retention.captured")File.AppendAllText(path," ");});Assert.Throws<InvalidDataException>(()=>Export(p,changed));Assert.That(File.ReadAllText(path),Does.EndWith(" "));Assert.That(Directory.GetFiles(RetentionCache),Is.Empty);
            using var cancellation=new CancellationTokenSource();var cancelled=new WorkspaceGenerationStore(directory,point=>{if(point=="retention.captured")cancellation.Cancel();});Assert.Throws<OperationCanceledException>(()=>cancelled.ExportRetained(Origin(),p.Id,p.Receipt.ManifestHash,"original",RetentionCache,cancellation.Token));Assert.That(Directory.GetFiles(RetentionCache),Is.Empty);Assert.That(Directory.Exists(Generation(p.Id)),Is.True);
        }
        [TestCase("room.v8.json")] [TestCase("controls.v3.json.backup")] [TestCase("motions/motions.v3.json.pending")]
        public void RetainedExportNeverTreatsAnOlderPrimaryAsCurrentWhenNewerVersionEvidenceExists(string name)
        {
            var p=Prepare();string path=Path.Combine(Generation(p.Id),"data",name);File.WriteAllText(path,"newer data must survive");Assert.Throws<InvalidDataException>(()=>Export(p));Assert.That(File.ReadAllText(path),Is.EqualTo("newer data must survive"));Assert.That(Directory.Exists(RetentionCache),Is.False);
        }
        [Test] public void DamagedSelectionInventoryIsExplicitAndExportCannotGuessItsLiveRoot()
        {
            var p=Prepare();File.WriteAllText(Pointer,"broken");var inventory=store.InspectRetention();Assert.That((bool)inventory["selectionReadable"],Is.False);Assert.That((string)inventory["entries"][0]["role"],Is.EqualTo("unknown"));Assert.That(()=>store.ExportRetained((string)inventory["originHash"],p.Id,p.Receipt.ManifestHash,"",RetentionCache),Throws.Exception);Assert.That(File.ReadAllText(Pointer),Is.EqualTo("broken"));
        }
    }
}
