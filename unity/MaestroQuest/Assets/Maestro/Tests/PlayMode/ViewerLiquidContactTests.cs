// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  static readonly Vector3 ViewerDry=new(4,2,0),ViewerWet=new(4,1.4f,0);
  List<LiquidContact> ViewerEvents(){
   var events=new List<LiquidContact>();
   editor.MediumContact+=e=>{if(e.Participant=="input:viewer")events.Add(e);};
   return events;
  }
  void ViewerContact(Vector3? point){editor.Liquids.SetViewerContact(point);ContactTick();}
  void AssertViewer(bool known,bool immersed){
   var state=editor.Liquids.ObserveContact("input:viewer");
   Assert.That((bool)state["known"],Is.EqualTo(known),state.ToString());
   Assert.That((bool)state["immersed"],Is.EqualTo(immersed),state.ToString());
   Assert.That(MediumContactFacts.Viewer().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,null,out _),Is.True,
    "User, agent and program must read the same typed fact");
  }
  [UnityTest]public IEnumerator ViewerLiquidContactsUseTrackedViewWithoutHandsWhileLookingDown(){
   var(pool,prop)=MediumFixture(.1f);editor.Find(prop).GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);
   var events=ViewerEvents();string before=JsonUtility.ToJson(editor.Snapshot());int revision=editor.Revision;
   var physical=new GameObject("Independent physical viewer");
   var camera=new GameObject("Tracked viewer").AddComponent<Camera>();camera.transform.SetParent(physical.transform,false);
   camera.transform.SetPositionAndRotation(ViewerDry,Quaternion.Euler(90,0,0));
   var view=physical.AddComponent<VirtualRoomView>();view.Initialize(root.transform,physical.transform,camera,null,physics);
   bool tracked=true;var viewpoint=root.AddComponent<WorkspaceViewpoint>();viewpoint.Initialize(editor,view,()=>tracked);
   try{
    ContactTick();AssertViewer(true,false);Assert.That(events,Is.Empty);
    var dry=editor.Liquids.ObserveContact("input:viewer");
    camera.transform.position=ViewerWet;viewpoint.Tick();ContactTick();AssertViewer(true,true);
    Assert.That(events.Select(e=>e.Phase),Is.EqualTo(new[]{"entered"}));Assert.That(events[0].Kind,Is.EqualTo("viewer"));
    Assert.That(events[0].BodyId,Is.EqualTo(pool));Assert.That(events[0].Point.y,Is.EqualTo(1.6f).Within(.001f));
    Assert.That((float)editor.Liquids.ObserveContact("input:viewer")["depthMetres"],Is.EqualTo(.2f).Within(.001f));
    Assert.That((bool)editor.Liquids.ObserveContact("input:left")["known"],Is.False);
    Assert.That((bool)editor.Liquids.ObserveContact("input:right")["known"],Is.False);
    var wet=editor.Liquids.ObserveContact("input:viewer");
    tracked=false;viewpoint.Tick();AssertViewer(false,false);
    var unavailable=editor.Liquids.ObserveContact("input:viewer");
    camera.transform.position=ViewerDry;tracked=true;viewpoint.Tick();ContactTick();AssertViewer(true,false);
    Assert.That(events.Count,Is.EqualTo(1),"Reacquisition is a baseline, not an exit");
    viewpoint.enabled=false;AssertViewer(false,false);
    Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));
    Assert.That(editor.Read(pool).containers[0].amountMl,Is.EqualTo(400000));
    string evidence=Environment.GetEnvironmentVariable("MAESTRO_VIEWER_CONTACT_EVIDENCE");
    if(!string.IsNullOrEmpty(evidence))File.WriteAllText(evidence,new JObject{
     ["dry"]=dry,["wet"]=wet,["unavailable"]=unavailable,["events"]=new JArray(events.Select(e=>new JObject{
      ["participantId"]=e.Participant,["kind"]=e.Kind,["phase"]=e.Phase,["speed"]=e.Speed,["depth"]=e.Depth,
      ["x"]=e.Point.x,["y"]=e.Point.y,["z"]=e.Point.z})),["workBudget"]=ContactWorkResources.ObserveBudget()}.ToString());
   }finally{UnityEngine.Object.DestroyImmediate(viewpoint);UnityEngine.Object.DestroyImmediate(physical);}
   yield return null;
  }
  [UnityTest]public IEnumerator ViewerLiquidContactsRelocationEstablishesBaselineWithoutASplash(){
   var(pool,prop)=MediumFixture(.1f);physics.PausePhysics();editor.Find(prop).gameObject.SetActive(false);
   var data=editor.Read(pool);data.position=new Vector3(4,0,0);
   data.collision=new CollisionRecipe{shapes=new[]{new CollisionShape{id="floor",position=new Vector3(0,-.05f,0),size=new Vector3(1,.1f,1)}}};
   data.containers=new[]{new RoomContainer{version=2,rectangle=new(){width=1,depth=1},height=2,capacityMl=2000,amountMl=1800}};
   Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},new[]{prop},out var error),Is.True,error);
   physics.StartPhysics();var events=ViewerEvents();
   var physical=new GameObject("Relocating viewer");
   var camera=new GameObject("Tracked view").AddComponent<Camera>();camera.transform.SetParent(physical.transform,false);
   camera.transform.SetPositionAndRotation(new Vector3(6,1.6f,0),Quaternion.identity);
   var view=physical.AddComponent<VirtualRoomView>();view.Initialize(root.transform,physical.transform,camera,null,physics);
   var viewpoint=root.AddComponent<WorkspaceViewpoint>();viewpoint.Initialize(editor,view,()=>true);
   try{
    ContactTick();AssertViewer(true,false);var physicalPosition=camera.transform.position;
    Assert.That(viewpoint.PreparePlacement(new RoomViewpoint{active=true,position=editor.Frame.PointToRoom(editor.Find(pool).transform.position)+Vector3.up*.05f},out error),Is.True,error);
    viewpoint.ApplyPlacement();AssertViewer(false,false);viewpoint.Tick();ContactTick();AssertViewer(true,true);
    Assert.That(camera.transform.position,Is.EqualTo(physicalPosition),"World movement must not move physical tracking");
    Assert.That(events,Is.Empty);Assert.That(editor.Find(pool).GetComponent<ContainerFillView>().RippleCount,Is.Zero);
    // Subsequent real movement is a normal edge, unlike the discrete relocation.
    camera.transform.position+=Vector3.up*.4f;viewpoint.Tick();ContactTick();AssertViewer(true,false);
    Assert.That(events.Select(e=>e.Phase),Is.EqualTo(new[]{"exited"}));
   }finally{UnityEngine.Object.DestroyImmediate(viewpoint);UnityEngine.Object.DestroyImmediate(physical);}
   yield return null;
  }
  [UnityTest]public IEnumerator ViewerLiquidContactsAreFiniteAndOnlyEmitMembershipEdges(){
   var(pool,prop)=MediumFixture(.1f);editor.Find(prop).GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);var events=ViewerEvents();
   ViewerContact(ViewerDry);ViewerContact(ViewerWet);ViewerContact(ViewerWet);AssertViewer(true,true);
   ViewerContact(new Vector3(5,1.4f,0));AssertViewer(true,false);
   Assert.That(events.Select(e=>e.Phase),Is.EqualTo(new[]{"entered","exited"}));
   Assert.That(events.All(e=>e.BodyId==pool&&e.Kind=="viewer"),Is.True);
   ViewerContact(new Vector3(float.NaN,1,0));AssertViewer(false,false);
   ViewerContact(ViewerWet);AssertViewer(true,true);Assert.That(events.Count,Is.EqualTo(2));
   yield return null;
  }
  [UnityTest]public IEnumerator ViewerLiquidContactsExpireAndPauseWithoutInventingCrossings(){
   var(_,prop)=MediumFixture(.1f);editor.Find(prop).GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);var events=ViewerEvents();
   ViewerContact(ViewerDry);yield return new WaitForSecondsRealtime(.16f);AssertViewer(false,false);
   ViewerContact(ViewerWet);AssertViewer(true,true);Assert.That(events,Is.Empty);
   physics.PausePhysics();AssertViewer(false,false);physics.StartPhysics();
   ViewerContact(ViewerDry);AssertViewer(true,false);Assert.That(events,Is.Empty);
   editor.Liquids.SetViewerContact(null);ViewerContact(ViewerWet);AssertViewer(true,true);Assert.That(events,Is.Empty);
  }
  [UnityTest]public IEnumerator ViewerLiquidContactsNeedTheirOwnGlobalEnvironmentReadiness(){
   var(_,prop)=MediumFixture(.1f);editor.Find(prop).GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);var events=ViewerEvents();
   ViewerContact(ViewerDry);AssertViewer(true,false);physics.Contains=p=>p.y>=1.7f;
   ViewerContact(ViewerWet);AssertViewer(false,false);Assert.That(events,Is.Empty);
   physics.Contains=p=>true;ViewerContact(ViewerWet);AssertViewer(true,true);Assert.That(events,Is.Empty);
   yield return null;
  }
  [UnityTest]public IEnumerator ViewerLiquidContactsDiscardAChangedViewDuringEventDelivery(){
   var(_,prop)=MediumFixture(.1f);editor.Find(prop).GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);var events=ViewerEvents();
   editor.Liquids.SetContactInput(0,ViewerDry,true);ViewerContact(ViewerDry);
   editor.MediumContact+=e=>{if(e.Participant=="input:left")editor.Liquids.SetViewerContact(ViewerDry);};
   editor.Liquids.SetContactInput(0,ViewerWet,true);ViewerContact(ViewerWet);
   Assert.That(events,Is.Empty,"The earlier hand listener replaced the sampled viewer");
   AssertViewer(false,false);ViewerContact(ViewerWet);AssertViewer(true,true);Assert.That(events,Is.Empty);
   yield return null;
  }
 }
}
