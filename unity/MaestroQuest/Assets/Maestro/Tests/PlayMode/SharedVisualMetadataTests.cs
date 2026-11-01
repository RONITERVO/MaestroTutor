// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Art;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  Material SharedVisualMaterial(string target)=>editor.Find(target).GetComponentsInChildren<Renderer>()
   .First(r=>!r.GetComponent<PencilMarks>()).sharedMaterial;
  JObject SharedLayerArgs(string id,float opacity)=>new(){["id"]=id,["revision"]=editor.VisibilityRevision(id),
   ["name"]=editor.ReadVisibility(id).name,["opacity"]=opacity,["realDepth"]=false,["members"]=new JArray(editor.VisibilityMembers(id))};
  RoomAgentRequest SharedVisualRequest(string capability,JObject args,string[] members){
   var request=TemplateRequest(new JObject{["id"]=capability,["version"]=1,["arguments"]=args});
   request.conditions=members.Select(id=>new RoomObjectCondition{id=id,revision=editor.ObjectRevision(id)}).ToArray();return request;
  }
  [UnityTest]public IEnumerator SharedVisualMetadataFullWorldEditsGuardEveryMemberWithoutNativeAdmission(){
   yield return WaitForModuleLibrary();string appearance=NewAppearance(),layer=NewVisualLayer();
   foreach(var data in editor.Snapshot().objects.Where(x=>!x.IsBuiltIn)){
    Assert.That(AssignAppearance(data.id,appearance,out var issue),Is.True,issue);
    Assert.That(AssignVisualLayer(data.id,layer,out issue),Is.True,issue);
   }
   int remaining=RoomDocument.MaximumObjects+2-editor.Snapshot().objects.Length;
   string storageArea=NewRegion();
   var prototype=new CreationPrototype{kind="block",physics=new ObjectPhysicsSettings{mode="fixed",shape="automatic",mass=.5f}};
   while(remaining>0){int count=Math.Min(16,remaining);
    var batch=new CreationBatch{position=new Vector3(2,1,1),blueprint=new CreationBlueprint{pieces=Enumerable.Range(0,count).Select(i=>new CreationPiece{
     slot="piece_"+i,name="Distant building",source=new CreationSource{kind="prototype",prototype=prototype}}).ToArray()}};
    Assert.That(editor.CreateBatch(batch,out var created,out var issue),Is.True,issue);
    foreach(string id in created){Assert.That(AssignAppearance(id,appearance,out issue),Is.True,issue);Assert.That(AssignVisualLayer(id,layer,out issue),Is.True,issue);Assert.That(AssignRegion(id,storageArea,out issue),Is.True,issue);}
    // Populate through ordinary verified bindings, retiring each cohort before
    // creating the next so fixture setup itself respects native capacity.
    yield return NativeAreaCompletion(editor.RetireNativeArea(storageArea));remaining-=count;
   }
   var members=editor.AppearanceMembers(appearance);Assert.That(members.Length,Is.EqualTo(256));
   Assert.That(editor.VisibilityMembers(layer),Is.EquivalentTo(members));
   var cold=members.Where(editor.NativeEntityDormant).ToArray();Assert.That(cold.Length,Is.GreaterThan(240));
   var originalObjects=editor.Snapshot().objects.Select(JsonUtility.ToJson).ToArray();
   var reserved=NativeObjectAdmissionFixture.Fill(editor.WorldIdentity);
   try{
    foreach(bool visual in new[]{false,true}){
     var args=visual?SharedLayerArgs(layer,.25f):AppearanceArgs(appearance);if(!visual)args["style"]["opacity"]=.7f;
     string capability=visual?"visibility.layer.save":"appearance.save";
     var executor=new RoomAgentExecutor(editor);var request=SharedVisualRequest(capability,args,members);
     request.conditions[255].revision--;Assert.That(executor.Execute(request,out var error,out _),Is.False);
     request.conditions[255].revision=editor.ObjectRevision(members[255]);
     Assert.That(executor.Execute(request,out error,out _),Is.True,error);yield return SpatialSettingsCompletion.Wait(executor.Executions);
     var receipt=executor.Executions.Observe()["selected"];Assert.That((string)receipt["phase"],Is.EqualTo("completed"),receipt.ToString());
     Assert.That(((JArray)receipt["resources"]).Values<string>(),Is.EquivalentTo(members));
     var disk=new RoomStorage(directory).Load(out error);Assert.That(error,Is.Null);
     Assert.That(visual?disk.visibilityLayers.Single(x=>x.id==layer).opacity:disk.appearances.Single(x=>x.id==appearance).style.opacity,Is.EqualTo(visual ? .25f:.7f));
     editor.Undo();Assert.That(visual?editor.ReadVisibility(layer).opacity:editor.ReadAppearance(appearance).style.opacity,Is.EqualTo(visual ? .5f:.4f));
     Assert.That(executor.Execute(request,out error,out _),Is.True,error);
     Assert.That(visual?editor.ReadVisibility(layer).opacity:editor.ReadAppearance(appearance).style.opacity,Is.EqualTo(visual ? .5f:.4f),"A completed receipt must not replay an undone action");
     editor.Redo();Assert.That(visual?editor.ReadVisibility(layer).opacity:editor.ReadAppearance(appearance).style.opacity,Is.EqualTo(visual ? .25f:.7f));
     Assert.That(cold.All(id=>editor.NativeEntityDormant(id)&&!editor.Find(id)),Is.True);
     Assert.That((int)NativeObjectResources.ObserveBudget()["instances"],Is.EqualTo(NativeObjectResources.MaximumInstances));
    }
   }finally{foreach(var lease in reserved)lease.Dispose();}
   Assert.That(editor.Snapshot().objects.Select(JsonUtility.ToJson),Is.EqualTo(originalObjects),"Definition edits must preserve object physics, motion and placement");
   string area=NativeAreaFor(cold[0]);yield return NativeAreaCompletion(editor.ActivateNativeArea(area));
   Assert.That(SharedVisualMaterial(cold[0]).GetFloat("_SurfaceOpacity"),Is.EqualTo(.7f));
   Assert.That(SharedVisualMaterial(cold[0]).GetFloat("_VisibilityOpacity"),Is.EqualTo(.25f));
  }
  [UnityTest]public IEnumerator SharedVisualMetadataColdAssignmentPreservesRetiredPoseAndRendersAfterLoad(){
   string target=editor.Identity(block),layer=NewVisualLayer(),area=NativeAreaFor(target);
   block.transform.localPosition+=Vector3.up*.3f;var pose=block.transform.localPosition;
   yield return NativeAreaCompletion(editor.RetireNativeArea(area));
   var executor=new RoomAgentExecutor(editor);var request=TemplateRequest(LayerCall(target,layer));
   request.conditions=new[]{new RoomObjectCondition{id=target,revision=editor.ObjectRevision(target)}};
   Assert.That(executor.Execute(request,out var error,out _),Is.True,error);yield return SpatialSettingsCompletion.Wait(executor.Executions);
   Assert.That((string)executor.Executions.Observe()["selected"]["phase"],Is.EqualTo("completed"));
   Assert.That(editor.Find(target),Is.Null);Assert.That(editor.Read(target).visibilityLayer,Is.EqualTo(layer));
   Assert.That(new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==target).visibilityLayer,Is.EqualTo(layer),error);
   editor.Undo();Assert.That(editor.Read(target).visibilityLayer,Is.Empty);Assert.That(editor.Find(target),Is.Null);
   editor.Redo();Assert.That(editor.Read(target).visibilityLayer,Is.EqualTo(layer));Assert.That(editor.Find(target),Is.Null);
   yield return NativeAreaCompletion(editor.ActivateNativeArea(area));
   Assert.That(editor.Find(target).transform.localPosition,Is.EqualTo(pose));Assert.That(SharedVisualMaterial(target).GetFloat("_VisibilityOpacity"),Is.EqualTo(.5f));
  }
  [UnityTest]public IEnumerator SharedVisualMetadataColdCancellationAndFailedSavePublishNothing(){
   string target=editor.Identity(block),appearance=NewAppearance(),layer=NewVisualLayer();
   Assert.That(AssignAppearance(target,appearance,out var error),Is.True,error);Assert.That(AssignVisualLayer(target,layer,out error),Is.True,error);
   yield return NativeAreaCompletion(editor.RetireNativeArea(NativeAreaFor(target)));
   var before=JsonUtility.ToJson(editor.Snapshot());var bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));int revision=editor.Revision;
   foreach(bool visual in new[]{false,true}){
    var style=editor.ReadAppearance(appearance);style.style.opacity=.8f;var visibility=editor.ReadVisibility(layer);visibility.opacity=.2f;
    RoomEditor.PendingPersistedEdit pending;
    bool begun=visual?editor.BeginVisibility(visibility,layer,editor.VisibilityRevision(layer),new[]{target},out pending,out error)
     :editor.BeginAppearance(style,appearance,editor.AppearanceRevision(appearance),new[]{target},out pending,out error);
    Assert.That(begun,Is.True,error);try{yield return Prepared(pending);}finally{pending.Dispose();}yield return Drained(pending);
    Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));
    string obstacle=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(obstacle);
    try{bool ok=visual?editor.EditVisibility(visibility,layer,editor.VisibilityRevision(layer),new[]{target},out error)
      :editor.EditAppearance(style,appearance,editor.AppearanceRevision(appearance),new[]{target},out error);
     Assert.That(ok,Is.False);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));
     CollectionAssert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));
    }finally{Directory.Delete(obstacle);}
    Assert.That(editor.NativeEntityDormant(target)&&!editor.Find(target),Is.True);
   }
  }
  [UnityTest]public IEnumerator SharedVisualMetadataRetirementAndLostViewDoNotBecomeEditableColdObjects(){
   string target=editor.Identity(block),appearance=NewAppearance(),layer=NewVisualLayer();
   Assert.That(AssignAppearance(target,appearance,out var error),Is.True,error);Assert.That(AssignVisualLayer(target,layer,out error),Is.True,error);
   string area=NativeAreaFor(target);var style=editor.ReadAppearance(appearance);style.style.opacity=.8f;var visibility=editor.ReadVisibility(layer);visibility.opacity=.2f;
   Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);Assert.That(editor.NativePhase(target),Is.EqualTo(NativeEntityPhase.Retiring));
   Assert.That(editor.EditAppearance(style,appearance,editor.AppearanceRevision(appearance),new[]{target},out _),Is.False);
   Assert.That(editor.EditVisibility(visibility,layer,editor.VisibilityRevision(layer),new[]{target},out _),Is.False);Assert.That(AssignVisualLayer(target,"",out _),Is.False);
   yield return null;
   Assert.That(editor.EditAppearance(style,appearance,editor.AppearanceRevision(appearance),new[]{target},out error),Is.True,error);
   Assert.That(editor.EditVisibility(visibility,layer,editor.VisibilityRevision(layer),new[]{target},out error),Is.True,error);
   Assert.That(AssignAppearance(target,appearance,out _),Is.False,"Material binding still requires a verified native surface");
   yield return NativeAreaCompletion(editor.ActivateNativeArea(area));UnityEngine.Object.Destroy(editor.Find(target).gameObject);yield return null;
   Assert.That(editor.NativeEntityDormant(target),Is.False);
   Assert.That(editor.EditAppearance(style,appearance,editor.AppearanceRevision(appearance),new[]{target},out _),Is.False);
   Assert.That(editor.EditVisibility(visibility,layer,editor.VisibilityRevision(layer),new[]{target},out _),Is.False);Assert.That(AssignVisualLayer(target,"",out _),Is.False);
  }
  [UnityTest]public IEnumerator SharedVisualMetadataTemporaryKeepAndDiscardRetainColdMembership(){
   yield return WaitForModuleLibrary();string target=editor.Identity(block),appearance=NewAppearance(),layer=NewVisualLayer();
   Assert.That(AssignAppearance(target,appearance,out var error),Is.True,error);Assert.That(AssignVisualLayer(target,layer,out error),Is.True,error);
   string area=NativeAreaFor(target);yield return NativeAreaCompletion(editor.RetireNativeArea(area));
   foreach(bool keep in new[]{false,true}){
    Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);yield return DormantRestorationFixture.Save(editor);
    var style=editor.ReadAppearance(appearance);style.style.opacity=.8f;var visibility=editor.ReadVisibility(layer);visibility.opacity=.2f;
    Assert.That(editor.EditAppearance(style,appearance,editor.AppearanceRevision(appearance),new[]{target},out error),Is.True,error);
    Assert.That(editor.EditVisibility(visibility,layer,editor.VisibilityRevision(layer),new[]{target},out error),Is.True,error);
    Assert.That(keep?editor.KeepTemporaryRoom(out error):editor.DiscardTemporaryRoom(out error),Is.True,error);yield return DormantRestorationFixture.Save(editor);
    Assert.That(editor.NativeEntityDormant(target)&&!editor.Find(target),Is.True);
    Assert.That(editor.ReadAppearance(appearance).style.opacity,Is.EqualTo(keep ? .8f:.4f));Assert.That(editor.ReadVisibility(layer).opacity,Is.EqualTo(keep ? .2f:.5f));
   }
   yield return NativeAreaCompletion(editor.ActivateNativeArea(area));Assert.That(SharedVisualMaterial(target).GetFloat("_SurfaceOpacity"),Is.EqualTo(.8f));
   Assert.That(SharedVisualMaterial(target).GetFloat("_VisibilityOpacity"),Is.EqualTo(.2f));
  }
 }
}
