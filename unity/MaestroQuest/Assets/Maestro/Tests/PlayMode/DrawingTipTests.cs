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
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        (string id,DrawingTipView view,Transform patch) DrawingTipStage()
        {
            var ex=new RoomAgentExecutor(editor);SurfaceRun(ex,SurfaceConfigure());var receipt=TemplateRun(ex,TemplateCall("chalk",new Vector3(2,1,0)));string id=(string)receipt["selected"]["output"]["objectId"];
            var view=editor.Find(id).GetComponent<DrawingTipView>();view.Sample(false,RoomActorRole.Control);return(id,view,block.GetComponent<DrawingSurfaceView>().Surface("Front"));
        }
        void PlaceTip(string id,Transform patch,float x,float z=0)
        {
            var item=editor.Find(id);var tip=editor.Read(id).drawingTips[0];var anchor=item.GetComponent<RecipeObject>().Part(tip.part);
            item.transform.rotation=patch.rotation;Vector3 local=item.transform.InverseTransformPoint(anchor.TransformPoint(tip.position));item.transform.position=patch.TransformPoint(new Vector3(x,0,z))-item.transform.TransformVector(local);
        }
        JObject TipCall(string id,bool remove=false)=>new(){["id"]="object.drawingTip.edit",["version"]=1,["arguments"]=remove?new JObject{["operation"]="remove",["target"]=id,["revision"]=editor.ObjectRevision(id)}:new JObject{["operation"]="configure",["target"]=id,["revision"]=editor.ObjectRevision(id),["definition"]=DrawingTipCapability.Definition(editor.Read(id).drawingTips[0])}};
        [UnityTest] public IEnumerator HeldTipUsesSavedInkAndOneUndoWithoutChangingTrayPreferences()
        {
            var (id,view,patch)=DrawingTipStage();var pencil=root.AddComponent<SpatialDrawing>();pencil.Editor=editor;Assert.That(editor.ConfigureDrawing("off",Color.magenta,.009f,out var error),Is.True,error);
            PlaceTip(id,patch,-.03f);view.Sample(false,RoomActorRole.Control);Assert.That(pencil.IsDrawing,Is.False);view.Sample(true,RoomActorRole.Control);Assert.That(pencil.IsToolDrawing(id),Is.True);
            PlaceTip(id,patch,.03f);view.Sample(true,RoomActorRole.Control);PlaceTip(id,patch,.03f,-.06f);view.Sample(true,RoomActorRole.Control);
            var ink=editor.Read(editor.Identity(block)).surfaces[0].strokes.Single();Assert.That(ink.color,Is.EqualTo(Color.white));Assert.That(ink.radius,Is.EqualTo(.004f));Assert.That(ink.points.Length,Is.EqualTo(2));Assert.That(editor.DrawingMode,Is.False);Assert.That(editor.Paint,Is.EqualTo(Color.magenta));
            editor.Undo();Assert.That(editor.Read(editor.Identity(block)).surfaces[0].strokes,Is.Empty);Assert.That(editor.Read(id).drawingTips.Single().enabled,Is.True);editor.Redo();Assert.That(editor.Read(editor.Identity(block)).surfaces[0].strokes.Length,Is.EqualTo(1));yield return null;
        }
        [UnityTest] public IEnumerator TipSaveFailureRetainsOneDraftAndDoesNotAutomaticallyReplay()
        {
            var (id,view,patch)=DrawingTipStage();PlaceTip(id,patch,-.03f);view.Sample(true,RoomActorRole.Control);PlaceTip(id,patch,.03f);view.Sample(true,RoomActorRole.Control);
            string blocked=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(blocked);PlaceTip(id,patch,.03f,-.1f);view.Sample(true,RoomActorRole.Control);var capture=root.GetComponent<SpatialDrawing>();Assert.That(capture.HasUnsavedStroke,Is.True);string session=capture.SessionId;
            Directory.Delete(blocked);PlaceTip(id,patch,-.03f);view.Sample(true,RoomActorRole.Control);PlaceTip(id,patch,.03f);view.Sample(true,RoomActorRole.Control);Assert.That(capture.SessionId,Is.EqualTo(session));Assert.That(editor.Read(editor.Identity(block)).surfaces[0].strokes,Is.Empty);
            Assert.That(capture.Resolve(session,false,out _,out var error),Is.True,error);view.Sample(true,RoomActorRole.Control);Assert.That(capture.IsDrawing,Is.False);Assert.That(editor.Read(editor.Identity(block)).surfaces[0].strokes.Length,Is.EqualTo(1));yield return null;
        }
        [UnityTest] public IEnumerator ProgramTipCannotTakeOverHumanSurfaceControlAndMustSeparateAfterBlocking()
        {
            var (id,view,patch)=DrawingTipStage();var claims=new[]{new BehaviourCatalog.Claim(editor.Identity(block),"wholeTarget")};Assert.That(editor.Ownership.TryAcquire("human","Human edit",RoomActorRole.Control,claims,null,out var lease,out var error),Is.True,error);
            PlaceTip(id,patch,-.03f);view.Sample(true,RoomActorRole.Program);var capture=root.GetComponent<SpatialDrawing>();Assert.That(capture.IsDrawing,Is.False);lease.Dispose();view.Sample(true,RoomActorRole.Program);Assert.That(capture.IsDrawing,Is.False);
            PlaceTip(id,patch,-.03f,-.1f);view.Sample(true,RoomActorRole.Program);PlaceTip(id,patch,-.03f);view.Sample(true,RoomActorRole.Program);Assert.That(capture.IsToolDrawing(id),Is.True);PlaceTip(id,patch,.03f);view.Sample(true,RoomActorRole.Program);
            Assert.That(editor.Ownership.TryAcquire("human","Human edit",RoomActorRole.Control,claims,null,out lease,out error),Is.True,error);Assert.That(capture.HasUnsavedStroke,Is.True);view.Sample(true,RoomActorRole.Program);Assert.That(capture.IsDrawing,Is.False);lease.Dispose();capture.ResolveManual(true);view.Sample(true,RoomActorRole.Program);Assert.That(capture.IsDrawing,Is.False);yield return null;
        }
        [UnityTest] public IEnumerator SharedTipEditingIsRevisionBoundUndoableAndTemporaryWithBoundedReadback()
        {
            var (id,view,patch)=DrawingTipStage();var ex=new RoomAgentExecutor(editor);var call=TipCall(id);call["arguments"]["definition"]["enabled"]=false;SurfaceRun(ex,call);Assert.That(editor.Read(id).drawingTips[0].enabled,Is.False);Assert.That(ex.Execute(ObjectEditRequest(call),out _,out _),Is.False);
            Assert.That(BehaviourCatalog.TryRead("object.drawingTip",1,new JObject{["target"]=id},new BehaviourCatalog.FactContext(editor:editor),out var fact),Is.True);Assert.That(fact.Characters,Is.LessThanOrEqualTo(1024));Assert.That((bool)((JObject)fact.Value)["configured"],Is.True);
            Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);while(editor.TemporarySavePending)yield return null;SurfaceRun(ex,TipCall(id,true));Assert.That(editor.Read(id).drawingTips,Is.Empty);Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.Read(id).drawingTips.Single().enabled,Is.False);
            SurfaceRun(ex,TipCall(id,true));editor.Undo();Assert.That(editor.Read(id).drawingTips.Single().enabled,Is.False);yield return null;
        }
        [UnityTest] public IEnumerator RealControllerGripActivatesTheComponentAndReleaseClosesTheStroke()
        {
            var (id,view,patch)=DrawingTipStage();var item=editor.Find(id);var hand=Hand(1,item.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,item.Grab);Assert.That(item.Grab.isSelected,Is.True);
            PlaceTip(id,patch,-.03f);view.SendMessage("LateUpdate");Assert.That(root.GetComponent<SpatialDrawing>().IsToolDrawing(id),Is.True);PlaceTip(id,patch,.03f);view.SendMessage("LateUpdate");manager.SelectExit((IXRSelectInteractor)hand,item.Grab);view.SendMessage("LateUpdate");Assert.That(editor.Read(editor.Identity(block)).surfaces[0].strokes.Length,Is.EqualTo(1));yield return null;
        }
        [UnityTest] public IEnumerator UndoCannotRemoveAToolWhileItHasAnActiveOrRetainedStroke()
        {
            var (id,view,patch)=DrawingTipStage();PlaceTip(id,patch,-.03f);view.Sample(true,RoomActorRole.Control);PlaceTip(id,patch,.03f);view.Sample(true,RoomActorRole.Control);var capture=root.GetComponent<SpatialDrawing>();
            editor.Undo();Assert.That(editor.Read(id),Is.Not.Null);Assert.That(editor.Read(editor.Identity(block)).surfaces[0].strokes,Is.Empty);Assert.That(capture.IsDrawing||capture.HasUnsavedStroke,Is.True);
            capture.InterruptTool(id);Assert.That(capture.HasUnsavedStroke,Is.True);editor.Undo();Assert.That(editor.Read(id),Is.Not.Null);capture.ResolveManual(true);editor.Undo();Assert.That(editor.Read(id),Is.Null);yield return null;
        }
        [UnityTest] public IEnumerator DisablingAToolRetainsInkWithoutSavingInsideObjectTeardown()
        {
            var (id,view,patch)=DrawingTipStage();PlaceTip(id,patch,-.03f);view.Sample(true,RoomActorRole.Control);PlaceTip(id,patch,.03f);view.Sample(true,RoomActorRole.Control);int revision=editor.Revision;view.enabled=false;
            var capture=root.GetComponent<SpatialDrawing>();Assert.That(capture.HasUnsavedStroke,Is.True);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(editor.Read(editor.Identity(block)).surfaces[0].strokes,Is.Empty);Assert.That(capture.Resolve(capture.SessionId,false,out _,out var error),Is.True,error);Assert.That(editor.Read(editor.Identity(block)).surfaces[0].strokes.Length,Is.EqualTo(1));yield return null;
        }
        [UnityTest] public IEnumerator NativeHeldPropActivatesTheSameTipWithoutControllerOrTrayMode()
        {
            var (id,view,patch)=DrawingTipStage();var holder=editor.Find("book");holder.transform.position=new Vector3(3,1,0);var item=editor.Find(id);item.transform.position=holder.transform.position+Vector3.forward*.35f;
            var attachment=new Maestro.Quest.Avatar.PropAttachment(id,"",Maestro.Quest.Rules.PropHand.Right,Maestro.Quest.Rules.PropRelease.Return,Vector3.forward*.35f,Quaternion.identity,1);
            var anchor=new RoomPropAnchor("object","book","",editor.ObjectRevision("book"));var held=HeldRoomProp.Begin(editor,attachment,anchor,10,out var error);Assert.That(held,Is.Not.Null,error);Assert.That(held.Holding,Is.True);
            PlaceTip(id,patch,-.03f);view.SendMessage("LateUpdate");Assert.That(root.GetComponent<SpatialDrawing>().IsToolDrawing(id),Is.True);PlaceTip(id,patch,.03f);view.SendMessage("LateUpdate");held.End(false);view.SendMessage("LateUpdate");Assert.That(editor.Read(editor.Identity(block)).surfaces[0].strokes.Length,Is.EqualTo(1));Assert.That(editor.DrawingMode,Is.False);yield return null;
        }
        [UnityTest] public IEnumerator SuspendedTipRequiresSeparationAndNeverDrawsOnItsOwnSurface()
        {
            var (id,view,patch)=DrawingTipStage();PlaceTip(id,patch,-.03f);view.Sample(true,RoomActorRole.Control);PlaceTip(id,patch,.03f);view.Sample(true,RoomActorRole.Control);editor.Ownership.Suspend(true);view.Sample(true,RoomActorRole.Control);var capture=root.GetComponent<SpatialDrawing>();Assert.That(capture.HasUnsavedStroke,Is.True);capture.ResolveManual(true);editor.Ownership.Suspend(false);view.Sample(true,RoomActorRole.Control);Assert.That(capture.IsDrawing,Is.False);
            PlaceTip(id,patch,-.03f,-.1f);view.Sample(true,RoomActorRole.Control);PlaceTip(id,patch,-.03f);view.Sample(true,RoomActorRole.Control);Assert.That(capture.IsDrawing,Is.True);capture.EndTool(id);
            var ray=new Ray(patch.TransformPoint(new Vector3(0,0,-.01f)),patch.forward);Assert.That(editor.FindDrawingSurface(ray,.02f,out _,out _,out _,out _,.003f,editor.Identity(block)),Is.False);yield return null;
        }
    }
}
