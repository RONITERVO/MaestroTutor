// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  [UnityTest]public IEnumerator DormantRestorationDoesNotNeedNativeCapacityToDiscardUnloadedEdits(){
   yield return WaitForModuleLibrary();string id=editor.Identity(block),area=NativeAreaFor(id);var original=editor.Read(id);
   Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
   Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);yield return DormantRestorationFixture.Save(editor);
   var changed=editor.Read(id);changed.name="Temporary dormant object";changed.position+=Vector3.right;
   Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{changed},Array.Empty<string>(),out error),Is.True,error);int revision=editor.ObjectRevision(id);
   var held=NativeObjectAdmissionFixture.Fill(editor.WorldIdentity);
   try{
    int before=(int)NativeObjectResources.ObserveBudget()["instances"];bool accepted=editor.DiscardTemporaryRoom(out error);
    string folder=Environment.GetEnvironmentVariable("MAESTRO_DORMANT_RESTORE_EVIDENCE");
    if(!string.IsNullOrEmpty(folder)){Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"pressure.json"),new JObject{
     ["accepted"]=accepted,["error"]=error,["before"]=before,["after"]=(int)NativeObjectResources.ObserveBudget()["instances"],["dormant"]=editor.NativeEntityDormant(id),["nativePresent"]=(bool)editor.Find(id),["temporary"]=editor.TemporaryRoom}.ToString());}
    Assert.That(accepted,Is.True,error);Assert.That(editor.NativeEntityDormant(id),Is.True);Assert.That(editor.Find(id),Is.Null);
    Assert.That((int)NativeObjectResources.ObserveBudget()["instances"],Is.EqualTo(before));Assert.That(editor.ObjectRevision(id),Is.GreaterThan(revision));
    Assert.That(JsonUtility.ToJson(editor.Read(id)),Is.EqualTo(JsonUtility.ToJson(original)));Assert.That(editor.TemporaryRoom,Is.False);Assert.That(workshop.Memory.Temporary,Is.False);
   }finally{foreach(var lease in held)lease.Dispose();}
   yield return NativeAreaCompletion(editor.ActivateNativeArea(area));Assert.That(editor.Find(id).transform.localPosition,Is.EqualTo(original.position));
  }
  [UnityTest]public IEnumerator DormantRestorationRecoversADeletedColdIdentityWithoutMaterializingIt(){
   yield return WaitForModuleLibrary();string id=editor.Identity(block),area=NativeAreaFor(id);var original=editor.Read(id);
   Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
   Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);yield return DormantRestorationFixture.Save(editor);
   Assert.That(editor.DeleteObject(id,out error),Is.True,error);Assert.That(editor.NativeEntityDormant(id),Is.False);Assert.That(editor.Read(id),Is.Null);
   var held=NativeObjectAdmissionFixture.Fill(editor.WorldIdentity);
   try{
    Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.NativeEntityDormant(id),Is.True);Assert.That(editor.Find(id),Is.Null);
    Assert.That(JsonUtility.ToJson(editor.Read(id)),Is.EqualTo(JsonUtility.ToJson(original)));Assert.That(editor.RegionFor(id),Is.EqualTo(area));
   }finally{foreach(var lease in held)lease.Dispose();}
   yield return NativeAreaCompletion(editor.ActivateNativeArea(area));Assert.That(editor.Identity(editor.Find(id)),Is.EqualTo(id));
  }
  [UnityTest]public IEnumerator DormantRestorationUsesTheDormancyOfTheLatestConfirmedSnapshot(){
   yield return WaitForModuleLibrary();string id=editor.Identity(block),area=NativeAreaFor(id);
   Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);yield return DormantRestorationFixture.Save(editor);
   Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);yield return null;
   var saved=editor.Read(id);saved.name="Kept cold checkpoint";saved.position+=Vector3.right*.3f;
   Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{saved},Array.Empty<string>(),out error),Is.True,error);
   Assert.That(editor.KeepTemporaryRoom(out error),Is.True,error);yield return DormantRestorationFixture.Save(editor);Assert.That(editor.TemporarySaveError,Is.Null);
   Assert.That(editor.DeleteObject(id,out error),Is.True,error);
   var held=NativeObjectAdmissionFixture.Fill(editor.WorldIdentity);
   try{
    Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.NativeEntityDormant(id),Is.True);Assert.That(editor.Find(id),Is.Null);
    Assert.That(JsonUtility.ToJson(editor.Read(id)),Is.EqualTo(JsonUtility.ToJson(saved)));
   }finally{foreach(var lease in held)lease.Dispose();}
   yield return NativeAreaCompletion(editor.ActivateNativeArea(area));Assert.That(editor.Find(id).transform.localPosition,Is.EqualTo(saved.position));
  }
  [UnityTest]public IEnumerator DormantRestorationRetainsAViewActivatedAfterItsColdCheckpoint(){
   yield return WaitForModuleLibrary();string id=editor.Identity(block),area=NativeAreaFor(id);var original=editor.Read(id);
   Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
   Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);yield return DormantRestorationFixture.Save(editor);
   yield return NativeAreaCompletion(editor.ActivateNativeArea(area));var view=editor.Find(id);
   var changed=editor.Read(id);changed.position+=Vector3.up;Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{changed},Array.Empty<string>(),out error),Is.True,error);
   Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.Find(id),Is.SameAs(view));Assert.That(editor.NativeEntityDormant(id),Is.False);
   Assert.That(view.transform.localPosition,Is.EqualTo(original.position));
  }
  [UnityTest]public IEnumerator DormantRestorationRevokesAnOldDisplayAttemptAndAllowsObservedReappearance(){
   yield return WaitForModuleLibrary();string id=RetireDisplayGroup();var original=editor.Read(id);yield return null;
   string error;
   Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);yield return DormantRestorationFixture.Save(editor);
   var changed=editor.Read(id);changed.name="Pending temporary display";changed.scale=1.5f;
   Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{changed},Array.Empty<string>(),out error),Is.True,error);
   NativeObservationCamera();editor.TickNativeDisplay();Assert.That(editor.NativePhase(id),Is.EqualTo(NativeEntityPhase.Preparing));
   Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.NativeEntityDormant(id),Is.True);
   yield return DisplayAppears(id);yield return DisplayFrames();Assert.That(editor.Find(id).transform.localPosition,Is.EqualTo(original.position));
   Assert.That(editor.Find(id).transform.localScale,Is.EqualTo(Vector3.one*original.scale));Assert.That(editor.Read(id).name,Is.EqualTo(original.name));
   root.AddComponent<Maestro.Quest.Diagnostics.RuntimeDiagnostics>();Assert.That(ObjectReservations(id).Length,Is.EqualTo(1));Assert.That(physics.Running,Is.False);
  }
 }
 public sealed partial class RoomEditorTests {
  [UnityTest]public IEnumerator DormantRestorationFailedKeepCannotReplaceTheSavedColdCheckpoint(){
   var saved=StartupSeparateAreas();yield return ReloadForStartup(saved.saved,observe:false);
   Assert.That(editor.NativeEntityDormant(saved.front),Is.True);
   Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);yield return DormantRestorationFixture.Save(editor);
   var activation=editor.ActivateNativeArea(editor.RegionFor(saved.front));float deadline=Time.realtimeSinceStartup+12;
   while(!activation.IsCompleted&&Time.realtimeSinceStartup<deadline)yield return null;
   Assert.That(activation.IsCompleted,Is.True);Assert.That(activation.Exception,Is.Null);Assert.That(activation.Result,Is.Null);
   Assert.That(editor.NativeEntityDormant(saved.front),Is.False);
   string pending=Path.Combine(directory,RoomStorage.FileName)+".pending";Directory.CreateDirectory(pending);
   try{
    Assert.That(editor.KeepTemporaryRoom(out error),Is.True,error);yield return DormantRestorationFixture.Save(editor);
    Assert.That(editor.TemporarySaveError,Is.Not.Null);Assert.That(editor.TemporarySaveRevision,Is.Zero);
   }finally{Directory.Delete(pending);}
   Assert.That(editor.DeleteObject(saved.front,out error),Is.True,error);yield return null;
   Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);
   Assert.That(editor.Read(saved.front),Is.Not.Null);Assert.That(editor.NativeEntityDormant(saved.front),Is.True);Assert.That(editor.Find(saved.front),Is.Null);
  }
 }
 static class DormantRestorationFixture {
  internal static IEnumerator Save(RoomEditor editor){
   float deadline=Time.realtimeSinceStartup+12;
   while(editor.TemporarySavePending&&Time.realtimeSinceStartup<deadline)yield return null;
   Assert.That(editor.TemporarySavePending,Is.False,"Temporary save did not finish");
  }
 }

}
