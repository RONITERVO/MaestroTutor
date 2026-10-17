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
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        JObject WindowCall(float reveal=1,string operation="save"){
            string id=editor.Identity(block);var args=new JObject{["operation"]=operation,["target"]=id,["revision"]=editor.ObjectRevision(id),["window"]="Window"};
            if(operation=="save"){args["surface"]="Front";args["shape"]="rectangle";args["reveal"]=reveal;}
            return new JObject{["id"]="object.window.edit",["version"]=1,["arguments"]=args};
        }
        [UnityTest]public IEnumerator WindowCommandPersistsReplaysAndUndoesWithoutChangingInkOrPhysics(){
            var ex=new RoomAgentExecutor(editor);SurfaceRun(ex,SurfaceConfigure());SurfaceRun(ex,SurfaceAdd());string id=editor.Identity(block);var original=editor.Read(id);var colliders=block.GetComponentsInChildren<Collider>();
            var request=ObjectEditRequest(WindowCall(.6f));Assert.That(ex.Execute(request,out var error,out _),Is.True,error);int revision=editor.ObjectRevision(id);Assert.That(ex.Execute(request,out error,out _),Is.True,error);Assert.That(editor.ObjectRevision(id),Is.EqualTo(revision));
            var view=block.GetComponent<RoomWindowView>();Assert.That(view,Is.Not.Null);Assert.That(view.Requested,Is.True);Assert.That(view.RenderingReady,Is.False,"No desktop passthrough is manufactured");Assert.That(block.GetComponentsInChildren<Collider>(),Is.EqualTo(colliders));
            Assert.That(editor.Read(id).physics,Is.EqualTo(original.physics));Assert.That(JsonUtility.ToJson(editor.Read(id).surfaces[0]),Is.EqualTo(JsonUtility.ToJson(original.surfaces[0])));
            var mask=block.GetComponentsInChildren<Renderer>().Single(RoomWindowView.IsMask);var position=mask.transform.position;block.transform.position+=Vector3.right;Assert.That(mask.transform.position-position,Is.EqualTo(Vector3.right));
            Assert.That(new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==id).windows[0].reveal,Is.EqualTo(.6f),error);
            SurfaceRun(ex,WindowCall(0));Assert.That(view.Requested,Is.False);editor.Undo();Assert.That(view.Requested,Is.True);Assert.That(editor.Read(id).windows[0].reveal,Is.EqualTo(.6f));editor.Redo();Assert.That(view.Requested,Is.False);
            SurfaceRun(ex,WindowCall(operation:"remove"));Assert.That(editor.Read(id).windows,Is.Empty);yield return null;Assert.That(block.GetComponentsInChildren<Renderer>().Any(RoomWindowView.IsMask),Is.False);
        }
        [UnityTest]public IEnumerator WindowRejectsStaleAndMissingPlaneEditsAndRetainsSourceOnSaveFailure(){
            var ex=new RoomAgentExecutor(editor);SurfaceRun(ex,SurfaceConfigure());var stale=ObjectEditRequest(WindowCall());SurfaceRun(ex,SurfaceAdd());Assert.That(ex.Execute(stale,out _,out _),Is.False);
            var invalid=WindowCall();invalid["arguments"]["surface"]="Missing";Assert.That(ex.Execute(ObjectEditRequest(invalid),out _,out _),Is.False);
            SurfaceRun(ex,WindowCall());Assert.That(ex.Execute(ObjectEditRequest(SurfaceCall("removeSurface")),out _,out _),Is.False,"Referenced plane cannot disappear silently");
            string obstacle=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(obstacle);Assert.That(ex.Execute(ObjectEditRequest(WindowCall(.25f)),out _,out _),Is.False);Assert.That(editor.Read(editor.Identity(block)).windows[0].reveal,Is.EqualTo(1));Directory.Delete(obstacle);yield return null;
        }
        [UnityTest]public IEnumerator WindowFactsRemainBoundedAndLayerFadesChangeOnlyEffectiveReveal(){
            var ex=new RoomAgentExecutor(editor);string id=editor.Identity(block);
            Assert.That(BehaviourCatalog.TryRead("object.windows",1,new JObject{["target"]=id},new BehaviourCatalog.FactContext(editor:editor),out var empty),Is.True,"A fresh object must expose a typed empty window list");Assert.That(((JArray)JObject.FromObject(empty.Value)["windows"]).Count,Is.Zero);
            SurfaceRun(ex,SurfaceConfigure());SurfaceRun(ex,WindowCall());
            Assert.That(BehaviourCatalog.TryRead("object.windows",1,new JObject{["target"]=id},new BehaviourCatalog.FactContext(editor:editor),out var fact),Is.True);Assert.That(fact.Characters,Is.LessThanOrEqualTo(1024));
            var view=block.GetComponent<RoomWindowView>();var state=new Maestro.Quest.Art.VisibilityState();view.ConfigureVisibility(state);state.Set(0,false);view.Refresh();Assert.That(view.Requested,Is.False);state.Set(.4f,false);view.Refresh();Assert.That(view.Requested,Is.True);Assert.That(editor.Read(id).windows[0].reveal,Is.EqualTo(1));
            var renderer=block.GetComponentsInChildren<Renderer>().Single(RoomWindowView.IsMask);var props=new MaterialPropertyBlock();renderer.GetPropertyBlock(props);Assert.That(props.GetFloat("_Reveal"),Is.EqualTo(.4f));SurfaceRun(ex,WindowCall(operation:"remove"));
            Assert.That(BehaviourCatalog.TryRead("object.windows",1,new JObject{["target"]=id},new BehaviourCatalog.FactContext(editor:editor),out empty),Is.True);Assert.That(((JArray)JObject.FromObject(empty.Value)["windows"]).Count,Is.Zero);yield return null;
        }
    }
}
