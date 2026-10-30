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
  [UnityTest]public IEnumerator PourWaterPresentationReachesActualReceivingSurfaceAndRipplesWithoutChangingTransferAuthority(){
   var(from,to)=FixedFlowCups();Assert.IsFalse(physics.Running);Assert.IsFalse(editor.Liquids.enabled);
   var receiverData=editor.Read(to);receiverData.containers[0].amountMl=200;
   Assert.IsTrue(editor.ApplyAgentEdit(editor.Revision,new[]{receiverData},Array.Empty<string>(),out var error),error);
   var source=editor.Find(from);var receiver=editor.Find(to);var water=receiver.GetComponent<ContainerFillView>();Assert.IsTrue(water.NativeReady);
   var surface=receiver.GetComponentsInChildren<MeshFilter>().Single(value=>value.sharedMesh?.name=="Bounded liquid level");
   var renderer=surface.GetComponent<MeshRenderer>();Assert.IsTrue(renderer.enabled&&surface.gameObject.activeInHierarchy);
   var cavity=receiverData.containers[0];var opening=receiver.transform.TransformPoint(cavity.frame.position+cavity.frame.rotation*(Vector3.up*cavity.height));
   float beforeLevel=surface.sharedMesh.vertices.Select(surface.transform.TransformPoint).Average(point=>point.y);
   Assert.That(opening.y-beforeLevel,Is.GreaterThan(.02f),"The prefilled receiver must have a visible air gap beneath its opening");
   yield return null;water.ClearRipples();Physics.SyncTransforms();
   int revision=editor.Revision,events=0;double accepted=0,spilled=0;
   var path=Path.Combine(directory,RoomStorage.FileName);var saved=File.ReadAllBytes(path);
   editor.ContainerPoured+=(id,moved,lost,receivers,liquid)=>{if(id==from){events++;accepted=moved;spilled=lost;Assert.AreEqual(1,receivers);}};
   physics.StartPhysics();editor.Liquids.Tick(.05f);
   Assert.IsTrue(editor.Liquids.Active);double left=FlowAmount(from),right=FlowAmount(to),transferred=right-200;
   Assert.That(transferred,Is.GreaterThan(0));Assert.That(left+right,Is.EqualTo(600).Within(1e-8));Assert.That(FlowAmount(from,"spilledMl"),Is.Zero);
   Assert.AreEqual(400,editor.Read(from).containers[0].amountMl);Assert.AreEqual(200,editor.Read(to).containers[0].amountMl);
   Assert.AreEqual(revision,editor.Revision);Assert.That(File.ReadAllBytes(path),Is.EqualTo(saved),"Presentation must not publish per-tick live quantities");
   Assert.AreEqual(0,events);Assert.IsFalse(editor.WriteGate.CanFreeze(out _));
   var stream=source.GetComponentsInChildren<LineRenderer>().Single(value=>value.enabled);
   Assert.IsTrue(stream.useWorldSpace);Assert.That(stream.positionCount,Is.InRange(2,LiquidResources.StreamPoints));Assert.That(stream.GetComponents<Collider>(),Is.Empty);
   var endpoint=stream.GetPosition(stream.positionCount-1);Assert.IsTrue(RoomRecipe.Finite(endpoint));
   var actual=surface.sharedMesh.vertices.Select(surface.transform.TransformPoint).ToArray();float level=actual.Average(point=>point.y);
   Assert.That(actual.Max(point=>point.y)-actual.Min(point=>point.y),Is.LessThan(.00001f));Assert.That(level,Is.GreaterThan(beforeLevel));
   Assert.That(opening.y-level,Is.GreaterThan(.02f),"This tick must not fill the receiver to its opening");
   var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);int rippleCount=water.RippleCount;
   Assert.AreEqual(rippleCount,(int)properties.GetFloat("_LiquidRippleCount"));
   float nearestRipple=float.PositiveInfinity;
   if(rippleCount>0){
    var ripples=properties.GetVectorArray("_LiquidRipples");Assert.That(ripples.Length,Is.GreaterThanOrEqualTo(rippleCount));
    for(int i=0;i<rippleCount;i++){
     Vector3 point=ripples[i];Assert.That(point.y,Is.EqualTo(level).Within(.0001f));
     var local=Quaternion.Inverse(cavity.frame.rotation)*(receiver.transform.InverseTransformPoint(point)-cavity.frame.position);
     Assert.IsTrue(cavity.ContainsHorizontal(local));nearestRipple=Mathf.Min(nearestRipple,Vector3.Distance(point,endpoint));
    }
   }
   // Capture visual evidence before pausing resets transient output, but retain
   // the existing real transfer/save/undo checks even on the failing baseline.
   float surfaceGap=Mathf.Abs(endpoint.y-level);
   physics.PausePhysics();yield return LiquidSaved();
   Assert.IsFalse(editor.Liquids.Active);Assert.AreEqual(1,events);Assert.That(accepted,Is.EqualTo(transferred).Within(1e-8));Assert.AreEqual(0,spilled);
   Assert.AreEqual(left,editor.Read(from).containers[0].amountMl);Assert.AreEqual(right,editor.Read(to).containers[0].amountMl);
   var loaded=new RoomStorage(directory).Load(out error);Assert.That(loaded,Is.Not.Null,error);
   Assert.AreEqual(left,loaded.objects.Single(value=>value.id==from).containers[0].amountMl);Assert.AreEqual(right,loaded.objects.Single(value=>value.id==to).containers[0].amountMl);
   editor.Undo();Assert.AreEqual(400,editor.Read(from).containers[0].amountMl);Assert.AreEqual(200,editor.Read(to).containers[0].amountMl);
   editor.Redo();Assert.AreEqual(left,editor.Read(from).containers[0].amountMl);Assert.AreEqual(right,editor.Read(to).containers[0].amountMl);
   Assert.That(surfaceGap<=.0001f&&rippleCount>=1&&rippleCount<=ContainerFillView.MaximumRipples&&nearestRipple<=.002f,Is.True,
    "Actual receiving-water contact: stream endpoint="+endpoint+", water level="+level+", surface gap="+surfaceGap+", ripples="+rippleCount+", nearest ripple="+nearestRipple);
  }

  (string from,string to) PourPresentationCups(double amount=200,string liquid=null){
   var(from,to)=FixedFlowCups();var data=editor.Read(to);data.containers[0].amountMl=amount;if(liquid!=null)data.containers[0].liquid=liquid;
   Assert.IsTrue(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),error);return(from,to);
  }
  MeshFilter PourPresentationSurface(string id)=>editor.Find(id).GetComponentsInChildren<MeshFilter>(true).Single(value=>value.sharedMesh?.name=="Bounded liquid level");
  float PourPresentationLevel(string id){
   var surface=PourPresentationSurface(id);var points=surface.sharedMesh.vertices.Select(surface.transform.TransformPoint).ToArray();
   Assert.That(points.Length,Is.GreaterThanOrEqualTo(3));Assert.That(points.Max(p=>p.y)-points.Min(p=>p.y),Is.LessThan(.00001f));return points.Average(p=>p.y);
  }
  LineRenderer PourPresentationStream(string id){
   var line=editor.Find(id).GetComponentsInChildren<LineRenderer>().Single(value=>value.enabled);
   Assert.That(line.positionCount,Is.InRange(2,LiquidResources.StreamPoints));Assert.IsTrue(line.useWorldSpace);Assert.That(line.GetComponents<Collider>(),Is.Empty);return line;
  }
  void AssertPourPresentationImpact(string from,string to){
   var line=PourPresentationStream(from);var endpoint=line.GetPosition(line.positionCount-1);float level=PourPresentationLevel(to);
   Assert.That(endpoint.y,Is.EqualTo(level).Within(.0001f),"The actual rendered stream must end on the final measured water plane");
   var water=editor.Find(to).GetComponent<ContainerFillView>();Assert.That(water.RippleCount,Is.InRange(1,ContainerFillView.MaximumRipples));
   var properties=new MaterialPropertyBlock();PourPresentationSurface(to).GetComponent<Renderer>().GetPropertyBlock(properties);
   Assert.AreEqual(water.RippleCount,(int)properties.GetFloat("_LiquidRippleCount"));var impacts=properties.GetVectorArray("_LiquidRipples");
   float nearest=float.PositiveInfinity;
   for(int i=0;i<water.RippleCount;i++){Vector3 point=impacts[i];Assert.That(point.y,Is.EqualTo(level).Within(.0001f));nearest=Mathf.Min(nearest,Vector3.Distance(point,endpoint));}
   Assert.That(nearest,Is.LessThanOrEqualTo(.002f),"The ripple and actual line endpoint must identify the same water contact");
  }
  IEnumerator PourPresentationInteriorBlocker(bool owned){
   var(from,to)=PourPresentationCups();var receiver=editor.Find(to);var data=editor.Read(to);var cavity=data.containers[0];
   var opening=receiver.transform.TransformPoint(cavity.frame.position+cavity.frame.rotation*(Vector3.up*cavity.height));
   float lidY=(opening.y+PourPresentationLevel(to))*.5f;Collider lid;
   if(owned){
    var local=receiver.transform.InverseTransformPoint(new Vector3(receiver.transform.position.x,lidY,receiver.transform.position.z));
    data.collision.shapes=data.collision.shapes.Concat(new[]{new CollisionShape{id="pour_lid",position=local,size=new Vector3(.14f,.006f,.14f)}}).ToArray();
    Assert.IsTrue(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out var error),error);
    receiver=editor.Find(to);Physics.SyncTransforms();lid=receiver.SolidColliders.Single(value=>Mathf.Abs(value.bounds.center.y-lidY)<.0001f);
   }else{
    var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.name="Foreign receiver interior lid";obstacle.transform.SetParent(root.transform,false);
    obstacle.transform.position=new Vector3(receiver.transform.position.x,lidY,receiver.transform.position.z);obstacle.transform.localScale=new Vector3(.14f,.006f,.14f);obstacle.layer=RoomPhysicsLayers.Environment;lid=obstacle.GetComponent<Collider>();
   }
   yield return null;var water=receiver.GetComponent<ContainerFillView>();water.ClearRipples();Physics.SyncTransforms();
   Assert.That(lid.bounds.max.y,Is.LessThan(opening.y-.01f));Assert.That(lid.bounds.min.y,Is.GreaterThan(PourPresentationLevel(to)+.01f));
   physics.StartPhysics();editor.Liquids.Tick(.05f);
   double left=FlowAmount(from),lost=FlowAmount(from,"spilledMl");Assert.That(lost,Is.GreaterThan(0));Assert.AreEqual(200,FlowAmount(to));Assert.That(left+lost,Is.EqualTo(400).Within(1e-8));
   var line=PourPresentationStream(from);Assert.That(line.GetPosition(line.positionCount-1).y,Is.EqualTo(lid.bounds.max.y).Within(.001f));
   Assert.That(water.RippleCount,Is.Zero,"An arbitrary interior solid must stop deposition even when owned by the selected receiver");
   physics.PausePhysics();yield return LiquidSaved();Assert.AreEqual(200,editor.Read(to).containers[0].amountMl);Assert.AreEqual(left,editor.Read(from).containers[0].amountMl);
  }
  [UnityTest]public IEnumerator PourWaterPresentationForeignInteriorSolidPreventsReceiptAndWaterRipple(){yield return PourPresentationInteriorBlocker(false);}
  [UnityTest]public IEnumerator PourWaterPresentationReceiverOwnedInteriorSolidIsNotACollectionExemption(){yield return PourPresentationInteriorBlocker(true);}
  [UnityTest]public IEnumerator PourWaterPresentationEmptyCupReachesItsNewMeasuredWaterSurface(){
   var(from,to)=PourPresentationCups(0);yield return null;Assert.AreEqual(0,editor.Find(to).GetComponent<ContainerFillView>().RippleCount);
   physics.StartPhysics();editor.Liquids.Tick(.05f);Assert.That(FlowAmount(to),Is.GreaterThan(0));Assert.That(FlowAmount(from)+FlowAmount(to),Is.EqualTo(400).Within(1e-8));
   AssertPourPresentationImpact(from,to);physics.PausePhysics();yield return LiquidSaved();Assert.That(editor.Read(to).containers[0].amountMl,Is.GreaterThan(0));
  }
  IEnumerator PourPresentationRejectsContentsButShowsImpact(bool full){
   double before=full?500:200;var(from,to)=PourPresentationCups(before,full?null:"Juice");yield return null;
   var water=editor.Find(to).GetComponent<ContainerFillView>();water.ClearRipples();physics.StartPhysics();editor.Liquids.Tick(.05f);
   Assert.AreEqual(before,FlowAmount(to));Assert.That(FlowAmount(from,"spilledMl"),Is.GreaterThan(0));Assert.That(FlowAmount(from)+FlowAmount(from,"spilledMl"),Is.EqualTo(400).Within(1e-8));
   Assert.AreEqual(0,FlowAmount(from,"transferredMl"));AssertPourPresentationImpact(from,to);
   physics.PausePhysics();yield return LiquidSaved();Assert.AreEqual(before,editor.Read(to).containers[0].amountMl);if(!full)Assert.AreEqual("Juice",editor.Read(to).containers[0].liquid);
  }
  [UnityTest]public IEnumerator PourWaterPresentationFullReceiverStillShowsTheActualWaterImpact(){yield return PourPresentationRejectsContentsButShowsImpact(true);}
  [UnityTest]public IEnumerator PourWaterPresentationIncompatibleContentsSpillButDoNotHideTheWaterImpact(){yield return PourPresentationRejectsContentsButShowsImpact(false);}
  [UnityTest]public IEnumerator PourWaterPresentationMultipleSourcesUseTheFinalReceivingPlane(){
   var(first,to)=FixedFlowCups();var source=editor.Read(first);source.position.z=-.3f;
   Assert.IsTrue(editor.CopyObject(first,editor.ObjectRevision(first),"Second independent pour",new Vector3(source.position.x,source.position.y,.3f),out var second,out var error),error);
   var peer=editor.Read(second);peer.rotation=source.rotation;peer.physics=ItemPhysics.Fixed;
   var recipient=editor.Read(to);recipient.position=new Vector3(3,0,0);recipient.physics=ItemPhysics.Fixed;
   recipient.collision=new CollisionRecipe{shapes=new[]{new CollisionShape{id="floor",position=new Vector3(0,-.01f,0),size=new Vector3(2,.02f,2)}}};
   recipient.containers=new[]{new RoomContainer{version=2,rectangle=new(){width=2,depth=2},height=1,capacityMl=2000,amountMl=500}};
   Assert.IsTrue(editor.ApplyAgentEdit(editor.Revision,new[]{source,peer,recipient},Array.Empty<string>(),out error),error);
   yield return null;var water=editor.Find(to).GetComponent<ContainerFillView>();water.ClearRipples();Physics.SyncTransforms();
   physics.StartPhysics();editor.Liquids.Tick(.05f);
   Assert.That(FlowAmount(first),Is.LessThan(400));Assert.That(FlowAmount(second),Is.LessThan(400));Assert.That(FlowAmount(to),Is.GreaterThan(500));
   Assert.That(FlowAmount(first)+FlowAmount(second)+FlowAmount(to),Is.EqualTo(1300).Within(1e-8));Assert.AreEqual(0,FlowAmount(first,"spilledMl")+FlowAmount(second,"spilledMl"));
   Assert.That(water.RippleCount,Is.GreaterThanOrEqualTo(2));AssertPourPresentationImpact(first,to);AssertPourPresentationImpact(second,to);
   physics.PausePhysics();yield return LiquidSaved();Assert.That(editor.Read(first).containers[0].amountMl+editor.Read(second).containers[0].amountMl+editor.Read(to).containers[0].amountMl,Is.EqualTo(1300).Within(1e-8));
  }
  [UnityTest]public IEnumerator PourWaterPresentationMissingReceiverGeometryWaitsWithoutAFalseStreamOrRipple(){
   var(from,to)=PourPresentationCups();yield return null;var water=editor.Find(to).GetComponent<ContainerFillView>();water.enabled=false;
   physics.StartPhysics();editor.Liquids.Tick(.05f);
   Assert.AreEqual(400,FlowAmount(from));Assert.AreEqual(200,FlowAmount(to));Assert.AreEqual(0,FlowAmount(from,"spilledMl"));Assert.IsFalse(editor.Liquids.Active);Assert.AreEqual(0,water.RippleCount);
   Assert.IsFalse(editor.Find(from).GetComponentsInChildren<LineRenderer>(true).Any(value=>value.enabled));
   water.enabled=true;water.Refresh();editor.Liquids.Tick(.05f);
   Assert.That(FlowAmount(to),Is.GreaterThan(200));Assert.That(FlowAmount(from)+FlowAmount(to),Is.EqualTo(600).Within(1e-8));AssertPourPresentationImpact(from,to);
   physics.PausePhysics();yield return LiquidSaved();
  }
  [UnityTest]public IEnumerator PourWaterPresentationSaturatedInteriorPrefixCannotInventReceiptOrAnImpact(){
   var(from,to)=PourPresentationCups();var item=editor.Find(to);var cavity=editor.Read(to).containers[0];
   float opening=item.transform.TransformPoint(cavity.frame.position+cavity.frame.rotation*(Vector3.up*cavity.height)).y;
   var at=new Vector3(item.transform.position.x,(opening+PourPresentationLevel(to))*.5f,item.transform.position.z);
   var colliders=new Collider[FlowWorkResources.QueryHits];
   for(int i=0;i<colliders.Length;i++){
    var obstacle=new GameObject("Saturated receiving interior "+i);obstacle.transform.SetParent(root.transform,false);obstacle.transform.position=at;obstacle.layer=RoomPhysicsLayers.Environment;
    var shape=obstacle.AddComponent<BoxCollider>();shape.size=new Vector3(.14f,.006f,.14f);colliders[i]=shape;
   }
   yield return null;Physics.SyncTransforms();var water=item.GetComponent<ContainerFillView>();water.ClearRipples();physics.StartPhysics();editor.Liquids.Tick(.05f);
   Assert.AreEqual(400,FlowAmount(from));Assert.AreEqual(200,FlowAmount(to));Assert.AreEqual(0,FlowAmount(from,"spilledMl"));Assert.IsFalse(editor.Liquids.Active);
   Assert.AreEqual(0,water.RippleCount);Assert.IsFalse(editor.Find(from).GetComponentsInChildren<LineRenderer>(true).Any(value=>value.enabled));
   for(int i=1;i<colliders.Length;i++)colliders[i].enabled=false;Physics.SyncTransforms();editor.Liquids.Tick(.05f);
   Assert.AreEqual(200,FlowAmount(to));Assert.That(FlowAmount(from,"spilledMl"),Is.GreaterThan(0));Assert.That(FlowAmount(from)+FlowAmount(from,"spilledMl"),Is.EqualTo(400).Within(1e-8));
   var line=PourPresentationStream(from);Assert.That(line.GetPosition(line.positionCount-1).y,Is.EqualTo(colliders[0].bounds.max.y).Within(.001f));Assert.AreEqual(0,water.RippleCount);
   physics.PausePhysics();yield return LiquidSaved();
  }
  [UnityTest]public IEnumerator PourWaterPresentationDepositsOnTheActualTerrainWaterAboveItsBed(){
   physics.PausePhysics();editor.Liquids.enabled=false;var(_,to)=TerrainReservoir();
   string from=CreateTemplateFixture(TemplateCall("cup",TerrainPoint(to,-.5f,.8f)));var source=editor.Read(from);source.physics=ItemPhysics.Fixed;source.rotation=Quaternion.Euler(0,0,80);source.containers[0].amountMl=400;
   Assert.IsTrue(editor.ApplyAgentEdit(editor.Revision,new[]{source},Array.Empty<string>(),out var error),error);
   yield return null;var water=editor.Find(to).GetComponent<ContainerFillView>();water.ClearRipples();Physics.SyncTransforms();
   double before=FlowAmount(to);Assert.That(before,Is.GreaterThan(0));physics.StartPhysics();editor.Liquids.Tick(.05f);
   Assert.That(FlowAmount(to),Is.GreaterThan(before));Assert.That(FlowAmount(from)+FlowAmount(to)+FlowAmount(from,"spilledMl"),Is.EqualTo(before+400).Within(1e-6));
   Assert.AreEqual(0,FlowAmount(from,"spilledMl"));AssertPourPresentationImpact(from,to);
   var surface=PourPresentationSurface(to);var endpoint=PourPresentationStream(from).GetPosition(PourPresentationStream(from).positionCount-1);
   Assert.IsTrue(RainRenderedSurfaceContains(endpoint,surface.sharedMesh.vertices.Select(surface.transform.TransformPoint).ToArray(),surface.sharedMesh.triangles,-.0001f));
   physics.PausePhysics();yield return LiquidSaved();Assert.That(editor.Read(to).containers[0].amountMl,Is.GreaterThan(before));
  }

  [UnityTest]public IEnumerator PourWaterPresentationLaterScoopUsesTheLowerFinalPlaneAndConservesAllThreeStores(){
   var(from,to)=FixedFlowCups();var recipient=editor.Read(to);recipient.position=new Vector3(3,0,0);recipient.physics=ItemPhysics.Fixed;
   recipient.collision=new CollisionRecipe{shapes=new[]{new CollisionShape{id="floor",position=new Vector3(0,-.01f,0),size=new Vector3(2,.02f,2)}}};
   recipient.containers=new[]{new RoomContainer{version=2,rectangle=new(){width=2,depth=2},height=1,capacityMl=2000,amountMl=500}};
   Assert.IsTrue(editor.ApplyAgentEdit(editor.Revision,new[]{recipient},Array.Empty<string>(),out var error),error);
   string dipper=CreateTemplateFixture(TemplateCall("cup",new Vector3(3.5f,.12f,.4f)));var cup=editor.Read(dipper);cup.physics=ItemPhysics.Fixed;
   Assert.IsTrue(editor.ApplyAgentEdit(editor.Revision,new[]{cup},Array.Empty<string>(),out error),error);
   Assert.IsTrue(ContainerScoopingGeometry.TryContact(editor.Read(to).containers[0],editor.Find(to).transform,cup.containers[0],editor.Find(dipper).transform,Vector3.up,out _),"The separate empty cup must be fully inside the receiver with its mouth submerged");
   yield return null;var water=editor.Find(to).GetComponent<ContainerFillView>();water.ClearRipples();Physics.SyncTransforms();float before=PourPresentationLevel(to);
   physics.StartPhysics();editor.Liquids.Tick(.05f);
   double left=FlowAmount(from),retained=FlowAmount(to),scooped=FlowAmount(dipper);
   Assert.That(left,Is.LessThan(400),"The pouring phase must actually deliver water");Assert.That(scooped,Is.GreaterThan(0),"The later scooping phase must actually remove water");
   Assert.That((double)editor.Liquids.ObserveScooping(dipper)["scoopedMl"],Is.EqualTo(scooped).Within(1e-8));
   Assert.That(retained,Is.LessThan(500),"The later scoop must outweigh this tick's pour, so the final surface is lower even than its initial level");
   Assert.That(left+retained+scooped,Is.EqualTo(900).Within(1e-8));Assert.AreEqual(0,FlowAmount(from,"spilledMl"));
   Assert.That(PourPresentationLevel(to),Is.LessThan(before-.0001f));AssertPourPresentationImpact(from,to);
   Assert.That(water.RippleCount,Is.InRange(1,ContainerFillView.MaximumRipples));
   physics.PausePhysics();yield return LiquidSaved();
   Assert.That(editor.Read(from).containers[0].amountMl+editor.Read(to).containers[0].amountMl+editor.Read(dipper).containers[0].amountMl,Is.EqualTo(900).Within(1e-8));
  }

  [UnityTest]public IEnumerator PourWaterPresentationEmptyTerrainRequiresItsNativeBedBeforeReceiptAndImpact(){
   physics.PausePhysics();editor.Liquids.enabled=false;var(_,to)=TerrainReservoir();
   var empty=editor.Read(to);empty.containers[0].amountMl=0;
   Assert.IsTrue(editor.ApplyAgentEdit(editor.Revision,new[]{empty},Array.Empty<string>(),out var error),error);
   // A small central low spot gives the first few millilitres a measurable
   // depth, rather than relying on micrometres spread across a flat large bed.
   var bed=editor.Read(to).heightFields[0];for(int i=0;i<bed.heights.Length;i++)bed.heights[i]=.4f;
   bed.heights[(bed.cells/2)*(bed.cells+1)+bed.cells/2]=.1f;
   Assert.IsTrue(editor.EditHeightField(to,editor.ObjectRevision(to),bed,out error),error);
   var target=TerrainPoint(to,0,.1f);string from=CreateTemplateFixture(TemplateCall("cup",TerrainPoint(to,0,.8f)));
   var source=editor.Read(from);source.physics=ItemPhysics.Fixed;source.rotation=Quaternion.Euler(0,0,80);source.containers[0].amountMl=400;
   Assert.IsTrue(editor.ApplyAgentEdit(editor.Revision,new[]{source},Array.Empty<string>(),out error),error);
   var origin=ContainerFlowGeometry.Lip(source.containers[0],editor.Find(from).transform,Vector3.up,out var outward);var velocity=outward*.15f;
   float gravity=-Physics.gravity.y;float time=(velocity.y+Mathf.Sqrt(velocity.y*velocity.y+2*gravity*(origin.y-target.y)))/gravity;
   var predicted=origin+velocity*time+Physics.gravity*(.5f*time*time);
   source=editor.Read(from);source.position+=new Vector3(target.x-predicted.x,0,target.z-predicted.z);
   Assert.IsTrue(editor.ApplyAgentEdit(editor.Revision,new[]{source},Array.Empty<string>(),out error),error);
   yield return null;Physics.SyncTransforms();var terrain=editor.Find(to).GetComponent<HeightFieldView>();var nativeBed=terrain.Collision;
   Assert.IsTrue(nativeBed&&nativeBed.enabled);Assert.IsTrue(nativeBed.Raycast(new Ray(target+Vector3.up,Vector3.down),out var bedHit,2));
   Assert.That(Vector3.Distance(bedHit.point,target),Is.LessThan(.0001f),"The receiving minimum must be proved by the actual accepted native terrain collider");
   var water=editor.Find(to).GetComponent<ContainerFillView>();water.ClearRipples();Assert.AreEqual(0,FlowAmount(to));
   nativeBed.enabled=false;Physics.SyncTransforms();physics.StartPhysics();editor.Liquids.Tick(.05f);
   Assert.AreEqual(400,FlowAmount(from));Assert.AreEqual(0,FlowAmount(to));Assert.AreEqual(0,FlowAmount(from,"spilledMl"));Assert.IsFalse(editor.Liquids.Active);
   Assert.AreEqual(0,water.RippleCount);Assert.IsFalse(editor.Find(from).GetComponentsInChildren<LineRenderer>(true).Any(value=>value.enabled));
   nativeBed.enabled=true;Physics.SyncTransforms();editor.Liquids.Tick(.05f);
   Assert.That(FlowAmount(to),Is.GreaterThan(0));Assert.That(FlowAmount(from)+FlowAmount(to),Is.EqualTo(400).Within(1e-8));Assert.AreEqual(0,FlowAmount(from,"spilledMl"));
   Assert.That(PourPresentationLevel(to),Is.GreaterThan(bedHit.point.y+.0001f));AssertPourPresentationImpact(from,to);
   var line=PourPresentationStream(from);var endpoint=line.GetPosition(line.positionCount-1);var surface=PourPresentationSurface(to);
   Assert.IsTrue(RainRenderedSurfaceContains(endpoint,surface.sharedMesh.vertices.Select(surface.transform.TransformPoint).ToArray(),surface.sharedMesh.triangles,-.0001f),"The actual final endpoint must lie on the newly wet part of the terrain mesh");
   physics.PausePhysics();yield return LiquidSaved();Assert.That(editor.Read(to).containers[0].amountMl,Is.GreaterThan(0));
   Assert.That(editor.Read(from).containers[0].amountMl+editor.Read(to).containers[0].amountMl,Is.EqualTo(400).Within(1e-8));
  }
 }
}
