// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Diagnostics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        static RoomRecipe ResourceParts(int count)=>new(){parts=Enumerable.Range(0,count).Select(i=>new RecipePart{id="part"+i}).ToArray()};
        List<RecipeVisual> FillProceduralPreparation()
        {
            var values=new List<RecipeVisual>();
            try{
                int remaining=RecipeResources.MaximumParts-(int)RecipeResources.ObserveBudget()["parts"];
                while(remaining>0){int n=Math.Min(32,remaining);values.Add(RecipeVisual.BeginPreparation(ResourceParts(n),new RoomResourceOwner(editor.WorldIdentity,Guid.NewGuid().ToString("N"),"preview")));remaining-=n;}
                return values;
            }catch{foreach(var value in values)value.Dispose();throw;}
        }
        [UnityTest] public IEnumerator ProceduralBudgetFailurePreservesSavedEditPlaybackAndUndoThenRetriesAfterNativeRetirement()
        {
            string id=RecipeTarget(true);yield return null;Assert.That(editor.TryFlush(out var error),Is.True,error);
            var item=editor.Find(id);var view=item.GetComponent<RecipeObject>();var old=view.Part("Head");int revision=editor.Revision;bool undo=editor.CanUndo,redo=editor.CanRedo;
            string saved=JsonUtility.ToJson(editor.Snapshot());var bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));
            var call=RecipePatch(id);var head=editor.Read(id).recipe.parts.Single(p=>p.id=="Head");head.size.x+=.01f;call["arguments"]["parts"]=new JArray(JObject.Parse(JsonUtility.ToJson(head)));
            var pending=FillProceduralPreparation();
            try{
                Assert.That(editor.EditRecipe(id,editor.ObjectRevision(id),(JObject)call["arguments"],out error),Is.False);StringAssert.Contains("live procedural geometry budget",error);
                Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(editor.CanUndo,Is.EqualTo(undo));Assert.That(editor.CanRedo,Is.EqualTo(redo));
                Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));
                Assert.That(view.Part("Head"),Is.SameAs(old));Assert.That(view.IsPlaying,Is.True);
            }finally{foreach(var value in pending)value.Dispose();}
            Assert.That((int)RecipeResources.ObserveBudget()["retiring"],Is.GreaterThan(0));
            Assert.That(editor.EditRecipe(id,editor.ObjectRevision(id),(JObject)call["arguments"],out error),Is.False,"Destroy has not released the old preparation yet");
            yield return null;
            Assert.That(editor.EditRecipe(id,editor.ObjectRevision(id),(JObject)call["arguments"],out error),Is.True,error);Assert.That(view.Part("Head"),Is.Not.SameAs(old));
            editor.Undo();Assert.That(editor.Read(id).recipe.parts.Single(p=>p.id=="Head").size.x,Is.EqualTo(head.size.x-.01f).Within(.00001f));
            yield return null;editor.Redo();Assert.That(editor.Read(id).recipe.parts.Single(p=>p.id=="Head").size.x,Is.EqualTo(head.size.x));
        }
        [UnityTest] public IEnumerator ProceduralBudgetFactsPreserveExactOwnershipUntilRootAndCustomMeshesAreDestroyed()
        {
            root.AddComponent<RuntimeDiagnostics>();var world=editor.WorldIdentity.Copy();string original=world.worldId;
            var owner=new RoomResourceOwner(world,"resource-probe","object");world.worldId=new string('e',32);
            var visual=RecipeVisual.BeginPreparation(LatheCup(),owner);
            JObject FindOwner()=>Enumerable.Range(0,(int)CollisionFact("runtime.proceduralBudget")["instances"]).Select(i=>CollisionFact("runtime.proceduralReservation",new JObject{["index"]=i})).SingleOrDefault(v=>(string)v["target"]=="resource-probe");
            JObject preparing=null,ready=null,active=null,retiring=null,budget=null;
            GameObject capturedRoot=null;Mesh[] meshes=null;
            try{
                preparing=FindOwner();Assert.That((string)preparing["phase"],Is.EqualTo("preparing"));Assert.That((string)preparing["worldId"],Is.EqualTo(original));
                Assert.That((bool)preparing["active"],Is.False);Assert.That((int)preparing["customVertices"],Is.EqualTo(RecipeGeometry.VertexCost(LatheCup())));
                foreach(var step in visual.BuildSteps()){}ready=FindOwner();Assert.That((string)ready["phase"],Is.EqualTo("ready"));Assert.That((bool)ready["active"],Is.False);
                visual.Attach(root.transform);active=FindOwner();Assert.That((bool)active["active"],Is.True);budget=CollisionFact("runtime.proceduralBudget");
                visual.SetActive(false);Assert.That((string)FindOwner()["reservationId"],Is.EqualTo((string)active["reservationId"]));
                capturedRoot=visual.Root;meshes=visual.Meshes.ToArray();
            }finally{visual.Dispose();}
            retiring=FindOwner();Assert.That((string)retiring["phase"],Is.EqualTo("retiring"));Assert.That(capturedRoot,Is.Not.Null);Assert.That(meshes.All(m=>m),Is.True);
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_PROCEDURAL_RESOURCE_EVIDENCE");
            if(!string.IsNullOrEmpty(evidence))File.WriteAllText(evidence,new JObject{["budget"]=budget,["preparing"]=preparing,["ready"]=ready,["active"]=active,["retiring"]=retiring}.ToString());
            yield return null;Assert.That(FindOwner(),Is.Null);Assert.That(!capturedRoot,Is.True);Assert.That(meshes.All(m=>!m),Is.True);
        }
        [UnityTest] public IEnumerator ProceduralDormantActivationCanFailAtBudgetAndRetryWithoutLosingSavedObject()
        {
            string id=RecipeTarget();editor.Select(null);string area=NativeAreaFor(id);yield return null;
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);yield return null;
            string saved=JsonUtility.ToJson(editor.Read(id));int revision=editor.Revision;var pending=FillProceduralPreparation();
            try{
                var failed=editor.ActivateNativeArea(area);yield return new WaitUntil(()=>failed.IsCompleted);Assert.That(failed.Exception,Is.Null);StringAssert.Contains("live procedural geometry budget",failed.Result);
                Assert.That(editor.Find(id),Is.Null);Assert.That(editor.NativeEntityDormant(id),Is.True);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Read(id)),Is.EqualTo(saved));
            }finally{foreach(var value in pending)value.Dispose();}
            yield return null;var retry=editor.ActivateNativeArea(area);yield return new WaitUntil(()=>retry.IsCompleted);Assert.That(retry.Exception,Is.Null);Assert.That(retry.Result,Is.Null);
            Assert.That(editor.Find(id),Is.Not.Null);Assert.That(editor.Read(id),Is.Not.Null);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Read(id)),Is.EqualTo(saved));
        }
    }
}
