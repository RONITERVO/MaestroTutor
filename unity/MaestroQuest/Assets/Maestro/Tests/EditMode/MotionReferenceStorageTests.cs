// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Rules;
using NUnit.Framework;
using UnityEngine;

namespace Maestro.Quest.Tests
{
    public sealed class MotionReferenceStorageTests
    {
        string directory;
        [SetUp] public void Setup() { directory = Path.Combine(Path.GetTempPath(),"MaestroMotionRefs-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory); }
        [TearDown] public void Cleanup() => Directory.Delete(directory,true);
        static RoomDocument Room() => new() { version = 1,objects = new[] { new RoomObjectData { id = "book",kind = RoomObjectKind.Book },new RoomObjectData { id = "maestro",kind = RoomObjectKind.Maestro } } };
        [Test] public void RoomMigrationIsIndependentOfDevelopmentBehaviourReset()
        {
            var room=Room();room.objects[1].modelHash=new string('a',64);room.objects[1].walkClip=2;
            string original=JsonUtility.ToJson(room),path=Path.Combine(directory,"room.v1.json");File.WriteAllText(path,original);
            File.WriteAllText(Path.Combine(directory,"rules.v5.json"),"retired development rules");
            var store=new RoomStorage(directory);var loaded=store.Load(out _);
            Assert.That(loaded.version,Is.EqualTo(2));Assert.That(loaded.objects[1].walkClip,Is.EqualTo(2));
            Assert.That(new RuleStorage(directory).Load(out _).sequences,Is.Empty);
            Assert.That(store.Save(loaded,out _),Is.True);Assert.That(File.ReadAllText(path),Is.EqualTo(original));
            File.WriteAllText(Path.Combine(directory,"room.v2.json"),"broken");
            store=new RoomStorage(directory);Assert.That(store.Load(out _),Is.Null);Assert.That(store.ReadOnly,Is.True);
        }
        [Test] public void StableMotionIdsPersistThroughCopyUndoAndReloadAndRejectPathsAndWrongTargets()
        {
            string id = Guid.NewGuid().ToString("N"); var journal = new RoomJournal(Room()); var data = journal.Read("maestro"); data.walkMotionId = id;
            Assert.That(journal.Apply(new[] { data },Array.Empty<string>(),out _),Is.True); Assert.That(journal.Snapshot().Copy().objects.Single(x => x.id == "maestro").walkMotionId,Is.EqualTo(id));
            Assert.That(journal.Undo(),Is.True); Assert.That(journal.Read("maestro").walkMotionId,Is.Null); Assert.That(journal.Redo(),Is.True);
            var storage = new RoomStorage(directory); Assert.That(storage.Save(journal.Snapshot(),out _),Is.True); Assert.That(storage.Load(out _).objects.Single(x => x.id == "maestro").walkMotionId,Is.EqualTo(id));
            var book = journal.Read("book"); book.walkMotionId = id; Assert.That(journal.Apply(new[] { book },Array.Empty<string>(),out _),Is.False);
            data.walkMotionId = "../clip.glb"; Assert.That(journal.Apply(new[] { data },Array.Empty<string>(),out _),Is.False);
            var sequence = new RuleSequence { id = Guid.NewGuid().ToString("N"),name = "Saved motion",program=Maestro.Quest.Programs.BehaviourProgram.FromSteps(new RuleStep { action = RuleActionKind.LibraryMotion,motionId = id }) };
            var rules = new RuleDocument { sequences = new[] { sequence } }; Assert.That(rules.Validate(out _),Is.True);
            var ruleStorage = new RuleStorage(directory); Assert.That(ruleStorage.Save(rules,out _),Is.True); Assert.That(ruleStorage.Load(out _).sequences[0].SimpleSteps()[0].motionId,Is.EqualTo(id));
            var bad=sequence.SimpleSteps();bad[0].motionId="../motion";sequence.SetSimpleSteps(bad); Assert.That(rules.Validate(out _),Is.False);
        }
    }
}
