// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        string RecoveryObstacle(Vector3 position,float scale=1){
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Recovery obstacle",position,scale,Color.white,out var id,out var error),Is.True,error);
            Assert.That(editor.SetItemPhysics(id,new(){mode="fixed",shape="box",mass=1}),Is.True,editor.Status);
            Assert.That(editor.RetireNativeArea(NativeAreaFor(id),out error),Is.True,error);return id;
        }
        string RecoveryAssembly(Vector3 position){
            // Eight parts force the production cooperative preparation budget to
            // span frames, even after destruction of the retired view has drained.
            var recipe=new RoomRecipe{parts=Enumerable.Range(0,8).Select(i=>new RecipePart{id="Part"+i,position=Vector3.up*i*.1f,size=Vector3.one*.1f}).ToArray()};
            Assert.That(editor.CreateRecipe("Recovery assembly",position,1,recipe,null,new ObjectPhysicsSettings{mode="fixed",shape="box",mass=1},out var id,out var error),Is.True,error);
            Assert.That(editor.RetireNativeArea(NativeAreaFor(id),out error),Is.True,error);return id;
        }
        void StartRecoveryFixture(){
            // Isolate the new fallback lane. Production still uses whole-world admission.
            physics.ConfigureCollisionAdmission(editor,()=>null);StartIsland();
        }
        void QueueRecovery(string actor,string target){
            var rigid=editor.Find(actor).GetComponent<RigidRoomItem>();Assert.That(rigid.Simulating,Is.True,PhysicsInputFact(actor).ToString());
            editor.QueueDormantContact(rigid,editor.DormantCollisionGuard(target).Shape.GetComponent<DormantCollisionProxy>());
        }
        [UnityTest]public IEnumerator DormantRecoveryNativeImpactQueuesWithoutChangingBodiesInsideTheCallback(){
            string actor=PhysicsInputBall(new(-2,3,10)),target=RecoveryObstacle(new(5,3,10),4);StartRecoveryFixture();
            var item=editor.Find(actor);var rigid=item.GetComponent<RigidRoomItem>();var body=item.GetComponent<Rigidbody>();var counter=item.gameObject.AddComponent<DormantCollisionCounter>();
            Assert.That(rigid.Launch(Vector3.right*15,Vector3.zero),Is.True);body.useGravity=false;int contacts=0;rigid.ContactStarted+=(_,__,___,____)=>contacts++;
            var mode=Physics.simulationMode;
            try{
                // Deliberately advance beyond the prepared normal timestep. This is
                // an unpredicted native-contact test, not a production timestep claim.
                Physics.simulationMode=SimulationMode.Script;Physics.SyncTransforms();Physics.Simulate(.5f);
                Assert.That(counter.Contacts,Is.GreaterThan(0));Assert.That(contacts,Is.Zero);Assert.That(body.isKinematic,Is.False,"The callback must only queue identity");
                Assert.That(editor.NativeActivationPending,Is.False);Assert.That((bool)PhysicsInputFact(actor)["interrupted"],Is.False);
                var pose=body.position;editor.ProcessDormantContacts();Assert.That(body.isKinematic,Is.True);Assert.That(body.position,Is.EqualTo(pose));
                Assert.That((bool)PhysicsInputFact(actor)["interrupted"],Is.True);Assert.That((bool)PhysicsInputFact(actor)["momentumHeld"],Is.False);
            }finally{Physics.simulationMode=mode;}
            yield return NavigationLoadingDone();Assert.That(editor.Find(target),Is.Not.Null);Assert.That(body.isKinematic,Is.True);Assert.That(contacts,Is.Zero);
        }
        [UnityTest]public IEnumerator DormantRecoveryHoldsConnectedGroupAndLoadsWithoutPausingUnrelatedBodies(){
            var ids=PhysicsIslandChain();string free=PhysicsInputBall(new(-15,3,-10)),target=RecoveryObstacle(new(15,2,15));StartRecoveryFixture();yield return IslandActive(ids);
            foreach(var id in ids)Assert.That(editor.Find(id).GetComponent<RigidRoomItem>().Launch(Vector3.right,Vector3.zero),Is.True);
            Assert.That(editor.Find(free).GetComponent<RigidRoomItem>().Launch(Vector3.right*2,Vector3.zero),Is.True);
            int revision=editor.Revision;QueueRecovery(ids[0],target);QueueRecovery(ids[0],target);Assert.That(editor.NativeActivationPending,Is.False);
            editor.ProcessDormantContacts();foreach(var id in ids){Assert.That(editor.Find(id).GetComponent<Rigidbody>().isKinematic,Is.True);Assert.That((bool)PhysicsInputFact(id)["interrupted"],Is.True);Assert.That((bool)PhysicsInputFact(id)["momentumHeld"],Is.False);}
            Assert.That(editor.Find(free).GetComponent<RigidRoomItem>().Simulating,Is.True);Assert.That((bool)PhysicsInputFact(free)["interrupted"],Is.False);float x=editor.Find(free).transform.position.x;
            yield return NavigationLoadingDone();editor.ProcessDormantContacts();Assert.That(editor.Find(target),Is.Not.Null);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(physics.Running,Is.True);
            yield return new WaitForFixedUpdate();Assert.That(editor.Find(free).transform.position.x,Is.GreaterThan(x));
            foreach(var id in ids){var fact=PhysicsInputFact(id);Assert.That((string)fact["state"],Is.EqualTo("unavailable"));Assert.That((bool)fact["interrupted"],Is.True);Assert.That((string)fact["reason"],Does.Contain("resume from rest"));}
            physics.PausePhysics();StartIsland();yield return IslandActive(ids);foreach(var id in ids){Assert.That((bool)PhysicsInputFact(id)["interrupted"],Is.False);Assert.That(Mathf.Abs(editor.Find(id).GetComponent<Rigidbody>().linearVelocity.x),Is.LessThan(.05f));}
        }
        [UnityTest]public IEnumerator DormantRecoveryPauseCancelsItsOwnerButAnIndependentDemandCanFinish(){
            string actor=PhysicsInputBall(new(-10,2,-10)),target=RecoveryObstacle(new(15,2,15));StartRecoveryFixture();QueueRecovery(actor,target);editor.ProcessDormantContacts();
            using var other=editor.DemandNativeEntities(new[]{target},()=>new[]{target},()=>true);physics.PausePhysics();yield return NavigationLoadingDone();
            Assert.That(other.Error,Is.Null);Assert.That(editor.Find(target),Is.Not.Null);Assert.That((bool)PhysicsInputFact(actor)["interrupted"],Is.False);Assert.That(physics.Running,Is.False);
            StartIsland();yield return new WaitForFixedUpdate();Assert.That(Mathf.Abs(editor.Find(actor).GetComponent<Rigidbody>().linearVelocity.x),Is.LessThan(.05f));
        }
        [UnityTest]public IEnumerator DormantRecoveryChangedTargetBeforeDrainStopsWithoutLoadingStaleGeometry(){
            string actor=PhysicsInputBall(new(-10,2,-10)),target=RecoveryObstacle(new(15,2,15));StartRecoveryFixture();QueueRecovery(actor,target);
            var changed=editor.Read(target);changed.position+=Vector3.left;Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{changed},Array.Empty<string>(),out var error),Is.True,error);
            editor.ProcessDormantContacts();Assert.That(editor.Find(target),Is.Null);Assert.That(editor.NativeActivationPending,Is.False);Assert.That(editor.Find(actor).GetComponent<Rigidbody>().isKinematic,Is.True);
            Assert.That((bool)PhysicsInputFact(actor)["interrupted"],Is.True);Assert.That((string)PhysicsInputFact(actor)["reason"],Does.Contain("changed"));yield return null;
        }
        [UnityTest]public IEnumerator DormantRecoveryTeleportBeforeDrainCancelsTheCapturedContact(){
            string actor=PhysicsInputBall(new(-10,2,-10)),target=RecoveryObstacle(new(15,2,15));StartRecoveryFixture();QueueRecovery(actor,target);
            var item=editor.Find(actor);item.transform.position+=Vector3.back;item.GetComponent<RigidRoomItem>().Teleported();editor.ProcessDormantContacts();
            Assert.That(editor.NativeActivationPending,Is.False);Assert.That(editor.Find(target),Is.Null);Assert.That((bool)PhysicsInputFact(actor)["interrupted"],Is.False);yield return null;
        }
        [UnityTest]public IEnumerator DormantRecoveryFrameChangeCancelsPreparationAndKeepsAnExplicitInterruption(){
            string actor=PhysicsInputBall(new(-10,2,-10)),target=RecoveryObstacle(new(15,2,15));StartRecoveryFixture();QueueRecovery(actor,target);editor.ProcessDormantContacts();
            root.transform.position=Vector3.right;editor.ProcessDormantContacts();yield return NavigationLoadingDone();
            Assert.That(editor.Find(target),Is.Null);Assert.That((bool)PhysicsInputFact(actor)["interrupted"],Is.True);Assert.That((string)PhysicsInputFact(actor)["reason"],Does.Contain("world moved"));Assert.That(editor.Find(actor).GetComponent<Rigidbody>().isKinematic,Is.True);
        }
        [UnityTest]public IEnumerator DormantRecoveryReadyLeaseRechecksAcceptedEditsAndDoesNotReportStaleReadiness(){
            string actor=PhysicsInputBall(new(-10,2,-10)),target=RecoveryObstacle(new(15,2,15));StartRecoveryFixture();QueueRecovery(actor,target);editor.ProcessDormantContacts();yield return NavigationLoadingDone();editor.ProcessDormantContacts();
            Assert.That((string)PhysicsInputFact(actor)["reason"],Does.Contain("ready"));var changed=editor.Read(target);changed.position+=Vector3.left;
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{changed},Array.Empty<string>(),out var error),Is.True,error);editor.ProcessDormantContacts();
            Assert.That((bool)PhysicsInputFact(actor)["interrupted"],Is.True);Assert.That((string)PhysicsInputFact(actor)["reason"],Does.Contain("changed"));Assert.That(editor.Find(actor).GetComponent<Rigidbody>().isKinematic,Is.True);
        }
        [UnityTest]public IEnumerator DormantRecoverySeveralContactsPrepareEveryTargetUnderOneConnectedHold(){
            var ids=PhysicsIslandChain();string a=RecoveryAssembly(new(15,2,15)),b=RecoveryAssembly(new(-15,2,15));
            StartRecoveryFixture();yield return IslandActive(ids);
            QueueRecovery(ids[0],a);QueueRecovery(ids[2],b);editor.ProcessDormantContacts();
            foreach(var id in ids){Assert.That(editor.DormantRecoveryTargets(editor.Find(id)),Is.EquivalentTo(ids.Concat(new[]{a,b})));Assert.That(editor.Find(id).GetComponent<Rigidbody>().isKinematic,Is.True);}
            yield return NavigationLoadingDone();Assert.That(editor.Find(a),Is.Not.Null);Assert.That(editor.Find(b),Is.Not.Null);
            foreach(var id in ids)Assert.That((bool)PhysicsInputFact(id)["interrupted"],Is.True);
        }
        [UnityTest]public IEnumerator DormantRecoveryFixedAnchorKeepsItsMovingChildrenIndependent(){
            var ids=PhysicsIslandChain(true,true);string a=RecoveryAssembly(new(15,2,15)),b=RecoveryAssembly(new(-15,2,15));
            StartRecoveryFixture();yield return IslandActive(ids);
            QueueRecovery(ids[0],a);editor.ProcessDormantContacts();
            Assert.That(editor.Find(ids[2]).GetComponent<RigidRoomItem>().Simulating,Is.True);
            QueueRecovery(ids[2],b);editor.ProcessDormantContacts();
            Assert.That(editor.DormantRecoveryTargets(editor.Find(ids[0])),Is.EquivalentTo(new[]{ids[0],ids[1],a}));
            Assert.That(editor.DormantRecoveryTargets(editor.Find(ids[2])),Is.EquivalentTo(new[]{ids[1],ids[2],b}));
            editor.Find(ids[0]).GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);
            Assert.That((bool)PhysicsInputFact(ids[0])["interrupted"],Is.False);Assert.That((bool)PhysicsInputFact(ids[2])["interrupted"],Is.True);
            yield return NavigationLoadingDone();Assert.That(editor.Find(a),Is.Null);Assert.That(editor.Find(b),Is.Not.Null);
            editor.Find(ids[1]).GetComponent<RigidRoomItem>().Teleported();Assert.That((bool)PhysicsInputFact(ids[2])["interrupted"],Is.False);
        }
        [UnityTest]public IEnumerator DormantRecoveryPauseBeforeDrainCannotStartAnAbandonedLoad(){
            string actor=PhysicsInputBall(new(-10,2,-10)),target=RecoveryObstacle(new(15,2,15));StartRecoveryFixture();QueueRecovery(actor,target);
            physics.PausePhysics();editor.ProcessDormantContacts();yield return NavigationLoadingDone();
            Assert.That(editor.Find(target),Is.Null);Assert.That((bool)PhysicsInputFact(actor)["interrupted"],Is.False);Assert.That(physics.Running,Is.False);
        }
        [UnityTest]public IEnumerator DormantRecoveryAnimationOwnershipCancelsTheWholeConnectedHold(){
            var ids=PhysicsIslandChain();string target=RecoveryAssembly(new(15,2,15));
            StartRecoveryFixture();yield return IslandActive(ids);QueueRecovery(ids[0],target);editor.ProcessDormantContacts();
            Assert.That(editor.NativeActivationPending,Is.True);Assert.That(editor.Find(target),Is.Null);
            editor.Find(ids[1]).GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);yield return NavigationLoadingDone();
            Assert.That(editor.Find(target),Is.Null);foreach(var id in ids)Assert.That((bool)PhysicsInputFact(id)["interrupted"],Is.False);
            editor.Find(ids[1]).GetComponent<RigidRoomItem>().SetAnimationOwner(this,false);yield return IslandActive(ids);
        }
    }
}
