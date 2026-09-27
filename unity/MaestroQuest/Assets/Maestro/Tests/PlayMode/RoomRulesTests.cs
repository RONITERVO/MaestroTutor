// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Book;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

namespace Maestro.Quest.Tests
{
    public sealed class RoomRulesTests
    {
        GameObject root, leftAnchor, rightAnchor;
        XRInteractionManager manager;
        RoomEditor editor;
        AnimationWorkshop animations;
        RuleWorkshop workshop;
        RoomRules runtime;
        RoomPhysicsWorld physics;
        RoomItem block;
        string directory, sequenceId;
        [UnitySetUp] public IEnumerator SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(),"MaestroRoomRules-"+Guid.NewGuid().ToString("N"));
            root = new GameObject("Room rules test"); manager = root.AddComponent<XRInteractionManager>(); var room = root.AddComponent<RoomInteraction>();
            RoomItem Included(string name)
            {
                var value = new GameObject(name); value.transform.SetParent(root.transform,false); var collider = value.AddComponent<BoxCollider>(); collider.size = Vector3.one*.1f;
                var item = value.AddComponent<RoomItem>(); item.Configure(new Collider[] { collider }); room.Register(item); return item;
            }
            physics=root.AddComponent<RoomPhysicsWorld>();
            var book = Included("book"); var avatar = Included("maestro"); editor = root.AddComponent<RoomEditor>(); editor.Initialize(room,book,avatar,directory,physics);
            var blockData = editor.Snapshot().objects.First(x => x.kind == RoomObjectKind.Block); block = editor.Find(blockData.id); editor.Select(block);
            editor.SaveAnimation(blockData.id,new RoomMotion { frames = new[] { new MotionFrame { position = blockData.position },new MotionFrame { time = 2,position = blockData.position + Vector3.right*.4f } } },null,false);
            animations = root.AddComponent<AnimationWorkshop>(); animations.Initialize(editor);
            workshop = root.AddComponent<RuleWorkshop>(); workshop.Initialize(editor,directory);
            leftAnchor = new GameObject("Left controller pose"); leftAnchor.transform.SetParent(root.transform,false); leftAnchor.transform.position = new Vector3(2,1,1);
            rightAnchor = new GameObject("Right controller pose"); rightAnchor.transform.SetParent(root.transform,false); rightAnchor.transform.position = new Vector3(3,1,1);
            runtime = root.AddComponent<RoomRules>(); runtime.Initialize(workshop,editor,animations,null,room,null,index => { var anchor = index == 0 ? leftAnchor : rightAnchor; return anchor && anchor.activeSelf ? anchor.transform : null; });
            workshop.NewSequence();
            for (int i = 0; i < System.Enum.GetValues(typeof(RuleActionKind)).Length && workshop.Selected.SimpleSteps()[0].action != RuleActionKind.RecordedAnimation; i++) workshop.CycleAction();
            Assert.That(workshop.Selected.SimpleSteps()[0].action,Is.EqualTo(RuleActionKind.RecordedAnimation));
            workshop.UseTarget(); sequenceId = workshop.Selected.id;
            yield return null;
        }


        [UnityTest] public IEnumerator FailedCreationSaveCannotApplyAnObjectOrReturnAnInventedResult()
        {
            int count=editor.Snapshot().objects.Length;
            Directory.CreateDirectory(directory);
            string future=Path.Combine(directory,"room.v999.json");File.WriteAllText(future,"preserve this newer room");
            var executor=new RoomAgentExecutor(editor);var request=new RoomAgentRequest {version=2,conditions=Array.Empty<RoomObjectCondition>(),
                commands=new[]{new RoomAgentCommand {action="execution",execution=new JObject {["operation"]="start",["runId"]=runtime.Scheduler.Receipts.NextId,["call"]=CreationCall()}}}};
            Assert.That(executor.Execute(request,out var error,out _),Is.False);
            Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));
            var selected=executor.Executions.Observe()["selected"];Assert.That((string)selected["phase"],Is.EqualTo("failed"));Assert.That(selected["output"],Is.Null);
            Assert.That(File.ReadAllText(future),Is.EqualTo("preserve this newer room"));
            yield return null;
        }


        [UnityTest] public IEnumerator RecipeCreationResultDrivesNativeRobotAnimationAndRetainsTheEditableObject()
        {
            var source=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-create.json")));
            var definition=Maestro.Quest.Programs.BehaviourCatalog.Action("object.create.recipe");
            source["functions"][0]["locals"][0]["name"]="robot";
            var create=source["functions"][0]["body"][0];create["capability"]=definition.Id;create["arguments"]=definition.Example;create["results"]["objectId"]="robot";
            var play=source["functions"][0]["body"][1];play["id"]="animate";play["capability"]="animation.recipe.play";play["bindings"]["target"]["var"]="robot";
            play["arguments"]=new JObject {["target"]=new string('0',32),["seconds"]=.6,["loop"]=true};
            var sequence=new RuleSequence {id="",name="Create waving robot",program=source.ToString(Newtonsoft.Json.Formatting.None)};
            Assert.That(workshop.Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",reference="robot",sequence=sequence}}},out var error,out var ids),Is.True,error);
            int count=editor.Snapshot().objects.Length;
            Assert.That(runtime.Trigger(ids.Single()),Is.True,runtime.Scheduler.LastError);
            string created=runtime.Scheduler.ObserveRuns().Single().locals.Single(x=>x.name=="robot").value;
            var geometry=editor.Find(created).GetComponent<RecipeObject>();
            Assert.That(geometry,Is.Not.Null);Assert.That(geometry.IsPlaying,Is.False,"Creation does not invent an automatic playback request");
            Assert.That(editor.Read(created).recipe.parts.Length,Is.EqualTo(19));
            runtime.Scheduler.Tick(Time.unscaledTime);Assert.That(geometry.IsPlaying,Is.True,runtime.Scheduler.LastError);
            var arm=geometry.Part("RightUpperArm");var pose=arm.localRotation;
            yield return new WaitForSeconds(.18f);Assert.That(Quaternion.Angle(pose,arm.localRotation),Is.GreaterThan(5),"Native recipe keyframes must move the actual joint");
            void Evidence(string phase) {
                string output=Environment.GetEnvironmentVariable("MAESTRO_RECIPE_CREATION_EVIDENCE");if(string.IsNullOrEmpty(output))return;
                Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,phase+".json"),new JObject {["program"]=source,
                    ["createdId"]=created,["recipe"]=JObject.Parse(JsonUtility.ToJson(editor.Read(created).recipe)),["playing"]=geometry.IsPlaying,
                    ["armRotation"]=JObject.Parse(JsonUtility.ToJson(arm.localRotation)),["rules"]=JObject.Parse(JsonUtility.ToJson(workshop.Observe(true)))}.ToString());
            }
            Evidence("animating");
            Assert.That(runtime.Scheduler.StopSequence(ids.Single()),Is.True);Assert.That(geometry.IsPlaying,Is.False);
            Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+1),"Stop never erases a creation");
            pose=arm.localRotation;yield return new WaitForSeconds(.1f);Assert.That(Quaternion.Angle(pose,arm.localRotation),Is.LessThan(.01f));Evidence("stopped");
            var saved=new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==created);
            Assert.That(saved.recipe.tracks.Length,Is.EqualTo(2));Assert.That(saved.recipe.playing,Is.False);
            editor.Undo();Assert.That(editor.Find(created),Is.Null);
            editor.Redo();Assert.That(editor.Find(created).GetComponent<RecipeObject>().Part("RightUpperArm"),Is.Not.Null);
        }
        [UnityTest] public IEnumerator RecipeCreationOneOffPersistsExactDefinitionAndRejectsCombinedPartOverflow()
        {
            var definition=Maestro.Quest.Programs.BehaviourCatalog.Action("object.create.recipe");
            // Model the actual JSON wire request, including float-to-JSON conversion.
            var call=JObject.Parse(new JObject {["id"]=definition.Id,["version"]=1,["arguments"]=definition.Example}.ToString());
            var executor=new RoomAgentExecutor(editor);string run=runtime.Scheduler.Receipts.NextId;int count=editor.Snapshot().objects.Length;
            var request=new RoomAgentRequest {version=2,conditions=Array.Empty<RoomObjectCondition>(),commands=new[]{new RoomAgentCommand {action="execution",execution=new JObject {["operation"]="start",["runId"]=run,["call"]=call}}}};
            Assert.That(executor.Execute(request,out var error,out _),Is.True,error);
            string id=(string)executor.Executions.Observe()["selected"]["output"]["objectId"];
            var before=editor.Read(id).recipe.Copy();Assert.That(executor.Execute(request,out error,out _),Is.True,error);
            Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+1));
            var receipt=new InvocationReceipts(directory).Find(run);
            Assert.That(JToken.DeepEquals(receipt["call"],call),Is.True);Assert.That((string)receipt["output"]["objectId"],Is.EqualTo(id));
            Assert.That(JsonUtility.ToJson(new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==id).recipe),Is.EqualTo(JsonUtility.ToJson(before)));
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_RECIPE_CREATION_EVIDENCE");
            if(!string.IsNullOrEmpty(evidence)) {Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"receipt.json"),executor.Executions.Observe().ToString());}
            editor.Undo();Assert.That(editor.Find(id),Is.Null);
            var bulk=new RoomRecipe {parts=Enumerable.Range(0,32).Select(i=>new RecipePart {id="part"+i,size=Vector3.one*.05f}).ToArray()};
            for(int i=0;i<8;i++)Assert.That(editor.CreateRecipe("Parts",Vector3.one,1,bulk,out _,out error),Is.True,error);
            count=editor.Snapshot().objects.Length;
            Assert.That(editor.CanCreateRecipe(before,out error),Is.False);Assert.That(error,Does.Contain("256"));
            string unused=runtime.Scheduler.Receipts.NextId;request.commands[0].execution["runId"]=unused;
            Assert.That(executor.Execute(request,out error,out _),Is.False);Assert.That(error,Does.Contain("256"));
            Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));
            Assert.That(runtime.Scheduler.Receipts.NextId,Is.EqualTo(unused),"Readiness rejects before authorizing an effect");
            Assert.That(runtime.Scheduler.Receipts.Find(unused),Is.Null);
            yield return null;
        }

        JObject CreationCall() {
            var program=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-create.json")));
            return new JObject {["id"]="object.create.primitive",["version"]=1,["arguments"]=program["functions"][0]["body"][0]["arguments"].DeepClone()};
        }
        [UnityTest] public IEnumerator CreationResultChainsIntoRealPhysicsWithoutInterruptingAnotherObject()
        {
            string source=File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-create.json"));
            var create=new RuleSequence {id="",name="Create then push",program=source};
            Assert.That(workshop.Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",reference="create",sequence=create}}},out var error,out var ids),Is.True,error);
            physics.SetSurfaces(true,"Ready");physics.StartPhysics();yield return new WaitForFixedUpdate();
            var executor=new RoomAgentExecutor(editor);
            var recording=new JObject {["id"]="animation.recording.play",["version"]=1,["arguments"]=new JObject {["target"]=editor.Identity(block),["seconds"]=2,["loop"]=false}};
            Assert.That(runtime.Scheduler.Invoke(recording,Time.unscaledTime,out var recordingId,out error),Is.True,error);
            var before=block.transform.localPosition;int count=editor.Snapshot().objects.Length;
            Assert.That(runtime.Trigger(ids.Single()),Is.True,runtime.Scheduler.LastError);
            var run=runtime.Scheduler.ObserveRuns().Single();string created=run.locals.Single(x=>x.name=="ball").value;
            Assert.That(Guid.TryParseExact(created,"N",out _),Is.True);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+1));
            Assert.That((string)runtime.Scheduler.Invocation(recordingId)["phase"],Is.EqualTo("running"),"Creation cannot cancel an unrelated animation");
            var body=editor.Find(created).GetComponent<Rigidbody>();Assert.That(body.mass,Is.EqualTo(.5f));
            runtime.Scheduler.Tick(Time.unscaledTime);
            Assert.That(body.linearVelocity.x,Is.EqualTo(1).Within(.001f));Assert.That(body.linearVelocity.y,Is.EqualTo(2).Within(.001f));
            var persisted=new RoomStorage(directory).Load(out error);Assert.That(persisted.objects.Any(x=>x.id==created),Is.True,error);
            yield return new WaitForSeconds(.15f);Assert.That(block.transform.localPosition.x,Is.GreaterThan(before.x));
            Assert.That(editor.Find(created).transform.localPosition.x,Is.GreaterThan(.35f));
            Assert.That(runtime.Scheduler.Outcomes.Last().phase,Is.EqualTo("completed"));
            editor.Undo();Assert.That(editor.Find(created),Is.Null);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));
            editor.Redo();Assert.That(editor.Find(created),Is.Not.Null);Assert.That(editor.Read(created).name,Is.EqualTo("Program ball"));
        }
        [UnityTest] public IEnumerator CreationReceiptRetainsExactResultAcrossDuplicateRequestAndRestart()
        {
            var executor=new RoomAgentExecutor(editor);int count=editor.Snapshot().objects.Length;
            string runId=runtime.Scheduler.Receipts.NextId;
            var request=new RoomAgentRequest {version=2,conditions=Array.Empty<RoomObjectCondition>(),commands=new[]{new RoomAgentCommand {action="execution",execution=new JObject {["operation"]="start",["runId"]=runId,["call"]=CreationCall()}}}};
            Assert.That(executor.Execute(request,out var error,out _),Is.True,error);
            var selected=(JObject)executor.Executions.Observe()["selected"];Assert.That((string)selected["phase"],Is.EqualTo("completed"));
            string id=(string)selected["output"]["objectId"];Assert.That(editor.Find(id),Is.Not.Null);
            var reopened=new RoomAgentExecutor(editor);Assert.That(reopened.Execute(request,out error,out _),Is.True,error);
            Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+1));Assert.That((string)reopened.Executions.Observe()["selected"]["output"]["objectId"],Is.EqualTo(id));
            var receipts=new InvocationReceipts(directory);Assert.That(receipts.Error,Is.Null);Assert.That((string)receipts.Find(runId)["output"]["objectId"],Is.EqualTo(id));
            Assert.That(new RoomStorage(directory).Load(out error).objects.Any(x=>x.id==id),Is.True,error);
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_CREATION_EVIDENCE");
            if(!string.IsNullOrEmpty(evidence)) {Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"created.json"),reopened.Executions.Observe().ToString());}
            editor.Undo();Assert.That(editor.Find(id),Is.Null);
            Assert.That(reopened.Execute(request,out error,out _),Is.True,error);Assert.That(editor.Find(id),Is.Null,"A historical successful receipt cannot recreate an undone object");
            yield return null;
        }

        XRRayInteractor Hand(int index, Vector3 position)
        {
            var hand = new GameObject("Rule test hand"); hand.SetActive(false); hand.transform.SetParent(root.transform,false); hand.transform.position = position;
            hand.AddComponent<ControllerIdentity>().PointerId = index;
            var ray = hand.AddComponent<XRRayInteractor>(); ray.enableUIInteraction = false; ray.interactionManager = manager; ray.keepSelectedTargetValid = true; ray.manipulateAttachTransform = false;
            ray.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.State;
            ray.selectInput = new XRInputButtonReader { inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue,manualPerformed = true,manualValue = 1 };
            hand.SetActive(true); return ray;
        }
        [UnityTest] public IEnumerator EventProgramsSaveSignalAnimateAndStopThroughTheSharedNativeExecutor()
        {
            string target=editor.SelectedId;var program=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-events.json")));
            program["resources"]=new JArray(target);
            var loop=(JArray)program["functions"][0]["body"][0]["body"];loop[0]["event"]="user.wave";loop[0]["source"]="";
            program["functions"][0]["locals"][1]["initial"]=0;
            loop[1]["then"][1]=new JObject {["id"]="wave",["op"]="invoke",["capability"]="animation.recording.play",["version"]=1,["arguments"]=new JObject {["target"]=target,["seconds"]=.5,["loop"]=false},["bindings"]=new JObject()};
            var sequence=workshop.Selected;sequence.program=program.ToString(Newtonsoft.Json.Formatting.None);sequence.repeat=false;
            var executor=new RoomAgentExecutor(editor);var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);
            bool Execute(RuleRequest rule,out string error)=>executor.Execute(new RoomAgentRequest {version=2,commands=new[]{new RoomAgentCommand {action="rules",rule=rule}}},out error,out _);
            Assert.That(Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",sequence=sequence}}},out var error),Is.True,error);
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero,"Saving does not start a subscriber");
            var saved=JsonUtility.ToJson(workshop.Snapshot());int revision=workshop.Revision;
            void Evidence(string phase) {
                string output=Environment.GetEnvironmentVariable("MAESTRO_EVENT_EVIDENCE");if(string.IsNullOrEmpty(output))return;
                Directory.CreateDirectory(output);var state=observer.Observe();state.rules=workshop.Observe();state.visible=true;state.workspaceView="rules";File.WriteAllText(Path.Combine(output,phase+".json"),RoomAgentWire.Serialize(state));
            }
            Assert.That(Execute(new RuleRequest {action="play",revision=revision,target=sequence.id},out error),Is.True,error);Evidence("waiting");
            Assert.That(runtime.Scheduler.TargetsBusy(new[]{target}),Is.False);
            var raw=new JObject {["version"]=2,["commands"]=new JArray(new JObject {["action"]="rules",["rule"]=new JObject {["action"]="signal",["revision"]=revision,["eventName"]="user.wave",["value"]=1}})};
            Assert.That(RoomControls.ValidWire(raw.ToString()),Is.True);var request=JsonUtility.FromJson<RoomAgentRequest>(raw.ToString());Assert.That(RoomAgentWire.PopulateStructured(request,raw),Is.True);
            Assert.That(executor.Execute(request,out error,out _),Is.True,error);
            var before=block.transform.localPosition;yield return new WaitForSeconds(.2f);
            Assert.That(block.transform.localPosition.x,Is.GreaterThan(before.x+.02f));Evidence("moving");
            Assert.That(runtime.Scheduler.ObserveRuns().Single().state.Single(x=>x.name=="count").value,Is.EqualTo("1"));
            yield return new WaitForSeconds(.7f);Assert.That(runtime.Scheduler.ObserveRuns().Single().waiting,Is.True);Assert.That(runtime.Scheduler.TargetsBusy(new[]{target}),Is.False);
            Assert.That(executor.Execute(request,out error,out _),Is.True,error);yield return new WaitForSeconds(.15f);
            Assert.That(runtime.Scheduler.ObserveRuns().Single().state.Single(x=>x.name=="count").value,Is.EqualTo("2"));Evidence("second");
            request.commands[0].rule.revision--;Assert.That(executor.Execute(request,out error,out _),Is.False,"A stale signal cannot target revised definitions");
            Assert.That(Execute(new RuleRequest {action="stop",target=sequence.id},out error),Is.True,error);Evidence("stopped");
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That(runtime.Scheduler.EventQueueCount,Is.Zero);
            Assert.That(workshop.Revision,Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(workshop.Snapshot()),Is.EqualTo(saved),"Signals are runtime effects, not edits");
            Assert.That(Execute(new RuleRequest {action="play",revision=revision,target=sequence.id},out error),Is.True,error);
            runtime.SendMessage("OnApplicationPause",true);runtime.SendMessage("OnApplicationPause",false);yield return null;
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Evidence("paused");
        }
        [UnityTest] public IEnumerator SharedPhysicsCallsPushAndStopTheRealBallWithoutPlaybackOrReplay()
        {
            var executor=new RoomAgentExecutor(editor);var data=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Ball);
            var item=editor.Find(data.id);var body=item.GetComponent<Rigidbody>();var rigid=item.GetComponent<RigidRoomItem>();
            bool Execute(RoomAgentCommand command,out string error)=>executor.Execute(new RoomAgentRequest {version=2,conditions=new[]{new RoomObjectCondition {id=data.id,revision=editor.ObjectRevision(data.id)}},commands=new[]{command}},out error,out _);
            Assert.That(Execute(new RoomAgentCommand {action="physicsSettings",target=data.id,physics=new ObjectPhysicsSettings {mode="bouncy",mass=.6f,shape="sphere"}},out var error),Is.True,error);
            physics.SetSurfaces(true,"Ready");physics.StartPhysics();yield return new WaitForFixedUpdate();rigid.StopVelocity();
            var call=new JObject {["id"]="object.physics.impulse",["version"]=1,["arguments"]=new JObject {["target"]=data.id,["x"]=.6,["y"]=1.2,["z"]=0}};
            string id=runtime.Scheduler.Receipts.NextId;
            var command=new RoomAgentCommand {action="execution",execution=new JObject {["operation"]="start",["runId"]=id,["call"]=call}};
            Assert.That(Execute(command,out error),Is.True,error);Assert.That(body.linearVelocity.x,Is.EqualTo(1).Within(.001f));Assert.That(body.linearVelocity.y,Is.EqualTo(2).Within(.001f));
            Assert.That(rigid.AnimationOwned,Is.False,"An instant push must leave ownership with physics");
            void Evidence(string phase) {
                string output=Environment.GetEnvironmentVariable("MAESTRO_PHYSICS_ACTION_EVIDENCE");if(string.IsNullOrEmpty(output))return;
                Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,phase+".json"),new JObject {
                    ["execution"]=executor.Executions.Observe(),["position"]=JObject.Parse(JsonUtility.ToJson(item.transform.position)),
                    ["velocity"]=JObject.Parse(JsonUtility.ToJson(body.linearVelocity)),["mass"]=body.mass,["gravity"]=body.useGravity,["simulating"]=rigid.Simulating
                }.ToString());
            }
            Evidence("pushed");
            runtime.Scheduler.Tick(Time.unscaledTime);
            Assert.That((string)executor.Executions.Observe()["selected"]["phase"],Is.EqualTo("completed"));
            var velocity=body.linearVelocity;Assert.That(Execute(command,out error),Is.True,error);Assert.That(body.linearVelocity,Is.EqualTo(velocity),"A duplicate cannot apply a second impulse");
            var before=item.transform.position;yield return new WaitForSeconds(.15f);
            Assert.That(item.transform.position.x,Is.GreaterThan(before.x+.05f));
            call=new JObject {["id"]="object.physics.stop",["version"]=1,["arguments"]=new JObject {["target"]=data.id}};
            command.execution=new JObject {["operation"]="start",["runId"]=runtime.Scheduler.Receipts.NextId,["call"]=call};
            before=item.transform.position;Assert.That(Execute(command,out error),Is.True,error);
            Assert.That(body.linearVelocity,Is.EqualTo(Vector3.zero));Assert.That(item.transform.position,Is.EqualTo(before),"Stop motion cannot restore a saved pose");Evidence("stopped");
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
            Assert.That(body.linearVelocity.y,Is.LessThan(0));Assert.That(body.useGravity,Is.True);Evidence("gravity");
            root.transform.rotation=Quaternion.Euler(0,90,0);Physics.SyncTransforms();rigid.StopVelocity();
            call=new JObject {["id"]="object.physics.impulse",["version"]=1,["arguments"]=new JObject {["target"]=data.id,["x"]=.6,["y"]=0,["z"]=0}};
            command.execution=new JObject {["operation"]="start",["runId"]=runtime.Scheduler.Receipts.NextId,["call"]=call};
            Assert.That(Execute(command,out error),Is.True,error);
            Assert.That(Vector3.Distance(body.linearVelocity,root.transform.TransformDirection(Vector3.right)),Is.LessThan(.001f),"Impulse axes follow the room without scaling physical units");Evidence("rotated-room");
        }
        [UnityTest] public IEnumerator LostReceiptAcrossBrowserReconnectReturnsTheOriginalActionBeforeStaleTargetChecks()
        {
            var executor=new RoomAgentExecutor(editor);string target=editor.SelectedId;
            string id=runtime.Scheduler.Receipts.NextId;
            var call=new JObject {["id"]="animation.recording.play",["version"]=1,["arguments"]=new JObject {["target"]=target,["seconds"]=1,["loop"]=false}};
            var request=new RoomAgentRequest {version=2,conditions=new[]{new RoomObjectCondition {id=target,revision=editor.ObjectRevision(target)}},
                commands=new[]{new RoomAgentCommand {action="execution",execution=new JObject {["operation"]="start",["runId"]=id,["call"]=call}}}};
            Assert.That(executor.Execute(request,out var error,out _),Is.True,error);
            yield return new WaitForSeconds(.2f);var moved=block.transform.localPosition;
            var reopened=new RoomAgentExecutor(editor);
            request.conditions[0].revision=0; // The old observation cannot authorize a new action.
            Assert.That(reopened.Execute(request,out error,out _),Is.True,error);
            Assert.That((string)reopened.Executions.Observe()["selected"]["id"],Is.EqualTo(id));
            Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));Assert.That(block.transform.localPosition,Is.EqualTo(moved));
            call["arguments"]["seconds"]=2;
            Assert.That(reopened.Execute(request,out error,out _),Is.False);Assert.That(error,Does.Contain("different call"));
            call["arguments"]["seconds"]=1;
            yield return new WaitForSeconds(1);
            Assert.That(reopened.Execute(request,out error,out _),Is.True,error);
            Assert.That((string)reopened.Executions.Observe()["selected"]["phase"],Is.EqualTo("completed"));
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
            var recovered=new InvocationReceipts(directory);Assert.That((string)recovered.Find(id)["phase"],Is.EqualTo("completed"));
            request.commands[0].execution["runId"]=Guid.NewGuid().ToString("N");
            Assert.That(reopened.Execute(request,out error,out _),Is.False);Assert.That(error,Does.Contain("unknown"));
        }
        [UnityTest] public IEnumerator OneOffNativeWireMovesTheRealItemOnceAndLeavesDocumentsUntouched()
        {
            var executor=new RoomAgentExecutor(editor);var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);
            var inbox=new RoomAgentInbox();string client=Guid.NewGuid().ToString("N");
            inbox.TryAccept(new RoomAgentSnapshot {clientId=client},out _);
            int revision=editor.Revision,ruleRevision=workshop.Revision;string selected=editor.SelectedId,document=JsonUtility.ToJson(editor.Snapshot()),rules=JsonUtility.ToJson(workshop.Snapshot());
            var call=new JObject {["id"]="animation.recording.play",["version"]=1,["arguments"]=new JObject {["target"]=selected,["seconds"]=1,["loop"]=false}};
            var raw=new JObject {["version"]=2,["sequence"]=1,["session"]=inbox.Session,["conditions"]=new JArray(new JObject {["id"]=selected,["revision"]=editor.ObjectRevision(selected)}),
                ["commands"]=new JArray(new JObject {["action"]="execution",["execution"]=new JObject {["operation"]="start",["call"]=call}})};
            Assert.That(RoomControls.ValidWire(raw.ToString()),Is.True);
            var request=JsonUtility.FromJson<RoomAgentRequest>(raw.ToString());Assert.That(RoomAgentWire.PopulateStructured(request,raw),Is.True);
            var envelope=new RoomAgentSnapshot {clientId=client,session=inbox.Session,request=request};
            Assert.That(inbox.TryAccept(envelope,out var accepted),Is.True);
            Assert.That(executor.Execute(accepted,out var error,out var created),Is.True,error);Assert.That(created,Is.Empty);
            var view=executor.Executions.Observe();string runId=(string)view["selected"]["id"];
            void Evidence(string phase) {
                string output=Environment.GetEnvironmentVariable("MAESTRO_EXECUTION_EVIDENCE");if(string.IsNullOrEmpty(output))return;
                Directory.CreateDirectory(output);var state=observer.Observe();state.execution=executor.Executions.Observe();
                File.WriteAllText(Path.Combine(output,phase+".json"),RoomAgentWire.Serialize(state));
            }
            Evidence("running");Assert.That(inbox.TryAccept(envelope,out _),Is.False,"A duplicated transport request never starts another action");
            var start=block.transform.localPosition;yield return new WaitForSeconds(.25f);
            Assert.That(block.transform.localPosition.x,Is.GreaterThan(start.x+.02f));
            Assert.That(executor.Executions.Execute(new JObject {["operation"]="inspect",["runId"]=runId},out _),Is.True);
            Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));
            Assert.That(executor.Executions.Execute(new JObject {["operation"]="cancel",["runId"]=runId},out _),Is.True);
            Evidence("cancelled");Assert.That((string)executor.Executions.Observe()["selected"]["phase"],Is.EqualTo("cancelled"));
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
            Assert.That(executor.Executions.Execute(new JObject {["operation"]="cancel",["runId"]=runId},out _),Is.True);
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(editor.SelectedId,Is.EqualTo(selected));
            Assert.That(workshop.Revision,Is.EqualTo(ruleRevision));Assert.That(JsonUtility.ToJson(workshop.Snapshot()),Is.EqualTo(rules));
            Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(document));
            raw["sequence"]=2;request=JsonUtility.FromJson<RoomAgentRequest>(raw.ToString());RoomAgentWire.PopulateStructured(request,raw);
            Assert.That(executor.Execute(request,out error,out _),Is.True,error);yield return new WaitForSeconds(1.2f);
            Evidence("completed");Assert.That((string)executor.Executions.Observe()["selected"]["phase"],Is.EqualTo("completed"));
            Assert.That(executor.Execute(request,out error,out _),Is.True,error);yield return new WaitForSeconds(.2f);
            var position=block.transform.position;var hand=Hand(1,position-Vector3.forward*.2f);
            manager.SelectEnter((IXRSelectInteractor)hand,block.Grab);
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That((string)executor.Executions.Observe()["selected"]["phase"],Is.EqualTo("cancelled"));
            Assert.That(Vector3.Distance(position,block.transform.position),Is.LessThan(.01f),"Manual grab keeps the current animated position");
            Evidence("grabbed");
        }
        [UnityTest] public IEnumerator OneOffRechecksTargetAndPropRevisionsAndRefusesBusyOrPausedRuntime()
        {
            var executor=new RoomAgentExecutor(editor);string target=editor.SelectedId;int revision=editor.Revision;
            var request=new RoomAgentRequest {version=2,conditions=new[] {new RoomObjectCondition {id=target,revision=editor.ObjectRevision(target)-1}},commands=new[] {new RoomAgentCommand {action="execution",execution=new JObject {["operation"]="start",["call"]=new JObject {["id"]="animation.recording.play",["version"]=1,["arguments"]=new JObject {["target"]=target,["seconds"]=1,["loop"]=false}}}}}};
            Assert.That(executor.Execute(request,out var error,out _),Is.False);Assert.That(error,Does.Contain("target changed"));Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
            request.conditions[0].revision=editor.ObjectRevision(target);Assert.That(runtime.Trigger(sequenceId),Is.True);
            Assert.That(executor.Execute(request,out error,out _),Is.False);Assert.That(error,Does.Contain("owns"));Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));
            runtime.StopAll();runtime.enabled=false;Assert.That(executor.Execute(request,out error,out _),Is.False);Assert.That(error,Does.Contain("paused"));runtime.enabled=true;
            var args=(JObject)request.commands[0].execution["call"]["arguments"];args["target"]="maestro";args["prop"]=new JObject {["objectId"]=target,["avatarHash"]="",["hand"]="right",["release"]="return",["offset"]=new JObject {["x"]=0,["y"]=0,["z"]=0},["rotation"]=new JObject {["x"]=0,["y"]=0,["z"]=0,["w"]=1},["releaseAt"]=1};
            Assert.That(Maestro.Quest.Programs.BehaviourCatalog.TryInvocation("animation.recording.play",1,args,out _,out _),Is.True);
            request.conditions=new[] {new RoomObjectCondition {id="maestro",revision=editor.ObjectRevision("maestro")},new RoomObjectCondition {id=target,revision=editor.ObjectRevision(target)-1}};
            Assert.That(executor.Execute(request,out error,out _),Is.False);Assert.That(error,Does.Contain("target changed"),"A stale prop is rejected before handler/authoring side effects");
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
            yield return null;
        }
        [UnityTest] public IEnumerator CatalogDiscoveryAndLiveChecksDoNotEditOrInterruptTheRoom()
        {
            var executor=new RoomAgentExecutor(editor);int roomRevision=editor.Revision,ruleRevision=workshop.Revision;
            string selected=editor.SelectedId,document=JsonUtility.ToJson(editor.Snapshot());
            var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);
            JObject Query(JObject query,string phase) {
                Assert.That(executor.Execute(new RoomAgentRequest {version=2,commands=new[] {new RoomAgentCommand {action="catalog",catalog=query}}},out var error,out var created),Is.True,error);
                Assert.That(created,Is.Empty);var result=executor.Catalog.Observe();
                string output=Environment.GetEnvironmentVariable("MAESTRO_CATALOG_EVIDENCE");
                if(!string.IsNullOrEmpty(output)) {Directory.CreateDirectory(output);var state=observer.Observe();state.catalog=result;File.WriteAllText(Path.Combine(output,phase+".json"),RoomAgentWire.Serialize(state));}
                Assert.That(editor.Revision,Is.EqualTo(roomRevision));Assert.That(workshop.Revision,Is.EqualTo(ruleRevision));Assert.That(editor.SelectedId,Is.EqualTo(selected));
                return result;
            }
            var first=Query(new JObject {["operation"]="search",["query"]="",["offset"]=0},"search");
            var second=Query(new JObject {["operation"]="search",["query"]="",["offset"]=6},"search-next");
            Assert.That(first["entries"].Count(),Is.EqualTo(6));Assert.That((int)first["total"],Is.EqualTo(Maestro.Quest.Programs.BehaviourCatalog.Actions.Count));
            Assert.That(first["entries"].Select(x=>(string)x["id"]).Intersect(second["entries"].Select(x=>(string)x["id"])),Is.Empty);
            var inspected=Query(new JObject {["operation"]="inspect",["capability"]="animation.recording.play",["version"]=1},"inspect");
            Assert.That(JToken.DeepEquals(inspected["definition"],Maestro.Quest.Programs.BehaviourCatalog.Action("animation.recording.play").ToJson()),Is.True);
            var check=new JObject {["operation"]="check",["call"]=new JObject {["id"]="animation.recording.play",["version"]=1,["arguments"]=new JObject {["target"]=selected,["seconds"]=1,["loop"]=false}}};
            var ready=Query(check,"ready");Assert.That((bool)ready["valid"],Is.True);Assert.That((bool)ready["available"],Is.True);
            Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(document));Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
            Assert.That(runtime.Trigger(sequenceId),Is.True);yield return null;
            var occupied=Query(check,"occupied");Assert.That((bool)occupied["occupied"],Is.True);Assert.That((bool)occupied["available"],Is.False);
            Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1),"Checking cannot stop another action");
            runtime.StopAll();Assert.That((bool)executor.Catalog.Observe()["available"],Is.True,"Checks refresh without another command");
            runtime.enabled=false;Assert.That((bool)executor.Catalog.Observe()["available"],Is.False);runtime.enabled=true;
            check["call"]["arguments"]["target"]=Guid.NewGuid().ToString("N");var missing=Query(check,"missing");
            Assert.That((bool)missing["valid"],Is.True);Assert.That((bool)missing["available"],Is.False);
            check["call"]["arguments"]["seconds"]=100;var invalid=Query(check,"invalid");
            Assert.That((bool)invalid["valid"],Is.False);Assert.That((bool)invalid["available"],Is.False);
            var unknown=Query(new JObject {["operation"]="inspect",["capability"]="future.unknown",["version"]=1},"unknown");
            Assert.That(unknown["definition"].Type,Is.EqualTo(JTokenType.Null));
        }
        [UnityTest] public IEnumerator RealButtonAndWebActivityTriggerTheSameRecordedActionAndPauseStopsIt()
        {
            workshop.AddBinding(); workshop.AddButton(ButtonMount.Room);
            yield return null; Physics.SyncTransforms();
            runtime.ObserveSnapshot(new BookSnapshot { activity = "idle" }); runtime.ObserveSnapshot(new BookSnapshot { activity = "speaking",audioPaused = true });
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero,"Paused browser snapshots cannot start rules");
            var before = block.transform.localPosition;
            runtime.ObserveSnapshot(new BookSnapshot { activity = "speaking" }); Assert.That(runtime.Scheduler.RunningCount,Is.Zero,"Resuming establishes a fresh baseline");
            runtime.ObserveSnapshot(new BookSnapshot { activity = "idle" }); runtime.ObserveSnapshot(new BookSnapshot { activity = "speaking" }); Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));
            yield return new WaitForSeconds(.25f); Assert.That(block.transform.localPosition.x,Is.GreaterThan(before.x+.02f));
            runtime.StopAll(); Assert.That(Vector3.Distance(block.transform.localPosition,before),Is.LessThan(.001f));
            var button = root.GetComponentInChildren<RuleButton>(); var router = root.AddComponent<BookPointerRouter>(); router.Editor = editor;
            var ray = new Ray(button.transform.position-Vector3.forward*.3f,Vector3.forward);
            Assert.That(router.Begin(1,ray),Is.True); router.End(1,ray); Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));
            runtime.SendMessage("OnApplicationPause",true); Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
            runtime.SendMessage("OnApplicationPause",false); yield return null; Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
        }
        [UnityTest] public IEnumerator MountedButtonFollowsControllerRejectsItsOwnerAndSavesAdjustedOffset()
        {
            workshop.AddButton(ButtonMount.LeftController); yield return null;
            var button = root.GetComponentInChildren<RuleButton>(); var data = workshop.Snapshot().buttons.Single();
            Assert.That(Vector3.Distance(button.transform.position,leftAnchor.transform.TransformPoint(data.position)),Is.LessThan(.001f));
            leftAnchor.transform.position += Vector3.up*.2f; yield return null;
            Assert.That(Vector3.Distance(button.transform.position,leftAnchor.transform.TransformPoint(data.position)),Is.LessThan(.001f));
            var own = Hand(0,button.transform.position-Vector3.forward*.25f); var other = Hand(1,button.transform.position-Vector3.forward*.25f);
            var item = button.GetComponent<RoomItem>(); Assert.That(button.Process(own,item.Grab),Is.False); Assert.That(button.Process(other,item.Grab),Is.True);
            var router = root.AddComponent<BookPointerRouter>(); router.Editor = editor; Physics.SyncTransforms();
            var ray = new Ray(button.transform.position-Vector3.forward*.3f,Vector3.forward);
            Assert.That(router.Begin(0,ray),Is.False); Assert.That(router.Begin(1,ray),Is.True); router.End(1,ray);
            Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1)); runtime.StopAll();
            own.gameObject.SetActive(false); manager.SelectEnter((IXRSelectInteractor)other,item.Grab); yield return new WaitForSeconds(.15f);
            other.transform.position += Vector3.up*.06f; yield return null; yield return null;
            other.gameObject.SetActive(false); yield return null;
            var adjusted = workshop.Snapshot().buttons.Single(); Assert.That(adjusted.position.y,Is.EqualTo(data.position.y+.06f).Within(.01f));
            leftAnchor.SetActive(false); yield return null; Assert.That(button.GetComponent<Collider>().enabled,Is.False);
            leftAnchor.SetActive(true); yield return null; Assert.That(button.GetComponent<Collider>().enabled,Is.True);
            workshop.SendMessage("OnApplicationPause",true);
            var restored = new RuleStorage(directory).Load(out var error); Assert.That(error,Is.Null); Assert.That(restored.buttons.Single().position,Is.EqualTo(adjusted.position));
        }
        [UnityTest] public IEnumerator AuthoringAndUserGripTakePriorityAndRuleEditsUndoAsOneDocument()
        {
            var originalMotion = editor.Read(editor.SelectedId).motion;
            animations.ToggleRecord(); Assert.That(runtime.Trigger(sequenceId),Is.False); animations.Stop();
            editor.SaveAnimation(editor.SelectedId,originalMotion,null,false);
            Assert.That(runtime.Trigger(sequenceId),Is.True); yield return new WaitForSeconds(.2f);
            Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1),"Grip must interrupt a still-running take");
            Assert.That(block.transform.localPosition.x,Is.GreaterThan(originalMotion.frames[0].position.x+.01f));
            var current = block.transform.position; var hand = Hand(1,current-Vector3.forward*.2f);
            manager.SelectEnter((IXRSelectInteractor)hand,block.Grab);
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero); Assert.That(Vector3.Distance(current,block.transform.position),Is.LessThan(.01f),"Grabbing does not snap an animated object back");
            hand.gameObject.SetActive(false); yield return null;
            workshop.AddBinding(); workshop.AddButton(ButtonMount.Room); int count = workshop.Snapshot().buttons.Length;
            workshop.DeleteSequence(); Assert.That(workshop.Snapshot().sequences,Is.Empty); workshop.Undo();
            Assert.That(workshop.Snapshot().buttons.Length,Is.EqualTo(count)); Assert.That(workshop.Snapshot().bindings.Length,Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator SharedRuleEditsPreserveStepIdentityRejectStaleAndUndoWholeBatch()
        {
            int revision=workshop.Revision;var sequence=workshop.Selected;string originalStep=sequence.SimpleSteps()[0].id;
            sequence.SetSimpleSteps(sequence.SimpleSteps().Append(new RuleStep {action=RuleActionKind.Wait,seconds=1}).ToArray());
            var edit=new RuleRequest {action="edit",revision=revision,edits=new[]{new RuleEdit {kind="save",sequence=sequence},new RuleEdit {kind="button",target=sequence.id,mount=ButtonMount.LeftController}}};
            Assert.That(workshop.Execute(edit,out var error,out _),Is.True,error);Assert.That(workshop.Revision,Is.EqualTo(revision+1));
            var saved=workshop.Selected;Assert.That(saved.SimpleSteps()[0].id,Is.EqualTo(originalStep));Assert.That(RuleDocument.IsId(saved.SimpleSteps()[1].id),Is.True);
            Assert.That(workshop.Execute(edit,out _,out _),Is.False,"An old draft cannot replace a newer edit");
            string before=JsonUtility.ToJson(workshop.Snapshot());var invalid=saved.SimpleSteps();invalid[0].seconds=-1;saved.SetSimpleSteps(invalid);
            edit.revision=workshop.Revision;edit.edits[0].sequence=saved;Assert.That(workshop.Execute(edit,out _,out _),Is.False);Assert.That(JsonUtility.ToJson(workshop.Snapshot()),Is.EqualTo(before));
            Assert.That(workshop.Execute(new RuleRequest {action="undo",revision=workshop.Revision},out _,out _),Is.True);
            Assert.That(workshop.Selected.SimpleSteps().Length,Is.EqualTo(1));Assert.That(workshop.Snapshot().buttons.Length,Is.Zero);
            Assert.That(workshop.Execute(new RuleRequest {action="redo",revision=workshop.Revision},out _,out _),Is.True);
            var restored=workshop.Selected;string added=restored.SimpleSteps()[1].id;restored.SetSimpleSteps(restored.SimpleSteps().Reverse().ToArray());
            Assert.That(workshop.Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",sequence=restored}}},out _,out _),Is.True);
            Assert.That(workshop.Selected.SimpleSteps()[0].id,Is.EqualTo(added));workshop.AddStep();Assert.That(workshop.Observe().revision,Is.GreaterThan(revision));
            workshop.SendMessage("OnApplicationPause",true);var loaded=new RuleStorage(directory).Load(out _);Assert.That(loaded.sequences.First(x=>x.id==sequence.id).SimpleSteps()[0].id,Is.EqualTo(added));
            yield return null;
        }
        [UnityTest] public IEnumerator AgentRuleRecipePlaybackSharesTutorEventsAndActualButtonInput()
        {
            var agent=new RoomAgentExecutor(editor);var recipe=RecipeTemplates.BoxRobot(true);recipe.playing=false;
            Assert.That(agent.Execute(new RoomAgentRequest {version=1,sceneRevision=editor.Revision,commands=new[]{new RoomAgentCommand {action="create",kind="recipe",reference="robot",name="Robot",recipe=recipe}}},out var error,out var created),Is.True,error);
            string target=created.Single();var geometry=editor.Find(target).GetComponent<RecipeObject>();Assert.That(geometry.IsPlaying,Is.False);
            var sequence=new RuleSequence {id="",name="Wave with speech",program=Maestro.Quest.Programs.BehaviourProgram.FromSteps(new RuleStep {action=RuleActionKind.RecipeAnimation,targetId=target,seconds=.3f,loop=true})};
            var request=new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{
                new RuleEdit {kind="save",reference="wave",sequence=sequence},
                new RuleEdit {kind="bind",binding=new RuleBinding {sequenceId="wave",trigger=RuleEventKind.Speaking,stopOnExit=true}},
                new RuleEdit {kind="button",target="wave",mount=ButtonMount.Room}}};
            Assert.That(agent.Execute(new RoomAgentRequest {version=2,commands=new[]{new RoomAgentCommand {action="rules",rule=request}}},out error,out created),Is.True,error);
            string id=created.Single();yield return null;runtime.ObserveSnapshot(new BookSnapshot {activity="idle"});runtime.ObserveSnapshot(new BookSnapshot {activity="speaking"});
            Assert.That(geometry.IsPlaying,Is.True,runtime.Scheduler.LastError);Assert.That(workshop.Observe().running.Single().nodeId,Is.EqualTo(workshop.Selected.SimpleSteps()[0].id));
            var arm=geometry.Part("RightUpperArm");var rotation=arm.localRotation;yield return new WaitForSeconds(.12f);Assert.That(Quaternion.Angle(rotation,arm.localRotation),Is.GreaterThan(.1f));
            runtime.ObserveSnapshot(new BookSnapshot {activity="idle"});Assert.That(geometry.IsPlaying,Is.False);
            var button=root.GetComponentInChildren<RuleButton>();var router=root.AddComponent<BookPointerRouter>();router.Editor=editor;Physics.SyncTransforms();
            var ray=new Ray(button.transform.position-Vector3.forward*.3f,Vector3.forward);Assert.That(router.Begin(1,ray),Is.True);router.End(1,ray);Assert.That(geometry.IsPlaying,Is.True);
            yield return new WaitForSeconds(.4f);Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That(geometry.IsPlaying,Is.False);
            Assert.That(editor.Read(target).recipe.playing,Is.False,"Rule playback does not rewrite the saved recipe flags");
        }
        [UnityTest] public IEnumerator ProgramsFromAgentDriveRealRecipeThroughEventsButtonsStopAndUndo()
        {
            var executor=new RoomAgentExecutor(editor);var recipe=RecipeTemplates.BoxRobot(true);recipe.playing=false;
            Assert.That(executor.Execute(new RoomAgentRequest {version=1,sceneRevision=editor.Revision,commands=new[] {new RoomAgentCommand {action="create",kind="recipe",reference="robot",name="Program robot",recipe=recipe}}},out var error,out var created),Is.True,error);
            string target=created.Single();var geometry=editor.Find(target).GetComponent<RecipeObject>();
            var json=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-prime.json")));
            json["resources"]=new JArray(target);
            foreach(var token in new[] {json["functions"][0]["body"][1]["then"][0],json["functions"][0]["body"][1]["else"][0]}) {
                token["capability"]="animation.recipe.play";token["arguments"]=new JObject {["target"]=target,["seconds"]=.8f,["loop"]=true};
            }
            var sequence=new RuleSequence {id="",name="Programmed wave",program=json.ToString(Newtonsoft.Json.Formatting.None)};
            var request=new RuleRequest {action="edit",revision=workshop.Revision,edits=new[] {
                new RuleEdit {kind="save",reference="wave",sequence=sequence},
                new RuleEdit {kind="bind",binding=new RuleBinding {sequenceId="wave",trigger=RuleEventKind.Speaking,stopOnExit=true}},
                new RuleEdit {kind="button",target="wave",mount=ButtonMount.LeftController}}};
            Assert.That(executor.Execute(new RoomAgentRequest {version=2,commands=new[] {new RoomAgentCommand {action="rules",rule=request}}},out error,out created),Is.True,error);
            string id=created.Single();var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);
            void Evidence(string phase) {
                string output=Environment.GetEnvironmentVariable("MAESTRO_PROGRAM_EVIDENCE");if(string.IsNullOrEmpty(output))return;Directory.CreateDirectory(output);
                var state=observer.Observe();state.visible=true;state.workspaceView="rules";state.rules=workshop.Observe(true);
                File.WriteAllText(Path.Combine(output,"program-"+phase+".json"),RoomAgentWire.Serialize(state));
            }
            runtime.ObserveSnapshot(new BookSnapshot {activity="idle"});runtime.ObserveSnapshot(new BookSnapshot {activity="speaking"});
            for(int i=0;i<20&&!geometry.IsPlaying;i++)yield return null;
            Assert.That(geometry.IsPlaying,Is.True,runtime.Scheduler.LastError);Assert.That(workshop.Observe().running.Single().nodeId,Is.EqualTo("prime_wave"));Evidence("running");
            var arm=geometry.Part("RightUpperArm");var rotation=arm.localRotation;yield return new WaitForSeconds(.12f);Assert.That(Quaternion.Angle(rotation,arm.localRotation),Is.GreaterThan(.1f));
            runtime.ObserveSnapshot(new BookSnapshot {activity="idle"});Assert.That(geometry.IsPlaying,Is.False);Assert.That(runtime.Scheduler.Outcomes.Last().phase,Is.EqualTo("cancelled"));Evidence("cancelled");
            var button=root.GetComponentInChildren<RuleButton>();var router=root.AddComponent<BookPointerRouter>();router.Editor=editor;yield return null;Physics.SyncTransforms();
            var ray=new Ray(button.transform.position-Vector3.forward*.3f,Vector3.forward);Assert.That(router.Begin(0,ray),Is.False);Assert.That(router.Begin(1,ray),Is.True);router.End(1,ray);
            for(int i=0;i<20&&!geometry.IsPlaying;i++)yield return null;Assert.That(geometry.IsPlaying,Is.True);
            yield return new WaitForSeconds(.95f);Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That(runtime.Scheduler.Outcomes.Last().phase,Is.EqualTo("completed"));Evidence("completed");
            Assert.That(editor.Read(target).recipe.playing,Is.False);
            var saved=workshop.Selected;string original=saved.program;Assert.That(runtime.Trigger(id),Is.True);saved.name="Changed program";
            Assert.That(workshop.Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[] {new RuleEdit {kind="save",sequence=saved}}},out error,out _),Is.True,error);
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero,"Editing stops the previous program revision");workshop.Undo();Assert.That(workshop.Selected.name,Is.EqualTo("Programmed wave"));Assert.That(workshop.Selected.program,Is.EqualTo(original));
            workshop.SendMessage("OnApplicationPause",true);var restored=new RuleStorage(directory).Load(out error);Assert.That(restored.sequences.Single(x=>x.id==id).program,Is.EqualTo(original),error);workshop.SendMessage("OnApplicationPause",false);Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
            Assert.That(workshop.Execute(new RuleRequest {action="play",revision=workshop.Revision,target=id},out error,out _),Is.True,error);
            for(int i=0;i<20&&!geometry.IsPlaying;i++)yield return null;Assert.That(geometry.IsPlaying,Is.True);runtime.SendMessage("OnApplicationPause",true);Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That(geometry.IsPlaying,Is.False);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            UnityEngine.Object.Destroy(root); yield return null;
            if (Directory.Exists(directory)) Directory.Delete(directory,true);
        }
    }
}
