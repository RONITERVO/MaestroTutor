// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class WorkspaceHostTests
    {
        JObject BundledArguments()
        {
            Assert.That(host.Runtime.TryRead("workspace.packs",1,new JObject {["offset"]=0},out var fact),Is.True);
            var page=JObject.FromObject(fact.Value);Assert.That((int)page["total"],Is.GreaterThan(0),"The actual bundled catalog must be loadable; an empty fallback is not coverage.");
            var entry=(JObject)page["entries"][0];Assert.That((string)entry["id"],Is.EqualTo("maestro.observatory"));
            Assert.That(ProgramValue.MeasureCharacters(page),Is.LessThanOrEqualTo(ProgramValue.MaximumCharacters));
            return new JObject {["packId"]=entry["id"].DeepClone(),["packVersion"]=entry["version"].DeepClone(),["packHash"]=entry["hash"].DeepClone(),["expectedRevision"]=host.Selection.Revision};
        }
        (string Id,JObject Command) BeginBundled()
        {
            var actions=new RoomExecutions(host.Current.Editor,host);var command=MaintenanceStart(actions,"workspace.pack.prepare",BundledArguments());
            Assert.That(actions.Execute(command,out var error),Is.True,error);var receipt=(JObject)actions.Observe()["workspace"]["selected"];
            Assert.That((string)receipt["phase"],Is.EqualTo("completed"),"This is the prepare-request receipt, not activation completion.");
            return ((string)receipt["output"]["requestId"],command);
        }
        JObject BundledPreview(string id)
        {
            Assert.That(host.Runtime.TryRead("workspace.pack.preview",1,new JObject {["requestId"]=id},out var fact),Is.True);
            var result=JObject.FromObject(fact.Value);Assert.That(ProgramValue.MeasureCharacters(result),Is.LessThanOrEqualTo(ProgramValue.MaximumCharacters));return result;
        }
        void ActivateBundledArguments(string id)
        {
            var preview=host.Import.ReadSelection(id);activationArgs=new JObject {["selectionRequestId"]=id,["generationId"]=preview["generationId"].DeepClone(),["manifestHash"]=preview["manifestHash"].DeepClone(),["expectedRevision"]=host.Selection.Revision};
        }
        [UnityTest] public IEnumerator BundledWorldPreparationUsesActualCatalogAndSharedCancelWithoutPickerOrCurrentEdits()
        {
            Open();yield return ReadyReviewOwners();var current=host.Current;var editor=current.Editor;
            string before=JsonUtility.ToJson(editor.Snapshot()),revision=host.Selection.Revision;
            Assert.That(host.Import.Available,Is.False);Assert.That(RoomControls.WorkspaceCapabilities(host),Does.Contain(WorkspacePackPrepareCapability.Feature));
            Assert.That(RoomControls.WorkspaceCapabilities(host),Does.Contain("workspaceSelectionCancellation.v1"));Assert.That(RoomControls.WorkspaceCapabilities(host),Does.Not.Contain("workspaceArchiveSelection.v1"));
            string pointer=Path.Combine(directory,"workspace-generations.v1","current.v1.json");bool existed=File.Exists(pointer);
            var (id,command)=BeginBundled();Assert.That(host.Import.ReadPackPreview(id),Is.Null);Assert.That(host.Import.CanSelectPack(BundledArguments(),out _),Is.False);
            yield return FinishPrevious(id);var preview=BundledPreview(id);var selection=host.Import.ReadSelection(id);
            Assert.That((int)preview["objects"],Is.EqualTo(3));Assert.That((int)preview["resources"],Is.EqualTo(1));Assert.That((int)preview["recipeParts"],Is.GreaterThan(0));
            Assert.That((int)preview["collisionPieces"],Is.GreaterThan(0));Assert.That((long)preview["bytes"],Is.GreaterThan(0));
            foreach(string field in new[]{"unavailablePrograms","missingImages","missingSounds","missingModels","missingMotions","missingControllerPrograms"})Assert.That((int)selection["summary"][field],Is.Zero,field);
            Assert.That(host.Current,Is.SameAs(current));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));Assert.That(host.Selection.Revision,Is.EqualTo(revision));
            Assert.That(File.Exists(pointer),Is.EqualTo(existed),"Preparation must not publish an implicit current-workspace pointer.");
            Assert.That(editor.WriteGate.Frozen,Is.False);Assert.That(host.Current.Rules.Runtime.Scheduler.RunningCount,Is.Zero);
            var actions=new RoomExecutions(editor,host);Assert.That(actions.Execute(command,out var error),Is.True,error);
            Assert.That((string)actions.Observe()["workspace"]["selected"]["output"]["requestId"],Is.EqualTo(id));Assert.That(host.Import.ReadPackPreview(id),Is.Not.Null);
            var cancel=MaintenanceStart(actions,"workspace.archive.cancel",new JObject {["requestId"]=id});Assert.That(actions.Execute(cancel,out error),Is.True,error);yield return FinishPrevious(id,"cancelled");
            Assert.That(host.Import.ReadPackPreview(id),Is.Null);Assert.That(Directory.GetDirectories(Path.Combine(directory,"workspace-generations.v1","generations")),Is.Empty);
            Assert.That(Directory.EnumerateFiles(Path.Combine(directory,"workspace-generations.v1","staging"),"bundled-*.zip"),Is.Empty);
            Assert.That(host.Import.CanSelectPack(BundledArguments(),out error),Is.True,error);
        }
        [UnityTest] public IEnumerator BundledWorldActivationRetainsAcceptedRoomAndExactInstanceAcrossEditUndoAndRestart()
        {
            Open();yield return ReadyReviewOwners();var original=host.Current;int browserId=browser.GetInstanceID();var (id,_)=BeginBundled();yield return FinishPrevious(id);
            var preview=BundledPreview(id);var accepted=AcceptedEdit("Keep this accepted original edit");ActivateBundledArguments(id);
            var (_,activation)=BeginActivation();yield return FinishActivation(activation);yield return ReadyReviewOwners();
            Assert.That(host.Current,Is.Not.SameAs(original));Assert.That(browser.GetInstanceID(),Is.EqualTo(browserId));Assert.That(host.ReviewRequired,Is.True);
            Assert.That(host.Current.Editor.WorldIdentity.worldId,Is.EqualTo((string)preview["worldId"]));Assert.That(physics.Running,Is.False);Assert.That(host.Current.Rules.Runtime.Scheduler.RunningCount,Is.Zero);
            var retained=new RoomStorage(store.DataDirectory(host.Selection.Previous)).Load(out var error);Assert.That(error,Is.Null);Assert.That(retained.objects.Single(x=>x.id==accepted.id).name,Is.EqualTo(accepted.name));
            string data=store.DataDirectory(host.Selection.Active),provenancePath=Path.Combine(data,WorldPackDocument.FileName);var bytes=File.ReadAllBytes(provenancePath);var provenance=WorldPackDocument.Decode(bytes);
            Assert.That(provenance.instances.Single().instanceId,Is.EqualTo((string)preview["instanceId"]));
            CollectionAssert.AreEquivalent(provenance.instances.Single().objects.Select(x=>x.id),host.Current.Editor.Snapshot().objects.Where(x=>!x.IsBuiltIn).Select(x=>x.id));
            Assert.That(host.Runtime.TryRead("workspace.pack.instance",1,new JObject {["offset"]=0},out var fact),Is.True);var origin=JObject.FromObject(fact.Value);
            Assert.That((string)origin["entries"][0]["instanceId"],Is.EqualTo((string)preview["instanceId"]));Assert.That((int)origin["entries"][0]["objects"],Is.EqualTo(3));
            string review=BeginReview();yield return FinishReview(review);ApproveReview(review);yield return FinishReview(review);
            var first=host.Current.Editor.Snapshot().objects.First(x=>!x.IsBuiltIn);string label=first.name;first.name="User-owned pack edit";
            Assert.That(host.Current.Editor.ApplyAgentEdit(host.Current.Editor.Revision,new[]{first},Array.Empty<string>(),out error),Is.True,error);
            Assert.That(host.Current.Editor.Read(first.id).name,Is.EqualTo("User-owned pack edit"));host.Current.Editor.Undo();Assert.That(host.Current.Editor.Read(first.id).name,Is.EqualTo(label));
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(provenancePath));string path=directory,revision=host.Selection.Revision;
            yield return TestLifecycleDrain.Destroy(root);BuildShell(path);Open();yield return ReadyHost();yield return ReadyReviewOwners();
            Assert.That(host.Selection.Revision,Is.EqualTo(revision));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(provenancePath));
            Assert.That(host.Runtime.TryRead("workspace.pack.instance",1,new JObject {["offset"]=0},out fact),Is.True);Assert.That(JToken.DeepEquals(JObject.FromObject(fact.Value),origin),Is.True);
            Assert.That(host.Import.ReadSelection(id),Is.Null,"A restart never restarts the old source-selection job.");
        }
        [UnityTest] public IEnumerator BundledWorldOriginCannotBeRebasedAfterWorkspaceReviewChangesSelection()
        {
            yield return ReadyForPrevious();var (id,_)=BeginBundled();yield return FinishPrevious(id);string origin=host.Selection.Revision;
            string review=BeginReview();yield return FinishReview(review);ApproveReview(review);yield return FinishReview(review);
            Assert.That(host.Selection.Revision,Is.Not.EqualTo(origin));ActivateBundledArguments(id);
            Assert.That(host.Activation.CanStart(activationArgs,out var error),Is.False);Assert.That(error,Does.Contain("earlier selection"));
            Assert.That(host.Import.MatchesOrigin(origin),Is.True);Assert.That(host.Import.MatchesOrigin(host.Selection.Revision),Is.False);
            host.Import.Cancel(id);yield return FinishPrevious(id,"cancelled");Assert.That(host.Current.Editor.Snapshot().objects.Single(x=>!x.IsBuiltIn).name,Is.EqualTo("Current robot"));
        }
        [UnityTest] public IEnumerator BundledWorldGuardsExactSourceTemporaryWritesAndBothSelectionDirections()
        {
            Open();yield return ReadyReviewOwners();var args=BundledArguments();var editor=host.Current.Editor;string before=JsonUtility.ToJson(editor.Snapshot());
            foreach(string field in new[]{"packId","packVersion","packHash","expectedRevision"}){
                var wrong=(JObject)args.DeepClone();wrong[field]=field=="packVersion"?new JValue(999999):new JValue(field=="packId"?"missing.world":field=="packHash"?new string('f',64):new string('e',32));
                Assert.That(host.Import.CanSelectPack(wrong,out _),Is.False,field);
            }
            Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);Assert.That(host.Import.CanSelectPack(args,out _),Is.False);
            float savedBy=Time.realtimeSinceStartup+5;while(editor.TemporarySavePending&&Time.realtimeSinceStartup<savedBy)yield return null;
            Assert.That(editor.TemporarySavePending,Is.False,"Starting temporary room must finish its actual baseline save before discard.");Assert.That(editor.TemporarySaveError,Is.Null);
            Assert.That(host.Import.CanSelectPack(args,out _),Is.False,"A durable temporary baseline is still a temporary room.");Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);
            using(var write=editor.WriteGate.TryWrite(out error)){Assert.That(write,Is.Not.Null,error);Assert.That(host.Import.CanSelectPack(args,out _),Is.False);}
            var picker=new MaintenancePicker();host.Import.InitializeForTests(directory,picker);string file=host.Import.Select();Assert.That(host.Import.CanSelectPack(args,out _),Is.False);
            host.Import.Cancel(file);var (id,_)=BeginBundled();Assert.That(host.Import.CanSelect(out _),Is.False);
            Assert.That(host.Import.CanSelectPrevious(new JObject(),out _),Is.False);host.Import.Cancel(id);yield return FinishPrevious(id,"cancelled");
            Assert.That(host.Import.CanCancel(file,out _),Is.False);Assert.That(picker.Starts,Is.EqualTo(1));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator BundledWorldCancellationDrainsLatePublicationBeforeAnotherSelection()
        {
            Open();yield return ReadyReviewOwners();using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
            host.Import.Fault=point=>{if(point=="prepare.ready"){entered.Set();if(!release.Wait(TimeSpan.FromSeconds(20)))throw new IOException("Test worker timeout");}};
            var (id,_)=BeginBundled();string revision=host.Selection.Revision;
            try {
                float deadline=Time.realtimeSinceStartup+15;while(!entered.IsSet&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(entered.IsSet,Is.True);
                host.Import.Cancel(id);host.Import.Poll();Assert.That((string)host.Import.ReadSelection(id)["phase"],Is.EqualTo("cancelling"));
                Assert.That(host.Import.ReadPackPreview(id),Is.Null);Assert.That(host.Import.CanSelectPack(BundledArguments(),out _),Is.False);Assert.That(host.Import.StableForRecovery,Is.False);
            }finally{release.Set();}
            yield return FinishPrevious(id,"cancelled");Assert.That(host.Selection.Revision,Is.EqualTo(revision));
            Assert.That(Directory.GetDirectories(Path.Combine(directory,"workspace-generations.v1","generations")),Is.Empty);
            Assert.That(Directory.EnumerateFiles(Path.Combine(directory,"workspace-generations.v1","staging"),"bundled-*.zip"),Is.Empty);Assert.That(host.Import.StableForRecovery,Is.True);
        }
        [UnityTest] public IEnumerator BundledWorldRetirementOwnsLateWorkerCleanupWithoutPublishingOrReopening()
        {
            Open();yield return ReadyReviewOwners();using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
            host.Import.Fault=point=>{if(point=="prepare.ready"){entered.Set();if(!release.Wait(TimeSpan.FromSeconds(20)))throw new IOException("Test worker timeout");}};
            var (id,_)=BeginBundled();System.Threading.Tasks.Task retirement=null;string generation=null,manifestHash=null;
            try {
                float deadline=Time.realtimeSinceStartup+15;while(!entered.IsSet&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(entered.IsSet,Is.True);
                string generated=Directory.GetDirectories(Path.Combine(directory,"workspace-generations.v1","generations")).Single();
                generation=Path.GetFileName(generated);manifestHash=(string)JObject.Parse(File.ReadAllText(Path.Combine(generated,"generation.v1.json")))["manifestHash"];
                retirement=host.Import.Retire();Assert.That(retirement.IsCompleted,Is.False);Assert.That(host.Import.CanSelectPack(BundledArguments(),out _),Is.False);
            }finally{release.Set();}
            float end=Time.realtimeSinceStartup+15;while(!retirement.IsCompleted&&Time.realtimeSinceStartup<end)yield return null;Assert.That(retirement.IsCompleted,Is.True);Assert.That(retirement.IsFaulted,Is.False);
            Assert.That(host.Import.isActiveAndEnabled,Is.True);host.Import.Poll();Assert.That((string)host.Import.ReadSelection(id)["phase"],Is.Not.EqualTo("prepared"));
            Assert.That(host.Import.CanActivate(id,generation,manifestHash,out _),Is.False);
            Assert.That(store.Load().Active.Generation,Is.EqualTo("original"));Assert.That(Directory.GetDirectories(Path.Combine(directory,"workspace-generations.v1","generations")),Is.Empty);
            Assert.That(Directory.EnumerateFiles(Path.Combine(directory,"workspace-generations.v1","staging"),"bundled-*.zip"),Is.Empty);Assert.That(host.Import.ReadPackPreview(id),Is.Null);
        }
        [UnityTest] public IEnumerator BundledWorldPreparationIncludesActualShippedAvatarAndMotionBaseline()
        {
            var avatar=BundledAvatar.FromApplication();var motions=BundledMotions.FromApplication();Assert.That(avatar,Is.Not.Null);Assert.That(motions,Is.Not.Null);
            host.Initialize(directory,room.transform,Build,agent,avatar,motions);yield return ReadyReviewOwners();
            var timing=System.Diagnostics.Stopwatch.StartNew();var checkpoints=new System.Collections.Concurrent.ConcurrentQueue<JObject>();
            host.Import.Fault=point=>checkpoints.Enqueue(new JObject {["stage"]=point,["elapsedMilliseconds"]=timing.ElapsedMilliseconds});
            var (id,_)=BeginBundled();JObject selection=null;
            try {
                // This real 178-motion archive follows the owner's ten-minute cancellation
                // contract, plus ten seconds to observe its terminal result. The small previous-
                // workspace helper's20s deadline is not a full content-pack performance claim.
                while(timing.Elapsed<TimeSpan.FromMinutes(10)+TimeSpan.FromSeconds(10)){
                    host.Import.Poll();selection=host.Import.ReadSelection(id);
                    if((string)selection["phase"] is "prepared" or "cancelled" or "failed")break;yield return null;
                }
                var observed=checkpoints.ToArray();
                Debug.Log("MAESTRO_BUNDLED_PREPARATION_TIMING "+new JObject {["phase"]=selection?["phase"],["elapsedMilliseconds"]=timing.ElapsedMilliseconds,
                    ["motions"]=motions.Count,["motionBytes"]=motions.Bytes,["checkpoints"]=new JArray(observed)}.ToString(Newtonsoft.Json.Formatting.None));
                Assert.That((string)selection?["phase"],Is.EqualTo("prepared"),selection?.ToString());
                CollectionAssert.AreEqual(new[]{"prepare.verified","prepare.contentMoved","prepare.ready"},observed.Select(x=>(string)x["stage"]));
            }finally{host.Import.Fault=null;}
            Assert.That((int)selection["summary"]["models"],Is.EqualTo(1));Assert.That((int)selection["summary"]["motions"],Is.EqualTo(motions.Count));
            string data=store.DataDirectory(new WorkspaceLocation((string)selection["generationId"],new string('a',32),true));
            var roomData=new RoomStorage(data).Load(out var error);Assert.That(error,Is.Null);Assert.That(roomData.objects.Single(x=>x.id=="maestro").modelHash,Is.EqualTo(avatar.Hash));
            Assert.That(ModelLibrary.Hash(File.ReadAllBytes(Path.Combine(data,"models",avatar.Hash+".glb"))),Is.EqualTo(avatar.Hash));
            Assert.That(File.ReadAllText(Path.Combine(data,"models",avatar.Hash+".txt")),Does.Contain(avatar.Attribution));
            long bytes=0;foreach(var motion in motions.Catalogue.entries){
                var payload=File.ReadAllBytes(Path.Combine(data,"motions",motion.hash+".motion.glb"));bytes+=payload.LongLength;
                Assert.That(payload.Length,Is.EqualTo(motion.bytes));Assert.That(ModelLibrary.Hash(payload),Is.EqualTo(motion.hash));
            }
            Assert.That(bytes,Is.EqualTo(motions.Bytes));var preview=BundledPreview(id);var provenance=WorldPackDocument.Decode(File.ReadAllBytes(Path.Combine(data,WorldPackDocument.FileName)));
            provenance.ValidateWorld(roomData.world.worldId);Assert.That(provenance.instances.Single().instanceId,Is.EqualTo((string)preview["instanceId"]));
            Assert.That(provenance.instances.Single().sourceHash,Is.EqualTo((string)preview["packHash"]));Assert.That(provenance.worldId,Is.EqualTo((string)preview["worldId"]));
            host.Import.Cancel(id);yield return FinishPrevious(id,"cancelled");
        }
        [TestCase("world-packs.v1.json","{broken")]
        [TestCase("world-packs.v2.json","{}")]
        public void BundledWorldMalformedOrFutureProvenanceRefusesBeforeContentOwners(string name,string payload)
        {
            string data=Path.Combine(directory,"room");Directory.CreateDirectory(data);string path=Path.Combine(data,name);File.WriteAllText(path,payload);
            Open();Assert.That(host.Current,Is.Null);Assert.That(builds,Is.Zero);Assert.That(File.ReadAllText(path),Is.EqualTo(payload));Assert.That(host.ReadPackInstance(0),Is.Null);
        }
        [UnityTest] public IEnumerator BundledWorldForeignProvenanceRetiresBeforeFirstLiveContentFrame()
        {
            string data=Path.Combine(directory,"room");Directory.CreateDirectory(data);string path=Path.Combine(data,WorldPackDocument.FileName);
            var bytes=new WorldPackDocument {worldId=new string('b',32)}.Encode();File.WriteAllBytes(path,bytes);
            Open();Assert.That(host.Current,Is.Null);Assert.That(builds,Is.EqualTo(1));Assert.That(host.ReadPackInstance(0),Is.Null);
            yield return ReadyHost(false);CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));Assert.That(physics.Running,Is.False);
            Assert.That(root.GetComponentsInChildren<RoomEditor>(),Is.Empty);
        }
        [UnityTest] public IEnumerator BundledWorldPreparationRefusesUnavailableWorkspaceHistoryWithoutRepairingIt()
        {
            string history=Path.Combine(directory,"workspace-activation.v1");Directory.CreateDirectory(history);string path=Path.Combine(history,"latest.json");File.WriteAllText(path,"{broken");
            Open();yield return ReadyReviewOwners();Assert.That(host.Activation.HistoryUnavailable,Is.True);
            Assert.That(host.Import.CanSelectPack(BundledArguments(),out var error),Is.False);Assert.That(error,Does.Contain("history"));Assert.That(File.ReadAllText(path),Is.EqualTo("{broken"));
        }
    }
}
