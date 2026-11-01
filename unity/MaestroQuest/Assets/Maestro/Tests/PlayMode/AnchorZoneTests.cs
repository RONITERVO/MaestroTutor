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
namespace Maestro.Quest.Tests
{
    public sealed partial class AvatarPropTests
    {
        JObject ZoneProgram(string robot){
            var program=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-anchor-zone.json")));
            program["resources"]=new JArray(editor.Identity(ball),robot);
            var wait=program["functions"][0]["body"][0]["arguments"];var hold=program["functions"][0]["body"][1]["then"][0]["arguments"];
            foreach(var args in new[]{wait,hold}){args["target"]=editor.Identity(ball);args["holder"]["objectId"]=robot;args["holder"]["revision"]=editor.ObjectRevision(robot);}return program;
        }
        [UnityTest] public IEnumerator AnchorZoneProgramPicksUpARealFlyingBallAndDropsItBackToTheFloor(){
            Time.captureDeltaTime=0;string robot=CreateRobot(),target=editor.Identity(ball);var hand=editor.Find(robot).GetComponent<RecipeObject>().Part("RightHand");var centre=hand.position+hand.rotation*Vector3.forward*.35f;
            ball.transform.position=centre+Vector3.forward*.55f+Vector3.up*.08f;ball.GetComponent<RigidRoomItem>().Teleported();editor.RememberPlacement(target);
            world.SetSurfaces(true,"Synthetic aligned room");world.StartPhysics();Assert.That(world.Running,Is.True);var program=ZoneProgram(robot);var before=JsonUtility.ToJson(editor.Read(robot));
            var sequence=new RuleSequence {id="",name="Pick up nearby ball",program=program.ToString(Newtonsoft.Json.Formatting.None)};
            var request=new RoomAgentRequest {version=2,commands=new[]{new RoomAgentCommand {action="rules",rule=new RuleRequest {
                action="edit",revision=rules.Revision,edits=new[]{new RuleEdit {kind="save",reference="catch",sequence=sequence}}
            }}}};
            var executor=new RoomAgentExecutor(editor);Assert.That(executor.Execute(request,out var error,out var ids),Is.True,error);
            var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);var capture=new JObject {["program"]=program};void Capture(string phase){var state=observer.Observe();state.visible=true;state.workspaceView="rules";state.rules=rules.Observe(true);capture[phase]=JObject.Parse(RoomAgentWire.Serialize(state));}
            Capture("saved");Assert.That(runtime.Trigger(ids.Single()),Is.True,runtime.Scheduler.LastError);Capture("watching");Assert.That(editor.Ownership.Observe().owners.Any(x=>x.role!="ambient"),Is.False);
            Assert.That(ball.GetComponent<RigidRoomItem>().Launch(Vector3.back*2.5f,Vector3.zero),Is.True);yield return Until(()=>ball.GetComponent<HeldRoomProp>()?.Holding==true||runtime.Scheduler.RunningCount==0);
            var carried=ball.GetComponent<HeldRoomProp>();Assert.That(carried&&carried.Holding,Is.True,runtime.Scheduler.LastError);Assert.That(ball.GetComponent<Rigidbody>().isKinematic,Is.True);Capture("holding");
            Assert.That(Vector3.Distance(ball.transform.position,centre),Is.LessThan(.01f));yield return Until(()=>runtime.Scheduler.RunningCount==0);Assert.That(runtime.Scheduler.Outcomes.Last().phase,Is.EqualTo("completed"),runtime.Scheduler.LastError);Capture("completed");
            Assert.That(ball.GetComponent<RigidRoomItem>().AnimationOwned,Is.False);bool falling=false;for(int i=0;i<120;i++){yield return new WaitForFixedUpdate();falling|=ball.GetComponent<Rigidbody>().linearVelocity.y<-.2f;Assert.That(ball.transform.position.y,Is.GreaterThan(.04f));}Assert.That(falling,Is.True);Assert.That(JsonUtility.ToJson(editor.Read(robot)),Is.EqualTo(before));
            string folder=Environment.GetEnvironmentVariable("MAESTRO_ANCHOR_ZONE_EVIDENCE");if(!string.IsNullOrEmpty(folder)){Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"anchor-zone.json"),capture.ToString());}
        }
        [UnityTest] public IEnumerator PickupRechecksReachAndFreePhysicsBeforeChangingOwnershipOrPlacement(){
            string robot=CreateRobot();FitRobotBall(robot);world.SetSurfaces(true,"Synthetic aligned room");world.StartPhysics();var call=HoldCall(robot);call["arguments"]["reach"]=new JObject {["radius"]=.2,["physics"]=true};
            Assert.That(BehaviourCatalog.TryCall((string)call["id"],1,(JObject)call["arguments"],out var compiled,out var error),Is.True,error);var actions=new RoomRuleActions(editor,root.GetComponent<AnimationWorkshop>());Assert.That(actions.CanRun(compiled,out error),Is.True,error);
            ball.transform.position+=Vector3.forward*2;ball.GetComponent<RigidRoomItem>().Teleported();var far=ball.transform.position;
            Assert.That(actions.Start("late-pickup",compiled,out _,out error),Is.False);Assert.That(error,Does.Contain("reach"));Assert.That(ball.transform.position,Is.EqualTo(far));Assert.That(ball.GetComponent<HeldRoomProp>(),Is.Null);Assert.That(ball.GetComponent<RigidRoomItem>().AnimationOwned,Is.False);
            FitRobotBall(robot);world.PausePhysics();Assert.That(actions.CanRun(compiled,out error),Is.False);Assert.That(error,Does.Contain("simulating"));world.StartPhysics();Assert.That(actions.CanRun(compiled,out error),Is.True,error);
            Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out var id,out error),Is.True,error);yield return null;Assert.That(ball.GetComponent<HeldRoomProp>()?.Holding,Is.True);runtime.Scheduler.CancelInvocation(id,out _);yield return null;
        }
        [UnityTest] public IEnumerator ProbeTracksPartMotionAndExactAvatarAndRootAnchorsButRefusesReplacedSockets(){
            string robot=CreateRobot();var args=(JObject)HoldCall(robot)["arguments"];var actions=new RoomRuleActions(editor,root.GetComponent<AnimationWorkshop>());
            using var probe=actions.OpenAnchorProbe(editor.Identity(ball),(JObject)args["holder"],new Vector3(0,0,.35f),false);Assert.That(probe.Read(out var before,out var error),Is.True,error);
            var recipe=editor.Find(robot).GetComponent<RecipeObject>();recipe.Part("RightHand").localPosition+=Vector3.up*.25f;Assert.That(probe.Read(out var moved,out error),Is.True,error);Assert.That(Vector3.Distance(before.Centre,moved.Centre),Is.GreaterThan(.2f));
            foreach(var holder in new[]{new JObject {["kind"]="object",["objectId"]="book",["revision"]=editor.ObjectRevision("book")},new JObject {["kind"]="avatarHand",["objectId"]="maestro",["hand"]="right",["avatarHash"]=""}}){using var other=actions.OpenAnchorProbe(editor.Identity(ball),holder,Vector3.forward*.2f,false);Assert.That(other.Read(out var sample,out error),Is.True,error);var pose=Fact("object.anchor",new JObject {["holder"]=holder.DeepClone()});Assert.That(Vector3.Distance(sample.Centre,pose["position"].ToObject<Vector3>()+pose["rotation"].ToObject<Quaternion>()*(Vector3.forward*.2f*(float)pose["scale"])),Is.LessThan(.001f));}
            var replacement=editor.Read(robot).recipe.Copy();replacement.loop=!replacement.loop;Assert.That(recipe.Apply(replacement),Is.True);yield return null;Assert.That(probe.Read(out _,out error),Is.False);Assert.That(error,Is.Not.Empty);
        }
        [UnityTest] public IEnumerator RememberingNormalBookAndAvatarTravelDoesNotResetAnAnchorWatch(){
            var actions=new RoomRuleActions(editor,root.GetComponent<AnimationWorkshop>());
            foreach(var holder in new[]{new JObject {["kind"]="object",["objectId"]="book",["revision"]=editor.ObjectRevision("book")},new JObject {["kind"]="avatarHand",["objectId"]="maestro",["hand"]="right",["avatarHash"]=""}}){
                var pose=Fact("object.anchor",new JObject {["holder"]=holder.DeepClone()});var offset=Vector3.forward*.35f;var centre=pose["position"].ToObject<Vector3>()+pose["rotation"].ToObject<Quaternion>()*(offset*(float)pose["scale"]);
                ball.transform.position=centre+Vector3.right*.5f;ball.GetComponent<RigidRoomItem>().Teleported();
                var args=(JObject)BehaviourCatalog.Event("object.anchor.proximity.changed").ToJson()["example"].DeepClone();args["target"]=editor.Identity(ball);args["holder"]=holder;args["offset"]=new JObject {["x"]=offset.x,["y"]=offset.y,["z"]=offset.z};args["radius"]=.2;args["physics"]=false;
                using var watch=new AnchorProximitySubscription(actions,args,0);string id=(string)holder["objectId"];int before=editor.ObjectRevision(id);editor.Find(id).transform.position+=Vector3.right*.4f;editor.RememberPlacement(id);Assert.That(editor.ObjectRevision(id),Is.GreaterThan(before));
                Assert.That(watch.Poll(.06f,out _,out var fields,out var error),Is.True,error);Assert.That((bool)fields["inside"],Is.True);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator PausedPhysicsAndLifecycleDoNotProduceOrReplayZoneActions(){
            string robot=CreateRobot();FitRobotBall(robot);world.SetSurfaces(true,"Synthetic aligned room");world.StartPhysics();var args=(JObject)ZoneProgram(robot)["functions"][0]["body"][0]["arguments"];args["initial"]="report";var actions=new RoomRuleActions(editor,root.GetComponent<AnimationWorkshop>());
            using var watch=new AnchorProximitySubscription(actions,args,0);world.PausePhysics();Assert.That(watch.Poll(.06f,out _,out _,out var error),Is.False);Assert.That(error,Is.Null);world.StartPhysics();Assert.That(watch.Poll(.12f,out _,out var fields,out error),Is.True,error);Assert.That((bool)fields["inside"],Is.True);
            watch.Dispose();Assert.That(watch.Poll(1,out _,out _,out _),Is.False);
            var p=ZoneProgram(robot);var sequence=new RuleSequence {id=Guid.NewGuid().ToString("N"),name="Watch",program=p.ToString()};runtime.Scheduler.Configure(new RuleDocument {sequences=new[]{sequence}});Assert.That(runtime.Trigger(sequence.id),Is.True);runtime.SendMessage("OnApplicationPause",true);runtime.SendMessage("OnApplicationPause",false);yield return null;Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That(ball.GetComponent<HeldRoomProp>(),Is.Null);
        }
    }
}
