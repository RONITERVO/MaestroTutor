// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  void BothContactPoints(Vector3 point){for(int i=0;i<2;i++)editor.Liquids.SetContactInput(i,point,true);ContactTick();}
  [UnityTest]public IEnumerator LiquidContactContinuityExpiredInputReacquisitionHasNoInventedEdgeOrRipple(){
   var(pool,prop)=MediumFixture(.1f);editor.Find(prop).GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);var events=new List<LiquidContact>();editor.MediumContact+=e=>{if(e.Participant=="input:left")events.Add(e);};
   var dry=new Vector3(4,2,0);var wet=new Vector3(4,1.4f,0);ContactPoint(dry);var baseline=editor.Liquids.ObserveContact("input:left");
   yield return new WaitForSecondsRealtime(.25f);var expired=editor.Liquids.ObserveContact("input:left");Assert.That((bool)expired["known"],Is.False);
   editor.Liquids.SetContactInput(0,wet,true);var refreshed=editor.Liquids.ObserveContact("input:left");Assert.That((bool)refreshed["known"],Is.False);ContactTick();var recovered=editor.Liquids.ObserveContact("input:left");
   Assert.That((bool)recovered["immersed"],Is.True);Assert.That((double)recovered["sampleSpeedMetresPerSecond"],Is.Zero);Assert.That(events,Is.Empty);Assert.That(editor.Find(pool).GetComponent<ContainerFillView>().RippleCount,Is.Zero);
   ContactPoint(dry);var following=editor.Liquids.ObserveContact("input:left");Assert.That(events.Select(e=>e.Phase),Is.EqualTo(new[]{"exited"}));Assert.That(editor.Read(pool).containers[0].amountMl,Is.EqualTo(400000));
   string output=Environment.GetEnvironmentVariable("MAESTRO_CONTACT_CONTINUITY_EVIDENCE");if(!string.IsNullOrEmpty(output))File.WriteAllText(output,new JObject{["baseline"]=baseline,["expired"]=expired,["refreshed"]=refreshed,["recovered"]=recovered,["following"]=following,["events"]=new JArray(events.Select(e=>new JObject{["phase"]=e.Phase,["participantId"]=e.Participant,["bodyId"]=e.BodyId,["kind"]=e.Kind}))}.ToString());physics.PausePhysics();
  }
  [UnityTest]public IEnumerator LiquidContactContinuityMissedObjectAndAvatarSamplesEstablishFreshBaselines(){
   var(pool,prop)=MediumFixture(.1f);var item=editor.Find(prop);item.GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);var maestro=editor.Find("maestro");var avatar=maestro.gameObject.AddComponent<MaestroAvatar>();avatar.PoseRig.SetManual(true);
   item.transform.position=new(4,2,0);maestro.transform.position=new(4,2,0);ContactTick();var events=new List<LiquidContact>();editor.MediumContact+=events.Add;
   yield return new WaitForSecondsRealtime(.25f);Assert.That((bool)editor.Liquids.ObserveContact(prop)["known"],Is.False);Assert.That((bool)editor.Liquids.ObserveContact("maestro")["known"],Is.False);
   item.transform.position=new(4,1.4f,0);maestro.transform.position+=new Vector3(4,1.4f,0)-avatar.PoseRig.Bone(PoseJoint.LeftFoot).position;ContactTick();
   Assert.That(events,Is.Empty);foreach(var id in new[]{prop,"maestro"}){var state=editor.Liquids.ObserveContact(id);Assert.That((bool)state["immersed"],Is.True);Assert.That((double)state["sampleSpeedMetresPerSecond"],Is.Zero);}Assert.That(editor.Find(pool).GetComponent<ContainerFillView>().RippleCount,Is.Zero);physics.PausePhysics();
  }
  [UnityTest]public IEnumerator LiquidContactContinuityExpiredFastCrossingIsNotReplayed(){
   var(pool,_)=MediumFixture(.1f);var events=new List<LiquidContact>();editor.MediumContact+=e=>{if(e.Participant=="input:left")events.Add(e);};ContactPoint(new(4,2,0));
   yield return new WaitForSecondsRealtime(.25f);ContactPoint(new(4,1,0));Assert.That(events,Is.Empty);Assert.That((bool)editor.Liquids.ObserveContact("input:left")["known"],Is.True);Assert.That((bool)editor.Liquids.ObserveContact("input:left")["immersed"],Is.False);Assert.That(editor.Find(pool).GetComponent<ContainerFillView>().RippleCount,Is.Zero);physics.PausePhysics();
  }
  [UnityTest]public IEnumerator LiquidContactContinuityInputKindRoundTripWithoutSamplingStillBreaksContinuity(){
   var(_,prop)=MediumFixture(.1f);editor.Find(prop).GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);var events=new List<LiquidContact>();editor.MediumContact+=e=>{if(e.Participant=="input:left")events.Add(e);};ContactPoint(new(4,2,0));
   editor.Liquids.SetContactInput(0,new Vector3(4,1.4f,0),false);editor.Liquids.SetContactInput(0,new Vector3(4,1.4f,0),true);Assert.That((bool)editor.Liquids.ObserveContact("input:left")["known"],Is.False);ContactTick();Assert.That(events,Is.Empty);Assert.That((bool)editor.Liquids.ObserveContact("input:left")["immersed"],Is.True);physics.PausePhysics();yield return null;
  }
  [UnityTest]public IEnumerator LiquidContactContinuityCallbackTrackingLossCancelsOnlyTheInvalidInput(){
   var(pool,prop)=MediumFixture(.1f);editor.Find(prop).GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);BothContactPoints(new(4,2,0));var events=new List<LiquidContact>();
   editor.MediumContact+=e=>{if(!e.Participant.StartsWith("input:"))return;events.Add(e);if(events.Count==1){editor.Liquids.SetContactInput(1,null,true);editor.Liquids.SetContactInput(1,new Vector3(4,1.4f,0),true);}};
   BothContactPoints(new(4,1.4f,0));Assert.That(events.Select(e=>e.Participant),Is.EqualTo(new[]{"input:left"}));Assert.That(editor.Find(pool).GetComponent<ContainerFillView>().RippleCount,Is.EqualTo(1));ContactTick();Assert.That(events.Count,Is.EqualTo(1));physics.PausePhysics();yield return null;
  }
  [UnityTest]public IEnumerator LiquidContactContinuityCallbackFreshInputSupersedesItsQueuedPose(){
   var(pool,prop)=MediumFixture(.1f);editor.Find(prop).GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);BothContactPoints(new(4,2,0));var events=new List<LiquidContact>();
   editor.MediumContact+=e=>{if(!e.Participant.StartsWith("input:"))return;events.Add(e);if(events.Count==1)editor.Liquids.SetContactInput(1,new Vector3(4,2,0),true);};
   BothContactPoints(new(4,1.4f,0));Assert.That(events.Select(e=>e.Participant),Is.EqualTo(new[]{"input:left"}));Assert.That((bool)editor.Liquids.ObserveContact("input:right")["known"],Is.False);ContactTick();Assert.That(events.Count,Is.EqualTo(1));Assert.That(editor.Find(pool).GetComponent<ContainerFillView>().RippleCount,Is.EqualTo(1));physics.PausePhysics();yield return null;
  }
  [UnityTest]public IEnumerator LiquidContactContinuityCallbackOwnershipChangeKeepsIndependentInputsDeliverable(){
   var(_,prop)=MediumFixture(.1f);var first=editor.Find(prop);first.GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Second contact",new(4,2,0),.1f,Color.white,out var other,out var error),Is.True,error);var second=editor.Find(other);second.GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);
   first.transform.position=new(4,2,0);second.transform.position=new(4,2,0);BothContactPoints(new(4,2,0));var events=new List<LiquidContact>();bool changed=false;
   editor.MediumContact+=e=>{events.Add(e);if(e.Kind=="object"&&!changed){changed=true;editor.Find(e.Participant==prop?other:prop).GetComponent<RigidRoomItem>().SetAnimationOwner(new object(),true);}};
   first.transform.position=new(4,1.4f,0);second.transform.position=new(4,1.4f,0);BothContactPoints(new(4,1.4f,0));Assert.That(events.Count(e=>e.Kind=="object"),Is.EqualTo(1));Assert.That(events.Count(e=>e.Kind=="hand"),Is.EqualTo(2));ContactTick();Assert.That(events.Count,Is.EqualTo(3));physics.PausePhysics();yield return null;
  }
  [UnityTest]public IEnumerator LiquidContactContinuityUnchangedAvatarPoseSurvivesTranslatedScaledWorld(){
   var(_,prop)=MediumFixture(.1f);editor.Find(prop).GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);var maestro=editor.Find("maestro");var avatar=maestro.gameObject.AddComponent<MaestroAvatar>();avatar.PoseRig.SetManual(true);
   editor.transform.SetPositionAndRotation(new(1000,50,-700),Quaternion.Euler(0,37,0));editor.transform.localScale=Vector3.one*1.7f;
   maestro.transform.position+=editor.Frame.PointToWorld(new(4,2,0))-avatar.PoseRig.Bone(PoseJoint.LeftFoot).position;ContactTick();var events=new List<LiquidContact>();editor.MediumContact+=e=>{if(e.Participant=="maestro")events.Add(e);};
   maestro.transform.position+=editor.Frame.PointToWorld(new(4,1.4f,0))-avatar.PoseRig.Bone(PoseJoint.LeftFoot).position;ContactTick();
   Assert.That(events.Select(e=>e.Phase),Is.EqualTo(new[]{"entered"}));Assert.That((bool)editor.Liquids.ObserveContact("maestro")["immersed"],Is.True);physics.PausePhysics();yield return null;
  }
  [UnityTest]public IEnumerator LiquidContactContinuityCallbackJointMoveInvalidatesOnlyTheAvatarPose(){
   var(_,prop)=MediumFixture(.1f);var item=editor.Find(prop);item.GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);var maestro=editor.Find("maestro");var avatar=maestro.gameObject.AddComponent<MaestroAvatar>();avatar.PoseRig.SetManual(true);item.transform.position=new(4,2,0);maestro.transform.position=new(4,2,0);ContactTick();var events=new List<LiquidContact>();
   editor.MediumContact+=e=>{events.Add(e);if(e.Participant==prop)avatar.PoseRig.Bone(PoseJoint.LeftFoot).position+=Vector3.up;};
   item.transform.position=new(4,1.4f,0);maestro.transform.position+=new Vector3(4,1.4f,0)-avatar.PoseRig.Bone(PoseJoint.LeftFoot).position;ContactTick();Assert.That(events.Select(e=>e.Participant),Is.EqualTo(new[]{prop}));Assert.That((bool)editor.Liquids.ObserveContact("maestro")["known"],Is.False);ContactTick();Assert.That(events.Count,Is.EqualTo(1));physics.PausePhysics();yield return null;
  }
  [UnityTest]public IEnumerator LiquidContactContinuityCallbackUnavailableWaterCannotDeliverOrRippleTheRemainingBatch(){
   var(pool,prop)=MediumFixture(.1f);editor.Find(prop).GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);BothContactPoints(new(4,2,0));var events=new List<LiquidContact>();
   editor.MediumContact+=e=>{if(!e.Participant.StartsWith("input:"))return;events.Add(e);if(events.Count==1)editor.Find(pool).GetComponent<RigidRoomItem>().SetGeometryReady(false);};
   BothContactPoints(new(4,1.4f,0));Assert.That(events.Count,Is.EqualTo(1));Assert.That(editor.Find(pool).GetComponent<ContainerFillView>().RippleCount,Is.EqualTo(1));Assert.That((bool)editor.Liquids.ObserveContact("input:right")["known"],Is.False);physics.PausePhysics();yield return null;
  }
  [UnityTest]public IEnumerator LiquidContactContinuityCallbackNewWaterLevelInvalidatesQueuedContacts(){
   var(pool,prop)=MediumFixture(.1f);editor.Find(prop).GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);Assert.That(editor.SetWeather(editor.WeatherRevision,1,new(){rainMmPerHour=60},0,out var error),Is.True,error);BothContactPoints(new(4,2,0));var events=new List<LiquidContact>();
   editor.MediumContact+=e=>{if(!e.Participant.StartsWith("input:"))return;events.Add(e);if(events.Count==1)editor.Liquids.Tick(.1f);};
   BothContactPoints(new(4,1.4f,0));Assert.That(events.Count,Is.EqualTo(1));Assert.That((double)editor.Liquids.ObserveRain(pool)["collectedMl"],Is.GreaterThan(0));Assert.That((bool)editor.Liquids.ObserveContact("input:right")["known"],Is.False);physics.PausePhysics();yield return null;
  }
  [UnityTest]public IEnumerator LiquidContactContinuityNestedPassSupersedesRemainingOldCallbacks(){
   var(_,prop)=MediumFixture(.1f);editor.Find(prop).GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);BothContactPoints(new(4,2,0));var events=new List<LiquidContact>();
   editor.MediumContact+=e=>{if(!e.Participant.StartsWith("input:"))return;events.Add(e);if(events.Count==1)ContactTick();};
   BothContactPoints(new(4,1.4f,0));Assert.That(events.Count,Is.EqualTo(1));physics.PausePhysics();yield return null;
  }
  [UnityTest]public IEnumerator LiquidContactContinuitySlowCallbackCannotDeliverExpiredSamplesWithinOneFrame(){
   var(_,prop)=MediumFixture(.1f);editor.Find(prop).GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);BothContactPoints(new(4,2,0));var events=new List<LiquidContact>();
   editor.MediumContact+=e=>{if(!e.Participant.StartsWith("input:"))return;events.Add(e);if(events.Count==1)System.Threading.Thread.Sleep(230);};
   BothContactPoints(new(4,1.4f,0));Assert.That(events.Count,Is.EqualTo(1));Assert.That((bool)editor.Liquids.ObserveContact("input:right")["known"],Is.False);physics.PausePhysics();yield return null;
  }
 }
}
