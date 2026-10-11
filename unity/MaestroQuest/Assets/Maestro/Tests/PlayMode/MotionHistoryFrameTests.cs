// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    [DefaultExecutionOrder(200)] public sealed class MotionHistoryHolderMotion:MonoBehaviour
    {
        Transform holder;Vector3 start;Quaternion rotation;float began;
        public void Initialize(Transform value){holder=value;start=value.localPosition;rotation=value.localRotation;began=Time.unscaledTime;}
        public void Sample(){float at=Time.unscaledTime-began;holder.SetLocalPositionAndRotation(start+Vector3.right*at,rotation*Quaternion.AngleAxis(at*30,Vector3.forward));}
        void LateUpdate()=>Sample();
    }

    public sealed partial class AvatarPropTests
    {
        // Test harness only: transfer native bodies along with the fixture root.
        // The production world-movement owner still needs its own atomic transfer.
        void RelocatePropRoom(Vector3 translation,float yaw)
        {
            Physics.SyncTransforms();var oldFrame=editor.Frame;var bodies=root.GetComponentsInChildren<Rigidbody>();
            var poses=bodies.Select(b=>(b,position:oldFrame.PointToRoom(b.position),rotation:oldFrame.RotationToRoom(b.rotation),
                velocity:b.isKinematic?Vector3.zero:oldFrame.VectorToRoom(b.linearVelocity),spin:b.isKinematic?Vector3.zero:oldFrame.DirectionToRoom(b.angularVelocity))).ToArray();
            var viewer=editor.Viewer;var eye=viewer?viewer.position:Vector3.zero;var look=viewer?viewer.rotation:Quaternion.identity;
            root.transform.SetPositionAndRotation(root.transform.position+translation,Quaternion.AngleAxis(yaw,Vector3.up)*root.transform.rotation);
            var frame=editor.Frame;
            foreach(var pose in poses){pose.b.position=frame.PointToWorld(pose.position);pose.b.rotation=frame.RotationToWorld(pose.rotation);
                if(!pose.b.isKinematic){pose.b.linearVelocity=frame.VectorToWorld(pose.velocity);pose.b.angularVelocity=frame.DirectionToWorld(pose.spin);}}
            if(viewer)viewer.SetPositionAndRotation(eye,look);Physics.SyncTransforms();
        }
        HeldRoomProp FrameProp()
        {
            avatar.SetEditing(true);world.SetSurfaces(true,"Synthetic motion-history floor");world.StartPhysics();
            var holder=editor.Find("book");ball.transform.position=holder.transform.position+Vector3.forward*.35f;
            ball.GetComponent<RigidRoomItem>().Teleported();
            var anchor=new RoomPropAnchor("object","book","",editor.ObjectRevision("book"),"");
            var attachment=new PropAttachment(editor.Identity(ball),"",PropHand.Right,PropRelease.Throw,Vector3.forward*.35f,Quaternion.identity,1);
            var prop=HeldRoomProp.Begin(editor,attachment,anchor,30,out var error);Assert.That(prop,Is.Not.Null,error);return prop;
        }
        [UnityTest] public IEnumerator MotionHistoryFrameMovementDoesNotInventAPropThrowOrSpin()
        {
            yield return null;var prop=FrameProp();
            for(int i=0;i<5;i++){yield return new WaitForSecondsRealtime(.025f);RelocatePropRoom(new Vector3(.3f,0,-.2f),15);prop.SendMessage("LateUpdate");Assert.That(prop.Error,Is.Null);}
            prop.Finish();Assert.That(prop.Released,Is.True,prop.Error);
            var body=ball.GetComponent<Rigidbody>();Assert.That(body.linearVelocity.magnitude,Is.LessThan(.002f));Assert.That(body.angularVelocity.magnitude,Is.LessThan(.002f));
            prop.End(true);
        }
        [UnityTest] public IEnumerator MotionHistoryFrameThrowKeepsTheRealHolderMotionInTheNewWorldAxes()
        {
            yield return null;var prop=FrameProp();var holder=editor.Find("book");var movement=root.AddComponent<MotionHistoryHolderMotion>();movement.Initialize(holder.transform);
            for(int i=0;i<5;i++){yield return new WaitForSecondsRealtime(.025f);movement.Sample();
                RelocatePropRoom(new Vector3(.3f,0,-.2f),15);prop.SendMessage("LateUpdate");Assert.That(prop.Error,Is.Null);}
            prop.Finish();Assert.That(prop.Released,Is.True,prop.Error);
            Assert.That(Vector3.Distance(ball.GetComponent<Rigidbody>().linearVelocity,editor.Frame.VectorToWorld(Vector3.right)),Is.LessThan(.02f));
            Assert.That(Vector3.Distance(ball.GetComponent<Rigidbody>().angularVelocity,editor.Frame.DirectionToWorld(Vector3.forward)*(30*Mathf.Deg2Rad)),Is.LessThan(.015f));prop.End(true);
        }
        [UnityTest] public IEnumerator MotionHistoryFrameCatchTracksActualFlightAcrossRepeatedWorldMoves()
        {
            var call=CatchSetup(3);Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out var run,out var error),Is.True,error);
            var attempt=editor.Find("book").GetComponent<RoomCatch>();ThrowAtCatch(new Vector3(3,1,.35f));
            bool reset=false;
            for(int i=0;i<60&&!attempt.Caught&&runtime.Scheduler.RunningCount>0;i++){
                yield return new WaitForFixedUpdate();reset|=attempt.Reason.Contains("Motion changed");RelocatePropRoom(new Vector3(.35f,0,0),8);
            }
            Assert.That(attempt.Caught,Is.True,attempt.Reason+"; "+runtime.Scheduler.Invocation(run));
            Assert.That(reset,Is.False,"A rigid world relocation must not discard valid incoming-flight samples");
            Assert.That(attempt.Error,Is.Null);runtime.Scheduler.CancelInvocation(run,out _);
        }
        [UnityTest] public IEnumerator MotionHistoryFrameChangesRefusePropReleaseAndDoNotRearmCatch()
        {
            yield return null;var prop=FrameProp();yield return new WaitForSecondsRealtime(.04f);
            root.transform.localScale=Vector3.one*1.1f;prop.Finish();Assert.That(prop.Released,Is.False);Assert.That(prop.Error,Does.Contain("motion frame"));
            root.transform.localScale=Vector3.one;prop.Finish();Assert.That(prop.Released,Is.False,"Restoring the frame must not resume a failed release");prop.End(true);yield return null;
            var call=CatchSetup();Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out var run,out var error),Is.True,error);
            root.transform.rotation=Quaternion.Euler(10,0,0);var attempt=editor.Find("book").GetComponent<RoomCatch>();attempt.SendMessage("Update");
            Assert.That(attempt.Error,Does.Contain("motion frame"));root.transform.rotation=Quaternion.identity;yield return null;yield return null;
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That((string)runtime.Scheduler.Invocation(run)["phase"],Is.EqualTo("failed"));
        }
        [UnityTest] public IEnumerator MotionHistoryFrameCatchReachUsesTheSelectedRealCollisionPolicy()
        {
            CatchSetup();avatar.SetEditing(true);var rig=avatar.PoseRig;var upper=rig.Bone(PoseJoint.RightUpperArm);
            var goal=upper.position+Vector3.forward*.3f+Vector3.down*.14f;var reach=AvatarCatchReach.Begin(rig,false);
            var clearance=new CatchClearance();var holder=editor.Find("maestro");
            Assert.That(reach.Apply(goal,ball,holder,editor.Viewer,clearance,.1f,world),Is.True);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform,false);wall.transform.position=upper.position;
            wall.transform.localScale=Vector3.one;wall.layer=RoomPhysicsLayers.Scanned;Physics.SyncTransforms();
            Assert.That(reach.Apply(goal,ball,holder,editor.Viewer,clearance,.1f,world),Is.False);
            Assert.That(world.SetEnvironment((string)world.ObserveEnvironment()["stateId"],false,out _,out var error),Is.True,error);
            Assert.That(reach.Apply(goal,ball,holder,editor.Viewer,clearance,.1f,world),Is.True,"An excluded physical wall cannot block the arm solve");
            wall.layer=RoomPhysicsLayers.Environment;Assert.That(reach.Apply(goal,ball,holder,editor.Viewer,clearance,.1f,world),Is.False,"Virtual objects remain obstacles");
            wall.layer=RoomPhysicsLayers.Controller;Assert.That(reach.Apply(goal,ball,holder,editor.Viewer,clearance,.1f,world),Is.False,"Tracked controllers remain obstacles");
            reach.End();yield return null;
        }
        [UnityTest] public IEnumerator MotionHistoryFrameCatchReachKeepsItsPoseWhenTheWholeWorldTurns()
        {
            CatchSetup();avatar.SetEditing(true);var rig=avatar.PoseRig;var upper=rig.Bone(PoseJoint.RightUpperArm);var lower=rig.Bone(PoseJoint.RightLowerArm);
            var goal=upper.position+Vector3.forward*.3f+Vector3.down*.14f;var localGoal=editor.Frame.PointToRoom(goal);
            var reach=AvatarCatchReach.Begin(rig,false);Assert.That(reach,Is.Not.Null);var clearance=new CatchClearance();var holder=editor.Find("maestro");
            for(int i=0;i<4;i++)Assert.That(reach.Apply(goal,ball,holder,editor.Viewer,clearance,.1f),Is.True);
            var beforeUpper=Quaternion.Inverse(holder.transform.rotation)*upper.rotation;var beforeLower=Quaternion.Inverse(holder.transform.rotation)*lower.rotation;
            RelocatePropRoom(new Vector3(2,0,0),90);
            Assert.That(reach.Apply(editor.Frame.PointToWorld(localGoal),ball,holder,editor.Viewer,clearance,.02f),Is.True);
            Assert.That(Quaternion.Angle(beforeUpper,Quaternion.Inverse(holder.transform.rotation)*upper.rotation),Is.LessThan(1));
            Assert.That(Quaternion.Angle(beforeLower,Quaternion.Inverse(holder.transform.rotation)*lower.rotation),Is.LessThan(1));reach.End();yield return null;
        }
    }
}
