// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        GameObject CaptureBox(Transform parent,Color color,Vector3 position){
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.transform.SetParent(parent,false);go.transform.position=position;
            var material=new Material(Shader.Find("Unlit/Color"));material.color=color;go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
        [UnityTest] public IEnumerator VirtualViewCaptureRendersOwnedContentExcludesBookAndUnrelatedSceneAndRestoresLayers(){
            var viewer=new GameObject("Capture viewer");viewer.transform.SetParent(root.transform,false);viewer.transform.position=new Vector3(0,1,0);root.GetComponent<RoomInteraction>().Viewer=viewer.transform;
            var owned=CaptureBox(block.transform,Color.red,new Vector3(0,1,3));var hiddenBook=CaptureBox(editor.Find("book").transform,Color.blue,new Vector3(0,1,1));var other=CaptureBox(root.transform,Color.green,new Vector3(0,1,2));
            // An enabled compositor mask would black out this JPEG if virtual-only
            // capture accidentally treated physical passthrough as virtual content.
            var mask=GameObject.CreatePrimitive(PrimitiveType.Quad);mask.transform.SetParent(block.transform,false);mask.transform.position=new Vector3(0,1,.5f);mask.transform.localScale=Vector3.one*4;
            mask.GetComponent<Renderer>().sharedMaterial=new Material(Shader.Find("Maestro/PassthroughWindow"));
            var originals=new[]{owned,hiddenBook,other,mask}.Select(x=>x.layer).ToArray();var before=JsonUtility.ToJson(editor.Snapshot());var ex=new RoomAgentExecutor(editor);
            var request=TemplateRequest(new JObject{["id"]="room.view.capture",["version"]=1,["arguments"]=new JObject()});Assert.That(ex.Execute(request,out var error,out _),Is.True,error);
            var output=(JObject)ex.Executions.Observe()["selected"]["output"];Assert.That(output,Is.Not.Null);var image=editor.ViewCapturePayload(new string('a',32),null);Assert.That(JToken.DeepEquals(output,image["capture"]),Is.True);Assert.That(image.ToString().Length,Is.LessThan(140000));
            var bytes=Convert.FromBase64String((string)image["data"]);var pixels=new Texture2D(2,2);Assert.That(pixels.LoadImage(bytes),Is.True);Assert.That(pixels.width,Is.EqualTo(512));Assert.That(pixels.height,Is.EqualTo(384));
            var colors=pixels.GetPixels32();Assert.That(colors.Count(p=>p.r>180&&p.g<70&&p.b<70),Is.GreaterThan(1000));Assert.That(colors.Count(p=>p.b>180&&p.r<70&&p.g<70),Is.Zero);Assert.That(colors.Count(p=>p.g>180&&p.r<70&&p.b<70),Is.Zero);
            for(int i=0;i<4;i++)Assert.That(new[]{owned,hiddenBook,other,mask}[i].layer,Is.EqualTo(originals[i]));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));Assert.That(editor.CanCaptureView(out _),Is.False);
            Assert.That(editor.ViewCapturePayload(new string('a',32),(string)output["captureId"]),Is.Null);
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_VIEW_CAPTURE_EVIDENCE");if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.WriteAllBytes(Path.Combine(evidence,"owned-room.jpg"),bytes);File.WriteAllText(Path.Combine(evidence,"capture.json"),image.ToString());}
            editor.ClearViewCapture();Assert.That(editor.ViewCaptureMetadata,Is.Null);Assert.That(ex.Execute(request,out error,out _),Is.True,error);Assert.That(editor.ViewCaptureMetadata,Is.Null,"Replaying a durable capture receipt cannot take a new picture");
            foreach(var go in new[]{owned,hiddenBook,other,mask})UnityEngine.Object.Destroy(go.GetComponent<Renderer>().sharedMaterial);UnityEngine.Object.Destroy(pixels);UnityEngine.Object.Destroy(viewer);yield return null;
        }
        [UnityTest] public IEnumerator VirtualViewCaptureHonoursRoomHoldPreservesPreviousOnFailureAndClearsOnClientChange(){
            var viewer=new GameObject("Capture viewer");viewer.transform.SetParent(root.transform,false);root.GetComponent<RoomInteraction>().Viewer=viewer.transform;
            var agent=root.AddComponent<RoomAgent>();agent.Initialize(editor,null);string client=new string('b',32);agent.Receive(new JObject{["clientId"]=client,["session"]=""}.ToString());
            Assert.That(editor.CaptureView(out var first,out var error),Is.True,error);Assert.That(agent.CapturePayload,Is.Not.Null);yield return new WaitForSecondsRealtime(1.05f);
            var conflict=CaptureBox(root.transform,Color.green,Vector3.forward);conflict.layer=31;Assert.That(editor.CaptureView(out _,out error),Is.False);Assert.That(JToken.DeepEquals(first,editor.ViewCaptureMetadata),Is.True);Assert.That(conflict.layer,Is.EqualTo(31));UnityEngine.Object.Destroy(conflict.GetComponent<Renderer>().sharedMaterial);UnityEngine.Object.Destroy(conflict);yield return null;
            agent.Receive(new JObject{["clientId"]=new string('c',32),["session"]=agent.Observe().session}.ToString());Assert.That(agent.CapturePayload,Is.Null);Assert.That(agent.Observe().capture,Is.Null);
            using(var hold=editor.RuntimeGate.Hold("Capture test hold")){Assert.That(editor.CanCaptureView(out _),Is.False);Assert.That(editor.CaptureView(out _,out _),Is.False);}
            UnityEngine.Object.Destroy(viewer);yield return null;
        }
    }
}
