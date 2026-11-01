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
        static JObject EmptyHistory()=>new() {["activation"]=null,["review"]=null,["recovery"]=null};
        WorkspaceGenerationStore.RemovalPreview Removal(string id,JObject history=null,JObject live=null)=>store.PreviewRemoval(Origin(),id,history??EmptyHistory(),live);
        JObject ActivationHistory(string id,string phase="interrupted")=>new() {["version"]=1,["requestId"]=Guid.NewGuid().ToString("N"),["selectionRequestId"]=Guid.NewGuid().ToString("N"),["generationId"]=id,["manifestHash"]=new string('a',64),["originRevision"]="initial",["retainedId"]="",["retainedHash"]="",["committedRevision"]="",["phase"]=phase,["status"]="Test"};
        void WriteHistory(string key,JObject value){string folder=Path.Combine(directory,"workspace-"+key+".v1");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"latest.json"),value.ToString());}
        [Test] public void ExplicitRemovalAtCapacityFreesOnlyTheReviewedGenerationIncludingPrivateEvidence()
        {
            var p=Prepare();for(int i=1;i<64;i++)Prepare();Assert.Throws<InvalidDataException>(()=>Prepare());string privatePath=Path.Combine(Generation(p.Id),"private","empty");Directory.CreateDirectory(privatePath);File.WriteAllText(Path.Combine(Generation(p.Id),"private","actions.json"),"private");
            var before=File.ReadAllBytes(Pointer);var preview=Removal(p.Id);Assert.That(preview.Eligible,Is.True);Assert.That(preview.Files,Is.GreaterThan(5));store.RemoveRetained(preview,EmptyHistory(),null);
            Assert.That(Directory.Exists(Generation(p.Id)),Is.False);Assert.That(Directory.GetDirectories(Path.Combine(root,"generations")),Has.Length.EqualTo(63));Assert.That(File.ReadAllBytes(Pointer),Is.EqualTo(before));Assert.That(File.ReadAllText(Path.Combine(directory,"room","keep.txt")),Is.EqualTo("Original workspace"));Assert.That(Prepare(),Is.Not.Null);
        }
        [Test] public void SelectionCurrentPreviousBackupAndLiveOwnersAreProtectedButOldReservationsCanBeDiscarded()
        {
            var first=Prepare();var one=Activate(store,first.Id,first.Receipt.ManifestHash,"initial");var second=Prepare();var two=Activate(store,second.Id,second.Receipt.ManifestHash,one.Revision);
            foreach(string id in new[]{first.Id,one.Previous.Generation,second.Id,two.Previous.Generation})Assert.That(Removal(id).Eligible,Is.False,id);
            var third=Prepare();var three=Activate(store,third.Id,third.Receipt.ManifestHash,two.Revision);
            Assert.That(Removal(first.Id,live:one.Json()).Eligible,Is.False);var preview=Removal(first.Id,live:three.Json());Assert.That(preview.Eligible,Is.True);store.RemoveRetained(preview,EmptyHistory(),three.Json());
        }
        [TestCase("bytes")] [TestCase("child")] [TestCase("pointer")] [TestCase("history")] public void RemovalRejectsStalePreviewBeforeDeletingAnything(string change)
        {
            var p=Prepare();var preview=Removal(p.Id);var names=Directory.GetFiles(Generation(p.Id),"*",SearchOption.AllDirectories);
            if(change=="bytes")File.AppendAllText(names[0]," ");if(change=="child")File.WriteAllText(Path.Combine(Generation(p.Id),"new.txt"),"new");if(change=="pointer")File.AppendAllText(Pointer," ");if(change=="history")WriteHistory("activation",ActivationHistory(p.Id));
            Assert.That(()=>store.RemoveRetained(preview,EmptyHistory(),null),Throws.Exception);foreach(string path in names)Assert.That(File.Exists(path),Is.True,path);
        }
        [Test] public void CorruptTargetMetadataCanBeDiscardedButUnclearSelectionCannot()
        {
            var p=Prepare();File.WriteAllText(Path.Combine(Generation(p.Id),"manifest.json"),"broken");var preview=Removal(p.Id);Assert.That(preview.Eligible,Is.True);
            File.WriteAllText(Pointer+".previous","broken");Assert.That(()=>Removal(p.Id),Throws.Exception);File.Delete(Pointer+".previous");store.RemoveRetained(preview,EmptyHistory(),null);Assert.That(Directory.Exists(Generation(p.Id)),Is.False);
        }
        [Test] public void PersistedHistoryMustMatchAcceptedHistoryAndProtectsTrackedSources()
        {
            var p=Prepare();var q=Prepare();var record=ActivationHistory(p.Id);WriteHistory("activation",record);Assert.Throws<InvalidDataException>(()=>Removal(q.Id));var accepted=EmptyHistory();accepted["activation"]=record.DeepClone();Assert.That(Removal(p.Id,accepted).Eligible,Is.False);Assert.That(Removal(q.Id,accepted).Eligible,Is.True);
            record["phase"]="activating";WriteHistory("activation",record);accepted["activation"]=record.DeepClone();Assert.Throws<InvalidDataException>(()=>Removal(q.Id,accepted));File.WriteAllText(Path.Combine(directory,"workspace-activation.v1","latest.json"),"broken");Assert.That(()=>Removal(q.Id,accepted),Throws.Exception);
        }
        [Test] public void CancellationBeforeDeletionKeepsEverythingButAfterDeletionDoesNotClaimRollback()
        {
            var p=Prepare();var preview=Removal(p.Id);using var cancel=new CancellationTokenSource();var interrupted=new WorkspaceGenerationStore(directory,where=>{if(where=="retention.beforeRemove")cancel.Cancel();});Assert.Throws<OperationCanceledException>(()=>interrupted.RemoveRetained(preview,EmptyHistory(),null,cancel.Token));Assert.That(Directory.Exists(Generation(p.Id)),Is.True);
            using var late=new CancellationTokenSource();var finish=new WorkspaceGenerationStore(directory,where=>{if(where=="retention.removing")late.Cancel();if(where=="retention.removed")throw new IOException("Lost acknowledgement");});finish.RemoveRetained(preview,EmptyHistory(),null,late.Token);Assert.That(late.IsCancellationRequested,Is.True);Assert.That(Directory.Exists(Generation(p.Id)),Is.False);
        }
        [Test] public void PartialDeletionRequiresNewInspectionAndDoesNotDeleteAnUninspectedChild()
        {
            var p=Prepare();var preview=Removal(p.Id);bool changed=false;var interrupted=new WorkspaceGenerationStore(directory,where=>{if(where=="retention.removing"&&!changed){changed=true;File.WriteAllText(Path.Combine(Generation(p.Id),"new.txt"),"uninspected");throw new IOException("Power loss");}});
            Assert.Throws<IOException>(()=>interrupted.RemoveRetained(preview,EmptyHistory(),null));Assert.That(File.ReadAllText(Path.Combine(Generation(p.Id),"new.txt")),Is.EqualTo("uninspected"));Assert.That(()=>store.RemoveRetained(preview,EmptyHistory(),null),Throws.Exception);var retry=Removal(p.Id);Assert.That(retry.Fingerprint,Is.Not.EqualTo(preview.Fingerprint));store.RemoveRetained(retry,EmptyHistory(),null);Assert.That(Directory.Exists(Generation(p.Id)),Is.False);
        }
        [Test] public void FileReplacedDuringDeletionIsRetainedForAnotherConfirmation()
        {
            var p=Prepare();var preview=Removal(p.Id);var last=preview.Nodes.OfType<JObject>().Where(n=>(string)n["kind"]=="file").OrderByDescending(n=>((string)n["path"]).Length).Last();string path=Path.Combine(Generation(p.Id),(string)last["path"]);bool changed=false;
            var interrupted=new WorkspaceGenerationStore(directory,where=>{if(where=="retention.removing"&&!changed){changed=true;File.WriteAllText(path,"new content");}});Assert.Throws<IOException>(()=>interrupted.RemoveRetained(preview,EmptyHistory(),null));Assert.That(File.ReadAllText(path),Is.EqualTo("new content"));
        }
    }
}
