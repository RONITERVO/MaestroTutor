// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Avatar;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  [UnityTest]public IEnumerator LiquidContactOrderDownwardTrackedCrossingSelectsTheFirstSurfaceRatherThanTheFirstId(){
   var(first,prop)=MediumFixture(.1f);physics.PausePhysics();editor.Find(prop).gameObject.SetActive(false);
   Assert.IsTrue(editor.CopyObject(first,editor.ObjectRevision(first),"Second contact basin",new Vector3(4,2,0),out var second,out var error),error);
   var ids=new[]{first,second}.OrderBy(id=>id,StringComparer.Ordinal).ToArray();string lower=ids[0],upper=ids[1];
   var low=editor.Read(lower);low.position=new Vector3(4,1,0);var high=editor.Read(upper);high.position=new Vector3(4,2,0);
   Assert.IsTrue(editor.ApplyAgentEdit(editor.Revision,new[]{low,high},Array.Empty<string>(),out error),error);
   Assert.That(StringComparer.Ordinal.Compare(lower,upper),Is.LessThan(0),"Fixture ordering must oppose downward geometric order");
   yield return null;Physics.SyncTransforms();
   var lowerItem=editor.Find(lower);var upperItem=editor.Find(upper);
   var lowerWater=lowerItem.GetComponent<ContainerFillView>();var upperWater=upperItem.GetComponent<ContainerFillView>();
   Assert.IsTrue(lowerWater.NativeReady&&upperWater.NativeReady);lowerWater.ClearRipples();upperWater.ClearRipples();
   var lowerSurface=lowerItem.GetComponentsInChildren<MeshFilter>().Single(value=>value.sharedMesh?.name=="Bounded liquid level");
   var upperSurface=upperItem.GetComponentsInChildren<MeshFilter>().Single(value=>value.sharedMesh?.name=="Bounded liquid level");
   float lowerLevel=lowerSurface.sharedMesh.vertices.Select(lowerSurface.transform.TransformPoint).Average(point=>point.y);
   float upperLevel=upperSurface.sharedMesh.vertices.Select(upperSurface.transform.TransformPoint).Average(point=>point.y);
   Assert.That(lowerLevel,Is.EqualTo(1.6f).Within(.0001f));Assert.That(upperLevel,Is.EqualTo(2.6f).Within(.0001f));
   int revision=editor.Revision,lowerRevision=editor.ObjectRevision(lower),upperRevision=editor.ObjectRevision(upper);
   string snapshot=JsonUtility.ToJson(editor.Snapshot());var path=Path.Combine(directory,RoomStorage.FileName);var saved=File.ReadAllBytes(path);
   var events=new List<LiquidContact>();editor.MediumContact+=value=>{if(value.Participant=="input:left")events.Add(value);};
   physics.StartPhysics();ContactPoint(new Vector3(4,3,0),false);
   var before=editor.Liquids.ObserveContact("input:left");Assert.IsTrue((bool)before["known"],before.ToString());Assert.IsFalse((bool)before["immersed"]);
   Assert.That(events,Is.Empty);ContactPoint(new Vector3(4,1,0),false);ContactTick();
   Assert.AreEqual(1,events.Count,"One strict dry-to-dry crossing keeps the existing single representative event contract");
   var contact=events[0];Assert.AreEqual("crossed",contact.Phase);Assert.AreEqual("controller",contact.Kind);
   var after=editor.Liquids.ObserveContact("input:left");Assert.IsTrue((bool)after["known"],after.ToString());Assert.IsFalse((bool)after["immersed"]);
   var lowerProperties=new MaterialPropertyBlock();var upperProperties=new MaterialPropertyBlock();
   lowerSurface.GetComponent<Renderer>().GetPropertyBlock(lowerProperties);upperSurface.GetComponent<Renderer>().GetPropertyBlock(upperProperties);
   Assert.AreEqual(lowerWater.RippleCount,(int)lowerProperties.GetFloat("_LiquidRippleCount"));Assert.AreEqual(upperWater.RippleCount,(int)upperProperties.GetFloat("_LiquidRippleCount"));
   int lowerRipples=lowerWater.RippleCount,upperRipples=upperWater.RippleCount;
   if(upperRipples>0){var impacts=upperProperties.GetVectorArray("_LiquidRipples");Assert.That(impacts.Length,Is.GreaterThanOrEqualTo(upperRipples));Assert.That(impacts[0].y,Is.EqualTo(upperLevel).Within(.0001f));}
   Assert.IsFalse(editor.Liquids.Active);Assert.AreEqual(400000,editor.Read(lower).containers[0].amountMl);Assert.AreEqual(400000,editor.Read(upper).containers[0].amountMl);
   Assert.AreEqual(revision,editor.Revision);Assert.AreEqual(lowerRevision,editor.ObjectRevision(lower));Assert.AreEqual(upperRevision,editor.ObjectRevision(upper));
   Assert.AreEqual(snapshot,JsonUtility.ToJson(editor.Snapshot()));Assert.That(File.ReadAllBytes(path),Is.EqualTo(saved));
   Assert.That(editor.Find(lower),Is.SameAs(lowerItem));Assert.That(editor.Find(upper),Is.SameAs(upperItem));Assert.IsFalse(editor.NativeActivationPending);
   Assert.That(contact.BodyId==upper&&Vector3.Distance(contact.Point,new Vector3(4,upperLevel,0))<.0001f&&upperRipples==1&&lowerRipples==0,Is.True,
    "First downward water contact: chosen="+contact.BodyId+", expected upper="+upper+", lower ID="+lower+", point="+contact.Point+", upper ripples="+upperRipples+", lower ripples="+lowerRipples);
  }

  (string lower,string upper) ContactOrderBasins(bool earlierIdOnTop=false){
   var(first,prop)=MediumFixture(.1f);physics.PausePhysics();editor.Find(prop).gameObject.SetActive(false);
   Assert.IsTrue(editor.CopyObject(first,editor.ObjectRevision(first),"Ordered water contact",new Vector3(4,2,0),out var second,out var error),error);
   var ids=new[]{first,second}.OrderBy(id=>id,StringComparer.Ordinal).ToArray();string lower=ids[earlierIdOnTop?1:0],upper=ids[earlierIdOnTop?0:1];
   var low=editor.Read(lower);low.position=new Vector3(4,1,0);var high=editor.Read(upper);high.position=new Vector3(4,2,0);
   Assert.IsTrue(editor.ApplyAgentEdit(editor.Revision,new[]{low,high},Array.Empty<string>(),out error),error);return(lower,upper);
  }
  List<LiquidContact> ContactOrderEvents(string participant="input:left"){
   var events=new List<LiquidContact>();editor.MediumContact+=value=>{if(value.Participant==participant)events.Add(value);};return events;
  }
  void ContactOrderClear(params string[] ids){foreach(string id in ids){var water=editor.Find(id).GetComponent<ContainerFillView>();water.ClearRipples();}Physics.SyncTransforms();}
  void ContactOrderBaseline(Vector3 point){
   ContactPoint(point,false);var observation=editor.Liquids.ObserveContact("input:left");Assert.IsTrue((bool)observation["known"],observation.ToString());Assert.IsFalse((bool)observation["immersed"]);
  }
  void AssertContactOrderSelected(List<LiquidContact> events,string selected,string other,Vector3 point){
   Assert.AreEqual(1,events.Count);Assert.AreEqual("crossed",events[0].Phase);Assert.AreEqual(selected,events[0].BodyId);
   Assert.That(Vector3.Distance(events[0].Point,point),Is.LessThan(.0001f));
   var water=editor.Find(selected).GetComponent<ContainerFillView>();Assert.AreEqual(1,water.RippleCount);Assert.AreEqual(0,editor.Find(other).GetComponent<ContainerFillView>().RippleCount);
   var surface=water.GetComponentsInChildren<MeshRenderer>().Single(value=>value.name=="Measured liquid surface");var properties=new MaterialPropertyBlock();surface.GetPropertyBlock(properties);
   Assert.AreEqual(1,(int)properties.GetFloat("_LiquidRippleCount"));Vector3 impact=properties.GetVectorArray("_LiquidRipples")[0];Assert.That(Vector3.Distance(impact,point),Is.LessThan(.0001f));
   Assert.AreEqual(400000,editor.Read(selected).containers[0].amountMl);Assert.AreEqual(400000,editor.Read(other).containers[0].amountMl);Assert.IsFalse(editor.Liquids.Active);
  }
  [UnityTest]public IEnumerator LiquidContactOrderUpwardTrackedCrossingAlsoIgnoresOpposingIds(){
   var(lower,upper)=ContactOrderBasins(true);yield return null;physics.StartPhysics();ContactOrderClear(lower,upper);var events=ContactOrderEvents();
   Assert.That(StringComparer.Ordinal.Compare(upper,lower),Is.LessThan(0));ContactOrderBaseline(new Vector3(4,1,0));
   ContactPoint(new Vector3(4,3,0),false);AssertContactOrderSelected(events,lower,upper,new Vector3(4,1.6f,0));
  }
  IEnumerator ContactOrderTiedCrossings(bool smallerVolume){
   var(earlier,later)=ContactOrderBasins();var high=editor.Read(later);high.position=new Vector3(4,1,0);
   if(smallerVolume)high.containers[0].rectangle=new ContainerRectangle{width=.5f,depth=.5f};
   Assert.IsTrue(editor.ApplyAgentEdit(editor.Revision,new[]{high},Array.Empty<string>(),out var error),error);
   yield return null;physics.StartPhysics();ContactOrderClear(earlier,later);var events=ContactOrderEvents();ContactOrderBaseline(new Vector3(4,2,0));
   ContactPoint(new Vector3(4,1,0),false);AssertContactOrderSelected(events,smallerVolume?later:earlier,smallerVolume?earlier:later,new Vector3(4,1.6f,0));
  }
  [UnityTest]public IEnumerator LiquidContactOrderEqualTimePrefersSmallerVolumeBeforeOrdinalId(){yield return ContactOrderTiedCrossings(true);}
  [UnityTest]public IEnumerator LiquidContactOrderEqualTimeAndVolumeUsesStableOrdinalId(){yield return ContactOrderTiedCrossings(false);}
  [UnityTest]public IEnumerator LiquidContactOrderNearestKnownUnreadyGeometryDoesNotFallBackToFartherWater(){
   var(lower,upper)=ContactOrderBasins();yield return null;physics.StartPhysics();ContactOrderClear(lower,upper);
   editor.Find(upper).GetComponent<RigidRoomItem>().SetGeometryReady(false);var events=ContactOrderEvents();
   // Establish readiness before the baseline: a later mutation must not reset
   // continuity and make an otherwise wrong selection appear safely silent.
   ContactOrderBaseline(new Vector3(4,3,0));ContactPoint(new Vector3(4,1,0),false);
   Assert.That(events,Is.Empty);var observation=editor.Liquids.ObserveContact("input:left");Assert.IsFalse((bool)observation["known"],observation.ToString());
   Assert.IsTrue(editor.Find(lower).GetComponent<RigidRoomItem>().GeometryReady);Assert.AreEqual(0,editor.Find(lower).GetComponent<ContainerFillView>().RippleCount);Assert.AreEqual(0,editor.Find(upper).GetComponent<ContainerFillView>().RippleCount);
   Assert.AreEqual(400000,editor.Read(lower).containers[0].amountMl);Assert.AreEqual(400000,editor.Read(upper).containers[0].amountMl);Assert.IsFalse(editor.Liquids.Active);
  }
  [UnityTest]public IEnumerator LiquidContactOrderFartherKnownUnreadyGeometryDoesNotPoisonNearReadyCrossing(){
   var(lower,upper)=ContactOrderBasins();yield return null;physics.StartPhysics();ContactOrderClear(lower,upper);
   editor.Find(lower).GetComponent<RigidRoomItem>().SetGeometryReady(false);var events=ContactOrderEvents();
   ContactOrderBaseline(new Vector3(4,3,0));ContactPoint(new Vector3(4,1,0),false);
   AssertContactOrderSelected(events,upper,lower,new Vector3(4,2.6f,0));var observation=editor.Liquids.ObserveContact("input:left");Assert.IsTrue((bool)observation["known"],observation.ToString());
   Assert.IsFalse(editor.Find(lower).GetComponent<RigidRoomItem>().GeometryReady);
  }
  [UnityTest]public IEnumerator LiquidContactOrderMissingViewStillMakesTheWholeTrackedSegmentUnknown(){
   var(lower,upper)=ContactOrderBasins();yield return null;physics.StartPhysics();ContactOrderClear(lower,upper);
   var missing=editor.Find(lower).GetComponent<ContainerFillView>();missing.enabled=false;var events=ContactOrderEvents();int revision=editor.Revision;
   ContactOrderBaseline(new Vector3(4,3,0));ContactPoint(new Vector3(4,1,0),false);
   Assert.That(events,Is.Empty);Assert.IsFalse((bool)editor.Liquids.ObserveContact("input:left")["known"]);
   Assert.AreEqual(0,missing.RippleCount);Assert.AreEqual(0,editor.Find(upper).GetComponent<ContainerFillView>().RippleCount);
   missing.enabled=true;missing.Refresh();ContactOrderBaseline(new Vector3(4,1,0));Assert.That(events,Is.Empty,"Recovery must establish a baseline, not replay the missed downward crossing");
   ContactPoint(new Vector3(4,3,0),false);AssertContactOrderSelected(events,lower,upper,new Vector3(4,1.6f,0));Assert.AreEqual(revision,editor.Revision);
  }
  [UnityTest]public IEnumerator LiquidContactOrderAvatarChoosesEarlierNormalizedTimeAcrossActualJointSamples(){
   var(lower,upper)=ContactOrderBasins();var item=editor.Find("maestro");var avatar=item.gameObject.AddComponent<MaestroAvatar>();avatar.PoseRig.SetManual(true);item.transform.position=new Vector3(4,5,0);
   yield return null;physics.StartPhysics();ContactOrderClear(lower,upper);
   var left=avatar.PoseRig.Bone(PoseJoint.LeftFoot);var right=avatar.PoseRig.Bone(PoseJoint.RightFoot);
   left.position=new Vector3(3.85f,2.8f,0);right.position=new Vector3(4.15f,3,0);
   foreach(var joint in new[]{PoseJoint.LeftHand,PoseJoint.RightHand,PoseJoint.Hips,PoseJoint.Head})Assert.That(avatar.PoseRig.Bone(joint).position.y,Is.GreaterThan(3));
   var events=ContactOrderEvents("maestro");ContactTick();var baseline=editor.Liquids.ObserveContact("maestro");Assert.IsTrue((bool)baseline["known"],baseline.ToString());Assert.IsFalse((bool)baseline["immersed"]);
   // Left foot is sampled first and travels only .2m to water, but reaches
   // it at fraction .2857. Right travels .4m yet arrives first at fraction .2.
   left.position=new Vector3(3.85f,2.1f,0);right.position=new Vector3(4.15f,1,0);ContactTick();
   AssertContactOrderSelected(events,upper,lower,new Vector3(4.15f,2.6f,0));Assert.AreEqual("avatar",events[0].Kind);
   var after=editor.Liquids.ObserveContact("maestro");Assert.IsTrue((bool)after["known"],after.ToString());Assert.IsFalse((bool)after["immersed"]);
  }
 }
}
