// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Book;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;

namespace Maestro.Quest.Tests
{
    public sealed class RoomEditorTests
    {
        GameObject root;
        string directory;
        RoomEditor editor;
        RoomInteraction room;
        RoomPhysicsWorld physics;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(),"MaestroEditorTests-"+Guid.NewGuid().ToString("N"));
            root = new GameObject("Room editor test"); root.AddComponent<XRInteractionManager>();
            room = root.AddComponent<RoomInteraction>();
            var book = Included("book"); var avatar = Included("maestro");
            physics = root.AddComponent<RoomPhysicsWorld>();
            editor = root.AddComponent<RoomEditor>(); editor.Initialize(room,book,avatar,directory,physics);
            yield return null;
        }
        RoomItem Included(string name)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube); item.name = name; item.transform.SetParent(root.transform,false);
            var roomItem = item.AddComponent<RoomItem>(); roomItem.Configure(new[] { item.GetComponent<Collider>() }); room.Register(roomItem); return roomItem;
        }

        [UnityTest] public IEnumerator AgentPhysicsUsesSharedSettingsAndOneUndoWithNoPartialInvalidBatch()
        {
            var agent=new RoomAgentExecutor(editor);
            bool Run(params RoomAgentCommand[] commands) => agent.Execute(new RoomAgentRequest {version=1,sceneRevision=editor.Revision,commands=commands},out _,out _);
            Assert.That(Run(new RoomAgentCommand {action="create",reference="ball",name="Agent test ball",kind="ball"},
                new RoomAgentCommand {action="physicsSettings",target="ball",physics=new ObjectPhysicsSettings {mode="solid",shape="box",mass=2}}),Is.True);
            var data=editor.Snapshot().objects.Single(x=>x.name=="Agent test ball");var id=data.id;
            Assert.That(data.physics,Is.EqualTo(ItemPhysics.Solid));Assert.That(data.mass,Is.EqualTo(2));Assert.That(data.collisionShape,Is.EqualTo(ItemCollider.Box));
            editor.Select(editor.Find(id));editor.CycleMass();Assert.That(editor.Read(id).mass,Is.EqualTo(5));
            editor.Undo();Assert.That(editor.Read(id).mass,Is.EqualTo(2));
            var before=JsonUtility.ToJson(editor.Snapshot());
            Assert.That(Run(new RoomAgentCommand {action="paint",target=id,color=Color.red},new RoomAgentCommand {action="physicsSettings",target=id,physics=new ObjectPhysicsSettings {mode="solid",shape="box",mass=100}}),Is.False);
            Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));
            Assert.That(Run(new RoomAgentCommand {action="physicsSettings",target="maestro",physics=new ObjectPhysicsSettings {mode="solid",shape="box",mass=1}}),Is.False);
            editor.Undo();Assert.That(editor.Find(id),Is.Null,"One undo removes the create/settings transaction");
            editor.Redo();Assert.That(editor.Read(id).mass,Is.EqualTo(2));
            Assert.That(Run(new RoomAgentCommand {action="physicsRun",operation="start"}),Is.False);Assert.That(physics.Running,Is.False);
            physics.SetSurfaces(true,"Synthetic aligned scan");
            Assert.That(Run(new RoomAgentCommand {action="physicsRun",operation="start"}),Is.True);Assert.That(physics.Running,Is.True);
            Assert.That(Run(new RoomAgentCommand {action="physicsRun",operation="pause"}),Is.True);Assert.That(physics.Running,Is.False);
            physics.SendMessage("OnApplicationFocus",false);Assert.That(Run(new RoomAgentCommand {action="physicsRun",operation="start"}),Is.False);
            physics.SendMessage("OnApplicationFocus",true);Assert.That(physics.Running,Is.False,"Focus recovery never restarts gravity");
            physics.SetSurfaces(false,"Lost alignment");Assert.That(Run(new RoomAgentCommand {action="physicsRun",operation="start"}),Is.False);
            var observer=root.AddComponent<RoomAgent>();observer.Initialize(editor,null);
            var state=observer.Observe();Assert.That(state.capabilities,Does.Contain("physicsSettings.v1"));Assert.That(state.capabilities,Does.Not.Contain("avatarMotion.v1"));
            Assert.That(state.physics.status,Is.EqualTo(physics.Status));
            var json=RoomAgentWire.Serialize(state);var evidence=Environment.GetEnvironmentVariable("MAESTRO_CONTROL_EVIDENCE");
            if(!string.IsNullOrEmpty(evidence)) {Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"native-physics-state.json"),json);}
            editor.SaveNow();yield return new WaitForSeconds(.3f);
            Assert.That(new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==id).mass,Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator DrawingPaintDuplicateEraseUndoAndReloadPreserveGeometry()
        {
            Assert.That(editor.AddDrawing(new[] { new Vector3(.2f,1,.5f),new Vector3(.3f,1,.5f),new Vector3(.3f,1.1f,.5f) },Color.blue),Is.True);
            editor.ChoosePaint(Color.red); editor.Duplicate();
            Assert.That(editor.Snapshot().objects.Count(x => x.kind == RoomObjectKind.Drawing),Is.EqualTo(2));
            editor.Erase(); yield return null;
            Assert.That(root.GetComponentsInChildren<CreatedRoomObject>().Length,Is.EqualTo(4));
            editor.Undo(); yield return null;
            Assert.That(root.GetComponentsInChildren<CreatedRoomObject>().Length,Is.EqualTo(5));
            editor.SaveNow();
            yield return new WaitForSeconds(.2f);
            // Destroy flushes any pending save, as app shutdown does.
            UnityEngine.Object.Destroy(editor); yield return null;
            var loaded = new RoomStorage(directory).Load(out var message);
            Assert.That(loaded,Is.Not.Null,message);
            var drawings = loaded.objects.Where(x => x.kind == RoomObjectKind.Drawing).ToArray();
            Assert.That(drawings.Length,Is.EqualTo(2));
            Assert.That(drawings.All(x => x.color == Color.red),Is.True);
            Assert.That(drawings[0].points,Is.EqualTo(drawings[1].points));
            Assert.That(drawings[0].points.Length,Is.EqualTo(3));
            foreach (var created in root.GetComponentsInChildren<CreatedRoomObject>()) UnityEngine.Object.Destroy(created.gameObject);
            yield return null;
            editor = root.AddComponent<RoomEditor>();
            var book = root.GetComponentsInChildren<RoomItem>().Single(x => x.name == "book");
            var maestro = root.GetComponentsInChildren<RoomItem>().Single(x => x.name == "maestro");
            editor.Initialize(room,book,maestro,directory); yield return null;
            Assert.That(root.GetComponentsInChildren<CreatedRoomObject>().Length,Is.EqualTo(5));
            Assert.That(editor.Snapshot().objects.Count(x => x.kind == RoomObjectKind.Drawing),Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator SolidToolClickCreatesObjectOnlyWhenReleasedOnSameTool()
        {
            var tray = new GameObject("Creation tools"); tray.transform.SetParent(root.transform,false); tray.transform.localPosition = new Vector3(2,0,1);
            tray.AddComponent<RoomToolTray>().Build(editor,room);
            var router = root.AddComponent<BookPointerRouter>(); router.Editor = editor;
            yield return null; Physics.SyncTransforms();
            int before = editor.Snapshot().objects.Length;
            var blockRay = new Ray(new Vector3(1.76f,.12f,0),Vector3.forward);
            var copyRay = new Ray(new Vector3(2.24f,.12f,0),Vector3.forward);
            Assert.That(router.Begin(0,blockRay),Is.True);
            router.End(0,copyRay);
            Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(before));
            Assert.That(router.Begin(0,blockRay),Is.True);
            router.End(0,blockRay);
            Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(before+1));
        }

        [UnityTest]
        public IEnumerator IncludedBookSurvivesEraseAndRoomRecoveryCanBeUndone()
        {
            var book = root.GetComponentsInChildren<RoomItem>().Single(x => x.name == "book");
            editor.Select(book); editor.Erase(); yield return null;
            Assert.That(editor.Snapshot().objects.Any(x => x.id == "book"),Is.True);
            var before = editor.Snapshot().objects.First(x => x.kind == RoomObjectKind.Block).position;
            room.Viewer = root.transform;
            room.RestoreInFrontOfViewer(); yield return null;
            editor.Undo(); yield return null;
            Assert.That(editor.Snapshot().objects.First(x => x.kind == RoomObjectKind.Block).position,Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator AgentRecipeCreatesAnimatedRobotAndUndoRedoPreserveItsEditableData()
        {
            var executor=new RoomAgentExecutor(editor);int before=editor.Snapshot().objects.Length;
            var request=new RoomAgentRequest {version=1,sceneRevision=editor.Revision,commands=new[] {
                new RoomAgentCommand {action="create",reference="robot",name="Practice robot",kind="boxRobot",scale=.4f},
                new RoomAgentCommand {action="move",target="robot",position=new Vector3(0,0,1)}
            }};
            Assert.That(executor.Execute(request,out var status,out var created),Is.True,status);
            Assert.That(created.Length,Is.EqualTo(1));Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(before+1));
            var data=editor.Read(created[0]);Assert.That(data.name,Is.EqualTo("Practice robot"));
            var geometry=editor.Find(created[0]).GetComponent<RecipeObject>();Assert.That(geometry,Is.Not.Null);
            var start=geometry.Part("RightUpperArm").localRotation;
            yield return new WaitForSeconds(.5f);
            Assert.That(Quaternion.Angle(start,geometry.Part("RightUpperArm").localRotation),Is.GreaterThan(40));
            var file=new RoomStorage(Path.Combine(directory,"recipe-reload"));Assert.That(file.Save(editor.Snapshot(),out status),Is.True,status);
            var loaded=file.Load(out status);Assert.That(loaded.objects.Single(x=>x.id==created[0]).recipe.Validate(out status),Is.True,status);
            editor.Undo();yield return null;Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(before));
            editor.Redo();yield return null;Assert.That(editor.Read(created[0]).recipe.parts.Length,Is.EqualTo(data.recipe.parts.Length));
            Assert.That(editor.Find(created[0]).GetComponent<RecipeObject>().Part("Head"),Is.Not.Null);
        }
        [UnityTest]
        public IEnumerator InvalidAndStaleAgentBatchesNeverPartiallyChangeTheRoom()
        {
            var executor=new RoomAgentExecutor(editor);int count=editor.Snapshot().objects.Length,revision=editor.Revision;
            var request=new RoomAgentRequest {version=1,sceneRevision=revision,commands=new[] {
                new RoomAgentCommand {action="create",reference="box",name="Box",kind="block"},
                new RoomAgentCommand {action="delete",target="book"}
            }};
            Assert.That(executor.Execute(request,out _,out _),Is.False);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count));Assert.That(editor.Revision,Is.EqualTo(revision));
            editor.Create(RoomObjectKind.Ball);
            request.commands=new[]{new RoomAgentCommand {action="create",reference="robot",name="Robot",kind="boxRobot"}};
            Assert.That(executor.Execute(request,out var status,out _),Is.False);StringAssert.Contains("changed",status);
            Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+1));
            request.sceneRevision=editor.Revision;request.commands[0].scale=float.NaN;
            Assert.That(executor.Execute(request,out _,out _),Is.False);Assert.That(editor.Snapshot().objects.Length,Is.EqualTo(count+1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ObjectPreconditionsAllowUnrelatedEditsButProtectTheTargetAndExposeRecipes()
        {
            var executor=new RoomAgentExecutor(editor);
            var target=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block);
            int revision=editor.ObjectRevision(target.id),scene=editor.Revision;
            editor.Create(RoomObjectKind.Ball);
            var request=new RoomAgentRequest {version=2,sceneRevision=scene,conditions=new[]{new RoomObjectCondition {id=target.id,revision=revision}},commands=new[]{new RoomAgentCommand {action="paint",target=target.id,color=Color.red}}};
            Assert.That(executor.Execute(request,out var status,out _),Is.True,status);Assert.That(editor.Read(target.id).color,Is.EqualTo(Color.red));
            request.commands[0].color=Color.blue;
            Assert.That(executor.Execute(request,out status,out _),Is.False);StringAssert.Contains("changed",status);Assert.That(editor.Read(target.id).color,Is.EqualTo(Color.red));
            request.conditions=Array.Empty<RoomObjectCondition>();request.commands=new[]{new RoomAgentCommand {action="create",reference="robot",name="Robot",kind="boxRobot"}};
            Assert.That(executor.Execute(request,out status,out var created),Is.True,status);
            request.commands=new[]{new RoomAgentCommand {action="inspect",target=created[0],partId="Head"}};
            Assert.That(executor.Execute(request,out status,out _),Is.True,status);Assert.That(executor.InspectionId,Is.EqualTo(created[0]));Assert.That(editor.SelectedId,Is.EqualTo(created[0]));
            Assert.That(editor.Read(executor.InspectionId).recipe.parts.Length,Is.GreaterThanOrEqualTo(17));
            Assert.That(editor.Find(created[0]).GetComponent<RecipeObject>().Part("Head").Find("Selected recipe part"),Is.Not.Null);
            yield return null;
        }
        [UnityTest]
        public IEnumerator RecipePlaybackCanBeStoppedAndExplicitlyRestartedAfterFocusLoss()
        {
            var executor=new RoomAgentExecutor(editor);var create=new RoomAgentRequest {version=2,conditions=Array.Empty<RoomObjectCondition>(),commands=new[]{new RoomAgentCommand {action="create",reference="robot",name="Robot",kind="boxRobot"}}};
            Assert.That(executor.Execute(create,out var status,out var created),Is.True,status);
            var geometry=editor.Find(created[0]).GetComponent<RecipeObject>();yield return null;Assert.That(geometry.IsPlaying,Is.True);
            geometry.SendMessage("OnApplicationFocus",false);Assert.That(geometry.IsPlaying,Is.False);
            var play=new RoomAgentRequest {version=2,conditions=new[]{new RoomObjectCondition{id=created[0],revision=editor.ObjectRevision(created[0])}},commands=new[]{new RoomAgentCommand {action="play",target=created[0]}}};
            Assert.That(executor.Execute(play,out status,out _),Is.True,status);Assert.That(geometry.IsPlaying,Is.True);
            play.commands[0].action="stop";Assert.That(executor.Execute(play,out status,out _),Is.True,status);Assert.That(geometry.IsPlaying,Is.False);
            play.conditions[0].revision=editor.ObjectRevision(created[0]);play.commands[0].action="play";
            Assert.That(executor.Execute(play,out status,out _),Is.True,status);Assert.That(geometry.IsPlaying,Is.True);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UnityEngine.Object.Destroy(root); yield return null;
            if (Directory.Exists(directory)) Directory.Delete(directory,true);
        }

        [UnityTest]
        public IEnumerator RecordedThrowReleasesIntoPhysicsAndUnrelatedEditsPreserveItsPosition()
        {
            var data = editor.Snapshot().objects.Single(x => x.kind == RoomObjectKind.Ball);
            var item = editor.Find(data.id); editor.Select(item); editor.CyclePhysics();
            var frames = new[] { new MotionFrame { time = 0,position = new Vector3(0,1.3f,0) },new MotionFrame { time = .5f,position = new Vector3(.5f,1.5f,0) } };
            editor.SaveAnimation(data.id,new RoomMotion { frames = frames },null,false);
            physics.SetSurfaces(true,"Test room"); physics.StartPhysics();
            var actions = new RoomRuleActions(editor,null);
            var step = new RuleStep { action = RuleActionKind.ThrowRecording,targetId = data.id };
            Assert.That(actions.Start("throw",step,out _,out var error),Is.True,error);
            Assert.That(item.GetComponent<Rigidbody>().isKinematic,Is.True);
            Assert.That(actions.Complete("throw",out var completionError),Is.True,completionError);
            Assert.That(item.transform.localPosition.x,Is.EqualTo(.5f).Within(.001f));
            Assert.That(item.GetComponent<Rigidbody>().linearVelocity.x,Is.EqualTo(1).Within(.01f));
            yield return new WaitForFixedUpdate();
            var beforeEdit = item.transform.position;
            editor.Create(RoomObjectKind.Block);
            Assert.That(Vector3.Distance(item.transform.position,beforeEdit),Is.LessThan(.001f),"Adding another object reset the flying ball");
            editor.SaveNow(); physics.PausePhysics();
            yield return new WaitForSeconds(.15f);
            var saved = editor.Snapshot().objects.Single(x => x.id == data.id);
            Assert.That(saved.position.x,Is.GreaterThanOrEqualTo(.5f));
            Assert.That(saved.physics,Is.EqualTo(ItemPhysics.Solid));
            Assert.That(actions.Start("canceled",step,out _,out _),Is.False,"Paused room must not start a throw");
        }
        [UnityTest]
        public IEnumerator ColliderAndMassEditsSurviveUndoAndSaveWithoutChangingTheBook()
        {
            editor.Create(RoomObjectKind.Block); var id = editor.SelectedId; var item = editor.Find(id);
            editor.CycleCollider(); editor.CycleCollider(); editor.CycleMass();
            Assert.That(item.Grab.colliders.Single(),Is.TypeOf<SphereCollider>());
            Assert.That(item.GetComponent<Rigidbody>().mass,Is.EqualTo(1));
            editor.Undo(); Assert.That(item.GetComponent<Rigidbody>().mass,Is.EqualTo(.5f));
            editor.Redo(); editor.SaveNow();
            editor.SendMessage("OnApplicationPause",true);
            var loaded = new RoomStorage(directory).Load(out var error); Assert.That(loaded,Is.Not.Null,error);
            var data = loaded.objects.Single(value => value.id == id);
            Assert.That(data.collisionShape,Is.EqualTo(ItemCollider.Sphere)); Assert.That(data.mass,Is.EqualTo(1));
            editor.Select(editor.Find("book")); editor.CyclePhysics();
            Assert.That(editor.Read("book").physics,Is.EqualTo(ItemPhysics.Fixed));
            yield return null;
        }
    }
}
