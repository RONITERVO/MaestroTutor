// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Threading;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        [UnityTest]public IEnumerator NativePhysicsLastGroundLossDoesNotResumeAcceptedMotionOnRepair(){
            string ground=AvailabilityTerrain(),ball=ColdPhysicsBall(NewEnvironment());
            physics.StartPhysics();yield return IslandActive(new[]{ball});yield return new WaitForFixedUpdate();
            var rigid=editor.Find(ball).GetComponent<RigidRoomItem>();var body=editor.Find(ball).GetComponent<Rigidbody>();
            Assert.That(rigid.Launch(Vector3.right,Vector3.zero),Is.True);yield return new WaitForFixedUpdate();
            int intent=physics.SimulationIntent;var surface=editor.Find(ground).GetComponentInChildren<RoomWalkableSurface>().Collision;
            surface.enabled=false;Physics.SyncTransforms();for(int i=0;i<4;i++)yield return new WaitForFixedUpdate();
            Assert.That(physics.Running,Is.True);Assert.That(physics.AnySimulationReady,Is.False);Assert.That(body.isKinematic,Is.True);
            var stopped=body.position;surface.enabled=true;Physics.SyncTransforms();for(int i=0;i<8;i++)yield return new WaitForFixedUpdate();
            Assert.That(physics.SimulationIntent,Is.EqualTo(intent));Assert.That(body.isKinematic,Is.True);Assert.That(body.position,Is.EqualTo(stopped));
            Assert.That((bool)PhysicsInputFact(ball)["interrupted"],Is.True);Assert.That((bool)PhysicsInputFact(ball)["ready"],Is.False);
            physics.PausePhysics();physics.StartPhysics();yield return IslandActive(new[]{ball});
            Assert.That(Mathf.Abs(body.linearVelocity.x),Is.LessThan(.001f));
        }
        [UnityTest]public IEnumerator NativePhysicsSelectiveStartCanWaitWithoutScanAndNeverInventsAFloor(){
            string ball=PhysicsInputBall(new(4,2,4));var body=editor.Find(ball).GetComponent<Rigidbody>();var before=body.position;
            Assert.That(physics.SurfacesReady,Is.False);Assert.That(physics.AnySimulationReady,Is.False);
            Assert.That(runtime.Scheduler.Invoke(NativePhysicsCall(),Time.unscaledTime,out var run,out var error),Is.True,error);
            yield return NativeActionDone(run);for(int i=0;i<15;i++)yield return new WaitForFixedUpdate();
            Assert.That(physics.Running,Is.True);Assert.That((bool)physics.ObserveSimulation()["ready"],Is.False);
            Assert.That(body.isKinematic,Is.True);Assert.That(body.position,Is.EqualTo(before));
            Assert.That((bool)PhysicsInputFact(ball)["ready"],Is.False);Assert.That((string)PhysicsInputFact(ball)["reason"],Does.Contain("ground"));
            Assert.That(physics.CanSimulate(body.position,editor.Find(ball)),Is.False);
            physics.PausePhysics();physics.SetSurfaces(true,"Accepted aligned scan");yield return null;
            Assert.That(physics.Running,Is.False,"Accepting ground after Pause is not a new Start");Assert.That(body.isKinematic,Is.True);
        }
        [UnityTest]public IEnumerator NativePhysicsSelectiveStartPreparesColdVirtualGroundBeforeActualGravity(){
            string ground=AvailabilityTerrain(),profile=NewEnvironment(),ball=ColdPhysicsBall(profile);
            Assert.That(physics.SetEnvironment((string)physics.ObserveEnvironment()["stateId"],false,out _,out var error),Is.True,error);
            Assert.That(editor.RetireNativeArea(NativeAreaFor(ground),out error),Is.True,error);
            var(far,area)=RegionalUnusedArea();Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);
            Assert.That(physics.SurfacesReady,Is.False);Assert.That((bool)physics.ObserveEnvironment()["authoredReady"],Is.False);
            var body=editor.Find(ball).GetComponent<Rigidbody>();float height=body.position.y;int revision=editor.Revision;
            Assert.That(runtime.Scheduler.Invoke(NativePhysicsCall(),Time.unscaledTime,out var run,out error),Is.True,error);
            Assert.That(physics.Running,Is.False);Assert.That(body.isKinematic,Is.True);Assert.That(editor.Find(ground),Is.Null);
            yield return NativeActionDone(run);Assert.That(editor.Revision,Is.EqualTo(revision));for(int i=0;i<60;i++)yield return new WaitForFixedUpdate();
            Assert.That(physics.Running,Is.True);Assert.That(editor.Find(ground),Is.Not.Null);Assert.That(editor.Find(far),Is.Null);
            Assert.That(body.position.y,Is.LessThan(height-.2f));Assert.That(body.position.y,Is.GreaterThan(.1f));
            Assert.That((bool)PhysicsInputFact(ball)["ready"],Is.True);
        }
        [UnityTest]public IEnumerator NativePhysicsSelectiveStartDoesNotReadAnUnusedMissingImport(){
            var asset=ModelLibrary.Inspect("unused.glb",ModelFixture.Create());var save=editor.Models.SaveAsync(asset);yield return new WaitUntil(()=>save.IsCompleted);Assert.That(save.Exception,Is.Null);
            var create=editor.CreateImportedModelAsync(asset.Hash,CancellationToken.None);yield return new WaitUntil(()=>create.IsCompleted);Assert.That(create.Exception,Is.Null);
            string id=create.Result;var data=editor.Read(id);data.position=new(-15,1,-15);
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),Is.True,error);
            Assert.That(editor.RetireNativeArea(NativeAreaFor(id),out error),Is.True,error);yield return null;
            string path=Path.Combine(directory,"models",asset.Hash+".glb"),parked=path+".pending";File.Move(path,parked);
            try{
                string ball=PhysicsInputBall(new(4,2,4));physics.SetSurfaces(true,"Aligned floor");
                Assert.That(runtime.Scheduler.Invoke(NativePhysicsCall(),Time.unscaledTime,out var run,out error),Is.True,error);
                yield return NativeActionDone(run);yield return IslandActive(new[]{ball});
                Assert.That(physics.Running,Is.True);Assert.That(editor.Find(id),Is.Null);Assert.That(editor.NativeActivationPending,Is.False);
            }finally{File.Move(parked,path);}
        }
        [UnityTest]public IEnumerator NativePhysicsSelectiveStartLeavesAnimatedGroundToItsCurrentOwner(){
            string ground=AvailabilityTerrain(),ball=ColdPhysicsBall(NewEnvironment());var rigid=editor.Find(ball).GetComponent<RigidRoomItem>();
            rigid.SetAnimationOwner(this,true);Assert.That(editor.RetireNativeArea(NativeAreaFor(ground),out var error),Is.True,error);
            physics.SetSurfaces(true,"Aligned floor");Assert.That(runtime.Scheduler.Invoke(NativePhysicsCall(),Time.unscaledTime,out var run,out error),Is.True,error);
            yield return NativeActionDone(run);yield return new WaitForFixedUpdate();
            Assert.That(physics.Running,Is.True);Assert.That(editor.Find(ground),Is.Null);Assert.That(rigid.AnimationOwned,Is.True);Assert.That(rigid.Simulating,Is.False);
            rigid.SetAnimationOwner(this,false);yield return NavigationLoadingDone();yield return IslandActive(new[]{ball});
            Assert.That(editor.Find(ground),Is.Not.Null);
        }
        [UnityTest]public IEnumerator NativePhysicsSelectiveStartDoesNotPrepareTheHeldActorsFreeBodyGround(){
            string ground=AvailabilityTerrain(),ball=ColdPhysicsBall(NewEnvironment());var item=editor.Find(ball);
            Assert.That(editor.RetireNativeArea(NativeAreaFor(ground),out var error),Is.True,error);
            var hand=Hand(1,item.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,item.Grab);
            Assert.That(item.Grab.isSelected,Is.True);
            physics.SetSurfaces(true,"Aligned floor");Assert.That(runtime.Scheduler.Invoke(NativePhysicsCall(),Time.unscaledTime,out var run,out error),Is.True,error);
            yield return NativeActionDone(run);
            Assert.That(item.Grab.isSelected,Is.True);Assert.That(editor.Find(ground),Is.Null);Assert.That(physics.Running,Is.True);
        }
        [UnityTest]public IEnumerator NativePhysicsSelectiveStartPreparesGroundForAConnectedGroup(){
            var ids=PhysicsIslandChain(true);string ground=AvailabilityTerrain();var data=editor.Read(ground);data.position.x=8;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),Is.True,error);
            Assert.That(editor.RetireNativeArea(NativeAreaFor(ground),out error),Is.True,error);
            var(far,area)=RegionalUnusedArea();Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);
            physics.SetSurfaces(true,"Aligned floor");Assert.That(runtime.Scheduler.Invoke(NativePhysicsCall(),Time.unscaledTime,out var run,out error),Is.True,error);
            Assert.That(physics.Running,Is.False);yield return NativeActionDone(run);yield return IslandActive(ids);
            Assert.That(editor.Find(ground),Is.Not.Null);Assert.That(editor.Find(far),Is.Null);
        }
    }
}
