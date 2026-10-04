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
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        (string target,string tool,DrawingTipView view,Transform patch) EraserStage(){
            var ex=new RoomAgentExecutor(editor);SurfaceRun(ex,SurfaceConfigure());string target=editor.Identity(block);
            foreach(float x in new[]{-.03f,.03f}){var call=SurfaceAdd();call["arguments"]["points"]=new JArray(new JObject{["x"]=x,["y"]=-.04,["z"]=0},new JObject{["x"]=x,["y"]=.04,["z"]=0});SurfaceRun(ex,call);}
            var receipt=TemplateRun(ex,TemplateCall("eraser",new Vector3(2,1,0)));string tool=(string)receipt["selected"]["output"]["objectId"];var view=editor.Find(tool).GetComponent<DrawingTipView>();view.Sample(false,RoomActorRole.Control);return(target,tool,view,block.GetComponent<DrawingSurfaceView>().Surface("Front"));
        }
        void PlaceRootTip(string tool,Transform patch,float x,float z=0){var item=editor.Find(tool);item.transform.rotation=patch.rotation;item.transform.position=patch.TransformPoint(new Vector3(x,0,z))-item.transform.TransformVector(editor.Read(tool).drawingTips[0].position);}
        [UnityTest] public IEnumerator HeldEraserGesturePreviewsTwoStrokesAndPublishesOneUndo(){
            var(target,tool,view,patch)=EraserStage();int revision=editor.ObjectRevision(target);PlaceRootTip(tool,patch,-.03f);view.Sample(true,RoomActorRole.Control);PlaceRootTip(tool,patch,.03f);view.Sample(true,RoomActorRole.Control);
            Assert.That(editor.Read(target).surfaces[0].strokes.Length,Is.EqualTo(2));Assert.That(editor.ObjectRevision(target),Is.EqualTo(revision));Assert.That(patch.Cast<Transform>().Count(t=>t.gameObject.activeSelf),Is.Zero);
            var capture=root.GetComponent<SpatialDrawing>();Assert.That((string)capture.Observe()["mode"],Is.EqualTo("erase"));Assert.That((int)capture.Observe()["strokes"],Is.EqualTo(2));
            view.Sample(false,RoomActorRole.Control);Assert.That(editor.Read(target).surfaces[0].strokes,Is.Empty);Assert.That(capture.HasUnsavedStroke,Is.False);editor.Undo();Assert.That(editor.Read(target).surfaces[0].strokes.Length,Is.EqualTo(2));Assert.That(editor.Read(tool).drawingTips.Single().Mode,Is.EqualTo("erase"));editor.Redo();Assert.That(editor.Read(target).surfaces[0].strokes,Is.Empty);yield return null;
        }
        [UnityTest] public IEnumerator HeldEraserFailedSaveRequiresExplicitSharedRetryAndKeepsInkSource(){
            var(target,tool,view,patch)=EraserStage();PlaceRootTip(tool,patch,-.03f);view.Sample(true,RoomActorRole.Control);PlaceRootTip(tool,patch,.03f);view.Sample(true,RoomActorRole.Control);string pending=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(pending);view.Sample(false,RoomActorRole.Control);
            var capture=root.GetComponent<SpatialDrawing>();Assert.That(capture.HasUnsavedStroke,Is.True);
            var tray=new GameObject("Erasing recovery tray");tray.transform.SetParent(root.transform,false);tray.AddComponent<RoomToolTray>().Build(editor,root.GetComponent<RoomInteraction>());var buttons=tray.GetComponentsInChildren<PhysicalRoomAction>();Assert.That(buttons.Single(b=>b.Tool==RoomTool.Pencil).AccessibleName,Is.EqualTo("Retry erasing"));Assert.That(buttons.Single(b=>b.Tool==RoomTool.Erase).AccessibleName,Is.EqualTo("Discard erasing"));string session=capture.SessionId;Assert.That(editor.Read(target).surfaces[0].strokes.Length,Is.EqualTo(2));Directory.Delete(pending);view.Sample(true,RoomActorRole.Control);Assert.That(capture.SessionId,Is.EqualTo(session));Assert.That(editor.Read(target).surfaces[0].strokes.Length,Is.EqualTo(2));
            var ex=new RoomAgentExecutor(editor);DrawingRun(ex,new JObject{["id"]="object.drawing.resolve",["version"]=1,["arguments"]=new JObject{["operation"]="retry",["sessionId"]=session}});var result=ex.Executions.Observe()["selected"]["output"];Assert.That((string)result["mode"],Is.EqualTo("erase"));Assert.That((int)result["strokes"],Is.EqualTo(2));Assert.That((int)result["points"],Is.Zero);Assert.That(editor.Read(target).surfaces[0].strokes,Is.Empty);editor.Undo();Assert.That(editor.Read(target).surfaces[0].strokes.Length,Is.EqualTo(2));yield return null;
        }
        [UnityTest] public IEnumerator HeldEraserProgramCannotOverrideHumanAndInterruptionRetainsPreviewUntilDiscard(){
            var(target,tool,view,patch)=EraserStage();var claims=new[]{new BehaviourCatalog.Claim(target,"wholeTarget")};Assert.That(editor.Ownership.TryAcquire("human","Human",RoomActorRole.Control,claims,null,out var lease,out var error),Is.True,error);PlaceRootTip(tool,patch,-.03f);view.Sample(true,RoomActorRole.Program);Assert.That(root.GetComponent<SpatialDrawing>().IsDrawing,Is.False);lease.Dispose();view.Sample(true,RoomActorRole.Program);Assert.That(root.GetComponent<SpatialDrawing>().IsDrawing,Is.False);
            PlaceRootTip(tool,patch,-.03f,-.1f);view.Sample(true,RoomActorRole.Program);PlaceRootTip(tool,patch,-.03f);view.Sample(true,RoomActorRole.Program);var capture=root.GetComponent<SpatialDrawing>();Assert.That(capture.IsDrawing,Is.True);
            Assert.That(editor.Ownership.TryAcquire("human","Human",RoomActorRole.Control,claims,null,out lease,out error),Is.True,error);Assert.That(capture.HasUnsavedStroke,Is.True);Assert.That(editor.Read(target).surfaces[0].strokes.Length,Is.EqualTo(2));lease.Dispose();Assert.That(capture.Resolve(capture.SessionId,true,out _,out error),Is.True,error);Assert.That(patch.Cast<Transform>().Count(t=>t.gameObject.activeSelf),Is.EqualTo(2));yield return null;
        }
        [UnityTest] public IEnumerator HeldEraserSharesAtomicStrokeRemovalAndTemporaryDiscard(){
            var(target,tool,view,patch)=EraserStage();var ex=new RoomAgentExecutor(editor);var ids=editor.Read(target).surfaces[0].strokes.Select(x=>x.id).ToArray();var call=SurfaceCall("removeStrokes",new JObject{["strokes"]=new JArray(ids[0],ids[0])});Assert.That(ex.Execute(ObjectEditRequest(call),out _,out _),Is.False);Assert.That(editor.Read(target).surfaces[0].strokes.Length,Is.EqualTo(2));call=SurfaceCall("removeStrokes",new JObject{["strokes"]=new JArray(ids)});SurfaceRun(ex,call);Assert.That(ex.Execute(ObjectEditRequest(call),out _,out _),Is.False);editor.Undo();
            Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);while(editor.TemporarySavePending)yield return null;patch=editor.Find(target).GetComponent<DrawingSurfaceView>().Surface("Front");view=editor.Find(tool).GetComponent<DrawingTipView>();view.Sample(false,RoomActorRole.Control);PlaceRootTip(tool,patch,-.03f);view.Sample(true,RoomActorRole.Control);Assert.That(editor.DiscardTemporaryRoom(out _),Is.False);view.Sample(false,RoomActorRole.Control);Assert.That(editor.Read(target).surfaces[0].strokes.Length,Is.EqualTo(1));Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.Read(target).surfaces[0].strokes.Length,Is.EqualTo(2));yield return null;
        }
        [UnityTest] public IEnumerator HeldEraserRealGripReleaseUsesItsOwnWidthAndKeepsTrayPreferences(){
            var(target,tool,view,patch)=EraserStage();Assert.That(editor.ConfigureDrawing("off",Color.green,.001f,out var error),Is.True,error);var item=editor.Find(tool);var hand=Hand(1,item.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,item.Grab);PlaceRootTip(tool,patch,0);view.SendMessage("LateUpdate");Assert.That(root.GetComponent<SpatialDrawing>().IsDrawing,Is.True);
            PlaceRootTip(tool,patch,.008f);view.SendMessage("LateUpdate");manager.SelectExit((IXRSelectInteractor)hand,item.Grab);view.SendMessage("LateUpdate");Assert.That(editor.Read(target).surfaces[0].strokes.Length,Is.EqualTo(1));Assert.That(editor.DrawingMode,Is.False);Assert.That(editor.DrawingRadius,Is.EqualTo(.001f));yield return null;
        }
        [UnityTest] public IEnumerator HeldEraserObstructionClosesOnlyTheVisiblePartOfTheGesture(){
            var(target,tool,view,patch)=EraserStage();PlaceRootTip(tool,patch,-.03f);view.Sample(true,RoomActorRole.Control);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform);wall.transform.SetPositionAndRotation(patch.TransformPoint(new Vector3(.03f,0,-.005f)),patch.rotation);wall.transform.localScale=new Vector3(.03f,.12f,.002f);PlaceRootTip(tool,patch,.03f);view.Sample(true,RoomActorRole.Control);
            Assert.That(root.GetComponent<SpatialDrawing>().IsDrawing,Is.False);Assert.That(editor.Read(target).surfaces[0].strokes.Length,Is.EqualTo(1));Assert.That(editor.Read(target).surfaces[0].strokes[0].points[0].x,Is.EqualTo(.03f));editor.Undo();Assert.That(editor.Read(target).surfaces[0].strokes.Length,Is.EqualTo(2));yield return null;
        }
        [UnityTest] public IEnumerator HeldEraserPencilAndBrushDefaultsDrawThroughTheSamePhysicalTip(){
            var ex=new RoomAgentExecutor(editor);SurfaceRun(ex,SurfaceConfigure());var patch=block.GetComponent<DrawingSurfaceView>().Surface("Front");string target=editor.Identity(block);
            foreach(string id in new[]{"pencil","brush"}){var receipt=TemplateRun(ex,TemplateCall(id,new Vector3(2,1,0)));string tool=(string)receipt["selected"]["output"]["objectId"];var view=editor.Find(tool).GetComponent<DrawingTipView>();view.Sample(false,RoomActorRole.Control);PlaceRootTip(tool,patch,-.03f);view.Sample(true,RoomActorRole.Control);PlaceRootTip(tool,patch,.03f);view.Sample(true,RoomActorRole.Control);view.Sample(false,RoomActorRole.Control);Assert.That(editor.Read(target).surfaces[0].strokes.Last().radius,Is.EqualTo(editor.Read(tool).drawingTips[0].radius));editor.Find(tool).transform.position=new Vector3(3,1,0);patch=block.GetComponent<DrawingSurfaceView>().Surface("Front");}
            Assert.That(editor.Read(target).surfaces[0].strokes.Length,Is.EqualTo(2));yield return null;
        }
    }
}
