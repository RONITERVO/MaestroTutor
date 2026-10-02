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
    public sealed partial class RoomRulesTests
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
            float memoryDeadline=Time.realtimeSinceStartup+5;while(!workshop.Memory.Ready&&Time.realtimeSinceStartup<memoryDeadline)yield return null;Assert.That(workshop.Memory.Ready,Is.True);Assert.That(workshop.Memory.Error,Is.Null);
            yield return null;
        }


        [UnityTest] public IEnumerator WorkspaceExportReceiptsWaitForPublicationAndReplayDoesNotExportTwice()
        {
            var controls=root.AddComponent<MovementControls>();controls.Initialize(root.GetComponent<RoomInteraction>(),editor,animations,null,runtime,workshop,null,null,()=>true,directory:directory);
            workshop.Modules.Flush();var export=root.AddComponent<Maestro.Quest.Persistence.WorkspaceExport>();
            var output=Path.Combine(directory,"exports");int publishes=0;string privatePath=null;
            using var release=new System.Threading.ManualResetEventSlim(false);
            using var entered=new System.Threading.ManualResetEventSlim(false);
            export.InitializeForTests(editor,workshop,controls,output,path=>{privatePath=path;System.Threading.Interlocked.Increment(ref publishes);entered.Set();if(!release.Wait(TimeSpan.FromSeconds(15)))throw new IOException("Test publisher timed out");return "Downloads/Maestro/"+Path.GetFileName(path);});
            var executor=new RoomAgentExecutor(editor);string id=runtime.Scheduler.Receipts.NextId;
            var request=new RoomAgentRequest {version=2,conditions=Array.Empty<RoomObjectCondition>(),commands=new[]{new RoomAgentCommand {action="execution",execution=new JObject {["operation"]="start",["runId"]=id,["call"]=new JObject {["id"]="workspace.archive.export",["version"]=1,["arguments"]=new JObject()}}}}};
            try {
                Assert.That(RoomControls.Capabilities(editor),Does.Contain("workspaceArchiveExport.v1"));Assert.That(executor.Execute(request,out var error,out _),Is.True,error);
                for(int i=0;i<300&&!entered.IsSet;i++)yield return null;Assert.That(entered.IsSet,Is.True);
                Assert.That((string)runtime.Scheduler.Invocation(id)["phase"],Is.EqualTo("preparing"));Assert.That(runtime.Scheduler.Invocation(id)["output"],Is.Null,"A closed private ZIP is not a publication receipt");
                Assert.That(export.CanStart(out error),Is.False);StringAssert.Contains("current workspace export",error);
                Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(publishes,Is.EqualTo(1));
                using(var input=File.OpenRead(privatePath))using(var staged=Maestro.Quest.Persistence.WorkspaceArchive.Stage(input,directory))Assert.That(staged.Receipt.Summary.Files,Is.GreaterThanOrEqualTo(5));
                release.Set();for(int i=0;i<300&&(string)runtime.Scheduler.Invocation(id)["phase"]=="preparing";i++)yield return null;
                var receipt=runtime.Scheduler.Invocation(id);Assert.That((string)receipt["phase"],Is.EqualTo("completed"),receipt.ToString());StringAssert.StartsWith("Downloads/Maestro/",(string)receipt["output"]["location"]);Assert.That((double)receipt["output"]["sizeKiB"],Is.GreaterThan(0));Assert.That(File.Exists(privatePath),Is.False);
                Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(publishes,Is.EqualTo(1));Assert.That(export.CanStart(out error),Is.True,error);
                string evidence=Environment.GetEnvironmentVariable("MAESTRO_WORKSPACE_EXPORT_EVIDENCE");if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"published-receipt.json"),receipt.ToString());}
            }finally{release.Set();}
        }
        [UnityTest] public IEnumerator FailedWorkspacePublicationKeepsLiveContentAndDoesNotClaimSaved()
        {
            var controls=root.AddComponent<MovementControls>();controls.Initialize(root.GetComponent<RoomInteraction>(),editor,animations,null,runtime,workshop,null,null,()=>true,directory:directory);
            workshop.Modules.Flush();var export=root.AddComponent<Maestro.Quest.Persistence.WorkspaceExport>();string privatePath=null;int attempts=0;var before=JsonUtility.ToJson(editor.Snapshot());
            export.InitializeForTests(editor,workshop,controls,Path.Combine(directory,"exports"),path=>{privatePath=path;attempts++;throw new IOException("Downloads did not confirm the file.");});
            var executor=new RoomAgentExecutor(editor);string id=runtime.Scheduler.Receipts.NextId;
            var request=new RoomAgentRequest {version=2,conditions=Array.Empty<RoomObjectCondition>(),commands=new[]{new RoomAgentCommand {action="execution",execution=new JObject {["operation"]="start",["runId"]=id,["call"]=new JObject {["id"]="workspace.archive.export",["version"]=1,["arguments"]=new JObject()}}}}};
            Assert.That(executor.Execute(request,out var error,out _),Is.True,error);for(int i=0;i<300&&(string)runtime.Scheduler.Invocation(id)["phase"]=="preparing";i++)yield return null;
            var receipt=runtime.Scheduler.Invocation(id);Assert.That((string)receipt["phase"],Is.EqualTo("failed"));Assert.That(receipt["output"],Is.Null);StringAssert.Contains("Downloads did not confirm",(string)receipt["status"]);Assert.That(File.Exists(privatePath),Is.False);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));
            Assert.That(executor.Execute(request,out error,out _),Is.False);Assert.That(attempts,Is.EqualTo(1));Assert.That(export.CanStart(out error),Is.True,error);
        }
        [UnityTest] public IEnumerator StoppingWorkspaceExportCannotRetractDispatchedPublication()
        {
            var controls=root.AddComponent<MovementControls>();controls.Initialize(root.GetComponent<RoomInteraction>(),editor,animations,null,runtime,workshop,null,null,()=>true,directory:directory);
            workshop.Modules.Flush();var export=root.AddComponent<Maestro.Quest.Persistence.WorkspaceExport>();bool published=false;string privatePath=null;
            using var release=new System.Threading.ManualResetEventSlim(false);using var entered=new System.Threading.ManualResetEventSlim(false);
            export.InitializeForTests(editor,workshop,controls,Path.Combine(directory,"exports"),path=>{privatePath=path;entered.Set();if(!release.Wait(TimeSpan.FromSeconds(15)))throw new IOException("Test publisher timed out");published=true;return "Downloads/Maestro/"+Path.GetFileName(path);});
            var executor=new RoomAgentExecutor(editor);string id=runtime.Scheduler.Receipts.NextId;
            var request=new RoomAgentRequest {version=2,conditions=Array.Empty<RoomObjectCondition>(),commands=new[]{new RoomAgentCommand {action="execution",execution=new JObject {["operation"]="start",["runId"]=id,["call"]=new JObject {["id"]="workspace.archive.export",["version"]=1,["arguments"]=new JObject()}}}}};
            try {
                Assert.That(executor.Execute(request,out var error,out _),Is.True,error);for(int i=0;i<300&&!entered.IsSet;i++)yield return null;Assert.That(entered.IsSet,Is.True);
                Assert.That(runtime.Scheduler.CancelInvocation(id,out error),Is.True,error);var receipt=runtime.Scheduler.Invocation(id);Assert.That((string)receipt["phase"],Is.EqualTo("cancelled"));StringAssert.Contains("may still appear",(string)receipt["status"]);Assert.That(receipt["output"],Is.Null);Assert.That(export.Busy,Is.True);
                release.Set();for(int i=0;i<300&&export.Busy;i++)yield return null;Assert.That(export.Busy,Is.False);Assert.That(published,Is.True);Assert.That(File.Exists(privatePath),Is.False);
                Assert.That((string)runtime.Scheduler.Invocation(id)["phase"],Is.EqualTo("cancelled"));Assert.That(runtime.Scheduler.Invocation(id)["output"],Is.Null);
            }finally{release.Set();}
        }

        [UnityTest] public IEnumerator NativeArchiveCapturesOneAcceptedWorkspaceAndFailureReleasesLibraryWriters()
        {
            var controls=root.AddComponent<MovementControls>();controls.Initialize(root.GetComponent<RoomInteraction>(),editor,animations,null,runtime,workshop,null,null,()=>true,directory:directory);
            workshop.Modules.Flush();var before=editor.Snapshot();var behaviourSource=workshop.Selected.program;var output=Path.Combine(directory,"exports");
            var capture=Maestro.Quest.Persistence.WorkspaceArchiveCapture.Start(editor,workshop,controls,output);
            string target=editor.Identity(block);Assert.That(editor.MoveObject(target,new Vector3(2,1,2),out var error),Is.True,error);workshop.NewSequence();
            while(!capture.IsCompleted)yield return null;
            Assert.That(capture.IsFaulted,Is.False,capture.Exception?.ToString());var result=capture.GetAwaiter().GetResult();Assert.That(File.Exists(result.Path),Is.True);
            using(var input=File.OpenRead(result.Path))using(var staged=Maestro.Quest.Persistence.WorkspaceArchive.Stage(input,directory)){
                var archivedRoom=new RoomStorage(staged.DirectoryPath).Load(out error);Assert.That(error,Is.Null);Assert.That(archivedRoom.objects.Single(x=>x.id==target).position,Is.EqualTo(before.objects.Single(x=>x.id==target).position));Assert.That(editor.Read(target).position,Is.EqualTo(new Vector3(2,1,2)));
                var archivedRules=new RuleStorage(staged.DirectoryPath).Load(out error);Assert.That(archivedRules.sequences.Single(x=>x.id==sequenceId).program,Is.EqualTo(behaviourSource));Assert.That(archivedRules.sequences.Length,Is.EqualTo(workshop.Snapshot().sequences.Length-1));
                Assert.That(runtime.Scheduler.ObserveRuns(),Is.Empty);
            }
            string blocked=Path.Combine(directory,"not-a-directory");File.WriteAllText(blocked,"leave this alone");var failed=Maestro.Quest.Persistence.WorkspaceArchiveCapture.Start(editor,workshop,controls,blocked);
            while(!failed.IsCompleted)yield return null;Assert.That(failed.IsFaulted,Is.True);Assert.That(File.ReadAllText(blocked),Is.EqualTo("leave this alone"));
            Assert.That(editor.Models.TryCaptureArchive(out var models),Is.True);models.Dispose();Assert.That(editor.Motions.TryCaptureArchive(out var motions),Is.True);motions.Dispose();
            string unavailable=Path.Combine(directory,"unavailable-controls");Directory.CreateDirectory(unavailable);File.WriteAllText(Path.Combine(unavailable,"controls.v2.json"),"{\"version\":99}");
            var separate=new GameObject("Unavailable controls");separate.transform.SetParent(root.transform,false);var badControls=separate.AddComponent<MovementControls>();badControls.Initialize(root.GetComponent<RoomInteraction>(),editor,animations,null,runtime,workshop,null,null,()=>true,directory:unavailable);
            Assert.Throws<InvalidOperationException>(()=>Maestro.Quest.Persistence.WorkspaceArchiveCapture.Start(editor,workshop,badControls,output),"Never archive default preferences substituted for unreadable storage");
        }

        [UnityTest] public IEnumerator ActualContactsWakeTypedProgramsAndDriveRecordedMotionThroughSharedNativeExecution()
        {
            RoomPhysicsLayers.Configure();
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform,false);
            floor.transform.position=new Vector3(0,-.1f,0);floor.transform.localScale=new Vector3(12,.2f,12);floor.layer=RoomPhysicsLayers.Scanned;
            string target=editor.Identity(block);var ballData=editor.Snapshot().objects.Single(x=>x.kind==RoomObjectKind.Ball);var ball=editor.Find(ballData.id);
            Assert.That(editor.MoveObject(target,new Vector3(3,.2f,3),out var error),Is.True,error);
            Assert.That(editor.ResizeObject(target,3,out error),Is.True,error);
            Assert.That(editor.MoveObject(ballData.id,new Vector3(3,1.2f,3),out error),Is.True,error);
            var rigid=ball.GetComponent<RigidRoomItem>();rigid.Configure(physics,ItemPhysics.Solid,.6f);
            var position=block.transform.localPosition;
            Assert.That(editor.SaveAnimation(target,new RoomMotion {frames=new[] {new MotionFrame {position=position},new MotionFrame {time=.4f,position=position+Vector3.right*.4f}}},null,false),Is.True);
            // SaveAnimation reconciles all entries. Apply the physics profile last.
            rigid.Configure(physics,ItemPhysics.Solid,.6f);
            var program=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-contact.json")));
            program["resources"]=new JArray(target);program["functions"][0]["body"][0]["body"][0]["source"]=ballData.id;
            ((JArray)program["functions"][0]["body"][0]["body"][1]["then"]).Add(new JObject {
                ["id"]="react",["op"]="invoke",["capability"]="animation.play",["version"]=1,
                ["arguments"]=new JObject {["target"]=target,["source"]=new JObject {["kind"]="recording"},["channel"]="wholeTarget",["seconds"]=.4f,["loop"]=false},["bindings"]=new JObject()});
            var sequence=new RuleSequence {id="",name="React to contacts",program=program.ToString(Newtonsoft.Json.Formatting.None)};
            var executor=new RoomAgentExecutor(editor);
            Assert.That(executor.Execute(new RoomAgentRequest {version=2,commands=new[] {new RoomAgentCommand {action="rules",rule=new RuleRequest {action="edit",revision=workshop.Revision,edits=new[] {new RuleEdit {kind="save",reference="watch",sequence=sequence}}}}}},out error,out var created),Is.True,error);
            string id=created.Single();string saved=workshop.Selected.program;
            var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);
            void Evidence(string phase) {
                string output=Environment.GetEnvironmentVariable("MAESTRO_EVENT_EVIDENCE");if(string.IsNullOrEmpty(output))return;Directory.CreateDirectory(output);
                var state=observer.Observe();state.visible=true;state.workspaceView="rules";state.rules=workshop.Observe(true);
                File.WriteAllText(Path.Combine(output,"contact-"+phase+".json"),RoomAgentWire.Serialize(state));
            }
            Assert.That(runtime.Trigger(id),Is.True);Evidence("waiting");
            Physics.SyncTransforms();physics.SetSurfaces(true,"Synthetic floor ready");physics.StartPhysics();
            for(int i=0;i<180&&block.transform.position.x<3.04f;i++)yield return new WaitForFixedUpdate();
            Assert.That(block.transform.position.x,Is.GreaterThan(3.04f),runtime.Scheduler.LastError??"A real collision should start the recorded movement");
            var run=runtime.Scheduler.ObserveRuns().Single();
            Assert.That(run.state.Single(x=>x.name=="lastKind").value,Is.EqualTo("object"));
            Assert.That(run.state.Single(x=>x.name=="lastOther").value,Is.EqualTo(target));
            Assert.That(double.Parse(run.state.Single(x=>x.name=="lastSpeed").value,System.Globalization.CultureInfo.InvariantCulture),Is.GreaterThan(1));
            Assert.That(run.locals.Single(x=>x.name=="source").value,Is.EqualTo(ballData.id));Evidence("reaction");
            Assert.That(workshop.Selected.program,Is.EqualTo(saved),"Observations and playback do not rewrite the program");
            runtime.StopAll();physics.PausePhysics();
            Assert.That(editor.MoveObject(target,new Vector3(5,.2f,3),out error),Is.True,error);
            Assert.That(editor.MoveObject(ballData.id,new Vector3(3,1.2f,3),out error),Is.True,error);
            rigid.Configure(physics,ItemPhysics.Solid,.6f);Physics.SyncTransforms();
            // Watch without invoking the recording this time; floor identity is
            // deliberately only scannedRoom, not an invented semantic wall/floor label.
            ((JArray)program["functions"][0]["body"][0]["body"][1]["then"]).Last.Remove();
            sequence=workshop.Selected;sequence.program=program.ToString(Newtonsoft.Json.Formatting.None);
            Assert.That(workshop.Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",sequence=sequence}}},out error,out _),Is.True,error);
            Assert.That(runtime.Trigger(id),Is.True);physics.StartPhysics();
            for(int i=0;i<180&&runtime.Scheduler.ObserveRuns().Single().state.Single(x=>x.name=="contacts").value=="0";i++)yield return new WaitForFixedUpdate();
            run=runtime.Scheduler.ObserveRuns().Single();Assert.That(run.state.Single(x=>x.name=="lastKind").value,Is.EqualTo("scannedRoom"));
            Assert.That(run.state.Single(x=>x.name=="lastOther").value,Is.EqualTo(""));Evidence("floor");
            physics.PausePhysics();var count=run.state.Single(x=>x.name=="contacts").value;yield return new WaitForSeconds(.1f);
            Assert.That(runtime.Scheduler.ObserveRuns().Single().state.Single(x=>x.name=="contacts").value,Is.EqualTo(count));
            root.SendMessage("OnApplicationPause",true,SendMessageOptions.DontRequireReceiver);Evidence("paused");
            root.SendMessage("OnApplicationPause",false,SendMessageOptions.DontRequireReceiver);yield return null;
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That(physics.Running,Is.False);
        }

        [UnityTest] public IEnumerator PhysicalSettlingPaintsTheBallAndANewThrowWakesTheSameCanonicalProgram()
        {
            RoomPhysicsLayers.Configure();var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform,false);floor.transform.position=new Vector3(3,-.1f,3);floor.transform.localScale=new Vector3(6,.2f,6);floor.layer=RoomPhysicsLayers.Scanned;
            var data=editor.Snapshot().objects.Single(x=>x.kind==RoomObjectKind.Ball);var ball=editor.Find(data.id);var rigid=ball.GetComponent<RigidRoomItem>();
            Assert.That(editor.MoveObject(data.id,new Vector3(3,1.2f,3),out var error),Is.True,error);
            Assert.That(editor.SetItemPhysics(data.id,new ObjectPhysicsSettings {mode="solid",shape="sphere",mass=.6f}),Is.True,editor.Status);
            string source=File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-physics-motion.json")).Replace(new string('b',32),data.id);
            var executor=new RoomAgentExecutor(editor);var sequence=new RuleSequence {id="",name="React to settling",program=source};
            Assert.That(executor.Execute(new RoomAgentRequest {version=2,commands=new[]{new RoomAgentCommand {action="rules",rule=new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",reference="motion",sequence=sequence}}}}}},out error,out var created),Is.True,error);
            string id=created.Single();int revision=workshop.Revision;var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);
            void Evidence(string phase){string output=Environment.GetEnvironmentVariable("MAESTRO_MOTION_EVENT_EVIDENCE");if(string.IsNullOrEmpty(output))return;Directory.CreateDirectory(output);var state=observer.Observe();state.visible=true;state.workspaceView="rules";state.rules=workshop.Observe(true);File.WriteAllText(Path.Combine(output,phase+".json"),RoomAgentWire.Serialize(state));}
            Assert.That(runtime.Trigger(id),Is.True);yield return new WaitForSeconds(.4f);
            Assert.That(runtime.Scheduler.ObserveRuns().Single().nodeId,Is.EqualTo("settling"),"Paused velocity zero must not be reported as landed");Evidence("paused-physics");
            Physics.SyncTransforms();physics.SetSurfaces(true,"Synthetic floor ready");physics.StartPhysics();Evidence("falling");
            for(int i=0;i<400&&!runtime.Scheduler.ObserveRuns().Any(r=>r.nodeId=="moving");i++)yield return new WaitForFixedUpdate();
            var run=runtime.Scheduler.ObserveRuns().Single();Assert.That(run.nodeId,Is.EqualTo("moving"),runtime.Scheduler.LastError);Assert.That(run.state.Single(x=>x.name=="landed").value,Is.EqualTo("True"));
            Assert.That(float.Parse(run.state.Single(x=>x.name=="quiet").value,System.Globalization.CultureInfo.InvariantCulture),Is.GreaterThanOrEqualTo(.3f));Assert.That(editor.Read(data.id).color.g,Is.EqualTo(.8f).Within(.001f));Assert.That(ball.transform.position.y,Is.LessThan(.15f));Evidence("settled");
            uint epoch=rigid.MotionRevision;Assert.That(rigid.Launch(new Vector3(0,2,0),Vector3.up),Is.True);Assert.That(rigid.MotionRevision,Is.EqualTo(epoch),"A physical impulse must not hide a motion transition by resetting the baseline");
            for(int i=0;i<80&&!runtime.Scheduler.ObserveRuns().Any(r=>r.nodeId=="finish");i++)yield return new WaitForFixedUpdate();
            run=runtime.Scheduler.ObserveRuns().Single();Assert.That(run.state.Single(x=>x.name=="movingAgain").value,Is.EqualTo("True"));Evidence("moving");Assert.That(workshop.Revision,Is.EqualTo(revision));Assert.That(workshop.Selected.program,Is.EqualTo(source));
            runtime.StopAll();physics.PausePhysics();Assert.That(runtime.Trigger(id),Is.True);yield return new WaitForSeconds(.4f);Assert.That(runtime.Scheduler.ObserveRuns().Single().nodeId,Is.EqualTo("settling"));
            root.SendMessage("OnApplicationPause",true,SendMessageOptions.DontRequireReceiver);Evidence("paused-app");root.SendMessage("OnApplicationPause",false,SendMessageOptions.DontRequireReceiver);yield return null;Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
            Assert.That(runtime.Trigger(id),Is.True);ball.gameObject.SetActive(false);yield return new WaitForSeconds(.15f);Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That(runtime.Scheduler.LastError,Does.Contain("disabled"));Evidence("missing");
        }

        [UnityTest] public IEnumerator ProximityCrossingsDriveRecordedMotionAndRetainTheSharedProgram()
        {
            var a=editor.Find("maestro");var b=editor.Find("book");a.transform.position=new Vector3(4,1,4);b.transform.position=a.transform.position+Vector3.right;
            string target=editor.Identity(block);var start=block.transform.localPosition;
            var program=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-proximity.json")));program["resources"]=new JArray(target);
            ((JArray)program["functions"][0]["body"][0]["body"][1]["then"]).Add(new JObject {
                ["id"]="react",["op"]="invoke",["capability"]="animation.play",["version"]=1,
                ["arguments"]=new JObject {["target"]=target,["source"]=new JObject {["kind"]="recording"},["channel"]="wholeTarget",["seconds"]=.4,["loop"]=false},["bindings"]=new JObject()});
            var sequence=new RuleSequence {id="",name="React to distance",program=program.ToString(Newtonsoft.Json.Formatting.None)};var executor=new RoomAgentExecutor(editor);
            Assert.That(executor.Execute(new RoomAgentRequest {version=2,commands=new[] {new RoomAgentCommand {action="rules",rule=new RuleRequest {action="edit",revision=workshop.Revision,edits=new[] {new RuleEdit {kind="save",reference="near",sequence=sequence}}}}}},out var error,out var created),Is.True,error);
            string id=created.Single(),saved=workshop.Selected.program;int revision=workshop.Revision;
            var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);
            void Evidence(string phase) {
                string output=Environment.GetEnvironmentVariable("MAESTRO_EVENT_EVIDENCE");if(string.IsNullOrEmpty(output))return;Directory.CreateDirectory(output);
                var state=observer.Observe();state.visible=true;state.workspaceView="rules";state.rules=workshop.Observe(true);
                File.WriteAllText(Path.Combine(output,"proximity-"+phase+".json"),RoomAgentWire.Serialize(state));
            }
            Assert.That(runtime.Trigger(id),Is.True);Evidence("waiting");yield return new WaitForSeconds(.15f);
            Assert.That(runtime.Scheduler.ObserveRuns().Single().state.Single(x=>x.name=="crossings").value,Is.EqualTo("0"));
            b.transform.position=a.transform.position+Vector3.right*.3f;
            for(int i=0;i<90&&block.transform.localPosition.x<start.x+.02f;i++)yield return new WaitForSeconds(.02f);
            Assert.That(block.transform.localPosition.x,Is.GreaterThan(start.x+.02f),runtime.Scheduler.LastError??"Distance crossing should start the recording");
            Assert.That(runtime.Scheduler.ObserveRuns().Single().state.Single(x=>x.name=="inside").value,Is.EqualTo("True"));Evidence("reaction");
            yield return new WaitForSeconds(.5f);b.transform.position=a.transform.position+Vector3.right*.55f;yield return new WaitForSeconds(.15f);
            Assert.That(runtime.Scheduler.ObserveRuns().Single().state.Single(x=>x.name=="crossings").value,Is.EqualTo("1"));
            b.transform.position=a.transform.position+Vector3.right*.8f;yield return new WaitForSeconds(.15f);
            Assert.That(runtime.Scheduler.ObserveRuns().Single().state.Single(x=>x.name=="inside").value,Is.EqualTo("False"));Evidence("exit");
            Assert.That(workshop.Revision,Is.EqualTo(revision));Assert.That(workshop.Selected.program,Is.EqualTo(saved));
            runtime.SendMessage("OnApplicationPause",true);Evidence("paused");runtime.SendMessage("OnApplicationPause",false);yield return null;Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
            Assert.That(runtime.Trigger(id),Is.True);b.gameObject.SetActive(false);yield return new WaitForSeconds(.15f);Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That(runtime.Scheduler.LastError,Does.Contain("disabled"));Evidence("missing");
        }

        [UnityTest] public IEnumerator BookRecoveryStopsActualOneOffMotionAndPreservesObjectsAndSavedBehaviours()
        {
            var executor=new RoomAgentExecutor(editor);var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);
            string target=editor.Identity(block);
            var call=new JObject {["id"]="animation.play",["version"]=1,["arguments"]=new JObject {["target"]=target,["seconds"]=2,["loop"]=false,["source"]=new JObject {["kind"]="recording"},["channel"]="wholeTarget"}};
            var receipts=runtime.Scheduler.Receipts;
            Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out var oldId,out _,receipts.NextId),Is.True);
            Vector3 before=block.transform.position;yield return new WaitForSeconds(.12f);Assert.That(Vector3.Distance(before,block.transform.position),Is.GreaterThan(.005f));
            string pending=Path.Combine(directory,"action-receipts.v1.json.pending");Directory.CreateDirectory(pending);
            var wait=new JObject {["id"]="time.wait",["version"]=1,["arguments"]=new JObject {["seconds"]=1}};
            Assert.That(runtime.Scheduler.Invoke(wait,Time.unscaledTime,out _,out _,receipts.NextId),Is.False);
            string token=(string)receipts.RecoveryView["id"];string saved=JsonUtility.ToJson(workshop.Snapshot());int objects=editor.Snapshot().objects.Length;
            void Evidence(string phase,string status=null){
                string output=Environment.GetEnvironmentVariable("MAESTRO_PROGRAM_EVIDENCE");if(string.IsNullOrEmpty(output))return;Directory.CreateDirectory(output);
                var state=observer.Observe();state.visible=true;state.workspaceView="rules";if(status!=null)state.status=status;
                File.WriteAllText(Path.Combine(output,"native-recovery-"+phase+".json"),RoomAgentWire.Serialize(state));
            }
            Evidence("error");
            var command=new JObject {["action"]="execution",["execution"]=new JObject {["operation"]="recover",["recoveryId"]=token}};
            var wire=new JObject {["version"]=2,["commands"]=new JArray(command)};
            Assert.That(RoomControls.ValidWire(wire.ToString()),Is.True);var request=JsonUtility.FromJson<RoomAgentRequest>(wire.ToString());Assert.That(RoomAgentWire.PopulateStructured(request,wire),Is.True);
            Assert.That(executor.Execute(request,out _,out _),Is.False,"A directory still blocks actual journal I/O");
            before=block.transform.position;yield return new WaitForSeconds(.12f);Assert.That(Vector3.Distance(before,block.transform.position),Is.LessThan(.001f));
            Directory.Delete(pending);
            Assert.That(executor.Execute(request,out var error,out _),Is.True,error);Evidence("success",error);
            Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(objects));Assert.That(JsonUtility.ToJson(workshop.Snapshot()),Is.EqualTo(saved));
            Assert.That(runtime.Scheduler.Invocation(oldId),Is.Null);
            Assert.That(runtime.Scheduler.Invoke(call,Time.unscaledTime,out _,out _,receipts.NextId),Is.True);
            Assert.That(executor.Execute(request,out error,out _),Is.True,error,"A duplicate recovery cannot stop newly started motion");
            before=block.transform.position;yield return new WaitForSeconds(.12f);Assert.That(Vector3.Distance(before,block.transform.position),Is.GreaterThan(.005f));
        }

        [UnityTest] public IEnumerator UnavailableSavedProgramRemainsRepairableWithoutBlockingPhysicalButtonsOrOtherPrograms()
        {
            var original=workshop.Snapshot();var good=original.sequences.Single();
            var bad=good.Copy();bad.id=Guid.NewGuid().ToString("N");bad.name="Needs repair";
            var unsupported=JObject.Parse(bad.program);unsupported["functions"][0]["body"][0]["capability"]="future.animation.play";bad.program=unsupported.ToString();
            string preserved=bad.program;
            UnityEngine.Object.Destroy(runtime);UnityEngine.Object.Destroy(workshop);yield return null;
            var document=new RuleDocument {sequences=new[]{bad,good},bindings=new[]{bad,good}.Select(x=>new RuleBinding {id=Guid.NewGuid().ToString("N"),sequenceId=x.id,trigger=RuleEventKind.Speaking}).ToArray(),
                buttons=new[]{new RuleButtonData {id=Guid.NewGuid().ToString("N"),sequenceId=bad.id,mount=ButtonMount.LeftController,position=new Vector3(-.12f,.06f,.05f)}}};
            Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"behaviours.v2.json"),JsonUtility.ToJson(document));
            workshop=root.AddComponent<RuleWorkshop>();workshop.Initialize(editor,directory);
            runtime=root.AddComponent<RoomRules>();runtime.Initialize(workshop,editor,animations,null,root.GetComponent<RoomInteraction>(),null,index=>index==0?leftAnchor.transform:rightAnchor.transform);
            var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);
            var state=observer.Observe();state.visible=true;state.workspaceView="rules";state.rules=workshop.Observe(true);
            Assert.That(state.rules.readOnly,Is.False);Assert.That(state.rules.selected.program,Is.EqualTo(preserved));Assert.That(state.rules.selectedError,Is.Not.Empty);
            Assert.That(state.rules.sequences.Single(x=>x.id==good.id).error,Is.Null);
            string output=Environment.GetEnvironmentVariable("MAESTRO_PROGRAM_EVIDENCE");
            if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"program-unavailable.json"),RoomAgentWire.Serialize(state));}
            runtime.ObserveSnapshot(new BookSnapshot {activity="idle"});runtime.ObserveSnapshot(new BookSnapshot {activity="speaking"});
            Assert.That(runtime.Scheduler.ObserveRuns().Single().sequenceId,Is.EqualTo(good.id));
            var button=root.GetComponentInChildren<RuleButton>();var router=root.AddComponent<BookPointerRouter>();router.Editor=editor;
            yield return null;Physics.SyncTransforms();
            var ray=new Ray(button.transform.position-Vector3.forward*.3f,Vector3.forward);
            Assert.That(router.Begin(1,ray),Is.True);router.End(1,ray);
            Assert.That(runtime.Scheduler.ObserveRuns().Single().sequenceId,Is.EqualTo(good.id));Assert.That(runtime.Scheduler.LastError,Is.Not.Empty);
            good.name="Edited working behaviour";
            Assert.That(workshop.Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",sequence=good}}},out var error,out _),Is.True,error);
            Assert.That(workshop.Snapshot().sequences.First().program,Is.EqualTo(preserved));
            bad.program=good.program;
            Assert.That(workshop.Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",sequence=bad}}},out error,out _),Is.True,error);
            Assert.That(workshop.Observe().selectedError,Is.Null);Assert.That(runtime.Trigger(bad.id),Is.True);
            workshop.Undo();Assert.That(workshop.Snapshot().sequences.First().program,Is.EqualTo(preserved));Assert.That(runtime.Trigger(bad.id),Is.False);
            workshop.Redo();Assert.That(runtime.Trigger(bad.id),Is.True);
            Assert.That(workshop.Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="delete",target=bad.id}}},out error,out _),Is.True,error);
            Assert.That(workshop.Snapshot().sequences.Single().id,Is.EqualTo(good.id));
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
            var definition=Maestro.Quest.Programs.BehaviourCatalog.Action("object.create");
            source["functions"][0]["locals"][0]["name"]="robot";
            var create=source["functions"][0]["body"][0];create["capability"]=definition.Id;create["arguments"]=((JObject)definition.InputSchema["oneOf"][1]["examples"][0]);create["results"]["objectId"]="robot";
            var play=source["functions"][0]["body"][1];play["id"]="animate";play["capability"]="animation.play";play["bindings"]["target"]["var"]="robot";
            play["arguments"]=new JObject {["target"]=new string('0',32),["seconds"]=.6,["loop"]=true,["source"]=new JObject {["kind"]="recipe"},["channel"]="wholeTarget"};
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
            var definition=Maestro.Quest.Programs.BehaviourCatalog.Action("object.create");
            // Model the actual JSON wire request, including float-to-JSON conversion.
            var call=JObject.Parse(new JObject {["id"]=definition.Id,["version"]=1,["arguments"]=((JObject)definition.InputSchema["oneOf"][1]["examples"][0])}.ToString());
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

        JObject ObjectEditCall(string capability,string target,params (string key,JToken value)[] values) {
            var args=new JObject {["target"]=target};foreach(var value in values)args[value.key]=value.value;
            if(capability=="animation.play") {args["source"]=new JObject {["kind"]="recording"};args["channel"]="wholeTarget";}
            return new JObject {["id"]=capability,["version"]=1,["arguments"]=args};
        }
        RoomAgentRequest ObjectEditRequest(JObject call) {
            string target=(string)call["arguments"]["target"];
            return new RoomAgentRequest {version=2,conditions=new[]{new RoomObjectCondition {id=target,revision=editor.ObjectRevision(target)}},
                commands=new[]{new RoomAgentCommand {action="execution",execution=new JObject {["operation"]="start",["runId"]=runtime.Scheduler.Receipts.NextId,["call"]=call}}}};
        }
        void EditEvidence(string phase,RoomAgentExecutor executor,JObject program=null) {
            string directory=Environment.GetEnvironmentVariable("MAESTRO_OBJECT_EDIT_EVIDENCE");if(string.IsNullOrEmpty(directory))return;
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory,phase+".json"),new JObject {["program"]=program,["execution"]=executor.Executions.Observe(),
                ["room"]=JObject.Parse(JsonUtility.ToJson(editor.Snapshot()))}.ToString());
        }
        [UnityTest] public IEnumerator CreatedResultsComposeWithSavedObjectEditsWithoutStoppingUnrelatedMotion()
        {
            var source=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-create.json")));
            var body=(JArray)source["functions"][0]["body"];body.RemoveAt(1);
            void Add(string node,string capability,JObject args) {
                args["target"]=new string('0',32);body.Add(new JObject {["id"]=node,["op"]="invoke",["capability"]=capability,["version"]=1,
                    ["arguments"]=args,["bindings"]=new JObject {["target"]=new JObject {["var"]="ball"}}});
            }
            Add("paint","object.color.set",new JObject {["red"]=1,["green"]=.2,["blue"]=.1});
            Add("resize","object.scale.set",new JObject {["scale"]=1.5});
            Add("move","object.position.set",new JObject {["x"]=.2,["y"]=1.7,["z"]=1});
            var sequence=new RuleSequence {id="",name="Make a red ball",program=source.ToString(Newtonsoft.Json.Formatting.None)};
            Assert.That(workshop.Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",reference="ball",sequence=sequence}}},out var error,out var ids),Is.True,error);
            Assert.That(runtime.Scheduler.Invoke(ObjectEditCall("animation.play",editor.Identity(block),("seconds",2),("loop",false)),Time.unscaledTime,out var recording,out error),Is.True,error);
            Assert.That(runtime.Trigger(ids.Single()),Is.True,runtime.Scheduler.LastError);
            string created=runtime.Scheduler.ObserveRuns().Single().locals.Single(x=>x.name=="ball").value;
            for(int i=0;i<5;i++)runtime.Scheduler.Tick(Time.unscaledTime);
            Assert.That(runtime.Scheduler.Outcomes.Last().phase,Is.EqualTo("completed"),runtime.Scheduler.LastError);
            var item=editor.Find(created);Assert.That(item.transform.localPosition,Is.EqualTo(new Vector3(.2f,1.7f,1)));
            Assert.That(item.transform.localScale.x,Is.EqualTo(1.5f));Assert.That(editor.Read(created).color,Is.EqualTo(new Color(1,.2f,.1f,1)));
            var saved=new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==created);
            Assert.That(saved.position,Is.EqualTo(item.transform.localPosition));Assert.That(saved.color,Is.EqualTo(editor.Read(created).color));Assert.That(saved.scale,Is.EqualTo(1.5f));
            Assert.That((string)runtime.Scheduler.Invocation(recording)["phase"],Is.EqualTo("running"));
            var before=block.transform.position;yield return new WaitForSeconds(.12f);Assert.That(block.transform.position.x,Is.GreaterThan(before.x));

            var executor=new RoomAgentExecutor(editor);var request=ObjectEditRequest(ObjectEditCall("object.delete",created));
            Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(editor.Find(created),Is.Null);EditEvidence("deleted",executor,source);
            string run=(string)request.commands[0].execution["runId"];
            Assert.That((string)new InvocationReceipts(directory).Find(run)["phase"],Is.EqualTo("completed"));
            Assert.That(new RoomStorage(directory).Load(out _).objects.Any(x=>x.id==created),Is.False);
            editor.Undo();Assert.That(editor.Find(created),Is.Not.Null);Assert.That(editor.Read(created).color,Is.EqualTo(saved.color));
            Assert.That(executor.Execute(request,out error,out _),Is.True,error);
            Assert.That(editor.Find(created),Is.Not.Null,"Replay of the completed deletion must not delete the restored object");
            editor.Redo();Assert.That(editor.Find(created),Is.Null);
            Assert.That(runtime.Scheduler.Invoke(ObjectEditCall("object.position.set",created,("x",0),("y",1),("z",0)),Time.unscaledTime,out _,out _),Is.False);
        }
        [UnityTest] public IEnumerator NamedOnlyRotationSharesSavedProgramsReceiptsPhysicsAndUndo()
        {
            Assert.That(Maestro.Quest.Programs.LegacyCapabilityAdapters.Kind("object.rotation.set"),Is.Null);
            physics.SetSurfaces(true,"Ready");physics.StartPhysics();
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Rotate me",new Vector3(0,1.6f,1),1,Color.white,out var id,out var error),Is.True,error);
            var item=editor.Find(id);var body=item.GetComponent<Rigidbody>();yield return new WaitForFixedUpdate();
            Assert.That(item.GetComponent<RigidRoomItem>().Launch(Vector3.right,Vector3.up),Is.True);yield return new WaitForFixedUpdate();
            // Undo restores the preceding journal pose, not an unsaved physics frame.
            var position=item.transform.localPosition;var scale=item.transform.localScale;var original=editor.Read(id).rotation;
            var executor=new RoomAgentExecutor(editor);var call=ObjectEditCall("object.rotation.set",id,("pitch",20),("yaw",90),("roll",-10));
            Assert.That(runtime.Scheduler.Invoke(ObjectEditCall("animation.play",editor.Identity(block),("seconds",2),("loop",false)),Time.unscaledTime,out var recording,out error),Is.True,error);
            var request=ObjectEditRequest(call);
            Assert.That(executor.Execute(request,out error,out _),Is.True,error);var expected=Quaternion.Euler(20,90,-10);
            Assert.That(Quaternion.Angle(item.transform.localRotation,expected),Is.LessThan(.01));
            Assert.That(item.transform.localPosition,Is.EqualTo(position));Assert.That(item.transform.localScale,Is.EqualTo(scale));
            Assert.That(body.linearVelocity,Is.EqualTo(Vector3.zero));Assert.That(body.angularVelocity,Is.EqualTo(Vector3.zero));Assert.That(body.useGravity,Is.True);
            Assert.That((string)executor.Executions.Observe()["selected"]["phase"],Is.EqualTo("completed"));
            Assert.That((string)runtime.Scheduler.Invocation(recording)["phase"],Is.EqualTo("running"));
            Assert.That(Quaternion.Angle(new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==id).rotation,expected),Is.LessThan(.01));
            editor.Undo();Assert.That(Quaternion.Angle(item.transform.localRotation,original),Is.LessThan(.01));
            Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(Quaternion.Angle(item.transform.localRotation,original),Is.LessThan(.01),"Receipt replay cannot rotate an undone object");
            editor.Redo();Assert.That(Quaternion.Angle(item.transform.localRotation,expected),Is.LessThan(.01));
            var source=File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-rotation.json"));var sequence=new RuleSequence {id="",name="Rotate module",program=source};
            Assert.That(workshop.Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",reference="rotation",sequence=sequence}}},out error,out var ids),Is.True,error);
            Assert.That(runtime.Trigger(ids.Single()),Is.True,runtime.Scheduler.LastError);runtime.Scheduler.Tick(Time.unscaledTime);
            Assert.That(runtime.Scheduler.Outcomes.Last().phase,Is.EqualTo("completed"));
            Assert.That(Quaternion.Angle(editor.Find("book").transform.localRotation,expected),Is.LessThan(.01));
            yield return new WaitForFixedUpdate();Assert.That(body.linearVelocity.y,Is.LessThan(0));
        }
        [UnityTest] public IEnumerator RotationModuleRejectsInvalidHeldConflictingAndUnsavedEdits()
        {
            var executor=new RoomAgentExecutor(editor);string id=editor.Identity(block);
            var call=ObjectEditCall("object.rotation.set",id,("pitch",0),("yaw",90),("roll",0));
            var stale=ObjectEditRequest(call);stale.conditions[0].revision--;
            Assert.That(executor.Execute(stale,out _,out _),Is.False);
            var invalid=(JObject)call.DeepClone();invalid["arguments"]["yaw"]=181;
            Assert.That(runtime.Scheduler.Invoke(invalid,Time.unscaledTime,out _,out _),Is.False);
            var hand=Hand(1,block.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,block.Grab);
            Assert.That(executor.Execute(ObjectEditRequest(call),out _,out _),Is.False);manager.SelectExit((IXRSelectInteractor)hand,block.Grab);yield return null;
            Assert.That(runtime.Trigger(sequenceId),Is.True);Assert.That(executor.Execute(ObjectEditRequest(call),out _,out _),Is.False);runtime.Scheduler.StopAll();
            string before=JsonUtility.ToJson(editor.Read(id));var orientation=block.transform.localRotation;
            Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"room.v999.json"),"preserve newer room");
            Assert.That(executor.Execute(ObjectEditRequest(call),out _,out _),Is.False);
            Assert.That(JsonUtility.ToJson(editor.Read(id)),Is.EqualTo(before));Assert.That(block.transform.localRotation,Is.EqualTo(orientation));
            Assert.That((string)executor.Executions.Observe()["selected"]["phase"],Is.EqualTo("failed"));
        }

        [UnityTest] public IEnumerator PaintPreservesLivePhysicsAndPlacementResetsMotionWithExactUndo()
        {
            physics.SetSurfaces(true,"Ready");physics.StartPhysics();
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Ball,"Moving ball",new Vector3(0,1.5f,1),1,Color.white,out var id,out var error),Is.True,error);
            var item=editor.Find(id);var rigid=item.GetComponent<RigidRoomItem>();var body=item.GetComponent<Rigidbody>();
            yield return new WaitForFixedUpdate();Assert.That(rigid.Launch(Vector3.right,Vector3.up),Is.True);
            yield return new WaitForFixedUpdate();
            var position=item.transform.localPosition;var velocity=body.linearVelocity;var spin=body.angularVelocity;
            var executor=new RoomAgentExecutor(editor);var paint=ObjectEditRequest(ObjectEditCall("object.color.set",id,("red",.2),("green",.4),("blue",1)));
            Assert.That(executor.Execute(paint,out error,out _),Is.True,error);
            Assert.That(item.transform.localPosition,Is.EqualTo(position));Assert.That(body.linearVelocity,Is.EqualTo(velocity));Assert.That(body.angularVelocity,Is.EqualTo(spin));
            Assert.That(editor.Read(id).position,Is.EqualTo(position));EditEvidence("painted",executor);
            int revision=editor.ObjectRevision(id);Assert.That(executor.Execute(paint,out error,out _),Is.True,error);Assert.That(editor.ObjectRevision(id),Is.EqualTo(revision));
            var move=ObjectEditRequest(ObjectEditCall("object.position.set",id,("x",.5),("y",2),("z",1)));
            Assert.That(executor.Execute(move,out error,out _),Is.True,error);
            Assert.That(item.transform.localPosition,Is.EqualTo(new Vector3(.5f,2,1)));Assert.That(body.linearVelocity,Is.EqualTo(Vector3.zero));
            Assert.That(body.angularVelocity,Is.EqualTo(Vector3.zero));Assert.That(body.useGravity,Is.True);
            editor.Undo();Assert.That(item.transform.localPosition,Is.EqualTo(position));Assert.That(editor.Read(id).color,Is.EqualTo(new Color(.2f,.4f,1,1)));
            yield return new WaitForFixedUpdate();Assert.That(body.linearVelocity.y,Is.LessThan(0));
        }
        [UnityTest] public IEnumerator ObjectEditsRejectStaleHeldBusyProtectedAndUnsavableTargets()
        {
            var executor=new RoomAgentExecutor(editor);string id=editor.Identity(block);string before=JsonUtility.ToJson(editor.Read(id));
            var request=ObjectEditRequest(ObjectEditCall("object.color.set",id,("red",1),("green",0),("blue",0)));
            request.conditions[0].revision--;
            Assert.That(executor.Execute(request,out var error,out _),Is.False);Assert.That(JsonUtility.ToJson(editor.Read(id)),Is.EqualTo(before));
            request.conditions[0].revision=editor.ObjectRevision(id);
            Assert.That(runtime.Trigger(sequenceId),Is.True);Assert.That(executor.Execute(request,out error,out _),Is.False);Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));
            runtime.Scheduler.StopAll();
            var hand=Hand(1,block.transform.position);manager.SelectEnter((IXRSelectInteractor)hand,block.Grab);
            Assert.That(executor.Execute(request,out error,out _),Is.False);manager.SelectExit((IXRSelectInteractor)hand,block.Grab);
            yield return null;
            Assert.That(executor.Execute(ObjectEditRequest(ObjectEditCall("object.delete","book")),out error,out _),Is.False);
            Assert.That(executor.Execute(ObjectEditRequest(ObjectEditCall("object.scale.set","book",("scale",4))),out error,out _),Is.False);
            Assert.That(executor.Execute(ObjectEditRequest(ObjectEditCall("object.scale.set","book",("scale",1.2))),out error,out _),Is.True,error);
            Assert.That(editor.Find("book").transform.localScale.x,Is.EqualTo(1.2f));
            before=JsonUtility.ToJson(editor.Read(id));
            Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"room.v999.json"),"preserve newer save");
            var failing=ObjectEditRequest(ObjectEditCall("object.color.set",id,("red",1),("green",0),("blue",0)));
            Assert.That(executor.Execute(failing,out error,out _),Is.False);Assert.That(JsonUtility.ToJson(editor.Read(id)),Is.EqualTo(before));
            Assert.That((string)executor.Executions.Observe()["selected"]["phase"],Is.EqualTo("failed"));
            Assert.That(File.ReadAllText(Path.Combine(directory,"room.v999.json")),Is.EqualTo("preserve newer save"));
        }

        JObject CreationCall() {
            var program=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-create.json")));
            return new JObject {["id"]="object.create",["version"]=1,["arguments"]=program["functions"][0]["body"][0]["arguments"].DeepClone()};
        }
        [UnityTest] public IEnumerator CreatedObjectCollectionsDriveActualNativeEditsAndPublishTypedState()
        {
            string source=File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-collections.json"));
            var sequence=new RuleSequence {id="",name="Collection painting",program=source};
            Assert.That(workshop.Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",reference="collection",sequence=sequence}}},out var error,out var ids),Is.True,error);
            int count=editor.Snapshot().objects.Length;Assert.That(runtime.Trigger(ids.Single()),Is.True,runtime.Scheduler.LastError);
            for(int i=0;i<60&&!runtime.Scheduler.ObserveRuns().Any(r=>r.waiting);i++)yield return null;
            var run=runtime.Scheduler.ObserveRuns().Single();Assert.That(run.waiting,Is.True,runtime.Scheduler.LastError);
            var state=run.state.Single(v=>v.name=="items");Assert.That(state.type,Is.EqualTo("list"));var items=JArray.Parse(state.value);Assert.That(items.Count,Is.EqualTo(2));
            Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+2));
            foreach(var item in items){var id=(string)item["id"];Assert.That(editor.Find(id),Is.Not.Null);Assert.That(editor.Read(id).color.r,Is.EqualTo(.2f).Within(.001f));Assert.That(editor.Read(id).color.g,Is.EqualTo(.4f).Within(.001f));}
            Assert.That((double)items[0]["red"],Is.EqualTo(.2));Assert.That((double)JArray.Parse(run.locals.Single(v=>v.name=="copy").value)[0]["red"],Is.EqualTo(.9));
            string output=Environment.GetEnvironmentVariable("MAESTRO_DATA_EVIDENCE");if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"collections.json"),new JObject {["program"]=JObject.Parse(source),["rules"]=JObject.Parse(JsonUtility.ToJson(workshop.Observe(true))),["objects"]=JArray.FromObject(items.Select(v=>JObject.Parse(JsonUtility.ToJson(editor.Read((string)v["id"])))))}.ToString());}
            Assert.That(runtime.Scheduler.StopSequence(ids.Single()),Is.True);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+2),"Stopping never erases completed creations");
            workshop.SendMessage("OnApplicationPause",true);
            Assert.That(new RuleStorage(directory).Load(out error).sequences.Single(v=>v.id==ids.Single()).program,Is.EqualTo(source));
        }
        [UnityTest] public IEnumerator CreationResultChainsIntoRealPhysicsWithoutInterruptingAnotherObject()
        {
            string source=File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-create.json"));
            var create=new RuleSequence {id="",name="Create then push",program=source};
            Assert.That(workshop.Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",reference="create",sequence=create}}},out var error,out var ids),Is.True,error);
            physics.SetSurfaces(true,"Ready");physics.StartPhysics();yield return new WaitForFixedUpdate();
            var executor=new RoomAgentExecutor(editor);
            var recording=new JObject {["id"]="animation.play",["version"]=1,["arguments"]=new JObject {["target"]=editor.Identity(block),["seconds"]=2,["loop"]=false,["source"]=new JObject {["kind"]="recording"},["channel"]="wholeTarget"}};
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
        [UnityTest] public IEnumerator ModuleLibraryPublishesInspectsAndRemovesWithoutChangingPinnedRuns()
        {
            for(int i=0;i<120&&!workshop.Modules.Ready;i++)yield return null;
            Assert.That(workshop.Modules.Ready,Is.True);
            var executor=new RoomAgentExecutor(editor);var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);
            bool Rule(RuleRequest rule,out string error)=>executor.Execute(new RoomAgentRequest {version=2,commands=new[]{new RoomAgentCommand {action="rules",rule=rule}}},out error,out _);
            bool Call(string id,JObject args,out string error)=>executor.Execute(new RoomAgentRequest {version=2,conditions=Array.Empty<RoomObjectCondition>(),commands=new[]{new RoomAgentCommand {action="execution",execution=new JObject {["operation"]="start",["runId"]=runtime.Scheduler.Receipts.NextId,["call"]=new JObject {["id"]=id,["version"]=1,["arguments"]=args}}}}},out error,out _);
            bool Query(JObject query,out string error)=>executor.Execute(new RoomAgentRequest {version=2,commands=new[]{new RoomAgentCommand {action="catalog",catalog=query}}},out error,out _);
            void Evidence(string phase){string output=Environment.GetEnvironmentVariable("MAESTRO_LIBRARY_EVIDENCE");if(string.IsNullOrEmpty(output))return;Directory.CreateDirectory(output);var state=observer.Observe();state.rules=workshop.Observe(true);state.catalog=executor.Catalog.Observe();state.execution=executor.Executions.Observe();state.visible=true;state.workspaceView="rules";File.WriteAllText(Path.Combine(output,phase+".json"),RoomAgentWire.Serialize(state));}
            var source=workshop.Selected;source.name="Remember amounts";source.repeat=false;source.program=File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-declarations.json"));
            Assert.That(Rule(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",sequence=source}}},out var error),Is.True,error);
            var args=new JObject {["sequenceId"]=source.id,["rulesRevision"]=workshop.Revision,["name"]="Remember amounts",["exports"]=new JArray("remember")};
            Assert.That(Call("program.module.publish",args,out error),Is.True,error);
            for(int i=0;i<120&&(string)executor.Executions.Observe()["selected"]?["phase"]!="completed";i++)yield return null;
            var receipt=executor.Executions.Observe()["selected"];Assert.That((string)receipt["phase"],Is.EqualTo("completed"),receipt.ToString());string hash=(string)receipt["output"]["hash"];
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Evidence("published");
            Assert.That(Query(JObject.Parse(@"{'operation':'search','category':'modules','query':'remember','offset':0}"),out error),Is.True,error);
            Assert.That((int)executor.Catalog.Observe()["total"],Is.EqualTo(1));Evidence("search");
            var inspect=new JObject {["operation"]="inspect",["category"]="modules",["capability"]=hash,["version"]=1};
            Assert.That(Query(inspect,out error),Is.True,error);var module=(JObject)executor.Catalog.Observe()["definition"];Evidence("inspected");
            var changed=JObject.Parse(source.program);changed["state"][0]["initial"]=10;source.program=changed.ToString();
            Assert.That(Rule(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",sequence=source}}},out error),Is.True,error);
            Assert.That(Call("program.module.publish",args,out error),Is.False,"Stale saved revision cannot publish a different source");
            args["rulesRevision"]=workshop.Revision;Assert.That(Call("program.module.publish",args,out error),Is.True,error);
            for(int i=0;i<120&&(string)executor.Executions.Observe()["selected"]?["phase"]!="completed";i++)yield return null;
            receipt=executor.Executions.Observe()["selected"];Assert.That((string)receipt["phase"],Is.EqualTo("completed"));string second=(string)receipt["output"]["hash"];Assert.That(second,Is.Not.EqualTo(hash));Assert.That(workshop.Modules.Count,Is.EqualTo(2));
            var caller=JObject.Parse(@"{'version':3,'moduleVersion':1,'dataVersion':1,'entry':'main','resources':[],'state':[],'events':[{'name':'user.request','type':'number'},{'name':'user.total','type':'number'}],'functions':[{'name':'main','returns':'void','parameters':[],'locals':[],'body':[{'id':'first','op':'call','module':'counter','function':'remember','args':[{'value':2}]},{'id':'second','op':'call','module':'counter','function':'remember','args':[{'value':4}]},{'id':'wait','op':'sleep','seconds':{'value':30}}]}]}");
            caller["imports"]=new JArray(new JObject {["alias"]="counter",["hash"]=hash,["module"]=module.DeepClone(),["signals"]=new JObject {["user.add"]="user.request",["user.stored"]="user.total"}});
            Assert.That(Rule(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",reference="caller",sequence=new RuleSequence {id="",name="Use pinned counter",program=caller.ToString()}}}},out error),Is.True,error);
            string callerId=workshop.Selected.id;Assert.That(Rule(new RuleRequest {action="play",revision=workshop.Revision,target=callerId},out error),Is.True,error);
            for(int i=0;i<60&&!runtime.Scheduler.ObserveRuns().Any(r=>r.nodeId=="wait");i++)yield return null;
            var run=runtime.Scheduler.ObserveRuns().Single();Assert.That(run.state.Single(x=>x.name=="counter.total").value,Is.EqualTo("6"),"New publication must not replace the pinned initial state");Evidence("running");
            string saved=workshop.Selected.program;Assert.That(Call("program.module.remove",new JObject {["hash"]=hash},out error),Is.True,error);
            for(int i=0;i<120&&(string)executor.Executions.Observe()["selected"]?["phase"]!="completed";i++)yield return null;
            Assert.That((string)executor.Executions.Observe()["selected"]["phase"],Is.EqualTo("completed"));Assert.That(workshop.Modules.Inspect(hash),Is.Null);
            Assert.That(runtime.Scheduler.ObserveRuns().Single().id,Is.EqualTo(run.id));Assert.That(workshop.Selected.program,Is.EqualTo(saved));Assert.That(executor.Catalog.Observe()["definition"].Type,Is.EqualTo(JTokenType.Null),"Cached inspection must refresh after deletion");Evidence("removed");
            workshop.SendMessage("OnApplicationPause",true);var restored=new RuleStorage(directory).Load(out error);Assert.That(restored.sequences.Single(x=>x.id==callerId).program,Is.EqualTo(saved));
            var library=new Maestro.Quest.Programs.ProgramModuleLibrary(directory);library.Flush();Assert.That(library.Count,Is.EqualTo(1));Assert.That(library.Inspect(second),Is.Not.Null);Assert.That(library.Inspect(hash),Is.Null);
            workshop.SendMessage("OnApplicationPause",false);Assert.That(Rule(new RuleRequest {action="play",revision=workshop.Revision,target=callerId},out error),Is.True,error);
            for(int i=0;i<60&&!runtime.Scheduler.ObserveRuns().Any(r=>r.nodeId=="wait");i++)yield return null;
            Assert.That(runtime.Scheduler.ObserveRuns().Single().state.Single(x=>x.name=="counter.total").value,Is.EqualTo("6"));
        }

        [UnityTest] public IEnumerator PortableModuleImportUsesReceiptsWithoutStartingTheImportedProgram()
        {
            for(int i=0;i<120&&!workshop.Modules.Ready;i++)yield return null;
            Assert.That(workshop.Modules.Ready,Is.True);
            var executor=new RoomAgentExecutor(editor);
            var module=Maestro.Quest.Programs.ProgramModuleLibrary.Definition("{\"version\":3,\"entry\":\"main\",\"resources\":[],\"state\":[],\"events\":[],\"functions\":[{\"name\":\"main\",\"returns\":\"void\",\"parameters\":[],\"locals\":[],\"body\":[{\"id\":\"sleep\",\"op\":\"sleep\",\"seconds\":{\"value\":10}}]}]}","Portable counter",new[]{"main"});
            module["program"]["state"]=new JArray(new JObject {["name"]="words",["initial"]=new string('ä',126)},new JObject {["name"]="savedAt",["initial"]="2026-09-30T12:34:56Z"});
            module["program"]["functions"][0]["body"]=new JArray(new JObject {["id"]="inside_module",["op"]="invoke",["capability"]="time.wait",["version"]=1,["arguments"]=new JObject {["seconds"]=10},["bindings"]=new JObject()});
            for(int depth=0;depth<7;depth++)module["program"]["functions"][0]["body"]=new JArray(new JObject {["id"]="if_"+depth,["op"]="if",["test"]=new JObject {["value"]=true},["then"]=module["program"]["functions"][0]["body"].DeepClone(),["else"]=new JArray()});
            string hash=Maestro.Quest.Programs.ProgramModules.Hash(module),id=runtime.Scheduler.Receipts.NextId;
            var request=new RoomAgentRequest {version=2,conditions=Array.Empty<RoomObjectCondition>(),commands=new[]{new RoomAgentCommand {action="execution",execution=new JObject {["operation"]="start",["runId"]=id,["call"]=new JObject {["id"]="program.module.import",["version"]=1,["arguments"]=new JObject {["hash"]=hash,["definition"]=module}}}}}};
            Assert.That(RoomControls.ValidWire(new JObject {["commands"]=new JArray(new JObject {["action"]="execution",["execution"]=request.commands[0].execution.DeepClone()})}.ToString()),Is.True,"Wire validation must preserve date-looking text and deep module documents");
            Assert.That(executor.Execute(request,out var error,out _),Is.True,error);
            for(int i=0;i<180&&(string)runtime.Scheduler.Invocation(id)["phase"]!="completed";i++)yield return null;
            Assert.That((string)runtime.Scheduler.Invocation(id)["phase"],Is.EqualTo("completed"));Assert.That((string)runtime.Scheduler.Invocation(id)["output"]["hash"],Is.EqualTo(hash));
            Assert.That(workshop.Modules.Count,Is.EqualTo(1));Assert.That(runtime.Scheduler.ObserveRuns(),Is.Empty,"Import is storage, not execution of its entry function");
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_MODULE_FILE_EVIDENCE");
            if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"module-file.json"),new JObject {["format"]="maestro-program-module",["version"]=1,["hash"]=hash,["definition"]=workshop.Modules.Inspect(hash).ReadDefinition()}.ToString());File.WriteAllText(Path.Combine(evidence,"receipt.json"),runtime.Scheduler.Invocation(id).ToString());}
            int revision=workshop.Modules.Revision;Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(workshop.Modules.Revision,Is.EqualTo(revision),"Same receipt does not replay the import");
            request.commands[0].execution["runId"]=runtime.Scheduler.Receipts.NextId;request.commands[0].execution["call"]["arguments"]["hash"]=new string('0',64);
            Assert.That(executor.Execute(request,out error,out _),Is.False);StringAssert.Contains("identity",error);Assert.That(workshop.Modules.Count,Is.EqualTo(1));
            var reloaded=new Maestro.Quest.Programs.ProgramModuleLibrary(directory);reloaded.Flush();Assert.That(JToken.DeepEquals(reloaded.Inspect(hash).ReadDefinition(),module),Is.True);
            var author=(JObject)module["program"].DeepClone();author["functions"][0]["body"]=new JArray(new JObject {["id"]="import_file",["op"]="invoke",["capability"]="program.module.import",["version"]=1,["arguments"]=new JObject {["hash"]=hash,["definition"]=module.DeepClone()},["bindings"]=new JObject()});
            var sequence=workshop.Selected;sequence.program=author.ToString();
            Assert.That(executor.Execute(new RoomAgentRequest {version=2,commands=new[]{new RoomAgentCommand {action="rules",rule=new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",sequence=sequence}}}}}},out error,out _),Is.True,error);
            var quick=new CapabilityQuickEdit(workshop,editor);quick.Step(1);Assert.That(quick.NodeId,Is.EqualTo("import_file"),"Embedded module actions are data, not quick-edit blocks");
            quick.FieldStep(1);Assert.That(quick.FieldPath,Is.EqualTo("definition"));quick.Adjust(1);Assert.That(quick.Dirty,Is.False);StringAssert.Contains("book",quick.Status);

        }

        [UnityTest] public IEnumerator PinnedModulesRunSaveRejectTamperingAndReloadThroughTheSharedExecutor()
        {
            string source=File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-modules-nested.json"));
            var sequence=workshop.Selected;sequence.program=source;sequence.repeat=false;sequence.name="Pinned counters";
            var executor=new RoomAgentExecutor(editor);var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);
            bool Execute(RuleRequest rule,out string error)=>executor.Execute(new RoomAgentRequest {version=2,commands=new[]{new RoomAgentCommand {action="rules",rule=rule}}},out error,out _);
            Assert.That(Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",sequence=sequence}}},out var error),Is.True,error);
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero);int revision=workshop.Revision;string saved=JsonUtility.ToJson(workshop.Snapshot());
            Assert.That(Execute(new RuleRequest {action="play",revision=revision,target=sequence.id},out error),Is.True,error);
            for(int i=0;i<60&&!runtime.Scheduler.ObserveRuns().Any(r=>r.nodeId=="wait");i++)yield return null;
            var run=runtime.Scheduler.ObserveRuns().Single();Assert.That(run.nodeId,Is.EqualTo("wait"));Assert.That(run.state.Single(v=>v.name=="first.inner.count").value,Is.EqualTo("3"));Assert.That(run.state.Single(v=>v.name=="second.count").value,Is.EqualTo("5"));
            Assert.That(JsonUtility.ToJson(workshop.Snapshot()),Is.EqualTo(saved));
            string output=Environment.GetEnvironmentVariable("MAESTRO_MODULE_EVIDENCE");if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);var state=observer.Observe();state.rules=workshop.Observe(true);state.visible=true;state.workspaceView="rules";File.WriteAllText(Path.Combine(output,"running.json"),RoomAgentWire.Serialize(state));}
            var corrupted=workshop.Selected;var changed=JObject.Parse(source);changed["imports"][0]["module"]["name"]="Unexpected replacement";corrupted.program=changed.ToString();
            Assert.That(Execute(new RuleRequest {action="edit",revision=revision,edits=new[]{new RuleEdit {kind="save",sequence=corrupted}}},out error),Is.False);Assert.That(error,Does.Contain("pinned hash"));Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));Assert.That(workshop.Revision,Is.EqualTo(revision));
            Assert.That(Execute(new RuleRequest {action="stop"},out error),Is.True,error);Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
            workshop.SendMessage("OnApplicationPause",true);var restored=new RuleStorage(directory).Load(out error);Assert.That(restored.sequences.Single(x=>x.id==sequence.id).program,Is.EqualTo(source));
            workshop.SendMessage("OnApplicationPause",false);
            Assert.That(Execute(new RuleRequest {action="play",revision=revision,target=sequence.id},out error),Is.True,error);runtime.SendMessage("OnApplicationPause",true);runtime.SendMessage("OnApplicationPause",false);yield return null;Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
        }

        [UnityTest] public IEnumerator VisuallyAuthoredDeclarationsShareSignalsAndTypedStateWithoutSavingRuntimeValues()
        {
            string source=File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-declarations.json"));
            var sender=workshop.Selected;sender.program=source;sender.repeat=false;sender.name="Remember amounts";
            var executor=new RoomAgentExecutor(editor);var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);
            bool Execute(RuleRequest rule,out string error)=>executor.Execute(new RoomAgentRequest {version=2,commands=new[]{new RoomAgentCommand {action="rules",rule=rule}}},out error,out _);
            Assert.That(Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",sequence=sender}}},out var error),Is.True,error);
            var listener=JObject.Parse(@"{'version':3,'entry':'main','resources':[],'state':[{'name':'latest','initial':0}],'events':[{'name':'user.stored','type':'number'}],'functions':[{'name':'main','returns':'void','parameters':[],'locals':[{'name':'received','initial':false},{'name':'value','initial':0}],'body':[{'id':'loop','op':'forever','body':[{'id':'wait','op':'awaitEvent','event':'user.stored','source':'','timeout':{'value':0},'received':'received','value':'value'},{'id':'store','op':'setState','variable':'latest','value':{'var':'value'}}]}]}]}");
            Assert.That(Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",reference="listener",sequence=new RuleSequence {id="",name="Observe total",program=listener.ToString()}}}},out error),Is.True,error);
            string listenerId=workshop.Selected.id;Assert.That(runtime.Scheduler.RunningCount,Is.Zero,"Saving declarations must not start either subscriber");
            var saved=JsonUtility.ToJson(workshop.Snapshot());int revision=workshop.Revision;
            Assert.That(Execute(new RuleRequest {action="play",revision=revision,target=listenerId},out error),Is.True,error);
            Assert.That(Execute(new RuleRequest {action="play",revision=revision,target=sender.id},out error),Is.True,error);
            Execute(new RuleRequest {action="inspect",target=sender.id},out _);
            void Evidence(string phase){string output=Environment.GetEnvironmentVariable("MAESTRO_DECLARATIONS_EVIDENCE");if(string.IsNullOrEmpty(output))return;Directory.CreateDirectory(output);var state=observer.Observe();state.rules=workshop.Observe(true);state.visible=true;state.workspaceView="rules";File.WriteAllText(Path.Combine(output,phase+".json"),RoomAgentWire.Serialize(state));}
            string Value(string id,string name)=>runtime.Scheduler.ObserveRuns().Single(r=>r.sequenceId==id).state.Single(v=>v.name==name).value;
            Evidence("waiting");
            foreach(int amount in new[]{2,4}){
                Assert.That(Execute(new RuleRequest {action="signal",revision=revision,eventName="user.add",value=new JValue(amount)},out error),Is.True,error);
                string expected=amount==2?"2":"6";for(int i=0;i<60&&Value(listenerId,"latest")!=expected;i++)yield return null;
                Assert.That(Value(sender.id,"total"),Is.EqualTo(expected));Assert.That(Value(listenerId,"latest"),Is.EqualTo(expected),"The emitted signal must reach another running program");
            }
            CollectionAssert.AreEqual(new[]{2d,4d},JArray.Parse(Value(sender.id,"amounts")).Values<double>().ToArray());Evidence("received");
            Assert.That(JsonUtility.ToJson(workshop.Snapshot()),Is.EqualTo(saved),"Session state must not rewrite initial declarations");Assert.That(workshop.Revision,Is.EqualTo(revision));
            var conflicting=workshop.Snapshot().sequences.Single(x=>x.id==listenerId);listener["events"][0]["type"]="text";listener["functions"][0]["locals"][1]["initial"]="";((JArray)listener["functions"][0]["body"][0]["body"]).RemoveAt(1);conflicting.program=listener.ToString();
            Assert.That(Execute(new RuleRequest {action="edit",revision=revision,edits=new[]{new RuleEdit {kind="save",sequence=conflicting}}},out error),Is.False);Assert.That(error,Does.Contain("different payload type"));
            Assert.That(JsonUtility.ToJson(workshop.Snapshot()),Is.EqualTo(saved));Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(2),"A rejected conflicting edit must not interrupt valid runs");
            Assert.That(Execute(new RuleRequest {action="stop"},out error),Is.True,error);Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Evidence("stopped");
            Assert.That(Execute(new RuleRequest {action="play",revision=revision,target=sender.id},out error),Is.True,error);Assert.That(Value(sender.id,"total"),Is.EqualTo("0"));Assert.That(Value(sender.id,"amounts"),Is.EqualTo("[]"));
            runtime.SendMessage("OnApplicationPause",true);runtime.SendMessage("OnApplicationPause",false);yield return null;Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Evidence("paused");
            workshop.SendMessage("OnApplicationPause",true);var restored=new RuleStorage(directory).Load(out error);Assert.That(restored.sequences.Single(x=>x.id==sender.id).program,Is.EqualTo(source));
        }

        [UnityTest] public IEnumerator EventProgramsSaveSignalAnimateAndStopThroughTheSharedNativeExecutor()
        {
            string target=editor.SelectedId;var program=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-events.json")));
            program["resources"]=new JArray(target);
            var loop=(JArray)program["functions"][0]["body"][0]["body"];loop[0]["event"]="user.wave";loop[0]["source"]="";
            program["functions"][0]["locals"][1]["initial"]=0;
            loop[1]["then"][1]=new JObject {["id"]="wave",["op"]="invoke",["capability"]="animation.play",["version"]=1,["arguments"]=new JObject {["target"]=target,["seconds"]=.5,["loop"]=false,["source"]=new JObject {["kind"]="recording"},["channel"]="wholeTarget"},["bindings"]=new JObject()};
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
            var call=new JObject {["id"]="animation.play",["version"]=1,["arguments"]=new JObject {["target"]=target,["seconds"]=1,["loop"]=false,["source"]=new JObject {["kind"]="recording"},["channel"]="wholeTarget"}};
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
            var call=new JObject {["id"]="animation.play",["version"]=1,["arguments"]=new JObject {["target"]=selected,["seconds"]=1,["loop"]=false,["source"]=new JObject {["kind"]="recording"},["channel"]="wholeTarget"}};
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
            var request=new RoomAgentRequest {version=2,conditions=new[] {new RoomObjectCondition {id=target,revision=editor.ObjectRevision(target)-1}},commands=new[] {new RoomAgentCommand {action="execution",execution=new JObject {["operation"]="start",["call"]=new JObject {["id"]="animation.play",["version"]=1,["arguments"]=new JObject {["target"]=target,["seconds"]=1,["loop"]=false,["source"]=new JObject {["kind"]="recording"},["channel"]="wholeTarget"}}}}}};
            Assert.That(executor.Execute(request,out var error,out _),Is.False);Assert.That(error,Does.Contain("target changed"));Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
            request.conditions[0].revision=editor.ObjectRevision(target);Assert.That(runtime.Trigger(sequenceId),Is.True);
            Assert.That(executor.Execute(request,out error,out _),Is.False);Assert.That(error,Does.Contain("owns"));Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));
            runtime.StopAll();runtime.enabled=false;Assert.That(executor.Execute(request,out error,out _),Is.False);Assert.That(error,Does.Contain("paused"));runtime.enabled=true;
            var args=(JObject)request.commands[0].execution["call"]["arguments"];args["target"]="maestro";args["prop"]=new JObject {["objectId"]=target,["avatarHash"]="",["hand"]="right",["release"]="return",["offset"]=new JObject {["x"]=0,["y"]=0,["z"]=0},["rotation"]=new JObject {["x"]=0,["y"]=0,["z"]=0,["w"]=1},["releaseAt"]=1};
            Assert.That(Maestro.Quest.Programs.BehaviourCatalog.TryInvocation("animation.play",1,args,out _,out _),Is.True);
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
            var inspected=Query(new JObject {["operation"]="inspect",["capability"]="animation.play",["version"]=1},"inspect");
            Assert.That(JToken.DeepEquals(inspected["definition"],Maestro.Quest.Programs.BehaviourCatalog.Action("animation.play").ToJson()),Is.True);
            var check=new JObject {["operation"]="check",["call"]=new JObject {["id"]="animation.play",["version"]=1,["arguments"]=new JObject {["target"]=selected,["seconds"]=1,["loop"]=false,["source"]=new JObject {["kind"]="recording"},["channel"]="wholeTarget"}}};
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
        [UnityTest] public IEnumerator SharedConditionWaitObservesRealAnimationAndCancelsOnPauseOrMissingTarget()
        {
            string target=editor.Identity(block);var program=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-conditions.json")));
            program["state"][0]["initial"]=target;program["state"][1]["initial"]=block.transform.position.x+.04f;string source=program.ToString();
            var executor=new RoomAgentExecutor(editor);var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);
            Assert.That(executor.Execute(new RoomAgentRequest {version=2,commands=new[]{new RoomAgentCommand {action="rules",rule=new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",reference="watch",sequence=new RuleSequence {id="",name="Wait for object position",program=source}}}}}}},out var error,out var created),Is.True,error);
            string id=created.Single();int revision=editor.Revision,rulesRevision=workshop.Revision;string document=JsonUtility.ToJson(editor.Snapshot());
            void Evidence(string phase){string output=Environment.GetEnvironmentVariable("MAESTRO_CONDITION_EVIDENCE");if(string.IsNullOrEmpty(output))return;Directory.CreateDirectory(output);var state=observer.Observe();state.visible=true;state.workspaceView="rules";state.rules=workshop.Observe(true);File.WriteAllText(Path.Combine(output,phase+".json"),RoomAgentWire.Serialize(state));}
            Evidence("saved");Assert.That(runtime.Scheduler.RunningCount,Is.Zero,"Saving must not start the watcher");
            Assert.That(runtime.Trigger(id),Is.True);Evidence("waiting");Assert.That(runtime.Scheduler.ObserveRuns().Single().status,Is.EqualTo("Waiting for condition"));
            Assert.That(runtime.Trigger(sequenceId),Is.True,"A read-only condition must not own the animation target");yield return new WaitForSeconds(.85f);
            var run=runtime.Scheduler.ObserveRuns().Single(x=>x.sequenceId==id);Assert.That(run.nodeId,Is.EqualTo("finish"),runtime.Scheduler.LastError);Assert.That(run.state.Single(x=>x.name=="matched").value,Is.EqualTo("True"));Evidence("matched");
            Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(2));Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(workshop.Revision,Is.EqualTo(rulesRevision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(document));Assert.That(workshop.Selected.program,Is.EqualTo(source));
            runtime.StopAll();Assert.That(runtime.Trigger(id),Is.True);root.SendMessage("OnApplicationPause",true,SendMessageOptions.DontRequireReceiver);Evidence("paused");root.SendMessage("OnApplicationPause",false,SendMessageOptions.DontRequireReceiver);yield return null;Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Evidence("resumed");
            Assert.That(runtime.Trigger(id),Is.True);block.gameObject.SetActive(false);yield return new WaitForSeconds(.2f);Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That(runtime.Scheduler.LastError,Does.Contain("unavailable"));Evidence("missing");
            block.gameObject.SetActive(true);
        }
        [UnityTest] public IEnumerator ParameterizedFactsObserveAnimationWithoutOwningEditingOrFabricatingTargets()
        {
            string target=editor.Identity(block),source=File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-object-facts.json")).Replace("\"book\"","\""+target+"\"");
            var executor=new RoomAgentExecutor(editor);var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);
            Assert.That(executor.Execute(new RoomAgentRequest {version=2,commands=new[]{new RoomAgentCommand {action="rules",rule=new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",reference="read",sequence=new RuleSequence {id="",name="Observe position",program=source}}}}}}},out var error,out var created),Is.True,error);
            string id=created.Single();int revision=editor.Revision,rulesRevision=workshop.Revision;string document=JsonUtility.ToJson(editor.Snapshot());
            var query=new JObject {["operation"]="inspect",["category"]="facts",["capability"]="object.position",["version"]=1};
            JObject Evidence(string phase){var result=executor.Catalog.Observe();string output=Environment.GetEnvironmentVariable("MAESTRO_OBJECT_FACT_EVIDENCE");if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);var state=observer.Observe();state.catalog=result;state.visible=true;state.workspaceView="rules";state.rules=workshop.Observe(true);File.WriteAllText(Path.Combine(output,phase+".json"),RoomAgentWire.Serialize(state));}return result;}
            Assert.That(executor.Catalog.Execute(query,out error),Is.True,error);Assert.That((bool)Evidence("definition")["available"],Is.False);
            query["arguments"]=new JObject {["target"]=target};Assert.That(executor.Catalog.Execute(query,out error),Is.True,error);var before=Evidence("before");Assert.That((bool)before["available"],Is.True);Assert.That((float)before["value"]["x"],Is.EqualTo(block.transform.position.x).Within(.0001));
            Assert.That(runtime.Trigger(sequenceId),Is.True);Assert.That(runtime.Trigger(id),Is.True);yield return new WaitForSeconds(.5f);
            var run=runtime.Scheduler.ObserveRuns().Single(x=>x.sequenceId==id);Assert.That(run.nodeId,Is.EqualTo("finish"),runtime.Scheduler.LastError);Assert.That(run.state.Single(x=>x.name=="moved").value,Is.EqualTo("True"));
            var after=Evidence("moving");Assert.That((float)after["value"]["x"],Is.GreaterThan((float)before["value"]["x"]));Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(2),"Reading cannot interrupt the animator");
            Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(workshop.Revision,Is.EqualTo(rulesRevision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(document));Assert.That(workshop.Selected.program,Is.EqualTo(source));
            root.SendMessage("OnApplicationPause",true,SendMessageOptions.DontRequireReceiver);Assert.That((bool)Evidence("paused")["available"],Is.False);root.SendMessage("OnApplicationPause",false,SendMessageOptions.DontRequireReceiver);yield return null;Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That((bool)Evidence("resumed")["available"],Is.True);
            query["arguments"]["target"]=new string('e',32);Assert.That(executor.Catalog.Execute(query,out error),Is.True,error);Assert.That((bool)Evidence("missing")["available"],Is.False);
            query["arguments"]["target"]=target;Assert.That(executor.Catalog.Execute(query,out error),Is.True,error);block.gameObject.SetActive(false);Assert.That((bool)Evidence("disabled")["available"],Is.False);block.gameObject.SetActive(true);
            var bad=JObject.Parse(source);bad["state"][0]["initial"]=new string('e',32);var seq=workshop.Selected;seq.program=bad.ToString();Assert.That(workshop.Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",sequence=seq}}},out error,out _),Is.True,error);Assert.That(runtime.Trigger(id),Is.False,"An immediately unavailable fact must reject the run");yield return null;Assert.That(runtime.Scheduler.LastError,Does.Contain("unavailable"));Evidence("failed");
        }
        [UnityTest] public IEnumerator VocabularyDiscoveryReadsLiveFactsWithoutEditingOrInterruptingPlayback()
        {
            var executor=new RoomAgentExecutor(editor);var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);
            int roomRevision=editor.Revision,ruleRevision=workshop.Revision;string selected=editor.SelectedId,document=JsonUtility.ToJson(editor.Snapshot());
            JObject Evidence(string phase) {
                var result=executor.Catalog.Observe();string output=Environment.GetEnvironmentVariable("MAESTRO_CATALOG_EVIDENCE");
                if(!string.IsNullOrEmpty(output)) {Directory.CreateDirectory(output);var state=observer.Observe();state.catalog=result;File.WriteAllText(Path.Combine(output,"vocabulary-"+phase+".json"),RoomAgentWire.Serialize(state));}
                Assert.That(editor.Revision,Is.EqualTo(roomRevision));Assert.That(workshop.Revision,Is.EqualTo(ruleRevision));Assert.That(editor.SelectedId,Is.EqualTo(selected));
                Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(document));return result;
            }
            JObject Query(JObject query,string phase) {
                Assert.That(executor.Execute(new RoomAgentRequest {version=2,commands=new[]{new RoomAgentCommand {action="catalog",catalog=query}}},out var error,out var created),Is.True,error);
                Assert.That(created,Is.Empty);return Evidence(phase);
            }
            Assert.That(runtime.Trigger(sequenceId),Is.True);var before=block.transform.localPosition;
            var events=Query(new JObject {["operation"]="search",["category"]="events",["query"]="",["offset"]=0},"events");
            var next=Query(new JObject {["operation"]="search",["category"]="events",["query"]="",["offset"]=6},"events-next");
            Assert.That(events["entries"].Count()+next["entries"].Count(),Is.EqualTo(Maestro.Quest.Programs.BehaviourCatalog.Events.Count));
            var contact=Query(new JObject {["operation"]="inspect",["category"]="events",["capability"]="object.collided",["version"]=1},"contact");
            Assert.That((string)contact["definition"]["fields"]["properties"]["speed"]["type"],Is.EqualTo("number"));
            yield return new WaitForSeconds(.2f);Assert.That(block.transform.localPosition.x,Is.GreaterThan(before.x+.01f));
            Assert.That(runtime.Scheduler.RunningCount,Is.EqualTo(1));runtime.StopAll();
            Query(new JObject {["operation"]="search",["category"]="facts",["query"]="",["offset"]=0},"facts");
            var ready=Query(new JObject {["operation"]="inspect",["category"]="facts",["capability"]="physics.ready",["version"]=1},"false");
            Assert.That((bool)ready["available"],Is.True);Assert.That((bool)ready["value"],Is.False);
            physics.SetSurfaces(true,"Test surfaces");var refreshed=Evidence("true");Assert.That((bool)refreshed["available"],Is.True);Assert.That((bool)refreshed["value"],Is.True);
            Assert.That(physics.Running,Is.False,"Reading readiness never starts physics");
            runtime.enabled=false;var disabled=Evidence("disabled");Assert.That((bool)disabled["available"],Is.False);Assert.That(disabled["value"].Type,Is.EqualTo(JTokenType.Null));runtime.enabled=true;
            runtime.SendMessage("OnApplicationPause",true);Assert.That((bool)Evidence("paused")["available"],Is.False);runtime.SendMessage("OnApplicationPause",false);
            var state=Query(new JObject {["operation"]="inspect",["category"]="facts",["capability"]="maestro.state",["version"]=1},"unavailable");
            Assert.That((bool)state["available"],Is.False);Assert.That(state["value"].Type,Is.EqualTo(JTokenType.Null));
            runtime.ObserveSnapshot(new BookSnapshot {activity="speaking"});var speaking=Evidence("speaking");Assert.That((bool)speaking["available"],Is.True);Assert.That((string)speaking["value"],Is.EqualTo("speaking"));
            runtime.ObserveSnapshot(new BookSnapshot {activity="speaking",audioPaused=true});Assert.That((bool)Evidence("audio-paused")["available"],Is.False);
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero,"Inspecting events never creates a listener or starts a program");
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
            var claim=editor.Ownership.Observe().owners.Single(x=>x.role=="grab");Assert.That(claim.claims.Single().target,Is.EqualTo(editor.Identity(block)));
            var otherHand=Hand(2,current+Vector3.right*.1f-Vector3.forward*.2f);manager.SelectEnter((IXRSelectInteractor)otherHand,block.Grab);
            hand.gameObject.SetActive(false);yield return null;Assert.That(block.Grab.isSelected,Is.True);
            Assert.That(editor.Ownership.Observe().owners.Any(x=>x.id==claim.id),Is.True,"The second hand still owns the object");
            var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);
            void Evidence(string phase){var folder=Environment.GetEnvironmentVariable("MAESTRO_OWNERSHIP_EVIDENCE");if(string.IsNullOrEmpty(folder))return;Directory.CreateDirectory(folder);var state=observer.Observe();state.visible=true;state.workspaceView="rules";File.WriteAllText(Path.Combine(folder,phase+".json"),RoomAgentWire.Serialize(state));}
            Evidence("held");otherHand.gameObject.SetActive(false);yield return null;Evidence("released");
            Assert.That(editor.Ownership.Observe().owners.Any(x=>x.id==claim.id),Is.False);
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero,"Releasing hands cannot restart the cancelled animation");
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
                token["capability"]="animation.play";token["arguments"]=new JObject {["target"]=target,["seconds"]=.8f,["loop"]=true,["source"]=new JObject {["kind"]="recipe"},["channel"]="wholeTarget"};
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
        [UnityTest] public IEnumerator PhysicalCatalogFieldsEditNamedOnlyRotationAndOpenTheSameBookProgram()
        {
            var saved=workshop.Selected;saved.program=File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-rotation.json"));
            Assert.That(workshop.Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[] {new RuleEdit {kind="save",sequence=saved}}},out var error,out _),Is.True,error);
            int revision=workshop.Revision,scene=editor.Revision;var book=editor.Find("book");var originalPose=book.transform.localRotation;
            var agent=root.AddComponent<RoomAgent>();agent.Initialize(editor,null);
            var board=new GameObject("Catalog rule tray");board.transform.SetParent(root.transform,false);board.transform.position=new Vector3(2,1,0);
            var tools=board.AddComponent<RuleTools>();tools.Build(workshop,root.GetComponent<RoomInteraction>());
            var pointer=root.AddComponent<BookPointerRouter>();pointer.Editor=editor;
            IEnumerator Click(string label) {
                yield return new WaitForSecondsRealtime(.31f);Physics.SyncTransforms();
                var button=board.GetComponentsInChildren<RuleToolAction>().Single(x=>x.AccessibleName==label);
                var ray=new Ray(button.transform.position-Vector3.forward*.2f,Vector3.forward);
                Assert.That(pointer.Begin(1,ray),Is.True,label);pointer.End(1,ray);
            }
            Assert.That(tools.Draft.CapabilityId,Is.EqualTo("object.rotation.set"));
            Assert.That(tools.Draft.Summary,Does.Contain("Book"),"Object fields display names while retaining exact IDs");
            yield return Click("Next field");yield return Click("Next field");Assert.That(tools.Draft.FieldPath,Is.EqualTo("yaw"));
            yield return Click("Value +");Assert.That(tools.Draft.Dirty,Is.True);Assert.That(workshop.Revision,Is.EqualTo(revision));
            Assert.That(editor.Revision,Is.EqualTo(scene));Assert.That(book.transform.localRotation,Is.EqualTo(originalPose));
            yield return Click("Try action");Assert.That(runtime.Scheduler.RunningCount,Is.Zero);Assert.That(tools.Draft.Status,Does.Contain("Apply or discard"));
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_QUICK_EDIT_EVIDENCE");
            if(!string.IsNullOrEmpty(evidence)) {Directory.CreateDirectory(evidence);CaptureQuickEdit(board,Path.Combine(evidence,"rotation-draft.png"));}
            yield return Click("Apply draft");Assert.That(tools.Draft.Dirty,Is.False);Assert.That(workshop.Revision,Is.EqualTo(revision+1));
            var source=JObject.Parse(workshop.Selected.program);var node=source["functions"][0]["body"][0];
            Assert.That((string)node["id"],Is.EqualTo("block_1"));Assert.That((float)node["arguments"]["yaw"],Is.EqualTo(105));
            Assert.That(editor.Revision,Is.EqualTo(scene),"Applying a behaviour never runs its object edit");
            yield return Click("Edit behaviour in book");
            var view=agent.Observe();Assert.That(view.visible,Is.True);Assert.That(view.workspaceView,Is.EqualTo("rules"));Assert.That(view.rules.selected.program,Is.EqualTo(workshop.Selected.program));
            if(!string.IsNullOrEmpty(evidence))File.WriteAllText(Path.Combine(evidence,"book-after-native-edit.json"),RoomAgentWire.Serialize(view));
            yield return Click("Try action");yield return null;
            Assert.That(Quaternion.Angle(book.transform.localRotation,Quaternion.Euler(20,105,-10)),Is.LessThan(.02f));
            yield return Click("Undo rules");Assert.That(workshop.Selected.program,Is.EqualTo(saved.program));
            // The type selector is catalog-driven, including actions with no old enum adapter.
            var seen=new System.Collections.Generic.HashSet<string>();string first=tools.Draft.CapabilityId;
            do {seen.Add(tools.Draft.CapabilityId);tools.Draft.CycleCapability();}while(tools.Draft.CapabilityId!=first&&seen.Count<=Maestro.Quest.Programs.BehaviourCatalog.Actions.Count);
            Assert.That(seen,Is.EquivalentTo(Maestro.Quest.Programs.BehaviourCatalog.Actions.Select(x=>x.Id)));tools.Draft.Reload();
            yield return Click("Change numeric step");Assert.That(tools.Draft.PrecisionLabel,Is.EqualTo("0.01"));
            yield return Click("Next field");yield return Click("Next field");yield return Click("Value +");yield return Click("Apply draft");
            Assert.That((double)JObject.Parse(workshop.Selected.program)["functions"][0]["body"][0]["arguments"]["yaw"],Is.EqualTo(90.01).Within(.00001));
            yield return Click("Undo rules");Assert.That(workshop.Selected.program,Is.EqualTo(saved.program));
        }
        void CaptureQuickEdit(GameObject board,string path)
        {
            var cameraRoot=new GameObject("Quick edit verification camera");cameraRoot.transform.SetParent(root.transform,false);
            var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.85f,.89f,.91f);
            cameraRoot.transform.position=board.transform.position+new Vector3(0,0,-1.6f);cameraRoot.transform.LookAt(board.transform.position);camera.fieldOfView=38;
            var render=new RenderTexture(1200,1100,24);var pixels=new Texture2D(1200,1100,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try {camera.targetTexture=render;camera.Render();RenderTexture.active=render;pixels.ReadPixels(new Rect(0,0,1200,1100),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
            finally {RenderTexture.active=previous;camera.targetTexture=null;render.Release();UnityEngine.Object.Destroy(render);UnityEngine.Object.Destroy(pixels);UnityEngine.Object.Destroy(cameraRoot);}
        }
        [UnityTest] public IEnumerator NativeAnimationSourceChoiceSharesBookProgramAndUndoWithoutPlayback()
        {
            var saved=workshop.Selected;saved.program=Maestro.Quest.Programs.BehaviourProgram.FromSteps(new RuleStep {id="wave",action=RuleActionKind.Gesture,targetId="maestro",seconds=2,gesture=RuleGesture.Pointing,propId=editor.Identity(block)});
            Assert.That(workshop.Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",sequence=saved}}},out var error,out _),Is.True,error);
            string output=Environment.GetEnvironmentVariable("MAESTRO_QUICK_EDIT_EVIDENCE");
            if(!string.IsNullOrEmpty(output)) {Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"rule-with-prop.json"),JsonUtility.ToJson(workshop.Observe(true)));}
            var board=new GameObject("Animation source controls");board.transform.SetParent(root.transform,false);
            var tools=board.AddComponent<RuleTools>();tools.Build(workshop,root.GetComponent<RoomInteraction>());
            Assert.That(tools.Draft.FieldPath,Is.EqualTo("Source and channel"));
            var button=board.GetComponentsInChildren<RuleToolAction>().Single(x=>x.AccessibleName=="Value +");
            var router=root.AddComponent<BookPointerRouter>();router.Editor=editor;Physics.SyncTransforms();
            var ray=new Ray(button.transform.position-Vector3.forward*.25f,Vector3.forward);Assert.That(router.Begin(1,ray),Is.True);router.End(1,ray);
            Assert.That(tools.Draft.Dirty,Is.True);Assert.That(workshop.Selected.program,Is.EqualTo(saved.program));
            var changed=JObject.Parse(tools.Draft.ProgramSource);var args=changed["functions"][0]["body"][0]["arguments"];
            Assert.That((string)args["channel"],Is.EqualTo("upperBody"));Assert.That((string)args["source"]["gesture"],Is.EqualTo("pointing"));Assert.That((int)args["seconds"],Is.EqualTo(2));Assert.That(args["prop"],Is.Null);
            if(!string.IsNullOrEmpty(output))CaptureQuickEdit(board,Path.Combine(output,"animation-source-draft.png"));
            Assert.That(tools.Draft.Apply(),Is.True,tools.Draft.Status);Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
            var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);Assert.That(observer.OpenRules(saved.id,out error),Is.True,error);
            if(!string.IsNullOrEmpty(output))File.WriteAllText(Path.Combine(output,"animation-book.json"),RoomAgentWire.Serialize(observer.Observe()));
            workshop.Undo();Assert.That(workshop.Selected.program,Is.EqualTo(saved.program));yield return null;
        }
        [UnityTest] public IEnumerator NativeCreationKindChoicePreservesResultsAndOnlyCreatesWhenRun()
        {
            var saved=workshop.Selected;var source=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-create.json")));
            ((JArray)source["functions"][0]["body"]).RemoveAt(1);saved.program=source.ToString();
            Assert.That(workshop.Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[]{new RuleEdit {kind="save",sequence=saved}}},out var error,out _),Is.True,error);
            int count=editor.Snapshot().objects.Length;var before=editor.Snapshot().objects.Select(x=>x.id).ToArray();
            var board=new GameObject("Creation kind controls");board.transform.SetParent(root.transform,false);
            var tools=board.AddComponent<RuleTools>();tools.Build(workshop,root.GetComponent<RoomInteraction>());
            Assert.That(tools.Draft.FieldPath,Is.EqualTo("Creation kind"));
            var button=board.GetComponentsInChildren<RuleToolAction>().Single(x=>x.AccessibleName=="Value +");
            var router=root.AddComponent<BookPointerRouter>();router.Editor=editor;Physics.SyncTransforms();
            var ray=new Ray(button.transform.position-Vector3.forward*.25f,Vector3.forward);Assert.That(router.Begin(1,ray),Is.True);router.End(1,ray);
            Assert.That(tools.Draft.Dirty,Is.True);Assert.That(workshop.Selected.program,Is.EqualTo(saved.program));
            var draft=JObject.Parse(tools.Draft.ProgramSource);var node=draft["functions"][0]["body"][0];var original=source["functions"][0]["body"][0];
            Assert.That((string)node["arguments"]["kind"],Is.EqualTo("recipe"));Assert.That(node["arguments"]["shape"],Is.Null);Assert.That(node["arguments"]["red"],Is.Null);
            Assert.That(((JArray)node["arguments"]["recipe"]["parts"]).Count,Is.EqualTo(19));Assert.That((bool)node["arguments"]["recipe"]["playing"],Is.False);
            Assert.That(JToken.DeepEquals(node["results"],original["results"]),Is.True);Assert.That((string)node["id"],Is.EqualTo((string)original["id"]));
            foreach(string field in new[]{"name","x","y","z","scale"})Assert.That(JToken.DeepEquals(node["arguments"][field],original["arguments"][field]),Is.True,field);
            string output=Environment.GetEnvironmentVariable("MAESTRO_QUICK_EDIT_EVIDENCE");
            if(!string.IsNullOrEmpty(output)) {Directory.CreateDirectory(output);CaptureQuickEdit(board,Path.Combine(output,"creation-kind-draft.png"));}
            Assert.That(tools.Draft.Apply(),Is.True,tools.Draft.Status);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
            var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);Assert.That(observer.OpenRules(saved.id,out error),Is.True,error);
            if(!string.IsNullOrEmpty(output))File.WriteAllText(Path.Combine(output,"creation-book.json"),RoomAgentWire.Serialize(observer.Observe()));
            Assert.That(runtime.Trigger(saved.id),Is.True,runtime.Scheduler.LastError);yield return null;
            var created=editor.Snapshot().objects.Single(x=>!before.Contains(x.id));Assert.That(created.recipe.parts.Length,Is.EqualTo(19));Assert.That(editor.Find(created.id).GetComponent<RecipeObject>().IsPlaying,Is.False);
            var persisted=new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==created.id);Assert.That(JsonUtility.ToJson(created.recipe),Is.EqualTo(JsonUtility.ToJson(persisted.recipe)));
            editor.Undo();Assert.That(editor.Find(created.id),Is.Null);workshop.Undo();Assert.That(workshop.Selected.program,Is.EqualTo(saved.program));
        }
        [UnityTest] public IEnumerator NativeQuickEditsPreserveBranchesAndRefuseStaleExternalChanges()
        {
            var saved=workshop.Selected;saved.program=File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-prime.json"));
            Assert.That(workshop.Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[] {new RuleEdit {kind="save",sequence=saved}}},out var error,out _),Is.True,error);
            var draft=new CapabilityQuickEdit(workshop,editor);Assert.That(draft.NodeId,Is.EqualTo("prime_wave"));for(int i=0;i<draft.FieldCount&&draft.FieldPath!="seconds";i++)draft.FieldStep(1);draft.Adjust(1);
            var expected=JObject.Parse(saved.program);expected["functions"][0]["body"][1]["then"][0]["arguments"]["seconds"]=1.1;
            Assert.That(draft.Apply(),Is.True,draft.Status);Assert.That(JToken.DeepEquals(JObject.Parse(workshop.Selected.program),expected),Is.True);
            Assert.That(runtime.Scheduler.RunningCount,Is.Zero);workshop.Undo();draft.Refresh();Assert.That(workshop.Selected.program,Is.EqualTo(saved.program));
            for(int i=0;i<draft.FieldCount&&draft.FieldPath!="seconds";i++)draft.FieldStep(1);draft.Adjust(1);string pending=draft.ProgramSource;
            var external=workshop.Selected;external.name="Changed in book";
            Assert.That(workshop.Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[] {new RuleEdit {kind="save",sequence=external}}},out error,out _),Is.True,error);
            draft.Refresh();Assert.That(draft.Stale,Is.True);Assert.That(draft.Apply(),Is.False);Assert.That(draft.ProgramSource,Is.EqualTo(pending));Assert.That(draft.Dirty,Is.True);
            Assert.That(workshop.Selected.name,Is.EqualTo("Changed in book"));draft.Reload();Assert.That(draft.Stale,Is.False);Assert.That(draft.Dirty,Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator NativeQuickEditsKeepComputedResourcesAndResultWiringAndRejectInvalidDrafts()
        {
            var saved=workshop.Selected;saved.program=File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-create.json"));
            Assert.That(workshop.Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[] {new RuleEdit {kind="save",sequence=saved}}},out var error,out _),Is.True,error);
            var draft=new CapabilityQuickEdit(workshop,editor);string original=draft.ProgramSource;draft.CycleCapability();Assert.That(draft.ProgramSource,Is.EqualTo(original),"Type changes cannot erase a creation result");
            draft.Step(1);Assert.That(draft.FieldPath,Is.EqualTo("target"));draft.SetField();draft.Adjust(1);Assert.That(draft.ProgramSource,Is.EqualTo(original),"A computed target is not replaced by a room selection");
            draft.FieldStep(1);draft.Adjust(1);Assert.That(draft.Apply(),Is.True,draft.Status);
            var expected=JObject.Parse(saved.program);expected["functions"][0]["body"][1]["arguments"]["x"]=1.5;
            Assert.That(JToken.DeepEquals(JObject.Parse(workshop.Selected.program),expected),Is.True);
            Assert.That(((JArray)JObject.Parse(workshop.Selected.program)["resources"]).Count,Is.Zero,"Computed authority stays attached to the creation result");

            var prime=workshop.Selected;prime.program=File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-prime.json"));
            Assert.That(workshop.Execute(new RuleRequest {action="edit",revision=workshop.Revision,edits=new[] {new RuleEdit {kind="save",sequence=prime}}},out error,out _),Is.True,error);
            draft.Reload();for(int i=0;i<draft.FieldCount&&draft.FieldPath!="prop (include)";i++)draft.FieldStep(1);
            Assert.That(draft.FieldPath,Is.EqualTo("prop (include)"));draft.SetField();
            for(int i=0;i<draft.FieldCount&&draft.FieldPath!="prop.rotation.w";i++)draft.FieldStep(1);
            Assert.That(draft.FieldPath,Is.EqualTo("prop.rotation.w"));draft.Adjust(-1);int revision=workshop.Revision;
            Assert.That(draft.Apply(),Is.False,"A non-unit quaternion cannot reach saved programs");Assert.That(draft.Dirty,Is.True);
            Assert.That(workshop.Revision,Is.EqualTo(revision));Assert.That(workshop.Selected.program,Is.EqualTo(prime.program));Assert.That(runtime.Scheduler.RunningCount,Is.Zero);
            yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            UnityEngine.Object.Destroy(root); yield return null;
            if (Directory.Exists(directory)) Directory.Delete(directory,true);
        }
    }
}
