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
        JObject AimSetup(float seconds=.75f){
            var view=new GameObject("Throw viewer");view.transform.SetParent(root.transform,false);view.transform.position=new Vector3(-4,1.6f,0);root.GetComponent<RoomInteraction>().Viewer=view.transform;
            world.SetSurfaces(true,"Synthetic aligned floor");world.StartPhysics();
            ball.transform.position=new Vector3(2,1,0);ball.GetComponent<RigidRoomItem>().Teleported();
            var args=BehaviourCatalog.Action("object.physics.launch").Example;args["target"]=editor.Identity(ball);args["destination"]["position"]=new JObject{["x"]=3.5,["y"]=1,["z"]=0};args["seconds"]=seconds;args["maxSpeed"]=8;return args;
        }
        static JObject AimCall(JObject args)=>new(){["id"]="object.physics.launch",["version"]=2,["arguments"]=args.DeepClone()};
        JObject AimPreview(JObject args)=>Fact("object.physics.trajectory",args);
        GameObject AimWall(Vector3 position){var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform,false);wall.layer=RoomPhysicsLayers.Scanned;wall.transform.position=position;wall.transform.localScale=new Vector3(.03f,5,3);Physics.SyncTransforms();return wall;}
        [UnityTest] public IEnumerator AimedThrowUsesRealGravityReportsLaunchAndNeverReplaysItsReceipt(){
            var args=AimSetup();var preview=AimPreview(args);Assert.That((bool)preview["ready"],Is.True,preview.ToString());
            var body=ball.GetComponent<Rigidbody>();Assert.That(body.linearVelocity.sqrMagnitude,Is.Zero,"Preview must not move the object");
            var executions=new RoomExecutions(editor);var request=new JObject{["operation"]="start",["runId"]=executions.Observe()["nextRunId"].DeepClone(),["call"]=AimCall(args)};
            var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);var capture=new JObject{["arguments"]=args,["preview"]=preview};
            var catalog=new RoomCapabilityCatalog(editor);
            void Capture(string phase,JObject query){Assert.That(catalog.Execute(query,out _),Is.True);var state=observer.Observe();state.visible=true;state.workspaceView="rules";state.rules=rules.Observe(true);state.catalog=catalog.Observe();state.execution=executions.Observe();capture[phase]=JObject.Parse(RoomAgentWire.Serialize(state));}
            Capture("search",new JObject{["operation"]="search",["query"]="object.physics.launch",["offset"]=0});
            Capture("inspect",new JObject{["operation"]="inspect",["capability"]="object.physics.launch",["version"]=2});
            Capture("previewed",new JObject{["operation"]="inspect",["category"]="facts",["capability"]="object.physics.trajectory",["version"]=2,["arguments"]=args.DeepClone()});
            Assert.That((bool)capture["previewed"]["catalog"]["value"]["ready"],Is.True,capture["previewed"].ToString());
            Assert.That(JToken.DeepEquals(capture["previewed"]["catalog"]["value"],JObject.Parse(preview.ToString())),Is.True,"The real scheduler/catalog must read the same serialized live plan: "+capture["previewed"]["catalog"]["value"]);
            Capture("ready",new JObject{["operation"]="check",["call"]=AimCall(args)});
            int revision=editor.Revision;float launchedAt=Time.fixedTime;Assert.That(executions.Execute(request,out var error),Is.True,error);Assert.That(editor.Revision,Is.EqualTo(revision));
            yield return null;Capture("launched",new JObject{["operation"]="check",["call"]=AimCall(args)});
            var receipt=executions.Observe()["selected"];Assert.That((string)receipt["phase"],Is.EqualTo("completed"),receipt.ToString());Assert.That((string)receipt["output"]["phase"],Is.EqualTo("launched"));
            float flight=(float)receipt["output"]["seconds"];var destination=receipt["output"]["destination"].ToObject<Vector3>();
            while(Time.fixedTime-launchedAt<flight-.0001f)yield return new WaitForFixedUpdate();
            Assert.That(Vector3.Distance(body.worldCenterOfMass,destination),Is.LessThan(.09f),$"Predicted target {destination}; actual {body.worldCenterOfMass}");
            bool fell=false,bounced=false;for(int i=0;i<100;i++){yield return new WaitForFixedUpdate();fell|=body.linearVelocity.y<-.5f;bounced|=fell&&body.linearVelocity.y>.3f;Assert.That(body.position.y,Is.GreaterThan(.04f));}Assert.That(bounced,Is.True);
            world.PausePhysics();world.StartPhysics();Assert.That(executions.Execute(request,out error),Is.True,error);Assert.That(body.linearVelocity.sqrMagnitude,Is.Zero,"A duplicate completed request cannot launch again");
            string folder=Environment.GetEnvironmentVariable("MAESTRO_AIMED_THROW_EVIDENCE");if(!string.IsNullOrEmpty(folder)){Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"aimed-throw.json"),capture.ToString());}
        }
        [UnityTest] public IEnumerator NewWallAfterPreviewRejectsLaunchAndTheWholeArcIsChecked(){
            var args=AimSetup();Assert.That((bool)AimPreview(args)["ready"],Is.True);var wall=AimWall(new Vector3(2.75f,1,0));
            Assert.That(runtime.Scheduler.Invoke(AimCall(args),Time.unscaledTime,out _,out var error),Is.False);Assert.That(error,Does.Contain("blocked"));Assert.That(ball.GetComponent<Rigidbody>().linearVelocity.sqrMagnitude,Is.Zero);
            wall.SetActive(false);Assert.That((bool)AimPreview(args)["ready"],Is.True);
            world.Contains=p=>p.x<3;Assert.That((string)AimPreview(args)["reason"],Does.Contain("leaves"));world.Contains=null;
            var viewer=root.GetComponent<RoomInteraction>().Viewer;viewer.position=new Vector3(2.75f,1.7f,0);Assert.That((string)AimPreview(args)["reason"],Does.Contain("head"));viewer.position=new Vector3(-4,1.6f,0);
            wall.SetActive(true);wall.transform.position=new Vector3(2.75f,1.75f,0);wall.transform.localScale=new Vector3(.1f,.15f,.5f);Assert.That((string)AimPreview(args)["reason"],Does.Contain("blocked"),"A thin obstacle near the apex must block the curved flight");
            wall.SetActive(false);args["maxSpeed"]=.1;Assert.That((string)AimPreview(args)["reason"],Does.Contain("speed"));yield return null;
        }
        [UnityTest] public IEnumerator AimedThrowResolvesExactAnchorsFreshAndRejectsStaleOrSelfTargets(){
            var args=AimSetup();string robot=CreateRobot();editor.Find(robot).transform.position=new Vector3(3,0,0);ball.transform.position=new Vector3(2,1,.8f);ball.GetComponent<RigidRoomItem>().Teleported();var anchor=new JObject{["kind"]="recipePart",["objectId"]=robot,["part"]="RightHand",["revision"]=editor.ObjectRevision(robot)};
            args["destination"]=new JObject{["kind"]="anchor",["anchor"]=anchor,["offset"]=new JObject{["x"]=0,["y"]=0,["z"]=.5}};
            var preview=AimPreview(args);Assert.That((bool)preview["ready"],Is.True,preview.ToString());
            editor.Find(robot).transform.position+=Vector3.right*.3f;var moved=AimPreview(args);Assert.That((bool)moved["ready"],Is.True,moved.ToString());Assert.That((float)moved["destination"]["x"]-(float)preview["destination"]["x"],Is.EqualTo(.3f).Within(.002f));
            Assert.That(runtime.Scheduler.Invoke(AimCall(args),Time.unscaledTime,out var run,out var error),Is.True,error);yield return null;Assert.That((string)runtime.Scheduler.Invocation(run)["phase"],Is.EqualTo("completed"));
            args["destination"]["anchor"]["revision"]=editor.ObjectRevision(robot)+1;Assert.That((string)AimPreview(args)["reason"],Does.Contain("changed"));
            args["destination"]["anchor"]=new JObject{["kind"]="object",["objectId"]=editor.Identity(ball),["revision"]=editor.ObjectRevision(editor.Identity(ball))};Assert.That((string)AimPreview(args)["reason"],Does.Contain("different"));
        }
        [UnityTest] public IEnumerator PauseOwnershipDisabledBodiesAndViewerLossPreventAimedThrows(){
            var args=AimSetup();var rigid=ball.GetComponent<RigidRoomItem>();var owner=new object();rigid.SetAnimationOwner(owner,true);Assert.That((string)AimPreview(args)["reason"],Does.Contain("owns"));rigid.SetAnimationOwner(owner,false);
            world.PausePhysics();Assert.That(runtime.Scheduler.Invoke(AimCall(args),Time.unscaledTime,out _,out _),Is.False);world.StartPhysics();
            root.GetComponent<RoomInteraction>().Viewer.gameObject.SetActive(false);Assert.That((string)AimPreview(args)["reason"],Does.Contain("view"));root.GetComponent<RoomInteraction>().Viewer.gameObject.SetActive(true);
            rigid.enabled=false;Assert.That((bool)AimPreview(args)["ready"],Is.False);rigid.enabled=true;rigid.Refresh();
            using(editor.RuntimeGate.Hold("Review first")){Assert.That((bool)AimPreview(args)["ready"],Is.False);Assert.That(runtime.Scheduler.Invoke(AimCall(args),Time.unscaledTime,out _,out _),Is.False);}
            Assert.That(world.Running,Is.False);yield return null;
        }
        [UnityTest] public IEnumerator BallCanLaunchFromTheFloorAndTheProgramUsesTheSamePhysicsAction(){
            var args=AimSetup(.5f);var body=ball.GetComponent<Rigidbody>();var collider=ball.GetComponentInChildren<SphereCollider>();Assert.That(collider,Is.Not.Null);
            ball.transform.position=new Vector3(2,collider.radius*collider.transform.lossyScale.y,0);ball.GetComponent<RigidRoomItem>().Teleported();
            for(int i=0;i<12;i++)yield return new WaitForFixedUpdate();
            Assert.That(body.linearVelocity.magnitude,Is.LessThan(.01f),"Use a real resting contact, not an elevated launch fixture");
            var preview=AimPreview(args);Assert.That((bool)preview["ready"],Is.True,preview.ToString());
            var source=BehaviourProgram.FromInvocation(AimCall(args));var sequence=new RuleSequence{id=Guid.NewGuid().ToString("N"),name="Toss the ball",program=source};runtime.Scheduler.Configure(new RuleDocument{sequences=new[]{sequence}});
            Assert.That(runtime.Scheduler.Trigger(sequence.id,Time.unscaledTime),Is.True,runtime.Scheduler.LastError);Assert.That(body.linearVelocity.x,Is.GreaterThan(1));
            yield return null;Assert.That(runtime.Scheduler.Outcomes.Last().phase,Is.EqualTo("completed"),runtime.Scheduler.LastError);yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();Assert.That(body.position.y,Is.GreaterThan(.1f));
        }
        [UnityTest] public IEnumerator AimedThrowFloorContactStillRejectsWallsPenetrationAndDownwardFlight(){
            var args=AimSetup(.5f);var collider=ball.GetComponentInChildren<SphereCollider>();float radius=collider.radius*collider.transform.lossyScale.y;
            void Place(float height){ball.transform.position=new Vector3(2,height,0);ball.GetComponent<RigidRoomItem>().Teleported();}
            Place(radius);Assert.That((bool)AimPreview(args)["ready"],Is.True);
            var wall=AimWall(new Vector3(2.15f,1,0));Assert.That((string)AimPreview(args)["reason"],Does.Contain("blocked"));wall.SetActive(false);
            Place(radius-.004f);Assert.That((string)AimPreview(args)["reason"],Does.Contain("blocked"));
            Place(radius);args["destination"]["position"]=new JObject{["x"]=2.1,["y"]=-1,["z"]=0};Assert.That((string)AimPreview(args)["reason"],Does.Contain("blocked"));
            yield return null;
        }
        [UnityTest] public IEnumerator BallisticEstimateTracksGravityAtBothDesktopAndQuestFixedSteps(){
            var args=AimSetup(.65f);var body=ball.GetComponent<Rigidbody>();float original=Time.fixedDeltaTime;var gravity=Physics.gravity;
            try{foreach(float dt in new[]{.02f,1f/72}){
                Time.fixedDeltaTime=dt;Physics.gravity=new Vector3(0,-7,0);world.PausePhysics();ball.transform.position=new Vector3(2,1,0);ball.GetComponent<RigidRoomItem>().Teleported();world.StartPhysics();
                var preview=AimPreview(args);Assert.That((bool)preview["ready"],Is.True,preview.ToString());float began=Time.fixedTime;
                Assert.That(runtime.Scheduler.Invoke(AimCall(args),Time.unscaledTime,out _,out var error),Is.True,error);
                while(Time.fixedTime-began<(float)preview["seconds"]-.0001f)yield return new WaitForFixedUpdate();
                Assert.That(Vector3.Distance(body.worldCenterOfMass,preview["destination"].ToObject<Vector3>()),Is.LessThan(.09f),$"dt {dt}");
            }}finally{Time.fixedDeltaTime=original;Physics.gravity=gravity;}
        }
    }
}
