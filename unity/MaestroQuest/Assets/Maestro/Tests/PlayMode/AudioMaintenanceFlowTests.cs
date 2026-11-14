// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        JObject SoundMaintenanceCall(string action,JObject args)=>new(){["operation"]="start",["runId"]=new RoomExecutions(editor).Observe()["nextRunId"].DeepClone(),["call"]=new JObject {["id"]=action,["version"]=1,["arguments"]=args}};
        static IEnumerator SoundMaintenanceTask(Task task){yield return PhysicalCreationTestWait.Until(()=>task.IsCompleted,"Sound maintenance dependency did not drain");Assert.That(task.Exception,Is.Null);Assert.That(task.IsCanceled,Is.False);}
        IEnumerator SoundMaintenanceReady(string hash){yield return PhysicalCreationTestWait.Until(()=>{workshop.RetainsAssetIdentity(hash,null,out bool uncertain);return !uncertain;},"Sound reference stores did not become ready");}
        IEnumerator SoundMaintenanceAction(string action,JObject args,Action<JObject,JObject> check,string expected="completed"){
            var request=SoundMaintenanceCall(action,args);string run=(string)request["runId"];bool accepted=new RoomExecutions(editor).Execute(request,out var error);
            Assert.That(accepted||expected=="failed"&&(string)runtime.Scheduler.Invocation(run)?["phase"]=="failed",Is.True,error);
            yield return PhysicalCreationTestWait.Until(()=>((string)runtime.Scheduler.Invocation(run)?["phase"] is "completed" or "failed" or "cancelled"),"Sound maintenance did not drain");
            var receipt=runtime.Scheduler.Invocation(run);Assert.That((string)receipt["phase"],Is.EqualTo(expected),receipt.ToString());
            if(expected=="completed")Assert.That(CapabilityArguments.Validate((JObject)receipt["output"],BehaviourCatalog.Action(action).OutputSchema,out error),Is.True,error);
            check?.Invoke(request,receipt);
        }
        [UnityTest]public IEnumerator SharedSoundRemovalPreservesMetadataExactChatReimportAndHistoricalReceipt(){
            var asset=AudioLibrary.Inspect("Unused canonical sound",SoundFile());yield return SoundMaintenanceTask(editor.Sounds.SaveAsync(asset));yield return SoundMaintenanceReady(asset.Hash);
            Assert.That(RoomControls.Capabilities(editor),Does.Contain(AudioMaintenanceCapability.Feature));Assert.That(BehaviourCatalog.TryRead("audio.library.review",1,new JObject {["assetHash"]=asset.Hash},new BehaviourCatalog.FactContext(),out _),Is.False,"An activity-only fact must not shadow native context");
            var imports=editor.GetComponent<AudioImportWorkshop>();yield return SoundMaintenanceTask(imports.Refresh(CancellationToken.None));Assert.That((bool)imports.Library(0)["ready"],Is.True);
            JObject entry=null,removeRequest=null,removedReceipt=null;yield return SoundMaintenanceAction("audio.library.inspect",new JObject {["assetHash"]=asset.Hash},(_,receipt)=>{entry=(JObject)receipt["output"]["entry"].DeepClone();Assert.That((bool)receipt["output"]["usage"]["canRemove"],Is.True,receipt.ToString());});
            Assert.That(runtime.TryReadFact("audio.library.review",1,new JObject {["assetHash"]=asset.Hash},out var fact),Is.True);Assert.That(JToken.DeepEquals((JObject)fact.Value,entry),Is.True);
            string path=Path.Combine(directory,"audio",asset.Hash+".wav"),metadata=Path.Combine(directory,"audio",asset.Hash+".txt");var name=File.ReadAllBytes(metadata);
            var removeArgs=new JObject {["assetHash"]=asset.Hash,["inspectionRevision"]=entry["inspectionRevision"].DeepClone()};
            yield return SoundMaintenanceAction("audio.library.edit",removeArgs,(request,receipt)=>{removeRequest=request;removedReceipt=(JObject)receipt.DeepClone();Assert.That((bool)receipt["output"]["removed"],Is.True);});
            Assert.That(File.Exists(path),Is.False);Assert.That(File.ReadAllBytes(metadata),Is.EqualTo(name));Assert.That((bool)imports.Library(0)["ready"],Is.False);Assert.That((string)imports.Library(0)["error"],Does.Contain("refresh"));
            Assert.That(new RoomExecutions(editor).Execute(SoundMaintenanceCall("audio.library.edit",removeArgs),out var stale),Is.False);Assert.That(stale,Is.EqualTo("Inspect this sound and load its current file revision before removing the download"));
            var wire=AudioOffers(asset.Content);imports.SyncChat("chat",wire);string selection=null;
            yield return SoundMaintenanceAction("audio.import",new JObject {["operation"]="selectChat",["offerSet"]=wire["offerSet"].DeepClone(),["assetHash"]=asset.Hash},(_,receipt)=>selection=(string)receipt["output"]["requestId"]);AudioSend(imports,wire,asset.Content);
            yield return PhysicalCreationTestWait.Until(()=>(string)imports.Observe()["phase"]=="preview","Canonical chat sound preview did not finish");var preview=imports.Observe()["preview"].DeepClone();Assert.That((string)preview["assetHash"],Is.EqualTo(asset.Hash));
            yield return SoundMaintenanceAction("audio.import",new JObject {["operation"]="accept",["requestId"]=selection,["assetHash"]=asset.Hash},(_,receipt)=>Assert.That((string)receipt["output"]["assetHash"],Is.EqualTo(asset.Hash)));
            Assert.That(File.ReadAllBytes(path),Is.EqualTo(asset.Content));Assert.That(File.ReadAllBytes(metadata),Is.EqualTo(name));Assert.That(JToken.DeepEquals(imports.Observe()["preview"],preview),Is.True);Assert.That((string)imports.Receipt()["assetHash"],Is.EqualTo(asset.Hash));
            Assert.That(typeof(AudioImportWorkshop).GetField("asset",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(imports),Is.Null,"Completed preview evidence must not retain the large AudioAsset");Assert.That(editor.GetComponent<WorldAudio>(),Is.Null,"Reimport must not start playback");
            yield return SoundMaintenanceAction("audio.library.inspect",new JObject {["assetHash"]=asset.Hash},(_,receipt)=>{Assert.That(JToken.DeepEquals(receipt["output"]["entry"],entry),Is.True);Assert.That((bool)receipt["output"]["usage"]["current"],Is.True);});
            var reloaded=new InvocationReceipts(editor.ReceiptDirectory);Assert.That(reloaded.Error,Is.Null);Assert.That(JToken.DeepEquals(reloaded.Find((string)removeRequest["runId"]),removedReceipt),Is.True,"Stored removal receipt must remain valid after reimport");
            var actions=new RoomExecutions(editor);Assert.That(actions.Execute(removeRequest,out var error),Is.True,error);Assert.That(JToken.DeepEquals(actions.Observe()["selected"],removedReceipt),Is.True);yield return null;Assert.That(File.ReadAllBytes(path),Is.EqualTo(asset.Content));
            yield return SoundMaintenanceAction("audio.library.inspect",new JObject {["assetHash"]=asset.Hash},(_,receipt)=>Assert.That(JToken.DeepEquals(receipt["output"]["entry"],entry),Is.True));
            yield return SoundMaintenanceTask(imports.Refresh(CancellationToken.None));Assert.That((int)imports.Library(0)["total"],Is.EqualTo(1));
        }
        [UnityTest]public IEnumerator DamagedSoundRemainsProtectedByUnboundSourceAndUndo(){
            var asset=AudioLibrary.Inspect("Retained sound",SoundFile());yield return SoundMaintenanceTask(editor.Sounds.SaveAsync(asset));yield return SoundMaintenanceReady(asset.Hash);
            string id=Guid.NewGuid().ToString("N"),path=Path.Combine(directory,"audio",asset.Hash+".wav");var source=new RoomAudioDefinition {id=id,name="Unbound clip",kind="clip",assetHash=asset.Hash,seconds=1};Assert.That(editor.EditAudio(id,0,source,out var error),Is.True,error);File.WriteAllBytes(path,new byte[]{0});
            JObject entry=null;yield return SoundMaintenanceAction("audio.library.inspect",new JObject {["assetHash"]=asset.Hash},(_,receipt)=>{entry=(JObject)receipt["output"]["entry"];Assert.That((bool)entry["contentValid"],Is.False);Assert.That((bool)receipt["output"]["usage"]["current"],Is.True);Assert.That((bool)receipt["output"]["usage"]["canRemove"],Is.False);});
            yield return SoundMaintenanceAction("audio.library.edit",new JObject {["assetHash"]=asset.Hash,["inspectionRevision"]=entry["inspectionRevision"].DeepClone()},null,"failed");Assert.That(File.ReadAllBytes(path),Is.EqualTo(new byte[]{0}));
            Assert.That(editor.EditAudio(id,editor.AudioRevision(id),null,out error),Is.True,error);
            yield return SoundMaintenanceAction("audio.library.inspect",new JObject {["assetHash"]=asset.Hash},(_,receipt)=>{entry=(JObject)receipt["output"]["entry"];Assert.That((bool)receipt["output"]["usage"]["current"],Is.False);Assert.That((bool)receipt["output"]["usage"]["history"],Is.True);});
            yield return SoundMaintenanceAction("audio.library.edit",new JObject {["assetHash"]=asset.Hash,["inspectionRevision"]=entry["inspectionRevision"].DeepClone()},null,"failed");Assert.That(File.ReadAllBytes(path),Is.EqualTo(new byte[]{0}));editor.Undo();Assert.That(editor.ReadAudio(id).assetHash,Is.EqualTo(asset.Hash));
        }
        [UnityTest]public IEnumerator UnknownSavedSoundReferencesRefuseDamagedPayloadRemovalWithoutRepair(){
            var asset=AudioLibrary.Inspect("Protected unknown sound",SoundFile());yield return SoundMaintenanceTask(editor.Sounds.SaveAsync(asset));yield return SoundMaintenanceReady(asset.Hash);
            string future=Path.Combine(directory,"behaviours.v999.json"),path=Path.Combine(directory,"audio",asset.Hash+".wav");File.WriteAllText(future,"{\"version\":999}");File.WriteAllBytes(path,new byte[]{0});JObject entry=null;
            yield return SoundMaintenanceAction("audio.library.inspect",new JObject {["assetHash"]=asset.Hash},(_,receipt)=>{entry=(JObject)receipt["output"]["entry"];Assert.That((bool)entry["contentValid"],Is.False);Assert.That((bool)receipt["output"]["usage"]["uncertain"],Is.True);});
            yield return SoundMaintenanceAction("audio.library.edit",new JObject {["assetHash"]=asset.Hash,["inspectionRevision"]=entry["inspectionRevision"].DeepClone()},null,"failed");Assert.That(File.ReadAllText(future),Is.EqualTo("{\"version\":999}"));Assert.That(File.ReadAllBytes(path),Is.EqualTo(new byte[]{0}));
        }
        [UnityTest]public IEnumerator SoundRemovalStopKeepsFenceUntilDispatchedDeletionDrains(){
            var asset=AudioLibrary.Inspect("Stopped sound removal",SoundFile());yield return SoundMaintenanceTask(editor.Sounds.SaveAsync(asset));yield return SoundMaintenanceReady(asset.Hash);JObject entry=null;
            yield return SoundMaintenanceAction("audio.library.inspect",new JObject {["assetHash"]=asset.Hash},(_,receipt)=>entry=(JObject)receipt["output"]["entry"]);
            var imports=editor.GetComponent<AudioImportWorkshop>();yield return SoundMaintenanceTask(imports.Refresh(CancellationToken.None));
            using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();editor.Sounds.MaintenanceCheckpoint=phase=>{if(phase=="deleting"){entered.Set();if(!release.Wait(10000))throw new TimeoutException();}};
            var request=SoundMaintenanceCall("audio.library.edit",new JObject {["assetHash"]=asset.Hash,["inspectionRevision"]=entry["inspectionRevision"].DeepClone()});string run=(string)request["runId"];var actions=new RoomExecutions(editor);
            try{
                Assert.That(actions.Execute(request,out var error),Is.True,error);yield return PhysicalCreationTestWait.Until(()=>entered.IsSet||((string)runtime.Scheduler.Invocation(run)?["phase"]=="failed"),"Sound deletion did not reach dispatch");Assert.That(entered.IsSet,Is.True,runtime.Scheduler.Invocation(run)?.ToString());
                Assert.That(editor.WriteGate.Frozen,Is.True);Assert.That(editor.Sounds.Assets.TryPin(ImportedAssetKind.Audio,asset.Hash,out _),Is.Null);Assert.That((bool)imports.Library(0)["ready"],Is.False);
                int revision=workshop.Revision;workshop.NewSequence();Assert.That(workshop.Revision,Is.EqualTo(revision));Assert.That(actions.Execute(new JObject {["operation"]="cancel",["runId"]=run},out error),Is.True,error);
                Assert.That((string)runtime.Scheduler.Invocation(run)["phase"],Is.EqualTo("cancelled"));Assert.That((string)runtime.Scheduler.Invocation(run)["status"],Does.Contain("may still finish"));Assert.That(editor.WriteGate.Frozen,Is.True);
            }finally{release.Set();}
            yield return PhysicalCreationTestWait.Until(()=>!editor.WriteGate.Frozen,"Cancelled sound removal did not drain its holds");editor.Sounds.MaintenanceCheckpoint=null;Assert.That(File.Exists(Path.Combine(directory,"audio",asset.Hash+".wav")),Is.False);
        }
        [UnityTest]public IEnumerator ActionableSoundPreviewBlocksRemovalUntilItsOwnerCancels(){
            var asset=AudioLibrary.Inspect("Preview sound",SoundFile());yield return SoundMaintenanceTask(editor.Sounds.SaveAsync(asset));yield return SoundMaintenanceReady(asset.Hash);var imports=editor.GetComponent<AudioImportWorkshop>();using var picker=new SoundPicker();imports.SetPickerForTests(picker);
            string request=imports.Select();picker.Pick(asset.Content);yield return PhysicalCreationTestWait.Until(()=>(string)imports.Observe()["phase"]=="preview","Sound preview did not finish");Assert.That(editor.WriteGate.CanFreeze(out _),Is.False);JObject entry=null;
            yield return SoundMaintenanceAction("audio.library.inspect",new JObject {["assetHash"]=asset.Hash},(_,receipt)=>entry=(JObject)receipt["output"]["entry"]);
            yield return SoundMaintenanceAction("audio.library.edit",new JObject {["assetHash"]=asset.Hash,["inspectionRevision"]=entry["inspectionRevision"].DeepClone()},null,"failed");Assert.That(File.Exists(Path.Combine(directory,"audio",asset.Hash+".wav")),Is.True);
            imports.Cancel(request);Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);
            yield return SoundMaintenanceAction("audio.library.inspect",new JObject {["assetHash"]=asset.Hash},(_,receipt)=>entry=(JObject)receipt["output"]["entry"]);
            yield return SoundMaintenanceAction("audio.library.edit",new JObject {["assetHash"]=asset.Hash,["inspectionRevision"]=entry["inspectionRevision"].DeepClone()},(_,receipt)=>Assert.That((bool)receipt["output"]["removed"],Is.True));
        }
    }
}
