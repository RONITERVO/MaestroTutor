// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        (string ground,string profile) ColdPhysicsGround(bool virtualWorld=false){
            string ground=AvailabilityTerrain(),profile=NewEnvironment();
            if(virtualWorld){
                Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Other accepted ground",new(-10,0,-10),1,Color.white,out var warm,out var issue),Is.True,issue);
                var data=editor.Read(warm);data.heightFields=editor.Read(ground).heightFields;data.physics=ItemPhysics.Fixed;
                Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out issue),Is.True,issue);
                Assert.That(physics.SetEnvironment((string)physics.ObserveEnvironment()["stateId"],false,out _,out issue),Is.True,issue);
            }
            physics.SetSurfaces(!virtualWorld,virtualWorld?"No physical scan":"Aligned physical scan");
            Assert.That(physics.SetRunning(true,out var error),Is.True,error);
            Assert.That(editor.RetireNativeArea(NativeAreaFor(ground),out error),Is.True,error);
            return(ground,profile);
        }
        string ColdPhysicsBall(string profile){
            var batch=new CreationBatch {position=new(4,1,4),blueprint=new CreationBlueprint {
                version=4,resources=new CreationResources {environmentProfiles=new[]{editor.ReadEnvironment(profile)}},
                pieces=new[]{new CreationPiece {slot="Ball",name="Virtual-ground ball",scale=.4f,
                    source=new CreationSource {kind="prototype",prototype=new CreationPrototype {version=2,kind="ball",color=Color.red,
                        environmentProfile=profile,physics=new(){mode="solid",shape="sphere",mass=1}}}}}
            }};
            Assert.That(editor.CreateBatch(batch,out var ids,out var error),Is.True,error);
            return ids[0];
        }
        void ColdPhysicsPending(string id){
            var item=editor.Find(id);var body=item.GetComponent<Rigidbody>();
            Assert.That(physics.Running,Is.True);Assert.That(physics.CanSimulate(item.transform.position,item),Is.False);
            Assert.That((string)PhysicsInputFact(id)["state"],Is.EqualTo("pending"));
            Assert.That(body.isKinematic,Is.True);Assert.That(body.useGravity,Is.False);
            Assert.That(item.GetComponent<RigidRoomItem>().Launch(Vector3.right,Vector3.zero),Is.False);
        }
        [UnityTest]public IEnumerator ColdPhysicsVirtualActorLoadsOnlyItsGroundAndFallsPastThePhysicalFloor(){
            var(ground,profile)=ColdPhysicsGround();var(far,area)=RegionalUnusedArea();
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);
            var scan=GameObject.CreatePrimitive(PrimitiveType.Cube);scan.transform.SetParent(root.transform,false);
            scan.transform.position=new(4,.65f,4);scan.transform.localScale=new(2,.1f,2);scan.layer=RoomPhysicsLayers.Scanned;
            string ball=ColdPhysicsBall(profile);ColdPhysicsPending(ball);
            var body=editor.Find(ball).GetComponent<Rigidbody>();var position=body.position;
            Assert.That((bool)PhysicsInputFact(ball)["momentumHeld"],Is.False);Assert.That(editor.Find(ground),Is.Null);
            yield return NavigationLoadingDone();
            Assert.That(editor.Find(ground),Is.Not.Null);Assert.That(editor.Find(far),Is.Null,"Unrelated dormant content stays unloaded");
            for(int i=0;i<60;i++)yield return new WaitForFixedUpdate();
            Assert.That((string)PhysicsInputFact(ball)["state"],Is.EqualTo("ready"));Assert.That(physics.Running,Is.True);
            Assert.That(body.position.y,Is.LessThan(.6f),"The virtual actor must not land on the scanned floor");
            Assert.That(body.position.y,Is.GreaterThan(.1f),"The actual authored ground must still support it");
            Assert.That(body.position.y,Is.LessThan(position.y));Assert.That(physics.IncludesRealRoom(editor.Find(ball)),Is.False);
        }
        [UnityTest]public IEnumerator ColdPhysicsVirtualWorldAcquiresANewGroundColumnWithoutAScan(){
            var(ground,profile)=ColdPhysicsGround(true);string ball=ColdPhysicsBall(profile);ColdPhysicsPending(ball);
            Assert.That(physics.SurfacesReady,Is.False);yield return NavigationLoadingDone();yield return new WaitForFixedUpdate();
            Assert.That(editor.Find(ground),Is.Not.Null);Assert.That((bool)PhysicsInputFact(ball)["ready"],Is.True);
            Assert.That(editor.Find(ball).GetComponent<Rigidbody>().isKinematic,Is.False);Assert.That(physics.RealCollisions,Is.False);
        }
        [UnityTest]public IEnumerator ColdPhysicsPausedInspectionCannotAcquireGroundOrStartSimulation(){
            var(ground,profile)=ColdPhysicsGround();physics.PausePhysics();string ball=ColdPhysicsBall(profile);
            int revision=editor.Revision;string saved=JsonUtility.ToJson(editor.Snapshot());
            for(int i=0;i<3;i++){Assert.That((string)PhysicsInputFact(ball)["state"],Is.EqualTo("inactive"));yield return null;}
            Assert.That(editor.Find(ground),Is.Null);Assert.That(editor.NativeActivationPending,Is.False);Assert.That(physics.Running,Is.False);
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
        }
        [UnityTest]public IEnumerator ColdPhysicsPauseCancelsPendingGroundAndSharedStartResumesFromRest(){
            var(ground,profile)=ColdPhysicsGround();string ball=ColdPhysicsBall(profile);ColdPhysicsPending(ball);physics.PausePhysics();
            yield return NavigationLoadingDone();Assert.That(editor.Find(ground),Is.Null);Assert.That(physics.Running,Is.False);
            Assert.That((string)PhysicsInputFact(ball)["state"],Is.EqualTo("inactive"));
            Assert.That(runtime.Scheduler.Invoke(NativePhysicsCall(),Time.unscaledTime,out var run,out var error),Is.True,error);
            yield return NativeActionDone(run);yield return new WaitForFixedUpdate();
            Assert.That(physics.Running,Is.True);Assert.That((bool)PhysicsInputFact(ball)["ready"],Is.True);
            Assert.That(editor.Find(ball).GetComponent<Rigidbody>().linearVelocity.x,Is.EqualTo(0).Within(.001f));
        }
        [UnityTest]public IEnumerator ColdPhysicsAnimationCancelsItsLoadAndReturnsWithAFreshOwner(){
            var(ground,profile)=ColdPhysicsGround();string ball=ColdPhysicsBall(profile);ColdPhysicsPending(ball);
            var rigid=editor.Find(ball).GetComponent<RigidRoomItem>();rigid.SetAnimationOwner(this,true);
            yield return NavigationLoadingDone();Assert.That(editor.Find(ground),Is.Null);Assert.That((string)PhysicsInputFact(ball)["state"],Is.EqualTo("inactive"));
            var position=rigid.transform.position;rigid.SetAnimationOwner(this,false);
            Assert.That(rigid.transform.position,Is.EqualTo(position));Assert.That((bool)PhysicsInputFact(ball)["momentumHeld"],Is.False);
            // Prepared geometry may publish synchronously on this fresh request.
            Assert.That((string)PhysicsInputFact(ball)["state"],Is.EqualTo("pending").Or.EqualTo("ready"));
            if((string)PhysicsInputFact(ball)["state"]=="pending")Assert.That(rigid.GetComponent<Rigidbody>().isKinematic,Is.True);
            yield return NavigationLoadingDone();yield return new WaitForFixedUpdate();
            Assert.That(editor.Find(ground),Is.Not.Null);Assert.That(physics.IncludesRealRoom(editor.Find(ball)),Is.False);
            Assert.That((bool)PhysicsInputFact(ball)["ready"],Is.True);Assert.That(editor.Find(ball).GetComponent<Rigidbody>().linearVelocity.x,Is.EqualTo(0).Within(.001f));
        }
        [UnityTest]public IEnumerator ColdPhysicsTeleportCancelsTheOldColumnAndDoesNotInventGround(){
            var(ground,profile)=ColdPhysicsGround();string ball=ColdPhysicsBall(profile);ColdPhysicsPending(ball);
            var item=editor.Find(ball);item.transform.position=new(-14,1,-14);item.GetComponent<RigidRoomItem>().Teleported();var expected=item.transform.position;
            yield return NavigationLoadingDone();yield return new WaitForFixedUpdate();
            Assert.That(editor.Find(ground),Is.Null);Assert.That(item.GetComponent<Rigidbody>().isKinematic,Is.True);
            Assert.That((string)PhysicsInputFact(ball)["state"],Is.EqualTo("unavailable"));Assert.That(item.transform.position,Is.EqualTo(expected));
            Assert.That(physics.Running,Is.True);Assert.That((bool)PhysicsInputFact(ball)["momentumHeld"],Is.False);
        }
        [UnityTest]public IEnumerator ColdPhysicsLostVirtualGroundStopsWithoutSettlementOrBorrowingTheScan(){
            string ground=AvailabilityTerrain(),profile=NewEnvironment(),ball=ColdPhysicsBall(profile),free=PhysicsInputBall(new(-10,3,-10));StartIsland();
            var item=editor.Find(ball);var rigid=item.GetComponent<RigidRoomItem>();var body=item.GetComponent<Rigidbody>();int settled=0;rigid.Settled+=_=>settled++;
            Assert.That(rigid.Launch(Vector3.up,Vector3.zero),Is.True);editor.Find(ground).gameObject.SetActive(false);
            var position=body.position;float otherHeight=editor.Find(free).transform.position.y;
            yield return new WaitForFixedUpdate();yield return new WaitForSeconds(.75f);
            Assert.That(body.isKinematic,Is.True);Assert.That(body.position,Is.EqualTo(position));Assert.That(settled,Is.Zero);
            Assert.That((string)PhysicsInputFact(ball)["state"],Is.EqualTo("unavailable"));Assert.That(physics.Running,Is.True);
            Assert.That(editor.Find(free).transform.position.y,Is.LessThan(otherHeight));editor.Find(ground).gameObject.SetActive(true);
            yield return new WaitForFixedUpdate();Assert.That(body.isKinematic,Is.True,"A failed input is not silently retried");
            physics.PausePhysics();physics.StartPhysics();Assert.That(body.isKinematic,Is.False);
        }
        [UnityTest]public IEnumerator ColdPhysicsConnectedVirtualMembersAcquireGroundBeforeTheirJointsRun(){
            var ids=PhysicsIslandChain();string ground=AvailabilityTerrain(),profile=NewEnvironment();
            foreach(var id in ids)Assert.That(AssignEnvironment(id,profile,out var error),Is.True,error);
            Assert.That(editor.RetireNativeArea(NativeAreaFor(ground),out var issue),Is.True,issue);StartIsland();
            foreach(var id in ids)ColdPhysicsPending(id);
            Assert.That(editor.Find(ids[0]).GetComponent<RoomConnectionView>().Active,Is.False);
            yield return NavigationLoadingDone();yield return IslandActive(ids);
            foreach(var id in ids)Assert.That((bool)PhysicsInputFact(id)["ready"],Is.True);
            Assert.That(editor.Find(ids[0]).GetComponent<RoomConnectionView>().Active,Is.True);Assert.That(physics.Running,Is.True);
        }
        [UnityTest]public IEnumerator ColdPhysicsDeletingTheWaitingActorCancelsItsGroundDemand(){
            var(ground,profile)=ColdPhysicsGround();string ball=ColdPhysicsBall(profile);ColdPhysicsPending(ball);
            Assert.That(editor.DeleteObject(ball,out var error),Is.True,error);yield return NavigationLoadingDone();
            Assert.That(editor.Find(ball),Is.Null);Assert.That(editor.Find(ground),Is.Null);Assert.That(physics.Running,Is.True);
        }
    }
}
