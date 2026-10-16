// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using Maestro.Quest.Diagnostics;
using Newtonsoft.Json.Linq;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  [UnityTest]public IEnumerator PhysicsFlowSchedulingServesCompleteMovingGroupUnderContinuingForcePressure(){
   var ids=PhysicsIslandChain();StartIsland();yield return IslandActive(ids);
   var bodies=ids.Select(id=>editor.Find(id).GetComponent<RigidRoomItem>()).ToArray();
   foreach(var body in bodies){body.GetComponent<Rigidbody>().linearVelocity=Vector3.right*3;PhysicsWorkResources.Release(body);}
   var held=FillPhysicsWork(1);
   try{
    foreach(var body in bodies)body.Refresh();
    Assert.That(bodies.All(x=>x.GetComponent<Rigidbody>().isKinematic),Is.True,"The pressure must initially defer the complete chain");
    bool served=false;long startStep=FlowWorkResources.Step;
    for(int i=0;i<256&&!served&&FlowWorkResources.Step-startStep<8;i++){
     foreach(var go in held)go.GetComponent<RigidRoomItem>().Refresh();
     foreach(var body in bodies)body.Refresh();
     served=bodies.All(x=>x.Simulating);
     if(served)break;
     yield return new WaitForFixedUpdate();
    }
    Assert.That(held.All(x=>x&&x.GetComponent<RigidRoomItem>().PhysicsWorkEligible),Is.True,"Continuing consumers must not disappear to free capacity");
    Assert.That(served,Is.True,"An accepted complete moving group must eventually run under continuing force pressure");
    foreach(var body in bodies)Assert.That(body.GetComponent<Rigidbody>().linearVelocity.x,Is.GreaterThan(2),"A scheduled wait must preserve the valid throw");
   }finally{ReleasePhysicsWork(held);physics.PausePhysics();}
  }
  [UnityTest]public IEnumerator PhysicsFlowSchedulingAdmitsEachCompleteMovingQuantityGroup(){
   root.AddComponent<RuntimeDiagnostics>();editor.Liquids.enabled=false;
   var ids=PhysicsIslandChain();string separate=PhysicsInputBall(new Vector3(-8,3,-8));var all=ids.Append(separate).ToArray();var ex=new RoomAgentExecutor(editor);
   foreach(var id in new[]{ids[0],separate})ContainerRun(ex,ContainerCall(id,new RoomContainer{capacityMl=500,radius=.05f,height=.1f}));
   StartIsland();yield return IslandActive(all);
   var bodies=all.Select(id=>editor.Find(id).GetComponent<RigidRoomItem>()).ToArray();
   foreach(var body in bodies){body.GetComponent<Rigidbody>().linearVelocity=Vector3.right*3;PhysicsWorkResources.Release(body);}
   var jointIds=new[]{ids[0],ids[2]}.Select(id=>editor.Find(id).GetComponent<Joint>().GetInstanceID()).ToArray();
   var motionRevisions=bodies.Select(body=>body.MotionRevision).ToArray();
   FlowWorkResources.Release(editor.Liquids);var force=FillPhysicsWork(1);var flow=FillFlowWork();var trace=new JArray();
   try{
    foreach(var body in bodies)body.Refresh();Assert.That(bodies.All(x=>!x.Simulating),Is.True);Assert.That(FlowWorkResources.Ready(editor.Liquids,out _),Is.False);
    bool served=false;long startStep=FlowWorkResources.Step;
    for(int frame=0;frame<256&&FlowWorkResources.Step-startStep<8;frame++){
     foreach(var go in (frame%2==0?force:force.AsEnumerable().Reverse()))go.GetComponent<RigidRoomItem>().Refresh();
     foreach(var go in (frame%2==0?flow:flow.AsEnumerable().Reverse()))SimulationWorkSchedule.TryAdmit(go);
     foreach(var body in bodies)body.Refresh();
     for(int i=0;i<jointIds.Length;i++)Assert.That(editor.Find(new[]{ids[0],ids[2]}[i]).GetComponent<Joint>().GetInstanceID(),Is.EqualTo(jointIds[i]),"Scheduling must retain the native joint and its drive reference");
     for(int i=0;i<bodies.Length;i++)Assert.That(bodies[i].MotionRevision,Is.EqualTo(motionRevisions[i]),$"Scheduling must not create a new contact identity: frame={frame}, body={i}, input={PhysicsInputFact(bodies[i].PhysicsWorkOwner.Target)}, joint={bodies[i].GetComponent<RoomConnectionView>()?.Phase}");
     long step=FlowWorkResources.Step;
     bool liquidReady=FlowWorkResources.Ready(editor.Liquids,out _);
     foreach(var body in bodies)Assert.That(PhysicsWorkResources.Ready(body,out _),Is.EqualTo(FlowWorkResources.Ready(editor.Liquids,body.PhysicsWorkOwner.Target==separate?separate:ids[0],out _)),"No half-admitted moving quantity group");
     if(trace.Count==0||(long)trace[trace.Count-1]["forceSchedule"]["step"]!=step||liquidReady)trace.Add(new JObject{["frame"]=frame,["liquidReady"]=liquidReady,["forceBudget"]=CollisionFact("runtime.physicsWorkBudget"),["flowBudget"]=CollisionFact("runtime.flowWorkBudget"),["forceSchedule"]=CollisionFact("runtime.physicsWorkSchedule"),["flowSchedule"]=CollisionFact("runtime.flowWorkSchedule"),["bodies"]=new JArray(bodies.Select(body=>new JObject{["target"]=body.PhysicsWorkOwner.Target,["ready"]=PhysicsWorkResources.Ready(body,out _),["groupReady"]=FlowWorkResources.Ready(editor.Liquids,body.PhysicsWorkOwner.Target==separate?separate:ids[0],out _),["simulating"]=body.Simulating,["motionHeld"]=(bool)PhysicsInputFact(body.PhysicsWorkOwner.Target)["momentumHeld"],["speedX"]=body.GetComponent<Rigidbody>().linearVelocity.x}))});
     Assert.That(FlowWorkResources.Step,Is.EqualTo(step),"Observations must not advance admission");
     served=bodies.All(x=>x.Simulating)&&liquidReady;if(served)break;
     yield return new WaitForFixedUpdate();
    }
    string output=Environment.GetEnvironmentVariable("MAESTRO_PHYSICS_FLOW_EVIDENCE");if(!string.IsNullOrEmpty(output))File.WriteAllText(output,new JObject{["forceCompetitors"]=force.Count,["flowCompetitors"]=flow.Count,["trace"]=trace}.ToString());
    Assert.That(served,Is.True,"Both resources must eventually be available in the same opportunity");
    foreach(var body in bodies)Assert.That(body.GetComponent<Rigidbody>().linearVelocity.x,Is.GreaterThan(2));
    Assert.That(force.All(go=>go&&go.GetComponent<RigidRoomItem>().PhysicsWorkEligible),Is.True);Assert.That(flow.All(SimulationWorkSchedule.Queued),Is.True);
   }finally{ReleaseFlowWork(flow);ReleasePhysicsWork(force);physics.PausePhysics();}
  }
  [UnityTest]public IEnumerator PhysicsFlowSchedulingQueueFullRefusalDiscardsMotionAndCannotReplayAnOldThrow(){
   var ids=PhysicsIslandChain();StartIsland();yield return IslandActive(ids);
   var bodies=ids.Select(id=>editor.Find(id).GetComponent<RigidRoomItem>()).ToArray();
   foreach(var body in bodies){body.GetComponent<Rigidbody>().linearVelocity=Vector3.right*3;PhysicsWorkResources.Release(body);}
   var held=FillPhysicsWork();
   try{
    while((int)PhysicsWorkResources.ObserveBudget()["reservations"]<PhysicsWorkResources.MaximumReservations){
     var go=new GameObject("Queued competing force consumer");held.Add(go);go.transform.position=new Vector3(40+held.Count,2,40);
     var collider=go.AddComponent<BoxCollider>();collider.size=Vector3.one*.01f;var item=go.AddComponent<RoomItem>();item.Configure(new Collider[]{collider});
     var rigid=go.AddComponent<RigidRoomItem>();rigid.Initialize(item);rigid.Configure(physics,ItemPhysics.Solid,1);
    }
    foreach(var body in bodies)body.Refresh();
    foreach(var id in ids){Assert.That(WorkGrants(id),Is.Empty);Assert.That((bool)PhysicsInputFact(id)["momentumHeld"],Is.False);Assert.That(editor.Find(id).GetComponent<Rigidbody>().isKinematic,Is.True);}
    Assert.That((int)PhysicsWorkResources.ObserveBudget()["reservations"],Is.EqualTo(PhysicsWorkResources.MaximumReservations));
   }finally{ReleasePhysicsWork(held);}
   yield return IslandActive(ids);foreach(var body in bodies)Assert.That(Mathf.Abs(body.GetComponent<Rigidbody>().linearVelocity.x),Is.LessThan(.05f));physics.PausePhysics();
  }
  [UnityTest]public IEnumerator PhysicsFlowSchedulingCheckpointPreservesPreparedInputsButCannotReviveAnEditedSource(){
   string id=PhysicsInputBall(new Vector3(-8,3,-8));StartIsland();yield return IslandActive(new[]{id});
   var item=editor.Find(id);var body=item.GetComponent<Rigidbody>();
   using var prepared=editor.DemandNativeEntities(new[]{id},()=>new[]{id},()=>true,followPhysicsPlacements:true);
   var strict=editor.CaptureNativeActionInput(new[]{id});int revision=editor.ObjectRevision(id);
   Assert.That(prepared.Preparation.IsCompleted,Is.True);Assert.That(prepared.Error,Is.Null);body.linearVelocity=Vector3.right*3;
   for(int i=0;i<4&&editor.ObjectRevision(id)==revision;i++){yield return new WaitForFixedUpdate();editor.SendMessage("CapturePhysicsPlacements");}
   Assert.That(editor.ObjectRevision(id),Is.Not.EqualTo(revision),"Persist an actual native physics pose");
   Assert.That(prepared.Error,Is.Null,"An owned physics checkpoint is not a new author edit");
   Assert.That(strict(),Is.False,"Ordinary agent/action guards must still reject their old snapshot");
   Assert.That(editor.SetItemPhysics(id,new(){mode="solid",shape="sphere",mass=2}),Is.True,editor.Status);
   Assert.That(prepared.Error,Is.Not.Null,"An author edit must still invalidate the prepared source");
   body.linearVelocity=Vector3.right*3;yield return new WaitForFixedUpdate();editor.SendMessage("CapturePhysicsPlacements");
   Assert.That(prepared.Error,Is.Not.Null,"A later checkpoint must not revive the edited source");physics.PausePhysics();
  }
 }
}
