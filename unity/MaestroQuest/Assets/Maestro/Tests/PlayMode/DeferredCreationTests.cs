// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  [UnityTest]public IEnumerator DeferredCreationAllKindsSaveAtFullNativeBudgetAndHistoryStaysCold(){
   yield return WaitForModuleLibrary();var held=NativeObjectAdmissionFixture.Fill(editor.WorldIdentity);
   try{
    foreach(var kind in PendingCreationKinds){
     var call=PendingCreationCall(kind);var args=(JObject)call["arguments"];args["loading"]="onDemand";
     int count=editor.Snapshot().objects.Length;var executor=new RoomAgentExecutor(editor);var request=ObjectEditRequest(call);
     Assert.That(executor.Execute(request,out var error,out _),Is.True,error);yield return AgentPlacementCompleted(executor);
     string id=(string)executor.Executions.Observe()["selected"]["output"]["objectId"];
     Assert.That(editor.HasSavedObject(id),Is.True);Assert.That(editor.NativeEntityDormant(id),Is.True);Assert.That(editor.Find(id),Is.Null);
     Assert.That(new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==id).name,Is.EqualTo("Pending "+kind));
     Assert.That((int)NativeObjectResources.ObserveBudget()["instances"],Is.EqualTo(NativeObjectResources.MaximumInstances));
     editor.Undo();Assert.That(editor.HasSavedObject(id),Is.False);editor.Redo();Assert.That(editor.NativeEntityDormant(id),Is.True);Assert.That(editor.Find(id),Is.Null);
     Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+1));
     editor.Undo();
    }
   }finally{foreach(var lease in held)lease.Dispose();}
  }
  [UnityTest]public IEnumerator DeferredCreationBatchSavesAtomicallyAtFullNativeBudgetAndCanLoadLater(){
   var call=BatchCall();call["arguments"]["loading"]="onDemand";var held=NativeObjectAdmissionFixture.Fill(editor.WorldIdentity);string[] ids;
   try{
    var executor=new RoomAgentExecutor(editor);Assert.That(executor.Execute(TemplateRequest(call),out var error,out _),Is.True,error);
    ids=((JArray)executor.Executions.Observe()["selected"]["output"]["objectIds"]).Values<string>().ToArray();
    Assert.That(ids.Length,Is.EqualTo(6));Assert.That(ids.All(editor.NativeEntityDormant),Is.True);Assert.That(ids.All(id=>!editor.Find(id)),Is.True);
    Assert.That(ids.All(id=>new RoomStorage(directory).Load(out _).objects.Any(x=>x.id==id)),Is.True);
    editor.Undo();Assert.That(ids.Any(editor.HasSavedObject),Is.False);editor.Redo();Assert.That(ids.All(editor.NativeEntityDormant),Is.True);
   }finally{foreach(var lease in held)lease.Dispose();}
   var area=NativeAreaFor(ids[0]);yield return NativeAreaCompletion(editor.ActivateNativeArea(area));
   Assert.That(editor.Find(ids[0]),Is.Not.Null);Assert.That(editor.Find(ids[0]).Grab,Is.Not.Null);
  }
  [UnityTest]public IEnumerator DeferredCreationCancelledOrFailedSaveHasNoDormantIdentity(){
   Assert.That(editor.TryFlush(out var error),Is.True,error);int count=editor.Snapshot().objects.Length;
   Assert.That(editor.BeginCreatePrimitive(RoomObjectKind.Block,"Cancelled",Vector3.one,1,Color.blue,out var id,out var pending,out error,deferCreations:true),Is.True,error);
   yield return Prepared(pending);pending.Dispose();yield return Drained(pending);
   Assert.That(editor.HasSavedObject(id),Is.False);Assert.That(editor.NativeEntityDormant(id),Is.False);
   string obstacle=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(obstacle);
   try{
    Assert.That(editor.BeginCreatePrimitive(RoomObjectKind.Block,"Failed",Vector3.one,1,Color.blue,out id,out pending,out error,deferCreations:true),Is.True,error);
    using(pending){yield return Rejected(pending);Assert.That(pending.Accepted,Is.False);}
    yield return Drained(pending);Assert.That(editor.NativeEntityDormant(id),Is.False);Assert.That(editor.HasSavedObject(id),Is.False);
   }finally{Directory.Delete(obstacle);}
   Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));
  }
  [UnityTest]public IEnumerator DeferredCreationDeletedColdObjectUndoAndRedoDoNotDemandNativeCapacity(){
   string id=editor.Identity(block),area=NativeAreaFor(id);var original=editor.Read(id);
   Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
   Assert.That(editor.DeleteObject(id,out error),Is.True,error);var held=NativeObjectAdmissionFixture.Fill(editor.WorldIdentity);
   try{
    editor.Undo();Assert.That(editor.NativeEntityDormant(id),Is.True);Assert.That(editor.Find(id),Is.Null);
    Assert.That(JsonUtility.ToJson(editor.Read(id)),Is.EqualTo(JsonUtility.ToJson(original)));
    editor.Redo();Assert.That(editor.HasSavedObject(id),Is.False);editor.Undo();Assert.That(editor.NativeEntityDormant(id),Is.True);
   }finally{foreach(var lease in held)lease.Dispose();}
   yield return NativeAreaCompletion(editor.ActivateNativeArea(area));Assert.That(editor.Find(id),Is.Not.Null);
  }
  [UnityTest]public IEnumerator DeferredCreationLiveActivationSupersedesColdHistoryAndFailedRedoCanRetry(){
   Assert.That(editor.BeginCreatePrimitive(RoomObjectKind.Block,"Later live",Vector3.one,1,Color.blue,out var id,out var pending,out var error,deferCreations:true),Is.True,error);
   using(pending)yield return Completed(pending);
   yield return NativeAreaCompletion(editor.AcquireNativeEntities(new[]{id},()=>true,System.Threading.CancellationToken.None));Assert.That(editor.Find(id),Is.Not.Null);
   editor.Undo();yield return null;var held=NativeObjectAdmissionFixture.Fill(editor.WorldIdentity);
   try{editor.Redo();Assert.That(editor.HasSavedObject(id),Is.False);Assert.That(editor.CanRedo,Is.True);}
   finally{foreach(var lease in held)lease.Dispose();}
   editor.Redo();Assert.That(editor.Find(id),Is.Not.Null);Assert.That(editor.NativeEntityDormant(id),Is.False);
  }
  [UnityTest]public IEnumerator DeferredCreationKeptDeletionUndoRestoresColdSavedCheckpoint(){
   yield return WaitForModuleLibrary();string id=editor.Identity(block),area=NativeAreaFor(id);
   Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
   Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);yield return DormantRestorationFixture.Save(editor);
   Assert.That(editor.DeleteObject(id,out error),Is.True,error);Assert.That(editor.KeepTemporaryRoom(out error),Is.True,error);yield return DormantRestorationFixture.Save(editor);
   Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.HasSavedObject(id),Is.False);
   var held=NativeObjectAdmissionFixture.Fill(editor.WorldIdentity);
   try{editor.Undo();Assert.That(editor.NativeEntityDormant(id),Is.True);Assert.That(editor.Find(id),Is.Null);editor.Redo();Assert.That(editor.HasSavedObject(id),Is.False);}
   finally{foreach(var lease in held)lease.Dispose();}
  }
  [UnityTest]public IEnumerator DeferredCreationProgramWaitsForNativeObjectsBeforePaintingReturnedIds(){
   Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);yield return DormantRestorationFixture.Save(editor);
   var before=editor.Snapshot().objects.Select(o=>o.id).ToHashSet();var source=BatchSource();source["functions"][0]["body"][0]["arguments"]["loading"]="onDemand";
   Assert.That(workshop.Execute(new RuleRequest{action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit{kind="save",reference="deferred",sequence=new RuleSequence{id="",name="Build then paint",program=source.ToString()}}}},out error,out var ids),Is.True,error);
   Assert.That(runtime.Trigger(ids.Single()),Is.True,runtime.Scheduler.LastError);float deadline=Time.realtimeSinceStartup+15;
   while(runtime.Scheduler.RunningCount>0&&Time.realtimeSinceStartup<deadline){runtime.Scheduler.Tick(Time.unscaledTime);yield return null;}
   Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That(runtime.Scheduler.Outcomes.Last().phase,Is.EqualTo("completed"),runtime.Scheduler.LastError);
   var created=editor.Snapshot().objects.Where(o=>!before.Contains(o.id)).ToArray();Assert.That(created.Length,Is.EqualTo(6));
   Assert.That(created.All(o=>o.color==new Color(.2f,.6f,.9f,1)),Is.True);Assert.That(created.All(o=>editor.Find(o.id)),Is.True);
  }
  [UnityTest]public IEnumerator DeferredCreationObservedObjectAppearsWithItsSavedPose(){
   Assert.That(editor.BeginCreatePrimitive(RoomObjectKind.Block,"Observed",new Vector3(0,1,2),1,Color.blue,out var id,out var pending,out var error,deferCreations:true),Is.True,error);
   using(pending)yield return Completed(pending);
   Assert.That(editor.NativeEntityDormant(id),Is.True);var data=editor.Read(id);NativeObservationCamera();editor.TickNativeDisplay();
   yield return DisplayAppears(id);Assert.That(editor.Find(id).transform.localPosition,Is.EqualTo(data.position));Assert.That(editor.Read(id).name,Is.EqualTo("Observed"));
  }
 }
}
