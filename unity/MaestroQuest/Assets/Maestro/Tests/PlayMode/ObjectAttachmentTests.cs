// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
namespace Maestro.Quest.Tests
{
 public sealed partial class AvatarPropTests
 {
  string CreateRobot(){var recipe=RecipeTemplates.BoxRobot(true);recipe.playing=false;Assert.That(editor.CreateRecipe("Prop robot",new Vector3(2,0,0),1,recipe,out var id,out var error),Is.True,error);return id;}
  JObject HoldCall(string holder,string part="RightHand",string release="return"){
   var args=(JObject)BehaviourCatalog.Action("object.hold").Example.DeepClone();args["target"]=editor.Identity(ball);args["holder"]=new JObject{["kind"]="recipePart",["objectId"]=holder,["part"]=part,["revision"]=editor.ObjectRevision(holder)};args["offset"]["z"]=.35;args["release"]=release;return new JObject{["id"]="object.hold",["version"]=1,["arguments"]=args};
  }
  JObject Fact(string name,JObject args){Assert.That(BehaviourCatalog.TryRead(name,BehaviourCatalog.Fact(name).Version,args,new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True,name);return JObject.FromObject(value.Value);}
  void FitRobotBall(string robot){var point=editor.Find(robot).GetComponent<RecipeObject>().Part("RightHand");ball.transform.position=point.position+Vector3.forward*.35f;ball.GetComponent<RigidRoomItem>().Teleported();editor.RememberPlacement(editor.Identity(ball));}
  [UnityTest] public IEnumerator RobotAnchorCarriesAndThrowsThroughSharedParallelProgramsAndRoomPhysics(){
   Time.captureDeltaTime=0; // Exercise the shared motion clock and real-time physics together.
   string robot=CreateRobot(),prop=editor.Identity(ball);FitRobotBall(robot);var before=JsonUtility.ToJson(editor.Read(robot));world.SetSurfaces(true,"Synthetic aligned floor");world.StartPhysics();
   var program=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-object-hold.json")));program["resources"]=new JArray(prop,robot);var branches=program["functions"][0]["body"][0]["branches"];branches[0]["args"][0]["value"]=prop;branches[0]["args"][1]["value"]=robot;branches[0]["args"][2]["value"]=editor.ObjectRevision(robot);branches[1]["args"][0]["value"]=robot;
   var sequence=new RuleSequence{id="",name="Robot throws a ball",program=program.ToString(Newtonsoft.Json.Formatting.None)};var executor=new RoomAgentExecutor(editor);Assert.That(executor.Execute(new RoomAgentRequest{version=2,commands=new[]{new RoomAgentCommand{action="rules",rule=new RuleRequest{action="edit",revision=rules.Revision,edits=new[]{new RuleEdit{kind="save",reference="throw",sequence=sequence}}}}}},out var error,out var ids),Is.True,error);
   var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);var capture=new JObject{["program"]=program};void Capture(string phase){var state=observer.Observe();state.visible=true;state.workspaceView="rules";state.rules=rules.Observe(true);capture[phase]=JObject.Parse(RoomAgentWire.Serialize(state));capture[phase+"Attachment"]=Fact("object.attachment",new JObject{["target"]=prop});}
   Capture("saved");Assert.That(runtime.Trigger(ids.Single()),Is.True,runtime.Scheduler.LastError);yield return Until(()=>ball.GetComponent<HeldRoomProp>()?.Holding==true||runtime.Scheduler.RunningCount==0);var held=ball.GetComponent<HeldRoomProp>();Assert.That(held&&held.Holding,Is.True,runtime.Scheduler.LastError);Capture("holding");Assert.That(ball.GetComponent<Rigidbody>().isKinematic,Is.True);
   yield return Until(()=>held&&held.Released||runtime.Scheduler.RunningCount==0);Assert.That(held&&held.Released,Is.True,runtime.Scheduler.LastError);Capture("released");Assert.That(ball.GetComponent<Rigidbody>().linearVelocity.magnitude,Is.GreaterThan(.3f));Assert.That(ball.GetComponent<Rigidbody>().linearVelocity.magnitude,Is.LessThanOrEqualTo(15.1f));
   bool falling=false,bounced=false;for(int i=0;i<220;i++){yield return new WaitForFixedUpdate();var velocity=ball.GetComponent<Rigidbody>().linearVelocity;falling|=velocity.y<-.5f;bounced|=falling&&velocity.y>.3f;Assert.That(ball.transform.position.y,Is.GreaterThan(.04f));if(runtime.Scheduler.ObserveRuns().Length==1&&runtime.Scheduler.ObserveRuns()[0].locals.Any(v=>v.name=="didThrow"&&v.value=="True")&&!capture.ContainsKey("joined"))Capture("joined");}
   Assert.That(bounced,Is.True,"Released prop must collide with the actual floor");Assert.That(capture.ContainsKey("joined"),Is.True,runtime.Scheduler.LastError);Assert.That(JsonUtility.ToJson(editor.Read(robot)),Is.EqualTo(before));Assert.That(ball.GetComponent<RigidRoomItem>().AnimationOwned,Is.False);
   string folder=Environment.GetEnvironmentVariable("MAESTRO_ATTACHMENT_EVIDENCE");if(!string.IsNullOrEmpty(folder)){Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"robot-throw.json"),capture.ToString());}
  }
  [UnityTest] public IEnumerator StandaloneRootAndAvatarAnchorsReturnWithTruthfulReceiptsAndExactIdentity(){
   string robot=CreateRobot();FitRobotBall(robot);var call=HoldCall(robot);call["arguments"]["seconds"]=.2;var home=ball.transform.position;
   Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out var id,out var error),Is.True,error);yield return Until(()=>(string)runtime.Scheduler.Invocation(id)["phase"] is "completed" or "failed");var receipt=runtime.Scheduler.Invocation(id);Assert.That((string)receipt["phase"],Is.EqualTo("completed"),receipt.ToString());Assert.That((string)receipt["output"]["phase"],Is.EqualTo("returned"));Assert.That((bool)receipt["output"]["released"],Is.False);Assert.That(Vector3.Distance(home,ball.transform.position),Is.LessThan(.001f));
   var stale=(JObject)call.DeepClone();stale["arguments"]["holder"]["revision"]=editor.ObjectRevision(robot)+1;Assert.That(runtime.Scheduler.Invoke(stale,Time.unscaledTime,out _,out _),Is.False);
   var book=editor.Find("book");ball.transform.position=book.transform.position+Vector3.forward*.35f;ball.GetComponent<RigidRoomItem>().Teleported();call["arguments"]["holder"]=new JObject{["kind"]="object",["objectId"]="book",["revision"]=editor.ObjectRevision("book")};var anchor=Fact("object.anchor",new JObject{["holder"]=call["arguments"]["holder"].DeepClone()});Assert.That(Vector3.Distance(anchor["position"].ToObject<Vector3>(),book.transform.position),Is.LessThan(.001f));Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out id,out error),Is.True,error);runtime.Scheduler.CancelInvocation(id,out _);
   var hand=avatar.PoseRig.Bone(PoseJoint.RightHand);ball.transform.position=hand.position+hand.rotation*Vector3.forward*.35f;ball.GetComponent<RigidRoomItem>().Teleported();call["arguments"]["holder"]=new JObject{["kind"]="avatarHand",["objectId"]="maestro",["hand"]="right",["avatarHash"]=""};Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out id,out error),Is.True,error);
   // This checks synchronous admission identity. Yielding could legitimately
   // retire a 0.2-second hold before observation on a loaded CI/editor frame.
   Assert.That((string)Fact("object.attachment",new JObject{["target"]=editor.Identity(ball)})["kind"],Is.EqualTo("avatarHand"));runtime.Scheduler.CancelInvocation(id,out _);call["arguments"]["holder"]["avatarHash"]=new string('f',64);Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out _,out _),Is.False);
  }
  [UnityTest] public IEnumerator HolderGripPauseReplacementAndNestedChainsCannotLeaveGhostAttachments(){
   string robot=CreateRobot();FitRobotBall(robot);var call=HoldCall(robot);Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out var id,out var error),Is.True,error);
   var nested=HoldCall(robot);nested["arguments"]["target"]=robot;nested["arguments"]["holder"]=new JObject{["kind"]="object",["objectId"]=editor.Identity(ball),["revision"]=editor.ObjectRevision(editor.Identity(ball))};Assert.That(runtime.Scheduler.Invoke(nested,Time.unscaledTime,out _,out _),Is.False);
   var handObject=new GameObject("Holder grip");handObject.transform.SetParent(root.transform,false);handObject.SetActive(false);var ray=handObject.AddComponent<XRRayInteractor>();ray.interactionManager=manager;ray.selectActionTrigger=XRBaseInputInteractor.InputTriggerType.State;ray.selectInput=new XRInputButtonReader{inputSourceMode=XRInputButtonReader.InputSourceMode.ManualValue,manualPerformed=true,manualValue=1};handObject.SetActive(true);var before=ball.transform.position;manager.SelectEnter((IXRSelectInteractor)ray,editor.Find(robot).Grab);Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That(Vector3.Distance(before,ball.transform.position),Is.LessThan(.01f));manager.SelectExit((IXRSelectInteractor)ray,editor.Find(robot).Grab);handObject.SetActive(false);yield return null;Assert.That(ball.GetComponent<RigidRoomItem>().AnimationOwned,Is.False);
   call["arguments"]["holder"]["revision"]=editor.ObjectRevision(robot);Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out id,out error),Is.True,error);runtime.SendMessage("OnApplicationPause",true);runtime.SendMessage("OnApplicationPause",false);yield return null;Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That(ball.GetComponent<RigidRoomItem>().AnimationOwned,Is.False);
   Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out id,out error),Is.True,error);var recipe=editor.Find(robot).GetComponent<RecipeObject>();var changed=editor.Read(robot).recipe.Copy();changed.loop=!changed.loop;recipe.Apply(changed);yield return null;yield return null;Assert.That((string)runtime.Scheduler.Invocation(id)["phase"],Is.EqualTo("failed"));Assert.That(ball.GetComponent<RigidRoomItem>().AnimationOwned,Is.False);
   var self=HoldCall(robot);self["arguments"]["target"]=robot;Assert.That(runtime.Scheduler.Invoke(self,Time.unscaledTime,out _,out _),Is.False);
  }
 }
}
