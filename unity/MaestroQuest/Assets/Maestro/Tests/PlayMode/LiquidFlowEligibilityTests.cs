// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  void CaptureFlowEligibility(string name,JObject value){
   string output=Environment.GetEnvironmentVariable("MAESTRO_FLOW_ELIGIBILITY_EVIDENCE");if(string.IsNullOrEmpty(output))return;
   var evidence=File.Exists(output)?JObject.Parse(File.ReadAllText(output)):new JObject();evidence[name]=value;File.WriteAllText(output,evidence.ToString());
  }
  [UnityTest]public IEnumerator FlowEligibilityFullTiltedVesselPreparesRainCoverBeforeDraining(){
   var(from,to)=FixedFlowCups();Assert.That(editor.DeleteObject(to,out var error),Is.True,error);
   var data=editor.Read(from);data.containers[0].amountMl=data.containers[0].capacityMl;Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);
   var item=editor.Find(from);var c=data.containers[0];var opening=item.transform.TransformPoint(c.frame.position+c.frame.rotation*(Vector3.up*c.height));
   Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Cover over full cup",opening+Vector3.up,1,Color.white,out var roof,out error),Is.True,error);Assert.That(editor.SetItemPhysics(roof,new(){mode="fixed",shape="box",mass=1}),Is.True,editor.Status);
   Physics.SyncTransforms();Assert.That(new WeatherCover().Exposure(editor,opening+Vector3.up*.001f,Vector3.down*6,item),Is.EqualTo(RainExposure.Covered));
   Assert.That(editor.RetireNativeArea(NativeAreaFor(roof),out error),Is.True,error);Assert.That(editor.SetWeather(editor.WeatherRevision,1,new(){rainMmPerHour=60},0,out error),Is.True,error);
   physics.StartPhysics();editor.Liquids.Tick(.05f);
   Assert.That((double)editor.Liquids.ObserveRain(from)["collectedMl"],Is.Zero,"Draining cannot collect rain through a saved roof in the same pass");
   Assert.That(FlowAmount(from),Is.EqualTo(c.capacityMl),"The whole pass waits before changing quantities");
   var observations=new JObject{["waiting"]=editor.Liquids.Observe(from),["waitingRain"]=editor.Liquids.ObserveRain(from)};yield return NavigationLoadingDone();editor.Liquids.Tick(.05f);
   Assert.That(editor.Find(roof),Is.Not.Null);Assert.That(FlowAmount(from),Is.LessThan(c.capacityMl));Assert.That((double)editor.Liquids.ObserveRain(from)["collectedMl"],Is.Zero);
   observations["flowing"]=editor.Liquids.Observe(from);observations["flowingRain"]=editor.Liquids.ObserveRain(from);physics.PausePhysics();observations["accepted"]=editor.Liquids.Observe(from);CaptureFlowEligibility("rain",observations);
  }
  [UnityTest]public IEnumerator FlowEligibilityEmptyTiltedReceiverPreparesItsPossibleOutgoingPath(){
   var(a,b)=FixedFlowCups();var ids=new[]{a,b}.OrderBy(id=>id,StringComparer.Ordinal).ToArray();string from=ids[0],to=ids[1];
   var source=editor.Read(from);source.position=new(3,2,0);source.rotation=Quaternion.Euler(0,0,80);source.containers[0].capacityMl=500;source.containers[0].amountMl=400;
   var receiver=editor.Read(to);receiver.rotation=Quaternion.Euler(0,0,-80);receiver.containers[0].capacityMl=100;receiver.containers[0].amountMl=0;
   Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{source,receiver},Array.Empty<string>(),out var error),Is.True,error);
   var sourceItem=editor.Find(from);var destination=editor.Find(to);var origin=ContainerFlowGeometry.Lip(source.containers[0],sourceItem.transform,Vector3.up,out var outward);var velocity=outward*.15f;
   Vector3 Incoming(float time)=>origin+velocity*time+Physics.gravity*(.5f*time*time);
   var c=receiver.containers[0];var mouthOffset=destination.transform.TransformVector(c.frame.position+c.frame.rotation*(Vector3.up*c.height));receiver.position=editor.Frame.PointToRoom(Incoming(.2f)-mouthOffset);
   Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{receiver},Array.Empty<string>(),out error),Is.True,error);destination=editor.Find(to);
   Assert.That(ContainerFlowGeometry.Enters(c,destination.transform,Incoming(.175f),Incoming(.225f),Vector3.up,out _),Is.True,"The initial stream must enter the tilted receiver");
   double excess=ContainerFlowGeometry.Excess(source.containers[0],sourceItem.transform.rotation,Vector3.up);var filled=c.Copy();filled.amountMl=Math.Min(excess,500*.75*.05*Math.Sqrt(excess/500));Assert.That(ContainerFlowGeometry.Excess(filled,destination.transform.rotation,Vector3.up),Is.GreaterThan(.000001),"This intake must make the receiver a new source");
   var nextOrigin=ContainerFlowGeometry.Lip(c,destination.transform,Vector3.up,out var nextOutward);Vector3 Outgoing(float time)=>nextOrigin+nextOutward*(.15f*time)+Physics.gravity*(.5f*time*time);
   Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"New stream obstruction",Outgoing(.5f),.1f,Color.white,out var wall,out error),Is.True,error);Assert.That(editor.SetItemPhysics(wall,new(){mode="fixed",shape="box",mass=1}),Is.True,editor.Status);
   Physics.SyncTransforms();var line=Outgoing(.525f)-Outgoing(.475f);var ray=new Ray(Outgoing(.475f),line.normalized);Assert.That(editor.Find(wall).GetComponentsInChildren<Collider>().Any(collider=>collider.Raycast(ray,out _,line.magnitude)),Is.True,"The loaded wall must intersect the newly possible stream");
   var oldBounds=new Bounds(origin,Vector3.zero);for(int i=1;i<=LiquidPouring.Segments;i++)oldBounds.Encapsulate(Incoming(i*.025f));oldBounds.Expand(.002f);Assert.That(editor.SpatialInputTargets(oldBounds,oldBounds,false,from),Does.Not.Contain(wall),"The obstruction must be outside the initial source's path bounds");
   Assert.That(editor.RetireNativeArea(NativeAreaFor(wall),out error),Is.True,error);physics.StartPhysics();editor.Liquids.Tick(.05f);
   Assert.That(FlowAmount(from),Is.EqualTo(400),"An initially empty receiver's future stream needs its inputs before the pass transfers anything");Assert.That(FlowAmount(to),Is.Zero);
   var observations=new JObject{["waitingSource"]=editor.Liquids.Observe(from),["waitingReceiver"]=editor.Liquids.Observe(to)};
   yield return NavigationLoadingDone();editor.Liquids.Tick(.05f);Assert.That(editor.Find(wall),Is.Not.Null);Assert.That(FlowAmount(to),Is.GreaterThan(0));Assert.That(FlowAmount(to,"spilledMl"),Is.GreaterThan(0));
   Assert.That(FlowAmount(from)+FlowAmount(to)+FlowAmount(to,"spilledMl"),Is.EqualTo(400).Within(1e-8));Assert.That(FlowAmount(from,"spilledMl"),Is.Zero);
   observations["flowingSource"]=editor.Liquids.Observe(from);observations["flowingReceiver"]=editor.Liquids.Observe(to);physics.PausePhysics();observations["acceptedSource"]=editor.Liquids.Observe(from);observations["acceptedReceiver"]=editor.Liquids.Observe(to);CaptureFlowEligibility("cascade",observations);
  }
  [UnityTest]public IEnumerator FlowEligibilityDryUprightCavityDoesNotLoadStreamOnlyGeometry(){
   var(from,to)=FixedFlowCups();Assert.That(editor.DeleteObject(to,out var error),Is.True,error);var data=editor.Read(from);data.rotation=Quaternion.identity;data.containers[0].amountMl=0;
   Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);
   var below=editor.Find(from).transform.position+Vector3.down;Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Unrelated dry ground",below,.25f,Color.white,out var wall,out error),Is.True,error);Assert.That(editor.SetItemPhysics(wall,new(){mode="fixed",shape="box",mass=1}),Is.True,editor.Status);
   Assert.That(editor.RetireNativeArea(NativeAreaFor(wall),out error),Is.True,error);physics.StartPhysics();editor.Liquids.Tick(.05f);yield return null;
   Assert.That(editor.Find(wall),Is.Null);Assert.That(editor.NativeActivationPending,Is.False);Assert.That(FlowAmount(from),Is.Zero);Assert.That(editor.Liquids.Active,Is.False);physics.PausePhysics();
  }
 }
}
