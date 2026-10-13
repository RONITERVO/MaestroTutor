// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        void ContinuityContact(string a,string b){
            editor.RememberPhysicsStep();
            editor.RecordPhysicsContact(editor.Find(a).GetComponent<RigidRoomItem>(),editor.Find(b).GetComponentInChildren<Collider>());
        }
        IEnumerator ContinuityGrip(bool beforeDrain){
            string a=PhysicsInputBall(new(-10,3,-10)),b=PhysicsInputBall(new(-9,3,-10)),target=RecoveryAssembly(new(15,3,15));
            StartRecoveryFixture();ContinuityContact(a,b);QueueRecovery(a,target);
            if(!beforeDrain)editor.ProcessDormantContacts();
            var(item,hand)=ContactGrab(a);Assert.That(item.Grab.isSelected,Is.True);editor.ProcessDormantContacts();
            Assert.That((bool)PhysicsInputFact(a)["interrupted"],Is.False,"Recovery must not take the user's held body");
            Assert.That((bool)PhysicsInputFact(b)["interrupted"],Is.True,"Touching is not a grant of ownership over the other object");
            Assert.That(editor.Find(b).GetComponent<Rigidbody>().isKinematic,Is.True);
            yield return NavigationLoadingDone();Assert.That(editor.Find(target),Is.Not.Null,"The free participant keeps its own preparation alive");
            Assert.That(item.Grab.isSelected,Is.True);Assert.That((bool)PhysicsInputFact(b)["interrupted"],Is.True);
            hand.gameObject.SetActive(false);yield return null;
        }
        [UnityTest]public IEnumerator ContactContinuityGripBeforeDrainKeepsAnotherContactParticipantInterrupted()=>ContinuityGrip(true);
        [UnityTest]public IEnumerator ContactContinuityGripAfterDrainKeepsAnotherContactParticipantInterrupted()=>ContinuityGrip(false);
        [UnityTest]public IEnumerator ContactContinuityTeleportBeforeDrainCancelsOnlyItsCurrentConnectionGroup(){
            string a=PhysicsInputBall(new(-10,3,-10)),b=PhysicsInputBall(new(-9,3,-10)),target=RecoveryAssembly(new(15,3,15));
            StartRecoveryFixture();ContinuityContact(a,b);QueueRecovery(a,target);
            var item=editor.Find(a);item.transform.position+=Vector3.back;item.GetComponent<RigidRoomItem>().Teleported();editor.ProcessDormantContacts();
            Assert.That((bool)PhysicsInputFact(a)["interrupted"],Is.False);Assert.That((bool)PhysicsInputFact(b)["interrupted"],Is.True);
            yield return NavigationLoadingDone();Assert.That(editor.Find(target),Is.Not.Null);Assert.That(editor.Find(b).GetComponent<Rigidbody>().isKinematic,Is.True);
        }
        [UnityTest]public IEnumerator ContactContinuityAnimationTakesTheLinkedAssemblyButNotItsContactNeighbour(){
            var ids=PhysicsIslandChain();string free=PhysicsInputBall(new(-15,3,-10)),target=RecoveryAssembly(new(15,3,15));
            StartRecoveryFixture();yield return IslandActive(ids);ContinuityContact(ids[0],free);QueueRecovery(ids[0],target);editor.ProcessDormantContacts();
            editor.Find(ids[1]).GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);
            foreach(var id in ids)Assert.That((bool)PhysicsInputFact(id)["interrupted"],Is.False,id);
            Assert.That((bool)PhysicsInputFact(free)["interrupted"],Is.True);yield return NavigationLoadingDone();
            Assert.That(editor.Find(target),Is.Not.Null);Assert.That(editor.Find(free).GetComponent<Rigidbody>().isKinematic,Is.True);
        }
        [UnityTest]public IEnumerator ContactContinuityNativeStayRequeuesAfterCancelledObservation(){
            string id=PhysicsInputBall(new(-2,3,10)),target=RecoveryObstacle(new(5,3,10),4);StartRecoveryFixture();
            var item=editor.Find(id);var rigid=item.GetComponent<RigidRoomItem>();var body=item.GetComponent<Rigidbody>();var counter=item.gameObject.AddComponent<ContinuityContactCounter>();int authored=0;rigid.ContactStarted+=(_,__,___,____)=>authored++;
            var mode=Physics.simulationMode;var gravity=Physics.gravity;
            try{
                Physics.gravity=Vector3.right*9.81f;Assert.That(rigid.Launch(Vector3.zero,Vector3.zero),Is.True);
                // Place a native sphere against the guard after initial admission,
                // with gravity pressing into it. A long speculative hit can stop
                // short of the wall and is not a persistent-contact fixture.
                Physics.SyncTransforms();var bounds=editor.DormantCollisionGuard(target).Shape.bounds;float radius=item.GetComponentInChildren<SphereCollider>().bounds.extents.x;
                item.transform.position=new(bounds.min.x-radius+.001f,bounds.center.y,bounds.center.z);body.position=item.transform.position;editor.RememberPhysicsStep();
                Physics.simulationMode=SimulationMode.Script;Physics.SyncTransforms();Physics.Simulate(.02f);Assert.That(counter.GuardEnters,Is.GreaterThan(0),"Guard="+bounds+" radius="+radius+" body="+body.position+" kinematic="+body.isKinematic);
                // Cancel that observed actor without running ordinary preparation:
                // this isolates real Enter/Stay dispatch, not a production frame or full ownership handoff.
                editor.CancelDormantRecovery(item);editor.ProcessDormantContacts();Assert.That(editor.Find(target),Is.Null);Assert.That((bool)PhysicsInputFact(id)["interrupted"],Is.False);
                int enters=counter.GuardEnters;body.linearVelocity=Vector3.zero;body.WakeUp();editor.RememberPhysicsStep();Physics.Simulate(.02f);
                Assert.That(counter.GuardEnters,Is.EqualTo(enters));Assert.That(counter.GuardStays,Is.GreaterThan(0),"The regression must use a real native Stay callback");
                editor.ProcessDormantContacts();Assert.That((bool)PhysicsInputFact(id)["interrupted"],Is.True);Assert.That(body.isKinematic,Is.True);Assert.That(authored,Is.Zero);
                Debug.Log("MAESTRO_CONTACT_CONTINUITY native guard enter="+counter.GuardEnters+" stay="+counter.GuardStays);
            }finally{Physics.simulationMode=mode;Physics.gravity=gravity;}
            yield return NavigationLoadingDone();
        }
        IEnumerator ContinuityCatch(bool guard){
            string a=PhysicsInputBall(new(4,3.045f,4)),b=PhysicsInputBall(new(4,3,4));if(guard)RecoveryObstacle(new(13,3,4),2);
            var viewer=new GameObject("Continuity catch viewer");viewer.transform.SetParent(root.transform,false);viewer.transform.position=new(-4,1.6f,0);root.GetComponent<RoomInteraction>().Viewer=viewer.transform;
            var holder=editor.Find("book");holder.transform.position=editor.Find(a).transform.position-Vector3.forward*.35f;holder.transform.rotation=Quaternion.identity;
            var rigid=editor.Find(a).GetComponent<RigidRoomItem>();var ca=rigid.gameObject.AddComponent<ContinuityContactCounter>();var cb=editor.Find(b).gameObject.AddComponent<ContinuityContactCounter>();int caught=0,settled=0;
            editor.ItemCaught+=(_,__,___,____,_____)=>caught++;rigid.Settled+=_=>settled++;
            var mode=Physics.simulationMode;var gravity=Physics.gravity;
            try{
                Time.captureDeltaTime=0;Physics.gravity=Vector3.zero;StartRecoveryFixture();Assert.That(rigid.Launch(Vector3.zero,Vector3.zero),Is.True);Assert.That(editor.Find(b).GetComponent<RigidRoomItem>().Launch(Vector3.right*15,Vector3.zero),Is.True);
                var args=BehaviourCatalog.Action("object.physics.catch").Example;args["target"]=a;args["holder"]=new JObject{["kind"]="object",["objectId"]="book",["revision"]=editor.ObjectRevision("book")};args["offset"]=JObject.Parse(JsonUtility.ToJson(Vector3.forward*.35f));args["timeout"]=3;args["holdSeconds"]=2;args["gripRadius"]=.08;args["maxSpeed"]=4;
                var call=new JObject{["id"]="object.physics.catch",["version"]=1,["arguments"]=args};Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out var run,out var error),Is.True,error);
                var attempt=holder.GetComponent<RoomCatch>();attempt.SendMessage("FixedUpdate");editor.RememberPhysicsStep();
                Physics.simulationMode=SimulationMode.Script;Physics.SyncTransforms();Physics.Simulate(.65f);
                // Explicit simulation occurs after this frame's editor Update;
                // complete its observation before yielding to ordinary LateUpdate.
                // The subsequent catch loop uses native FixedUpdate timing.
                editor.ProcessDormantContacts();Physics.simulationMode=mode;
                Assert.That(ca.ItemContacts,Is.GreaterThan(0));Assert.That(ca.GuardEnters,Is.Zero);Assert.That(cb.GuardEnters,guard?Is.GreaterThan(0):Is.Zero);
                if(guard){
                    for(int i=0;i<3;i++)yield return new WaitForFixedUpdate();yield return new WaitForSeconds(.75f);
                    Assert.That(attempt.Caught,Is.False);Assert.That(caught,Is.Zero);Assert.That(settled,Is.Zero,"Interrupted preparation is not settlement");
                    Assert.That((bool)PhysicsInputFact(a)["interrupted"],Is.True);Assert.That(attempt.Reason,Does.Contain("from rest"));
                    Assert.That(BehaviourCatalog.TryRead("object.catch",1,new JObject{["target"]=a},new BehaviourCatalog.FactContext(editor:editor),out var fact),Is.True);
                    var reason=JObject.FromObject(fact.Value)["attempts"][0]["reason"].Value<string>();
                    Assert.That(reason,Is.EqualTo(Maestro.Quest.Imports.ImportObservation.Text(attempt.Reason)));Assert.That(reason,Does.Contain("Movement stopped"));Assert.That(reason.Length,Is.LessThanOrEqualTo(128));
                }else{
                    for(int i=0;i<12&&!attempt.Caught;i++)yield return new WaitForFixedUpdate();
                    Assert.That(attempt.Caught,Is.True,"Matching available geometry must permit a real catch: "+attempt.Reason);Assert.That(caught,Is.EqualTo(1));
                }
                runtime.Scheduler.CancelInvocation(run,out _);Debug.Log("MAESTRO_CONTACT_CONTINUITY guard="+guard+" caught="+caught+" settled="+settled+" native contacts="+ca.ItemContacts+" reason="+attempt.Reason);
            }finally{Physics.simulationMode=mode;Physics.gravity=gravity;}
            yield return null;
        }
        [UnityTest]public IEnumerator ContactContinuityInterruptedSecondaryBodyCannotCatchOrSettle()=>ContinuityCatch(true);
        [UnityTest]public IEnumerator ContactContinuityMatchingUninterruptedBodyCanCatch()=>ContinuityCatch(false);
    }
    internal sealed class ContinuityContactCounter:MonoBehaviour {
        internal int GuardEnters,GuardStays,ItemContacts;
        void OnCollisionEnter(Collision collision){if(collision.collider.GetComponent<DormantCollisionProxy>())GuardEnters++;else if(collision.collider.GetComponentInParent<RoomItem>())ItemContacts++;}
        void OnCollisionStay(Collision collision){if(collision.collider.GetComponent<DormantCollisionProxy>())GuardStays++;}
    }
}
