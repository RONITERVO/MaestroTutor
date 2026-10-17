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
        JObject CurvedConfigure(string kind){var call=SurfaceConfigure();var d=call["arguments"]["definition"];d["shape"]=kind;d["curvatureRadius"]=.06;d["width"]=.18;d["height"]=.12;return call;}
        Ray CurvedRay(DrawingSurface surface,Transform patch,float x,float y){var p=DrawingSurfaceGeometry.Point(surface,new Vector3(x,y,0));var n=(DrawingSurfaceGeometry.Point(surface,new Vector3(x,y,0),.01f)-p).normalized;return new Ray(patch.TransformPoint(p+n*.08f),-patch.TransformDirection(n));}
        [UnityTest] public IEnumerator CurvedSurfaceAndFlatEmptyFactPagesRemainTypedAndRejectStaleReads(){
            var ex=new RoomAgentExecutor(editor);string target=editor.Identity(block);var context=new BehaviourCatalog.FactContext(editor:editor);
            foreach(string kind in new[]{"plane","cylinder","sphere"}){
                Assert.That(BehaviourCatalog.TryRead("object.surfaces",1,new JObject{["target"]=target},context,out var overview),Is.True);
                Assert.That(((JArray)((JObject)overview.Value)["surfaces"]),Is.Empty);Assert.That(overview.Type.Fields["surfaces"].Item.Kind,Is.EqualTo(ProgramType.Record));
                SurfaceRun(ex,kind=="plane"?SurfaceConfigure():CurvedConfigure(kind));
                var query=new JObject{["target"]=target,["surface"]="Front",["revision"]=editor.ObjectRevision(target),["offset"]=0};
                Assert.That(BehaviourCatalog.TryRead("object.surface.strokes",1,query,context,out var empty),Is.True);
                Assert.That(((JArray)((JObject)empty.Value)["strokes"]),Is.Empty);Assert.That(empty.Type.Fields["strokes"].Item.Kind,Is.EqualTo(ProgramType.Record));
                SurfaceRun(ex,SurfaceAdd());Assert.That(BehaviourCatalog.TryRead("object.surface.strokes",1,query,context,out _),Is.False,"A pre-edit revision must still be rejected");
                query["revision"]=editor.ObjectRevision(target);query["offset"]=1;
                Assert.That(BehaviourCatalog.TryRead("object.surface.strokes",1,query,context,out var end),Is.True);Assert.That(((JArray)((JObject)end.Value)["strokes"]),Is.Empty);
                query["offset"]=2;Assert.That(BehaviourCatalog.TryRead("object.surface.strokes",1,query,context,out _),Is.False);
                var stroke=editor.Read(target).surfaces[0].strokes.Single();query["stroke"]=stroke.id;query["offset"]=stroke.points.Length;
                Assert.That(BehaviourCatalog.TryRead("object.surface.stroke",1,query,context,out var points),Is.True);
                Assert.That(((JArray)((JObject)points.Value)["points"]),Is.Empty);Assert.That(points.Type.Fields["points"].Item.Kind,Is.EqualTo(ProgramType.Record));
                query["offset"]=stroke.points.Length+1;Assert.That(BehaviourCatalog.TryRead("object.surface.stroke",1,query,context,out _),Is.False);
                query["offset"]=stroke.points.Length;SurfaceRun(ex,SurfaceCall("clear"));Assert.That(BehaviourCatalog.TryRead("object.surface.stroke",1,query,context,out _),Is.False);
                SurfaceRun(ex,SurfaceCall("removeSurface"));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator CurvedSurfacePhysicalCaptureEraseAndUndoUseSharedCoordinates(){
            var ex=new RoomAgentExecutor(editor);SurfaceRun(ex,CurvedConfigure("sphere"));string target=editor.Identity(block);var view=block.GetComponent<DrawingSurfaceView>();var patch=view.Surface("Front");var surface=editor.Read(target).surfaces[0];
            var pencil=root.AddComponent<SpatialDrawing>();pencil.Editor=editor;editor.ToggleSurfaceDrawing();pencil.Begin(0,CurvedRay(surface,patch,-.04f,0));pencil.Move(0,CurvedRay(surface,patch,0,.03f));pencil.Move(0,CurvedRay(surface,patch,.04f,0));pencil.End(0);
            Assert.That(pencil.HasUnsavedStroke,Is.False);Assert.That(view.StrokeCount,Is.EqualTo(1));var stroke=editor.Read(target).surfaces[0].strokes.Single();Assert.That(stroke.points.Length,Is.EqualTo(3));Assert.That(Vector3.Distance(stroke.points[1],new Vector3(0,.03f,0)),Is.LessThan(.00001));
            Assert.That(BehaviourCatalog.TryRead("object.surface",1,new JObject{["target"]=target,["surface"]="Front"},new BehaviourCatalog.FactContext(editor:editor),out var fact),Is.True);Assert.That((string)((JObject)fact.Value)["geometry"]["shape"],Is.EqualTo("sphere"));
            Assert.That(editor.ConfigureDrawing("surfaceErase",Color.white,.003f,out var error),Is.True,error);patch=view.Surface("Front");pencil.Begin(0,CurvedRay(surface,patch,0,.03f));Assert.That(view.StrokeCount,Is.Zero);Assert.That(editor.Find(target),Is.Not.Null);editor.Undo();Assert.That(view.StrokeCount,Is.EqualTo(1));yield return null;
        }
        [UnityTest] public IEnumerator CurvedSurfaceScaledRayReadbackAndFailedSaveKeepTheSamePatch(){
            var ex=new RoomAgentExecutor(editor);SurfaceRun(ex,CurvedConfigure("cylinder"));var surface=editor.Read(editor.Identity(block)).surfaces[0];var view=block.GetComponent<DrawingSurfaceView>();var patch=view.Surface("Front");
            block.transform.SetPositionAndRotation(new Vector3(3,2,1),Quaternion.Euler(10,35,8));block.transform.localScale=Vector3.one*1.7f;
            var ray=CurvedRay(surface,patch,.035f,.025f);Assert.That(view.Hit(ray,.25f,out _,out var uv,out var distance),Is.True);Assert.That(Vector3.Distance(uv,new Vector3(.035f,.025f,0)),Is.LessThan(.00001));Assert.That(distance,Is.EqualTo(.136f).Within(.00001));
            var pencil=root.AddComponent<SpatialDrawing>();pencil.Editor=editor;editor.ToggleSurfaceDrawing();pencil.Begin(0,CurvedRay(surface,patch,-.03f,0));pencil.Move(0,CurvedRay(surface,patch,.03f,.02f));
            string pending=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(pending);pencil.End(0);Assert.That(pencil.HasUnsavedStroke,Is.True);Assert.That(view.StrokeCount,Is.Zero);Directory.Delete(pending);block.transform.position+=Vector3.up*.1f;
            Assert.That(pencil.Resolve(pencil.SessionId,false,out _,out var error),Is.True,error);Assert.That(view.StrokeCount,Is.EqualTo(1));Assert.That(new RoomStorage(directory).Load(out error).objects.Single(o=>o.id==editor.Identity(block)).surfaces[0].Kind,Is.EqualTo("cylinder"),error);yield return null;
        }
        [UnityTest] public IEnumerator CurvedSurfaceAuthoredInkCaptureAndTemporaryEditsPreserveSource(){
            var ex=new RoomAgentExecutor(editor);SurfaceRun(ex,CurvedConfigure("sphere"));SurfaceRun(ex,SurfaceAdd());string target=editor.Identity(block);var original=editor.Read(target).surfaces[0];
            var invalid=CurvedConfigure("sphere");invalid["arguments"]["definition"]["curvatureRadius"]=.01;Assert.That(ex.Execute(ObjectEditRequest(invalid),out _,out _),Is.False);Assert.That(editor.Read(target).surfaces[0].curvatureRadius,Is.EqualTo(.06f));
            Assert.That(editor.CaptureConstruction(new[]{new ConstructionMember{target=target,revision=editor.ObjectRevision(target),slot="Painted"}},out var capture,out var error),Is.True,error);
            var module=ConstructionModule.Definition(capture,"Curved painted object");var args=(JObject)module["program"]["functions"][1]["body"][0]["arguments"];Assert.That(editor.CreateBatch(CreationBatch.Read(args),out var ids,out error),Is.True,error);
            Assert.That(editor.Read(ids[0]).surfaces[0].strokes[0].points,Is.EqualTo(original.strokes[0].points));Assert.That(editor.Read(ids[0]).surfaces[0].Kind,Is.EqualTo("sphere"));
            Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);while(editor.TemporarySavePending)yield return null;SurfaceRun(ex,SurfaceCall("clear"));Assert.That(editor.Read(target).surfaces[0].strokes,Is.Empty);Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.Read(target).surfaces[0].strokes.Length,Is.EqualTo(1));yield return null;
        }
        [UnityTest] public IEnumerator CurvedSurfaceHeldChalkUsesTheSameContactAndSavedArcCoordinates(){
            var(id,tip,_)=DrawingTipStage();var ex=new RoomAgentExecutor(editor);SurfaceRun(ex,CurvedConfigure("cylinder"));var surface=editor.Read(editor.Identity(block)).surfaces[0];var patch=block.GetComponent<DrawingSurfaceView>().Surface("Front");
            void Place(float x){var item=editor.Find(id);var definition=editor.Read(id).drawingTips[0];var anchor=item.GetComponent<RecipeObject>().Part(definition.part);var ray=CurvedRay(surface,patch,x,0);item.transform.rotation=Quaternion.LookRotation(ray.direction,patch.up);item.transform.position+=patch.TransformPoint(DrawingSurfaceGeometry.Point(surface,new Vector3(x,0,0)))-anchor.TransformPoint(definition.position);}
            tip.Sample(false,RoomActorRole.Control);Place(-.03f);tip.Sample(true,RoomActorRole.Control);Assert.That(root.GetComponent<SpatialDrawing>().IsToolDrawing(id),Is.True);Place(.03f);tip.Sample(true,RoomActorRole.Control);tip.Sample(false,RoomActorRole.Control);
            var mark=editor.Read(editor.Identity(block)).surfaces[0].strokes.Single();Assert.That(mark.points.Length,Is.EqualTo(2));Assert.That(mark.points[0].x,Is.EqualTo(-.03f).Within(.00001));Assert.That(mark.points[1].x,Is.EqualTo(.03f).Within(.00001));editor.Undo();Assert.That(editor.Read(editor.Identity(block)).surfaces[0].strokes,Is.Empty);yield return null;
        }
        [UnityTest] public IEnumerator CurvedSurfaceRealPrimitiveInkRendersOnSphereAndCylinder(){
            var ex=new RoomAgentExecutor(editor);int index=0;
            foreach(var kind in new[]{RoomObjectKind.Ball,RoomObjectKind.Cylinder}){
                Assert.That(editor.CreatePrimitive(kind,"Paint sample",new Vector3(index==0?9.83f:10.17f,2,1),2,Color.white,out var id,out var error),Is.True,error);
                var cfg=CurvedConfigure(kind==RoomObjectKind.Ball?"sphere":"cylinder");cfg["arguments"]["target"]=id;cfg["arguments"]["revision"]=editor.ObjectRevision(id);cfg["arguments"]["definition"]["curvatureRadius"]=.065;cfg["arguments"]["definition"]["position"]["z"]=-.065;cfg["arguments"]["definition"]["width"]=.28;SurfaceRun(ex,cfg);
                var ink=SurfaceAdd();ink["arguments"]["target"]=id;ink["arguments"]["revision"]=editor.ObjectRevision(id);ink["arguments"]["red"]=.05;ink["arguments"]["blue"]=.8;ink["arguments"]["points"]=new JArray(Enumerable.Range(0,17).Select(i=>new JObject{["x"]=-.12+i*.015,["y"]=.025*Math.Sin(i*.65),["z"]=0}));SurfaceRun(ex,ink);
                var item=editor.Find(id);Assert.That(item.GetComponent<DrawingSurfaceView>().StrokeCount,Is.EqualTo(1));foreach(var t in item.GetComponentsInChildren<Transform>())t.gameObject.layer=31;index++;
            }
            yield return null;
            string output=Environment.GetEnvironmentVariable("MAESTRO_CURVED_SURFACE_PREVIEW");if(!string.IsNullOrEmpty(output)){
                var cameraObject=new GameObject("Curved ink preview camera");cameraObject.transform.SetParent(root.transform,false);var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.transform.position=new Vector3(10.3f,2.22f,.35f);camera.transform.LookAt(new Vector3(10,2,1));camera.orthographic=true;camera.orthographicSize=.27f;camera.nearClipPlane=.01f;camera.farClipPlane=3;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.89f,.87f,.81f);
                var target=new RenderTexture(1000,700,24){antiAliasing=4};var pixels=new Texture2D(1000,700,TextureFormat.RGB24,false);var previous=RenderTexture.active;
                try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1000,700),0,0);pixels.Apply();Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllBytes(output,pixels.EncodeToPNG());}
                finally{camera.targetTexture=null;RenderTexture.active=previous;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(pixels);}
            }
        }
    }
}
