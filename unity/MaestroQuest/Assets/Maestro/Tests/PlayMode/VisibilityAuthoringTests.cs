// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        string NewVisualLayer() {
            var cap=new VisibilityLayerSaveCapability();Assert.That(cap.Start(new CapabilityContext(editor,animations),"layer",cap.Example,out var op,out var error),Is.True,error);return (string)op.Result["id"];
        }
        bool AssignVisualLayer(string target,string id,out string error)=>editor.BindVisibility(target,editor.ObjectRevision(target),id,editor.VisibilityRevision(id),out error);
        JObject LayerCall(string target,string id)=>new(){["id"]="object.visibility.assign",["version"]=1,["arguments"]=new JObject{["target"]=target,["revision"]=editor.ObjectRevision(target),["layerId"]=id,["layerRevision"]=editor.VisibilityRevision(id)}};
        [UnityTest] public IEnumerator VisualLayersSaveRenderObserveAndUndoWithoutChangingPhysics() {
            Assert.That(RoomControls.Capabilities(editor),Does.Contain(VisibilityLayerCapability.Feature));
            string id=NewVisualLayer(),target=AppearanceBlock(),style=NewAppearance();Assert.That(AssignAppearance(target,style,out var error),Is.True,error);
            var physicsBefore=editor.Read(target).physics;var item=editor.Find(target);var body=item.GetComponent<Rigidbody>();body.isKinematic=false;body.useGravity=false;
            body.linearVelocity=new Vector3(1,2,3);body.angularVelocity=new Vector3(.1f,.2f,.3f);var position=item.transform.position;
            Assert.That(AssignVisualLayer(target,id,out error),Is.True,error);
            Assert.That(body.linearVelocity,Is.EqualTo(new Vector3(1,2,3)));Assert.That(body.angularVelocity,Is.EqualTo(new Vector3(.1f,.2f,.3f)));Assert.That(item.transform.position,Is.EqualTo(position));Assert.That(editor.Read(target).physics,Is.EqualTo(physicsBefore));body.isKinematic=true;
            Assert.That(AppearanceBlockMaterial().GetFloat("_VisibilityOpacity"),Is.EqualTo(.5f));Assert.That(AppearanceBlockMaterial().GetFloat("_SurfaceOpacity"),Is.EqualTo(.4f));
            var context=new BehaviourCatalog.FactContext(editor:editor);
            Assert.That(VisibilityLayerFacts.Layer().TryRead(context,1,new JObject{["id"]=id},out _),Is.True);
            Assert.That(VisibilityLayerFacts.Layers().TryRead(context,1,new JObject{["offset"]=0},out _),Is.True);
            Assert.That(VisibilityLayerFacts.Members().TryRead(context,1,new JObject{["id"]=id,["offset"]=0},out _),Is.True);
            Assert.That(VisibilityLayerFacts.Binding().TryRead(context,1,new JObject{["target"]=target},out _),Is.True);
            var saved=new RoomStorage(directory).Load(out error);Assert.That(saved.objects.Single(o=>o.id==target).visibilityLayer,Is.EqualTo(id),error);
            editor.Undo();Assert.That(editor.Read(target).visibilityLayer,Is.Empty);Assert.That(AppearanceBlockMaterial().GetFloat("_VisibilityOpacity"),Is.EqualTo(1));
            editor.Redo();Assert.That(AppearanceBlockMaterial().GetFloat("_VisibilityOpacity"),Is.EqualTo(.5f));yield return null;
        }
        [UnityTest] public IEnumerator VisualLayerChangesCoexistWithScheduledPartAnimationAndReplayOnce() {
            string target=RecipeTarget(),id=NewVisualLayer();WorldEmitter(target,WorldSound(5));var audio=WorldAudio.For(editor);Assert.That(audio.Begin(target,"sound",out var voice,out var audioError),Is.True,audioError);
            for(int i=0;i<180&&!voice.Output&&!voice.Closed;i++)yield return null;Assert.That(voice.Output,Is.Not.Null,voice.Error);var originalVoice=voice.Output;
            Assert.That(runtime.Scheduler.Invoke(PartCall(target,"RightUpperArm",10),Time.unscaledTime,out var animation,out var error),Is.True,error);
            yield return new WaitForSeconds(.1f);var recipe=editor.Find(target).GetComponent<RecipeObject>();var before=recipe.Part("RightUpperArm").localRotation;
            var call=LayerCall(target,id);Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out var assigned,out error),Is.True,error);
            Assert.That((string)runtime.Scheduler.Invocation(assigned)["phase"],Is.EqualTo("completed"));Assert.That((string)runtime.Scheduler.Invocation(animation)["phase"],Is.EqualTo("running"));
            Assert.That(Quaternion.Angle(before,recipe.Part("RightUpperArm").localRotation),Is.LessThan(.001f));
            yield return new WaitForSeconds(.2f);Assert.That(Quaternion.Angle(before,recipe.Part("RightUpperArm").localRotation),Is.GreaterThan(1));
            var cap=new VisibilityLayerSaveCapability();var args=cap.Example;args["id"]=id;args["revision"]=editor.VisibilityRevision(id);args["members"]=new JArray(target);args["opacity"]=.2;
            var executor=new RoomAgentExecutor(editor);var request=TemplateRequest(new JObject{["id"]=cap.Id,["version"]=1,["arguments"]=args});request.conditions=new[]{new RoomObjectCondition{id=target,revision=editor.ObjectRevision(target)}};
            Assert.That(executor.Execute(request,out error,out _),Is.True,error);int revision=editor.VisibilityRevision(id);
            Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(editor.VisibilityRevision(id),Is.EqualTo(revision));Assert.That((string)runtime.Scheduler.Invocation(animation)["phase"],Is.EqualTo("running"));
            Assert.That(voice.Closed,Is.False,voice.Error);Assert.That(voice.Output,Is.SameAs(originalVoice));audio.Close(voice);runtime.Scheduler.CancelInvocation(animation,out _);
        }
        [UnityTest] public IEnumerator VisualLayersRejectStalePartialAndFailedPersistenceWithoutPartialMutation() {
            string id=NewVisualLayer(),target=AppearanceBlock();Assert.That(AssignVisualLayer(target,id,out var error),Is.True,error);
            Assert.That(editor.CopyObject(target,editor.ObjectRevision(target),"Layer sibling",Vector3.one,out var sibling,out error),Is.True,error);
            var changed=editor.ReadVisibility(id);changed.opacity=.25f;int rev=editor.VisibilityRevision(id);string before=JsonUtility.ToJson(editor.Snapshot());
            Assert.That(editor.EditVisibility(changed,id,rev,new[]{target},out _),Is.False);Assert.That(editor.EditVisibility(changed,id,rev-1,new[]{target,sibling},out _),Is.False);
            string obstacle=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(obstacle);
            try{Assert.That(editor.EditVisibility(changed,id,rev,new[]{target,sibling},out _),Is.False);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));Assert.That(AppearanceBlockMaterial().GetFloat("_VisibilityOpacity"),Is.EqualTo(.5f));}finally{Directory.Delete(obstacle);}
            Assert.That(editor.EditVisibility(changed,id,rev,new[]{target,sibling},out error),Is.True,error);Assert.That(editor.EditVisibility(null,id,editor.VisibilityRevision(id),Array.Empty<string>(),out _),Is.False);yield return null;
        }
        [UnityTest] public IEnumerator VisualLayerTemporaryAndPortableCreationsKeepSharingWithFreshDefinitions() {
            yield return WaitForModuleLibrary();string target=AppearanceBlock(),id=NewVisualLayer();Assert.That(AssignVisualLayer(target,id,out var error),Is.True,error);
            Assert.That(editor.CopyObject(target,editor.ObjectRevision(target),"Sibling",Vector3.one,out var sibling,out error),Is.True,error);
            Assert.That(editor.CaptureConstruction(new[]{target,sibling}.Select((t,i)=>new ConstructionMember{target=t,revision=editor.ObjectRevision(t),slot="piece"+i}).ToArray(),out var captured,out error),Is.True,error);
            var module=ConstructionModule.Definition(captured,"Visual pair");ProgramModuleLibrary.Validate(module);Assert.That(captured.blueprint.resources.version,Is.EqualTo(2));Assert.That(captured.blueprint.pieces.All(p=>p.source.prototype.version==3),Is.True);
            var args=(JObject)module["program"]["functions"][1]["body"][0]["arguments"];captured=CreationBatch.Read(args);
            Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);while(editor.TemporarySavePending)yield return null;
            Assert.That(editor.CreateBatch(captured,out var ids,out error),Is.True,error);string copy=editor.Read(ids[0]).visibilityLayer;
            Assert.That(copy,Is.Not.EqualTo(id));Assert.That(editor.Read(ids[1]).visibilityLayer,Is.EqualTo(copy));Assert.That(editor.ReadVisibility(copy).opacity,Is.EqualTo(.5f));
            editor.Undo();Assert.That(editor.ReadVisibility(copy),Is.Null);Assert.That(ids.All(t=>!editor.Find(t)),Is.True);editor.Redo();Assert.That(editor.ReadVisibility(copy),Is.Not.Null);
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.ReadVisibility(copy),Is.Null);Assert.That(editor.ReadVisibility(id),Is.Not.Null);
            Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);while(editor.TemporarySavePending)yield return null;
            Assert.That(editor.CreateBatch(captured,out ids,out error),Is.True,error);copy=editor.Read(ids[0]).visibilityLayer;
            Assert.That(editor.KeepTemporaryRoom(out error),Is.True,error);while(editor.TemporarySavePending)yield return null;
            var saved=new RoomStorage(directory).Load(out error);Assert.That(saved.visibilityLayers.Any(v=>v.id==copy),Is.True,error);
        }
        [UnityTest] public IEnumerator HiddenVisualsSkipPointingButRetainCollidersAndBookPages() {
            string id=NewVisualLayer(),target=AppearanceBlock();var layer=editor.ReadVisibility(id);layer.opacity=0;Assert.That(editor.EditVisibility(layer,id,editor.VisibilityRevision(id),Array.Empty<string>(),out var error),Is.True,error);Assert.That(AssignVisualLayer(target,id,out error),Is.True,error);
            block.transform.position=new Vector3(0,2,1);block.transform.localScale=Vector3.one;var colliders=block.GetComponentsInChildren<Collider>();Physics.SyncTransforms();
            Assert.That(block.PointerVisible,Is.False);Assert.That(colliders.Any(c=>c.enabled),Is.True);Assert.That(block.Process((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)null,block.Grab),Is.False);
            var ray=new Ray(new Vector3(0,2,0),Vector3.forward);Assert.That(Physics.Raycast(ray,out _,2,RoomPhysicsLayers.InteractionMask),Is.True);
            Assert.That(RoomPointerHit.Raycast(ray,out _,2,RoomPhysicsLayers.InteractionMask,QueryTriggerInteraction.Ignore),Is.False);
            var page=new GameObject("Visible book page");page.transform.SetParent(block.transform,false);page.transform.localPosition=Vector3.forward*.5f;page.AddComponent<BookPageTarget>();page.AddComponent<BoxCollider>();Physics.SyncTransforms();
            Assert.That(RoomPointerHit.Raycast(ray,out var hit,2,RoomPhysicsLayers.InteractionMask,QueryTriggerInteraction.Ignore),Is.True);Assert.That(hit.collider.gameObject,Is.EqualTo(page));yield return null;
        }
        [UnityTest] public IEnumerator ProceduralInkAddedAfterBindingInheritsLayerAndKeepsItsPigment() {
            string id=NewVisualLayer(),target=AppearanceBlock();Assert.That(AssignVisualLayer(target,id,out var error),Is.True,error);
            var ink=new GameObject("New ink");ink.transform.SetParent(block.transform,false);var marks=ink.AddComponent<PencilMarks>();marks.SetPaths(new[]{new[]{Vector3.zero,Vector3.right}},.003f);marks.SetColor(Color.cyan);
            yield return null;yield return null;var material=ink.GetComponent<Renderer>().sharedMaterial;Assert.That(material.GetFloat("_VisibilityOpacity"),Is.EqualTo(.5f));Assert.That(material.color,Is.EqualTo(Color.cyan));
            marks.SetColor(Color.magenta);yield return null;yield return null;Assert.That(ink.GetComponent<Renderer>().sharedMaterial.color,Is.EqualTo(Color.magenta));
        }
    }
}
