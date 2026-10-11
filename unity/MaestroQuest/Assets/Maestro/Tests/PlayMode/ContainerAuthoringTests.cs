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
        RoomAgentRequest ContainerRequest(JObject call){
            Assert.That(BehaviourCatalog.TryCall((string)call["id"],1,(JObject)call["arguments"],out var invocation,out var error),Is.True,error);
            var request=ObjectEditRequest(call);request.conditions=invocation.Resources.Select(id=>new RoomObjectCondition{id=id,revision=editor.ObjectRevision(id)}).ToArray();return request;
        }
        JObject ContainerRun(RoomAgentExecutor ex,JObject call){Assert.That(ex.Execute(ContainerRequest(call),out var error,out _),Is.True,error);return ex.Executions.Observe();}
        JObject ContainerCall(string target,RoomContainer data)=>new(){["id"]="object.container.edit",["version"]=1,["arguments"]=data==null?new JObject{["operation"]="remove",["target"]=target,["revision"]=editor.ObjectRevision(target)}:new JObject{["operation"]="configure",["target"]=target,["revision"]=editor.ObjectRevision(target),["definition"]=ContainerCapability.Definition(data)}};
        JObject TransferCall(string from,string to,double amount)=>new(){["id"]="object.container.transfer",["version"]=1,["arguments"]=new JObject{["source"]=new JObject{["target"]=from,["revision"]=editor.ObjectRevision(from)},["destination"]=new JObject{["target"]=to,["revision"]=editor.ObjectRevision(to)},["amountMl"]=amount}};
        (RoomAgentExecutor ex,string from,string to) Containers(){var ex=new RoomAgentExecutor(editor);string from=editor.Identity(block),to=editor.Snapshot().objects.First(o=>o.kind==RoomObjectKind.Ball).id;ContainerRun(ex,ContainerCall(from,new RoomContainer{amountMl=200}));ContainerRun(ex,ContainerCall(to,new RoomContainer{capacityMl=100}));return(ex,from,to);}
        [UnityTest] public IEnumerator MeasuredTransferUsesOneAtomicUndoReadbackAndPersistentRoom(){
            var(ex,from,to)=Containers();Vector3 live=editor.Find(from).transform.localPosition+new Vector3(.2f,.1f,0);editor.Find(from).transform.localPosition=live;var receipt=ContainerRun(ex,TransferCall(from,to,150));Assert.That((double)receipt["selected"]["output"]["transferredMl"],Is.EqualTo(100));Assert.That(editor.Read(from).containers.Single().amountMl,Is.EqualTo(100));Assert.That(editor.Read(to).containers.Single().amountMl,Is.EqualTo(100));var loaded=new RoomStorage(directory).Load(out var error);Assert.That(loaded.objects.Single(x=>x.id==to).containers.Single().amountMl,Is.EqualTo(100),error);
            Assert.That(BehaviourCatalog.TryRead("object.container",1,new JObject{["target"]=to},new BehaviourCatalog.FactContext(editor:editor),out var fact),Is.True);Assert.That(fact.Characters,Is.LessThanOrEqualTo(1024));Assert.That((double)((JObject)fact.Value)["definition"]["amountMl"],Is.EqualTo(100));editor.Undo();Assert.That(editor.Find(from).transform.localPosition,Is.EqualTo(live));Assert.That(editor.Read(from).containers.Single().amountMl,Is.EqualTo(200));Assert.That(editor.Read(to).containers.Single().amountMl,Is.Zero);editor.Redo();Assert.That(editor.Read(to).containers.Single().amountMl,Is.EqualTo(100));yield return null;
        }
        [UnityTest] public IEnumerator StaleRevisionIncompatibleContentsAndHumanOwnershipDoNotTransfer(){
            var(ex,from,to)=Containers();var stale=TransferCall(from,to,50);ContainerRun(ex,ContainerCall(to,new RoomContainer{amountMl=20,liquid="Juice"}));Assert.That(ex.Execute(ContainerRequest(stale),out _,out _),Is.False);Assert.That(ex.Execute(ContainerRequest(TransferCall(from,to,50)),out _,out _),Is.False);Assert.That(editor.Read(from).containers.Single().amountMl,Is.EqualTo(200));ContainerRun(ex,ContainerCall(to,new RoomContainer()));
            Assert.That(editor.Ownership.TryAcquire("human","Your edit",RoomActorRole.Control,new[]{new BehaviourCatalog.Claim(to,"wholeTarget")},null,out var lease,out var error),Is.True,error);try{Assert.That(ex.Execute(ContainerRequest(TransferCall(from,to,50)),out _,out _),Is.False);Assert.That(editor.Read(to).containers.Single().amountMl,Is.Zero);}finally{lease.Dispose();}yield return null;
        }
        [UnityTest] public IEnumerator TransferSaveFailureChangesNeitherContainerAndDoesNotReplay(){
            var(ex,from,to)=Containers();string path=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(path);try{Assert.That(ex.Execute(ContainerRequest(TransferCall(from,to,50)),out var failure,out _),Is.False);StringAssert.Contains("save",failure.ToLowerInvariant());Assert.That(editor.Read(from).containers.Single().amountMl,Is.EqualTo(200));Assert.That(editor.Read(to).containers.Single().amountMl,Is.Zero);}finally{Directory.Delete(path);}yield return null;Assert.That(editor.Read(to).containers.Single().amountMl,Is.Zero);
        }
        [UnityTest] public IEnumerator TemporaryTransferCopyAndRemovalRetainIndependentQuantities(){
            var(ex,from,to)=Containers();Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);while(editor.TemporarySavePending)yield return null;ContainerRun(ex,TransferCall(from,to,75));Assert.That(editor.Read(from).containers.Single().amountMl,Is.EqualTo(125));Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.Read(from).containers.Single().amountMl,Is.EqualTo(200));Assert.That(editor.Read(to).containers.Single().amountMl,Is.Zero);
            Assert.That(editor.CopyObject(from,editor.ObjectRevision(from),"Copy",Vector3.one,out var copy,out error),Is.True,error);ContainerRun(ex,ContainerCall(copy,new RoomContainer{amountMl=25}));Assert.That(editor.Read(from).containers.Single().amountMl,Is.EqualTo(200));ContainerRun(ex,ContainerCall(copy,null));Assert.That(editor.Read(copy).containers,Is.Empty);editor.Undo();Assert.That(editor.Read(copy).containers.Single().amountMl,Is.EqualTo(25));yield return null;
        }
        [UnityTest] public IEnumerator IncludedCupShowsSavedFillAndTiltWithoutChangingQuantities(){
            var ex=new RoomAgentExecutor(editor);var receipt=TemplateRun(ex,TemplateCall("cup",new Vector3(3,1,0)));string id=(string)receipt["selected"]["output"]["objectId"];var definition=editor.Read(id).containers.Single();definition.amountMl=250;ContainerRun(ex,ContainerCall(id,definition));var item=editor.Find(id);item.transform.localRotation=Quaternion.Euler(0,0,18);item.GetComponent<ContainerFillView>().Refresh();
            string output=Environment.GetEnvironmentVariable("MAESTRO_GROUP_EVIDENCE");
            if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);var go=new GameObject("Liquid evidence",typeof(Camera));var camera=go.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.9f,.92f,.93f);camera.nearClipPlane=.01f;camera.farClipPlane=5;camera.fieldOfView=32;go.transform.position=item.transform.position+new Vector3(.28f,.7f,-.45f);go.transform.LookAt(item.transform.position);var render=new RenderTexture(1000,900,24);var pixels=new Texture2D(1000,900,TextureFormat.RGB24,false);var previous=RenderTexture.active;
                try{camera.targetTexture=render;camera.Render();RenderTexture.active=render;pixels.ReadPixels(new Rect(0,0,1000,900),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(output,"measured-cup.png"),pixels.EncodeToPNG());var fill=item.transform.Find("Measured liquid surface");File.WriteAllText(Path.Combine(output,"measured-cup-state.json"),new JObject{["definition"]=ContainerCapability.Definition(editor.Read(id).containers[0]),["active"]=fill.gameObject.activeSelf,["vertices"]=fill.GetComponent<MeshFilter>().sharedMesh.vertexCount,["color"]=JObject.Parse(JsonUtility.ToJson(fill.GetComponent<Renderer>().sharedMaterial.GetColor("_Color"))),["position"]=JObject.Parse(JsonUtility.ToJson(fill.position)),["bounds"]=fill.GetComponent<Renderer>().bounds.ToString()}.ToString());Assert.That(pixels.GetPixels32().Count(p=>p.b>p.r+40&&p.b>p.g+20),Is.GreaterThan(100),"The saved blue liquid must be visible inside the actual cup");}finally{RenderTexture.active=previous;camera.targetTexture=null;render.Release();UnityEngine.Object.Destroy(render);UnityEngine.Object.Destroy(pixels);UnityEngine.Object.Destroy(go);}}
            Assert.That(editor.Read(id).containers.Single().amountMl,Is.EqualTo(250));yield return null;
        }
        [UnityTest] public IEnumerator LiquidSurfaceTracksGravityWithBoundedGeometryAndNoColliders(){
            var(ex,from,to)=Containers();var item=editor.Find(from);var view=item.GetComponent<ContainerFillView>();Assert.That(view,Is.Not.Null);item.transform.rotation=Quaternion.Euler(20,30,50);view.Refresh();var mesh=view.GetComponentInChildren<MeshFilter>(true);var surface=item.transform.Find("Measured liquid surface");Assert.That(surface,Is.Not.Null);mesh=surface.GetComponent<MeshFilter>();Assert.That(mesh.sharedMesh.vertexCount,Is.InRange(3,74));Assert.That(surface.GetComponentsInChildren<Collider>(),Is.Empty);var points=mesh.sharedMesh.vertices.Select(surface.TransformPoint).ToArray();Assert.That(points.Max(p=>p.y)-points.Min(p=>p.y),Is.LessThan(.0001));Assert.That(mesh.sharedMesh.normals.All(n=>n.sqrMagnitude>.99f),Is.True);Assert.That(editor.Read(from).containers.Single().amountMl,Is.EqualTo(200));
            foreach(float amount in new[]{.05f,25,200,249.95f})foreach(float angle in new[]{18,45,66,90,135}){
                var display=editor.Read(from).containers[0].Copy();display.amountMl=amount;view.Apply(new[]{display});item.transform.rotation=Quaternion.Euler(13,24,angle);view.Refresh();var shape=surface.GetComponent<MeshFilter>().sharedMesh;var vertices=shape.vertices;var triangles=shape.triangles;var normal=surface.InverseTransformDirection(Vector3.up);Assert.That(vertices.Length,Is.InRange(3,74),amount+" ml at "+angle);
                for(int i=0;i<triangles.Length;i+=3)Assert.That(Vector3.Dot(Vector3.Cross(vertices[triangles[i+1]]-vertices[triangles[i]],vertices[triangles[i+2]]-vertices[triangles[i]]),normal),Is.GreaterThanOrEqualTo(-1e-10),"Convex liquid surface must not fold across itself");
            }
            ContainerRun(ex,ContainerCall(from,new RoomContainer()));Assert.That(surface.gameObject.activeSelf,Is.False);yield return null;
        }
    }
}
