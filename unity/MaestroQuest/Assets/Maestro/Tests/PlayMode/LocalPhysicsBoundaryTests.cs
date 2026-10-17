// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        void BoundaryPose(string id,Vector3 position){
            // A controlled native-step outcome, deliberately not an authored teleport.
            var item=editor.Find(id);item.transform.position=position;item.GetComponent<Rigidbody>().position=position;Physics.SyncTransforms();
        }
        void BoundaryHeld(string id){
            var fact=PhysicsInputFact(id);
            Assert.That((bool)fact["interrupted"],Is.True,fact.ToString());
            Assert.That((bool)fact["ready"],Is.False);Assert.That((bool)fact["momentumHeld"],Is.False);
            Assert.That(editor.Find(id).GetComponent<Rigidbody>().isKinematic,Is.True);
        }
        [UnityTest]public IEnumerator LocalPhysicsBoundaryNativeExitKeepsIndependentMotionAndRestartsWithoutMomentum(){
            string id=PhysicsInputBall(new(4,3,4)),free=PhysicsInputBall(new(-4,3,4));
            physics.Contains=p=>p.x<5;StartIsland();yield return IslandActive(new[]{id,free});
            var rigid=editor.Find(id).GetComponent<RigidRoomItem>();var body=rigid.GetComponent<Rigidbody>();var other=editor.Find(free).GetComponent<Rigidbody>();
            int settled=0;rigid.Settled+=_=>settled++;Assert.That(rigid.Launch(Vector3.right*15,Vector3.up*3),Is.True);
            var accepted=body.position;float before=other.position.y;int intent=physics.SimulationIntent;
            var mode=Physics.simulationMode;
            try{
                editor.RememberPhysicsStep();Physics.simulationMode=SimulationMode.Script;Physics.SyncTransforms();Physics.Simulate(.12f);
                Assert.That(body.position.x,Is.GreaterThan(5),"The native solver must cross the admitted scan boundary");
                editor.ProcessDormantContacts();BoundaryHeld(id);
                Assert.That(Vector3.Distance(body.position,accepted),Is.LessThan(.001f));Assert.That(other.position.y,Is.LessThan(before));
                Assert.That(other.isKinematic,Is.False);Assert.That(physics.Running,Is.True);Assert.That(physics.SimulationIntent,Is.EqualTo(intent));
            }finally{Physics.simulationMode=mode;}
            yield return new WaitForSeconds(.65f);BoundaryHeld(id);Assert.That(settled,Is.Zero);
            Assert.That(PhysicsInputFact(id).ToString(),Does.Contain("ground boundary"));
            physics.PausePhysics();StartIsland();yield return IslandActive(new[]{id,free});
            Assert.That((bool)PhysicsInputFact(id)["interrupted"],Is.False);
            Assert.That(Mathf.Abs(body.linearVelocity.x),Is.LessThan(.05f));Assert.That(body.angularVelocity.magnitude,Is.LessThan(.05f));
        }
        [UnityTest]public IEnumerator LocalPhysicsBoundaryRestoresTheConnectedAssemblyBeforeAnyMemberResumes(){
            var ids=PhysicsIslandChain();string free=PhysicsInputBall(new(-10,3,-10));physics.Contains=p=>p.x<5;StartIsland();yield return IslandActive(ids);
            var accepted=ids.ToDictionary(id=>id,id=>editor.Find(id).GetComponent<Rigidbody>().position);
            editor.RememberPhysicsStep();foreach(var id in ids)BoundaryPose(id,accepted[id]+Vector3.right*2);
            editor.ProcessDormantContacts();
            foreach(var id in ids){BoundaryHeld(id);Assert.That(Vector3.Distance(editor.Find(id).transform.position,accepted[id]),Is.LessThan(.001f));}
            Assert.That(editor.Find(free).GetComponent<RigidRoomItem>().Simulating,Is.True);Assert.That(physics.Running,Is.True);
            physics.PausePhysics();StartIsland();yield return IslandActive(ids);
        }
        [UnityTest]public IEnumerator LocalPhysicsBoundaryFixedAnchorDoesNotInterruptItsOtherChild(){
            var ids=PhysicsIslandChain(false,true);physics.Contains=p=>p.x<5;StartIsland();yield return IslandActive(ids);
            editor.RememberPhysicsStep();BoundaryPose(ids[0],new(6,2,4));editor.ProcessDormantContacts();
            BoundaryHeld(ids[0]);Assert.That((bool)PhysicsInputFact(ids[2])["interrupted"],Is.False);
            Assert.That(editor.Find(ids[2]).GetComponent<RigidRoomItem>().Simulating,Is.True);Assert.That(physics.Running,Is.True);
        }
        [UnityTest]public IEnumerator LocalPhysicsBoundaryContactGroupMarksObservationAndGripDoesNotOwnNeighbour(){
            string a=PhysicsInputBall(new(4,3,4)),b=PhysicsInputBall(new(2,3,4)),free=PhysicsInputBall(new(-10,3,-10));
            physics.Contains=p=>p.x<5;StartIsland();yield return IslandActive(new[]{a,b,free});ContinuityContact(a,b);
            var observation=new NativePhysicsObservation();editor.ObservePhysicsEvent(b,a,observation);
            BoundaryPose(a,new(6,3,4));editor.ProcessDormantContacts();BoundaryHeld(a);BoundaryHeld(b);
            Assert.That(observation.Ready,Is.True);Assert.That(observation.Interrupted,Is.True);Assert.That(observation.Cancelled,Is.False);
            var(item,hand)=ContactGrab(a);Assert.That(item.Grab.isSelected,Is.True);editor.ProcessDormantContacts();
            Assert.That((bool)PhysicsInputFact(a)["interrupted"],Is.False);BoundaryHeld(b);Assert.That(editor.Find(free).GetComponent<RigidRoomItem>().Simulating,Is.True);
            hand.gameObject.SetActive(false);yield return null;
        }
        [UnityTest]public IEnumerator LocalPhysicsBoundaryCannotRewindAnAnimatedContactNeighbour(){
            string a=PhysicsInputBall(new(4,3,4)),b=PhysicsInputBall(new(2,3,4));physics.Contains=p=>p.x<5;StartIsland();yield return IslandActive(new[]{a,b});
            ContinuityContact(a,b);var peer=editor.Find(b);peer.GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);
            BoundaryPose(b,new(2,4,4));BoundaryPose(a,new(6,3,4));editor.ProcessDormantContacts();BoundaryHeld(a);
            Assert.That(peer.transform.position,Is.EqualTo(new Vector3(2,4,4)));Assert.That((bool)PhysicsInputFact(b)["interrupted"],Is.False);yield return null;
        }
        [UnityTest]public IEnumerator LocalPhysicsBoundaryKeepsAcceptedPoseAndBreakObservationAfterNativeJointBreak(){
            var(id,mount)=FixedPieces();Assert.That(Connect(AttachCall(id,mount,.1f),out var error),Is.True,error);
            physics.Contains=p=>p.x<5;var mode=Physics.simulationMode;var gravity=Physics.gravity;
            try{
                Physics.gravity=Vector3.zero;StartIsland();yield return IslandActive(new[]{id});
                var item=editor.Find(id);var body=item.GetComponent<Rigidbody>();var view=item.GetComponent<RoomConnectionView>();var accepted=body.position;
                Assert.That(view.Active,Is.True,view.Error);editor.RememberPhysicsStep();
                Physics.simulationMode=SimulationMode.Script;Physics.gravity=Vector3.down*9.81f;Physics.SyncTransforms();Physics.Simulate(.02f);
                Assert.That(view.Broken,Is.True,"Exercise the actual PhysX joint-break callback");
                // A controlled boundary outcome in the same unclassified step isolates
                // the break callback's invalidation of accepted recovery poses.
                var observation=new NativePhysicsObservation();editor.ObservePhysicsEvent(id,mount,observation);
                BoundaryPose(id,new(6,accepted.y,4));editor.ProcessDormantContacts();BoundaryHeld(id);
                Assert.That(Vector3.Distance(body.position,accepted),Is.LessThan(.001f));Assert.That(view.Broken,Is.True);
                Assert.That(observation.Ready,Is.True);Assert.That(observation.Interrupted,Is.True);Assert.That(physics.Running,Is.True);
            }finally{Physics.simulationMode=mode;Physics.gravity=gravity;}
        }
        [UnityTest]public IEnumerator LocalPhysicsBoundaryNeverSimulatedBodyWaitsWithoutInterruptingOthers(){
            string outside=PhysicsInputBall(new(6,3,4)),inside=PhysicsInputBall(new(2,3,4));physics.Contains=p=>p.x<5;StartIsland();
            yield return IslandActive(new[]{inside});Assert.That(editor.Find(outside).transform.position,Is.EqualTo(new Vector3(6,3,4)));
            Assert.That((bool)PhysicsInputFact(outside)["interrupted"],Is.False);Assert.That(physics.Running,Is.True);
        }
    }
}
