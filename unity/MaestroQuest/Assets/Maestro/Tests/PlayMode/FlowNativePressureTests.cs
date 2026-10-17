// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
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
  static void RemoveFlowFixtureStarters(RoomEditor owner) {
   var ids=owner.Snapshot().objects.Where(value=>!value.IsBuiltIn).Select(value=>value.id).ToArray();
   Assert.That(owner.ApplyAgentEdit(owner.Revision,Array.Empty<RoomObjectData>(),ids,out var error),Is.True,error);
  }
  static RoomObjectData[] FlowFixtureCopies(RoomEditor owner,string source,int count,Vector3 start) {
   return Enumerable.Range(0,count).Select(i=>{
    var copy=owner.Read(source);copy.id=Guid.NewGuid().ToString("N");copy.name="Distant empty cup "+i;
    copy.position=start+new Vector3((i%10)*.3f,0,(i/10)*.3f);copy.rotation=Quaternion.identity;
    copy.physics=ItemPhysics.Fixed;copy.containers[0].amountMl=0;return copy;
   }).ToArray();
  }
  static JObject NativeFlowHost(RoomEditor owner,string[] sources,string[] receivers) {
   var work=owner.Liquids.LastFlowExecution;var discovery=owner.Liquids.LastFlowDiscovery;
   var saved=owner.Snapshot().objects.Where(value=>value.containers?.Length==1).ToArray();
   return new JObject {
    ["worldId"]=owner.WorldIdentity.worldId,["vessels"]=saved.Length,
    ["nativeVessels"]=saved.Count(value=>owner.Find(value.id)),
    ["dormantVessels"]=saved.Count(value=>owner.NativePhase(value.id)==NativeEntityPhase.Dormant),
    ["pending"]=owner.NativeActivationPending,["ready"]=FlowWorkResources.Ready(owner.Liquids,out _),
    ["sources"]=new JArray(sources.Select(id=>owner.Liquids.Observe(id))),
    ["receivers"]=new JArray(receivers.Select(id=>owner.Liquids.Observe(id))),
    ["executedGroups"]=work.Groups,["executedMembers"]=work.Members,
    ["pourSegments"]=work.PourSegments,["pourCandidates"]=work.PourCandidates,
    ["scoopCandidates"]=work.ScoopCandidates,["discoveryCaptures"]=discovery.Captures,
    ["collisionReads"]=discovery.CollisionReads,["cavityReads"]=discovery.CavityReads
   };
  }
  sealed class NativeFlowFixture {
   internal RoomEditor Editor;internal RoomPhysicsWorld Physics;internal string[] Sources,Receivers;
   internal readonly List<string> Dormant=new();
  }
  IEnumerator ParkFlowFixtureCopies(NativeFlowFixture fixture,int count) {
   int offset=0;
   while(offset<count) {
    int batch=Math.Min(6,count-offset);
    var copies=FlowFixtureCopies(fixture.Editor,fixture.Receivers[0],batch,new Vector3(-4,1,20+offset*.05f));
    Assert.That(fixture.Editor.ApplyAgentEdit(fixture.Editor.Revision,copies,Array.Empty<string>(),out var error),Is.True,error);
    var views=copies.Select(copy=>fixture.Editor.Find(copy.id)).ToArray();
    string area=Guid.NewGuid().ToString("N");
    Assert.That(fixture.Editor.EditRegion(new RoomRegion{id=area,name="Dormant test cups"},area,0,Array.Empty<string>(),out error),Is.True,error);
    foreach(var copy in copies) {
     Assert.That(fixture.Editor.BindRegion(copy.id,fixture.Editor.ObjectRevision(copy.id),area,fixture.Editor.RegionRevision(area),out error),Is.True,error);
     fixture.Dormant.Add(copy.id);
    }
    Assert.That(fixture.Editor.RetireNativeArea(area,out error),Is.True,error);
    yield return null;yield return null;
    Assert.That(views.All(item=>!item),Is.True,"Retirement must destroy actual native views, not just disable them");
    Assert.That(fixture.Dormant.All(id=>fixture.Editor.NativePhase(id)==NativeEntityPhase.Dormant&&fixture.Editor.Find(id)==null),Is.True);
    offset+=batch;
   }
  }
  // Uses real, independently saved rooms, not synthetic scheduler claims.
  // This is process-wide contention evidence, not one world's size/performance.
  [UnityTest]public IEnumerator FlowNativePressureValidRoomsTakeTurnsWithDormantCupsAndPreserveUndo() {
   RemoveFlowFixtureStarters(editor);
   var(a,b,c,d)=IndependentFlowCups();var main=editor;var mainPhysics=physics;
   var fixtures=new List<NativeFlowFixture>{new(){Editor=main,Physics=mainPhysics,Sources=new[]{a,c},Receivers=new[]{b,d}}};
   var secondaryRoots=new List<GameObject>();var evidence=new JObject();var trace=new JArray();evidence["trace"]=trace;
   try {
    yield return ParkFlowFixtureCopies(fixtures[0],RoomContainer.MaximumPerRoom-4);
    for(int roomIndex=1;roomIndex<9;roomIndex++) {
     var secondaryRoot=new GameObject("Independent native flow room "+roomIndex);secondaryRoot.transform.SetParent(root.transform,false);secondaryRoots.Add(secondaryRoot);
     var room=secondaryRoot.AddComponent<RoomInteraction>();
     RoomItem Included(string name) {
      var go=new GameObject(name);go.transform.SetParent(secondaryRoot.transform,false);
      var collider=go.AddComponent<BoxCollider>();collider.size=Vector3.one*.1f;
      var item=go.AddComponent<RoomItem>();item.Configure(new Collider[]{collider});room.Register(item);return item;
     }
     var book=Included("book");var avatar=Included("maestro");
     var otherPhysics=secondaryRoot.AddComponent<RoomPhysicsWorld>();
     var other=secondaryRoot.AddComponent<RoomEditor>();
     other.Initialize(room,book,avatar,Path.Combine(directory,"native-room-"+roomIndex),otherPhysics);
     RemoveFlowFixtureStarters(other);
     other.Liquids.enabled=false;otherPhysics.SetSurfaces(true,"Test room");
     var pair=new[]{a,b}.Select(id=>{var value=main.Read(id);value.id=Guid.NewGuid().ToString("N");value.position+=Vector3.forward*(2*roomIndex);return value;}).ToArray();
     Assert.That(other.ApplyAgentEdit(other.Revision,pair,Array.Empty<string>(),out var error),Is.True,error);
     var fixture=new NativeFlowFixture{Editor=other,Physics=otherPhysics,Sources=new[]{pair[0].id},Receivers=new[]{pair[1].id}};fixtures.Add(fixture);
     yield return ParkFlowFixtureCopies(fixture,RoomContainer.MaximumPerRoom-2);
    }
    editor=main;physics=mainPhysics;
    Assert.That(fixtures.All(f=>f.Editor.Liquids.FlowWorkVessels==RoomContainer.MaximumPerRoom),Is.True);
    Assert.That(fixtures.Select(f=>f.Editor.WorldIdentity.worldId).Distinct().Count(),Is.EqualTo(9));
    Assert.That((int)LiquidResources.ObserveBudget()["instances"],Is.EqualTo(20));
    Assert.That((int)LiquidResources.ObserveBudget()["retiring"],Is.Zero);
    Assert.That(fixtures.Sum(f=>f.Dormant.Count),Is.EqualTo(124));
    evidence["rooms"]=9;evidence["savedContainers"]=144;evidence["nativeContainers"]=20;evidence["dormantContainers"]=124;
    evidence["hostUnits"]=fixtures.Sum(f=>FlowWorkResources.Demand(f.Editor.Liquids.FlowWorkVessels));
    evidence["flowUnitLimit"]=FlowWorkResources.MaximumUnits;evidence["groupUnits"]=10*FlowWorkResources.Demand(2);
    evidence["resourceBudgets"]=new JObject {
     ["objects"]=NativeObjectResources.ObserveBudget(),["physics"]=NativePhysicsResources.ObserveBudget(),
     ["recipes"]=RecipeResources.ObserveBudget(),["collision"]=CollisionResources.ObserveBudget(),
     ["liquids"]=LiquidResources.ObserveBudget()
    };
    evidence["discoveryUnits"]=fixtures.Sum(f=>FlowDiscoveryWork.Demand(f.Editor.Liquids.FlowWorkVessels,f.Editor.SpatialObjectCount));
    evidence["discoveryUnitLimit"]=FlowDiscoveryWork.MaximumUnits;
    Assert.That((int)evidence["hostUnits"],Is.GreaterThan(FlowWorkResources.MaximumUnits));
    Assert.That((int)evidence["discoveryUnits"],Is.LessThan(FlowDiscoveryWork.MaximumUnits));
    foreach(var f in fixtures) {
     var saved=new RoomStorage(f.Editor.SaveDirectory).Load(out var error);
     Assert.That(saved,Is.Not.Null,error);Assert.That(saved.Validate(out error),Is.True,error);
     Assert.That(saved.objects.Count(value=>value.containers?.Length==1),Is.EqualTo(RoomContainer.MaximumPerRoom));
     f.Physics.StartPhysics();
    }
    foreach(var f in fixtures)f.Editor.Liquids.Tick(.05f);
    void Capture() {trace.Add(new JObject{["step"]=FlowWorkResources.Step,["rooms"]=new JArray(fixtures.Select(f=>NativeFlowHost(f.Editor,f.Sources,f.Receivers))),["flowBudget"]=FlowWorkResources.ObserveBudget(),["discoveryBudget"]=FlowWorkResources.ObserveDiscovery(),["nativeBudget"]=LiquidResources.ObserveBudget()});}
    Capture();
    Assert.That(main.Liquids.LastFlowExecution.Groups,Is.EqualTo(2));
    Assert.That(main.Liquids.LastFlowExecution.Members,Is.EqualTo(4));
    Assert.That(main.Liquids.LastFlowExecution.PourCandidates,Is.EqualTo(2*main.Liquids.LastFlowExecution.PourSegments));
    Assert.That(FlowAmount(b),Is.GreaterThan(0));Assert.That(FlowAmount(d),Is.GreaterThan(0));
    var last=fixtures.Last();
    double Received(NativeFlowFixture f)=>(double)f.Editor.Liquids.Observe(f.Receivers[0])["contents"]["amountMl"];
    Assert.That(Received(last),Is.GreaterThan(0),"All ten ready circuits must fit without paying for 124 unrelated dormant cups");
    Assert.That(fixtures.Sum(f=>f.Editor.Liquids.LastFlowExecution.Groups),Is.EqualTo(10));
    Assert.That((long)FlowWorkResources.ObserveBudget()["units"],Is.EqualTo(10*FlowWorkResources.Demand(2)));
    Assert.That(last.Editor.Liquids.LastFlowDiscovery.Captures,Is.EqualTo(1));
    long step=FlowWorkResources.Step;
    for(int read=0;read<5;read++)foreach(var f in fixtures)NativeFlowHost(f.Editor,f.Sources,f.Receivers);
    Assert.That(FlowWorkResources.Step,Is.EqualTo(step),"Passive inspection cannot serve waiting work");
    for(int attempt=0;attempt<4&&Received(last)==0;attempt++) {
     FlowWorkResources.Advance(.05f);
     foreach(var f in fixtures)f.Editor.Liquids.Tick(.05f);
     Capture();
    }
    Assert.That(Received(last),Is.GreaterThan(0).And.LessThanOrEqualTo(18.75),"Skipped opportunities cannot create catch-up intake");
    Assert.That(fixtures.All(f=>Received(f)>0),Is.True,"Every real room must receive an opportunity");
    // A real receiver crosses quantity groups while all other owners remain
    // active. Service cannot require dropping pressure or recreating the room.
    var moved=main.Find(b);var originalPosition=moved.transform.position;
    foreach(bool merge in new[]{true,false}) {
     moved.transform.position=merge?main.Find(d).transform.position+Vector3.right*.03f:originalPosition;
     moved.GetComponent<RigidRoomItem>().Teleported();
     bool served=false;
     for(int attempt=0;attempt<fixtures.Count&&!served;attempt++) {
      FlowWorkResources.Advance(.05f);
      foreach(var f in fixtures)f.Editor.Liquids.Tick(.05f);
      served=main.Liquids.LastFlowExecution.Members==4;
     }
     Assert.That(served,Is.True,"Actual regrouping must not starve the room");
     var work=main.Liquids.LastFlowExecution;
     Assert.That(work.Groups,Is.EqualTo(2));
     Assert.That(work.ScoopCandidates,Is.EqualTo(merge?10:8),"Execute the rebuilt 1+3 or 2+2 complete quantity groups");
     Capture();((JObject)trace.Last)["topology"]=merge?"merged":"split";
    }
    foreach(var f in fixtures) {
     double total=f.Sources.Concat(f.Receivers).Sum(id=>(double)f.Editor.Liquids.Observe(id)["contents"]["amountMl"]+(double)f.Editor.Liquids.Observe(id)["contents"]["spilledMl"]);
     Assert.That(total,Is.EqualTo(400*f.Sources.Length).Within(1e-8));
     Assert.That(f.Dormant.All(id=>f.Editor.Find(id)==null&&f.Editor.Read(id).containers[0].amountMl==0),Is.True);
     Assert.That(f.Editor.NativeActivationPending,Is.False);f.Physics.PausePhysics();
    }
    double accepted=Received(last);Assert.That(last.Editor.Read(last.Receivers[0]).containers[0].amountMl,Is.EqualTo(accepted));
    main.Undo();Assert.That(FlowAmount(a),Is.EqualTo(400));Assert.That(FlowAmount(c),Is.EqualTo(400));Assert.That(FlowAmount(b)+FlowAmount(d),Is.Zero);
    Assert.That(main.Snapshot().objects.Length,Is.EqualTo(RoomContainer.MaximumPerRoom+2));
    Assert.That(fixtures[0].Dormant.All(id=>main.Find(id)==null),Is.True);
    Assert.That(last.Editor.Read(last.Receivers[0]).containers[0].amountMl,Is.EqualTo(accepted),"Undo stays within its own world");
    evidence["conservationAndUndo"]=true;
    string output=Environment.GetEnvironmentVariable("MAESTRO_FLOW_NATIVE_PRESSURE_EVIDENCE");
    if(!string.IsNullOrEmpty(output))File.WriteAllText(output,evidence.ToString());
   } finally {
    editor=main;physics=mainPhysics;
    foreach(var f in fixtures)if(f.Physics)f.Physics.PausePhysics();
    foreach(var go in secondaryRoots)UnityEngine.Object.Destroy(go);
   }
   yield return null;
  }
 }
}
