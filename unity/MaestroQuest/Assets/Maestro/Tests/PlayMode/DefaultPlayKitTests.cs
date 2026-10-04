// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
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
    public sealed partial class RoomRulesTests
    {
        static CreationBatch PlayKitBatch(string resource)
        {
            var module = JObject.Parse(Resources.Load<TextAsset>("Programs/Modules/" + resource).text);
            var args = (JObject)module["program"]["functions"][1]["body"][0]["arguments"].DeepClone();
            args["position"] = new JObject { ["x"] = 4, ["y"] = 2, ["z"] = 4 };
            return JsonUtility.FromJson<CreationBatch>(args.ToString());
        }
        [UnityTest] public IEnumerator DefaultPlayKitFortProgramCreatesOneEditableSavedBatchAndUndoRemovesAll()
        {
            var before = editor.Snapshot().objects.Select(x => x.id).ToHashSet();
            var source = JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "Maestro/Tests/Fixtures/program-small-fort.json"))).ToString(Newtonsoft.Json.Formatting.None);
            Assert.That(workshop.Execute(new RuleRequest { action = "edit", revision = workshop.Revision, edits = new[] { new RuleEdit { kind = "save", reference = "fort", sequence = new RuleSequence { id = "", name = "Build small fort", program = source } } } }, out var error, out var saved), Is.True, error);
            Assert.That(runtime.Scheduler.RunningCount, Is.Zero, "Saving the constructor must not build or start physics");
            Assert.That(runtime.Trigger(saved.Single()), Is.True, runtime.Scheduler.LastError);
            for (int i = 0; i < 30 && runtime.Scheduler.RunningCount > 0; i++) { runtime.Scheduler.Tick(Time.unscaledTime); yield return null; }
            Assert.That(runtime.Scheduler.Outcomes.Last().phase, Is.EqualTo("completed"), runtime.Scheduler.LastError);
            var pieces = editor.Snapshot().objects.Where(x => !before.Contains(x.id)).ToArray();
            Assert.That(pieces.Length, Is.EqualTo(16));
            Assert.That(physics.Running, Is.False);
            foreach (var item in pieces)
            {
                Assert.That(item.recipe, Is.Not.Null);
                Assert.That(editor.Find(item.id).Grab, Is.Not.Null);
                Assert.That(editor.Find(item.id).GetComponent<Rigidbody>(), Is.Not.Null);
            }
            var disk = new RoomStorage(directory).Load(out _);
            Assert.That(disk.objects.Count(x => !before.Contains(x.id)), Is.EqualTo(16));
            editor.Undo();
            Assert.That(editor.Snapshot().objects.Select(x => x.id), Is.EquivalentTo(before));
            editor.Redo();
            Assert.That(editor.Snapshot().objects.Where(x => !before.Contains(x.id)).Select(x => x.id), Is.EquivalentTo(pieces.Select(x => x.id)));
        }
        [UnityTest] public IEnumerator DefaultPlayKitFortSettlesTakesRealBallContactAndResetsThroughSharedStructureActions()
        {
            Assert.That(editor.CreateBatch(PlayKitBatch("SmallFort"), out var ids, out var error), Is.True, error);
            Physics.SyncTransforms(); physics.SetSurfaces(true, "Synthetic room ready"); physics.StartPhysics();
            for (int i = 0; i < 100; i++) yield return new WaitForFixedUpdate();
            foreach (int towerTop in new[] { 3, 6, 9, 12 })
                Assert.That(editor.Find(ids[towerTop]).transform.localPosition.y, Is.GreaterThan(2.17f), "Tower must stand before any strike");
            var executor = new RoomAgentExecutor(editor);
            string group = SaveGroup(executor, ids);
            int revision = editor.StructureRevision(group);
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Ball, "Fort ball", new Vector3(3.77f, 2.14f, 3.64f), .55f, Color.white, out var ballId, out error), Is.True, error);
            yield return new WaitForFixedUpdate();
            editor.Find(ballId).GetComponent<Rigidbody>().linearVelocity = Vector3.forward * 3;
            for (int i = 0; i < 65; i++) yield return new WaitForFixedUpdate();
            Assert.That(editor.ObserveStructure(editor.ReadStructure(group)).Displaced, Is.GreaterThan(0), "Contact must move a fort piece; moving the projectile alone does not count");
            // Move the projectile away before rebuilding so it cannot immediately knock the fort down again.
            physics.PausePhysics();
            editor.Find(ballId).transform.localPosition = new Vector3(6, 2, 6);
            Assert.That(executor.Execute(StructureRequest(StructureResetCall(group, revision, ids)), out error, out _), Is.True, error);
            Assert.That(editor.ObserveStructure(editor.ReadStructure(group)).Displaced, Is.Zero);
            physics.StartPhysics();
            for (int i = 0; i < 100; i++) yield return new WaitForFixedUpdate();
            foreach (int towerTop in new[] { 3, 6, 9, 12 }) Assert.That(editor.Find(ids[towerTop]).transform.localPosition.y, Is.GreaterThan(2.17f));
            Assert.That(editor.ObserveStructure(editor.ReadStructure(group)).Displaced, Is.Zero, "Rebuilt fort remains stable");
        }
        [UnityTest] public IEnumerator DefaultPlayKitSpinnerTurnsOnContactStaysAnchoredAndCopiesItsPassiveHinge()
        {
            Assert.That(editor.CreateBatch(PlayKitBatch("Spinner"), out var ids, out var error), Is.True, error);
            var rotor = editor.Find(ids[1]); var view = rotor.GetComponent<RoomConnectionView>();
            Physics.SyncTransforms(); physics.SetSurfaces(true, "Synthetic room ready"); physics.StartPhysics();
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            Assert.That(view.Active, Is.True, view.Error);
            var body = rotor.GetComponent<Rigidbody>();
            Assert.That(body.angularVelocity.magnitude, Is.LessThan(.01f), "Passive spinner does not start itself");
            var rotation = body.rotation;
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Ball, "Spinner pusher", new Vector3(3.85f, 2.055f, 4.11f), .35f, Color.white, out var ballId, out error), Is.True, error);
            yield return new WaitForFixedUpdate();
            var ball = editor.Find(ballId).GetComponent<Rigidbody>(); ball.useGravity = false; ball.linearVelocity = Vector3.right * 1.5f;
            for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();
            Assert.That(Quaternion.Angle(rotation, body.rotation), Is.GreaterThan(5), "A tangential physical contact must spin the rotor");
            Assert.That(Vector3.Distance(body.position, editor.Find(ids[0]).transform.TransformPoint(new Vector3(0, .055f, 0))), Is.LessThan(.02f));
            physics.PausePhysics(); view.Refresh(); yield return null;
            var members = ids.Select((target, index) => new ConstructionMember { target = target, slot = "piece_" + index, revision = editor.ObjectRevision(target) }).ToArray();
            Assert.That(editor.CaptureConstruction(members, out var captured, out error), Is.True, error);
            ProgramModuleLibrary.Validate(ConstructionModule.Definition(captured, "My spinner"));
            Assert.That(editor.CreateBatch(captured, out var fresh, out error), Is.True, error);
            Assert.That(editor.Read(fresh[1]).connections.Single().connected, Is.EqualTo(fresh[0]));
            Assert.That(editor.Read(fresh[1]).connections.Single().drive.mode, Is.EqualTo("passive"));
            Assert.That(fresh.Intersect(ids), Is.Empty);
            var disk = new RoomStorage(directory).Load(out _);
            Assert.That(disk.objects.Single(x => x.id == fresh[1]).connections.Single().connected, Is.EqualTo(fresh[0]));
            editor.Undo(); Assert.That(editor.Find(fresh[0]), Is.Null); Assert.That(editor.Find(ids[0]), Is.Not.Null);
        }
    }
}
