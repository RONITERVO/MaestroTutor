// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Imports;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
namespace Maestro.Quest.Tests {
 [DefaultExecutionOrder(300)] public sealed class CatchPoseObservation:MonoBehaviour {
  public AvatarPoseRig Rig;public Vector3 Hand;public float UpperLength,LowerLength;
  void LateUpdate(){if(!Rig)return;var u=Rig.Bone(PoseJoint.RightUpperArm);var l=Rig.Bone(PoseJoint.RightLowerArm);var h=Rig.Bone(PoseJoint.RightHand);Hand=h.position;UpperLength=Vector3.Distance(u.position,l.position);LowerLength=Vector3.Distance(l.position,h.position);}
 }

 public sealed partial class AvatarPropTests {
  JObject CatchSetup(float timeout=2){
   Time.captureDeltaTime=0;var view=new GameObject("Catch viewer");view.transform.SetParent(root.transform,false);view.transform.position=new Vector3(-4,1.6f,0);root.GetComponent<RoomInteraction>().Viewer=view.transform;
   var holder=editor.Find("book");holder.transform.position=new Vector3(3,1,0);holder.transform.rotation=Quaternion.identity;
   world.SetSurfaces(true,"Synthetic aligned catch floor");world.StartPhysics();
   ball.transform.position=new Vector3(2,1,.35f);ball.GetComponent<RigidRoomItem>().Teleported();
   var args=BehaviourCatalog.Action("object.physics.catch").Example;args["target"]=editor.Identity(ball);args["holder"]=new JObject{["kind"]="object",["objectId"]="book",["revision"]=editor.ObjectRevision("book")};args["offset"]=JObject.Parse(JsonUtility.ToJson(new Vector3(0,0,.35f)));args["timeout"]=timeout;args["holdSeconds"]=.3;return new JObject{["id"]="object.physics.catch",["version"]=1,["arguments"]=args};
  }
  void ThrowAtCatch(Vector3 centre,float flight=.35f){ball.transform.position=centre+Vector3.left*.7f;var rigid=ball.GetComponent<RigidRoomItem>();rigid.Teleported();Assert.That(rigid.Launch(Vector3.right*(.7f/flight)-Physics.gravity*(flight*.5f),Vector3.zero),Is.True);}
  XRRayInteractor CatchHand(RoomItem target){var obj=new GameObject("Catch grip");obj.transform.SetParent(root.transform,false);obj.transform.position=target.transform.position;obj.SetActive(false);var ray=obj.AddComponent<XRRayInteractor>();ray.interactionManager=manager;ray.keepSelectedTargetValid=true;ray.selectActionTrigger=XRBaseInputInteractor.InputTriggerType.State;ray.selectInput=new XRInputButtonReader{inputSourceMode=XRInputButtonReader.InputSourceMode.ManualValue,manualPerformed=true,manualValue=1};obj.SetActive(true);manager.SelectEnter((IXRSelectInteractor)ray,target.Grab);return ray;}
  [UnityTest] public IEnumerator PhysicalCatchWaitsForRealContactThenDropsAndKeepsTruthfulReceipt(){
   var call=CatchSetup();var incoming=ball.GetComponent<Rigidbody>();Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out var run,out var error),Is.True,error);
   var attempt=editor.Find("book").GetComponent<RoomCatch>();Assert.That(attempt.Phase,Is.EqualTo("waiting"));Assert.That(incoming.isKinematic,Is.False);
   var fact=Fact("object.catch",new JObject{["target"]=editor.Identity(ball)});Assert.That((string)fact["attempts"][0]["phase"],Is.EqualTo("waiting"));
   ThrowAtCatch(new Vector3(3,1,.35f));yield return Until(()=>attempt.Caught||runtime.Scheduler.RunningCount==0);Assert.That(attempt.Caught,Is.True,runtime.Scheduler.LastError);Assert.That(incoming.isKinematic,Is.True);Assert.That(Vector3.Distance(ball.transform.position,new Vector3(3,1,.35f)),Is.LessThan(.02f));
   Assert.That(editor.Ownership.Observe().owners.Any(o=>o.role=="reflex"),Is.True);
   yield return Until(()=>runtime.Scheduler.RunningCount==0);var receipt=runtime.Scheduler.Invocation(run);Assert.That((string)receipt["phase"],Is.EqualTo("completed"),receipt.ToString());Assert.That(receipt["resources"].Values<string>(),Is.EquivalentTo(new[]{editor.Identity(ball),"book"}));Assert.That((bool)receipt["output"]["caught"],Is.True);Assert.That((bool)receipt["output"]["dropped"],Is.True);Assert.That(incoming.isKinematic,Is.False);
   Assert.That(Fact("object.catch",new JObject{["target"]=editor.Identity(ball)})["attempts"].Count(),Is.Zero);
   float height=incoming.position.y;for(int i=0;i<8;i++)yield return new WaitForFixedUpdate();Assert.That(incoming.position.y,Is.LessThan(height));Assert.That(editor.Ownership.Observe().owners.Any(o=>o.role=="reflex"),Is.False);
  }
  [UnityTest] public IEnumerator PhysicalCatchTimesOutWithoutTakingAFarObjectAndCancellationNeverPullsIt(){
   var call=CatchSetup(.2f);var body=ball.GetComponent<Rigidbody>();Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out var run,out var error),Is.True,error);yield return Until(()=>runtime.Scheduler.RunningCount==0);var receipt=runtime.Scheduler.Invocation(run);Assert.That((string)receipt["phase"],Is.EqualTo("completed"),receipt.ToString());Assert.That((bool)receipt["output"]["caught"],Is.False);Assert.That((string)receipt["output"]["phase"],Is.EqualTo("missed"));Assert.That(body.position.x,Is.EqualTo(2).Within(.02f));
   call["arguments"]["timeout"]=2;Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out run,out error),Is.True,error);runtime.Scheduler.CancelInvocation(run,out _);ThrowAtCatch(new Vector3(3,1,.35f));yield return new WaitForSeconds(.5f);Assert.That(ball.GetComponent<HeldRoomProp>(),Is.Null);Assert.That((string)runtime.Scheduler.Invocation(run)["phase"],Is.EqualTo("cancelled"));
  }
  [UnityTest] public IEnumerator PhysicalCatchSurvivesIncomingGripButUserTakesACaughtBallImmediately(){
   var call=CatchSetup(4);call["arguments"]["holdSeconds"]=2;Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out var run,out var error),Is.True,error);var ray=CatchHand(ball);Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1),"Waiting only observes the incoming prop");yield return null;
   manager.SelectExit((IXRSelectInteractor)ray,ball.Grab);ray.gameObject.SetActive(false);yield return null;ThrowAtCatch(new Vector3(3,1,.35f));var attempt=editor.Find("book").GetComponent<RoomCatch>();yield return Until(()=>attempt.Caught||runtime.Scheduler.RunningCount==0);Assert.That(attempt.Caught,Is.True,runtime.Scheduler.LastError);
   var position=ball.transform.position;ray=CatchHand(ball);Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That(Vector3.Distance(position,ball.transform.position),Is.LessThan(.02f));Assert.That(ball.GetComponent<RigidRoomItem>().AnimationOwned,Is.False);Assert.That((string)runtime.Scheduler.Invocation(run)["phase"],Is.EqualTo("cancelled"));ray.gameObject.SetActive(false);yield return null;
  }
  [UnityTest] public IEnumerator PhysicalCatchRefusesWallAndHeadCaptureAndPauseDoesNotRearm(){
   var call=CatchSetup(.35f);var centre=new Vector3(3,1,.35f);ball.transform.position=centre+Vector3.left*.1f;ball.GetComponent<RigidRoomItem>().Teleported();
   root.GetComponent<RoomInteraction>().Viewer.position=centre;Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out var run,out var error),Is.True,error);yield return Until(()=>runtime.Scheduler.RunningCount==0);Assert.That((bool)runtime.Scheduler.Invocation(run)["output"]["caught"],Is.False);
   root.GetComponent<RoomInteraction>().Viewer.position=new Vector3(-4,1.6f,0);call["arguments"]["timeout"]=2;Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out run,out error),Is.True,error);world.PausePhysics();yield return null;yield return null;Assert.That(runtime.Scheduler.RunningCount,Is.Zero);world.StartPhysics();ThrowAtCatch(centre);yield return new WaitForSeconds(.5f);Assert.That(ball.GetComponent<HeldRoomProp>(),Is.Null);
  }
  [UnityTest] public IEnumerator PhysicalCatchAvatarActuallyInterceptsAFlightOnTheShippedRig(){
   var source=BundledAvatar.FromApplication().Read();var save=editor.Models.SaveAsync(source);yield return Until(()=>save.IsCompleted);Assert.That(save.Exception,Is.Null);Assert.That(editor.SetMaestroModel(source.Hash),Is.True);yield return Until(()=>!avatar.ModelBusy);
   var call=CatchSetup(3);var shoulder=avatar.PoseRig.Bone(PoseJoint.RightUpperArm);var hand=avatar.PoseRig.Bone(PoseJoint.RightHand);var centre=shoulder.position+Vector3.forward*.3f+Vector3.down*.14f;
   call["arguments"]["holder"]=new JObject{["kind"]="avatarHand",["objectId"]="maestro",["hand"]="right",["avatarHash"]=avatar.ModelHash};call["arguments"]["offset"]=new JObject{["x"]=0,["y"]=0,["z"]=0};call["arguments"]["holdSeconds"]=1;
   var origin=centre+Vector3.forward*.75f;ball.transform.position=origin;var rigid=ball.GetComponent<RigidRoomItem>();rigid.Teleported();Assert.That(rigid.Launch(Vector3.back*(.75f/.5f)-Physics.gravity*.25f,Vector3.zero),Is.True);
   Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out var run,out var error),Is.True,error);var attempt=avatar.GetComponent<RoomCatch>();
   yield return Until(()=>attempt.Caught||runtime.Scheduler.RunningCount==0);Assert.That(attempt.Caught,Is.True,runtime.Scheduler.Invocation(run)+"; "+attempt.Reason);Assert.That(Vector3.Distance(origin,ball.transform.position),Is.GreaterThan(.3f),"Capture must follow the actual incoming flight, not pull from its distant origin");Assert.That(ball.GetComponent<Rigidbody>().isKinematic,Is.True);runtime.Scheduler.CancelInvocation(run,out _);
  }
  [UnityTest] public IEnumerator PhysicalCatchClearanceChecksThinWallsControllersAndQuerySaturation(){
   CatchSetup();var holder=editor.Find("book");var view=root.GetComponent<RoomInteraction>().Viewer;var check=new CatchClearance();var a=new Vector3(2,1,.35f);var b=new Vector3(3,1,.35f);
   Assert.That(check.Segment(ball,holder,view,a,b,.04f),Is.True);
   var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform,false);wall.transform.position=(a+b)/2;wall.transform.localScale=new Vector3(.01f,1,1);wall.layer=RoomPhysicsLayers.Scanned;
   Assert.That(check.Segment(ball,holder,view,a,b,.04f),Is.False);wall.layer=RoomPhysicsLayers.Controller;Assert.That(check.Segment(ball,holder,view,a,b,.04f),Is.False);wall.SetActive(false);
   for(int i=0;i<49;i++){var obstacle=GameObject.CreatePrimitive(PrimitiveType.Sphere);obstacle.transform.SetParent(root.transform,false);obstacle.transform.position=b;obstacle.transform.localScale=Vector3.one*.01f;obstacle.layer=RoomPhysicsLayers.Item;}
   Assert.That(check.Segment(ball,holder,view,b,b,.04f),Is.False);yield return null;
  }
  [UnityTest] public IEnumerator PhysicalCatchRejectsOverspeedAndLegacyReservationButRunsAsAReactiveProgram(){
   var call=CatchSetup(.2f);call["arguments"]["maxSpeed"]=.1;ThrowAtCatch(new Vector3(3,1,.35f));Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out var run,out var error),Is.True,error);yield return Until(()=>runtime.Scheduler.RunningCount==0);Assert.That((bool)runtime.Scheduler.Invocation(run)["output"]["caught"],Is.False);
   var source=JObject.Parse(BehaviourProgram.FromInvocation(call));Assert.That((int)source["version"],Is.EqualTo(3));var legacy=(JObject)source.DeepClone();legacy["version"]=2;legacy.Remove("state");legacy.Remove("events");Assert.That(BehaviourProgram.TryParse(legacy.ToString(),out _,out error),Is.False);Assert.That(error,Does.Contain("version 3"));
   var sequence=new RuleSequence{id=Guid.NewGuid().ToString("N"),name="Catch attempt",program=source.ToString()};runtime.Scheduler.Configure(new RuleDocument{sequences=new[]{sequence}});Assert.That(runtime.Scheduler.Trigger(sequence.id,Time.unscaledTime),Is.True,runtime.Scheduler.LastError);yield return Until(()=>runtime.Scheduler.RunningCount==0);Assert.That(runtime.Scheduler.Outcomes.Last().phase,Is.EqualTo("completed"));
  }
  [UnityTest] public IEnumerator PhysicalCatchModelReplacementAndHolderGripCancelWithoutReplay(){
   var call=CatchSetup();var ray=CatchHand(editor.Find("book"));Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out _,out _),Is.False);manager.SelectExit((IXRSelectInteractor)ray,editor.Find("book").Grab);ray.gameObject.SetActive(false);yield return null;
   call["arguments"]["holder"]["revision"]=editor.ObjectRevision("book");Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out var run,out var error),Is.True,error);ray=CatchHand(editor.Find("book"));Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That((string)runtime.Scheduler.Invocation(run)["phase"],Is.EqualTo("cancelled"));ray.gameObject.SetActive(false);yield return null;
   string robot=CreateRobot();call["arguments"]["holder"]=new JObject{["kind"]="recipePart",["objectId"]=robot,["part"]="RightHand",["revision"]=editor.ObjectRevision(robot)};Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out run,out error),Is.True,error);
   var recipe=editor.Find(robot).GetComponent<RecipeObject>();var changed=editor.Read(robot).recipe.Copy();changed.loop=!changed.loop;recipe.Apply(changed);yield return Until(()=>runtime.Scheduler.RunningCount==0);Assert.That((string)runtime.Scheduler.Invocation(run)["phase"],Is.EqualTo("failed"));Assert.That(ball.GetComponent<RigidRoomItem>().AnimationOwned,Is.False);
  }
  [UnityTest] public IEnumerator PhysicalCatchPublishesOneTypedContactThroughTheSharedEventScheduler(){
   var call=CatchSetup();var source=System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-physical-catch.json"));var sequence=new RuleSequence{id=Guid.NewGuid().ToString("N"),name="Observe physical catch",program=source};runtime.Scheduler.Configure(new RuleDocument{sequences=new[]{sequence}});Assert.That(runtime.Scheduler.Trigger(sequence.id,Time.unscaledTime),Is.True,runtime.Scheduler.LastError);yield return Until(()=>runtime.Scheduler.IsListening("object.caught",editor.Identity(ball)));
   int count=0;editor.ItemCaught+=(id,holder,part,point,speed)=>{count++;Assert.That(id,Is.EqualTo(editor.Identity(ball)));Assert.That(holder,Is.EqualTo("book"));Assert.That(speed,Is.GreaterThan(0));};
   Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out var run,out var error),Is.True,error);ThrowAtCatch(new Vector3(3,1,.35f));yield return Until(()=>runtime.Scheduler.ObserveRuns().Any(r=>r.sequenceId==sequence.id&&r.nodeId=="hold"));var observed=runtime.Scheduler.ObserveRuns().Single(r=>r.sequenceId==sequence.id);Assert.That(observed.state.Single(v=>v.name=="caughtBy").value,Is.EqualTo("book"));Assert.That(count,Is.EqualTo(1));runtime.Scheduler.CancelInvocation(run,out _);runtime.StopAll();yield return null;Assert.That(count,Is.EqualTo(1));
  }
  [UnityTest] public IEnumerator PhysicalCatchShippedMeshyRigUsesVisibleBones(){
   var source=BundledAvatar.FromApplication().Read();var save=editor.Models.SaveAsync(source);yield return Until(()=>save.IsCompleted);Assert.That(save.Exception,Is.Null);Assert.That(editor.SetMaestroModel(source.Hash),Is.True);yield return Until(()=>!avatar.ModelBusy);Assert.That(avatar.CustomModel,Is.Not.Null,avatar.ModelStatus);
   yield return PhysicalCatchAvatarReachesWithoutStretchingAndRestoresOnStop();
  }
  [UnityTest] public IEnumerator PhysicalCatchAvatarReachesWithoutStretchingAndRestoresOnStop(){
   var call=CatchSetup(3);var rig=avatar.PoseRig;var upper=rig.Bone(PoseJoint.RightUpperArm);var lower=rig.Bone(PoseJoint.RightLowerArm);var hand=rig.Bone(PoseJoint.RightHand);float first=Vector3.Distance(upper.position,lower.position),second=Vector3.Distance(lower.position,hand.position);
   var observation=avatar.gameObject.AddComponent<CatchPoseObservation>();observation.Rig=rig;
   var before=rig.Capture();var start=hand.position;var goal=(upper.position+lower.position)*.5f+Vector3.forward*.24f;
   ball.transform.position=goal+Vector3.forward*.18f;ball.GetComponent<RigidRoomItem>().Teleported();ball.GetComponent<Rigidbody>().useGravity=false;
   call["arguments"]["holder"]=new JObject{["kind"]="avatarHand",["objectId"]="maestro",["hand"]="right",["avatarHash"]=avatar.ModelHash??""};call["arguments"]["offset"]=JObject.Parse(JsonUtility.ToJson(Vector3.forward*.18f));call["arguments"]["holdSeconds"]=2;call["arguments"]["gripRadius"]=.02;
   Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out var run,out var error),Is.True,error);for(int i=0;i<20;i++)yield return null;
   Assert.That(Vector3.Distance(observation.Hand,start),Is.GreaterThan(.02f),runtime.Scheduler.LastError+"; "+avatar.GetComponent<RoomCatch>()?.Reason+"; start "+start+" goal "+goal);Assert.That(observation.UpperLength,Is.EqualTo(first).Within(.002f));Assert.That(observation.LowerLength,Is.EqualTo(second).Within(.002f));
   runtime.Scheduler.CancelInvocation(run,out _);Assert.That(avatar.GetComponent<AvatarCatchReach>(),Is.Not.Null);yield return null;Assert.That(avatar.GetComponent<AvatarCatchReach>(),Is.Null);Assert.That(ball.GetComponent<RigidRoomItem>().AnimationOwned,Is.False);
  }
 }
}
