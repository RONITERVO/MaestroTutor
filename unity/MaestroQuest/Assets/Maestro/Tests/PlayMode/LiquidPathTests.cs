// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
 public sealed partial class RoomRulesTests {
  JObject PathFact(Vector3 from,Vector3 to,string target=""){
   Assert.That(BehaviourCatalog.TryRead("world.medium.path",1,new JObject{["from"]=WorkspaceViewpoint.Point(from),["to"]=WorkspaceViewpoint.Point(to),["target"]=target},new BehaviourCatalog.FactContext(editor:editor),out var result),Is.True);
   return (JObject)result.Value;
  }
  [UnityTest]public IEnumerator LiquidPathSharedFactUsesActualPausedGeometryAndDoesNotChangeTheWorld(){
   var(pool,_)=MediumFixture(.05f);physics.PausePhysics();editor.SaveNow();
   string before=RoomLiquidCodec.Encode(editor.Snapshot());int revision=editor.Revision;
   Assert.That(RoomControls.Capabilities(editor),Does.Contain(LiquidPathFact.Feature));
   var value=PathFact(new(3,1.4f,0),new(5,1.4f,0),pool);
   Assert.That((string)value["availability"],Is.EqualTo("ready"));Assert.That((double)value["wetMetres"],Is.EqualTo(1).Within(.00001));Assert.That((double)value["dryMetres"],Is.EqualTo(1).Within(.00001));
   Assert.That((double)value["targetMetres"],Is.EqualTo(1).Within(.00001));Assert.That((int)value["bodyCount"],Is.EqualTo(1));
   var reverse=PathFact(new(5,1.4f,0),new(3,1.4f,0),pool);Assert.That((double)reverse["wetMetres"],Is.EqualTo((double)value["wetMetres"]).Within(.00001));
   var far=PathFact(new(20,1,0),new(24,1,0));Assert.That((string)far["availability"],Is.EqualTo("ready"));Assert.That((double)far["dryMetres"],Is.EqualTo(4).Within(.00001));
   Assert.IsNull(editor.Liquids.ObserveLiquidPath(new(25,25,0),Vector3.zero,""));
   var point=PathFact(new(4,1.4f,0),new(4,1.4f,0));Assert.That((double)point["wetMetres"],Is.Zero);Assert.That((double)point["dryMetres"],Is.Zero);
   Assert.That(physics.Running,Is.False);Assert.That(editor.NativeActivationPending,Is.False);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(RoomLiquidCodec.Encode(editor.Snapshot()),Is.EqualTo(before));
   yield return null;
  }
  [UnityTest]public IEnumerator LiquidPathNestedWaterKeepsIndependentOwnershipAndReusesCurrentPose(){
   var(pool,_)=MediumFixture(.05f);physics.PausePhysics();
   Assert.IsTrue(editor.CreatePrimitive(RoomObjectKind.Block,"Inner water",new Vector3(4,1.3f,0),1,Color.white,out var inner,out var error),error);
   ContainerFixture(new RoomAgentExecutor(editor),ContainerCall(inner,new RoomContainer{height=.3f,radius=.2f,capacityMl=1000,amountMl=800}));
   var from=new Vector3(3,1.4f,0);var to=new Vector3(5,1.4f,0);
   var outer=PathFact(from,to,pool);var nested=PathFact(from,to,inner);
   Assert.That((double)outer["wetMetres"],Is.EqualTo(1).Within(.00001));Assert.That((double)outer["targetMetres"],Is.EqualTo(.6).Within(.00001));Assert.That((double)nested["targetMetres"],Is.EqualTo(.4).Within(.00001));
   Assert.That((int)outer["bodyCount"],Is.EqualTo(2));Assert.IsFalse(editor.Read(inner).containers[0].underwater.enabled);
   editor.Find(inner).transform.position=new(8,1.3f,0);Physics.SyncTransforms();
   outer=PathFact(from,to,pool);Assert.That((double)outer["targetMetres"],Is.EqualTo(1).Within(.00001));Assert.That((int)outer["bodyCount"],Is.EqualTo(1));
   yield return null;
  }
  [UnityTest]public IEnumerator LiquidPathRequiresMissingWaterBeyondTheFirstHitButNotBeyondItsEnd(){
   var(pool,_)=MediumFixture(.05f);physics.PausePhysics();string missing=MissingMedium(new(5.7f,1.3f,0));
   var from=new Vector3(3,1.4f,0);var to=new Vector3(6,1.4f,0);int revision=editor.Revision;
   editor.Liquids.CaptureSurfaceContacts();Assert.That(editor.Liquids.QuerySurfaceContact(from,to).State,Is.Not.EqualTo(LiquidSurfaceState.Unknown));
   var value=PathFact(from,to,pool);Assert.That((string)value["availability"],Is.EqualTo("unavailable"));Assert.That((int)value["dependencyCount"],Is.EqualTo(1));Assert.That((string)value["dependencies"][0]["target"],Is.EqualTo(missing));
   Assert.That((double)value["wetMetres"],Is.Zero);Assert.That((double)value["dryMetres"],Is.Zero);Assert.That((double)value["targetMetres"],Is.Zero);
   var unavailable=(JObject)value.DeepClone();
   value=PathFact(from,new(5,1.4f,0),pool);Assert.That((string)value["availability"],Is.EqualTo("ready"));Assert.That((double)value["wetMetres"],Is.EqualTo(1).Within(.00001));
   var evidence=System.Environment.GetEnvironmentVariable("MAESTRO_LIQUID_PATH_EVIDENCE");
   if(!string.IsNullOrEmpty(evidence)){
    var dry=PathFact(new(3,3,0),new(5,3,0),pool);
    Assert.That((string)dry["availability"],Is.EqualTo("ready"));Assert.That((double)dry["wetMetres"],Is.Zero);
    System.IO.File.WriteAllText(evidence,new JObject{["wet"]=value,["dry"]=dry,["unavailable"]=unavailable}.ToString()+System.Environment.NewLine);
   }
   Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(editor.NativeActivationPending,Is.False);Assert.IsFalse(editor.Find(missing).gameObject.activeSelf);yield return null;
  }
  [UnityTest]public IEnumerator LiquidPathPendingWaterCannotBecomeDryWhileNativePreparationIsQueued(){
   var(pool,_)=MediumFixture(.05f);physics.PausePhysics();
   Assert.That(editor.RetireNativeArea(NativeAreaFor(pool),out var error),Is.True,error);yield return null;
   var(group,_)=NativePreparationGroup();Assert.That(editor.RetireNativeArea(group,out error),Is.True,error);yield return null;
   var first=editor.ActivateNativeArea(group);Assert.That(first.IsCompleted,Is.False);
   using var demand=editor.DemandNativeEntities(new[]{pool},()=>new[]{pool},()=>true);
   var value=PathFact(new(3,1.4f,0),new(5,1.4f,0),pool);
   Assert.That((string)value["availability"],Is.EqualTo("pending"));Assert.That((double)value["dryMetres"],Is.Zero);Assert.That((double)value["wetMetres"],Is.Zero);
   float deadline=Time.realtimeSinceStartup+12;while(!demand.Preparation.IsCompleted&&Time.realtimeSinceStartup<deadline)yield return null;
   Assert.That(demand.Preparation.IsCompleted,Is.True);Assert.That(demand.Error,Is.Null);Assert.That(first.IsCompleted,Is.True);Assert.That(first.Result,Is.Null);
   value=PathFact(new(3,1.4f,0),new(5,1.4f,0),pool);Assert.That((string)value["availability"],Is.EqualTo("ready"));Assert.That((double)value["wetMetres"],Is.EqualTo(1).Within(.00001));Assert.IsFalse(physics.Running);
  }
  [UnityTest]public IEnumerator LiquidPathFollowsUnsavedLiveRainAndFreshNativeAvailability(){
   var(pool,_)=MediumFixture(.05f);
   var from=new Vector3(4,1.2f,0);var to=new Vector3(4,1.8f,0);var before=PathFact(from,to,pool);
   Assert.That(editor.SetWeather(editor.WeatherRevision,1,new(){rainMmPerHour=120},0,out var error),Is.True,error);editor.Liquids.Tick(.1f);
   var after=PathFact(from,to,pool);Assert.That((double)after["wetMetres"],Is.GreaterThan((double)before["wetMetres"]));Assert.That(editor.Read(pool).containers[0].amountMl,Is.EqualTo(400000));
   physics.PausePhysics();Assert.That(editor.RetireNativeArea(NativeAreaFor(pool),out error),Is.True,error);yield return null;
   after=PathFact(from,to,pool);Assert.That((string)after["availability"],Is.EqualTo("unavailable"));Assert.That((double)after["wetMetres"],Is.Zero);
  }
 }
}
