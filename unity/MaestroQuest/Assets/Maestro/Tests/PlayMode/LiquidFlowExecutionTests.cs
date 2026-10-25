// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
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
  void CaptureFlowExecution(string name){
   string directory=Environment.GetEnvironmentVariable("MAESTRO_FLOW_EXECUTION_EVIDENCE");if(string.IsNullOrEmpty(directory))return;
   Directory.CreateDirectory(directory);var w=editor.Liquids.LastFlowExecution;
   File.WriteAllText(Path.Combine(directory,name+".json"),new JObject{["groups"]=w.Groups,["members"]=w.Members,["pourOriginQueries"]=w.PourOriginQueries,["pourOriginCandidates"]=w.PourOriginCandidates,["pourSegments"]=w.PourSegments,["pourCandidates"]=w.PourCandidates,["pourRayQueries"]=w.PourRayQueries,["pourHitCandidates"]=w.PourHitCandidates,["immersionChecks"]=w.ImmersionChecks,["immersionCandidates"]=w.ImmersionCandidates,["scoopRecipients"]=w.ScoopRecipients,["scoopCandidates"]=w.ScoopCandidates,["scoopPathQueries"]=w.ScoopPathQueries,["scoopPathCandidates"]=w.ScoopPathCandidates,["rainSamples"]=w.RainSamples}.ToString());
  }
  [UnityTest]public IEnumerator FlowExecutionIndependentCircuitsUseLocalCandidatesAndOneUndo(){
   var(a,b,c,d)=IndependentFlowCups();Assert.That(editor.SetWeather(editor.WeatherRevision,1,new(){rainMmPerHour=60},0,out var error),Is.True,error);
   int events=0;editor.ContainerPoured+=(_,_,_,_,_)=>events++;
   physics.StartPhysics();editor.Liquids.Tick(.05f);CaptureFlowExecution("independent");var w=editor.Liquids.LastFlowExecution;
   Assert.That(FlowAmount(b),Is.GreaterThan(0));Assert.That(FlowAmount(d),Is.GreaterThan(0));Assert.That(events,Is.Zero);
   Assert.That(w.Groups,Is.EqualTo(2));Assert.That(w.Members,Is.EqualTo(4));Assert.That(w.PourSegments,Is.GreaterThan(0));Assert.That(w.ImmersionChecks,Is.GreaterThan(0));Assert.That(w.ScoopRecipients,Is.GreaterThan(0));Assert.That(w.RainSamples,Is.GreaterThan(0));
   Assert.That(w.PourCandidates,Is.EqualTo(2*w.PourSegments),"A stream must not enumerate the other complete circuit");
   Assert.That(w.ImmersionCandidates,Is.EqualTo(2*w.ImmersionChecks));Assert.That(w.ScoopCandidates,Is.EqualTo(2*w.ScoopRecipients));
   var ids=new[]{a,b,c,d};double rain=ids.Sum(id=>(double)editor.Liquids.ObserveRain(id)["collectedMl"]);
   Assert.That(ids.Sum(id=>FlowAmount(id)+FlowAmount(id,"spilledMl")),Is.EqualTo(800+rain).Within(1e-8));
   Assert.That(editor.Read(b).containers[0].amountMl,Is.Zero);double accepted=FlowAmount(b);
   physics.PausePhysics();yield return LiquidSaved();Assert.That(events,Is.EqualTo(2));Assert.That(editor.Read(b).containers[0].amountMl,Is.EqualTo(accepted));
   editor.Undo();Assert.That(FlowAmount(a),Is.EqualTo(400));Assert.That(FlowAmount(c),Is.EqualTo(400));Assert.That(FlowAmount(b)+FlowAmount(d),Is.Zero);
   editor.Redo();Assert.That(FlowAmount(b),Is.EqualTo(accepted));yield return null;
  }
  [UnityTest]public IEnumerator FlowExecutionUnavailableCircuitIsExcludedFromQuantityCandidateLoops(){
   var(a,b,c,d)=IndependentFlowCups();var body=editor.Find(b).GetComponent<RigidRoomItem>();body.SetGeometryReady(false);
   physics.StartPhysics();editor.Liquids.Tick(.05f);CaptureFlowExecution("waiting");var w=editor.Liquids.LastFlowExecution;
   Assert.That(FlowAmount(a),Is.EqualTo(400));Assert.That(FlowAmount(b),Is.Zero);Assert.That(FlowAmount(d),Is.GreaterThan(0));Assert.That(FlowAmount(c)+FlowAmount(d),Is.EqualTo(400).Within(1e-8));
   Assert.That(w.Groups,Is.EqualTo(1));Assert.That(w.Members,Is.EqualTo(2));Assert.That(w.PourSegments,Is.GreaterThan(0));
   Assert.That(w.PourCandidates,Is.EqualTo(2*w.PourSegments));Assert.That(w.ImmersionCandidates,Is.EqualTo(2*w.ImmersionChecks));Assert.That(w.ScoopCandidates,Is.EqualTo(2*w.ScoopRecipients));
   body.SetGeometryReady(true);editor.Liquids.Tick(.05f);CaptureFlowExecution("recovered");w=editor.Liquids.LastFlowExecution;
   Assert.That(w.Groups,Is.EqualTo(2));Assert.That(w.Members,Is.EqualTo(4));Assert.That(FlowAmount(b),Is.GreaterThan(0).And.LessThanOrEqualTo(18.75));
   Assert.That(FlowAmount(a)+FlowAmount(b)+FlowAmount(c)+FlowAmount(d),Is.EqualTo(800).Within(1e-8));physics.PausePhysics();yield return null;
  }
  [UnityTest]public IEnumerator FlowExecutionMovedReceiverRebuildsCompleteCandidateGroups(){
   var(a,b,c,d)=IndependentFlowCups();physics.StartPhysics();editor.Liquids.Tick(.05f);var item=editor.Find(b);var start=item.transform.position;
   item.transform.position=editor.Find(d).transform.position+Vector3.right*.03f;item.GetComponent<RigidRoomItem>().Teleported();
   editor.Liquids.Tick(.05f);CaptureFlowExecution("moved");var w=editor.Liquids.LastFlowExecution;
   Assert.That(w.Groups,Is.EqualTo(2));Assert.That(w.Members,Is.EqualTo(4));Assert.That(w.ScoopRecipients,Is.EqualTo(4));
   Assert.That(w.ScoopCandidates,Is.EqualTo(10),"One isolated source plus a complete three-vessel circuit: 1 + 3*3 visits");
   Assert.That(FlowAmount(a,"spilledMl"),Is.GreaterThan(0));
   item.transform.position=start;item.GetComponent<RigidRoomItem>().Teleported();editor.Liquids.Tick(.05f);CaptureFlowExecution("split");w=editor.Liquids.LastFlowExecution;
   Assert.That(w.Groups,Is.EqualTo(2));Assert.That(w.ScoopCandidates,Is.EqualTo(8),"Both restored two-vessel circuits must be rebuilt before execution");
   Assert.That(new[]{a,b,c,d}.Sum(id=>FlowAmount(id)+FlowAmount(id,"spilledMl")),Is.EqualTo(800).Within(1e-8));
   physics.PausePhysics();yield return null;
  }
 }
}
