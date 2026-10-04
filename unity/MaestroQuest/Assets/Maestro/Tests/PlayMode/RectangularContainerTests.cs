// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        (RoomAgentExecutor executor,string pool,string bucket) PoolVessels(){
            editor.Liquids.enabled=false;var ex=new RoomAgentExecutor(editor);
            string pool=(string)TemplateRun(ex,TemplateCall("shallow-pool",new Vector3(4,2,0)))["selected"]["output"]["objectId"];
            string bucket=(string)TemplateRun(ex,TemplateCall("bucket",new Vector3(4,2.15f,0)))["selected"]["output"]["objectId"];
            physics.SetSurfaces(true,"Synthetic room ready");return(ex,pool,bucket);
        }
        [UnityTest] public IEnumerator RectangularPoolDipsThroughTheRealSharedRuntimeAndPersistsOneAtomicUndo(){
            var(ex,pool,bucket)=PoolVessels();double total=editor.Read(pool).containers[0].amountMl;int published=0;
            editor.ContainerScooped+=(id,amount,donors,liquid)=>{Assert.That(id,Is.EqualTo(bucket));Assert.That(amount,Is.GreaterThan(0));published++;};
            physics.StartPhysics();editor.Liquids.Tick(.05f);double picked=Liquid(bucket);
            Assert.That(picked,Is.GreaterThan(0));Assert.That(Liquid(pool)+picked,Is.EqualTo(total).Within(1e-8));Assert.That(editor.Read(bucket).containers[0].amountMl,Is.Zero);Assert.That(published,Is.Zero);
            physics.PausePhysics();Assert.That(published,Is.EqualTo(1));Assert.That(editor.Read(bucket).containers[0].amountMl,Is.EqualTo(picked));
            var loaded=new RoomStorage(directory).Load(out var error);Assert.That(loaded,Is.Not.Null,error);var saved=loaded.objects.Single(x=>x.id==pool).containers[0];Assert.That(saved.version,Is.EqualTo(2));Assert.That(saved.rectangle.width,Is.EqualTo(1.18f));Assert.That(saved.amountMl+picked,Is.EqualTo(total).Within(1e-8));
            editor.Undo();Assert.That(editor.Read(pool).containers[0].amountMl,Is.EqualTo(total));Assert.That(editor.Read(bucket).containers[0].amountMl,Is.Zero);editor.Redo();Assert.That(editor.Read(bucket).containers[0].amountMl,Is.EqualTo(picked));Assert.That(published,Is.EqualTo(1));
            // Same catalog fact/configuration path used by the advanced book and agent.
            Assert.That(BehaviourCatalog.TryRead("object.container",1,new JObject{["target"]=pool},new BehaviourCatalog.FactContext(editor:editor),out var before),Is.True);
            var configured=editor.Read(pool).containers[0].Copy();configured.rectangle.width=1.16f;var call=ContainerCall(pool,configured);var reply=ContainerRun(ex,call);
            Assert.That(BehaviourCatalog.TryRead("object.container",1,new JObject{["target"]=pool},new BehaviourCatalog.FactContext(editor:editor),out var after),Is.True);
            string output=Environment.GetEnvironmentVariable("MAESTRO_GROUP_EVIDENCE");if(!string.IsNullOrEmpty(output)){
                Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"rectangular-container-authoring.json"),new JObject{["target"]=pool,["before"]=JToken.FromObject(before.Value),["call"]=call,["reply"]=reply,["after"]=JToken.FromObject(after.Value)}.ToString());
            }
            Assert.That(editor.Read(pool).containers[0].rectangle.width,Is.EqualTo(1.16f));editor.Undo();Assert.That(editor.Read(pool).containers[0].rectangle.width,Is.EqualTo(1.18f));yield return null;
        }
        [UnityTest] public IEnumerator RectangularPoolRejectsBlockedImmersionAndRollsBackFailedPublication(){
            var(_,pool,bucket)=PoolVessels();double total=editor.Read(pool).containers[0].amountMl;
            editor.Find(bucket).transform.position+=Vector3.right*.56f;Physics.SyncTransforms();physics.StartPhysics();editor.Liquids.Tick(.05f);Assert.That(Liquid(bucket),Is.Zero,"Bucket cavity crosses the wall");physics.PausePhysics();
            editor.Find(bucket).transform.position=new Vector3(4,2.15f,0);Physics.SyncTransforms();physics.StartPhysics();editor.Liquids.Tick(.05f);Assert.That(Liquid(bucket),Is.GreaterThan(0));
            string pending=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(pending);
            try{Assert.That(editor.Liquids.Finish(out _),Is.False);}finally{Directory.Delete(pending);}
            Assert.That(Liquid(bucket),Is.Zero);Assert.That(Liquid(pool),Is.EqualTo(total));Assert.That(editor.Read(pool).containers[0].rectangle.depth,Is.EqualTo(.78f));Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);yield return null;
        }
        [UnityTest] public IEnumerator RectangularPoolCanReceiveAPourAndRestoresQuantitiesInTemporaryPlay(){
            var(ex,pool,bucket)=PoolVessels();double total=editor.Read(pool).containers[0].amountMl;
            physics.StartPhysics();for(int i=0;i<20;i++)editor.Liquids.Tick(.05f);physics.PausePhysics();double picked=editor.Read(bucket).containers[0].amountMl;Assert.That(picked,Is.GreaterThan(1000));
            var held=editor.Find(bucket);held.transform.SetPositionAndRotation(new Vector3(4,2.8f,0),Quaternion.Euler(0,0,80));Physics.SyncTransforms();physics.StartPhysics();for(int i=0;i<30;i++)editor.Liquids.Tick(.05f);physics.PausePhysics();
            Assert.That(editor.Read(pool).containers[0].amountMl,Is.GreaterThan(total-picked));Assert.That(editor.Read(pool).containers[0].amountMl+editor.Read(bucket).containers[0].amountMl,Is.EqualTo(total).Within(.001));
            // Put both vessels away from each other before beginning a temporary edit.
            held.transform.position=new Vector3(6,2,0);held.transform.rotation=Quaternion.identity;
            var baseline=editor.Read(pool).containers[0].amountMl;Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);while(editor.TemporarySavePending)yield return null;
            var changed=editor.Read(pool).containers[0].Copy();changed.rectangle.width=1;changed.amountMl=12;ContainerRun(ex,ContainerCall(pool,changed));
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.Read(pool).containers[0].amountMl,Is.EqualTo(baseline));Assert.That(editor.Read(pool).containers[0].rectangle.width,Is.EqualTo(1.18f));
        }
        [UnityTest] public IEnumerator RectangularFillIsHorizontalBoundedAndVisibleInTheActualPool(){
            var(_,pool,bucket)=PoolVessels();editor.Find(bucket).gameObject.SetActive(false);var item=editor.Find(pool);var view=item.GetComponent<ContainerFillView>();var fill=item.transform.Find("Measured liquid surface");Assert.That(fill,Is.Not.Null);
            foreach(var rotation in new[]{Quaternion.identity,Quaternion.Euler(20,30,45),Quaternion.Euler(45,0,90)}){
                item.transform.rotation=rotation;view.Refresh();var mesh=fill.GetComponent<MeshFilter>().sharedMesh;Assert.That(mesh.vertexCount,Is.InRange(4,7));var points=mesh.vertices.Select(fill.TransformPoint).ToArray();Assert.That(points.Max(p=>p.y)-points.Min(p=>p.y),Is.LessThan(.0001));Assert.That(fill.GetComponentsInChildren<Collider>(),Is.Empty);
                foreach(var point in mesh.vertices){Assert.That(Mathf.Abs(point.x),Is.LessThanOrEqualTo(.59001f));Assert.That(Mathf.Abs(point.z),Is.LessThanOrEqualTo(.39001f));}
            }
            item.transform.rotation=Quaternion.identity;view.Refresh();string output=Environment.GetEnvironmentVariable("MAESTRO_GROUP_EVIDENCE");
            if(!string.IsNullOrEmpty(output)){
                Directory.CreateDirectory(output);var go=new GameObject("Shallow pool evidence",typeof(Camera));var camera=go.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.9f,.92f,.93f);camera.nearClipPlane=.01f;camera.farClipPlane=5;camera.fieldOfView=38;go.transform.position=item.transform.position+new Vector3(1.4f,1.8f,-1.6f);go.transform.LookAt(item.transform.position+Vector3.up*.12f);
                var render=new RenderTexture(1000,900,24);var pixels=new Texture2D(1000,900,TextureFormat.RGB24,false);var previous=RenderTexture.active;
                try{camera.targetTexture=render;camera.Render();RenderTexture.active=render;pixels.ReadPixels(new Rect(0,0,1000,900),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(output,"shallow-pool.png"),pixels.EncodeToPNG());Assert.That(pixels.GetPixels32().Count(p=>p.b>p.r+40&&p.b>p.g+20),Is.GreaterThan(2000),"The actual pool must contain visible blue water");}
                finally{RenderTexture.active=previous;camera.targetTexture=null;render.Release();UnityEngine.Object.Destroy(render);UnityEngine.Object.Destroy(pixels);UnityEngine.Object.Destroy(go);}
            }
            yield return null;
        }
    }
}
