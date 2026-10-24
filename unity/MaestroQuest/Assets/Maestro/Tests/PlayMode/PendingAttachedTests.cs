// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        static readonly string[] AttachedActions={"object.drawingTip.edit","object.sculptTip.edit","object.surface.edit","object.light.edit","object.window.edit"};
        JObject AttachedArgs(string action){
            var a=(JObject)BehaviourCatalog.Action(action).Module.Example.DeepClone();
            a["target"]=editor.Identity(block);a["revision"]=editor.ObjectRevision(editor.Identity(block));
            if(action=="object.window.edit")a["surface"]="Front";
            if(action=="object.drawingTip.edit"||action=="object.sculptTip.edit")a["definition"]["enabled"]=false;
            return a;
        }
        bool BeginAttachedTest(string action,JObject a,out RoomEditor.PendingPersistedEdit pending,out string error,Action<string> checkpoint=null){
            string target=(string)a["target"];int revision=(int)a["revision"];
            switch(action){
                case "object.drawingTip.edit":return editor.BeginDrawingTip(target,revision,JsonUtility.FromJson<DrawingTip>(a["definition"].ToString()),out pending,out error,checkpoint);
                case "object.sculptTip.edit":return editor.BeginSculptTip(target,revision,JsonUtility.FromJson<SculptTip>(a["definition"].ToString()),out pending,out error,checkpoint);
                case "object.surface.edit":return editor.BeginSurfaceEdit(target,revision,a,out _,out pending,out error,checkpoint);
                case "object.light.edit":return editor.BeginLightEdit(target,revision,a,out pending,out error,checkpoint);
                default:return editor.BeginWindowEdit(target,revision,a,out pending,out error,checkpoint);
            }
        }
        [UnityTest] public IEnumerator PendingAttachedQueueCapturesAllFiveRequestsWithoutPublishingEarly(){
            SurfaceFixture(SurfaceConfigure());string target=editor.Identity(block);
            foreach(string action in AttachedActions){
                var a=AttachedArgs(action);var module=BehaviourCatalog.Action(action).Module;
                Assert.AreEqual("completion",module.Duration);string before=JsonUtility.ToJson(editor.Read(target));
                Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation operation=null;
                try{
                    yield return Prepared(blocker);Assert.IsTrue(module.Start(new CapabilityContext(editor,animations),action,a,out operation,out error),error);
                    Assert.AreEqual(RuleActionState.Preparing,operation.State(out error),error);Assert.IsEmpty(operation.Result);
                    Assert.AreEqual(before,JsonUtility.ToJson(editor.Read(target)));
                    // The caller may reuse or replace its JSON while this request waits.
                    a.RemoveAll();blocker.Dispose();yield return Drained(blocker);yield return GeometryReady(operation);
                    int accepted=editor.ObjectRevision(target);Assert.Greater(accepted,0);
                    var data=editor.Read(target);var saved=new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==target);
                    switch(action){
                        case "object.drawingTip.edit":Assert.IsFalse(data.drawingTips.Single().enabled);Assert.AreEqual(data.drawingTips.Single().radius,saved.drawingTips.Single().radius);break;
                        case "object.sculptTip.edit":Assert.IsFalse(data.sculptTips.Single().enabled);Assert.AreEqual(data.sculptTips.Single().mode,saved.sculptTips.Single().mode);break;
                        case "object.surface.edit":Assert.IsNotNull(block.GetComponent<DrawingSurfaceView>().Surface("Front"));Assert.AreEqual(.12f,saved.surfaces.Single().width);break;
                        case "object.light.edit":Assert.AreEqual(1,saved.lights.Length);Assert.IsTrue(block.GetComponent<RoomLightView>().Active("Lamp"));break;
                        case "object.window.edit":Assert.AreEqual(1,saved.windows.Length);Assert.IsTrue(block.GetComponent<RoomWindowView>().Requested);Assert.IsFalse(block.GetComponent<RoomWindowView>().RenderingReady,"Desktop cannot claim headset passthrough");break;
                    }
                    Assert.IsTrue(editor.PaintObject(target,Color.green,out error),error);
                    Assert.IsTrue(operation.Complete(out error),error);
                    if(operation.Result["revision"]!=null)Assert.AreEqual(accepted,(int)operation.Result["revision"]);
                }finally{operation?.Stop(false);blocker.Dispose();}
            }
        }
        [UnityTest] public IEnumerator PendingAttachedCancellationPreservesAllFiveSourcesAndSavedFiles(){
            SurfaceFixture(SurfaceConfigure());string target=editor.Identity(block);
            foreach(string action in AttachedActions){
                var a=AttachedArgs(action);string before=JsonUtility.ToJson(editor.Read(target));byte[] bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));
                Assert.IsTrue(BeginAttachedTest(action,a,out var pending,out var error),error);
                try{yield return Prepared(pending);Assert.IsFalse(pending.Accepted);pending.Dispose();yield return Drained(pending);
                    Assert.AreEqual(before,JsonUtility.ToJson(editor.Read(target)),action);Assert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)),action);
                }finally{pending.Dispose();}
            }
        }
        [UnityTest] public IEnumerator PendingAttachedSurfaceReturnsOnlyItsAcceptedStrokeAndRevision(){
            SurfaceFixture(SurfaceConfigure());string target=editor.Identity(block);var a=(JObject)SurfaceAdd()["arguments"];
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation operation=null;
            try{
                yield return Prepared(blocker);Assert.IsTrue(BehaviourCatalog.Action("object.surface.edit").Module.Start(new CapabilityContext(editor,animations),"ink",a,out operation,out error),error);
                a["points"][0]["x"]=.05;blocker.Dispose();yield return Drained(blocker);yield return GeometryReady(operation);
                var stroke=editor.Read(target).surfaces.Single().strokes.Single();Assert.AreEqual(-.04f,stroke.points[0].x);int accepted=editor.ObjectRevision(target);
                Assert.AreEqual(stroke.id,new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==target).surfaces.Single().strokes.Single().id,error);
                Assert.IsTrue(editor.EditSurface(target,accepted,(JObject)SurfaceAdd()["arguments"],out var later,out error),error);Assert.AreNotEqual(stroke.id,later);
                Assert.IsTrue(operation.Complete(out error),error);Assert.AreEqual(stroke.id,(string)operation.Result["stroke"]);Assert.AreEqual(accepted,(int)operation.Result["revision"]);
                var copy=operation.Result;copy["stroke"]=later;Assert.AreEqual(stroke.id,(string)operation.Result["stroke"]);
            }finally{operation?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingAttachedWindowPublicationFailureRetainsUnconfirmedNativeEdit(){
            SurfaceFixture(SurfaceConfigure());string target=editor.Identity(block);var a=AttachedArgs("object.window.edit");
            byte[] bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));using var pause=new PublicationPause();
            Assert.IsTrue(editor.BeginWindowEdit(target,editor.ObjectRevision(target),a,out var pending,out var error,phase=>{if(phase=="publishing"){pause.Check(phase);throw new IOException("Injected window publication failure");}}),error);
            try{
                yield return Prepared(pending);Assert.IsTrue(pending.Poll(out var ready,out error),error);Assert.IsFalse(ready);Assert.IsTrue(pending.Accepted);Assert.IsTrue(block.GetComponent<RoomWindowView>().Requested);
                pause.Release();yield return Rejected(pending);Assert.IsFalse(pending.Published);Assert.IsFalse(pending.Complete(out _));
                Assert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));Assert.AreEqual(1,editor.Read(target).windows.Length);
                Assert.IsFalse(editor.BeginWindowEdit(target,editor.ObjectRevision(target),a,out _,out _));
            }finally{pause.Release();pending.Dispose();}
            yield return Drained(pending);
        }
        [UnityTest] public IEnumerator PendingAttachedLightStopDrainsWhilePhysicsKeepsMovingAndUndoPreservesPose(){
            string target=editor.Identity(block);Assert.IsTrue(editor.ConfigurePhysics(target,editor.ObjectRevision(target),new ObjectPhysicsSettings{mode="solid",shape="box",mass=.5f},out var error),error);
            physics.SetSurfaces(true,"Synthetic lamp continuity setup");physics.StartPhysics();var body=block.GetComponent<Rigidbody>();Assert.IsFalse(body.isKinematic);
            var a=AttachedArgs("object.light.edit");using var pause=new PublicationPause();
            Assert.IsTrue(editor.BeginLightEdit(target,editor.ObjectRevision(target),a,out var pending,out error,pause.Check),error);
            try{
                yield return Prepared(pending);body.linearVelocity=Vector3.right;body.angularVelocity=Vector3.up;
                Assert.IsTrue(pending.Poll(out var ready,out error),error);Assert.IsFalse(ready);Assert.IsTrue(pending.Accepted);
                Assert.AreEqual(Vector3.right,body.linearVelocity);Assert.AreEqual(Vector3.up,body.angularVelocity);Assert.IsFalse(body.isKinematic);
                var position=body.position;for(int i=0;i<3;i++)yield return new WaitForFixedUpdate();Assert.Greater(Vector3.Distance(position,body.position),.001f);
                pending.Dispose();StringAssert.Contains("Saving is still pending",pending.InterruptionStatus);
            }finally{pause.Release();pending.Dispose();}
            yield return Drained(pending);Assert.IsTrue(pending.Published);
            var now=body.position;var velocity=body.linearVelocity;var spin=body.angularVelocity;editor.Undo();
            Assert.AreEqual(now,body.position);Assert.AreEqual(velocity,body.linearVelocity);Assert.AreEqual(spin,body.angularVelocity);Assert.IsEmpty(editor.Read(target).lights);
        }
        [UnityTest] public IEnumerator PendingAttachedStaleWaiterCannotOverwriteNewerHumanToolSettings(){
            string target=editor.Identity(block);var a=AttachedArgs("object.sculptTip.edit");
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation operation=null;
            try{
                yield return Prepared(blocker);Assert.IsTrue(BehaviourCatalog.Action("object.sculptTip.edit").Module.Start(new CapabilityContext(editor,animations),"sculpt",a,out operation,out error),error);
                Assert.IsTrue(editor.EditSculptTip(target,editor.ObjectRevision(target),new SculptTip{mode="level",height=.2f},out error),error);
                blocker.Dispose();yield return Drained(blocker);Assert.AreEqual(RuleActionState.Failed,operation.State(out error));Assert.IsEmpty(operation.Result);
                Assert.AreEqual(.2f,editor.Read(target).sculptTips.Single().height);
            }finally{operation?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingAttachedContentAndUndoKeepCurrentPlacement(){
            SurfaceFixture(SurfaceConfigure());string target=editor.Identity(block);
            foreach(string action in AttachedActions.Take(3)){
                var a=AttachedArgs(action);if(action=="object.surface.edit")a["definition"]["width"]=.14;int revision=editor.ObjectRevision(target);Assert.IsTrue(BeginAttachedTest(action,a,out var pending,out var error),error);
                try{
                    yield return Prepared(pending);var position=block.transform.localPosition+Vector3.right*.1f;block.transform.localPosition=position;
                    yield return Completed(pending);Assert.IsTrue(pending.Complete(out error),error);Assert.Greater(editor.ObjectRevision(target),revision);Assert.AreEqual(position,block.transform.localPosition);
                    block.transform.localPosition+=Vector3.up*.1f;position=block.transform.localPosition;editor.Undo();Assert.AreEqual(position,block.transform.localPosition);
                }finally{pending.Dispose();}
                yield return Drained(pending);
            }
        }
    }
}
