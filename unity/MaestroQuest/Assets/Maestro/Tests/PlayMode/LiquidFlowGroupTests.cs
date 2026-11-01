// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  (string a,string b,string c,string d) IndependentFlowCups(float separation=6){
   var(a,b)=FixedFlowCups();var left=new[]{a,b}.Select(id=>{var v=editor.Read(id);v.position+=Vector3.left*separation;return v;}).ToArray();Assert.That(editor.ApplyAgentEdit(editor.Revision,left,Array.Empty<string>(),out var error),Is.True,error);
   var(c,d)=FixedFlowCups();return(a,b,c,d);
  }
  [UnityTest]public IEnumerator FlowGroupUnavailableReceiverDoesNotStopIndependentTransfer(){
   var(a,b,c,d)=IndependentFlowCups();editor.Find(b).GetComponent<RigidRoomItem>().SetGeometryReady(false);physics.StartPhysics();editor.Liquids.Tick(.05f);
   Assert.That(FlowAmount(a),Is.EqualTo(400));Assert.That(FlowAmount(b),Is.Zero);Assert.That(FlowAmount(d),Is.GreaterThan(0),"A separate circuit must keep running");Assert.That(FlowAmount(c)+FlowAmount(d),Is.EqualTo(400).Within(1e-8));
   Assert.That((string)editor.Liquids.Observe(a)["phase"],Is.EqualTo("waiting"));Assert.That((string)editor.Liquids.Observe(c)["phase"],Is.EqualTo("flowing"));Assert.That((string)editor.Liquids.Observe(c)["error"],Is.Empty);
   var observation=new JObject{["waiting"]=editor.Liquids.Observe(a),["waitingRain"]=editor.Liquids.ObserveRain(a),["flowing"]=editor.Liquids.Observe(c),["receiver"]=editor.Liquids.Observe(d)};
   editor.Find(b).GetComponent<RigidRoomItem>().SetGeometryReady(true);editor.Liquids.Tick(.05f);Assert.That(FlowAmount(b),Is.GreaterThan(0).And.LessThanOrEqualTo(18.75));Assert.That(FlowAmount(a)+FlowAmount(b)+FlowAmount(c)+FlowAmount(d),Is.EqualTo(800).Within(1e-8));observation["recovered"]=editor.Liquids.Observe(a);observation["recoveredReceiver"]=editor.Liquids.Observe(b);
   physics.PausePhysics();yield return LiquidSaved();observation["accepted"]=editor.Liquids.Observe(b);string output=Environment.GetEnvironmentVariable("MAESTRO_FLOW_GROUP_EVIDENCE");if(!string.IsNullOrEmpty(output))File.WriteAllText(output,observation.ToString());yield return null;
  }
  [UnityTest]public IEnumerator FlowGroupNewWaitPublishesEarlierFlowThenIndependentCircuitContinues(){
   var(a,b,c,d)=IndependentFlowCups();physics.StartPhysics();editor.Liquids.Tick(.05f);double left=FlowAmount(b),right=FlowAmount(d);Assert.That(left,Is.GreaterThan(0));Assert.That(right,Is.GreaterThan(0));
   editor.Find(b).GetComponent<RigidRoomItem>().SetGeometryReady(false);editor.Liquids.Tick(.05f);Assert.That(editor.Read(b).containers[0].amountMl,Is.EqualTo(left));Assert.That(editor.Read(d).containers[0].amountMl,Is.EqualTo(right));
   editor.Liquids.Tick(.05f);Assert.That(FlowAmount(b),Is.EqualTo(left));Assert.That(FlowAmount(d),Is.GreaterThan(right));Assert.That(FlowAmount(a)+FlowAmount(b),Is.EqualTo(400).Within(1e-8));physics.PausePhysics();yield return LiquidSaved();yield return null;
  }
  [UnityTest]public IEnumerator FlowGroupPendingObstacleLoadsWhileIndependentCircuitAdvances(){
   var(a,b,c,d)=IndependentFlowCups();var position=editor.Find(b).transform.position+Vector3.up*.4f;Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Left stream obstacle",position,.25f,Color.white,out var wall,out var error),Is.True,error);Assert.That(editor.SetItemPhysics(wall,new(){mode="fixed",shape="box",mass=1}),Is.True,editor.Status);Assert.That(editor.RetireNativeArea(NativeAreaFor(wall),out error),Is.True,error);
   physics.StartPhysics();editor.Liquids.Tick(.05f);Assert.That(FlowAmount(a),Is.EqualTo(400));Assert.That(FlowAmount(b),Is.Zero);Assert.That(FlowAmount(d),Is.GreaterThan(0),"Unrelated flow progresses while native input preparation is pending");double previous=FlowAmount(d);
   yield return NavigationLoadingDone();editor.Liquids.Tick(.05f);editor.Liquids.Tick(.05f);Assert.That(editor.Find(wall),Is.Not.Null);Assert.That(FlowAmount(b),Is.Zero);Assert.That(FlowAmount(a,"spilledMl"),Is.GreaterThan(0));Assert.That(FlowAmount(d),Is.GreaterThanOrEqualTo(previous));physics.PausePhysics();yield return LiquidSaved();
  }
  [UnityTest]public IEnumerator FlowGroupNearbyCircuitsSharingAnObstacleStayIndependent(){
   var(a,b,c,d)=IndependentFlowCups(.3f);var middle=(editor.Find(b).transform.position+editor.Find(d).transform.position)*.5f;middle.y=.5f;
   Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Shared solid",middle,4,Color.white,out var solid,out var error),Is.True,error);Assert.That(editor.SetItemPhysics(solid,new(){mode="fixed",shape="box",mass=1}),Is.True,editor.Status);
   foreach(var left in new[]{a,b})foreach(var right in new[]{c,d})Assert.That(editor.MissingLiquidExtent(left,editor.Read(left).containers[0],out _).Bounds.Intersects(editor.MissingLiquidExtent(right,editor.Read(right).containers[0],out _).Bounds),Is.False,"The two quantity circuits must have separate cavities");
   foreach(var id in new[]{a,c}){var item=editor.Find(id);var origin=ContainerFlowGeometry.Lip(editor.Read(id).containers[0],item.transform,Vector3.up,out var outward);var bounds=new Bounds(origin,Vector3.zero);for(int i=1;i<=LiquidPouring.Segments;i++){float t=i*.025f;bounds.Encapsulate(origin+outward*(.15f*t)+Physics.gravity*(.5f*t*t));}Assert.That(editor.SpatialInputTargets(bounds,bounds,false,id),Does.Contain(solid),"Both circuits must use the same read-only obstacle");}
   editor.Find(b).GetComponent<RigidRoomItem>().SetGeometryReady(false);physics.StartPhysics();editor.Liquids.Tick(.05f);Assert.That(FlowAmount(a),Is.EqualTo(400));Assert.That(FlowAmount(d),Is.GreaterThan(0));physics.PausePhysics();yield return LiquidSaved();yield return null;
  }
  [UnityTest]public IEnumerator FlowGroupNearbyDormantReceiverUsesRetainedPoseWhileOtherCircuitRuns(){
   var(a,b,c,d)=IndependentFlowCups(.6f);Assert.That(editor.RetireNativeArea(NativeAreaFor(b),out var error),Is.True,error);physics.StartPhysics();editor.Liquids.Tick(.05f);
   Assert.That(FlowAmount(a),Is.EqualTo(400));Assert.That(FlowAmount(d),Is.GreaterThan(0));yield return NavigationLoadingDone();editor.Liquids.Tick(.05f);editor.Liquids.Tick(.05f);Assert.That(FlowAmount(b),Is.GreaterThan(0));Assert.That(FlowAmount(a)+FlowAmount(b)+FlowAmount(c)+FlowAmount(d),Is.EqualTo(800).Within(1e-8));physics.PausePhysics();yield return LiquidSaved();
  }
  [UnityTest]public IEnumerator FlowGroupDormantReceiverResolvesInMovedRotatedScaledWorld(){
   var(a,b,c,d)=IndependentFlowCups(.6f);editor.transform.SetPositionAndRotation(new(2,0,-4),Quaternion.Euler(0,37,0));editor.transform.localScale=Vector3.one*1.7f;Physics.SyncTransforms();var expected=editor.Find(b).transform.position;
   Assert.That(editor.RetireNativeArea(NativeAreaFor(b),out var error),Is.True,error);physics.StartPhysics();editor.Liquids.Tick(.05f);Assert.That(FlowAmount(a),Is.EqualTo(400));Assert.That(FlowAmount(d),Is.GreaterThan(0));
   yield return NavigationLoadingDone();editor.Liquids.Tick(.05f);editor.Liquids.Tick(.05f);Assert.That(Vector3.Distance(editor.Find(b).transform.position,expected),Is.LessThan(.0001f));Assert.That(FlowAmount(b),Is.GreaterThan(0));Assert.That(FlowAmount(a)+FlowAmount(b)+FlowAmount(c)+FlowAmount(d),Is.EqualTo(800).Within(1e-8));physics.PausePhysics();yield return LiquidSaved();
  }
  [UnityTest]public IEnumerator FlowGroupTransitiveCavityDependencyHoldsOnlyItsCircuit(){
   var(a,b,c,d)=IndependentFlowCups();var ex=new RoomAgentExecutor(editor);var receiver=editor.Read(b).containers[0];var position=editor.Find(b).transform.position+Vector3.right*(receiver.radius*1.8f);string extra=CreateTemplateFixture(TemplateCall("cup",position));
   Assert.That(editor.SetItemPhysics(extra,new(){mode="fixed",shape="box",mass=1}),Is.True,editor.Status);var bound=editor.MissingLiquidExtent(extra,editor.Read(extra).containers[0],out _).Bounds;Assert.That(bound.Intersects(editor.MissingLiquidExtent(b,receiver,out _).Bounds),Is.True);
   var source=editor.Find(a);var origin=ContainerFlowGeometry.Lip(editor.Read(a).containers[0],source.transform,Vector3.up,out var outward);var path=new Bounds(origin,Vector3.zero);for(int i=1;i<=LiquidPouring.Segments;i++){float t=i*.025f;path.Encapsulate(origin+outward*(.15f*t)+Physics.gravity*(.5f*t*t));}Assert.That(path.Intersects(bound),Is.False,"The unavailable cavity must be connected through the receiver rather than directly along the source stream");
   editor.Find(extra).GetComponent<RigidRoomItem>().SetGeometryReady(false);physics.StartPhysics();editor.Liquids.Tick(.05f);Assert.That(FlowAmount(a),Is.EqualTo(400));Assert.That(FlowAmount(b),Is.Zero);Assert.That(FlowAmount(d),Is.GreaterThan(0));physics.PausePhysics();yield return LiquidSaved();yield return null;
  }
  [UnityTest]public IEnumerator FlowGroupMovingReceiverChangesWhichCircuitWaits(){
   var(a,b,c,d)=IndependentFlowCups();physics.StartPhysics();editor.Liquids.Tick(.05f);double right=FlowAmount(d);var moved=editor.Find(b);moved.transform.position=editor.Find(d).transform.position+Vector3.right*.03f;moved.GetComponent<RigidRoomItem>().Teleported();moved.GetComponent<RigidRoomItem>().SetGeometryReady(false);
   editor.Liquids.Tick(.05f);double leftAfterPublication=FlowAmount(a);editor.Liquids.Tick(.05f);Assert.That(FlowAmount(a),Is.LessThan(leftAfterPublication));Assert.That(FlowAmount(a,"spilledMl"),Is.GreaterThan(0));Assert.That(FlowAmount(d),Is.EqualTo(right));Assert.That((string)editor.Liquids.Observe(c)["phase"],Is.EqualTo("waiting"));physics.PausePhysics();yield return LiquidSaved();yield return null;
  }
  [UnityTest]public IEnumerator FlowGroupWaitCallbackCanBeginFreshIndependentFlow(){
   var(a,b,c,d)=IndependentFlowCups();physics.StartPhysics();editor.Liquids.Tick(.05f);double before=FlowAmount(d);int callbacks=0;var body=editor.Find(b).GetComponent<RigidRoomItem>();
   editor.ContainerPoured+=(id,_,_,_,_)=>{if(id!=a||callbacks++!=0)return;body.SetGeometryReady(true);editor.Liquids.Tick(.05f);};body.SetGeometryReady(false);editor.Liquids.Tick(.05f);
   yield return LiquidSaved();Assert.That(callbacks,Is.EqualTo(1));Assert.That(editor.Liquids.Active,Is.True);Assert.That(FlowAmount(d),Is.GreaterThan(before));Assert.That((string)editor.Liquids.Observe(c)["phase"],Is.EqualTo("flowing"));physics.PausePhysics();yield return LiquidSaved();yield return null;
  }
  [UnityTest]public IEnumerator FlowGroupFailedPublicationPreservesAcceptedIndependentAmountsAndNeedsRecovery(){
 var(a,b,c,d)=IndependentFlowCups();var body=editor.Find(b).GetComponent<RigidRoomItem>();body.SetGeometryReady(false);physics.StartPhysics();editor.Liquids.Tick(.05f);double accepted=FlowAmount(d);Assert.Greater(accepted,0);
 string pending=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(pending);
 try{physics.PausePhysics();yield return LiquidSaved();}finally{Directory.Delete(pending);}
 Assert.AreEqual(400,FlowAmount(a));Assert.AreEqual(0,FlowAmount(b));Assert.AreEqual(400-accepted,FlowAmount(c));Assert.AreEqual(accepted,FlowAmount(d));Assert.IsTrue(editor.WriteGate.CanFreeze(out _));
 body.SetGeometryReady(true);physics.StartPhysics();editor.Liquids.Tick(.05f);Assert.AreEqual(0,FlowAmount(b));Assert.AreEqual(accepted,FlowAmount(d));Assert.IsFalse(editor.CanSaveRoom);physics.PausePhysics();
}
 }
}
