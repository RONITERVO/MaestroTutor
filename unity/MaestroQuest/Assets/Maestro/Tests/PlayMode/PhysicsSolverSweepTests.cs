// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        // Manual native steps make the exact pre/post interval observable. This
        // exercises PhysX with production bodies/joints; it is not a solver proof.
        JObject SolverSweep(string name,string[] ids,int steps,Action<int> before=null){
            var previous=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
            var items=ids.Select(editor.Find).ToArray();var bodies=items.Select(x=>x.GetComponent<Rigidbody>()).ToArray();
            int contacts=0;float speed=0,spin=0,displacement=0,minMargin=float.PositiveInfinity;
            Action<RoomItem,Collider,Vector3,float> contact=(_,__,___,____)=>contacts++;
            foreach(var item in items)item.GetComponent<RigidRoomItem>().ContactStarted+=contact;
            try{
                for(int tick=0;tick<steps;tick++){
                    before?.Invoke(tick);Physics.SyncTransforms();
                    foreach(var item in items)item.GetComponent<RoomConnectionView>()?.Refresh();
                    foreach(var item in items)item.GetComponent<RigidRoomItem>().Refresh();
                    var admitted=new Bounds[items.Length];var positions=bodies.Select(x=>x.position).ToArray();
                    for(int i=0;i<items.Length;i++){
                        Assert.That(bodies[i].isKinematic,Is.False,name+" must actually simulate");
                        Assert.That(RoomPhysicsSweep.Bounds(items[i],bodies[i],Time.fixedDeltaTime,out admitted[i],out _),Is.True);
                    }
                    Physics.Simulate(Time.fixedDeltaTime);
                    for(int i=0;i<items.Length;i++){
                        speed=Mathf.Max(speed,bodies[i].linearVelocity.magnitude);spin=Mathf.Max(spin,bodies[i].angularVelocity.magnitude);
                        displacement=Mathf.Max(displacement,Vector3.Distance(positions[i],bodies[i].position));
                        foreach(var collider in items[i].Grab.colliders.Where(x=>!x.isTrigger)){
                            var actual=collider.bounds;var margin=Vector3.Min(actual.min-admitted[i].min,admitted[i].max-actual.max);
                            minMargin=Mathf.Min(minMargin,margin.x,margin.y,margin.z);
                            Assert.That(admitted[i].Contains(actual.min)&&admitted[i].Contains(actual.max),Is.True,name+" left its prepared bounds at step "+tick+": "+actual+" vs "+admitted[i]);
                        }
                    }
                }
            }finally{
                Physics.simulationMode=previous;
                foreach(var item in items)if(item)item.GetComponent<RigidRoomItem>().ContactStarted-=contact;
            }
            var result=new JObject{["scenario"]=name,["steps"]=steps,["secondsPerStep"]=Time.fixedDeltaTime,["contacts"]=contacts,["maximumSpeed"]=speed,["maximumSpin"]=spin,["maximumStepDisplacement"]=displacement,["minimumBoundsMargin"]=minMargin};
            Debug.Log("MAESTRO_SOLVER_SWEEP "+result.ToString(Newtonsoft.Json.Formatting.None));return result;
        }
        [UnityTest]public IEnumerator SolverSweepOpposedMaximumMassRatioImpact(){
            string heavy=PhysicsInputBall(new(3.5f,4,4)),light=PhysicsInputBall(new(4.5f,4,4));
            Assert.That(editor.SetItemPhysics(heavy,new(){mode="bouncy",shape="sphere",mass=20}),Is.True);
            Assert.That(editor.SetItemPhysics(light,new(){mode="bouncy",shape="sphere",mass=.05f}),Is.True);StartIsland();
            Assert.That(editor.Find(heavy).GetComponent<RigidRoomItem>().Launch(Vector3.right*15,Vector3.zero),Is.True);
            Assert.That(editor.Find(light).GetComponent<RigidRoomItem>().Launch(Vector3.left*15,Vector3.zero),Is.True);
            var result=SolverSweep("opposed impact, 400:1 mass",new[]{heavy,light},20);Assert.That((int)result["contacts"],Is.GreaterThan(0));yield return null;
        }
        [UnityTest]public IEnumerator SolverSweepOverlappingMaximumMassRatioBodies(){
            string heavy=PhysicsInputBall(new(4,4,4)),light=PhysicsInputBall(new(4.1f,4,4));
            Assert.That(editor.SetItemPhysics(heavy,new(){mode="solid",shape="sphere",mass=20}),Is.True);
            Assert.That(editor.SetItemPhysics(light,new(){mode="solid",shape="sphere",mass=.05f}),Is.True);StartIsland();
            var result=SolverSweep("overlap recovery, 400:1 mass",new[]{heavy,light},20);Assert.That((int)result["contacts"],Is.GreaterThan(0));yield return null;
        }
        [UnityTest]public IEnumerator SolverSweepOffsetColliderWithMaximumSpinAndLaunch(){
            string id=PhysicsInputBall(new(4,4,4));var item=editor.Find(id);var body=item.GetComponent<Rigidbody>();
            item.GetComponentInChildren<SphereCollider>().center=new(2,0,0);body.centerOfMass=Vector3.zero;Physics.SyncTransforms();StartIsland();
            Assert.That(item.GetComponent<RigidRoomItem>().Launch(Vector3.up*15,Vector3.forward*30),Is.True);
            var result=SolverSweep("offset collider spin",new[]{id},20);Assert.That((float)result["maximumSpin"],Is.GreaterThan(20));yield return null;
        }
        [UnityTest]public IEnumerator SolverSweepStrongScaledSliderSpring(){
            var(id,_,view)=SliderStage(new(){minimum=-1,maximum=1,target=1,mode="spring",spring=500,damper=0,force=100},scale:4);
            physics.PausePhysics();yield return null;Assert.That(editor.SetItemPhysics(id,new(){mode="solid",shape="box",mass=.05f}),Is.True,editor.Status);physics.StartPhysics();view.Refresh();Assert.That(view.Active,Is.True,view.Error);
            var result=SolverSweep("scaled slider at maximum spring and force",new[]{id},30);Assert.That((float)result["maximumStepDisplacement"],Is.GreaterThan(.001f));yield return null;
        }
        [UnityTest]public IEnumerator SolverSweepMaximumHingeMotor(){
            var(id,_,view)=HingeStage(new(){mode="motor",speed=360,force=20});
            physics.PausePhysics();yield return null;Assert.That(editor.SetItemPhysics(id,new(){mode="solid",shape="box",mass=.05f}),Is.True,editor.Status);physics.StartPhysics();view.Refresh();Assert.That(view.Active,Is.True,view.Error);
            var result=SolverSweep("maximum hinge motor",new[]{id},30);Assert.That((float)result["maximumSpin"],Is.GreaterThan(1));yield return null;
        }
        [UnityTest]public IEnumerator SolverSweepSleepingBodyWakesUnderImpact(){
            string incoming=PhysicsInputBall(new(3.5f,4,4)),sleeping=PhysicsInputBall(new(4.5f,4,4));StartIsland();
            var sleeper=editor.Find(sleeping).GetComponent<Rigidbody>();sleeper.Sleep();Assert.That(sleeper.IsSleeping(),Is.True);
            Assert.That(editor.Find(incoming).GetComponent<RigidRoomItem>().Launch(Vector3.right*15,Vector3.zero),Is.True);
            var result=SolverSweep("sleeping body impact",new[]{incoming,sleeping},20);Assert.That((int)result["contacts"],Is.GreaterThan(0));Assert.That(sleeper.IsSleeping(),Is.False);yield return null;
        }
        [UnityTest]public IEnumerator SolverSweepContinuousTrackedPusher(){
            string id=PhysicsInputBall(new(4,4,4));StartIsland();var pusher=ContactPusher(new(3.25f,4,4),out var hand);
            pusher.SendMessage("FixedUpdate");pusher.SendMessage("FixedUpdate");Assert.That(pusher.GetComponent<SphereCollider>().enabled,Is.True,pusher.ObserveInputs().ToString());
            var result=SolverSweep("near-limit continuous tracked push",new[]{id},10,tick=>{hand.transform.position=new Vector3(3.25f+.49f*(tick+1),4,4);pusher.SendMessage("FixedUpdate");});
            Assert.That((int)result["contacts"],Is.GreaterThan(0));yield return null;
        }
        [UnityTest]public IEnumerator SolverSweepFixedChainRedistributesMaximumLaunchAcrossMassRatios(){
            var ids=PhysicsIslandChain();
            for(int i=0;i<ids.Length;i++)Assert.That(editor.SetItemPhysics(ids[i],new(){mode="solid",shape="box",mass=i==0?20:.05f}),Is.True,editor.Status);
            StartIsland();foreach(var id in ids)editor.Find(id).GetComponent<RoomConnectionView>()?.Refresh();
            Assert.That(editor.Find(ids[0]).GetComponent<RigidRoomItem>().Launch(Vector3.up*15,Vector3.forward*30),Is.True);
            var result=SolverSweep("fixed chain, 400:1 mass and spin",ids,30);
            Assert.That((float)result["maximumStepDisplacement"],Is.GreaterThan(.01f));
            Assert.That(editor.Find(ids[0]).GetComponent<RoomConnectionView>().Active,Is.True);yield return null;
        }
        [UnityTest]public IEnumerator SolverSweepBrokenJointChangesTheIslandDuringNativeSimulation(){
            var ids=PhysicsIslandChain();var link=editor.Read(ids[0]).connections.Single();link.breakForce=.1f;
            Assert.That(editor.EditConnection(ids[0],editor.ObjectRevision(ids[0]),"configure",ids[1],link,0,out var error),Is.True,error);StartIsland();
            foreach(var id in ids)editor.Find(id).GetComponent<RoomConnectionView>()?.Refresh();
            Assert.That(editor.Find(ids[0]).GetComponent<RigidRoomItem>().Launch(Vector3.up*15,Vector3.zero),Is.True);
            SolverSweep("native fixed-joint break",ids,30);
            Assert.That(editor.Find(ids[0]).GetComponent<RoomConnectionView>().Broken,Is.True);Assert.That(editor.PhysicsIslandMembers(editor.Find(ids[0])),Is.EquivalentTo(new[]{ids[0]}));yield return null;
        }
        [UnityTest]public IEnumerator SolverSweepRealBuoyancyAndDragWithLightSpinningBody(){
            var(_,id)=MediumFixture(.05f);int applied=0;
            Assert.That(editor.Find(id).GetComponent<RigidRoomItem>().Launch(Vector3.right*3,Vector3.forward*30),Is.True);
            var result=SolverSweep("light spinning body in real liquid forces",new[]{id},30,_=>{
                editor.Liquids.TickMedium(Time.fixedDeltaTime);if((bool)editor.Liquids.ObserveMediumBody(id)["applied"])applied++;
            });
            Assert.That(applied,Is.GreaterThan(0));Assert.That((float)result["maximumStepDisplacement"],Is.GreaterThan(.001f));yield return null;
        }
        [UnityTest]public IEnumerator SolverSweepAutomaticFixedUpdatesContainActualImpactAndRefreshInputs(){
            Assert.That(Physics.simulationMode,Is.EqualTo(SimulationMode.FixedUpdate));
            string heavy=PhysicsInputBall(new(3.5f,4,4)),light=PhysicsInputBall(new(4.5f,4,4));
            Assert.That(editor.SetItemPhysics(heavy,new(){mode="bouncy",shape="sphere",mass=20}),Is.True);
            Assert.That(editor.SetItemPhysics(light,new(){mode="bouncy",shape="sphere",mass=.05f}),Is.True);StartIsland();
            var items=new[]{editor.Find(heavy),editor.Find(light)};var observer=root.AddComponent<SolverSweepObserver>();observer.Items=items;
            int contacts=0;items[1].GetComponent<RigidRoomItem>().ContactStarted+=(_,__,___,____)=>contacts++;
            Assert.That(items[0].GetComponent<RigidRoomItem>().Launch(Vector3.right*15,Vector3.zero),Is.True);
            Assert.That(items[1].GetComponent<RigidRoomItem>().Launch(Vector3.left*15,Vector3.zero),Is.True);
            float speed=0,step=0;
            for(int tick=0;tick<20;tick++){
                int before=observer.Steps;yield return new WaitForFixedUpdate();
                Assert.That(observer.Steps,Is.EqualTo(before+1),"Observe exactly one real automatic step");Assert.That(observer.Error,Is.Null);
                for(int i=0;i<items.Length;i++){
                    var body=items[i].GetComponent<Rigidbody>();Assert.That(body.isKinematic,Is.False);
                    speed=Mathf.Max(speed,body.linearVelocity.magnitude);step=Mathf.Max(step,Vector3.Distance(observer.Positions[i],body.position));
                    foreach(var collider in items[i].Grab.colliders.Where(x=>!x.isTrigger))Assert.That(observer.Coverage[i].Contains(collider.bounds.min)&&observer.Coverage[i].Contains(collider.bounds.max),Is.True,"Automatic step escaped prepared bounds");
                    Assert.That((bool)PhysicsInputFact(i==0?heavy:light)["ready"],Is.True);
                }
            }
            Assert.That(contacts,Is.GreaterThan(0));Debug.Log("MAESTRO_SOLVER_AUTOMATIC "+new JObject{["steps"]=observer.Steps,["contacts"]=contacts,["maximumSpeed"]=speed,["maximumStepDisplacement"]=step}.ToString(Newtonsoft.Json.Formatting.None));
        }
    }
    [DefaultExecutionOrder(32000)]
    internal sealed class SolverSweepObserver:MonoBehaviour {
        internal RoomItem[] Items;internal Bounds[] Coverage;internal Vector3[] Positions;internal int Steps;internal string Error;
        void FixedUpdate(){
            if(Items==null)return;Coverage=new Bounds[Items.Length];Positions=new Vector3[Items.Length];Steps++;
            for(int i=0;i<Items.Length;i++){
                var body=Items[i].GetComponent<Rigidbody>();Positions[i]=body.position;
                if(body.isKinematic||!RoomPhysicsSweep.Bounds(Items[i],body,Time.fixedDeltaTime,out Coverage[i],out _))Error="The automatic step did not admit the body";
            }
        }
    }
}
