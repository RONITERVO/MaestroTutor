// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        (string id,string area) RegionalUnusedArea(Vector3? position=null){
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Unused distant area",position??new Vector3(-15,1,-15),1,Color.white,out var id,out var error),Is.True,error);
            Assert.That(editor.SetItemPhysics(id,new(){mode="fixed",shape="box",mass=1}),Is.True,editor.Status);
            return(id,NativeAreaFor(id));
        }
        [UnityTest]public IEnumerator RegionalPhysicsRetiresAndPublishesUnrelatedGeometryWithoutPausingMotion(){
            string ball=PhysicsInputBall(new(4,4,4));var(far,area)=RegionalUnusedArea();StartIsland();
            var item=editor.Find(ball);var rigid=item.GetComponent<RigidRoomItem>();var body=item.GetComponent<Rigidbody>();
            Assert.That(rigid.Launch(Vector3.right,Vector3.up),Is.True);var velocity=body.linearVelocity;var spin=body.angularVelocity;
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);
            Assert.That(physics.Running,Is.True);Assert.That(body.isKinematic,Is.False);Assert.That(body.linearVelocity,Is.EqualTo(velocity));Assert.That(body.angularVelocity,Is.EqualTo(spin));
            float height=body.position.y;yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
            Assert.That(editor.Find(far),Is.Null);Assert.That(body.position.y,Is.LessThan(height));Assert.That((bool)PhysicsInputFact(ball)["ready"],Is.True);
            yield return NativeAreaCompletion(editor.ActivateNativeArea(area));Assert.That(editor.Find(far),Is.Not.Null);
            Assert.That(physics.Running,Is.True);Assert.That(body.isKinematic,Is.False);Assert.That(body.linearVelocity.x,Is.GreaterThan(.8f));
        }
        [UnityTest]public IEnumerator RegionalPhysicsSleepingBodyKeepsItsGroundWhileDistantAreaRetires(){
            string ground=AvailabilityTerrain(),ball=PhysicsInputBall(new(4,.5f,4)),groundArea=NativeAreaFor(ground);var(_,farArea)=RegionalUnusedArea();
            StartIsland();var body=editor.Find(ball).GetComponent<Rigidbody>();body.Sleep();
            Assert.That(body.IsSleeping(),Is.True);var graph=editor.ReadRetention();
            Assert.That(graph.Reasons(ground)&RoomRetentionReason.CollisionEnvironment,Is.Not.EqualTo(RoomRetentionReason.None));
            Assert.That(editor.RetireNativeArea(groundArea,out _),Is.False);Assert.That(editor.RetireNativeArea(farArea,out var error),Is.True,error);
            Assert.That(body.IsSleeping(),Is.True);Assert.That(physics.Running,Is.True);
            Assert.That(editor.Find(ball).GetComponent<RigidRoomItem>().Launch(Vector3.up,Vector3.zero),Is.True);
            yield return new WaitForFixedUpdate();Assert.That(body.IsSleeping(),Is.False);Assert.That(editor.Find(ground),Is.Not.Null);
        }
        [UnityTest]public IEnumerator RegionalPhysicsPendingBodyRetainsItsIdentityAndAcquiresGroundThroughProductionAdmission(){
            string ground=AvailabilityTerrain(),ball=PhysicsInputBall(new(4,1,4));NativeAreaFor(ball);
            Assert.That(editor.RetireNativeArea(NativeAreaFor(ground),out var error),Is.True,error);StartIsland();
            var body=editor.Find(ball).GetComponent<Rigidbody>();Assert.That(physics.Running,Is.True);Assert.That(body.isKinematic,Is.True);
            Assert.That((string)PhysicsInputFact(ball)["state"],Is.EqualTo("pending"));
            Assert.That(editor.ReadRetention().Reasons(ball)&RoomRetentionReason.Physics,Is.Not.EqualTo(RoomRetentionReason.None),"Pending input preparation must not retire its own actor");
            yield return NavigationLoadingDone();yield return new WaitForFixedUpdate();
            Assert.That(editor.Find(ground),Is.Not.Null);Assert.That(editor.Find(ball),Is.Not.Null);Assert.That(body.isKinematic,Is.False);Assert.That(physics.Running,Is.True);
        }
        [UnityTest]public IEnumerator RegionalPhysicsRetirementRechecksNewNearbyGeometryBeforeTheNextTick(){
            string ball=PhysicsInputBall(new(4,4,4));var(far,area)=RegionalUnusedArea();StartIsland();
            Assert.That(editor.ReadRetention().Reasons(far)&RoomRetentionReason.CollisionEnvironment,Is.EqualTo(RoomRetentionReason.None));
            editor.Find(far).transform.position=editor.Find(ball).transform.position+Vector3.right;
            Physics.SyncTransforms();
            Assert.That(editor.ReadRetention().Reasons(far)&RoomRetentionReason.CollisionEnvironment,Is.Not.EqualTo(RoomRetentionReason.None));
            Assert.That(editor.RetireNativeArea(area,out _),Is.False,"Retirement cannot rely on the previous fixed tick's dependency lease");
            Assert.That(physics.Running,Is.True);yield return null;
        }
        [UnityTest]public IEnumerator RegionalPhysicsUnknownActorShapeConservativelyRetainsEveryArea(){
            string ball=PhysicsInputBall(new(4,4,4));var(far,area)=RegionalUnusedArea();StartIsland();
            var item=editor.Find(ball);var sphere=item.GetComponentInChildren<SphereCollider>();sphere.enabled=false;
            Assert.That(editor.ReadRetention().Reasons(far)&RoomRetentionReason.CollisionEnvironment,Is.Not.EqualTo(RoomRetentionReason.None));
            Assert.That(editor.RetireNativeArea(area,out _),Is.False);yield return new WaitForFixedUpdate();
            Assert.That(item.GetComponent<Rigidbody>().isKinematic,Is.True);
            sphere.enabled=true;item.GetComponent<RigidRoomItem>().Teleported();Physics.SyncTransforms();
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);Assert.That(physics.Running,Is.True);
        }
        [UnityTest]public IEnumerator RegionalPhysicsConnectedIslandRetainsRemoteMembersAndTheirSupport(){
            var ids=PhysicsIslandChain(true);string ground=AvailabilityTerrain();var(_,farArea)=RegionalUnusedArea(new(16,2,-16));StartIsland();
            foreach(var id in ids)Assert.That(editor.ReadRetention().Reasons(id),Is.Not.EqualTo(RoomRetentionReason.None));
            Assert.That(editor.ReadRetention().Reasons(ground)&RoomRetentionReason.CollisionEnvironment,Is.Not.EqualTo(RoomRetentionReason.None));
            Assert.That(editor.RetireNativeArea(farArea,out var error),Is.True,error);
            yield return IslandActive(ids);Assert.That(physics.Running,Is.True);
        }
        [UnityTest]public IEnumerator RegionalPhysicsMovingBodyPreparesDormantObstacleBeforeItsActualContact(){
            string ball=PhysicsInputBall(new(4,3,4));var(far,area)=RegionalUnusedArea(new(12,3,4));StartIsland();
            var item=editor.Find(ball);var body=item.GetComponent<Rigidbody>();var rigid=item.GetComponent<RigidRoomItem>();
            Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);Assert.That(editor.Find(far),Is.Null);
            Assert.That(rigid.Launch(Vector3.right*15,Vector3.zero),Is.True);
            float deadline=Time.realtimeSinceStartup+6;int steps=0;
            while(!editor.Find(far)&&Time.realtimeSinceStartup<deadline){yield return new WaitForFixedUpdate();steps++;Assert.That(body.position.x,Is.LessThan(11),"Do not reach a dormant collider before preparation");}
            Assert.That(steps,Is.GreaterThan(0));Assert.That(editor.Find(far),Is.Not.Null);Assert.That(physics.Running,Is.True);
            yield return new WaitForFixedUpdate();Assert.That((bool)PhysicsInputFact(ball)["interrupted"],Is.False);Assert.That(body.linearVelocity.x,Is.GreaterThan(10));
        }
        [UnityTest]public IEnumerator RegionalPhysicsAutomaticRetirementUsesSharedFactsAndKeepsRealGroundContacts(){
            string ground=AvailabilityTerrain(),ball=PhysicsInputBall(new(4,.5f,4));NativeAreaFor(ground);var(far,area)=RegionalUnusedArea();
            physics.SetSurfaces(true,"Aligned test room");NativeObservationCamera();int contacts=0;editor.Find(ball).GetComponent<RigidRoomItem>().ContactStarted+=(_,other,__,___)=>{if(other.GetComponentInParent<RoomItem>()==editor.Find(ground))contacts++;};
            Assert.That(runtime.Scheduler.Invoke(NativePhysicsCall(),Time.unscaledTime,out var run,out var error),Is.True,error);yield return NativeActionDone(run);
            editor.ConfigureNativeRetirement();
            yield return AutomaticAreaUntil(()=>!editor.Find(far),"Unused geometry did not retire while a body was running");
            yield return new WaitForSeconds(.4f);Assert.That(physics.Running,Is.True);Assert.That(editor.Find(ground),Is.Not.Null);Assert.That(editor.Find(ball),Is.Not.Null);
            Assert.That(contacts,Is.GreaterThan(0),"Exercise real native ground collision, not only retention metadata");
            Assert.That(RegionFacts.Retention().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,new JObject{["id"]=area},out var fact),Is.True);
            Assert.That((string)((JObject)fact.Value)["retirementState"],Is.EqualTo("dormant"));
            Assert.That((int)((JObject)fact.Value)["residentCount"],Is.Zero);
            Assert.That((bool)PhysicsInputFact(ball)["ready"],Is.True);
        }
    }
}
