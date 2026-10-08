// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
namespace Maestro.Quest.Tests
{
    public sealed class WorldMotionTests
    {
        GameObject root,content;RoomPhysicsWorld world;RoomWorldMotion motion;RoomNavigation navigation;
        Rigidbody ball;RigidRoomItem rigid;Transform tracking;float previousDelta;
        [UnitySetUp]public IEnumerator Setup() {
            previousDelta=Time.captureDeltaTime;Time.captureDeltaTime=1f/72;
            root=new GameObject("Physical world-motion test");root.AddComponent<XRInteractionManager>();RoomPhysicsLayers.Configure();
            content=new GameObject("Virtual content");content.transform.SetParent(root.transform,false);
            world=root.AddComponent<RoomPhysicsWorld>();navigation=root.AddComponent<RoomNavigation>();navigation.Initialize(world);navigation.SetVirtualFrame(content.transform);
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.transform.SetParent(content.transform,false);ground.transform.localPosition=Vector3.down*.1f;ground.transform.localScale=new Vector3(10,.2f,10);ground.layer=RoomPhysicsLayers.Environment;
            ground.AddComponent<RoomWalkableSurface>().Publish(ground.GetComponent<Collider>());
            tracking=new GameObject("Fixed physical tracking").transform;tracking.SetParent(root.transform,false);tracking.position=new Vector3(2,1.6f,2);
            ball=Body(new Vector3(0,1.5f,0),ItemPhysics.Bouncy);rigid=ball.GetComponent<RigidRoomItem>();
            motion=new RoomWorldMotion(content.transform,world);Physics.SyncTransforms();
            Assert.That(world.SetEnvironment((string)world.ObserveEnvironment()["stateId"],false,out _,out var error),Is.True,error);world.StartPhysics();yield return null;
        }
        Rigidbody Body(Vector3 at,ItemPhysics profile) {
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.transform.SetParent(content.transform,false);go.transform.localPosition=at;go.transform.localScale=Vector3.one*.15f;go.layer=RoomPhysicsLayers.Item;
            var item=go.AddComponent<RoomItem>();item.Configure(new[]{go.GetComponent<Collider>()});var component=go.AddComponent<RigidRoomItem>();component.Initialize(item);component.Configure(world,profile,.5f);return go.GetComponent<Rigidbody>();
        }
        void Shift(Vector3 at,float yaw) { Assert.That(motion.SetPose(at,Quaternion.Euler(0,yaw,0),out var error),Is.True,error); }
        [UnityTest]public IEnumerator WorldTransferKeepsVelocityGravityAndActionIdentityThroughRealPhysics() {
            Assert.That(rigid.Launch(new Vector3(1,2,.5f),new Vector3(1,2,3)),Is.True);Physics.SyncTransforms();
            var before=new RoomFrame(content.transform);var local=before.PointToRoom(ball.position);var velocity=before.VectorToRoom(ball.linearVelocity);var spin=before.DirectionToRoom(ball.angularVelocity);
            uint placement=rigid.PlacementRevision,revision=rigid.MotionRevision;var eye=tracking.position;
            Shift(new Vector3(12,0,-4),75);var after=new RoomFrame(content.transform);
            Assert.That(Vector3.Distance(ball.position,after.PointToWorld(local)),Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(ball.linearVelocity,after.VectorToWorld(velocity)),Is.LessThan(.0001f));Assert.That(Vector3.Distance(ball.angularVelocity,after.DirectionToWorld(spin)),Is.LessThan(.0001f));
            Assert.That(tracking.position,Is.EqualTo(eye));Assert.That(rigid.PlacementRevision,Is.EqualTo(placement));Assert.That(rigid.MotionRevision,Is.EqualTo(revision));
            Assert.That(world.Running&&ball.useGravity&&!ball.isKinematic,Is.True);Assert.That(ball.interpolation,Is.EqualTo(RigidbodyInterpolation.Interpolate));
            bool fell=false,bounced=false;
            for(int i=0;i<100;i++){yield return new WaitForFixedUpdate();fell|=ball.linearVelocity.y< -1;bounced|=fell&&ball.linearVelocity.y>1;Assert.That(world.Running,Is.True);Assert.That(ball.position.y,Is.GreaterThan(.025f));}
            Assert.That(fell&&bounced,Is.True,"The relocated ball must still fall and bounce on the relocated floor");
        }
        [UnityTest]public IEnumerator WorldTransferPreservesSleepingAndConnectedIslands() {
            rigid.StopVelocity();ball.useGravity=false;ball.Sleep();var frozen=Body(new Vector3(.5f,1.5f,0),ItemPhysics.Fixed);
            var joint=ball.gameObject.AddComponent<FixedJoint>();joint.connectedBody=frozen;joint.autoConfigureConnectedAnchor=true;
            Physics.SyncTransforms();ball.Sleep();var separation=frozen.position-ball.position;var q=Quaternion.Euler(0,90,0);
            Shift(new Vector3(4,0,6),90);Assert.That(ball.IsSleeping(),Is.True);Assert.That(joint.connectedBody,Is.SameAs(frozen));Assert.That(Vector3.Distance(frozen.position-ball.position,q*separation),Is.LessThan(.0001f));
            Assert.That(frozen.isKinematic,Is.True);yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();Assert.That(joint,Is.Not.Null);Assert.That((frozen.position-ball.position).magnitude,Is.EqualTo(separation.magnitude).Within(.005f));
        }
        [UnityTest]public IEnumerator PhysicalBindingsKeepExactPoseWhileOrdinaryItemsMove() {
            var ink=Body(new Vector3(1,1,1),ItemPhysics.Fixed);ink.gameObject.AddComponent<ScannedDrawingView>();var at=ink.position;var rotation=ink.rotation;
            Shift(new Vector3(-3,0,2),30);Assert.That(Vector3.Distance(ink.position,at),Is.LessThan(.0001f));Assert.That(Quaternion.Angle(ink.rotation,rotation),Is.LessThan(.001f));
            Assert.That(Vector3.Distance(ink.transform.position,at),Is.LessThan(.0001f));yield return null;
            Assert.That(Vector3.Distance(ink.position,at),Is.LessThan(.0001f));
        }
        [UnityTest]public IEnumerator BoundaryRecoveryUsesTheMovedAcceptedPose() {
            yield return new WaitForFixedUpdate();rigid.SendMessage("FixedUpdate");var accepted=new RoomFrame(content.transform).PointToRoom(ball.transform.position);
            Shift(new Vector3(15,0,4),45);var expected=content.transform.TransformPoint(accepted);
            ball.position=content.transform.TransformPoint(new Vector3(30,2,0));ball.transform.position=ball.position;Physics.SyncTransforms();rigid.SendMessage("FixedUpdate");
            Assert.That(world.Running,Is.False);Assert.That(Vector3.Distance(ball.position,expected),Is.LessThan(.001f),"Recovery must not jump back to the old physical coordinates");
        }
        [UnityTest]public IEnumerator RefusalLeavesAllBodiesAndTheFrameUnchanged() {
            var at=content.transform.position;var p=ball.position;var velocity=ball.linearVelocity;
            Assert.That(motion.SetPose(Vector3.right,Quaternion.Euler(15,0,0),out _),Is.False);Assert.That(motion.SetPose(Vector3.positiveInfinity,Quaternion.identity,out _),Is.False);
            var joint=ball.gameObject.AddComponent<FixedJoint>(); // Unconnected joint anchors to physical world.
            Assert.That(motion.SetPose(Vector3.right,Quaternion.identity,out var error),Is.False);Assert.That(error,Does.Contain("connection"));
            Assert.That(content.transform.position,Is.EqualTo(at));Assert.That(ball.position,Is.EqualTo(p));Assert.That(ball.linearVelocity,Is.EqualTo(velocity));
            Object.Destroy(joint);yield return null;
            world.SetSurfaces(true,"Physical scan ready");Assert.That(world.SetEnvironment((string)world.ObserveEnvironment()["stateId"],true,out _,out error),Is.True,error);world.StartPhysics();
            p=ball.position;Assert.That(motion.SetPose(Vector3.right,Quaternion.identity,out error),Is.False);Assert.That(error,Does.Contain("real-room collisions"));Assert.That(ball.position,Is.EqualTo(p));Assert.That(content.transform.position,Is.EqualTo(at));
        }
        [UnityTest]public IEnumerator AuthoredNavigationMovesItsInstanceWithoutRebakingAndTrackingStaysFixed() {
            Assert.That(navigation.Prepare(.2f,1.6f,out var error),Is.True,error);var build=navigation.BuildRevision;var revision=navigation.SurfaceRevision;var eye=tracking.position;
            for(int i=1;i<=10;i++){Shift(new Vector3(i*.3f,0,-i*.2f),i*5);Assert.That(navigation.Sample(content.transform.TransformPoint(Vector3.right),.2f,out var ground),Is.True);Assert.That(Vector3.Distance(ground,content.transform.TransformPoint(Vector3.right)),Is.LessThan(.05f));Assert.That(navigation.BuildRevision,Is.EqualTo(build));}
            Assert.That(navigation.SurfaceRevision,Is.GreaterThan(revision));Assert.That(tracking.position,Is.EqualTo(eye));yield return null;
        }
        [UnityTest]public IEnumerator GrabbedBodyStaysWithItsPhysicalHandWithoutAFrameShiftThrow() {
            var manager=root.GetComponent<XRInteractionManager>();var hand=new GameObject("Physical hand");hand.SetActive(false);hand.transform.SetParent(root.transform,false);hand.transform.position=ball.position-Vector3.forward*.3f;
            var ray=hand.AddComponent<XRRayInteractor>();ray.enableUIInteraction=false;ray.interactionManager=manager;ray.keepSelectedTargetValid=true;ray.manipulateAttachTransform=false;
            ray.selectActionTrigger=XRBaseInputInteractor.InputTriggerType.State;ray.selectInput=new XRInputButtonReader {inputSourceMode=XRInputButtonReader.InputSourceMode.ManualValue,manualPerformed=true,manualValue=1};
            hand.SetActive(true);var item=ball.GetComponent<RoomItem>();manager.SelectEnter((IXRSelectInteractor)ray,item.Grab);yield return new WaitForSeconds(.2f);
            for(int i=1;i<=5;i++){Physics.SyncTransforms();var p=ball.position;Shift(new Vector3(i*.04f,0,0),i*5);Assert.That(Vector3.Distance(ball.position,p),Is.LessThan(.0001f));yield return new WaitForSeconds(.025f);}
            ray.selectInput.manualPerformed=false;ray.selectInput.manualValue=0;manager.SelectExit((IXRSelectInteractor)ray,item.Grab);yield return null;
            Assert.That(Vector3.ProjectOnPlane(ball.linearVelocity,Vector3.up).magnitude,Is.LessThan(.05f));Assert.That(ball.angularVelocity.magnitude,Is.LessThan(.05f));
        }
        [UnityTest]public IEnumerator DisabledRoomRetainsWorldPoseWithoutWritingTracking() {
            world.PausePhysics();var camera=tracking.gameObject.AddComponent<Camera>();var view=root.AddComponent<VirtualRoomView>();view.Initialize(content.transform,root.transform,camera,null,world);
            var eye=tracking.position;var facing=tracking.rotation;var home=content.transform.position;
            Assert.That(view.Enter(),Is.True);Assert.That(view.Turn(30),Is.True,view.MovementError);var position=content.transform.position;var rotation=content.transform.rotation;Assert.That(rotation,Is.Not.EqualTo(Quaternion.identity));
            root.SetActive(false);Assert.That(view.Active,Is.False);Assert.That(view.MovementError,Is.Null);Assert.That(content.transform.position,Is.EqualTo(position));Assert.That(content.transform.rotation,Is.EqualTo(rotation));
            root.SetActive(true);yield return null;Assert.That(tracking.position,Is.EqualTo(eye));Assert.That(tracking.rotation,Is.EqualTo(facing));Assert.That(view.Active,Is.False);Assert.That(world.Running,Is.False);
        }
        [UnityTest]public IEnumerator ViewChangesKeepTheMovingWorldAndAcceptedGroundAlive() {
            var camera=tracking.gameObject.AddComponent<Camera>();camera.backgroundColor=Color.clear;
            var scan=root.AddComponent<ScannedRoom>();scan.Initialize(world);world.SetSurfaces(true,"Aligned real scan");
            var view=root.AddComponent<VirtualRoomView>();view.Initialize(content.transform,root.transform,camera,scan,world);
            var ground=content.GetComponentInChildren<RoomWalkableSurface>();var collider=ground.Collision;var groundRevision=ground.Revision;
            Assert.That(rigid.Launch(new Vector3(.3f,2,0),Vector3.up),Is.True);Shift(new Vector3(2,0,-1),35);
            Assert.That(navigation.Prepare(.2f,1.6f,out var error),Is.True,error);var bake=navigation.BuildRevision;
            var frame=content.transform.localToWorldMatrix;var physical=tracking.localToWorldMatrix;var simulation=world.ObserveSimulation().ToString();
            var velocity=ball.linearVelocity;var angular=ball.angularVelocity;var placement=rigid.PlacementRevision;var motionRevision=rigid.MotionRevision;
            for(int i=0;i<3;i++) {
                Assert.That(view.SetPresentation(.5f,true),Is.True);
                Assert.That(camera.backgroundColor.a,Is.EqualTo(.5f));
                Assert.That(view.SetPresentation(.5f,false),Is.True);Assert.That(view.WantsRealDepth,Is.False);
                Assert.That(view.Enter(),Is.True);view.Exit();
                Assert.That(content.transform.localToWorldMatrix,Is.EqualTo(frame));Assert.That(tracking.localToWorldMatrix,Is.EqualTo(physical));
                Assert.That(ball.linearVelocity,Is.EqualTo(velocity));Assert.That(ball.angularVelocity,Is.EqualTo(angular));
                Assert.That(rigid.PlacementRevision,Is.EqualTo(placement));Assert.That(rigid.MotionRevision,Is.EqualTo(motionRevision));
                Assert.That(world.ObserveSimulation().ToString(),Is.EqualTo(simulation));Assert.That(world.SurfacesReady,Is.True);
                Assert.That(ground.Collision,Is.SameAs(collider));Assert.That(ground.Available,Is.True);Assert.That(ground.Revision,Is.EqualTo(groundRevision));
                Assert.That(content.GetComponentsInChildren<RoomWalkableSurface>().Length,Is.EqualTo(1));
                Assert.That(navigation.Sample(content.transform.TransformPoint(Vector3.zero),.1f,out _),Is.True);Assert.That(navigation.BuildRevision,Is.EqualTo(bake));
            }
            var before=ball.position;yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
            Assert.That(world.Running,Is.True);Assert.That(Vector3.Distance(before,ball.position),Is.GreaterThan(.001f),"Native physics must continue after returning to MR");
        }
        [UnityTest]public IEnumerator ViewNeverInventsGroundAndTravelRefusesHolesAndUnsupportedRaisedTerrain() {
            world.PausePhysics();var camera=tracking.gameObject.AddComponent<Camera>();var view=root.AddComponent<VirtualRoomView>();view.Initialize(content.transform,root.transform,camera,null,world);
            var floor=content.GetComponentInChildren<RoomWalkableSurface>();var collider=floor.Collision;var at=content.transform.position;
            floor.gameObject.SetActive(false);Assert.That(view.Enter(),Is.True);Assert.That(view.Move(Vector3.right*.05f),Is.False);Assert.That(view.MovementError,Does.Contain("accepted ground"));
            Assert.That(content.GetComponentsInChildren<RoomWalkableSurface>(true).Length,Is.EqualTo(1));Assert.That(content.transform.position,Is.EqualTo(at));
            floor.gameObject.SetActive(true);floor.transform.position+=Vector3.up*.2f;Assert.That(view.Move(Vector3.right*.05f),Is.False,"No floating over raised terrain");
            floor.transform.position-=Vector3.up*.2f;Assert.That(view.Move(Vector3.right*.05f),Is.True,view.MovementError);
            tracking.position=new Vector3(collider.bounds.max.x-.22f,1.6f,2);at=content.transform.position;
            Assert.That(view.Move(Vector3.right*.05f),Is.False,"A footprint crossing an edge must stop");Assert.That(content.transform.position,Is.EqualTo(at));
            view.Exit();Assert.That(floor.Available,Is.True);yield return null;
        }
        [UnityTearDown]public IEnumerator Cleanup(){Object.Destroy(root);Time.captureDeltaTime=previousDelta;yield return null;yield return null;}
    }
}
