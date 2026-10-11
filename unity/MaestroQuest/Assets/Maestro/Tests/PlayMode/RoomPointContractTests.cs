// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Reflection;
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
    public sealed partial class RoomRulesTests
    {
        [UnityTest] public IEnumerator RoomPointFactsUseTheAuthoredFrameAndCanDriveLaterPlacement()
        {
            string id=editor.Identity(block);FrameGroup(id);
            var expected=new Vector3(.4f,1.3f,.9f);block.transform.position=editor.Frame.PointToWorld(expected);
            var actions=new RoomRuleActions(editor,animations);
            Assert.That(actions.TryRead("object.position",2,new JObject {["target"]=id},out var fact),Is.True);
            var point=((JObject)fact.Value).ToObject<Vector3>();FrameNear(point,expected);
            var motion=new RoomWorldMotion(root.transform,physics);
            Assert.That(motion.SetPose(new Vector3(-6,0,8),Quaternion.Euler(0,-47,0),out var error),Is.True,error);
            Assert.That(actions.TryPosition(id,out var physical),Is.True);FrameNear(physical,editor.Frame.PointToWorld(expected));
            Assert.That(BehaviourCatalog.TryCall("object.position.set",1,new JObject {["target"]="book",["x"]=point.x,["y"]=point.y,["z"]=point.z},out var call,out error),Is.True,error);
            Assert.That(actions.Start("later placement",call,out _,out error),Is.True,error);
            Assert.That(actions.Complete("later placement",out error),Is.True,error);FrameNear(editor.Find("book").transform.position,physical);
            root.transform.localScale=new Vector3(1,2,1);
            Assert.That(actions.TryRead("object.position",2,new JObject {["target"]=id},out _),Is.False);
            root.transform.localScale=Vector3.one;yield return null;
        }
        [TestCase("object.collided","Collided")] [TestCase("object.caught","Caught")]
        public void RoomPointQueuedContactsStillPlaceAtTheCapturedLocationAfterWorldMovement(string eventId,string callback)
        {
            var roomPoint=new Vector3(2,1.3f,-1);
            root.transform.SetPositionAndRotation(new Vector3(6,0,-3),Quaternion.Euler(0,65,0));
            var p=new JObject {
                ["version"]=3,["entry"]="main",["resources"]=new JArray("book"),["state"]=new JArray(),["events"]=new JArray(),
                ["functions"]=new JArray(new JObject {["name"]="main",["returns"]="void",["parameters"]=new JArray(),
                    ["locals"]=new JArray(new JObject {["name"]="got",["initial"]=false},new JObject {["name"]="source",["initial"]=""},new JObject {["name"]="x",["initial"]=0},new JObject {["name"]="y",["initial"]=0},new JObject {["name"]="z",["initial"]=0}),
                    ["body"]=new JArray(
                        new JObject {["id"]="watch",["op"]="awaitEvent",["event"]=eventId,["version"]=2,["source"]="book",["timeout"]=new JObject {["value"]=0},["received"]="got",["value"]="source",["fields"]=new JObject {["x"]="x",["y"]="y",["z"]="z"}},
                        new JObject {["id"]="delay",["op"]="sleep",["seconds"]=new JObject {["value"]=.2}},
                        new JObject {["id"]="place",["op"]="invoke",["capability"]="object.position.set",["version"]=1,["arguments"]=new JObject {["target"]="book",["x"]=0,["y"]=0,["z"]=0},["bindings"]=new JObject {["x"]=new JObject {["var"]="x"},["y"]=new JObject {["var"]="y"},["z"]=new JObject {["var"]="z"}}}
                    )})};
            var sequence=new RuleSequence {id=Guid.NewGuid().ToString("N"),name="Use queued contact",program=p.ToString()};
            runtime.Scheduler.Configure(new RuleDocument {sequences=new[]{sequence}});float now=Time.unscaledTime;
            Assert.That(runtime.Scheduler.Trigger(sequence.id,now),Is.True,runtime.Scheduler.LastError);
            // Enter at the real native event adapter; the scheduler has not consumed the point yet.
            typeof(RoomRules).GetMethod(callback,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(runtime,
                new object[]{"book",callback=="Caught"?"maestro":"",callback=="Caught"?"right":"environment",editor.Frame.PointToWorld(roomPoint),.5f});
            var motion=new RoomWorldMotion(root.transform,physics);
            Assert.That(motion.SetPose(new Vector3(-4,0,7),Quaternion.Euler(0,-35,0),out var error),Is.True,error);
            runtime.Scheduler.Tick(now+.01f);Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));
            runtime.Scheduler.Tick(now+.25f);runtime.Scheduler.Tick(now+.3f);
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero,runtime.Scheduler.LastError);
            FrameNear(editor.Find("book").transform.position,editor.Frame.PointToWorld(roomPoint));
        }
        [UnityTest] public IEnumerator RoomPointImpulseUsesRoomAxesInsteadOfNestedParentAxes()
        {
            string id=editor.Identity(block);FrameGroup(id);
            Assert.That(editor.SetItemPhysics(id,new ObjectPhysicsSettings {mode="solid",shape="box",mass=1}),Is.True);
            physics.SetSurfaces(true,"Synthetic aligned room");physics.StartPhysics();
            var args=new JObject {["target"]=id,["x"]=1,["y"]=0,["z"]=0};
            Assert.That(BehaviourCatalog.TryCall("object.physics.impulse",1,args,out var call,out var error),Is.True,error);
            var actions=new RoomRuleActions(editor,animations);
            Assert.That(actions.Start("room push",call,out _,out error),Is.True,error);
            Assert.That(actions.Complete("room push",out error),Is.True,error);
            yield return new WaitForFixedUpdate();
            var velocity=editor.Frame.DirectionToRoom(block.GetComponent<Rigidbody>().linearVelocity);
            Assert.That(velocity.x,Is.GreaterThan(.8f));Assert.That(Mathf.Abs(velocity.z),Is.LessThan(.01f));
            physics.PausePhysics();
        }
    }
    public sealed partial class AvatarPropTests
    {
        static void RoomPointNear(Vector3 actual,Vector3 expected)=>Assert.That(Vector3.Distance(actual,expected),Is.LessThan(.001f));
        [UnityTest] public IEnumerator RoomPointAnchorPartAndWatchAgreeAcrossContentMovement()
        {
            string robot=CreateRobot();var holder=new JObject {["kind"]="recipePart",["objectId"]=robot,["part"]="RightHand",["revision"]=editor.ObjectRevision(robot)};
            var group=new GameObject("Nested recipe region").transform;group.SetParent(root.transform,false);group.SetLocalPositionAndRotation(new Vector3(.2f,0,-.5f),Quaternion.Euler(0,32,0));group.localScale=Vector3.one*1.3f;editor.Find(robot).transform.SetParent(group,true);
            var actions=new RoomRuleActions(editor,root.GetComponent<AnimationWorkshop>());
            using var probe=actions.OpenAnchorProbe(editor.Identity(ball),holder,Vector3.forward*.2f,false);
            var before=Fact("object.anchor",new JObject {["holder"]=holder});Assert.That(probe.Read(out var initial,out var error),Is.True,error);
            var motion=new RoomWorldMotion(root.transform,world);
            Assert.That(motion.SetPose(new Vector3(4,0,7),Quaternion.Euler(0,81,0),out error),Is.True,error);
            var after=Fact("object.anchor",new JObject {["holder"]=holder});
            RoomPointNear(after["position"].ToObject<Vector3>(),before["position"].ToObject<Vector3>());
            Assert.That(Quaternion.Angle(after["rotation"].ToObject<Quaternion>(),before["rotation"].ToObject<Quaternion>()),Is.LessThan(.01f));
            Assert.That(probe.Read(out var moved,out error),Is.True,error);RoomPointNear(moved.RoomCentre,initial.RoomCentre);
            RoomPointNear(moved.Centre,editor.Frame.PointToWorld(moved.RoomCentre));
            var pose=Fact("object.recipe.pose",new JObject {["target"]=robot,["part"]="RightHand"});
            Assert.That(pose.ContainsKey("world"),Is.False);RoomPointNear(pose["room"]["position"].ToObject<Vector3>(),after["position"].ToObject<Vector3>());
            root.transform.localScale=Vector3.one*2;Assert.That(probe.Read(out _,out error),Is.False);Assert.That(error,Does.Contain("frame"));
            root.transform.localScale=Vector3.one;yield return null;
        }
        [UnityTest] public IEnumerator RoomPointDelayedThrowResolvesFreshFrameAndReportsRoomCoordinates()
        {
            var args=AimSetup(.5f);var before=AimPreview(args);Assert.That((bool)before["ready"],Is.True,before.ToString());
            // Pause only for this explicit real-collider relocation. The saved destination remains untouched.
            world.PausePhysics();var motion=new RoomWorldMotion(root.transform,world);
            Assert.That(motion.SetPose(new Vector3(4,0,-3),Quaternion.Euler(0,63,0),out var error),Is.True,error);
            world.StartPhysics();var preview=AimPreview(args);Assert.That((bool)preview["ready"],Is.True,preview.ToString());
            RoomPointNear(preview["destination"].ToObject<Vector3>(),before["destination"].ToObject<Vector3>());
            RoomPointNear(preview["velocity"].ToObject<Vector3>(),before["velocity"].ToObject<Vector3>());
            float began=Time.fixedTime;Assert.That(runtime.Scheduler.Invoke(AimCall(args),Time.unscaledTime,out var run,out error),Is.True,error);
            var body=ball.GetComponent<Rigidbody>();
            RoomPointNear(editor.Frame.DirectionToRoom(body.linearVelocity),preview["velocity"].ToObject<Vector3>());
            while(Time.fixedTime-began<(float)preview["seconds"]-.0001f)yield return new WaitForFixedUpdate();
            Assert.That(Vector3.Distance(body.worldCenterOfMass,editor.Frame.PointToWorld(preview["destination"].ToObject<Vector3>())),Is.LessThan(.09f));
            yield return null;Assert.That((string)runtime.Scheduler.Invocation(run)["phase"],Is.EqualTo("completed"));
            RoomPointNear(runtime.Scheduler.Invocation(run)["output"]["destination"].ToObject<Vector3>(),before["destination"].ToObject<Vector3>());
        }
    }
}
