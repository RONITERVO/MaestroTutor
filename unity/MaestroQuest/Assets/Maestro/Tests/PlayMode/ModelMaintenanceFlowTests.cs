// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UniGLTF;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class AnimationWorkshopTests
    {
        JObject ModelDownloadCall(string action,JObject args)=>new(){["operation"]="start",["runId"]=authorActions.Observe()["nextRunId"].DeepClone(),["call"]=new JObject {["id"]=action,["version"]=1,["arguments"]=args}};
        static IEnumerator ModelDownloadTask(Task task){yield return PhysicalCreationTestWait.Until(()=>task.IsCompleted,"Model maintenance dependency did not drain");Assert.That(task.Exception,Is.Null);Assert.That(task.IsCanceled,Is.False);}
        IEnumerator ModelDownloadReady(string hash){var rules=root.GetComponent<RuleWorkshop>();yield return PhysicalCreationTestWait.Until(()=>{rules.RetainsAssetIdentity(hash,null,out bool uncertain);return !uncertain;},"Model reference stores did not become ready");}
        IEnumerator ModelDownloadAction(string action,JObject args,Action<JObject,JObject> check=null,string expected="completed"){
            var request=ModelDownloadCall(action,args);string run=(string)request["runId"];bool accepted=authorActions.Execute(request,out var error);
            Assert.That(accepted||expected=="failed"&&(string)authorRuntime.Scheduler.Invocation(run)?["phase"]=="failed",Is.True,error);
            yield return PhysicalCreationTestWait.Until(()=>((string)authorRuntime.Scheduler.Invocation(run)?["phase"] is "completed" or "failed" or "cancelled"),"Model maintenance did not drain");
            var receipt=authorRuntime.Scheduler.Invocation(run);Assert.That((string)receipt["phase"],Is.EqualTo(expected),receipt.ToString());
            if(expected=="completed")Assert.That(CapabilityArguments.Validate((JObject)receipt["output"],BehaviourCatalog.Action(action).OutputSchema,out error),Is.True,error);
            check?.Invoke(request,receipt);
        }
        static JObject ModelDownloadArguments(string hash,JObject entry)=>new(){["modelHash"]=hash,["inspectionRevision"]=entry["inspectionRevision"].DeepClone()};
        [UnityTest]public IEnumerator SharedModelRemovalKeepsMetadataExactPickerReimportAndHistoricalReceipt(){
            ImportRuntime();using var asset=ModelLibrary.Inspect("Original model",ModelFixture.Create());yield return SaveModelAsset(asset);yield return ModelDownloadReady(asset.Hash);
            Assert.That(RoomControls.Capabilities(editor),Does.Contain(ModelMaintenanceCapability.Feature));Assert.That(BehaviourCatalog.TryRead("model.download.review",1,new JObject {["modelHash"]=asset.Hash},new BehaviourCatalog.FactContext(),out _),Is.False);
            yield return ModelDownloadAction("model.library.inspect",new JObject {["offset"]=0},(_,receipt)=>{Assert.That((int)receipt["output"]["total"],Is.EqualTo(1));Assert.That((string)receipt["output"]["entries"][0]["modelHash"],Is.EqualTo(asset.Hash));});
            JObject entry=null,removeRequest=null,removedReceipt=null;yield return ModelDownloadAction("model.download.inspect",new JObject {["modelHash"]=asset.Hash},(_,receipt)=>{entry=(JObject)receipt["output"]["entry"].DeepClone();Assert.That((bool)entry["included"],Is.False);Assert.That((bool)receipt["output"]["usage"]["canRemove"],Is.True,receipt.ToString());});
            Assert.That(authorRuntime.TryReadFact("model.download.review",1,new JObject {["modelHash"]=asset.Hash},out var fact),Is.True);Assert.That(JToken.DeepEquals((JObject)fact.Value,entry),Is.True);
            string path=Path.Combine(directory,"models",asset.Hash+".glb"),metadata=Path.Combine(directory,"models",asset.Hash+".txt");var originalMetadata=File.ReadAllBytes(metadata);var removeArgs=ModelDownloadArguments(asset.Hash,entry);
            yield return ModelDownloadAction("model.download.edit",removeArgs,(request,receipt)=>{removeRequest=request;removedReceipt=(JObject)receipt.DeepClone();Assert.That((bool)receipt["output"]["removed"],Is.True);});
            Assert.That(File.Exists(path),Is.False);Assert.That(File.ReadAllBytes(metadata),Is.EqualTo(originalMetadata));Assert.That(editor.Models.TryReadClips(asset.Hash,out _),Is.False);
            Assert.That(authorActions.Execute(ModelDownloadCall("model.download.edit",removeArgs),out var stale),Is.False);Assert.That(stale,Is.Not.Empty);
            yield return ChooseModel(asset.Bytes);yield return ImportAction(ImportAccept("library"));
            Assert.That((string)ImportFact()["phase"],Is.EqualTo("completed"));Assert.That(File.ReadAllBytes(path),Is.EqualTo(asset.Bytes));Assert.That(File.ReadAllBytes(metadata),Is.EqualTo(originalMetadata));Assert.That(imports.HasPreview,Is.False);Assert.That(avatar.IsImportedClipPlaying,Is.False);
            yield return PhysicalCreationTestWait.Until(()=>editor.Models.Assets.Pinned(ImportedAssetKind.Model,asset.Hash)==0,"Library acceptance retained native or read ownership");
            yield return ModelDownloadAction("model.download.inspect",new JObject {["modelHash"]=asset.Hash},(_,receipt)=>Assert.That(JToken.DeepEquals(receipt["output"]["entry"],entry),Is.True));
            var stored=new InvocationReceipts(editor.ReceiptDirectory);Assert.That(stored.Error,Is.Null);Assert.That(JToken.DeepEquals(stored.Find((string)removeRequest["runId"]),removedReceipt),Is.True);
            Assert.That(authorActions.Execute(removeRequest,out var error),Is.True,error);Assert.That(JToken.DeepEquals(authorActions.Observe()["selected"],removedReceipt),Is.True);yield return null;Assert.That(File.ReadAllBytes(path),Is.EqualTo(asset.Bytes));
            yield return ModelDownloadAction("model.download.inspect",new JObject {["modelHash"]=asset.Hash},(_,receipt)=>Assert.That(JToken.DeepEquals(receipt["output"]["entry"],entry),Is.True));
        }
        [UnityTest]public IEnumerator ActionableModelPreviewProtectsItsDownloadUntilSelectionIsCancelled(){
            ImportRuntime();using var asset=ModelLibrary.Inspect("Preview model",ModelFixture.Create());yield return SaveModelAsset(asset);yield return ModelDownloadReady(asset.Hash);yield return ChooseModel(asset.Bytes);JObject entry=null;
            Assert.That(imports.HasPreview,Is.True);Assert.That(editor.Models.Assets.Pinned(ImportedAssetKind.Model,asset.Hash),Is.GreaterThan(0));
            yield return ModelDownloadAction("model.download.inspect",new JObject {["modelHash"]=asset.Hash},(_,receipt)=>{entry=(JObject)receipt["output"]["entry"];Assert.That((bool)receipt["output"]["usage"]["inUse"],Is.True);Assert.That((bool)receipt["output"]["usage"]["canRemove"],Is.False);});
            yield return ModelDownloadAction("model.download.edit",ModelDownloadArguments(asset.Hash,entry),expected:"failed");Assert.That(File.Exists(Path.Combine(directory,"models",asset.Hash+".glb")),Is.True);
            imports.Cancel();yield return PhysicalCreationTestWait.Until(()=>editor.Models.Assets.Pinned(ImportedAssetKind.Model,asset.Hash)==0,"Cancelled model preview did not drain");
            yield return ModelDownloadAction("model.download.inspect",new JObject {["modelHash"]=asset.Hash},(_,receipt)=>{entry=(JObject)receipt["output"]["entry"];Assert.That((bool)receipt["output"]["usage"]["canRemove"],Is.True,receipt.ToString());});
            yield return ModelDownloadAction("model.download.edit",ModelDownloadArguments(asset.Hash,entry));
        }
        [UnityTest]public IEnumerator SavedCalibrationAndUndoProtectAnInactiveDamagedModel(){
            AuthorRuntime();using var asset=ModelLibrary.Inspect("Calibrated model",ModelFixture.Create());yield return SaveModelAsset(asset);yield return ModelDownloadReady(asset.Hash);
            var value=editor.Read("maestro");value.footPlacement=new(){enabled=true,modelHash=asset.Hash};Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{value},Array.Empty<string>(),out var error),Is.True,error);
            string path=Path.Combine(directory,"models",asset.Hash+".glb");File.WriteAllBytes(path,new byte[]{0});JObject entry=null;
            yield return ModelDownloadAction("model.download.inspect",new JObject {["modelHash"]=asset.Hash},(_,receipt)=>{entry=(JObject)receipt["output"]["entry"];Assert.That((bool)entry["contentValid"],Is.False);Assert.That((bool)receipt["output"]["usage"]["current"],Is.True);Assert.That((bool)receipt["output"]["usage"]["canRemove"],Is.False);});
            yield return ModelDownloadAction("model.download.edit",ModelDownloadArguments(asset.Hash,entry),expected:"failed");Assert.That(File.ReadAllBytes(path),Is.EqualTo(new byte[]{0}));
            value=editor.Read("maestro");value.footPlacement=new();Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{value},Array.Empty<string>(),out error),Is.True,error);
            yield return ModelDownloadAction("model.download.inspect",new JObject {["modelHash"]=asset.Hash},(_,receipt)=>{entry=(JObject)receipt["output"]["entry"];Assert.That((bool)receipt["output"]["usage"]["current"],Is.False);Assert.That((bool)receipt["output"]["usage"]["history"],Is.True);});
            yield return ModelDownloadAction("model.download.edit",ModelDownloadArguments(asset.Hash,entry),expected:"failed");Assert.That(File.ReadAllBytes(path),Is.EqualTo(new byte[]{0}));editor.Undo();Assert.That(editor.Read("maestro").footPlacement.modelHash,Is.EqualTo(asset.Hash));
        }
        [UnityTest]public IEnumerator InactiveAvatarActivityAndItsOwnUndoHistoryProtectTheModel(){
            AuthorRuntime();using var asset=ModelLibrary.Inspect("Inactive profile model",ModelFixture.Mixamo());yield return SaveModelAsset(asset);yield return ModelDownloadReady(asset.Hash);
            var importing=editor.Motions.ImportAsync("Profile motion.glb",asset.Bytes);yield return ModelDownloadTask(importing);var motion=importing.Result.Single();var choice=new TutorMotionChoice{motionId=motion.id};Assert.That(editor.ActivityProfiles.Assign(asset.Hash,motion.rigHash,TutorMotionRole.Speaking,choice,editor.Motions,out var error),Is.True,error);
            Assert.That(editor.Read("maestro").modelHash,Is.Null.Or.Empty);Assert.That(avatar.CustomModel,Is.Null);JObject entry=null;
            yield return ModelDownloadAction("model.download.inspect",new JObject {["modelHash"]=asset.Hash},(_,receipt)=>{entry=(JObject)receipt["output"]["entry"];Assert.That((bool)receipt["output"]["usage"]["current"],Is.True);Assert.That((bool)receipt["output"]["usage"]["canRemove"],Is.False);});
            yield return ModelDownloadAction("model.download.edit",ModelDownloadArguments(asset.Hash,entry),expected:"failed");
            Assert.That(editor.ActivityProfiles.Remove(asset.Hash,TutorMotionRole.Speaking,motion.id,out error),Is.True,error);
            yield return ModelDownloadAction("model.download.inspect",new JObject {["modelHash"]=asset.Hash},(_,receipt)=>{entry=(JObject)receipt["output"]["entry"];Assert.That((bool)receipt["output"]["usage"]["current"],Is.False);Assert.That((bool)receipt["output"]["usage"]["history"],Is.True);});
            yield return ModelDownloadAction("model.download.edit",ModelDownloadArguments(asset.Hash,entry),expected:"failed");Assert.That(File.ReadAllBytes(Path.Combine(directory,"models",asset.Hash+".glb")),Is.EqualTo(asset.Bytes));
            Assert.That(editor.ActivityProfiles.Undo(asset.Hash,out error),Is.True,error);Assert.That(editor.ActivityProfiles.Find(asset.Hash),Is.Not.Null);
        }
        [UnityTest]public IEnumerator UnknownSavedAvatarEvidenceRefusesRemovalWithoutRepairingIt(){
            AuthorRuntime();using var asset=ModelLibrary.Inspect("Unknown retained model",ModelFixture.Create());yield return SaveModelAsset(asset);yield return ModelDownloadReady(asset.Hash);
            string future=Path.Combine(directory,"avatar-activities.v999.json"),path=Path.Combine(directory,"models",asset.Hash+".glb");File.WriteAllText(future,"{\"version\":999}");File.WriteAllBytes(path,new byte[]{0});JObject entry=null;
            yield return ModelDownloadAction("model.download.inspect",new JObject {["modelHash"]=asset.Hash},(_,receipt)=>{entry=(JObject)receipt["output"]["entry"];Assert.That((bool)entry["contentValid"],Is.False);Assert.That((bool)receipt["output"]["usage"]["uncertain"],Is.True);Assert.That((bool)receipt["output"]["usage"]["canRemove"],Is.False);});
            yield return ModelDownloadAction("model.download.edit",ModelDownloadArguments(asset.Hash,entry),expected:"failed");Assert.That(File.ReadAllText(future),Is.EqualTo("{\"version\":999}"));Assert.That(File.ReadAllBytes(path),Is.EqualTo(new byte[]{0}));
        }
        [UnityTest]public IEnumerator ModelRemovalStopRetainsTheFenceUntilDispatchedDeletionDrains(){
            AuthorRuntime();using var asset=ModelLibrary.Inspect("Stopped model removal",ModelFixture.Create());yield return SaveModelAsset(asset);yield return ModelDownloadReady(asset.Hash);JObject entry=null;
            yield return ModelDownloadAction("model.download.inspect",new JObject {["modelHash"]=asset.Hash},(_,receipt)=>entry=(JObject)receipt["output"]["entry"]);
            using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();editor.Models.MaintenanceCheckpoint=phase=>{if(phase=="deleting"){entered.Set();if(!release.Wait(10000))throw new TimeoutException();}};
            var request=ModelDownloadCall("model.download.edit",ModelDownloadArguments(asset.Hash,entry));string run=(string)request["runId"];
            try{
                Assert.That(authorActions.Execute(request,out var error),Is.True,error);yield return PhysicalCreationTestWait.Until(()=>entered.IsSet||((string)authorRuntime.Scheduler.Invocation(run)?["phase"]=="failed"),"Model removal did not reach dispatch");Assert.That(entered.IsSet,Is.True,authorRuntime.Scheduler.Invocation(run)?.ToString());
                Assert.That(editor.WriteGate.Frozen,Is.True);Assert.That(editor.Models.Assets.TryPin(ImportedAssetKind.Model,asset.Hash,out _),Is.Null);
                Assert.That(authorActions.Execute(new JObject {["operation"]="cancel",["runId"]=run},out error),Is.True,error);Assert.That((string)authorRuntime.Scheduler.Invocation(run)["phase"],Is.EqualTo("cancelled"));Assert.That((string)authorRuntime.Scheduler.Invocation(run)["status"],Does.Contain("may still finish"));Assert.That(editor.WriteGate.Frozen,Is.True);
            }finally{release.Set();}
            yield return PhysicalCreationTestWait.Until(()=>!editor.WriteGate.Frozen,"Stopped model removal did not drain its holds");editor.Models.MaintenanceCheckpoint=null;Assert.That(File.Exists(Path.Combine(directory,"models",asset.Hash+".glb")),Is.False);
        }
        sealed class ModelDownloadImportBarrier:IAwaitCaller
        {
            readonly TaskCompletionSource<bool> release=new();internal bool Entered;internal void Open()=>release.TrySetResult(true);
            async Task Wait(){Entered=true;await release.Task;}public Task NextFrame()=>Wait();public Task NextFrameIfTimedOut()=>Wait();public async Task Run(Action action){await Wait();action();}public async Task<T> Run<T>(Func<T> action){await Wait();return action();}
        }
        [UnityTest]public IEnumerator ActualImporterProtectsTheDownloadUntilCancelledNativeWorkDrains(){
            AuthorRuntime();using var asset=ModelLibrary.Inspect("Native drain model",ModelFixture.Create());yield return SaveModelAsset(asset);yield return ModelDownloadReady(asset.Hash);JObject entry=null;
            yield return ModelDownloadAction("model.download.inspect",new JObject {["modelHash"]=asset.Hash},(_,receipt)=>entry=(JObject)receipt["output"]["entry"]);
            var read=editor.Models.ReadAsync(asset.Hash);yield return ModelDownloadTask(read);var child=new GameObject("Pending native model");child.transform.SetParent(root.transform,false);child.SetActive(false);var model=child.AddComponent<ImportedModel>();model.ConfigureResourceOwner(RoomWorldIdentity.Create(),Guid.NewGuid().ToString("N"),"preview");model.ConfigureAssetSource(editor.Models);
            var barrier=new ModelDownloadImportBarrier();var load=model.LoadAsync(read.Result,barrier);read.Result.Dispose();
            try{
                yield return PhysicalCreationTestWait.Until(()=>barrier.Entered||load.IsCompleted,"Real importer did not reach its barrier");Assert.That(barrier.Entered,Is.True,load.Exception?.ToString());
                yield return ModelDownloadAction("model.download.edit",ModelDownloadArguments(asset.Hash,entry),expected:"failed");Assert.That(File.ReadAllBytes(Path.Combine(directory,"models",asset.Hash+".glb")),Is.EqualTo(asset.Bytes));
                model.Dispose();Assert.That(load.IsCompleted,Is.False);Assert.That(editor.Models.Assets.Pinned(ImportedAssetKind.Model,asset.Hash),Is.EqualTo(1));
                yield return ModelDownloadAction("model.download.edit",ModelDownloadArguments(asset.Hash,entry),expected:"failed");
            }finally{barrier.Open();model.Dispose();}
            yield return ModelDownloadTask(load);Assert.That(model.Ready,Is.False);Assert.That(editor.Models.Assets.Pinned(ImportedAssetKind.Model,asset.Hash),Is.Zero);
            yield return ModelDownloadAction("model.download.inspect",new JObject {["modelHash"]=asset.Hash},(_,receipt)=>entry=(JObject)receipt["output"]["entry"]);yield return ModelDownloadAction("model.download.edit",ModelDownloadArguments(asset.Hash,entry));
        }
    }
}
