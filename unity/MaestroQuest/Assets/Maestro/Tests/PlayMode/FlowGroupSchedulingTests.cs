// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;
using Maestro.Quest.Diagnostics;
using Maestro.Quest.Interaction;
using UnityEngine;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  [UnityTest]public IEnumerator FlowGroupSchedulingRealIndependentCircuitsFitTheirOwnExecutionAllowance(){
   var(a,b,c,d)=IndependentFlowCups();physics.StartPhysics();FlowWorkResources.Release(editor.Liquids);FlowWorkResources.Advance(.05f);
   int required=2*FlowWorkResources.Demand(2);
   Assert.That(required,Is.LessThan(FlowWorkResources.Demand(4)));
   var pressure=SimulationWorkSchedule.Register(()=>true,0,0);pressure.FlowUnits=FlowWorkResources.MaximumUnits-required;
   try{
    Assert.That(SimulationWorkSchedule.TryAdmit(pressure),Is.True);
    editor.Liquids.Tick(.05f);
    Assert.That(FlowAmount(b),Is.GreaterThan(0),"A ready two-vessel circuit must not pay for the unrelated circuit");
    Assert.That(FlowAmount(d),Is.GreaterThan(0));
    Assert.That(editor.Liquids.LastFlowExecution.Groups,Is.EqualTo(2));
    Assert.That(FlowAmount(a)+FlowAmount(b)+FlowAmount(c)+FlowAmount(d),Is.EqualTo(800).Within(1e-8));
    Assert.That((long)SimulationWorkSchedule.ObserveLedger()["committed"]["liquidUnits"],Is.EqualTo(FlowWorkResources.MaximumUnits));
   }finally{SimulationWorkSchedule.Remove(pressure);physics.PausePhysics();FlowWorkResources.Advance(.05f);}
   editor.Undo();Assert.That(FlowAmount(a),Is.EqualTo(400));Assert.That(FlowAmount(c),Is.EqualTo(400));Assert.That(FlowAmount(b)+FlowAmount(d),Is.Zero);
   yield return null;
  }
  [UnityTest]public IEnumerator FlowGroupSchedulingFreshPlacementInvalidatesPriorAdmissionAndKeepsCanonicalService(){
   var(a,b,c,d)=IndependentFlowCups();physics.StartPhysics();
   Assert.That(FlowWorkResources.TryAdmit(editor.Liquids,out var error),Is.True,error);
   var ids=new[]{a,b,c,d};var services=ids.Select(id=>editor.Liquids.FlowService(id)).ToArray();
   Assert.That(ids.All(id=>FlowWorkResources.Ready(editor.Liquids,id,out _)),Is.True);
   editor.Find(b).transform.position=editor.Find(d).transform.position+Vector3.right*.03f;
   Assert.That(ids.All(id=>!FlowWorkResources.Ready(editor.Liquids,id,out _)),Is.True,"A grant cannot authorize a stale placement");
   editor.Liquids.Tick(.05f);
   Assert.That(editor.Liquids.LastFlowExecution.Groups,Is.EqualTo(2));
   Assert.That(editor.Liquids.LastFlowExecution.ScoopCandidates,Is.EqualTo(10),"Recapture actual 1+3 closure");
   for(int i=0;i<ids.Length;i++)Assert.That(editor.Liquids.FlowService(ids[i]),Is.SameAs(services[i]));
   Assert.That(FlowAmount(a)+FlowAmount(b)+FlowAmount(c)+FlowAmount(d)+ids.Sum(id=>FlowAmount(id,"spilledMl")),Is.EqualTo(800).Within(1e-8));
   physics.PausePhysics();yield return null;
  }
  [UnityTest]public IEnumerator FlowGroupSchedulingAutomaticPassCannotRepeatMembersAfterRegroupOrRequestRecreation(){
   var(a,b,c,d)=IndependentFlowCups();physics.StartPhysics();FlowWorkResources.Advance(.05f);
   editor.Liquids.TickPass(.05f,true);Assert.That(FlowAmount(b),Is.GreaterThan(0));
   double left=FlowAmount(b),right=FlowAmount(d);var service=editor.Liquids.FlowService(a);
   FlowWorkResources.Release(editor.Liquids);editor.Liquids.TickPass(.05f,true);
   Assert.That(editor.Liquids.LastFlowExecution.Groups,Is.Zero);Assert.That(FlowAmount(b),Is.EqualTo(left));Assert.That(FlowAmount(d),Is.EqualTo(right));
   editor.Find(b).transform.position=editor.Find(d).transform.position+Vector3.right*.03f;
   editor.Liquids.TickPass(.05f,true);Assert.That(editor.Liquids.LastFlowExecution.Groups,Is.Zero);
   Assert.That(FlowAmount(d),Is.EqualTo(right));Assert.That(editor.Liquids.FlowService(a),Is.SameAs(service));
   FlowWorkResources.Advance(.05f);editor.Liquids.TickPass(.05f,true);
   Assert.That(editor.Liquids.LastFlowExecution.Groups,Is.EqualTo(2));physics.PausePhysics();yield return null;
  }
  [UnityTest]public IEnumerator FlowGroupSchedulingNativeRetirementPreservesServiceButCanonicalDeletionDoesNot(){
   var(a,b)=FixedFlowCups();physics.StartPhysics();Assert.That(FlowWorkResources.TryAdmit(editor.Liquids,out var error),Is.True,error);
   var service=editor.Liquids.FlowService(b);long order=service.Order;physics.PausePhysics();
   var view=editor.Find(b);Assert.That(editor.RetireNativeArea(NativeAreaFor(b),out error),Is.True,error);yield return null;yield return null;
   Assert.That(view==null,Is.True);Assert.That(editor.Liquids.FlowService(b),Is.SameAs(service));
   physics.StartPhysics();editor.Liquids.Tick(.05f);yield return NavigationLoadingDone();editor.Liquids.Tick(.05f);editor.Liquids.Tick(.05f);
   Assert.That(FlowAmount(b),Is.GreaterThan(0));Assert.That(editor.Liquids.FlowService(b),Is.SameAs(service));Assert.That(service.Order,Is.EqualTo(order));
   physics.PausePhysics();Assert.That(editor.DeleteObject(b,out error),Is.True,error);editor.Undo();
   Assert.That(editor.Liquids.FlowService(b),Is.Not.SameAs(service),"Explicit deletion/Undo recreates the canonical service identity");
  }
  [UnityTest]public IEnumerator FlowGroupSchedulingSeparateMovingContainersTakeIndependentCompleteTurns(){
   editor.Liquids.enabled=false;var ex=new RoomAgentExecutor(editor);
   var ids=new[]{PhysicsInputBall(new Vector3(-8,3,-8)),PhysicsInputBall(new Vector3(8,3,-8))};
   foreach(var id in ids)ContainerRun(ex,ContainerCall(id,new RoomContainer{capacityMl=500,radius=.05f,height=.1f}));
   StartIsland();yield return IslandActive(ids);var bodies=ids.Select(id=>editor.Find(id).GetComponent<RigidRoomItem>()).ToArray();
   FlowWorkResources.Release(editor.Liquids);foreach(var body in bodies)PhysicsWorkResources.Release(body);FlowWorkResources.Advance(.05f);
   var pressure=SimulationWorkSchedule.Register(()=>true,0,0);pressure.FlowUnits=FlowWorkResources.MaximumUnits-FlowWorkResources.Demand(1);
   try{
    Assert.That(SimulationWorkSchedule.TryAdmit(pressure),Is.True);
    foreach(var body in bodies){body.GetComponent<Rigidbody>().linearVelocity=Vector3.right*3;body.Refresh();}
    Assert.That(bodies.Count(body=>body.Simulating),Is.EqualTo(1),"Unrelated moving containers must not need a two-container allowance");
    var waiting=bodies.Single(body=>!body.Simulating);Assert.That((bool)waiting.ObservePhysicsInputs()["momentumHeld"],Is.True);
    Assert.That(waiting.FlowPointVelocity(waiting.transform.position).x,Is.GreaterThan(2),"Discovery includes the trajectory held during a scheduling delay");
    for(int i=0;i<3&&!waiting.Simulating;i++){FlowWorkResources.Advance(.05f);SimulationWorkSchedule.TryAdmit(pressure);foreach(var body in bodies)body.Refresh();}
    Assert.That(waiting.Simulating,Is.True);Assert.That(waiting.GetComponent<Rigidbody>().linearVelocity.x,Is.GreaterThan(2));
    foreach(var body in bodies)Assert.That(PhysicsWorkResources.Ready(body,out _),Is.EqualTo(FlowWorkResources.Ready(editor.Liquids,body.PhysicsWorkOwner.Target,out _)));
   }finally{SimulationWorkSchedule.Remove(pressure);physics.PausePhysics();}
  }
  [UnityTest]public IEnumerator FlowGroupSchedulingSharedFactsExposeBoundedGroupsAndPassiveMemberReads(){
   root.AddComponent<RuntimeDiagnostics>();var(a,b,c,d)=IndependentFlowCups();physics.StartPhysics();editor.Liquids.Tick(.05f);
   var schedule=CollisionFact("runtime.flowGroupSchedule");Assert.That((int)schedule["groups"],Is.EqualTo(2));Assert.That((int)schedule["ready"],Is.EqualTo(2));
   var groups=new JArray();var members=new JArray();long step=FlowWorkResources.Step;double quantity=FlowAmount(b)+FlowAmount(d);int revision=editor.Revision;
   for(int i=0;i<2;i++){
    var group=CollisionFact("runtime.flowGroup",new JObject{["index"]=i});Assert.That((int)group["members"],Is.EqualTo(2));Assert.That((int)group["units"],Is.EqualTo(FlowWorkResources.Demand(2)));groups.Add(group);
    for(int j=0;j<2;j++)members.Add(CollisionFact("runtime.flowGroupMember",new JObject{["index"]=i,["memberIndex"]=j}));
   }
   Assert.That(members.Select(x=>(string)x["target"]),Is.EquivalentTo(new[]{a,b,c,d}));
   Assert.That(FlowWorkResources.Step,Is.EqualTo(step));Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(FlowAmount(b)+FlowAmount(d),Is.EqualTo(quantity));
   Assert.That(FlowWorkResources.ObserveGroup(2),Is.Null);Assert.That(FlowWorkResources.ObserveGroupMember(0,2),Is.Null);
   groups[0]["units"]=0;Assert.That((int)CollisionFact("runtime.flowGroup",new JObject{["index"]=0})["units"],Is.GreaterThan(0));
   groups[0]=CollisionFact("runtime.flowGroup",new JObject{["index"]=0});
   var before=FlowWorkResources.ObserveGroup(0);string first=(string)before["firstMember"];
   foreach(var member in members)Assert.That((long)member["membershipRevision"],Is.EqualTo((long)before["membershipRevision"]));
   var current=members.Where(member=>(string)member["firstMember"]==first).Select(member=>(string)member["target"]).ToArray();
   foreach(var id in new[]{a,b,c,d}.Except(current))editor.Find(id).transform.position=editor.Find(current[0]).transform.position+Vector3.right*.01f;
   Assert.That(FlowWorkResources.TryAdmit(editor.Liquids,out var issue),Is.True,issue);
   var after=FlowWorkResources.ObserveGroup(0);var afterMember=FlowWorkResources.ObserveGroupMember(0,0);
   Assert.That((long)after["step"],Is.EqualTo((long)before["step"]));Assert.That((string)after["reservationId"],Is.EqualTo((string)before["reservationId"]));
   Assert.That((string)after["firstMember"],Is.EqualTo(first));Assert.That((int)after["members"],Is.EqualTo(4));
   Assert.That((long)after["membershipRevision"],Is.GreaterThan((long)before["membershipRevision"]),"Same-step regrouping must invalidate a paged membership read even when its first member stays the same");
   Assert.That((long)afterMember["membershipRevision"],Is.EqualTo((long)after["membershipRevision"]));
   Assert.That(FlowWorkResources.TryAdmit(editor.Liquids,out issue),Is.True,issue);
   Assert.That((long)FlowWorkResources.ObserveGroup(0)["membershipRevision"],Is.EqualTo((long)after["membershipRevision"]),"Repeated preparation of unchanged groups does not invalidate their identity");
   var regroup=new JObject{["before"]=before,["after"]=after,["member"]=afterMember};
   string output=Environment.GetEnvironmentVariable("MAESTRO_FLOW_GROUP_WORK_EVIDENCE");
   if(!string.IsNullOrEmpty(output))File.WriteAllText(output,new JObject{["schedule"]=schedule,["groups"]=groups,["members"]=members,["regroup"]=regroup}.ToString());
   physics.PausePhysics();yield return null;
  }
  [UnityTest]public IEnumerator FlowGroupSchedulingMovingPlacementRetainsFairReservationButRequiresFreshExecution(){
   var(a,b,c,d)=IndependentFlowCups();physics.StartPhysics();FlowWorkResources.Release(editor.Liquids);FlowWorkResources.Advance(.05f);
   var pressure=SimulationWorkSchedule.Register(()=>true,0,0);pressure.FlowUnits=FlowWorkResources.MaximumUnits-FlowWorkResources.Demand(2);
   try{
    Assert.That(SimulationWorkSchedule.TryAdmit(pressure),Is.True);editor.Liquids.Tick(.05f);
    Assert.That(new[]{b,d}.Count(id=>FlowAmount(id)>0),Is.EqualTo(1));
    string waiting=FlowAmount(b)==0?b:d;
    for(int i=0;i<3&&FlowAmount(waiting)==0;i++){
     foreach(var id in new[]{a,b,c,d})editor.Find(id).transform.position+=Vector3.forward*.0001f;
     Assert.That(FlowWorkResources.Ready(editor.Liquids,out _),Is.False,"Previous geometry cannot authorize execution");
     FlowWorkResources.Advance(.05f);
     Assert.That(FlowWorkResources.ExecutionClaims(editor.Liquids).Any(claim=>claim.Granted),Is.True,"Fair reservations must survive normal motion between opportunities");
     Assert.That(FlowWorkResources.Ready(editor.Liquids,out _),Is.False,"Booking does not make stale geometry current");
     SimulationWorkSchedule.TryAdmit(pressure);editor.Liquids.Tick(.05f);
    }
    Assert.That(FlowAmount(waiting),Is.GreaterThan(0),"Moving geometry cannot force admission back to caller order every opportunity");
   }finally{SimulationWorkSchedule.Remove(pressure);physics.PausePhysics();}
   yield return null;
  }
 }
}
