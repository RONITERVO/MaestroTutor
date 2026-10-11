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
namespace Maestro.Quest.Tests
{
    public sealed partial class AvatarSpatialTests
    {
        JObject SimulationFact()
        {
            Assert.That(BehaviourCatalog.TryRead("physics.simulation",1,null,new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True);
            Assert.That(value.Characters,Is.LessThanOrEqualTo(1024));return JObject.FromObject(value.Value);
        }
        JObject SimulationRequest(string operation)=>new() {["operation"]="start",["runId"]=modeActions.Observe()["nextRunId"].DeepClone(),["call"]=new JObject {["id"]="physics.simulation.set",["version"]=1,["arguments"]=new JObject {["operation"]=operation,["stateId"]=SimulationFact()["stateId"].DeepClone()}}};
        IEnumerator ChangeSimulation(string operation)
        {
            Assert.That(modeActions.Execute(SimulationRequest(operation),out var error),Is.True,error);yield return null;
            Assert.That((string)modeActions.Observe()["selected"]["phase"],Is.EqualTo("completed"),modeActions.Observe().ToString());
        }
        [UnityTest] public IEnumerator SharedSimulationDrivesGravityAndPauseWithoutSavingOrReplayingOldThrows()
        {
            SharedModes(out _,out _);world.PausePhysics();
            Assert.That(RoomControls.Capabilities(editor),Does.Contain("physicsSimulation.v1"));
            string id=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block).id;var item=editor.Find(id);var rigid=item.GetComponent<RigidRoomItem>();var body=item.GetComponent<Rigidbody>();
            Assert.That(editor.SetItemPhysics(id,new ObjectPhysicsSettings {mode="solid",shape="box",mass=.5f}),Is.True);var before=SimulationFact();int revision=editor.Revision;
            item.transform.position=new Vector3(1,2,1);rigid.Teleported();Physics.SyncTransforms();Assert.That(body.isKinematic,Is.True);
            var startRequest=SimulationRequest("start");Assert.That(modeActions.Execute(startRequest,out var error),Is.True,error);Assert.That(editor.Revision,Is.EqualTo(revision),"The transition does not edit the room document");yield return null;var start=modeActions.Observe().DeepClone();
            Assert.That((string)start["selected"]["phase"],Is.EqualTo("completed"));Assert.That(body.useGravity&&!body.isKinematic,Is.True);
            yield return new WaitForSeconds(.15f);Assert.That(body.position.y,Is.LessThan(1.95f));Assert.That(rigid.Launch(Vector3.right*3,Vector3.up),Is.True);
            yield return ChangeSimulation("pause");var pause=modeActions.Observe().DeepClone();var after=SimulationFact();var position=body.position;
            Assert.That(body.isKinematic,Is.True);yield return new WaitForSeconds(.1f);Assert.That(Vector3.Distance(position,body.position),Is.LessThan(.001f));
            Assert.That(modeActions.Execute(startRequest,out error),Is.True,error);Assert.That(world.Running,Is.False,"Completed start receipt must not run again after Pause");Assert.That(JToken.DeepEquals(after,SimulationFact()),Is.True);
            yield return ChangeSimulation("start");Assert.That(Mathf.Abs(body.linearVelocity.x),Is.LessThan(.001f),"Resuming gravity must not replay the old throw");
            var current=SimulationFact();int notifications=0;Action changed=()=>notifications++;world.Changed+=changed;yield return ChangeSimulation("start");world.Changed-=changed;Assert.That(notifications,Is.Zero,"Already satisfied shared starts do not reset motion observations");Assert.That(JToken.DeepEquals(current,SimulationFact()),Is.True);
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_PHYSICS_SIMULATION");if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"simulation.json"),new JObject {["before"]=before,["start"]=start,["pause"]=pause,["after"]=after}.ToString());}
        }
        [UnityTest] public IEnumerator SharedSimulationRejectsStaleIntentAfterManualReversalScanAndLifecycleRecovery()
        {
            SharedModes(out _,out _);world.PausePhysics();
            Action[] changes={world.PausePhysics,()=>{world.StartPhysics();world.PausePhysics();},()=>{world.SetSurfaces(false,"Scan changed");world.SetSurfaces(true,"Aligned again");},()=>{world.SendMessage("OnApplicationFocus",false);world.SendMessage("OnApplicationFocus",true);},()=>{world.SendMessage("OnApplicationPause",true);world.SendMessage("OnApplicationPause",false);},()=>{world.enabled=false;world.enabled=true;}};
            foreach(var change in changes){var stale=SimulationRequest("start");change();var current=SimulationFact();Assert.That(modeActions.Execute(stale,out var error),Is.False);StringAssert.Contains("changed",error);Assert.That(JToken.DeepEquals(current,SimulationFact()),Is.True);Assert.That(world.Running,Is.False);}
            string notified=null;Action observe=()=>notified=(string)SimulationFact()["stateId"];world.Changed+=observe;world.PausePhysics();world.Changed-=observe;Assert.That(notified,Is.EqualTo((string)SimulationFact()["stateId"]));yield return null;
        }
        [UnityTest] public IEnumerator SharedSimulationReadsReadinessWithoutMutatingAndRespectsWorkspaceAndRuntimeHolds()
        {
            SharedModes(out _,out _);world.SetSurfaces(false,new string('x',1024)+"🧪");var unavailable=SimulationFact();Assert.That((bool)unavailable["canStart"],Is.False);Assert.That((string)unavailable["status"],Has.Length.LessThanOrEqualTo(128));
            Assert.That(modeActions.Execute(SimulationRequest("start"),out _),Is.False);Assert.That(JToken.DeepEquals(unavailable,SimulationFact()),Is.True);
            world.SetSurfaces(true,"Aligned");var before=SimulationFact();using(editor.WriteGate.TryFreeze(out var error)){Assert.That(error,Is.Null);Assert.That(modeActions.Execute(SimulationRequest("start"),out _),Is.False);Assert.That(JToken.DeepEquals(before,SimulationFact()),Is.True);world.PausePhysics();}
            var stale=SimulationRequest("start");using(editor.RuntimeGate.Hold("Review room content first")){Assert.That((bool)SimulationFact()["held"],Is.True);Assert.That((bool)SimulationFact()["canStart"],Is.False);Assert.That(modeActions.Execute(SimulationRequest("start"),out _),Is.False);}
            Assert.That(world.Running,Is.False);Assert.That((bool)SimulationFact()["held"],Is.False);Assert.That(modeActions.Execute(stale,out _),Is.False);
            world.SendMessage("OnApplicationFocus",false);Assert.That((bool)SimulationFact()["active"],Is.False);Assert.That((bool)SimulationFact()["canStart"],Is.False);Assert.That(modeActions.Execute(SimulationRequest("start"),out _),Is.False);world.SendMessage("OnApplicationFocus",true);Assert.That(world.Running,Is.False);
            yield return ChangeSimulation("start");
        }
        [UnityTest] public IEnumerator SharedSimulationCooperatesWithOtherActorsAndPauseStopsOnlyPhysicsDependentMotion()
        {
            SharedModes(out _,out _);world.PausePhysics();
            var wave=new JObject {["id"]="animation.play",["version"]=1,["arguments"]=new JObject {["target"]="maestro",["seconds"]=10,["source"]=new JObject {["kind"]="gesture",["gesture"]="greeting"},["channel"]="upperBody"}};
            Assert.That(modeRules.Scheduler.Invoke(wave,Time.unscaledTime,out var waving,out var error),Is.True,error);
            var wait=new JObject {["id"]="time.wait",["version"]=1,["arguments"]=new JObject {["seconds"]=20}};Assert.That(modeRules.Scheduler.Invoke(wait,Time.unscaledTime,out var waiting,out error),Is.True,error);
            var item=editor.Find(editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block).id);var rigid=item.GetComponent<RigidRoomItem>();rigid.Configure(world,ItemPhysics.Solid,.5f);var owner=new object();rigid.SetAnimationOwner(owner,true);
            yield return ChangeSimulation("start");Assert.That(avatar.UpperBodyOwnedBy(waving),Is.True);Assert.That(rigid.AnimationOwned,Is.True);Assert.That(item.GetComponent<Rigidbody>().isKinematic,Is.True);
            Assert.That(motion.Begin("direct follow",AvatarSpatialMode.Follow,out error),Is.True,error);yield return null;Assert.That(motion.Active,Is.True);
            yield return ChangeSimulation("pause");yield return null;Assert.That(motion.Active,Is.False);Assert.That(avatar.UpperBodyOwnedBy(waving),Is.True);Assert.That((string)modeRules.Scheduler.Invocation(waiting)["phase"],Is.EqualTo("running"));
            yield return ChangeSimulation("start");Assert.That(motion.Active,Is.False,"Physics start must not resume a stopped owner");Assert.That(rigid.AnimationOwned,Is.True);rigid.SetAnimationOwner(owner,false);Assert.That(item.GetComponent<Rigidbody>().isKinematic,Is.False);
        }
        [UnityTest] public IEnumerator ProgramCanReadFreshSimulationStateAtEachInvocationWithoutASecondControlPath()
        {
            SharedModes(out _,out _);world.PausePhysics();var source=JObject.Parse(BehaviourProgram.FromInvocation((JObject)SimulationRequest("start")["call"]));source["version"]=3;source["dataVersion"]=1;source["state"]=new JArray();source["events"]=new JArray();var body=(JArray)source["functions"][0]["body"];
            body[0]["bindings"]=new JObject {["stateId"]=new JObject {["op"]="field",["args"]=new JArray(new JObject {["fact"]="physics.simulation"},new JObject {["value"]="stateId"})}};
            var pause=body[0].DeepClone();pause["id"]="pause";pause["arguments"]["operation"]="pause";
            var wait=JObject.Parse(BehaviourProgram.FromInvocation(new JObject {["id"]="time.wait",["version"]=1,["arguments"]=new JObject {["seconds"]=.1}}))["functions"][0]["body"][0];wait["id"]="wait";body.Add(wait);body.Add(pause);
            var sequence=new RuleSequence {id=Guid.NewGuid().ToString("N"),name="Start wait pause",program=source.ToString()};modeRules.Scheduler.Configure(new RuleDocument {sequences=new[]{sequence}});
            Assert.That(world.Running,Is.False,"Defining a program is not running it");Assert.That(modeRules.Scheduler.Trigger(sequence.id,Time.unscaledTime),Is.True,modeRules.Scheduler.LastError);Assert.That(world.Running,Is.True);
            float until=Time.unscaledTime+2;while(modeRules.Scheduler.RunningCount>0&&Time.unscaledTime<until)yield return null;
            Assert.That(modeRules.Scheduler.RunningCount,Is.Zero);Assert.That(modeRules.Scheduler.Outcomes.Last().phase,Is.EqualTo("completed"),modeRules.Scheduler.LastError);Assert.That(world.Running,Is.False);
        }
    }
}
