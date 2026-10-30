// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
 public sealed partial class RoomRulesTests
 {
  [UnityTest] public IEnumerator ParallelProgramMovesTwoNativeObjectsJoinsResultsAndStopsAllBranches(){
   string target=editor.Identity(block);var other=editor.Find("book");var start=block.transform.localPosition;var otherStart=other.transform.localPosition;
   Assert.That(editor.SaveAnimation("book",new RoomMotion{frames=new[]{new MotionFrame{position=otherStart},new MotionFrame{time=2,position=otherStart+Vector3.up*.4f}}},null,false),Is.True);
   var program=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-parallel.json")));
   program["resources"]=new JArray(target,"book");program["functions"][0]["body"][0]["branches"][0]["args"][0]["value"]=target;program["functions"][0]["body"][0]["branches"][1]["args"][0]["value"]="book";
   program["functions"][0]["body"][0]["branches"][0]["args"][1]["value"]=1;program["functions"][0]["body"][0]["branches"][1]["args"][1]["value"]=1.3;
   var sequence=new RuleSequence{id="",name="Two objects together",program=program.ToString(Newtonsoft.Json.Formatting.None)};var executor=new RoomAgentExecutor(editor);
   Assert.That(executor.Execute(new RoomAgentRequest{version=2,commands=new[]{new RoomAgentCommand{action="rules",rule=new RuleRequest{action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit{kind="save",reference="together",sequence=sequence}}}}}},out var error,out var ids),Is.True,error);
   string id=ids.Single();var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);var capture=new JObject{["program"]=program};
   void Evidence(string phase){var state=observer.Observe();state.visible=true;state.workspaceView="rules";state.rules=workshop.Observe(true);capture[phase]=JObject.Parse(RoomAgentWire.Serialize(state));}
   Evidence("saved");Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That(runtime.Trigger(id),Is.True);yield return new WaitForSeconds(.2f);
   Assert.That(block.transform.localPosition.x,Is.GreaterThan(start.x+.01f),runtime.Scheduler.LastError);Assert.That(other.transform.localPosition.y,Is.GreaterThan(otherStart.y+.01f));Assert.That(runtime.Scheduler.ObserveRuns().Count(r=>!string.IsNullOrEmpty(r.parentRunId)),Is.EqualTo(2));Evidence("running");
   for(int i=0;i<150&&runtime.Scheduler.ObserveRuns().Single(r=>string.IsNullOrEmpty(r.parentRunId)).state.Single(v=>v.name=="count").value!="17";i++)yield return new WaitForSeconds(.02f);
   Assert.That(runtime.Scheduler.ObserveRuns().Single().state.Single(v=>v.name=="count").value,Is.EqualTo("17"));Evidence("joined");Assert.That(workshop.Selected.program,Is.EqualTo(sequence.program));
   runtime.Scheduler.StopAll();Assert.That(runtime.Trigger(id),Is.True);yield return new WaitForSeconds(.15f);runtime.SendMessage("OnApplicationPause",true);runtime.SendMessage("OnApplicationPause",false);yield return null;
   Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Evidence("paused");var stopped=block.transform.localPosition;yield return new WaitForSeconds(.1f);Assert.That(block.transform.localPosition,Is.EqualTo(stopped));
   string output=Environment.GetEnvironmentVariable("MAESTRO_PARALLEL_EVIDENCE");if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"parallel-program.json"),capture.ToString());}
  }
 }
}
