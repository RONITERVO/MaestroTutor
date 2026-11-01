// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Threading;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        JObject ImageMaintenanceCall(string action,JObject args)=>new(){["operation"]="start",["runId"]=new RoomExecutions(editor).Observe()["nextRunId"].DeepClone(),["call"]=new JObject {["id"]=action,["version"]=1,["arguments"]=args}};
        IEnumerator ImageMaintenanceReady(string hash){
            yield return PhysicalCreationTestWait.Until(()=>{workshop.RetainsAssetIdentity(hash,null,out bool uncertain);return !uncertain;},"Image reference stores did not become ready");
        }
        IEnumerator ImageMaintenanceAction(string action,JObject args,Action<JObject,JObject> check,string expected="completed"){
            var request=ImageMaintenanceCall(action,args);Assert.That(new RoomExecutions(editor).Execute(request,out var error),Is.True,error);string run=(string)request["runId"];
            yield return PhysicalCreationTestWait.Until(()=>((string)runtime.Scheduler.Invocation(run)?["phase"] is "completed" or "failed" or "cancelled"),"Image maintenance did not drain");
            var receipt=runtime.Scheduler.Invocation(run);Assert.That((string)receipt["phase"],Is.EqualTo(expected),receipt.ToString());
            if(expected=="completed")Assert.That(CapabilityArguments.Validate((JObject)receipt["output"],BehaviourCatalog.Action(action).OutputSchema,out error),Is.True,error);
            check?.Invoke(request,receipt);
        }
        [UnityTest]public IEnumerator SharedImageRemovalPreservesMetadataAndHistoricalReplayAfterReimport(){
            var asset=ImageLibrary.Inspect("Unused image",ImageFiles.Png());yield return ImageTask(editor.Images.SaveAsync(asset));yield return ImageMaintenanceReady(asset.Hash);
            var imports=root.AddComponent<ImageImportWorkshop>();imports.Initialize(editor);yield return ImageTask(imports.Refresh(CancellationToken.None));Assert.That((bool)imports.Library(0)["ready"],Is.True);
            JObject entry=null,removeRequest=null;yield return ImageMaintenanceAction("image.library.inspect",new JObject {["imageHash"]=asset.Hash},(_,receipt)=>{entry=(JObject)receipt["output"]["entry"];Assert.That((bool)receipt["output"]["usage"]["canRemove"],Is.True,receipt.ToString());});
            Assert.That(runtime.TryReadFact("image.library.review",1,new JObject {["imageHash"]=asset.Hash},out var fact),Is.True);Assert.That((string)((JObject)fact.Value)["inspectionRevision"],Is.EqualTo((string)entry["inspectionRevision"]));
            string path=Path.Combine(directory,"images",asset.Hash+".image"),metadata=Path.Combine(directory,"images",asset.Hash+".txt");var name=File.ReadAllBytes(metadata);
            yield return ImageMaintenanceAction("image.library.edit",new JObject {["imageHash"]=asset.Hash,["inspectionRevision"]=entry["inspectionRevision"].DeepClone()},(request,receipt)=>{removeRequest=request;Assert.That((bool)receipt["output"]["removed"],Is.True);});
            Assert.That(File.Exists(path),Is.False);Assert.That(File.ReadAllBytes(metadata),Is.EqualTo(name));Assert.That((bool)imports.Library(0)["ready"],Is.False,"Old cached pages cannot reintroduce a removed image");Assert.That((string)imports.Library(0)["error"],Does.Contain("refresh"));
            yield return ImageTask(editor.Images.SaveAsync(asset));Assert.That(File.ReadAllBytes(path),Is.EqualTo(asset.Content));
            Assert.That(new RoomExecutions(editor).Execute(removeRequest,out var error),Is.True,error);yield return null;Assert.That(File.ReadAllBytes(path),Is.EqualTo(asset.Content),"Historical removal replay must not delete a restored image");
            yield return ImageTask(imports.Refresh(CancellationToken.None));Assert.That((int)imports.Library(0)["total"],Is.EqualTo(1));
        }
        [UnityTest]public IEnumerator UnboundAppearanceAndUndoProtectItsImageDownload(){
            var asset=ImageLibrary.Inspect("Remembered appearance",ImageFiles.Png());yield return ImageTask(editor.Images.SaveAsync(asset));yield return ImageMaintenanceReady(asset.Hash);
            string id=Guid.NewGuid().ToString("N");var style=new RoomAppearance {id=id,name="Unbound",style=new AppearanceStyle {patternMode="image",imageHash=asset.Hash}};
            Assert.That(editor.EditAppearance(style,id,0,Array.Empty<string>(),out var error),Is.True,error);
            JObject entry=null;yield return ImageMaintenanceAction("image.library.inspect",new JObject {["imageHash"]=asset.Hash},(_,receipt)=>{entry=(JObject)receipt["output"]["entry"];Assert.That((bool)receipt["output"]["usage"]["current"],Is.True);Assert.That((bool)receipt["output"]["usage"]["canRemove"],Is.False);});
            yield return ImageMaintenanceAction("image.library.edit",new JObject {["imageHash"]=asset.Hash,["inspectionRevision"]=entry["inspectionRevision"].DeepClone()},null,"failed");
            Assert.That(editor.EditAppearance(null,id,editor.AppearanceRevision(id),Array.Empty<string>(),out error),Is.True,error);
            yield return ImageMaintenanceAction("image.library.inspect",new JObject {["imageHash"]=asset.Hash},(_,receipt)=>{entry=(JObject)receipt["output"]["entry"];Assert.That((bool)receipt["output"]["usage"]["current"],Is.False);Assert.That((bool)receipt["output"]["usage"]["history"],Is.True);});
            yield return ImageMaintenanceAction("image.library.edit",new JObject {["imageHash"]=asset.Hash,["inspectionRevision"]=entry["inspectionRevision"].DeepClone()},null,"failed");
            editor.Undo();Assert.That(editor.ReadAppearance(id)?.style.imageHash,Is.EqualTo(asset.Hash));var loaded=editor.Images.ReadAsync(asset.Hash);yield return ImageTask(loaded);Assert.That(loaded.Result.Hash,Is.EqualTo(asset.Hash));
        }
        [UnityTest]public IEnumerator UnknownSavedImageReferencesRefuseDeletionWithoutRepair(){
            var asset=ImageLibrary.Inspect("Protected unknown",ImageFiles.Png());yield return ImageTask(editor.Images.SaveAsync(asset));yield return ImageMaintenanceReady(asset.Hash);
            string future=Path.Combine(directory,"behaviours.v999.json");File.WriteAllText(future,"{\"version\":999}");JObject entry=null;
            yield return ImageMaintenanceAction("image.library.inspect",new JObject {["imageHash"]=asset.Hash},(_,receipt)=>{entry=(JObject)receipt["output"]["entry"];Assert.That((bool)receipt["output"]["usage"]["uncertain"],Is.True);});
            yield return ImageMaintenanceAction("image.library.edit",new JObject {["imageHash"]=asset.Hash,["inspectionRevision"]=entry["inspectionRevision"].DeepClone()},null,"failed");
            Assert.That(File.ReadAllText(future),Is.EqualTo("{\"version\":999}"));Assert.That(File.Exists(Path.Combine(directory,"images",asset.Hash+".image")),Is.True);
        }
        [UnityTest]public IEnumerator ImageRemovalStopKeepsTheFenceUntilDispatchedDeletionDrains(){
            var asset=ImageLibrary.Inspect("Stopped removal",ImageFiles.Png());yield return ImageTask(editor.Images.SaveAsync(asset));yield return ImageMaintenanceReady(asset.Hash);
            JObject entry=null;yield return ImageMaintenanceAction("image.library.inspect",new JObject {["imageHash"]=asset.Hash},(_,receipt)=>entry=(JObject)receipt["output"]["entry"]);
            var imports=root.AddComponent<ImageImportWorkshop>();imports.Initialize(editor);yield return ImageTask(imports.Refresh(CancellationToken.None));
            using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();editor.Images.MaintenanceCheckpoint=phase=>{if(phase=="deleting"){entered.Set();if(!release.Wait(10000))throw new TimeoutException();}};
            var request=ImageMaintenanceCall("image.library.edit",new JObject {["imageHash"]=asset.Hash,["inspectionRevision"]=entry["inspectionRevision"].DeepClone()});string run=(string)request["runId"];var actions=new RoomExecutions(editor);
            try{
                Assert.That(actions.Execute(request,out var error),Is.True,error);yield return PhysicalCreationTestWait.Until(()=>entered.IsSet||((string)runtime.Scheduler.Invocation(run)?["phase"]=="failed"),"Deletion did not reach dispatch");Assert.That(entered.IsSet,Is.True,runtime.Scheduler.Invocation(run)?.ToString());
                Assert.That(editor.WriteGate.Frozen,Is.True);Assert.Throws<InvalidOperationException>(()=>editor.ImageTextures.Acquire(asset.Hash));Assert.That((bool)imports.Library(0)["ready"],Is.False);
                int revision=workshop.Revision;workshop.NewSequence();Assert.That(workshop.Revision,Is.EqualTo(revision));
                Assert.That(actions.Execute(new JObject {["operation"]="cancel",["runId"]=run},out error),Is.True,error);Assert.That((string)runtime.Scheduler.Invocation(run)["phase"],Is.EqualTo("cancelled"));Assert.That((string)runtime.Scheduler.Invocation(run)["status"],Does.Contain("may still finish"));Assert.That(editor.WriteGate.Frozen,Is.True);
            }finally{release.Set();}
            yield return PhysicalCreationTestWait.Until(()=>!editor.WriteGate.Frozen,"Cancelled removal did not drain its holds");editor.Images.MaintenanceCheckpoint=null;Assert.That(File.Exists(Path.Combine(directory,"images",asset.Hash+".image")),Is.False);
        }
    }
}
