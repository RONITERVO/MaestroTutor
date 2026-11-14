// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Imports;
using Newtonsoft.Json.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed class AppearanceBindingTests
    {
        readonly List<Object> owned=new();
        T Own<T>(T o)where T:Object{owned.Add(o);return o;}
        [TearDown]public void Cleanup(){for(int i=owned.Count-1;i>=0;i--)if(owned[i])Object.DestroyImmediate(owned[i]);owned.Clear();}
        [UnityTest]public IEnumerator SharedLeaseIsImmutableAndTracksSourceChangesWithoutLosingOpacity() {
            int before=AppearanceMaterials.LiveVariants;var source=Own(IllustratedMaterials.Create(Color.white));
            var style=new AppearanceStyle{tint="#FF0000",renderMode="blend",opacity=.5f};
            var a=AppearanceMaterials.Acquire(source,style);var b=AppearanceMaterials.Acquire(source,style.Copy());
            try {
                Assert.That(a.Material,Is.SameAs(b.Material));Assert.That(a.Material.GetFloat("_SurfaceOpacity"),Is.EqualTo(.5f));
                source.color=Color.blue;using var changed=AppearanceMaterials.Acquire(source,style);
                Assert.That(changed.Material,Is.Not.SameAs(a.Material));Assert.That(a.Material.color,Is.EqualTo(Color.red));
            }finally{a.Dispose();b.Dispose();}
            yield return null;Assert.That(AppearanceMaterials.LiveVariants,Is.EqualTo(before));
        }
        [UnityTest]public IEnumerator ImportedSlotBindingsUseExactAssetIdentityAndSurviveRefresh() {
            var bytes=ModelFixture.Create(json=>{
                var first=(JObject)json["materials"][0];first["name"]="Duplicate";first["pbrMetallicRoughness"]["baseColorFactor"]=new JArray(1,1,1,1);
                var second=(JObject)first.DeepClone();((JArray)json["materials"]).Add(second);
                var extra=(JObject)json["meshes"][0]["primitives"][0].DeepClone();extra["material"]=1;((JArray)json["meshes"][0]["primitives"]).Add(extra);
            });
            var root=Own(new GameObject("Imported appearance"));var model=root.AddComponent<ImportedModel>();var asset=ModelLibrary.Inspect("slots.glb",bytes);var task=model.LoadAsync(asset);
            yield return new WaitUntil(()=>task.IsCompleted);Assert.That(task.Exception,Is.Null);
            string a=new string('a',32),b=new string('b',32);
            var definitions=new[]{new RoomAppearance{id=a,name="Root",style=new(){tint="#FF0000"}},new RoomAppearance{id=b,name="Slot",style=new(){tint="#0000FF",renderMode="blend",opacity=.2f}}};
            var data=new RoomObjectData{kind=RoomObjectKind.ImportedModel,modelHash=asset.Hash,appearanceBindings=new[]{
                new AppearanceBinding{appearanceId=a},new AppearanceBinding{appearanceId=b,kind="material",modelHash=asset.Hash,materialIndex=1}}};
            var view=root.AddComponent<RoomAppearanceView>();view.Configure(data,definitions);
            Material Slot(int index)=>model.Instance.Renderers.SelectMany(x=>x.sharedMaterials).First(m=>m.GetTag("MaestroMaterialIndex",false,"")==index.ToString());
            Assert.That(Slot(0).color,Is.EqualTo(Color.red));Assert.That(Slot(1).color,Is.EqualTo(Color.blue));
            view.Refresh();Assert.That(Slot(1).GetFloat("_SurfaceOpacity"),Is.EqualTo(.2f));
            model.Instance.GetComponent<PencilModelStyle>().Tint(Color.green);view.Refresh();
            Assert.That(Slot(1).color,Is.EqualTo(Color.blue),"Slot tint is independent of ordinary whole-object paint");
            model.Instance.GetComponent<PencilModelStyle>().Tint(Color.white);
            data.appearanceBindings[1].modelHash=new string('f',64);view.Configure(data,definitions);
            Assert.That(Slot(1).color,Is.EqualTo(Color.red),"A stale source slot must fall back to root, never bind the new model by index alone");
            data.appearanceBindings=System.Array.Empty<AppearanceBinding>();view.Configure(data,definitions);
            Assert.That(Slot(0).color,Is.EqualTo(Color.white));Assert.That(Slot(1).color,Is.EqualTo(Color.white));
        }
        [UnityTest]public IEnumerator RecipePartOverridesRootAndBookPageRemainsUntouched() {
            var root=Own(new GameObject("Recipe appearance"));var recipe=root.AddComponent<RecipeObject>();
            Assert.That(recipe.Apply(new RoomRecipe{parts=new[]{new RecipePart{id="body",shape="box",size=Vector3.one,color=Color.white},new RecipePart{id="tail",parent="body",shape="box",size=Vector3.one*.2f,color=Color.white}}}),Is.True);
            string a=new string('a',32),b=new string('b',32);
            var definitions=new[]{new RoomAppearance{id=a,name="Root",style=new(){tint="#FF0000"}},new RoomAppearance{id=b,name="Tail",style=new(){tint="#0000FF"}}};
            var data=new RoomObjectData{kind=RoomObjectKind.Assembly,appearanceBindings=new[]{new AppearanceBinding{appearanceId=a},new AppearanceBinding{appearanceId=b,kind="part",partId="tail"}}};
            var page=GameObject.CreatePrimitive(PrimitiveType.Quad);page.transform.SetParent(root.transform);page.AddComponent<Maestro.Quest.Book.BookPageTarget>();
            var pageMaterial=Own(IllustratedMaterials.Create(Color.white));page.GetComponent<Renderer>().sharedMaterial=pageMaterial;
            var view=root.AddComponent<RoomAppearanceView>();view.Configure(data,definitions);
            Assert.That(recipe.Part("body").GetComponentsInChildren<Renderer>().First(r=>recipe.AppearancePart(r)=="body").sharedMaterial.color,Is.EqualTo(Color.red));
            Assert.That(recipe.Part("tail").GetComponentInChildren<Renderer>().sharedMaterial.color,Is.EqualTo(Color.blue));
            Assert.That(page.GetComponent<Renderer>().sharedMaterial,Is.SameAs(pageMaterial));yield return null;
        }
        [UnityTest]public IEnumerator RootBindingsRestoreOriginalMaterialsAndRepeatedRefreshKeepsTheSameVariant() {
            int before=AppearanceMaterials.LiveVariants;var source=Own(IllustratedMaterials.Create(Color.white));var obj=Own(GameObject.CreatePrimitive(PrimitiveType.Cube));obj.GetComponent<Renderer>().sharedMaterial=source;
            var view=obj.AddComponent<RoomAppearanceView>();string id=new string('a',32);
            var data=new RoomObjectData{id=new string('b',32),kind=RoomObjectKind.Block,appearanceBindings=new[]{new AppearanceBinding{appearanceId=id}}};
            var definition=new RoomAppearance{id=id,name="Glass",style=new(){renderMode="blend",opacity=.3f}};
            view.Configure(data,new[]{definition});var first=obj.GetComponent<Renderer>().sharedMaterial;Assert.That(first,Is.Not.SameAs(source));
            view.Refresh();Assert.That(obj.GetComponent<Renderer>().sharedMaterial,Is.SameAs(first));
            data.appearanceBindings=System.Array.Empty<AppearanceBinding>();view.Configure(data,new[]{definition});Assert.That(obj.GetComponent<Renderer>().sharedMaterial,Is.SameAs(source));
            yield return null;Assert.That(AppearanceMaterials.LiveVariants,Is.EqualTo(before));
        }
    }
}
