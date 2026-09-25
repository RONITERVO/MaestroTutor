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
            actions.Complete("throw");
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
