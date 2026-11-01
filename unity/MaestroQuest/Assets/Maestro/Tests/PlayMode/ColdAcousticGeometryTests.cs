// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        static List<Vector3[]> AcousticTriangles(IEnumerable<AcousticMeshInput> meshes)
        {
            var result=new List<Vector3[]>();foreach(var mesh in meshes)for(int i=0;i<mesh.Indices.Length;i+=3){
                var triangle=new Vector3[3];for(int j=0;j<3;j++){int n=mesh.Indices[i+j]*3;triangle[j]=mesh.Local.MultiplyPoint3x4(new Vector3(mesh.Vertices[n],mesh.Vertices[n+1],mesh.Vertices[n+2]));}result.Add(triangle);
            }return result;
        }
        IEnumerator CompareColdAcousticGeometry(string id)
        {
            var item=editor.Find(id);Assert.That(item,Is.Not.Null);var data=editor.Read(id);var saved=JsonUtility.ToJson(editor.Snapshot());
            var budget=ImportedModel.LiveBudget;
            var preparation=RoomAcousticGeometry.Prepare(data,item.GetComponent<RecipeObject>()?.CaptureIdleState(),editor.Models,new RoomPreparationBudget(()=>{}),CancellationToken.None);
            while(!preparation.IsCompleted)yield return null;Assert.That(preparation.IsFaulted,Is.False,preparation.Exception?.ToString());
            var actual=new List<AcousticMeshInput>();foreach(var surface in item.GetComponentsInChildren<AcousticSurface>()){
                Assert.That(RoomAcoustics.ReadMesh(surface.Mesh,out var vertices,out var indices,out var issue),Is.True,issue);
                actual.Add(new AcousticMeshInput(vertices,indices,item.transform.worldToLocalMatrix*surface.transform.localToWorldMatrix));
            }
            var expected=AcousticTriangles(actual);var prepared=AcousticTriangles(preparation.Result);Assert.That(prepared.Count,Is.EqualTo(expected.Count));
            foreach(var triangle in expected){
                int match=prepared.FindIndex(candidate=>triangle.All(point=>candidate.Any(other=>Vector3.Distance(point,other)<.00002f)));
                Assert.That(match,Is.GreaterThanOrEqualTo(0),"Prepared acoustic triangles must match the real rendered geometry");prepared.RemoveAt(match);
            }
            Assert.That(ImportedModel.LiveBudget,Is.EqualTo(budget));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
        }
        [UnityTest] public IEnumerator ColdAcousticGeometryMatchesEnginePrimitives()
        {
            foreach(var kind in new[]{RoomObjectKind.Block,RoomObjectKind.Ball,RoomObjectKind.Cylinder}){
                var before=editor.Snapshot().objects.Select(o=>o.id).ToHashSet();editor.Create(kind);yield return PhysicalCreationTestWait.Finish(editor);var id=editor.Snapshot().objects.Single(o=>!before.Contains(o.id)).id;
                yield return CompareColdAcousticGeometry(id);
            }
        }
        [UnityTest] public IEnumerator ColdAcousticGeometryMatchesNestedRecipeParts()
        {
            var recipe=new RoomRecipe{parts=new[]{new RecipePart{id="root",shape="box",size=new Vector3(.4f,.2f,.3f),rotation=Quaternion.Euler(11,20,3)},new RecipePart{id="arm",parent="root",shape="cylinder",position=new Vector3(.2f,.3f,0),size=new Vector3(.08f,.5f,.08f),rotation=Quaternion.Euler(0,0,40)}}};
            Assert.That(editor.CreateRecipe("Acoustic recipe",Vector3.one,1,recipe,out var id,out var issue),Is.True,issue);yield return CompareColdAcousticGeometry(id);
        }
        [UnityTest] public IEnumerator ColdAcousticGeometryMatchesCustomRecipesAndStoppedPoses()
        {
            foreach(var recipe in new[]{ExtrusionBracket(),LatheCup(),SweepHandle()}){
                Assert.That(editor.CreateRecipe("Acoustic custom shape",Vector3.one,1,recipe,out var id,out var error),Is.True,error);
                var part=editor.Find(id).GetComponent<RecipeObject>().Part(recipe.parts[0].id);
                part.localPosition+=new Vector3(.1f,.2f,0);part.localRotation=Quaternion.Euler(13,21,7);part.localScale=new Vector3(.8f,1.2f,.9f);
                yield return CompareColdAcousticGeometry(id);
            }
        }
        [UnityTest] public IEnumerator ColdAcousticGeometryMatchesAcceptedTerrain()
        {
            string id=editor.Identity(block);
            var field=new RoomHeightField{cells=8,width=4,depth=4,maxHeight=.5f,heights=Enumerable.Range(0,81).Select(i=>.2f+.1f*Mathf.Sin(i)).ToArray()};
            field.frame.position=new Vector3(.1f,.2f,.3f);field.frame.rotation=Quaternion.Euler(7,18,0);
            Assert.That(editor.EditHeightField(id,editor.ObjectRevision(id),field,out var error),Is.True,error);
            yield return CompareColdAcousticGeometry(id);
        }
        [Test] public void ColdAcousticUnsupportedModelMappingIsUnknownInsteadOfEmpty()
        {
            foreach(var bytes in new[]{StaticModelFixture.Hierarchy(),StaticModelFixture.Create(json=>{
                json["nodes"][0]["matrix"]=new JArray(1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1);
            })}){
                var result=ModelInspection.InspectAcoustics(bytes).Acoustics;
                Assert.That(result.Known,Is.False);Assert.That(result.Issue,Is.Not.Empty);Assert.That(result.Meshes,Is.Empty);
            }
        }
        [UnityTest] public IEnumerator ColdAcousticGeometryMatchesRigidModelFittingWithoutAnotherImport()
        {
            foreach(var source in new[]{ModelFixture.Create(),StaticModelFixture.Create(),StaticModelFixture.Create(json=>{
                json["nodes"]=new JArray(new JObject{["translation"]=new JArray(1,2,-1),["children"]=new JArray(1)},new JObject{["mesh"]=0,["scale"]=new JArray(.7,1.2,.8)},new JObject{["mesh"]=0,["translation"]=new JArray(200,200,200)});
            })}){
                var asset=ModelLibrary.Inspect("acoustic model",source);var save=editor.Models.SaveAsync(asset);while(!save.IsCompleted)yield return null;Assert.That(save.IsFaulted,Is.False,save.Exception?.ToString());var create=editor.CreateImportedModelAsync(asset.Hash,CancellationToken.None);while(!create.IsCompleted)yield return null;Assert.That(create.IsFaulted,Is.False,create.Exception?.ToString());
                yield return CompareColdAcousticGeometry(create.Result);
            }
        }
        [UnityTest] public IEnumerator ColdAcousticGeometryExcludesCutoutsAndSkinsLikeNativeImport()
        {
            foreach(var source in new[]{StaticModelFixture.Create(json=>{json["materials"]=new JArray(new JObject{["alphaMode"]="MASK",["alphaCutoff"]=.5});json["meshes"][0]["primitives"][0]["material"]=0;}),RestModelFixture.Skin(),RestModelFixture.Morph()}){
                var asset=ModelLibrary.Inspect("non-rigid acoustic exclusion",source);var save=editor.Models.SaveAsync(asset);while(!save.IsCompleted)yield return null;Assert.That(save.IsFaulted,Is.False,save.Exception?.ToString());var create=editor.CreateImportedModelAsync(asset.Hash,CancellationToken.None);while(!create.IsCompleted)yield return null;Assert.That(create.IsFaulted,Is.False,create.Exception?.ToString());yield return CompareColdAcousticGeometry(create.Result);
            }
        }
    }
}
