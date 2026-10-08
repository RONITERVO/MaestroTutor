// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using Maestro.Quest.Interaction;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class WorldMotionTests
    {
        GameObject RealSurface(Vector3 at,Vector3 size) {
            var surface=GameObject.CreatePrimitive(PrimitiveType.Cube);surface.transform.SetParent(root.transform,false);
            surface.transform.position=at;surface.transform.localScale=size;surface.layer=RoomPhysicsLayers.Scanned;return surface;
        }
        void RealPhysics() {
            world.SetSurfaces(true,"Aligned synthetic physical geometry");
            Assert.That(world.SetEnvironment((string)world.ObserveEnvironment()["stateId"],true,out _,out var error),Is.True,error);
            world.StartPhysics();Physics.SyncTransforms();Assert.That(world.Running,Is.True);
        }
        [UnityTest]public IEnumerator WorldSweepAllowsClearRealPhysicsAndPreservesBodiesOnBlockedTranslation() {
            RealSurface(new Vector3(.5f,1.5f,0),new Vector3(.005f,1,1));RealPhysics();
            Assert.That(rigid.Launch(Vector3.up,Vector3.right),Is.True);var position=ball.position;var velocity=ball.linearVelocity;var spin=ball.angularVelocity;
            var frame=content.transform.localToWorldMatrix;var revision=rigid.MotionRevision;
            Assert.That(motion.SetPose(Vector3.right,Quaternion.identity,out var error),Is.False,error);
            StringAssert.Contains("clearance",error);Assert.That(content.transform.localToWorldMatrix,Is.EqualTo(frame));
            Assert.That(ball.position,Is.EqualTo(position));Assert.That(ball.linearVelocity,Is.EqualTo(velocity));Assert.That(ball.angularVelocity,Is.EqualTo(spin));Assert.That(rigid.MotionRevision,Is.EqualTo(revision));
            Shift(Vector3.left*.2f,0);Assert.That(ball.position.x,Is.EqualTo(position.x-.2f).Within(.0001f));Assert.That(world.Running,Is.True);
            yield return new WaitForFixedUpdate();Assert.That(world.Running,Is.True);
        }
        [UnityTest]public IEnumerator WorldSweepAllowsRestingFloorContactButRefusesMovingThroughIt() {
            RealSurface(new Vector3(0,-.1f,0),new Vector3(10,.2f,10));
            ball.position=new Vector3(0,.075f,0);ball.transform.position=ball.position;RealPhysics();
            Shift(Vector3.right*.2f,0);Assert.That(ball.position.y,Is.EqualTo(.075f).Within(.0001f));
            var position=content.transform.position;
            Assert.That(motion.SetPose(position+Vector3.down*.2f,Quaternion.identity,out var error),Is.False,error);
            Assert.That(content.transform.position,Is.EqualTo(position));Assert.That(ball.position.y,Is.EqualTo(.075f).Within(.0001f));
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();Assert.That(ball.position.y,Is.GreaterThan(.07f));
        }
        [UnityTest]public IEnumerator WorldSweepChecksYawArcBetweenClearEndpoints() {
            ball.position=new Vector3(1,1.5f,0);ball.transform.position=ball.position;
            var midpoint=Quaternion.Euler(0,15,0)*ball.position;
            var wall=RealSurface(midpoint,new Vector3(.005f,.5f,.005f));RealPhysics();
            var before=ball.position;
            Assert.That(motion.SetPose(Vector3.zero,Quaternion.Euler(0,30,0),out var error),Is.False,error);
            Assert.That(ball.position,Is.EqualTo(before));Assert.That(content.transform.rotation,Is.EqualTo(Quaternion.identity));
            wall.SetActive(false);Shift(Vector3.zero,30);
            Assert.That(Vector3.Distance(ball.position,Quaternion.Euler(0,30,0)*before),Is.LessThan(.0001f));yield return null;
        }
        [UnityTest]public IEnumerator WorldSweepUsesResolvedProfilesAndDoesNotMovePhysicalBindings() {
            RealSurface(new Vector3(.3f,1.5f,0),new Vector3(.01f,1,1));RealPhysics();
            var item=ball.GetComponent<RoomItem>();ball.gameObject.AddComponent<RoomEnvironmentBinding>().Apply(world,item,false);
            var physical=Body(new Vector3(2,1,2),ItemPhysics.Fixed);physical.gameObject.AddComponent<ScannedDrawingView>();var fixedPosition=physical.position;
            Shift(Vector3.right*.6f,0);Assert.That(ball.position.x,Is.EqualTo(.6f).Within(.0001f));Assert.That(physical.position,Is.EqualTo(fixedPosition));
            world.SetSurfaces(false,"Scan lost");Assert.That(world.Running,Is.True);Shift(Vector3.right*.8f,0);
            Assert.That(physical.position,Is.EqualTo(fixedPosition));Assert.That(world.Running,Is.True);yield return null;
        }
        [UnityTest]public IEnumerator WorldSweepProtectsAnchoredItemsEvenWhenRealScanCollisionsAreOff() {
            var physical=Body(new Vector3(.3f,1.5f,0),ItemPhysics.Fixed);physical.gameObject.AddComponent<ScannedDrawingView>();Physics.SyncTransforms();
            var at=ball.position;Assert.That(motion.SetPose(Vector3.right*.6f,Quaternion.identity,out var error),Is.False,error);
            Assert.That(ball.position,Is.EqualTo(at));Assert.That(physical.position.x,Is.EqualTo(.3f));yield return null;
        }
        [UnityTest]public IEnumerator WorldSweepChecksRealContainmentAndUnknownCrowdedQueries() {
            RealPhysics();world.Contains=point=>point.x<.2f;
            Assert.That(motion.SetPose(Vector3.right*.3f,Quaternion.identity,out var error),Is.False);StringAssert.Contains("outside",error);
            world.Contains=null;
            for(int i=0;i<130;i++)RealSurface(ball.position,new Vector3(.01f,.01f,.01f));Physics.SyncTransforms();
            var at=content.transform.position;Assert.That(motion.SetPose(Vector3.right*.3f,Quaternion.identity,out error),Is.False);StringAssert.Contains("crowded",error);
            Assert.That(content.transform.position,Is.EqualTo(at));yield return null;
        }
        [UnityTest]public IEnumerator MixedViewWalkingAndTurningKeepTheRealFrameAndIndependentDepth() {
            world.PausePhysics();var camera=tracking.gameObject.AddComponent<Camera>();camera.backgroundColor=Color.clear;
            var view=root.AddComponent<VirtualRoomView>();view.Initialize(content.transform,root.transform,camera,null,world);
            tracking.position=new Vector3(2,1.6f,2);RealPhysics();
            var eye=tracking.localToWorldMatrix;var physical=RealSurface(new Vector3(-2,-.1f,-2),new Vector3(1,.2f,1));var floor=physical.transform.localToWorldMatrix;
            foreach(float opacity in new[]{0f,.25f,.5f,1f,0f}) {
                Assert.That(view.SetPresentation(opacity,true),Is.True);Assert.That(view.Move(Vector3.right*.03f),Is.True,view.MovementError);
                Assert.That(view.Turn(30),Is.True,view.MovementError);
                Assert.That(tracking.localToWorldMatrix,Is.EqualTo(eye));Assert.That(physical.transform.localToWorldMatrix,Is.EqualTo(floor));
                Assert.That(view.RealDepth,Is.True);Assert.That(view.WantsRealDepth,Is.EqualTo(opacity<1));Assert.That(world.Running,Is.True);
            }
            yield return null;
        }
        [UnityTest]public IEnumerator MixedNavigationRetiresStalePathsWithoutRebakingEveryMovingFrame() {
            // The authored floor is separate from this scanned island, so no
            // collider starts interpenetrating its physical counterpart.
            RealSurface(new Vector3(0,-.1f,12),new Vector3(4,.2f,4));RealPhysics();
            Assert.That(navigation.Prepare(.2f,1.6f,out var error),Is.True,error);
            var build=navigation.BuildRevision;var path=new UnityEngine.AI.NavMeshPath();
            Assert.That(navigation.Path(Vector3.zero,Vector3.right,path),Is.True);
            for(int i=1;i<=30;i++) {
                Shift(new Vector3(i*.01f,0,0),0);
                Assert.That(navigation.Ready,Is.True);Assert.That(navigation.PathsPending,Is.True);
                Assert.That(navigation.Path(content.transform.position,content.transform.position+Vector3.right,path),Is.False,"A stale route must not remain available");
                Assert.That(navigation.BuildRevision,Is.EqualTo(build));
                Assert.That(navigation.Traverse(content.transform.TransformPoint(new Vector3(1,0,1)),content.transform.TransformPoint(new Vector3(1.02f,0,1)),c=>true,out _,out error),Is.True,error);
                Assert.That(navigation.DirectStep(new Vector3(5.2f,0,0),new Vector3(6,0,0),out _),Is.False,"Pending routes cannot invent ground beyond the moved floor");
                yield return null;
            }
            yield return new WaitForSecondsRealtime(RoomNavigation.FrameSettleSeconds+.05f);
            Assert.That(navigation.Path(content.transform.position,content.transform.position+Vector3.right,path),Is.True);
            Assert.That(navigation.PathsPending,Is.False);Assert.That(navigation.BuildRevision,Is.EqualTo(build+1));
            Shift(content.transform.position+Vector3.right*.01f,0);Assert.That(navigation.Ready&&navigation.PathsPending,Is.True);
            world.SetSurfaces(false,"Alignment lost during movement");Assert.That(navigation.Ready,Is.False);Assert.That(navigation.PathsPending,Is.False);
        }
    }
}
