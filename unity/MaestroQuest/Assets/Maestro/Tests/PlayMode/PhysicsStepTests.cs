// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        string PhysicsStepListener(string name,string target){
            workshop.NewSequence();var sequence=workshop.Selected;
            var program=JObject.Parse(@"{'version':3,'entry':'main','resources':[],'state':[{'name':'seen','initial':false},{'name':'interrupted','initial':false}],'events':[],
                'functions':[{'name':'main','returns':'void','parameters':[],'locals':[{'name':'received','initial':false},{'name':'source','initial':''},{'name':'uncertain','initial':false}],
                'body':[{'id':'wait','op':'awaitEvent','event':'object.collided','version':2,'source':'','timeout':{'value':0},'received':'received','value':'source','fields':{'interrupted':'uncertain'}},
                {'id':'seen','op':'setState','variable':'seen','value':{'var':'received'}},{'id':'uncertain','op':'setState','variable':'interrupted','value':{'var':'uncertain'}},{'id':'hold','op':'sleep','seconds':{'value':20}}]}]}");
            program["functions"][0]["body"][0]["event"]=name;program["functions"][0]["body"][0]["version"]=name=="object.collided"?2:1;program["functions"][0]["body"][0]["source"]=target;
            sequence.program=program.ToString();sequence.repeat=false;
            Assert.That(new RoomAgentExecutor(editor).Execute(new RoomAgentRequest{version=2,commands=new[]{new RoomAgentCommand{action="rules",rule=new RuleRequest{action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit{kind="save",sequence=sequence}}}}}},out var error,out _),Is.True,error);
            Assert.That(runtime.Trigger(sequence.id),Is.True,runtime.Scheduler.LastError);Assert.That(runtime.Scheduler.IsListening(name,target),Is.True);return sequence.id;
        }
        string PhysicsStepState(string sequence,string name)=>runtime.Scheduler.ObserveRuns().Single(x=>x.sequenceId==sequence).state.Single(x=>x.name==name).value;
        IEnumerator PhysicsStepOrder(bool guardFirst){
            var ids=PhysicsIslandChain();string target=RecoveryObstacle(new(15,2,15));StartRecoveryFixture();yield return IslandActive(ids);
            foreach(var id in ids)editor.RememberPhysicsStepBody(editor.Find(id).GetComponent<RigidRoomItem>());
            if(guardFirst)QueueRecovery(ids[0],target);
            // Controlled callback-order regression, not a claim of a native break:
            // the production break callback releases inputs then latches break state.
            editor.BeforePhysicsJointBreak(editor.Find(ids[0]).GetComponent<RigidRoomItem>());editor.InterruptPhysicsIsland(editor.Find(ids[0]));editor.Find(ids[0]).GetComponent<RoomConnectionView>().RestoreBrokenState(true);
            Assert.That(editor.PhysicsIslandMembers(editor.Find(ids[0])),Is.EquivalentTo(new[]{ids[0]}));
            if(!guardFirst)QueueRecovery(ids[0],target);editor.ProcessDormantContacts();
            foreach(var id in ids)Assert.That((bool)PhysicsInputFact(id)["interrupted"],Is.True,id);
            yield return NavigationLoadingDone();foreach(var id in ids)Assert.That(editor.Find(id).GetComponent<Rigidbody>().isKinematic,Is.True);
        }
        [UnityTest]public IEnumerator PhysicsStepGuardThenBrokenLinkKeepsTheOriginalGroup()=>PhysicsStepOrder(true);
        [UnityTest]public IEnumerator PhysicsStepBrokenLinkThenGuardKeepsTheOriginalGroup()=>PhysicsStepOrder(false);
        [UnityTest]public IEnumerator PhysicsStepNativeGuardImpactPreservesTheActualBrokenJointAndMarksItsEvent(){
            var ids=PhysicsIslandChain();var link=editor.Read(ids[0]).connections.Single();link.breakForce=.1f;
            Assert.That(editor.EditConnection(ids[0],editor.ObjectRevision(ids[0]),"configure",ids[1],link,0,out var error),Is.True,error);
            string target=RecoveryObstacle(new(13,2,4),2),listener=PhysicsStepListener("object.connection.broken",ids[0]);
            var gravity=Physics.gravity;var mode=Physics.simulationMode;
            try{
                Physics.gravity=Vector3.zero;StartRecoveryFixture();yield return IslandActive(ids);
                var counters=ids.Select(id=>editor.Find(id).gameObject.AddComponent<DormantCollisionCounter>()).ToArray();
                foreach(var id in ids){var rigid=editor.Find(id).GetComponent<RigidRoomItem>();Assert.That(rigid.Launch(Vector3.right*15,Vector3.zero),Is.True);editor.RememberPhysicsStepBody(rigid);}
                Physics.simulationMode=SimulationMode.Script;Physics.SyncTransforms();Physics.Simulate(.65f);
                Assert.That(counters.Sum(c=>c.Contacts),Is.GreaterThan(0));var view=editor.Find(ids[0]).GetComponent<RoomConnectionView>();Assert.That(view.Broken,Is.True,"This case must produce a real native joint break");
                runtime.Scheduler.Tick(Time.unscaledTime);Assert.That(PhysicsStepState(listener,"seen"),Is.EqualTo("False"));
                editor.ProcessDormantContacts();runtime.Scheduler.Tick(Time.unscaledTime+.001f);
                Assert.That(PhysicsStepState(listener,"seen"),Is.EqualTo("True"));Assert.That(PhysicsStepState(listener,"interrupted"),Is.EqualTo("True"));Assert.That(view.Broken,Is.True);
                foreach(var id in ids)Assert.That((bool)PhysicsInputFact(id)["interrupted"],Is.True);
                Debug.Log("MAESTRO_PHYSICS_STEP native guard/joint break observed; contact count="+counters.Sum(c=>c.Contacts));
            }finally{Physics.simulationMode=mode;Physics.gravity=gravity;}
            yield return NavigationLoadingDone();
        }
        [UnityTest]public IEnumerator PhysicsStepNativeSecondaryContactKeepsRealEventsAndSeparatesAnUnrelatedImpact(){
            string a=PhysicsInputBall(new(4,3.045f,4)),b=PhysicsInputBall(new(4,3,4)),c=PhysicsInputBall(new(0,3,-10)),d=PhysicsInputBall(new(.5f,3,-10));
            string target=RecoveryObstacle(new(13,3,4),2);string affected=PhysicsStepListener("object.collided",a),independent=PhysicsStepListener("object.collided",c);
            // Saving the second program correctly stops existing runs. Arm both only after authoring finishes.
            Assert.That(runtime.Trigger(affected),Is.True);Assert.That(runtime.Scheduler.IsListening("object.collided",a),Is.True);Assert.That(runtime.Scheduler.IsListening("object.collided",c),Is.True);
            var gravity=Physics.gravity;var mode=Physics.simulationMode;
            try{
                Physics.gravity=Vector3.zero;StartRecoveryFixture();
                var ca=editor.Find(a).gameObject.AddComponent<DormantCollisionCounter>();var cb=editor.Find(b).gameObject.AddComponent<DormantCollisionCounter>();
                // The resting body overlaps the moving ball, but has no direct sweep to the distant guard.
                Assert.That(editor.Find(a).GetComponent<RigidRoomItem>().Launch(Vector3.zero,Vector3.zero),Is.True);
                Assert.That(editor.Find(b).GetComponent<RigidRoomItem>().Launch(Vector3.right*15,Vector3.zero),Is.True);
                Assert.That(editor.Find(c).GetComponent<RigidRoomItem>().Launch(Vector3.right*2,Vector3.zero),Is.True);Assert.That(editor.Find(d).GetComponent<RigidRoomItem>().Launch(Vector3.left*2,Vector3.zero),Is.True);
                foreach(var id in new[]{a,b,c,d})editor.RememberPhysicsStepBody(editor.Find(id).GetComponent<RigidRoomItem>());
                Physics.simulationMode=SimulationMode.Script;Physics.SyncTransforms();
                Physics.Simulate(.65f);
                Assert.That(ca.Contacts,Is.Zero,"The secondary body must not hit the guard directly");Assert.That(cb.Contacts,Is.GreaterThan(0));
                editor.ProcessDormantContacts();runtime.Scheduler.Tick(Time.unscaledTime+.001f);
                Assert.That(PhysicsStepState(affected,"seen"),Is.EqualTo("True"));Assert.That(PhysicsStepState(affected,"interrupted"),Is.EqualTo("True"));
                Assert.That(PhysicsStepState(independent,"seen"),Is.EqualTo("True"));Assert.That(PhysicsStepState(independent,"interrupted"),Is.EqualTo("False"));
                Assert.That((bool)PhysicsInputFact(a)["interrupted"],Is.True);Assert.That((bool)PhysicsInputFact(b)["interrupted"],Is.True);Assert.That((bool)PhysicsInputFact(c)["interrupted"],Is.False);
                Debug.Log("MAESTRO_PHYSICS_STEP native secondary impact observed; guard hits="+ca.Contacts+","+cb.Contacts);
            }finally{Physics.simulationMode=mode;Physics.gravity=gravity;}
            yield return NavigationLoadingDone();
        }
        [UnityTest]public IEnumerator PhysicsStepSharedFixedContactDoesNotMergeIndependentMovingBodies(){
            string a=PhysicsInputBall(new(-10,2,-10)),b=PhysicsInputBall(new(10,2,-10)),target=RecoveryObstacle(new(15,2,15));StartRecoveryFixture();
            var ra=editor.Find(a).GetComponent<RigidRoomItem>();var rb=editor.Find(b).GetComponent<RigidRoomItem>();var floor=editor.Find("book").GetComponentInChildren<Collider>();
            editor.RememberPhysicsStepBody(ra);editor.RememberPhysicsStepBody(rb);editor.RecordPhysicsContact(ra,floor);editor.RecordPhysicsContact(rb,floor);QueueRecovery(a,target);editor.ProcessDormantContacts();
            Assert.That((bool)PhysicsInputFact(a)["interrupted"],Is.True);Assert.That((bool)PhysicsInputFact(b)["interrupted"],Is.False);Assert.That(rb.Simulating,Is.True);yield return NavigationLoadingDone();
        }
        [UnityTest]public IEnumerator PhysicsStepRecorderRunsAfterLateFixedStepPropRelease(){
            string id=PhysicsInputBall(new(-10,2,-10));var body=editor.Find(id).GetComponent<RigidRoomItem>();
            var early=root.AddComponent<PhysicsStepEarlyProbe>();early.Editor=editor;early.Body=body;
            var late=root.AddComponent<PhysicsStepLateRelease>();late.Body=body;body.SetAnimationOwner(late,true);StartRecoveryFixture();
            yield return new WaitForFixedUpdate();Assert.That(late.Released,Is.True);Assert.That(body.Simulating,Is.True);
            Assert.That(editor.PhysicsStepDynamic(body),Is.True,"The final record must replace an early kinematic observation");
        }
        [UnityTest]public IEnumerator PhysicsStepPauseRestartCancelsAnUndeliveredObservation(){
            string id=PhysicsInputBall(new(-10,2,-10)),listener=PhysicsStepListener("object.collided",id);StartRecoveryFixture();
            var observation=new NativePhysicsObservation();var fields=new JObject{["otherId"]="book",["otherKind"]="object",["speed"]=1,["x"]=0,["y"]=0,["z"]=0,["interrupted"]=false};
            Assert.That(runtime.Scheduler.EmitNativeObservation("object.collided",id,new Maestro.Quest.Programs.ProgramValue(id),fields,Time.unscaledTime,observation,out _),Is.True);editor.ObservePhysicsEvent(id,"book",observation);
            physics.PausePhysics();physics.StartPhysics();editor.ProcessDormantContacts();runtime.Scheduler.Tick(Time.unscaledTime+.001f);
            Assert.That(observation.Cancelled,Is.True);Assert.That(PhysicsStepState(listener,"seen"),Is.EqualTo("False"));yield return null;
        }
    }
    [DefaultExecutionOrder(200)]internal sealed class PhysicsStepEarlyProbe:MonoBehaviour {
        internal RoomEditor Editor;internal RigidRoomItem Body;
        void FixedUpdate(){if(Editor&&Body)Editor.RememberPhysicsStepBody(Body);}
    }
    [DefaultExecutionOrder(250)]internal sealed class PhysicsStepLateRelease:MonoBehaviour {
        internal RigidRoomItem Body;internal bool Released;
        void FixedUpdate(){if(Body&&!Released){Body.SetAnimationOwner(this,false);Released=true;}}
    }
}
