// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace Maestro.Quest.Tests
{
 public sealed partial class RoomRulesTests
 {
  JObject ChannelProgram(string target){var p=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-channel-wait.json")));p["resources"]=new JArray(target);p["functions"][0]["body"][0]["arguments"]["target"]=target;return p;}
  [UnityTest] public IEnumerator NativeRecipeActionWaitsForItsChannelThenPlaysAndReportsCompletion(){
   string target=RecipeTarget();var item=editor.Find(target);var recipe=item.GetComponent<RecipeObject>();var p=ChannelProgram(target);
   var sequence=new RuleSequence {id="",name="Wait then wave",program=p.ToString(Newtonsoft.Json.Formatting.None)};var executor=new RoomAgentExecutor(editor);
   Assert.That(executor.Execute(new RoomAgentRequest {version=2,commands=new[]{new RoomAgentCommand {action="rules",rule=new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",reference="waiting",sequence=sequence}}}}}},out var error,out var ids),Is.True,error);
   string id=ids.Single(),before=JsonUtility.ToJson(editor.Read(target));var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);var capture=new JObject {["program"]=p};
   void Capture(string phase){var state=observer.Observe();state.visible=true;state.workspaceView="rules";state.rules=workshop.Observe(true);capture[phase]=JObject.Parse(RoomAgentWire.Serialize(state));capture[phase+"Pose"]=LivePart(target,"RightUpperArm");}
   Capture("saved");Assert.That(runtime.Scheduler.Invoke(PartCall(target,"RightUpperArm",.5f),Time.unscaledTime,out var first,out error),Is.True,error);Assert.That(runtime.Trigger(id),Is.True,runtime.Scheduler.LastError);
   var pending=runtime.Scheduler.ObserveRuns().Single();Assert.That(pending.waiting,Is.True);Assert.That(editor.Ownership.Observe().owners.Any(o=>o.id==pending.id),Is.False);Capture("waiting");
   float deadline=Time.realtimeSinceStartup+5;while(runtime.Scheduler.ObserveRuns().Any(r=>r.waiting)&&Time.realtimeSinceStartup<deadline)yield return null;
   Assert.That((string)runtime.Scheduler.Invocation(first)["phase"],Is.EqualTo("completed"));Assert.That(runtime.Scheduler.ObserveRuns().Single().waiting,Is.False);Capture("running");
   var pose=recipe.Part("RightUpperArm").localRotation;yield return new WaitForSeconds(.12f);Assert.That(Quaternion.Angle(pose,recipe.Part("RightUpperArm").localRotation),Is.GreaterThan(5));
   while(runtime.Scheduler.RunningCount>0&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(runtime.Scheduler.RunningCount,Is.Zero,runtime.Scheduler.LastError);Assert.That(runtime.Scheduler.Outcomes.Last().phase,Is.EqualTo("completed"));Capture("completed");
   Assert.That(Quaternion.Angle(recipe.Part("RightUpperArm").localRotation,Quaternion.Euler(0,0,101.25f)),Is.LessThan(.1f));Assert.That(JsonUtility.ToJson(editor.Read(target)),Is.EqualTo(before));
   string folder=Environment.GetEnvironmentVariable("MAESTRO_CHANNEL_WAIT_EVIDENCE");if(!string.IsNullOrEmpty(folder)){Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"channel-wait.json"),capture.ToString());}
  }
  [UnityTest] public IEnumerator ActualGripCancelsQueuedRecipeWorkWithoutRestartingAfterRelease(){
   string target=RecipeTarget();var item=editor.Find(target);var recipe=item.GetComponent<RecipeObject>();var seq=new RuleSequence {id=Guid.NewGuid().ToString("N"),name="Queued arm",program=ChannelProgram(target).ToString()};runtime.Scheduler.Configure(new RuleDocument {sequences=new[]{seq}});
   Assert.That(runtime.Scheduler.Invoke(PartCall(target,"RightUpperArm",2),Time.unscaledTime,out _,out var error),Is.True,error);Assert.That(runtime.Trigger(seq.id),Is.True);Assert.That(runtime.Scheduler.ObserveRuns().Single().waiting,Is.True);
   var hand=Hand(1,item.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,item.Grab);yield return null;Assert.That(runtime.Scheduler.RunningCount,Is.Zero);var pose=recipe.Part("RightUpperArm").localRotation;
   manager.SelectExit((IXRSelectInteractor)hand,item.Grab);yield return new WaitForSeconds(.2f);Assert.That(Quaternion.Angle(pose,recipe.Part("RightUpperArm").localRotation),Is.LessThan(.01f));Assert.That(recipe.IsPlaying,Is.False);Assert.That(runtime.Scheduler.Outcomes.Last().phase,Is.EqualTo("cancelled"));
  }
 }
 public sealed partial class AvatarPropTests
 {
  [UnityTest] public IEnumerator QueuedPickupChecksCurrentReachAfterThePreviousOwnerReturnsTheProp(){
   string robot=CreateRobot();FitRobotBall(robot);var original=ball.transform.position;var first=HoldCall(robot);first["arguments"]["seconds"]=.4;var pickup=(JObject)first.DeepClone();pickup["arguments"]["reach"]=new JObject {["radius"]=.1,["physics"]=false};
   var p=JObject.Parse(BehaviourProgram.FromInvocation(pickup));p["version"]=3;p["state"]=new JArray();p["events"]=new JArray();p["functions"][0]["body"][0]["waitForChannels"]=new JObject {["value"]=3};
   var seq=new RuleSequence {id=Guid.NewGuid().ToString("N"),name="Nearby pickup",program=p.ToString()};runtime.Scheduler.Configure(new RuleDocument {sequences=new[]{seq}});
   Assert.That(runtime.Scheduler.Invoke(first,Time.unscaledTime,out var carrying,out var error),Is.True,error);Assert.That(runtime.Trigger(seq.id),Is.True);Assert.That(runtime.Scheduler.ObserveRuns().Single().waiting,Is.True);
   editor.Find(robot).transform.position+=Vector3.right*.5f;yield return Until(()=>runtime.Scheduler.RunningCount==0);
   Assert.That((string)runtime.Scheduler.Invocation(carrying)["phase"],Is.EqualTo("completed"));Assert.That(runtime.Scheduler.Outcomes.Last().phase,Is.EqualTo("failed"));Assert.That(runtime.Scheduler.LastError,Does.Contain("reach"));Assert.That(ball.GetComponent<HeldRoomProp>(),Is.Null,"A completed hold releases its component; the failed pickup must not attach another");Assert.That(Vector3.Distance(ball.transform.position,original),Is.LessThan(.01f));
  }
 }
}
