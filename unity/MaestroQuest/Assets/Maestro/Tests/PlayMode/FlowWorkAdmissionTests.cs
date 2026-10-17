// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Diagnostics;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  // Logical contenders isolate allowance pressure from native geometry. Actual
  // multi-room/native allocation is covered separately by FlowNativePressure.
  List<SimulationWorkSchedule.Claim> FillFlowWork(){
   var claims=new List<SimulationWorkSchedule.Claim>();int demand=FlowWorkResources.Demand(editor.Liquids.FlowWorkVessels);
   try{while((int)SimulationWorkSchedule.ObserveLedger()["committed"]["liquidPasses"]<FlowWorkResources.MaximumExecutions&&FlowWorkResources.MaximumUnits-(int)SimulationWorkSchedule.ObserveLedger()["committed"]["liquidUnits"]>=demand){
    var claim=SimulationWorkSchedule.Register(()=>true,0,1);claim.FlowUnits=demand;claims.Add(claim);
    Assert.That(SimulationWorkSchedule.TryAdmit(claim),Is.True);
   }return claims;}catch{ReleaseFlowWork(claims);throw;}
  }
  static void ReleaseFlowWork(IEnumerable<SimulationWorkSchedule.Claim> claims){foreach(var claim in claims)SimulationWorkSchedule.Remove(claim);}
  static void ReleaseFlowWork(IEnumerable<GameObject> hosts){foreach(var go in hosts)if(go)UnityEngine.Object.DestroyImmediate(go);FlowWorkResources.Collect();}
  JObject FlowGrant(){Assert.That((int)FlowWorkResources.ObserveBudget()["reservations"],Is.EqualTo(1));return FlowWorkResources.Observe(0);}
  [UnityTest]public IEnumerator FlowWorkAdmissionRefusesPourWithoutQuantityWriteOrCatchUp(){
   var(_,from,to)=PouringCups();FlowWorkResources.Release(editor.Liquids);var held=FillFlowWork();int events=0;editor.ContainerPoured+=(_,_,_,_,_)=>events++;
   int revision=editor.Revision;string saved=JsonUtility.ToJson(editor.Snapshot());
   try{for(int i=0;i<50;i++)editor.Liquids.Tick(.1f);Assert.That(Liquid(from),Is.EqualTo(400));Assert.That(Liquid(to),Is.Zero);Assert.That(events,Is.Zero);Assert.That(editor.Liquids.Active,Is.False);Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);
    Assert.That((string)editor.Liquids.Observe(from)["phase"],Is.EqualTo("waiting"));StringAssert.Contains("work capacity",(string)editor.Liquids.Observe(from)["error"]);
    Assert.That(editor.Liquids.GetComponentsInChildren<LineRenderer>().Any(x=>x.enabled),Is.False);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
   }finally{ReleaseFlowWork(held);}
   editor.Liquids.Tick(.05f);double received=Liquid(to);Assert.That(received,Is.GreaterThan(0));Assert.That(received,Is.LessThanOrEqualTo(editor.Read(from).containers[0].capacityMl*.75*.05+.00001));Assert.That(Liquid(from)+received,Is.EqualTo(400).Within(1e-8));physics.PausePhysics();Assert.That(events,Is.EqualTo(1));yield return null;
  }
  [UnityTest]public IEnumerator FlowWorkAdmissionRefusesScoopAndRetriesOnlyCurrentInterval(){
   var(_,basin,bucket)=ScoopingVessels();FlowWorkResources.Release(editor.Liquids);var held=FillFlowWork();
   try{for(int i=0;i<30;i++)editor.Liquids.Tick(.1f);Assert.That(Liquid(basin),Is.EqualTo(32000));Assert.That(Liquid(bucket),Is.Zero);Assert.That((string)editor.Liquids.ObserveScooping(bucket)["phase"],Is.EqualTo("waiting"));Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);}finally{ReleaseFlowWork(held);}
   editor.Liquids.Tick(.05f);Assert.That(Liquid(bucket),Is.EqualTo(75).Within(.00001));Assert.That(Liquid(basin)+Liquid(bucket),Is.EqualTo(32000).Within(1e-8));physics.PausePhysics();yield return null;
  }
  [UnityTest]public IEnumerator FlowWorkAdmissionRainIsUnknownWhileWaitingAndNeverBackfillsSkippedTime(){
   string id=RainCup();FlowWorkResources.Release(editor.Liquids);var held=FillFlowWork();
   try{for(int i=0;i<40;i++)editor.Liquids.Tick(.1f);var rain=editor.Liquids.ObserveRain(id);Assert.That((string)rain["phase"],Is.EqualTo("waiting"));Assert.That((bool)rain["ready"],Is.False);Assert.That((double)rain["exposure"],Is.Zero);Assert.That(Liquid(id),Is.Zero);StringAssert.Contains("work capacity",(string)rain["reason"]);}finally{ReleaseFlowWork(held);}
   editor.Liquids.Tick(.1f);double expected=60*editor.Read(id).containers[0].FootprintArea*.1*1000/3600;Assert.That(Liquid(id),Is.EqualTo(expected).Within(.000001));physics.PausePhysics();yield return null;
  }
  [UnityTest]public IEnumerator FlowWorkAdmissionScheduledDelayKeepsEpisodeUntilExplicitPause(){
   var(_,from,to)=PouringCups();editor.Liquids.Tick(.05f);double accepted=Liquid(to);FlowWorkResources.Release(editor.Liquids);var held=FillFlowWork();int events=0;
   editor.ContainerPoured+=(id,amount,_,_,_)=>{Assert.That(id,Is.EqualTo(from));Assert.That(amount,Is.EqualTo(accepted));Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);Assert.That(editor.Liquids.Active,Is.False);events++;};
   try{editor.Liquids.Tick(.05f);Assert.That(events,Is.Zero);Assert.That(Liquid(to),Is.EqualTo(accepted));Assert.That(editor.Read(to).containers[0].amountMl,Is.Zero);Assert.That(editor.WriteGate.CanFreeze(out _),Is.False);editor.Liquids.Tick(.1f);Assert.That(events,Is.Zero);physics.PausePhysics();Assert.That(events,Is.EqualTo(1));}finally{ReleaseFlowWork(held);}
   physics.PausePhysics();editor.Undo();Assert.That(Liquid(from),Is.EqualTo(400));Assert.That(Liquid(to),Is.Zero);yield return null;
  }
  [UnityTest]public IEnumerator FlowWorkAdmissionWaitingEpisodeFailedSaveRollsBackAndCannotResumeUntilRestart(){
   var(_,from,to)=PouringCups();editor.Liquids.Tick(.05f);FlowWorkResources.Release(editor.Liquids);var held=FillFlowWork();int events=0;editor.ContainerPoured+=(_,_,_,_,_)=>events++;
   string pending=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(pending);
   try{editor.Liquids.Tick(.05f);Assert.That(editor.Liquids.Active,Is.True);physics.PausePhysics();Assert.That(Liquid(from),Is.EqualTo(400));Assert.That(Liquid(to),Is.Zero);Assert.That((string)editor.Liquids.Observe(to)["phase"],Is.EqualTo("failed"));Assert.That(events,Is.Zero);Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);}finally{Directory.Delete(pending);ReleaseFlowWork(held);}
   editor.Liquids.Tick(.1f);Assert.That(Liquid(to),Is.Zero);physics.PausePhysics();physics.StartPhysics();editor.Liquids.Tick(.05f);Assert.That(Liquid(to),Is.GreaterThan(0));physics.PausePhysics();yield return null;
  }
  [UnityTest]public IEnumerator FlowWorkAdmissionGrowthCannotEvictPeersAndShrinkKeepsReservation(){
   editor.Liquids.enabled=false;var ex=new RoomAgentExecutor(editor);var initial=new List<string>();
   for(int i=0;i<13;i++){
    string id=CreateTemplateFixture(TemplateCall("cup",new Vector3(3+i*.002f,1,0)));initial.Add(id);
    Assert.That(editor.SetItemPhysics(id,new(){mode="fixed",shape="box",mass=1}),Is.True,editor.Status);
   }
   StartIsland();Assert.That(FlowWorkResources.TryAdmit(editor.Liquids,out var error),Is.True,error);var original=FlowGrant();var held=FillFlowWork();
   var peer=held[0];var cost=peer.FlowUnits;var added=new List<string>();
   try{
    for(int i=0;i<3;i++){
     string id=CreateTemplateFixture(TemplateCall("cup",new Vector3(3+(13+i)*.002f,1,0)));added.Add(id);
     Assert.That(editor.SetItemPhysics(id,new(){mode="fixed",shape="box",mass=1}),Is.True,editor.Status);
     bool admitted=FlowWorkResources.TryAdmit(editor.Liquids,out _);
     Assert.That(admitted,Is.EqualTo(i<2),"The third complete-group growth exceeds the remaining allowance");
     Assert.That(peer.Granted,Is.True);Assert.That(peer.FlowUnits,Is.EqualTo(cost),"Growth cannot evict or resize peers");
    }
    foreach(var id in added)Assert.That(editor.DeleteObject(id,out error),Is.True,error);
    Assert.That(FlowWorkResources.TryAdmit(editor.Liquids,out error),Is.True,error);
    Assert.That((string)FlowWorkResources.Observe(0)["reservationId"],Is.EqualTo((string)original["reservationId"]));
    Assert.That((int)FlowWorkResources.Observe(0)["units"],Is.EqualTo(FlowWorkResources.Demand(13)));
   }finally{ReleaseFlowWork(held);}physics.PausePhysics();yield return null;
  }
  [UnityTest]public IEnumerator FlowWorkAdmissionCouplesMovingContainerGroupButLeavesUnrelatedDryBodyActive(){
   editor.Liquids.enabled=false;var ids=PhysicsIslandChain();var ex=new RoomAgentExecutor(editor);ContainerRun(ex,ContainerCall(ids[0],new RoomContainer{capacityMl=500,radius=.05f,height=.1f}));
   string dry=PhysicsInputBall(new Vector3(-8,2,-8));StartIsland();yield return IslandActive(ids);FlowWorkResources.Release(editor.Liquids);var held=FillFlowWork();
   try{foreach(var id in ids){var body=editor.Find(id).GetComponent<Rigidbody>();body.linearVelocity=Vector3.right*3;editor.Find(id).GetComponent<RigidRoomItem>().Refresh();}
    var before=editor.Find(ids[0]).transform.position;

    foreach(var id in ids){Assert.That(editor.Find(id).GetComponent<Rigidbody>().isKinematic,Is.True);Assert.That((bool)PhysicsInputFact(id)["momentumHeld"],Is.True);}
    Assert.That(Vector3.Distance(editor.Find(ids[0]).transform.position,before),Is.LessThan(.001f));Assert.That(editor.Find(dry).GetComponent<Rigidbody>().isKinematic,Is.False);Assert.That(physics.Running,Is.True);
   }finally{ReleaseFlowWork(held);}
   yield return IslandActive(ids);foreach(var id in ids)Assert.That(editor.Find(id).GetComponent<Rigidbody>().linearVelocity.x,Is.GreaterThan(.1f));physics.PausePhysics();yield return null;
  }
  [UnityTest]public IEnumerator FlowWorkAdmissionPassiveLifecycleFactsStayDetachedAndNeverRestartPhysics(){
   root.AddComponent<RuntimeDiagnostics>();string id=RainCup();Assert.That(FlowWorkResources.TryAdmit(editor.Liquids,out var error),Is.True,error);var current=new JArray(FlowGrant());
   // A synchronized accepted snapshot can add demand before the next admitted pass.
   var doc=editor.Snapshot();var source=doc.objects.Single(x=>x.id==id).Copy();source.id=Guid.NewGuid().ToString("N");doc.objects=doc.objects.Append(source).ToArray();editor.Liquids.Synchronize(doc);
   var stale=new JArray(FlowGrant());Assert.That((bool)stale[0]["current"],Is.False);Assert.That((bool)current[0]["current"],Is.True);
   int revision=editor.Revision;string saved=JsonUtility.ToJson(editor.Snapshot());var budget=CollisionFact("runtime.flowWorkBudget");var detached=CollisionFact("runtime.flowWorkBudget");detached["units"]=0;Assert.That((int)CollisionFact("runtime.flowWorkBudget")["units"],Is.GreaterThan(0));
   Assert.That(CollisionFact("runtime.flowWorkReservation",new JObject{["index"]=0}).ToString(),Is.EqualTo(stale[0].ToString()));Assert.That(FlowWorkResources.TryAdmit(editor.Liquids,out _),Is.False,"A canonical-only duplicate cannot execute without native geometry");var resized=new JArray(FlowGrant());Assert.That((int)resized[0]["vessels"],Is.EqualTo(2));Assert.That((string)resized[0]["reservationId"],Is.EqualTo((string)current[0]["reservationId"]));
   physics.PausePhysics();var released=new JArray();Assert.That((int)CollisionFact("runtime.flowWorkBudget")["reservations"],Is.Zero);Assert.That(FlowWorkResources.Observe(0),Is.Null);editor.Liquids.Tick(.1f);Assert.That(physics.Running,Is.False);
   Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
   string path=Environment.GetEnvironmentVariable("MAESTRO_FLOW_WORK_EVIDENCE");if(!string.IsNullOrEmpty(path))File.WriteAllText(path,new JObject{["budget"]=budget,["current"]=current,["stale"]=stale,["resized"]=resized,["released"]=released}.ToString());
   editor.Liquids.Synchronize(editor.Snapshot());physics.StartPhysics();Assert.That(FlowWorkResources.TryAdmit(editor.Liquids,out error),Is.True,error);editor.Liquids.SendMessage("OnApplicationFocus",false);Assert.That((int)FlowWorkResources.ObserveBudget()["reservations"],Is.Zero);editor.Liquids.SendMessage("OnApplicationFocus",true);Assert.That(physics.Running,Is.False);editor.Liquids.Tick(.1f);Assert.That((int)FlowWorkResources.ObserveBudget()["reservations"],Is.Zero);
   physics.StartPhysics();Assert.That(FlowWorkResources.TryAdmit(editor.Liquids,out error),Is.True,error);root.SetActive(false);Assert.That((int)FlowWorkResources.ObserveBudget()["reservations"],Is.Zero);root.SetActive(true);physics.PausePhysics();yield return null;
  }
  [UnityTest]public IEnumerator FlowWorkAdmissionRecoveryCallbackKeepsFreshStreamsAndRainObservation(){
   var(_,from,to)=PouringCups();Assert.That(editor.SetWeather(editor.WeatherRevision,1,new(){rainMmPerHour=60},0,out var error),Is.True,error);editor.Liquids.Tick(.05f);
   FlowWorkResources.Release(editor.Liquids);var held=FillFlowWork();int events=0;
   void Resume(){events++;if(events==1){ReleaseFlowWork(held);held.Clear();editor.Liquids.Tick(.05f);}}
   editor.ContainerPoured+=(_,_,_,_,_)=>Resume();editor.ContainerRainCollected+=(_,_)=>Resume();
   try{editor.Liquids.Tick(.05f);Assert.That(events,Is.Zero);Assert.That(editor.Liquids.Finish(out error),Is.True,error);Assert.That(events,Is.EqualTo(1));Assert.That(editor.Liquids.Active,Is.True);Assert.That((bool)editor.Liquids.ObserveRain(to)["ready"],Is.True);Assert.That((double)editor.Liquids.ObserveRain(to)["collectedMl"],Is.GreaterThan(0));Assert.That(editor.Liquids.GetComponentsInChildren<LineRenderer>().Any(x=>x.enabled),Is.True);Assert.That(Liquid(to),Is.GreaterThan(editor.Read(to).containers[0].amountMl));}finally{ReleaseFlowWork(held);}physics.PausePhysics();yield return null;
  }
  [UnityTest]public IEnumerator FlowWorkAdmissionCallbackCanBeginNextEpisodeWithoutOldCleanupErasingIt(){
   var(_,from,to)=PouringCups();Assert.That(editor.SetWeather(editor.WeatherRevision,1,new(){rainMmPerHour=60},0,out var error),Is.True,error);editor.Liquids.Tick(.05f);int events=0;
   Assert.That((double)editor.Liquids.ObserveRain(to)["collectedMl"],Is.GreaterThan(0));
   void BeginNext(){events++;if(events==1){Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);editor.Liquids.Tick(.05f);}}
   editor.ContainerPoured+=(_,_,_,_,_)=>BeginNext();editor.ContainerRainCollected+=(_,_)=>BeginNext();
   Assert.That(editor.Liquids.Finish(out error),Is.True,error);Assert.That(events,Is.EqualTo(1));Assert.That(editor.Liquids.Active,Is.True);Assert.That(editor.WriteGate.CanFreeze(out _),Is.False);Assert.That(Liquid(to),Is.GreaterThan(editor.Read(to).containers[0].amountMl));physics.PausePhysics();yield return null;
  }
  [UnityTest]public IEnumerator FlowWorkAdmissionPublicationStopsRemainingEventsWhenListenerSynchronizesSameIds(){
   var(_,from,to)=PouringCups();Assert.That(editor.SetWeather(editor.WeatherRevision,1,new(){rainMmPerHour=60},0,out var error),Is.True,error);editor.Liquids.Tick(.05f);int events=0;
   Assert.That((double)editor.Liquids.ObserveRain(to)["collectedMl"],Is.GreaterThan(0));
   void ReplaceSource(){events++;Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);editor.Liquids.Synchronize(editor.Snapshot());}
   editor.ContainerPoured+=(_,_,_,_,_)=>ReplaceSource();editor.ContainerRainCollected+=(_,_)=>ReplaceSource();
   Assert.That(editor.Liquids.Finish(out error),Is.True,error);Assert.That(events,Is.EqualTo(1),"The listener replaced the sampled source, even though IDs remained equal");physics.PausePhysics();yield return null;
  }
 }
}
