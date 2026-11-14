// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  (string from,string to) FixedFlowCups(){
   var(_,from,to)=PouringCups();physics.PausePhysics();
   var values=new[]{from,to}.Select(id=>{var data=editor.Read(id);data.position=editor.Frame.PointToRoom(editor.Find(id).transform.position);data.rotation=editor.Find(id).transform.localRotation;data.physics=ItemPhysics.Fixed;return data;}).ToArray();
   Assert.That(editor.ApplyAgentEdit(editor.Revision,values,Array.Empty<string>(),out var error),Is.True,error);return(from,to);
  }
  double FlowAmount(string id,string field="amountMl")=>(double)editor.Liquids.Observe(id)["contents"][field];
  [UnityTest]public IEnumerator FlowSpatialUnavailableEmptyReceiverPreservesWaterAndRecoversWithoutBacklog(){
   var(from,to)=FixedFlowCups();var rigid=editor.Find(to).GetComponent<RigidRoomItem>();rigid.SetGeometryReady(false);physics.StartPhysics();
   for(int i=0;i<10;i++)editor.Liquids.Tick(.1f);
   Assert.That(FlowAmount(from),Is.EqualTo(400));Assert.That(FlowAmount(from,"spilledMl"),Is.Zero);Assert.That(editor.Liquids.Active,Is.False);Assert.That((string)editor.Liquids.Observe(from)["phase"],Is.EqualTo("waiting"));
   var observations=new JObject{["waiting"]=editor.Liquids.Observe(from),["scooping"]=editor.Liquids.ObserveScooping(to),["rain"]=editor.Liquids.ObserveRain(to)};
   rigid.SetGeometryReady(true);editor.Liquids.Tick(.05f);Assert.That(FlowAmount(to),Is.GreaterThan(0).And.LessThanOrEqualTo(18.75));Assert.That(FlowAmount(from)+FlowAmount(to),Is.EqualTo(400).Within(1e-8));observations["flowing"]=editor.Liquids.Observe(from);observations["receiver"]=editor.Liquids.Observe(to);physics.PausePhysics();yield return LiquidSaved();observations["accepted"]=editor.Liquids.Observe(to);
   string output=Environment.GetEnvironmentVariable("MAESTRO_FLOW_SPATIAL_EVIDENCE");if(!string.IsNullOrEmpty(output))File.WriteAllText(output,observations.ToString());yield return null;
  }
  [UnityTest]public IEnumerator FlowSpatialUnavailableEmptyFillViewDoesNotBecomeAnUncollectedSpill(){
   var(from,to)=FixedFlowCups();var fill=editor.Find(to).GetComponent<ContainerFillView>();fill.enabled=false;physics.StartPhysics();editor.Liquids.Tick(.05f);
   Assert.That(FlowAmount(from),Is.EqualTo(400));Assert.That(FlowAmount(from,"spilledMl"),Is.Zero);Assert.That(editor.Liquids.Active,Is.False);fill.enabled=true;editor.Liquids.Tick(.05f);Assert.That(FlowAmount(to),Is.GreaterThan(0));physics.PausePhysics();yield return LiquidSaved();yield return null;
  }
  [UnityTest]public IEnumerator FlowSpatialDormantEmptyReceiverLoadsAndUnrelatedContentStaysDormant(){
   var(from,to)=FixedFlowCups();string area=NativeAreaFor(to);var(far,farArea)=RegionalUnusedArea();Assert.That(editor.RetireNativeArea(area,out var error),Is.True,error);Assert.That(editor.RetireNativeArea(farArea,out error),Is.True,error);
   physics.StartPhysics();editor.Liquids.Tick(.05f);Assert.That(FlowAmount(from,"spilledMl"),Is.Zero);yield return NavigationLoadingDone();editor.Liquids.Tick(.05f);
   Assert.That(editor.Find(to),Is.Not.Null);Assert.That(editor.Find(far),Is.Null);Assert.That(FlowAmount(to),Is.GreaterThan(0));Assert.That(FlowAmount(from)+FlowAmount(to),Is.EqualTo(400).Within(1e-8));physics.PausePhysics();yield return LiquidSaved();
  }
  [UnityTest]public IEnumerator FlowSpatialDormantSolidBlocksTheStreamBeforeItReachesAReceiver(){
   var(from,to)=FixedFlowCups();Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Stream obstruction",new(editor.Find(to).transform.position.x,1.4f,0),.25f,Color.white,out var wall,out var error),Is.True,error);
   var data=editor.Read(wall);data.physics=ItemPhysics.Fixed;Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);Assert.That(editor.RetireNativeArea(NativeAreaFor(wall),out error),Is.True,error);
   physics.StartPhysics();editor.Liquids.Tick(.05f);Assert.That(FlowAmount(to),Is.Zero);yield return NavigationLoadingDone();editor.Liquids.Tick(.05f);
   Assert.That(editor.Find(wall),Is.Not.Null);Assert.That(FlowAmount(to),Is.Zero);Assert.That(FlowAmount(from,"spilledMl"),Is.GreaterThan(0));physics.PausePhysics();yield return LiquidSaved();
  }
  [UnityTest]public IEnumerator FlowSpatialDormantRoofLoadsBeforeRainCanEnterTheCup(){
   var(from,to)=FixedFlowCups();Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Rain roof",new(editor.Find(to).transform.position.x,3,0),.75f,Color.white,out var roof,out var error),Is.True,error);var data=editor.Read(roof);data.physics=ItemPhysics.Fixed;Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);
   Physics.SyncTransforms();Assert.That(new WeatherCover().Exposure(editor,editor.Find(to).transform.position+Vector3.up*.3f,Vector3.down*6,editor.Find(to)),Is.EqualTo(RainExposure.Covered),"The actual roof must cover the receiving cup before testing dormancy");
   Assert.That(editor.RetireNativeArea(NativeAreaFor(roof),out error),Is.True,error);Assert.That(editor.SetWeather(editor.WeatherRevision,1,new(){rainMmPerHour=60},0,out error),Is.True,error);
   physics.StartPhysics();editor.Liquids.Tick(.05f);Assert.That((double)editor.Liquids.ObserveRain(to)["collectedMl"],Is.Zero);yield return NavigationLoadingDone();editor.Liquids.Tick(.05f);
   Assert.That(editor.Find(roof),Is.Not.Null);Assert.That((double)editor.Liquids.ObserveRain(to)["collectedMl"],Is.Zero);physics.PausePhysics();yield return LiquidSaved();
  }
  [UnityTest]public IEnumerator FlowSpatialRefusesTheWholeTransferPassBeforeRainOrPourQuantitiesChange(){
   var(from,to)=FixedFlowCups();editor.Find(to).GetComponent<RigidRoomItem>().SetGeometryReady(false);Assert.That(editor.SetWeather(editor.WeatherRevision,1,new(){rainMmPerHour=60},0,out var error),Is.True,error);physics.StartPhysics();editor.Liquids.Tick(.05f);
   Assert.That(FlowAmount(from),Is.EqualTo(400));Assert.That(FlowAmount(to),Is.Zero);Assert.That((bool)editor.Liquids.ObserveRain(to)["ready"],Is.False);Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);physics.PausePhysics();yield return LiquidSaved();yield return null;
  }
  [UnityTest]public IEnumerator FlowSpatialWaitingPublishesOnlyEarlierAcceptedFlowAndReleasesTheWriter(){
   var(from,to)=FixedFlowCups();physics.StartPhysics();editor.Liquids.Tick(.05f);double accepted=FlowAmount(to);Assert.That(accepted,Is.GreaterThan(0));int events=0;double published=0;editor.ContainerPoured+=(id,moved,spill,_,_)=>{if(id==from){events++;published=moved;Assert.That(spill,Is.Zero);Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);}};
   editor.Find(to).GetComponent<RigidRoomItem>().SetGeometryReady(false);editor.Liquids.Tick(.05f);yield return LiquidSaved();Assert.That(editor.Liquids.Active,Is.False);Assert.That(events,Is.EqualTo(1));Assert.That(published,Is.EqualTo(accepted));Assert.That(editor.Read(to).containers[0].amountMl,Is.EqualTo(accepted));Assert.That(editor.Read(from).containers[0].amountMl+accepted,Is.EqualTo(400).Within(1e-8));physics.PausePhysics();yield return LiquidSaved();yield return null;
  }
  [UnityTest]public IEnumerator FlowSpatialPassiveFactsDoNotLoadMissingReceiversOrStartFlow(){
   var(from,to)=FixedFlowCups();Assert.That(editor.RetireNativeArea(NativeAreaFor(to),out var error),Is.True,error);
   for(int i=0;i<10;i++){editor.Liquids.Observe(from);editor.Liquids.ObserveScooping(to);editor.Liquids.ObserveRain(to);}
   yield return null;Assert.That(editor.NativeActivationPending,Is.False);Assert.That(editor.Find(to),Is.Null);Assert.That(physics.Running,Is.False);Assert.That(FlowAmount(from),Is.EqualTo(400));Assert.That(editor.Liquids.Active,Is.False);
  }
  [UnityTest]public IEnumerator FlowSpatialPauseCancelsPendingReceiverLoadWithoutRestarting(){
   var(from,to)=FixedFlowCups();Assert.That(editor.RetireNativeArea(NativeAreaFor(to),out var error),Is.True,error);physics.StartPhysics();editor.Liquids.Tick(.05f);
   Assert.That(editor.NativeActivationPending,Is.True);physics.PausePhysics();yield return LiquidSaved();yield return NavigationLoadingDone();Assert.That(editor.Find(to),Is.Null);Assert.That(physics.Running,Is.False);Assert.That(FlowAmount(from),Is.EqualTo(400));Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);
  }
  [UnityTest]public IEnumerator FlowSpatialWaitingCallbackMayStartFreshFlowWithoutOuterCleanup(){
   var(from,to)=FixedFlowCups();physics.StartPhysics();editor.Liquids.Tick(.05f);double accepted=FlowAmount(to);int callbacks=0;
   var rigid=editor.Find(to).GetComponent<RigidRoomItem>();editor.ContainerPoured+=(id,_,_,_,_)=>{if(id!=from||callbacks++!=0)return;rigid.SetGeometryReady(true);editor.Liquids.Tick(.05f);};
   rigid.SetGeometryReady(false);editor.Liquids.Tick(.05f);yield return LiquidSaved();Assert.That(callbacks,Is.EqualTo(1));Assert.That(editor.Liquids.Active,Is.True);Assert.That(FlowAmount(to),Is.GreaterThan(accepted));Assert.That((string)editor.Liquids.Observe(to)["phase"],Is.EqualTo("flowing"));Assert.That(editor.Read(to).containers[0].amountMl,Is.EqualTo(accepted));physics.PausePhysics();yield return LiquidSaved();yield return null;
  }
  [UnityTest]public IEnumerator FlowSpatialFailedWaitPublicationPreservesAcceptedAmountsAndNeedsRecovery(){
 var(from,to)=FixedFlowCups();physics.StartPhysics();editor.Liquids.Tick(.05f);double accepted=FlowAmount(to);Assert.Greater(accepted,0);int events=0;editor.ContainerPoured+=(_,_,_,_,_)=>events++;
 var rigid=editor.Find(to).GetComponent<RigidRoomItem>();rigid.SetGeometryReady(false);string pending=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(pending);
 try{editor.Liquids.Tick(.05f);yield return LiquidSaved();}finally{Directory.Delete(pending);}
 Assert.AreEqual(0,events);Assert.AreEqual(accepted,FlowAmount(to));Assert.AreEqual(400-accepted,FlowAmount(from));Assert.AreEqual("failed",(string)editor.Liquids.Observe(from)["phase"]);Assert.IsTrue(editor.WriteGate.CanFreeze(out _));
 rigid.SetGeometryReady(true);int intent=physics.SimulationIntent;
 typeof(RoomPhysicsWorld).GetMethod("Notify",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(physics,new object[]{false,true});Assert.AreEqual(intent,physics.SimulationIntent);
 editor.Liquids.Tick(.05f);physics.PausePhysics();physics.StartPhysics();editor.Liquids.Tick(.05f);Assert.AreEqual(accepted,FlowAmount(to));Assert.IsFalse(editor.CanSaveRoom);physics.PausePhysics();
}
  [UnityTest]public IEnumerator FlowSpatialDistantUnavailableCavityDoesNotBlockLocalTransfer(){
   var(from,to)=FixedFlowCups();var ex=new RoomAgentExecutor(editor);string far=CreateTemplateFixture(TemplateCall("cup",new Vector3(-10,2,-10)));
   Assert.That(editor.SetItemPhysics(far,new(){mode="fixed",shape="box",mass=1}),Is.True,editor.Status);editor.Find(far).GetComponent<ContainerFillView>().enabled=false;
   physics.StartPhysics();editor.Liquids.Tick(.05f);Assert.That(FlowAmount(to),Is.GreaterThan(0));Assert.That(FlowAmount(from)+FlowAmount(to),Is.EqualTo(400).Within(1e-8));physics.PausePhysics();yield return LiquidSaved();yield return null;
  }
  [UnityTest]public IEnumerator FlowSpatialDormantOffsetCavityIsFoundOutsideItsArtwork(){
   var(from,to)=FixedFlowCups();var value=editor.Read(to);value.position+=Vector3.right;value.containers[0].frame.position-=Vector3.right;
   Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{value},Array.Empty<string>(),out var error),Is.True,error);Assert.That(editor.RetireNativeArea(NativeAreaFor(to),out error),Is.True,error);
   physics.StartPhysics();editor.Liquids.Tick(.05f);Assert.That(FlowAmount(from),Is.EqualTo(400));yield return NavigationLoadingDone();editor.Liquids.Tick(.05f);Assert.That(editor.Find(to),Is.Not.Null);Assert.That(FlowAmount(to),Is.GreaterThan(0));Assert.That(FlowAmount(from)+FlowAmount(to),Is.EqualTo(400).Within(1e-8));physics.PausePhysics();yield return LiquidSaved();
  }
 }
}
