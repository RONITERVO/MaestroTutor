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
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        static RoomRecipe SweepHandle()=>JsonUtility.FromJson<RoomRecipe>(JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/sweep-contract.json")))[0]["recipe"].ToString());
        [UnityTest] public IEnumerator SweepSharedCreationEditingQueriesSaveUndoAndMeshDisposal(){
            var recipe=SweepHandle();var ex=new RoomAgentExecutor(editor);var create=TemplateRequest(new JObject{["id"]="object.create",["version"]=1,["arguments"]=new JObject{["kind"]="recipe",["name"]="Editable handle",["x"]=4,["y"]=1,["z"]=2,["scale"]=1,["recipe"]=JObject.Parse(JsonUtility.ToJson(recipe))}});Assert.That(ex.Execute(create,out var createError,out _),Is.True,createError);string id=(string)ex.Executions.Observe()["selected"]["output"]["objectId"];Assert.That(editor.Find(id),Is.Not.Null);
            var geometry=editor.Find(id).GetComponent<RecipeObject>();var first=geometry.Part("Handle").GetComponentInChildren<MeshFilter>().sharedMesh;
            var call=RecipePatch(id);recipe.parts[0].path[2].z=.05f;call["arguments"]["parts"]=new JArray(JObject.Parse(JsonUtility.ToJson(recipe.parts[0])));RecipeRun(ex,call);yield return null;Assert.That(first==null,Is.True);
            var current=geometry.Part("Handle").GetComponentInChildren<MeshFilter>().sharedMesh;var args=new JObject{["target"]=id,["revision"]=editor.ObjectRevision(id),["part"]="Handle",["offset"]=0};var context=new BehaviourCatalog.FactContext(editor:editor);
            Assert.That(BehaviourCatalog.TryRead("object.recipe.path",1,args,context,out var value),Is.True);Assert.That((float)((JObject)value.Value)["points"][2]["z"],Is.EqualTo(.05f));Assert.That(value.Characters,Is.LessThanOrEqualTo(1024));
            Assert.That(BehaviourCatalog.TryRead("object.recipe.profile",1,args,context,out value),Is.True);Assert.That(((JArray)((JObject)value.Value)["points"]).Count,Is.EqualTo(4));
            var later=(JObject)args.DeepClone();later["offset"]=6;Assert.That(BehaviourCatalog.TryRead("object.recipe.path",1,later,context,out value),Is.True);Assert.That(((JArray)((JObject)value.Value)["points"]).Count,Is.Zero);later["offset"]=7;Assert.That(BehaviourCatalog.TryRead("object.recipe.path",1,later,context,out _),Is.False);
            later["offset"]=8;Assert.That(BehaviourCatalog.TryRead("object.recipe.profile",1,later,context,out value),Is.True);Assert.That(((JArray)((JObject)value.Value)["points"]).Count,Is.Zero);
            CaptureSweepEvidence(geometry);
            Assert.That(new RoomStorage(directory).Load(out _).objects.Single(o=>o.id==id).recipe.parts[0].path[2].z,Is.EqualTo(.05f));editor.Undo();yield return null;Assert.That(current==null,Is.True);Assert.That(editor.Read(id).recipe.parts[0].path[2].z,Is.Zero);Assert.That(BehaviourCatalog.TryRead("object.recipe.path",1,args,context,out _),Is.False);editor.Undo();yield return null;Assert.That(editor.Find(id),Is.Null);
        }
        [UnityTest] public IEnumerator SweepFailedPublicationPreservesMeshAndTemporaryDiscardRestoresSource(){
            var recipe=SweepHandle();Assert.That(editor.CreateRecipe("Handle",new Vector3(4,1,2),1,recipe,out var id,out var error),Is.True,error);var item=editor.Find(id);var mesh=item.GetComponent<RecipeObject>().Part("Handle").GetComponentInChildren<MeshFilter>().sharedMesh;var patch=RecipePatch(id);recipe.parts[0].path[2].z=.05f;patch["arguments"]["parts"]=new JArray(JObject.Parse(JsonUtility.ToJson(recipe.parts[0])));string pending=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(pending);
            try{Assert.That(editor.EditRecipe(id,editor.ObjectRevision(id),(JObject)patch["arguments"],out error),Is.False);Assert.That(item.GetComponent<RecipeObject>().Part("Handle").GetComponentInChildren<MeshFilter>().sharedMesh,Is.SameAs(mesh));Assert.That(editor.Read(id).recipe.parts[0].path[2].z,Is.Zero);}finally{Directory.Delete(pending);}
            Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);while(editor.TemporarySavePending)yield return null;patch["arguments"]["revision"]=editor.ObjectRevision(id);RecipeRun(new RoomAgentExecutor(editor),patch);Assert.That(editor.Read(id).recipe.parts[0].path[2].z,Is.EqualTo(.05f));Assert.That(new RoomStorage(directory).Load(out _).objects.Single(o=>o.id==id).recipe.parts[0].path[2].z,Is.Zero);Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.Read(id).recipe.parts[0].path[2].z,Is.Zero);
        }
        void CaptureSweepEvidence(RecipeObject geometry){
            string directory=Environment.GetEnvironmentVariable("MAESTRO_SWEEP_EVIDENCE");if(string.IsNullOrEmpty(directory))return;Directory.CreateDirectory(directory);var go=new GameObject("Sweep evidence",typeof(Camera));var camera=go.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.9f,.92f,.93f);camera.nearClipPlane=.01f;camera.farClipPlane=5;camera.fieldOfView=38;var target=geometry.transform.position;go.transform.position=target+new Vector3(.38f,.28f,-.7f);go.transform.LookAt(target);var render=new RenderTexture(1000,900,24);var pixels=new Texture2D(1000,900,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try{camera.targetTexture=render;camera.Render();RenderTexture.active=render;pixels.ReadPixels(new Rect(0,0,1000,900),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(directory,"swept-handle.png"),pixels.EncodeToPNG());Assert.That(pixels.GetPixels32().Count(p=>p.b>p.r+30&&p.b>p.g+30),Is.GreaterThan(1000));}finally{RenderTexture.active=previous;camera.targetTexture=null;render.Release();UnityEngine.Object.Destroy(render);UnityEngine.Object.Destroy(pixels);UnityEngine.Object.Destroy(go);}
        }
    }
}
