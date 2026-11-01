// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  string SurfaceQueryVessel(Vector3 position,float width=1,double amount=500,bool circular=false){
   physics.PausePhysics();editor.Liquids.enabled=false;
   Assert.IsTrue(editor.CreatePrimitive(RoomObjectKind.Block,"Surface query vessel",position,1,Color.white,out var id,out var error),error);
   var data=editor.Read(id);data.physics=ItemPhysics.Fixed;
   data.collision=new CollisionRecipe{shapes=new[]{new CollisionShape{id="floor",position=new Vector3(0,-.01f,0),size=new Vector3(width,.02f,width)}}};
   data.containers=new[]{new RoomContainer{version=circular?1:2,rectangle=circular?null:new ContainerRectangle{width=width,depth=width},radius=width*.5f,height=1,capacityMl=1000,amountMl=amount}};
   Assert.IsTrue(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),error);
   Assert.IsTrue(editor.Find(id).GetComponent<ContainerFillView>().NativeReady);
   Assert.IsTrue(editor.AuthoredCollisionReady(editor.Find(id)));return id;
  }
  float SurfaceQueryRenderedLevel(string id){
   var filter=editor.Find(id).GetComponentsInChildren<MeshFilter>().Single(value=>value.sharedMesh?.name=="Bounded liquid level");
   var points=filter.sharedMesh.vertices.Select(filter.transform.TransformPoint).ToArray();Assert.That(points.Length,Is.GreaterThan(3));
   Assert.That(points.Max(value=>value.y)-points.Min(value=>value.y),Is.LessThan(.000001f));return points[0].y;
  }
  [UnityTest]public IEnumerator LiquidSurfaceQuerySelectsNearestThenVolumeAndStableIdentity(){
   var lower=SurfaceQueryVessel(new(8,0,8),.8f);var wide=SurfaceQueryVessel(new(8,1,8),1.5f);
   var first=SurfaceQueryVessel(new(8,1,8));var second=SurfaceQueryVessel(new(8,1,8));
   var expected=new[]{first,second}.OrderBy(id=>id,StringComparer.Ordinal).First();
   float level=SurfaceQueryRenderedLevel(expected);var from=new Vector3(8,3,8);var to=new Vector3(8,-1,8);
   for(int pass=0;pass<3;pass++){
    editor.Liquids.CaptureSurfaceContacts();var hit=editor.Liquids.QuerySurfaceContact(from,to);
    Assert.AreEqual(LiquidSurfaceState.Surface,hit.State);Assert.AreEqual(expected,hit.Medium.Id);
    Assert.AreNotEqual(wide,hit.Medium.Id,"Equal-distance surfaces prefer the smaller finite volume");
    Assert.That(hit.Point.y,Is.EqualTo(level).Within(.000001f));Assert.That(hit.Fraction,Is.EqualTo((from.y-level)/(from.y-to.y)).Within(.000001f));
   }
   var below=editor.Liquids.QuerySurfaceContact(new(8,.9f,8),to);
   Assert.AreEqual(LiquidSurfaceState.Surface,below.State);Assert.AreEqual(lower,below.Medium.Id,"A higher cavity already behind the origin is not the next surface");
   Assert.IsFalse(physics.Running);yield return null;
  }
  [UnityTest]public IEnumerator LiquidSurfaceQueryExactEndpointImpactsButPlaneAndWetOriginsDoNot(){
   var id=SurfaceQueryVessel(new(8,0,8));float level=SurfaceQueryRenderedLevel(id);Assert.That(level,Is.EqualTo(.5f).Within(.000001f));
   var plane=new Vector3(8,level,8);var above=plane+Vector3.up;var below=plane-Vector3.up*.1f;
   editor.Liquids.CaptureSurfaceContacts();var end=editor.Liquids.QuerySurfaceContact(above,plane);
   Assert.AreEqual(LiquidSurfaceState.Surface,end.State);Assert.AreEqual(id,end.Medium.Id);Assert.AreEqual(1,end.Fraction);
   Assert.That(Vector3.Distance(end.Point,plane),Is.LessThan(.000001f));
   foreach(var origin in new[]{plane,below})foreach(var destination in new[]{above,below,origin}){
    var hit=editor.Liquids.QuerySurfaceContact(origin,destination);
    Assert.AreEqual(LiquidSurfaceState.Submerged,hit.State,"An origin already in/on water is suppression, never a fresh impact");
    Assert.AreEqual(0,hit.Fraction);Assert.AreEqual(origin,hit.Point);
   }
   Assert.AreEqual(0,editor.Find(id).GetComponent<ContainerFillView>().RippleCount,"The passive query itself must not publish a ripple");yield return null;
  }
  [UnityTest]public IEnumerator LiquidSurfaceQueryMissingWaterOnlyBlocksThePrefixBeforeProvenContact(){
   var known=SurfaceQueryVessel(new(8,0,8));var behind=SurfaceQueryVessel(new(8,-2,8));var ahead=SurfaceQueryVessel(new(8,1,8));
   editor.Find(behind).GetComponent<ContainerFillView>().enabled=false;
   var aheadView=editor.Find(ahead).GetComponent<ContainerFillView>();aheadView.enabled=false;
   int revision=editor.Revision;string snapshot=JsonUtility.ToJson(editor.Snapshot()),raw=File.ReadAllText(Path.Combine(directory,RoomStorage.FileName));
   var from=new Vector3(8,3,8);var to=new Vector3(8,-3,8);
   editor.Liquids.CaptureSurfaceContacts();Assert.AreEqual(LiquidSurfaceState.Unknown,editor.Liquids.QuerySurfaceContact(from,to).State);
   // A query beginning below the unavailable upper cavity reaches the complete
   // surface before the unavailable lower cavity. Neither absence may be loaded.
   editor.Liquids.CaptureSurfaceContacts();var hit=editor.Liquids.QuerySurfaceContact(new(8,.9f,8),to);
   Assert.AreEqual(LiquidSurfaceState.Surface,hit.State);Assert.AreEqual(known,hit.Medium.Id);
   Assert.AreEqual(LiquidSurfaceState.Clear,editor.Liquids.QuerySurfaceContact(new(12,3,8),new(12,-3,8)).State,"Known distant missing bounds must not poison an unrelated path");
   Assert.IsFalse(aheadView.enabled);Assert.IsFalse(editor.Find(behind).GetComponent<ContainerFillView>().enabled);
   Assert.IsFalse(editor.NativeActivationPending);Assert.IsFalse(physics.Running);Assert.AreEqual(revision,editor.Revision);
   Assert.AreEqual(snapshot,JsonUtility.ToJson(editor.Snapshot()));Assert.AreEqual(raw,File.ReadAllText(Path.Combine(directory,RoomStorage.FileName)));yield return null;
  }
  [UnityTest]public IEnumerator LiquidSurfaceQueryRejectsInvalidInputsGravityAndReplacedCaptures(){
   var id=SurfaceQueryVessel(new(8,0,8));var from=new Vector3(8,2,8);var to=new Vector3(8,0,8);
   editor.Liquids.CaptureMedia(false);Assert.AreEqual(LiquidSurfaceState.Unknown,editor.Liquids.QuerySurfaceContact(from,to).State);
   editor.Liquids.CaptureSurfaceContacts();Assert.AreEqual(LiquidSurfaceState.Surface,editor.Liquids.QuerySurfaceContact(from,to).State);
   foreach(var invalid in new[]{new Vector3(float.NaN,0,0),new Vector3(0,float.PositiveInfinity,0)}){
    Assert.AreEqual(LiquidSurfaceState.Unknown,editor.Liquids.QuerySurfaceContact(invalid,to).State);
    Assert.AreEqual(LiquidSurfaceState.Unknown,editor.Liquids.QuerySurfaceContact(from,invalid).State);
   }
   Assert.AreEqual(LiquidSurfaceState.Unknown,editor.Liquids.QuerySurfaceContact(new(float.MaxValue,0,0),new(-float.MaxValue,0,0)).State);
   var gravity=Physics.gravity;try{
    Physics.gravity=Vector3.zero;editor.Liquids.CaptureSurfaceContacts();Assert.AreEqual(LiquidSurfaceState.Unknown,editor.Liquids.QuerySurfaceContact(from,to).State);
   }finally{Physics.gravity=gravity;}
   editor.Liquids.CaptureSurfaceContacts();Assert.AreEqual(id,editor.Liquids.QuerySurfaceContact(from,to).Medium.Id);
   editor.Liquids.NavigationAvailability(new Bounds(from,Vector3.one),null);
   Assert.AreEqual(LiquidSurfaceState.Unknown,editor.Liquids.QuerySurfaceContact(from,to).State,"A different missing-dependency capture invalidates this synchronous batch");
   editor.Liquids.CaptureSurfaceContacts();Assert.AreEqual(LiquidSurfaceState.Surface,editor.Liquids.QuerySurfaceContact(from,to).State);yield return null;
  }
  [UnityTest]public IEnumerator LiquidSurfaceQueryIncompleteGeometryStaysUnknownWithoutChangingExistingMediumFacts(){
   var id=SurfaceQueryVessel(new(8,0,8));var item=editor.Find(id);var rigid=item.GetComponent<RigidRoomItem>();
   var from=new Vector3(8,2,8);var to=new Vector3(8,.2f,8);
   rigid.SetGeometryReady(false);editor.Liquids.CaptureSurfaceContacts();Assert.AreEqual(LiquidSurfaceState.Unknown,editor.Liquids.QuerySurfaceContact(from,to).State);
   var fact=editor.Liquids.ObserveMedium(to,"");Assert.IsTrue((bool)fact["found"],"The existing false-mode fact retains known geometry while reporting inactive physics");Assert.IsFalse((bool)fact["active"]);
   rigid.SetGeometryReady(true);var collider=item.SolidColliders.Single();collider.enabled=false;
   editor.Liquids.CaptureSurfaceContacts();Assert.AreEqual(LiquidSurfaceState.Unknown,editor.Liquids.QuerySurfaceContact(from,to).State);
   collider.enabled=true;editor.Liquids.CaptureSurfaceContacts();Assert.AreEqual(LiquidSurfaceState.Surface,editor.Liquids.QuerySurfaceContact(from,to).State);
   Assert.IsFalse(editor.NativeActivationPending);Assert.IsFalse(physics.Running);yield return null;
  }
  [UnityTest]public IEnumerator LiquidSurfaceQuerySideEntryAndCompletePassThroughSuppressWithoutRippleOrMutation(){
   var id=SurfaceQueryVessel(new(8,0,8));var view=editor.Find(id).GetComponent<ContainerFillView>();
   string snapshot=JsonUtility.ToJson(editor.Snapshot()),raw=File.ReadAllText(Path.Combine(directory,RoomStorage.FileName));int revision=editor.Revision,events=0;
   editor.MediumContact+=_=>events++;editor.Liquids.CaptureSurfaceContacts();
   foreach(var destination in new[]{new Vector3(8,.2f,8),new Vector3(9,.2f,8)}){
    var hit=editor.Liquids.QuerySurfaceContact(new(7,.4f,8),destination);
    Assert.AreEqual(LiquidSurfaceState.Submerged,hit.State,"Wind entering below the finite free surface must suppress the drop even when its endpoint is dry again");
    Assert.AreEqual(id,hit.Medium.Id);Assert.That(hit.Fraction,Is.GreaterThan(0).And.LessThan(1));Assert.That(hit.Point.x,Is.EqualTo(7.5f).Within(.000001f));
   }
   Assert.AreEqual(0,view.RippleCount);Assert.AreEqual(0,events);Assert.AreEqual(revision,editor.Revision);
   Assert.AreEqual(snapshot,JsonUtility.ToJson(editor.Snapshot()));Assert.AreEqual(raw,File.ReadAllText(Path.Combine(directory,RoomStorage.FileName)));yield return null;
  }
  [UnityTest]public IEnumerator LiquidSurfaceQueryEarlierSideEntryCannotBorrowALaterSurfaceImpact(){
   var later=SurfaceQueryVessel(new(9,-.3f,8));var from=new Vector3(6,.8f,8);var to=new Vector3(10,0,8);
   editor.Liquids.CaptureSurfaceContacts();var surface=editor.Liquids.QuerySurfaceContact(from,to);
   Assert.AreEqual(LiquidSurfaceState.Surface,surface.State);Assert.AreEqual(later,surface.Medium.Id);
   var earlier=SurfaceQueryVessel(new(7,.5f,8));editor.Liquids.CaptureSurfaceContacts();var first=editor.Liquids.QuerySurfaceContact(from,to);
   Assert.AreEqual(LiquidSurfaceState.Submerged,first.State);Assert.AreEqual(earlier,first.Medium.Id);Assert.That(first.Fraction,Is.GreaterThan(0).And.LessThan(surface.Fraction));
   Assert.AreEqual(0,editor.Find(earlier).GetComponent<ContainerFillView>().RippleCount);Assert.AreEqual(0,editor.Find(later).GetComponent<ContainerFillView>().RippleCount);yield return null;
  }
  [UnityTest]public IEnumerator LiquidSurfaceQueryCircularRimRejectsSquareCornersButFindsActualChord(){
   var id=SurfaceQueryVessel(new(8,0,8),circular:true);editor.Liquids.CaptureSurfaceContacts();
   var corner=editor.Liquids.QuerySurfaceContact(new(7.4f,.4f,8.49f),new(7.6f,.2f,8.49f));
   Assert.AreEqual(LiquidSurfaceState.Clear,corner.State,"A square cavity bound must not become the actual circular water volume");
   Assert.AreEqual(LiquidSurfaceState.Clear,editor.Liquids.QuerySurfaceContact(new(7.6f,2,8.49f),new(7.6f,0,8.49f)).State);
   var chord=editor.Liquids.QuerySurfaceContact(new(7.4f,.4f,8.3f),new(8.6f,.2f,8.3f));
   Assert.AreEqual(LiquidSurfaceState.Submerged,chord.State);Assert.AreEqual(id,chord.Medium.Id);
   Assert.That(chord.Point.x,Is.EqualTo(7.6f).Within(.000002f));Assert.That(chord.Fraction,Is.GreaterThan(0).And.LessThan(1));
   Assert.AreEqual(0,editor.Find(id).GetComponent<ContainerFillView>().RippleCount);yield return null;
  }
  [UnityTest]public IEnumerator LiquidSurfaceQueryTerrainSideSweepUsesWetTrianglesAndActualBedEntry(){
   physics.PausePhysics();var(_,id)=TerrainReservoir(true);editor.Liquids.enabled=false;
   int revision=editor.Revision;var before=JsonUtility.ToJson(editor.Read(id));editor.Liquids.CaptureSurfaceContacts();
   var wet=editor.Liquids.QuerySurfaceContact(TerrainPoint(id,-.75f,.15f,-.75f),TerrainPoint(id,-.75f,.15f,.75f));
   Assert.AreEqual(LiquidSurfaceState.Submerged,wet.State);Assert.AreEqual(id,wet.Medium.Id);
   Assert.That(Vector3.Distance(wet.Point,TerrainPoint(id,-.75f,.15f,-.5f)),Is.LessThan(.000002f));
   var dry=editor.Liquids.QuerySurfaceContact(TerrainPoint(id,.75f,.15f,-.75f),TerrainPoint(id,.75f,.15f,.75f));
   Assert.AreEqual(LiquidSurfaceState.Clear,dry.State,"A path below the high dry bed has no liquid despite crossing the reservoir footprint");
   var emerging=editor.Liquids.QuerySurfaceContact(TerrainPoint(id,.75f,.15f),TerrainPoint(id,-.75f,.15f));
   Assert.AreEqual(LiquidSurfaceState.Submerged,emerging.State);Assert.AreEqual(id,emerging.Medium.Id);
   Assert.That(Vector3.Distance(emerging.Point,TerrainPoint(id,-.25f,.15f)),Is.LessThan(.000002f),"First wet entry is where the actual sloping triangle drops below the path");
   Assert.AreEqual(0,editor.Find(id).GetComponent<ContainerFillView>().RippleCount);Assert.AreEqual(revision,editor.Revision);
   Assert.AreEqual(before,JsonUtility.ToJson(editor.Read(id)));Assert.IsFalse(physics.Running);Assert.IsFalse(editor.NativeActivationPending);yield return null;
  }
 }
}
