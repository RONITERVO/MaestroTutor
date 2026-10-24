// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed class RecipeMaterialSharingTests
    {
        readonly List<GameObject> objects=new();
        RoomRecipe Source()=>new() {parts=new[] {new RecipePart {id="panel",size=new Vector3(.3f,.3f,.08f),color=new Color(.72f,.53f,.31f),
            pattern=new RecipePattern {kind="checker",plane="xy",columns=4,rows=4,secondary="#C5E9F0"}}}};
        RecipeObject Create(RoomRecipe source=null) {var go=new GameObject("Shared recipe paint");objects.Add(go);var recipe=go.AddComponent<RecipeObject>();Assert.That(recipe.Apply(source??Source()),Is.True);return recipe;}
        static Material Paint(RecipeObject recipe)=>recipe.Part("panel").GetComponentInChildren<Renderer>().sharedMaterial;
        [TearDown] public void Cleanup(){foreach(var go in objects)if(go)UnityEngine.Object.DestroyImmediate(go);objects.Clear();}
        [UnityTest] public IEnumerator IdenticalPaintSharesWithoutRecolouringOrDestroyingAnotherOwner()
        {
            var first=Create();var second=Create();var shared=Paint(first);
            Assert.That(Paint(second),Is.SameAs(shared),"Repeated parts must reuse an identical instancing material");
            var original=shared.color;var secondary=shared.GetColor("_PatternColor");var tint=new Color(.4f,.8f,.6f,1);
            first.Tint(tint);Assert.That(Paint(first),Is.Not.SameAs(shared));Assert.That(Paint(second).color,Is.EqualTo(original));
            Assert.That(Paint(second).GetColor("_PatternColor"),Is.EqualTo(secondary));
            second.Tint(tint);var tinted=Paint(second);Assert.That(Paint(first),Is.SameAs(tinted));
            Assert.That(Vector4.Distance(tinted.color,original*tint),Is.LessThan(.000001f));Assert.That(Vector4.Distance(tinted.GetColor("_PatternColor"),secondary*tint),Is.LessThan(.000001f));
            var changed=Source();changed.parts[0].pattern.columns=7;Assert.That(first.Apply(changed),Is.True);yield return null;
            Assert.That(tinted!=null,Is.True,"Rebuilding one owner must not destroy the other's material");
            UnityEngine.Object.Destroy(first.gameObject);yield return null;Assert.That(tinted!=null,Is.True);
            UnityEngine.Object.Destroy(second.gameObject);yield return null;yield return null;
            Assert.That(tinted==null,Is.True,"The last owner releases the material instead of retaining every historical paint");
        }
        [UnityTest] public IEnumerator PatternAndCylinderCoordinatesRemainIndependent()
        {
            var original=Create();var material=Paint(original);
            Action<RecipePart>[] changes={p=>p.color=Color.green,p=>p.pattern.secondary="#ABCDEF",p=>p.pattern.kind="stripes",p=>p.pattern.plane="xz",p=>p.pattern.columns=5,p=>p.pattern.rows=5,p=>p.shape="cylinder"};
            foreach(var change in changes){var source=Source();change(source.parts[0]);var other=Create(source);Assert.That(Paint(other),Is.Not.SameAs(material));}
            Assert.That(Paint(objects.Last().GetComponent<RecipeObject>()).GetVector("_PatternCoordinates").y,Is.EqualTo(.5f));
            Assert.That(material.GetVector("_PatternCoordinates").y,Is.EqualTo(1));yield return null;
        }
        static Color32[] Render(Camera camera,RenderTexture target,string label)
        {
            var previous=RenderTexture.active;var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            try {camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
                var output=Environment.GetEnvironmentVariable("MAESTRO_RECIPE_SHARING_PREVIEW");if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllBytes(Path.Combine(output,label+".png"),texture.EncodeToPNG());}
                return texture.GetPixels32();
            }finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(texture);}
        }
        static double Difference(Color32[] first,Color32[] second,int minX,int maxX)
        {
            double sum=0;int count=0;for(int y=0;y<128;y++)for(int x=minX;x<maxX;x++){int i=y*256+x;sum+=Math.Abs(first[i].r-second[i].r)+Math.Abs(first[i].g-second[i].g)+Math.Abs(first[i].b-second[i].b);count+=3;}return sum/(count*255.0);
        }
        [UnityTest] public IEnumerator RepaintingChangesOnlyItsPixelsAndMovingKeepsObjectSpacePattern()
        {
            var first=Create();var second=Create();first.transform.position=Vector3.left*.3f;second.transform.position=Vector3.right*.3f;
            foreach(var go in objects)foreach(var child in go.GetComponentsInChildren<Transform>())child.gameObject.layer=31;
            var cameraObject=new GameObject("Recipe paint camera");objects.Add(cameraObject);var camera=cameraObject.AddComponent<Camera>();
            camera.enabled=false;camera.cullingMask=1<<31;camera.orthographic=true;camera.orthographicSize=.3f;camera.nearClipPlane=.01f;camera.farClipPlane=3;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.12f,.12f,1);camera.transform.position=Vector3.back;
            var target=new RenderTexture(256,128,24);camera.targetTexture=target;
            try {yield return null;var before=Render(camera,target,"before");first.Tint(new Color(.2f,.65f,.85f,1));var painted=Render(camera,target,"painted");
                Assert.That(Difference(before,painted,0,128),Is.GreaterThan(.01),"Repainting must change the visible object");
                Assert.That(Difference(before,painted,128,256),Is.LessThan(.001),"Repainting one object must not bleed into its neighbour");
                var shift=new Vector3(.137f,.087f,.031f);first.transform.position+=shift;second.transform.position+=shift;camera.transform.position+=shift;
                var moved=Render(camera,target,"moved");Assert.That(Difference(painted,moved,0,256),Is.LessThan(.005),"Pigment and pattern must remain fixed in object space");
            }finally{camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
    }
}
