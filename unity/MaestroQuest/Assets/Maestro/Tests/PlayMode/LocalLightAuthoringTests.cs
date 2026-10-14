// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        JObject LampArguments(string target,RoomLight lamp){var settings=JObject.Parse(JsonUtility.ToJson(lamp));settings.Remove("id");return new(){["operation"]="save",["target"]=target,["revision"]=editor.ObjectRevision(target),["light"]=lamp.id,["settings"]=settings};}
        [UnityTest]public IEnumerator LocalLampUsesSharedActionsAndPersistsWithUndoAndTemporaryDiscard(){
            string target=editor.Identity(block);var args=LampArguments(target,new RoomLight{kind="spot",shadows="hard"});
            Assert.That(RoomControls.Capabilities(editor),Does.Contain(ObjectLightCapability.Feature));
            var call=new JObject{["id"]="object.light.edit",["version"]=1,["arguments"]=args};var request=ObjectEditRequest(call);var executor=new RoomAgentExecutor(editor);
            Assert.That(executor.Execute(request,out var error,out _),Is.True,error);int revision=editor.ObjectRevision(target);yield return null;
            var view=block.GetComponent<RoomLightView>();Assert.That(view.Active("Lamp"),Is.True);Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(editor.ObjectRevision(target),Is.EqualTo(revision));
            Assert.That(new RoomStorage(directory).Load(out error).objects.Single(o=>o.id==target).lights.Length,Is.EqualTo(1),error);
            Assert.That(ObjectLightCapability.Fact().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,new JObject{["target"]=target},out _),Is.True);
            Assert.That(editor.EditLight(target,revision-1,args,out _),Is.False);editor.Undo();Assert.That(view.Active("Lamp"),Is.False);editor.Redo();yield return null;Assert.That(view.Active("Lamp"),Is.True);
            Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);while(editor.TemporarySavePending)yield return null;
            var off=LampArguments(target,new RoomLight{kind="spot",shadows="hard",enabled=false});Assert.That(editor.EditLight(target,editor.ObjectRevision(target),off,out error),Is.True,error);Assert.That(view.Active("Lamp"),Is.False);
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);yield return null;Assert.That(view.Active("Lamp"),Is.True);
        }
        [UnityTest]public IEnumerator LocalLampFollowsRecipePartsAndReleasesOnTrackingAndDeletion(){
            string target=RecipeTarget();var data=editor.Read(target);string part=data.recipe.parts[0].id;var args=LampArguments(target,new RoomLight{part=part,position=Vector3.up*.1f,kind="spot",shadows="hard"});
            Assert.That(editor.EditLight(target,editor.ObjectRevision(target),args,out var error),Is.True,error);var item=editor.Find(target);var view=item.GetComponent<RoomLightView>();yield return null;
            var lamp=item.GetComponentInChildren<Light>();var anchor=item.GetComponent<RecipeObject>().Part(part);Assert.That(Vector3.Distance(lamp.transform.position,anchor.TransformPoint(Vector3.up*.1f)),Is.LessThan(.001));
            anchor.localRotation=Quaternion.Euler(20,30,40);view.Refresh();Assert.That(Vector3.Distance(lamp.transform.position,anchor.TransformPoint(Vector3.up*.1f)),Is.LessThan(.001));
            root.transform.localScale=new Vector3(1,2,1);view.Refresh();Assert.That(view.Active("Lamp"),Is.False);root.transform.localScale=Vector3.one;view.Refresh();Assert.That(view.Active("Lamp"),Is.True);
            Assert.That(editor.DeleteObject(target,out error),Is.True,error);yield return null;Assert.That(lamp==null||!lamp.isActiveAndEnabled,Is.True);Assert.That(Shader.GetGlobalFloat("_MaestroLocalLightingEnabled"),Is.Zero);
        }
        JObject LampControl(string target,string operation="set",bool enabled=false)=>new(){["operation"]=operation,["target"]=target,["light"]="Lamp",["stateId"]=editor.ObserveLight(target,"Lamp")["stateId"], ["enabled"]=enabled,["intensity"]=.4,["color"]="#2244FF"};
        [UnityTest]public IEnumerator LocalLampControlsUseSharedSchedulerWhileHeldWithoutSavingOrReplaying(){
            string target=editor.Identity(block);Assert.That(editor.EditLight(target,editor.ObjectRevision(target),LampArguments(target,new RoomLight()),out var error),Is.True,error);
            Assert.That(editor.TryFlush(out error),Is.True,error);var file=Path.Combine(directory,RoomStorage.FileName);var bytes=File.ReadAllBytes(file);int revision=editor.ObjectRevision(target);
            var hand=Hand(1,block.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,block.Grab);Assert.That(block.Grab.isSelected,Is.True);
            var args=LampControl(target);var request=ObjectEditRequest(new JObject{["id"]="object.light.control",["version"]=1,["arguments"]=args});var executor=new RoomAgentExecutor(editor);
            using(var locked=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read)){
                Assert.That(executor.Execute(request,out error,out _),Is.True,error);var state=editor.ObserveLight(target,"Lamp");Assert.That((bool)state["active"],Is.False);Assert.That((bool)state["effective"]["overridden"],Is.True);
                Assert.That(ObjectLightControlCapability.Fact().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,new JObject{["target"]=target,["light"]="Lamp"},out _),Is.True);
                Assert.That(editor.ControlLight(target,"Lamp",(string)args["stateId"],args,out _),Is.False);
                Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That((string)editor.ObserveLight(target,"Lamp")["stateId"],Is.EqualTo((string)state["stateId"]));
                Assert.That(editor.ObjectRevision(target),Is.EqualTo(revision));Assert.That(File.ReadAllBytes(file),Is.EqualTo(bytes));Assert.That(editor.Read(target).lights[0].enabled,Is.True);
            }
            manager.SelectExit((IXRSelectInteractor)hand,block.Grab);yield return null;
        }
        [UnityTest]public IEnumerator LocalLampRuntimeSurvivesRendererRecreationAndResetsAtExplicitBoundaries(){
            string target=editor.Identity(block);Assert.That(editor.EditLight(target,editor.ObjectRevision(target),LampArguments(target,new RoomLight()),out var error),Is.True,error);
            void Off(){var args=LampControl(target);Assert.That(editor.ControlLight(target,"Lamp",(string)args["stateId"],args,out var e),Is.True,e);}
            Off();var stateId=(string)editor.ObserveLight(target,"Lamp")["stateId"];var view=block.GetComponent<RoomLightView>();UnityEngine.Object.DestroyImmediate(view);view=block.gameObject.AddComponent<RoomLightView>();view.Apply(editor,target,editor.Read(target).lights);Assert.That(view.Active("Lamp"),Is.False);Assert.That((string)editor.ObserveLight(target,"Lamp")["stateId"],Is.EqualTo(stateId));
            Assert.That(CreationPrototype.Capture(editor.Read(target)).lights[0].enabled,Is.True,"Copies retain authored defaults");
            var reset=LampControl(target,"reset");reset.Remove("enabled");reset.Remove("intensity");reset.Remove("color");Assert.That(editor.ControlLight(target,"Lamp",(string)reset["stateId"],reset,out error),Is.True,error);Assert.That(view.Active("Lamp"),Is.True);
            Off();var changed=new RoomLight{intensity=2};Assert.That(editor.EditLight(target,editor.ObjectRevision(target),LampArguments(target,changed),out error),Is.True,error);Assert.That(view.Active("Lamp"),Is.True);Assert.That((bool)editor.ObserveLight(target,"Lamp")["effective"]["overridden"],Is.False);
            Off();Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);while(editor.TemporarySavePending)yield return null;view.Refresh();Assert.That(view.Active("Lamp"),Is.True);
            Off();Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);yield return null;Assert.That(view.Active("Lamp"),Is.True);
            Off();editor.enabled=false;editor.enabled=true;view.Refresh();Assert.That(view.Active("Lamp"),Is.True);Assert.That((bool)editor.ObserveLight(target,"Lamp")["effective"]["overridden"],Is.False);
        }
        [UnityTest]public IEnumerator FailedLocalLampSaveDoesNotPublishAnyNativeLight(){
            string target=editor.Identity(block);Assert.That(editor.TryFlush(out var error),Is.True,error);int revision=editor.ObjectRevision(target);
            using(var locked=new FileStream(Path.Combine(directory,RoomStorage.FileName),FileMode.Open,FileAccess.Read,FileShare.Read)){
                Assert.That(editor.EditLight(target,revision,LampArguments(target,new RoomLight()),out _),Is.False);Assert.That(editor.ObjectRevision(target),Is.EqualTo(revision));Assert.That(editor.Read(target).lights,Is.Empty);Assert.That(block.GetComponentsInChildren<Light>(),Is.Empty);
            }yield return null;
        }
    }
}
