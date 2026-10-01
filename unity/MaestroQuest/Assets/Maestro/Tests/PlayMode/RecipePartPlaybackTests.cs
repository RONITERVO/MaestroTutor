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
  JObject PartCall(string target,string part,float seconds=1)=>new(){["id"]="animation.play",["version"]=1,["arguments"]=new JObject{["target"]=target,["source"]=new JObject{["kind"]="recipe",["part"]=part},["channel"]="recipePart",["seconds"]=seconds,["loop"]=false}};
  JObject LivePart(string target,string part){Assert.That(BehaviourCatalog.TryRead("object.recipe.pose",1,new JObject{["target"]=target,["part"]=part},new BehaviourCatalog.FactContext(editor:editor),out var pose),Is.True);return JObject.FromObject(pose.Value);}
  [UnityTest] public IEnumerator RecipePartsRunIndependentlyWithLivePoseAndNoSavedEditsOrPhysicsOwnership(){
   string target=RecipeTarget(true);var item=editor.Find(target);var recipe=item.GetComponent<RecipeObject>();string before=JsonUtility.ToJson(editor.Read(target));int revision=editor.ObjectRevision(target);var host=new RoomRuleActions(editor,animations);
   CapabilityCall Call(string part){var json=PartCall(target,part);Assert.That(BehaviourCatalog.TryCall("animation.play",1,(JObject)json["arguments"],out var call,out var error),Is.True,error);return call;}
   Assert.That(host.Start("upper",Call("RightUpperArm"),out _,out var error),Is.True,error);Assert.That(host.Start("lower",Call("RightLowerArm"),out _,out error),Is.True,error);Assert.That(item.GetComponent<RigidRoomItem>().AnimationOwned,Is.False);
   yield return new WaitForSeconds(.2f);Assert.That(Quaternion.Angle(recipe.Part("RightUpperArm").localRotation,Quaternion.identity),Is.GreaterThan(10));Assert.That((bool)LivePart(target,"RightUpperArm")["playing"],Is.True);
   Assert.That(host.Complete("upper",out error),Is.True,error);var upper=recipe.Part("RightUpperArm").localRotation;var lower=recipe.Part("RightLowerArm").localRotation;yield return new WaitForSeconds(.6f);
   Assert.That(Quaternion.Angle(upper,recipe.Part("RightUpperArm").localRotation),Is.LessThan(.01f));Assert.That(Quaternion.Angle(lower,recipe.Part("RightLowerArm").localRotation),Is.GreaterThan(2));Assert.That((bool)LivePart(target,"RightUpperArm")["playing"],Is.False);Assert.That((bool)LivePart(target,"RightLowerArm")["playing"],Is.True);
   host.Stop("lower",true);var stopped=recipe.Part("RightLowerArm").localRotation;yield return new WaitForSeconds(.1f);Assert.That(Quaternion.Angle(stopped,recipe.Part("RightLowerArm").localRotation),Is.LessThan(.01f));Assert.That(recipe.IsPlaying,Is.False);
   Assert.That(JsonUtility.ToJson(editor.Read(target)),Is.EqualTo(before));Assert.That(editor.ObjectRevision(target),Is.EqualTo(revision));Assert.That(editor.Ownership.Observe().owners,Is.Empty);
   var pose=LivePart(target,"RightLowerArm");Assert.That((string)pose["parent"],Is.EqualTo("RightUpperArm"));Assert.That(Vector3.Distance(pose["world"]["position"].ToObject<Vector3>(),recipe.Part("RightLowerArm").position),Is.LessThan(.0001f));
   recipe.Restart();yield return new WaitForSeconds(.1f);Assert.That((bool)LivePart(target,"RightUpperArm")["playing"],Is.True,"Only explicit whole restart re-enables suppressed autoplay");
  }
  [UnityTest] public IEnumerator RecipePartFailureLifecycleAndConflictsDoNotClaimSuccessOrRestart(){
   string target=RecipeTarget();var item=editor.Find(target);var recipe=item.GetComponent<RecipeObject>();var call=PartCall(target,"RightUpperArm");
   Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out var run,out var error),Is.True,error);Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out _,out _),Is.False);
   var whole=(JObject)call.DeepClone();whole["arguments"]["channel"]="wholeTarget";((JObject)whole["arguments"]["source"]).Remove("part");Assert.That(runtime.Scheduler.Invoke(whole,Time.unscaledTime,out _,out _),Is.False);
   Assert.That(runtime.Scheduler.Invoke(PartCall(target,"Missing"),Time.unscaledTime,out _,out _),Is.False);Assert.That(runtime.Scheduler.Invoke(PartCall("book","RightUpperArm"),Time.unscaledTime,out _,out _),Is.False);
   runtime.Scheduler.CancelInvocation(run,out _);Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out run,out error),Is.True,error);
   recipe.enabled=false;yield return null;Assert.That((string)runtime.Scheduler.Invocation(run)["phase"],Is.EqualTo("failed"));recipe.enabled=true;yield return null;Assert.That(recipe.IsPlaying,Is.False);
   Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out run,out error),Is.True,error);yield return new WaitForSeconds(.1f);runtime.SendMessage("OnApplicationPause",true);runtime.SendMessage("OnApplicationPause",false);var pose=recipe.Part("RightUpperArm").localRotation;yield return new WaitForSeconds(.1f);Assert.That(Quaternion.Angle(pose,recipe.Part("RightUpperArm").localRotation),Is.LessThan(.01f));Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
   var host=new RoomRuleActions(editor,animations);Assert.That(BehaviourCatalog.TryCall("animation.play",1,(JObject)call["arguments"],out var parsed,out error),Is.True,error);Assert.That(host.Start("replacement",parsed,out _,out error),Is.True,error);var changed=editor.Read(target).recipe.Copy();changed.duration=4;foreach(var track in changed.tracks)foreach(var key in track.keys)key.time*=2;Assert.That(recipe.Apply(changed),Is.True);Assert.That(host.Complete("replacement",out _),Is.False);
  }
  [UnityTest] public IEnumerator ParallelNamedPartsUseSharedProgramsAndRealGripCancellation(){
   string target=RecipeTarget();var item=editor.Find(target);var recipe=item.GetComponent<RecipeObject>();var program=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-recipe-parts.json")));program["resources"]=new JArray(target);foreach(var branch in program["functions"][0]["body"][0]["branches"])branch["args"][0]["value"]=target;
   var sequence=new RuleSequence{id="",name="Robot arm parts together",program=program.ToString(Newtonsoft.Json.Formatting.None)};var executor=new RoomAgentExecutor(editor);Assert.That(executor.Execute(new RoomAgentRequest{version=2,commands=new[]{new RoomAgentCommand{action="rules",rule=new RuleRequest{action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit{kind="save",reference="robot",sequence=sequence}}}}}},out var error,out var ids),Is.True,error);
   var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);var capture=new JObject{["program"]=program};void Evidence(string phase){var state=observer.Observe();state.visible=true;state.workspaceView="rules";state.rules=workshop.Observe(true);capture[phase]=JObject.Parse(RoomAgentWire.Serialize(state));capture[phase+"Pose"]=LivePart(target,"RightUpperArm");}
   Evidence("saved");Assert.That(runtime.Trigger(ids.Single()),Is.True);yield return new WaitForSeconds(.2f);Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(3));Assert.That(editor.Ownership.Observe().owners.SelectMany(o=>o.claims).Select(c=>c.channel),Is.EquivalentTo(new[]{"recipePart:RightUpperArm","recipePart:RightLowerArm"}));Evidence("running");
   for(int i=0;i<150&&runtime.Scheduler.RunningCount>0;i++)yield return new WaitForSeconds(.02f);Assert.That(runtime.Scheduler.RunningCount,Is.Zero,runtime.Scheduler.LastError);Assert.That(Quaternion.Angle(recipe.Part("RightUpperArm").localRotation,Quaternion.Euler(0,0,135)),Is.LessThan(.1f));Evidence("completed");
   Assert.That(runtime.Trigger(ids.Single()),Is.True);yield return new WaitForSeconds(.1f);var hand=Hand(1,item.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,item.Grab);yield return null;Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Evidence("held");var pose=recipe.Part("RightUpperArm").localRotation;manager.SelectExit((IXRSelectInteractor)hand,item.Grab);yield return new WaitForSeconds(.1f);Assert.That(Quaternion.Angle(pose,recipe.Part("RightUpperArm").localRotation),Is.LessThan(.01f));Assert.That(workshop.Selected.program,Is.EqualTo(sequence.program));
   string output=Environment.GetEnvironmentVariable("MAESTRO_RECIPE_PARTS_EVIDENCE");if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"recipe-parts.json"),capture.ToString());}
  }
 }
}
