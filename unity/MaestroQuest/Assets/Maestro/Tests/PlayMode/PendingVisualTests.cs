// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
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
        [UnityTest] public IEnumerator PendingVisualQueueCapturesInputAndPublishesOnlyAfterItsTurn() {
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation layer=null,light=null;
            try {
                yield return Prepared(blocker);var context=new CapabilityContext(editor,animations);var module=new VisibilityLayerSaveCapability();var args=module.Example;
                Assert.AreEqual("completion",module.Duration);Assert.IsTrue(module.Start(context,"view",args,out layer,out error),error);args["name"]="Changed after request";
                var lighting=new WorldLightingCapability();var settings=lighting.Example;settings["revision"]=editor.LightingRevision;
                Assert.IsTrue(lighting.Start(context,"light",settings,out light,out error),error);settings["settings"]["sunIntensity"]=0;
                Assert.IsEmpty(editor.VisibilityLayers());Assert.IsFalse(editor.Lighting.enabled);Assert.IsEmpty(layer.Result);Assert.IsEmpty(light.Result);
                blocker.Dispose();yield return Drained(blocker);Assert.AreEqual(RuleActionState.Preparing,light.State(out error),error);Assert.IsFalse(editor.Lighting.enabled);
                yield return PlacementOperationDone(layer);yield return PlacementOperationDone(light);
                string id=(string)layer.Result["id"];Assert.AreEqual("Distant landscape",editor.ReadVisibility(id).name);Assert.IsTrue(editor.Lighting.enabled);Assert.AreNotEqual(0,editor.Lighting.sunIntensity);
                var disk=new RoomStorage(directory).Load(out error);Assert.AreEqual(id,disk.visibilityLayers.Single().id);Assert.IsTrue(disk.lighting.Same(editor.Lighting));
            }finally{layer?.Stop(false);light?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingVisualBindingPreservesMovingTargetAndUndoRedoMomentum() {
            string id=NewVisualLayer(),target=editor.Identity(block);var body=block.GetComponent<Rigidbody>();
            Assert.IsTrue(editor.BeginVisibilityBinding(target,editor.ObjectRevision(target),id,editor.VisibilityRevision(id),out var pending,out var error),error);
            using(pending) {
                yield return Prepared(pending);var position=block.transform.position+Vector3.right*.35f;block.transform.position=position;editor.RememberPlacement(target);
                body.isKinematic=false;body.useGravity=false;body.detectCollisions=false;body.linearDamping=0;body.angularDamping=0;body.linearVelocity=Vector3.right;body.angularVelocity=Vector3.up;
                float started=Time.fixedTime;yield return Completed(pending);
                Assert.Less(Vector3.Distance(block.transform.position,position+Vector3.right*(Time.fixedTime-started)),.01f);Assert.AreEqual(Vector3.right,body.linearVelocity);Assert.AreEqual(Vector3.up,body.angularVelocity);Assert.IsFalse(body.isKinematic);
                position=block.transform.position;editor.RememberPlacement(target);Assert.AreEqual(id,editor.Read(target).visibilityLayer);Assert.IsTrue(editor.HasUnsavedChanges);
                editor.Undo();Assert.AreEqual(position,block.transform.position);Assert.AreEqual(position,editor.Read(target).position);Assert.AreEqual("",editor.Read(target).visibilityLayer);Assert.AreEqual(Vector3.right,body.linearVelocity);
                editor.Redo();Assert.AreEqual(position,block.transform.position);Assert.AreEqual(position,editor.Read(target).position);Assert.AreEqual(id,editor.Read(target).visibilityLayer);Assert.AreEqual(Vector3.up,body.angularVelocity);
                Assert.IsTrue(pending.Complete(out error),error);Assert.AreEqual(id,editor.Read(target).visibilityLayer);
            }
        }
        [UnityTest] public IEnumerator PendingVisualBindingDoesNotResetAnAnimatingPartAtAcceptance() {
            string target=RecipeTarget(),id=NewVisualLayer();
            Assert.IsTrue(runtime.Scheduler.Invoke(PartCall(target,"RightUpperArm",10),Time.unscaledTime,out var animation,out var error),error);
            yield return new WaitForSeconds(.1f);
            Assert.IsTrue(editor.BeginVisibilityBinding(target,editor.ObjectRevision(target),id,editor.VisibilityRevision(id),out var pending,out error),error);
            using(pending) {
                yield return Prepared(pending);var recipe=editor.Find(target).GetComponent<RecipeObject>();var part=recipe.Part("RightUpperArm");var before=part.localRotation;
                Assert.IsTrue(pending.Poll(out _,out error),error);Assert.IsTrue(pending.Accepted);Assert.Less(Quaternion.Angle(before,part.localRotation),.001f);
                yield return Completed(pending);Assert.AreEqual("running",(string)runtime.Scheduler.Invocation(animation)["phase"]);
                before=part.localRotation;editor.Undo();editor.Redo();Assert.Less(Quaternion.Angle(before,part.localRotation),.001f);Assert.AreEqual("running",(string)runtime.Scheduler.Invocation(animation)["phase"]);
                yield return new WaitForSeconds(.1f);Assert.Greater(Quaternion.Angle(before,part.localRotation),.1f);
            }
            runtime.Scheduler.CancelInvocation(animation,out _);
        }
        [UnityTest] public IEnumerator PendingVisualSharedLayerKeepsMovingMembersAndClockWhileSaving() {
            string id=NewVisualLayer(),target=editor.Identity(block);Assert.IsTrue(AssignVisualLayer(target,id,out var error),error);
            Assert.IsTrue(editor.ConfigureWorldTime(editor.WorldTimeRevision,new WorldTimeSettings{running=true},out error),error);
            var value=editor.ReadVisibility(id);value.opacity=.8f;
            Assert.IsTrue(editor.BeginVisibility(value,id,editor.VisibilityRevision(id),new[]{target},out var pending,out error),error);
            using(pending){yield return Prepared(pending);var position=block.transform.position+Vector3.up*.1f;block.transform.position=position;editor.RememberPlacement(target);editor.TickWorldTime(.2);editor.TickWorldTime(.2);double second=editor.WorldSecond;
                yield return Completed(pending);Assert.AreEqual(position,block.transform.position);Assert.AreEqual(.8f,editor.ReadVisibility(id).opacity);Assert.GreaterOrEqual(editor.WorldSecond,second);Assert.IsTrue(editor.HasUnsavedChanges);}
        }
        [UnityTest] public IEnumerator PendingVisualCancellationAndAuthoredChangeNeverPublishPreparedBinding() {
            string id=NewVisualLayer(),target=editor.Identity(block);
            foreach(bool cancel in new[]{true,false}) {
                Assert.IsTrue(editor.BeginVisibilityBinding(target,editor.ObjectRevision(target),id,editor.VisibilityRevision(id),out var pending,out var error),error);
                using(pending){yield return Prepared(pending);if(cancel)pending.Dispose();else {Assert.IsTrue(editor.PaintObject(target,Color.red,out error),error);yield return Rejected(pending);}yield return Drained(pending);Assert.IsFalse(pending.Accepted);Assert.AreEqual("",editor.Read(target).visibilityLayer);Assert.AreEqual("",new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==target).visibilityLayer);}
            }
        }
        [UnityTest] public IEnumerator PendingVisualNoopReceiptsRemainExactAfterLaterHumanEdits() {
            var context=new CapabilityContext(editor,animations);var module=new WorldLightingCapability();var args=module.Example;args["revision"]=editor.LightingRevision;args["settings"]=JObject.Parse(JsonUtility.ToJson(editor.Lighting));int original=editor.LightingRevision;
            Assert.IsTrue(module.Start(context,"same-light",args,out var light,out var error),error);
            try{yield return PendingGroupReady(light);Assert.IsTrue(editor.SetLighting(editor.LightingRevision,new(){enabled=true},out error),error);Assert.IsTrue(light.Complete(out error),error);Assert.AreEqual(original,(int)light.Result["revision"]);Assert.IsFalse((bool)light.Result["settings"]["enabled"]);light.Result["settings"]["enabled"]=true;Assert.IsFalse((bool)light.Result["settings"]["enabled"]);}finally{light.Stop(false);}
            string id=NewVisualLayer(),target=editor.Identity(block);Assert.IsTrue(AssignVisualLayer(target,id,out error),error);int revision=editor.ObjectRevision(target);
            var assign=new VisibilityAssignCapability();args=(JObject)LayerCall(target,id)["arguments"];
            Assert.IsTrue(assign.Start(context,"same-binding",args,out var binding,out error),error);
            try{yield return PendingGroupReady(binding);Assert.IsTrue(editor.PaintObject(target,Color.blue,out error),error);Assert.IsTrue(binding.Complete(out error),error);Assert.AreEqual(revision,(int)binding.Result["revision"]);Assert.AreNotEqual(revision,editor.ObjectRevision(target));}finally{binding.Stop(false);}
        }
        [UnityTest] public IEnumerator PendingVisualTemporaryKeepDiscardAndRemovalUseOneSharedHistory() {
            var context=new CapabilityContext(editor,animations);var cap=new VisibilityLayerSaveCapability();
            foreach(bool keep in new[]{false,true}) {
                Assert.IsTrue(editor.BeginTemporaryRoom(out var error),error);yield return PendingGroupTemporaryDone();
                Assert.IsTrue(cap.Start(context,"temp-view",cap.Example,out var operation,out error),error);string id;
                try{yield return PlacementOperationDone(operation);id=(string)operation.Result["id"];Assert.IsTrue((bool)operation.Result["temporary"]);Assert.IsFalse(new RoomStorage(directory).Load(out error).visibilityLayers.Any(x=>x.id==id));}finally{operation.Stop(false);}
                if(keep)Assert.IsTrue(editor.KeepTemporaryRoom(out error),error);else Assert.IsTrue(editor.DiscardTemporaryRoom(out error),error);yield return PendingGroupTemporaryDone();
                Assert.AreEqual(keep,editor.ReadVisibility(id)!=null);Assert.AreEqual(keep,new RoomStorage(directory).Load(out error).visibilityLayers.Any(x=>x.id==id));
                if(!keep)continue;
                Assert.IsTrue(new VisibilityLayerRemoveCapability().Start(context,"remove",new(){["id"]=id,["revision"]=editor.VisibilityRevision(id)},out operation,out error),error);
                try{yield return PlacementOperationDone(operation);Assert.AreEqual(0,(int)operation.Result["revision"]);Assert.IsNull(editor.ReadVisibility(id));editor.Undo();Assert.IsNotNull(editor.ReadVisibility(id));}finally{operation.Stop(false);}
            }
        }
        [UnityTest] public IEnumerator PendingVisualWaitingBindingRefusesStaleTargetWithoutChangingIt() {
            string id=NewVisualLayer(),target=editor.Identity(block);Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation operation=null;
            try{yield return Prepared(blocker);Assert.IsTrue(new VisibilityAssignCapability().Start(new CapabilityContext(editor,animations),"waiting-view",(JObject)LayerCall(target,id)["arguments"],out operation,out error),error);
                Assert.IsTrue(editor.PaintObject(target,Color.blue,out error),error);blocker.Dispose();yield return Drained(blocker);Assert.AreEqual(RuleActionState.Failed,operation.State(out error));Assert.IsEmpty(operation.Result);Assert.AreEqual("",editor.Read(target).visibilityLayer);
            }finally{operation?.Stop(false);blocker.Dispose();}
        }
    }
}
