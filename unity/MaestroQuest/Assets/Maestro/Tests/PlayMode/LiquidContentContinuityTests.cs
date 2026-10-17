// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        static void MovingLiquidBody(Rigidbody body) {
            body.isKinematic=false;body.useGravity=false;body.detectCollisions=false;
            body.linearDamping=0;body.angularDamping=0;body.linearVelocity=Vector3.right;body.angularVelocity=Vector3.up;
        }
        static void AssertLiquidMomentum(Rigidbody body) {
            Assert.IsFalse(body.isKinematic);Assert.IsFalse(body.useGravity);Assert.IsFalse(body.detectCollisions);
            Assert.AreEqual(Vector3.right,body.linearVelocity);Assert.AreEqual(Vector3.up,body.angularVelocity);
        }
        [UnityTest] public IEnumerator LiquidContentTransferAndHistoryKeepCurrentPoseAndMomentum() {
            var(_,from,to)=Containers();var item=editor.Find(from);var body=item.GetComponent<Rigidbody>();MovingLiquidBody(body);
            var pose=item.transform.position+Vector3.right*.4f;item.transform.position=pose;editor.RememberPlacement(from);
            var reservation=item.GetComponent<ContainerFillView>().ReusableReservation;int editing=0;editor.Editing+=()=>editing++;
            Assert.IsTrue(editor.TransferContainer(from,editor.ObjectRevision(from),to,editor.ObjectRevision(to),50,out var moved,out var error),error);
            Assert.AreEqual(50,moved);Assert.AreEqual(pose,item.transform.position);AssertLiquidMomentum(body);
            pose+=Vector3.forward*.2f;item.transform.position=pose;editor.RememberPlacement(from);
            editor.Undo();Assert.AreEqual(pose,item.transform.position);Assert.AreEqual(pose,editor.Read(from).position);Assert.AreEqual(200,FlowAmount(from));Assert.AreEqual(0,FlowAmount(to));AssertLiquidMomentum(body);
            editor.Redo();Assert.AreEqual(pose,item.transform.position);Assert.AreEqual(150,FlowAmount(from));Assert.AreEqual(50,FlowAmount(to));AssertLiquidMomentum(body);Assert.AreEqual(0,editing);
            Assert.AreSame(reservation,item.GetComponent<ContainerFillView>().ReusableReservation);yield return null;
        }
        [UnityTest] public IEnumerator LiquidContentAutomaticFlowPublicationKeepsLiveMomentumAndLaterUndoPose() {
            var(from,to)=FixedFlowCups();physics.StartPhysics();editor.Liquids.Tick(.05f);double accepted=FlowAmount(to);Assert.Greater(accepted,0);
            var item=editor.Find(to);var body=item.GetComponent<Rigidbody>();MovingLiquidBody(body);var pose=item.transform.position+Vector3.right*.2f;item.transform.position=pose;
            int events=0;editor.ContainerPoured+=(_,_,_,_,_)=>{events++;Assert.AreEqual(pose,item.transform.position);AssertLiquidMomentum(body);};
            Assert.IsTrue(editor.Liquids.Finish(out var error),error);Assert.AreEqual(1,events);Assert.AreEqual(accepted,editor.Read(to).containers[0].amountMl);AssertLiquidMomentum(body);
            pose+=Vector3.forward;item.transform.position=pose;editor.RememberPlacement(to);editor.Undo();Assert.AreEqual(pose,item.transform.position);Assert.AreEqual(0,FlowAmount(to));AssertLiquidMomentum(body);
            editor.Redo();Assert.AreEqual(pose,item.transform.position);Assert.AreEqual(accepted,FlowAmount(to));AssertLiquidMomentum(body);yield return null;
        }
        [UnityTest] public IEnumerator LiquidContentPublicationAndHistoryPreserveAnimatingVesselPart() {
            string target=RecipeTarget();Assert.IsTrue(editor.EditContainer(target,editor.ObjectRevision(target),new(){amountMl=200},out var error),error);
            Assert.IsTrue(runtime.Scheduler.Invoke(PartCall(target,"RightUpperArm",10),Time.unscaledTime,out var animation,out error),error);
            try {
                yield return new WaitForSeconds(.1f);var recipe=editor.Find(target).GetComponent<RecipeObject>();var part=recipe.Part("RightUpperArm");var pose=part.localRotation;
                var before=editor.Read(target).containers[0];var after=before.Copy();after.amountMl=150;
                Assert.IsTrue(editor.CommitLiquidPour(new Dictionary<string,RoomContainer>{{target,before}},new Dictionary<string,RoomContainer>{{target,after}},out error),error);
                Assert.Less(Quaternion.Angle(pose,part.localRotation),.001f);editor.Undo();editor.Redo();Assert.Less(Quaternion.Angle(pose,part.localRotation),.001f);
                Assert.AreEqual("running",(string)runtime.Scheduler.Invocation(animation)["phase"]);yield return new WaitForSeconds(.1f);Assert.Greater(Quaternion.Angle(pose,part.localRotation),.1f);
            }finally{runtime.Scheduler.CancelInvocation(animation,out _);}
        }
        [UnityTest] public IEnumerator LiquidContentUndoDoesNotRematerializeRetiredVessels() {
            var(_,from,to)=Containers();string area=NativeAreaFor(to);
            Assert.IsTrue(editor.TransferContainer(from,editor.ObjectRevision(from),to,editor.ObjectRevision(to),50,out _,out var error),error);
            Assert.IsTrue(editor.RetireNativeArea(area,out error),error);Assert.IsFalse(editor.Find(to));
            editor.Undo();Assert.IsFalse(editor.Find(to));Assert.AreEqual(0,editor.Read(to).containers[0].amountMl);
            editor.Redo();Assert.IsFalse(editor.Find(to));Assert.AreEqual(50,editor.Read(to).containers[0].amountMl);yield return null;
        }
    }
}
