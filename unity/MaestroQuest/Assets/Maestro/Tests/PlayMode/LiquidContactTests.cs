// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;using System.Collections;using System.Collections.Generic;using System.IO;using System.Linq;
using Maestro.Quest.Creation;using Maestro.Quest.Interaction;using Maestro.Quest.Programs;using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;using NUnit.Framework;using UnityEngine;using UnityEngine.TestTools;
using Maestro.Quest.Avatar;using Maestro.Quest.Book;using UnityEngine.InputSystem;using UnityEngine.InputSystem.Controls;using UnityEngine.InputSystem.LowLevel;using UnityEngine.XR.OpenXR.Features.Interactions;
namespace Maestro.Quest.Tests {public sealed partial class RoomRulesTests {
 void ContactTick()=>editor.Liquids.TickContacts(.05f);
 void ContactPoint(Vector3? point,bool hand=true){editor.Liquids.SetContactInput(0,point,hand);ContactTick();}
 [UnityTest]public IEnumerator LiquidContactPropEdgesAreSingleAndNeverApplyForcesOrConsumeWater(){
  var(pool,prop)=MediumFixture(.1f);var item=editor.Find(prop);var body=item.GetComponent<Rigidbody>();item.GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);var events=new List<LiquidContact>();editor.MediumContact+=e=>{if(e.Participant==prop)events.Add(e);};
  item.transform.position=new Vector3(4,2,0);ContactTick();Assert.That((bool)editor.Liquids.ObserveContact(prop)["known"],Is.True);Assert.That(events,Is.Empty);
  item.transform.position=new Vector3(4,1.4f,0);ContactTick();ContactTick();Assert.That(events.Select(e=>e.Phase),Is.EqualTo(new[]{"entered"}));Assert.That(events[0].BodyId,Is.EqualTo(pool));Assert.That(events[0].Point.y,Is.EqualTo(1.6f).Within(.001));Assert.That((bool)editor.Liquids.ObserveContact(prop)["immersed"],Is.True);
  Assert.That(body.isKinematic,Is.True);Assert.That(body.linearVelocity,Is.EqualTo(Vector3.zero));Assert.That(editor.Read(pool).containers[0].amountMl,Is.EqualTo(400000));
  item.transform.position=new Vector3(4,2,0);ContactTick();ContactTick();Assert.That(events.Select(e=>e.Phase),Is.EqualTo(new[]{"entered","exited"}));
  item.transform.position=new Vector3(4,1.4f,0);item.GetComponent<RigidRoomItem>().Teleported();Assert.That((bool)editor.Liquids.ObserveContact(prop)["known"],Is.False);ContactTick();Assert.That(events.Count,Is.EqualTo(2),"Teleport establishes a new baseline");yield return null;
 }
 [UnityTest]public IEnumerator LiquidContactTrackingLossPauseAndWorkspaceHoldsDoNotManufactureEdges(){
  var(pool,_)=MediumFixture(.1f);var events=new List<LiquidContact>();editor.MediumContact+=e=>{if(e.Participant=="input:left")events.Add(e);};var dry=new Vector3(4,2,0);var wet=new Vector3(4,1.4f,0);
  ContactPoint(dry);ContactPoint(wet);Assert.That(events.Count,Is.EqualTo(1));var view=editor.Find(pool).GetComponent<ContainerFillView>();Assert.That(view.RippleCount,Is.GreaterThan(0));
  editor.Liquids.SetContactInput(0,null,true);Assert.That((bool)editor.Liquids.ObserveContact("input:left")["known"],Is.False);ContactPoint(wet);Assert.That(events.Count,Is.EqualTo(1));
  editor.Liquids.SendMessage("OnApplicationFocus",false);Assert.That(view.RippleCount,Is.Zero);ContactPoint(dry);Assert.That((bool)editor.Liquids.ObserveContact("input:left")["known"],Is.False);editor.Liquids.SendMessage("OnApplicationFocus",true);ContactPoint(wet);Assert.That(events.Count,Is.EqualTo(1));
  using(var hold=editor.WriteGate.TryFreeze(out var error)){Assert.That(hold,Is.Not.Null,error);ContactPoint(dry);Assert.That((bool)editor.Liquids.ObserveContact("input:left")["known"],Is.False);}ContactPoint(wet);Assert.That(events.Count,Is.EqualTo(1));
  physics.PausePhysics();Assert.That((bool)editor.Liquids.ObserveContact("input:left")["known"],Is.False);physics.StartPhysics();ContactPoint(dry);Assert.That(events.Count,Is.EqualTo(1));
  yield return new WaitForSecondsRealtime(.21f);Assert.That((bool)editor.Liquids.ObserveContact("input:left")["known"],Is.False);
 }
 [UnityTest]public IEnumerator LiquidContactFastPointCrossingAndUnavailableGeometryAreExplicit(){
  var(pool,_)=MediumFixture(.1f);var events=new List<LiquidContact>();editor.MediumContact+=e=>{if(e.Participant=="input:left")events.Add(e);};
  ContactPoint(new Vector3(4,2,0));ContactPoint(new Vector3(4,1,0));Assert.That(events.Select(e=>e.Phase),Is.EqualTo(new[]{"crossed"}));ContactTick();Assert.That(events.Count,Is.EqualTo(1));
  var rigid=editor.Find(pool).GetComponent<RigidRoomItem>();rigid.SetGeometryReady(false);ContactPoint(new Vector3(4,1.4f,0));Assert.That((bool)editor.Liquids.ObserveContact("input:left")["known"],Is.False);Assert.That((bool)editor.Liquids.ObserveMedium(new Vector3(4,1.4f,0),"")["active"],Is.False);Assert.That(events.Count,Is.EqualTo(1));
  rigid.SetGeometryReady(true);ContactPoint(new Vector3(4,1.4f,0));Assert.That(events.Count,Is.EqualTo(1));Assert.That((bool)editor.Liquids.ObserveContact("input:left")["immersed"],Is.True);
  rigid.SetGeometryReady(false);Assert.That((bool)editor.Liquids.ObserveContact("input:left")["known"],Is.False);yield return null;
 }
 [UnityTest]public IEnumerator LiquidContactPublishesThroughTheSameTypedProgramScheduler(){
  var(pool,_)=MediumFixture(.1f);var sequence=workshop.Selected;sequence.program=File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-liquid-contact.json"));sequence.repeat=false;
  Assert.That(workshop.Execute(new RuleRequest{action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit{kind="save",sequence=sequence}}},out var error,out _),Is.True,error);Assert.That(runtime.Trigger(sequence.id),Is.True);
  for(int i=0;i<40&&!runtime.Scheduler.IsListening("object.medium.contact",pool);i++)yield return null;Assert.That(runtime.Scheduler.IsListening("object.medium.contact",pool),Is.True);
  ContactPoint(new Vector3(4,2,0));ContactPoint(new Vector3(4,1.4f,0));
  for(int i=0;i<40&&!runtime.Scheduler.ObserveRuns().Any(r=>r.nodeId=="hold");i++)yield return null;
  var run=runtime.Scheduler.ObserveRuns().Single();Assert.That(run.nodeId,Is.EqualTo("hold"));Assert.That(run.state.Single(v=>v.name=="participant").value,Is.EqualTo("input:left"));
  Assert.That(BehaviourCatalog.TryRead("input.medium.contactState",1,new JObject{["side"]="left"},new BehaviourCatalog.FactContext(editor:editor),out var fact),Is.True);Assert.That(((JObject)fact.Value)["bodyId"].Value<string>(),Is.EqualTo(pool));
 }
 [UnityTest]public IEnumerator LiquidContactRipplesRenderInsideSurfaceExpireAndKeepMaterialsAndQuantity(){
  var(pool,prop)=MediumFixture(.1f);editor.Find(prop).gameObject.SetActive(false);var view=editor.Find(pool).GetComponent<ContainerFillView>();var surface=view.GetComponentsInChildren<MeshRenderer>().Single(x=>x.name=="Measured liquid surface");
  var camera=new GameObject("Liquid contact acceptance",typeof(Camera)).GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=.65f;camera.nearClipPlane=.01f;camera.farClipPlane=4;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.09f,.13f,.17f);camera.transform.position=new Vector3(4,3,0);camera.transform.rotation=Quaternion.Euler(90,0,0);
  var render=new RenderTexture(640,640,24);var pixels=new Texture2D(640,640,TextureFormat.RGB24,false);var previous=RenderTexture.active;
  Color32[] Capture(){camera.targetTexture=render;camera.Render();RenderTexture.active=render;pixels.ReadPixels(new Rect(0,0,640,640),0,0);pixels.Apply();return pixels.GetPixels32();}
  try{var before=Capture();var color=surface.sharedMaterial.GetColor("_Color");for(int i=0;i<20;i++)view.AddRipple(new Vector3(4,1.6f,0),1);Assert.That(view.RippleCount,Is.EqualTo(8));yield return new WaitForSecondsRealtime(.35f);view.UpdateRipples();var after=Capture();int changed=0,outside=0;for(int y=0;y<640;y++)for(int x=0;x<640;x++){int i=y*640+x;if(Math.Abs(after[i].r-before[i].r)>10){changed++;if(x<60||x>580||y<60||y>580)outside++;}}
   Assert.That(changed,Is.GreaterThan(100));Assert.That(outside,Is.Zero,"The clipped water surface contains the effect");Assert.That(surface.sharedMaterial.GetColor("_Color"),Is.EqualTo(color));Assert.That(editor.Read(pool).containers[0].amountMl,Is.EqualTo(400000));
   string output=Environment.GetEnvironmentVariable("MAESTRO_LIQUID_CONTACT_PREVIEW");if(!string.IsNullOrEmpty(output))File.WriteAllBytes(output,pixels.EncodeToPNG());
   yield return new WaitForSecondsRealtime(1.05f);view.UpdateRipples();Assert.That(view.RippleCount,Is.Zero);
  }finally{RenderTexture.active=previous;camera.targetTexture=null;render.Release();UnityEngine.Object.Destroy(render);UnityEngine.Object.Destroy(pixels);UnityEngine.Object.Destroy(camera.gameObject);}
 }

 [UnityTest]public IEnumerator LiquidContactUsesActualBoundControllerPositionAndLosesTrackingImmediately(){
  var(pool,_)=MediumFixture(.1f);const string layout="MaestroWaterContactTouch";InputSystem.RegisterLayout<OculusTouchControllerProfile.OculusTouchController>(layout);var device=(OculusTouchControllerProfile.OculusTouchController)InputSystem.AddDevice(layout);InputSystem.SetDeviceUsage(device,CommonUsages.LeftHand);
  var input=root.AddComponent<BookControllerInput>();input.Router=root.AddComponent<BookPointerRouter>();input.TrackingSpace=root.transform;input.Editor=editor;var events=new List<LiquidContact>();editor.MediumContact+=e=>{if(e.Participant=="input:left")events.Add(e);};
  void Send(float y,bool tracked=true){using(StateEvent.From(device,out var state)){device.isTracked.WriteValueIntoEvent(tracked?1f:0f,state);device.GetChildControl<Vector3Control>("pointerPosition").WriteValueIntoEvent(new Vector3(4,y,0),state);device.GetChildControl<QuaternionControl>("pointerRotation").WriteValueIntoEvent(Quaternion.identity,state);InputSystem.QueueEvent(state);}InputSystem.Update();}
  try{Send(2);yield return null;yield return null;ContactTick();Assert.That((bool)editor.Liquids.ObserveContact("input:left")["known"],Is.True);Send(1.4f);yield return null;yield return null;ContactTick();Assert.That(events.Count,Is.EqualTo(1));Assert.That(events[0].Kind,Is.EqualTo("controller"));Assert.That(events[0].BodyId,Is.EqualTo(pool));Send(1.4f,false);yield return null;yield return null;Assert.That((bool)editor.Liquids.ObserveContact("input:left")["known"],Is.False);Assert.That(events.Count,Is.EqualTo(1));}
  finally{UnityEngine.Object.DestroyImmediate(input);InputSystem.RemoveDevice(device);InputSystem.RemoveLayout(layout);}
 }
 [UnityTest]public IEnumerator LiquidContactUsesMaestroVisibleJointMotion(){
  var(pool,_)=MediumFixture(.1f);var item=editor.Find("maestro");var avatar=item.gameObject.AddComponent<MaestroAvatar>();avatar.PoseRig.SetManual(true);item.transform.position=new Vector3(4,2,0);ContactTick();var events=new List<LiquidContact>();editor.MediumContact+=e=>{if(e.Participant=="maestro")events.Add(e);};
  item.transform.position+=new Vector3(4,1.4f,0)-avatar.PoseRig.Bone(PoseJoint.LeftFoot).position;ContactTick();Assert.That((bool)editor.Liquids.ObserveContact("maestro")["immersed"],Is.True);Assert.That(events.Count,Is.EqualTo(1));Assert.That(events[0].BodyId,Is.EqualTo(pool));Assert.That(events[0].Kind,Is.EqualTo("avatar"));yield return null;
 }
 [UnityTest]public IEnumerator LiquidContactBelowTheScanRequiresEachParticipantsSelectedEnvironment(){
  var(pool,prop)=MediumFixture(.1f);physics.PausePhysics();physics.Contains=p=>p.y>=0;
  var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.transform.SetParent(root.transform,false);ground.transform.position=new Vector3(4,-2.1f,0);ground.transform.localScale=new Vector3(4,.2f,4);ground.layer=RoomPhysicsLayers.Environment;ground.AddComponent<RoomWalkableSurface>().Publish(ground.GetComponent<Collider>());
  string profile=NewEnvironment();Assert.That(AssignEnvironment(pool,profile,out var error),Is.True,error);Assert.That(AssignEnvironment(prop,profile,out error),Is.True,error);
  foreach(string id in new[]{pool,prop}){editor.Find(id).transform.position+=Vector3.down*2.5f;editor.Find(id).GetComponent<RigidRoomItem>().Teleported();}editor.Find(prop).GetComponent<RigidRoomItem>().SetAnimationOwner(this,true);Physics.SyncTransforms();physics.SetSurfaces(false,"No scan");physics.StartPhysics();
  ContactTick();Assert.That((bool)editor.Liquids.ObserveContact(prop)["immersed"],Is.True);ContactPoint(new Vector3(4,-1.1f,0));Assert.That((bool)editor.Liquids.ObserveContact("input:left")["known"],Is.False,"Physical hands must not borrow a virtual prop's scan opt-out");
  physics.PausePhysics();Assert.That(AssignEnvironment(pool,"",out error),Is.True,error);physics.StartPhysics();ContactTick();Assert.That((bool)editor.Liquids.ObserveContact(prop)["known"],Is.False);yield return null;
 }

 [UnityTest]public IEnumerator LiquidContactUnavailableNestedVesselCannotBorrowOuterWaterReadiness(){
  var(outer,_)=MediumFixture(.1f);physics.PausePhysics();Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Nested water",new Vector3(4,1.3f,0),1,Color.white,out var inner,out var error),Is.True,error);
  ContainerRun(new RoomAgentExecutor(editor),ContainerCall(inner,new RoomContainer{radius=.2f,height=.3f,capacityMl=1000,amountMl=800}));Assert.That(editor.SetItemPhysics(inner,new ObjectPhysicsSettings{mode="fixed",shape="box",mass=1}),Is.True);physics.StartPhysics();var point=new Vector3(4,1.4f,0);ContactPoint(point);
  Assert.That((string)editor.Liquids.ObserveContact("input:left")["bodyId"],Is.EqualTo(inner));editor.Find(inner).GetComponent<RigidRoomItem>().SetGeometryReady(false);ContactPoint(point);var medium=editor.Liquids.ObserveMedium(point,"");Assert.That((string)medium["bodyId"],Is.EqualTo(inner));Assert.That((bool)medium["active"],Is.False);Assert.That((bool)editor.Liquids.ObserveContact("input:left")["known"],Is.False);Assert.That(editor.Find(outer).GetComponent<RigidRoomItem>().GeometryReady,Is.True);yield return null;
 }
}}
