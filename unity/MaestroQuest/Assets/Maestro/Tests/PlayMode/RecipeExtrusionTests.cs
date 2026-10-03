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
        RoomRecipe ExtrusionBracket()=>JsonUtility.FromJson<RoomRecipe>(JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/extrusion-contract.json")))[0]["recipe"].ToString());
        [UnityTest] public IEnumerator ExtrusionSharedCreationEditingQuerySaveUndoAndMeshDisposal(){
            var recipe=ExtrusionBracket();var ex=new RoomAgentExecutor(editor);var create=TemplateRequest(new JObject{["id"]="object.create",["version"]=1,["arguments"]=new JObject{["kind"]="recipe",["name"]="Editable bracket",["x"]=.2,["y"]=1,["z"]=.4,["scale"]=1,["recipe"]=JObject.Parse(JsonUtility.ToJson(recipe))}});Assert.That(ex.Execute(create,out var createError,out _),Is.True,createError);var created=(JObject)ex.Executions.Observe().DeepClone();string id=(string)created["selected"]["output"]["objectId"];Assert.That(editor.Find(id),Is.Not.Null);
            var geometry=editor.Find(id).GetComponent<RecipeObject>();var first=geometry.Part("Outline").GetComponentInChildren<MeshFilter>().sharedMesh;
            var call=RecipePatch(id);recipe.parts[0].profile[3].x=0;call["arguments"]["parts"]=new JArray(JObject.Parse(JsonUtility.ToJson(recipe.parts[0])));RecipeRun(ex,call);yield return null;Assert.That(first==null,Is.True);
            var current=geometry.Part("Outline").GetComponentInChildren<MeshFilter>().sharedMesh;var args=new JObject{["target"]=id,["revision"]=editor.ObjectRevision(id),["part"]="Outline",["offset"]=0};Assert.That(BehaviourCatalog.TryRead("object.recipe.profile",1,args,new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True);var profile=(JObject)value.Value;Assert.That((int)profile["segments"],Is.Zero);Assert.That((float)profile["points"][3]["x"],Is.Zero);Assert.That(value.Characters,Is.LessThanOrEqualTo(1024));
            CaptureExtrusionEvidence(geometry);
            Assert.That(new RoomStorage(directory).Load(out _).objects.Single(o=>o.id==id).recipe.parts[0].profile[3].x,Is.Zero);editor.Undo();yield return null;Assert.That(current==null,Is.True);Assert.That(editor.Read(id).recipe.parts[0].profile[3].x,Is.EqualTo(-.1f));Assert.That(BehaviourCatalog.TryRead("object.recipe.profile",1,args,new BehaviourCatalog.FactContext(editor:editor),out _),Is.False);editor.Undo();yield return null;Assert.That(editor.Find(id),Is.Null);
        }
        [UnityTest] public IEnumerator ExtrusionFailedPublicationKeepsAcceptedGeometryAndTemporaryDiscardRestoresIt(){
            var recipe=ExtrusionBracket();Assert.That(editor.CreateRecipe("Bracket",new Vector3(.2f,1,.4f),1,recipe,out var id,out var error),Is.True,error);var item=editor.Find(id);var mesh=item.GetComponent<RecipeObject>().Part("Outline").GetComponentInChildren<MeshFilter>().sharedMesh;var patch=RecipePatch(id);recipe.parts[0].profile[3].x=0;patch["arguments"]["parts"]=new JArray(JObject.Parse(JsonUtility.ToJson(recipe.parts[0])));string pending=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(pending);
            try{Assert.That(editor.EditRecipe(id,editor.ObjectRevision(id),(JObject)patch["arguments"],out error),Is.False);Assert.That(item.GetComponent<RecipeObject>().Part("Outline").GetComponentInChildren<MeshFilter>().sharedMesh,Is.SameAs(mesh));Assert.That(editor.Read(id).recipe.parts[0].profile[3].x,Is.EqualTo(-.1f));}finally{Directory.Delete(pending);}
            Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);while(editor.TemporarySavePending)yield return null;patch["arguments"]["revision"]=editor.ObjectRevision(id);RecipeRun(new RoomAgentExecutor(editor),patch);Assert.That(editor.Read(id).recipe.parts[0].profile[3].x,Is.Zero);Assert.That(new RoomStorage(directory).Load(out _).objects.Single(o=>o.id==id).recipe.parts[0].profile[3].x,Is.EqualTo(-.1f));Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.Read(id).recipe.parts[0].profile[3].x,Is.EqualTo(-.1f));
        }
        void CaptureExtrusionEvidence(RecipeObject geometry){
            string directory=Environment.GetEnvironmentVariable("MAESTRO_EXTRUSION_EVIDENCE");if(string.IsNullOrEmpty(directory))return;Directory.CreateDirectory(directory);var go=new GameObject("Extrusion evidence",typeof(Camera));var camera=go.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.9f,.92f,.93f);camera.nearClipPlane=.01f;camera.farClipPlane=5;camera.fieldOfView=38;var target=geometry.transform.position;go.transform.position=target+new Vector3(.8f,.4f,-1.6f);go.transform.LookAt(target);var render=new RenderTexture(1000,900,24);var pixels=new Texture2D(1000,900,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try{camera.targetTexture=render;camera.Render();RenderTexture.active=render;pixels.ReadPixels(new Rect(0,0,1000,900),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(directory,"extruded-bracket.png"),pixels.EncodeToPNG());Assert.That(pixels.GetPixels32().Count(p=>p.b>p.r+30&&p.g>p.r+20),Is.GreaterThan(1000));}finally{RenderTexture.active=previous;camera.targetTexture=null;render.Release();UnityEngine.Object.Destroy(render);UnityEngine.Object.Destroy(pixels);UnityEngine.Object.Destroy(go);}
        }
    }
}
