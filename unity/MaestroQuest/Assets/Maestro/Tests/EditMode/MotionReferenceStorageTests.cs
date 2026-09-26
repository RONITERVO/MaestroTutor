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
        [Test] public void LegacyRoomAndRulesUpgradeWithoutRewritingOriginalsAndRetainAllExistingBindings()
        {
            var room = Room(); room.objects[1].modelHash = new string('a',64); room.objects[1].walkClip = 2;
            var sequence = new RuleSequence { id = Guid.NewGuid().ToString("N"),name = "Old action",steps = new[] { new RuleStep { action = RuleActionKind.ImportedClip,clipModelHash = new string('a',64),clipIndex = 1 } } };
            var rules = new RuleDocument { version = 1,sequences = new[] { sequence },buttons = new[] { new RuleButtonData { id = Guid.NewGuid().ToString("N"),sequenceId = sequence.id,mount = ButtonMount.RightController } } };
            string roomText = JsonUtility.ToJson(room),ruleText = JsonUtility.ToJson(rules);
            File.WriteAllText(Path.Combine(directory,"room.v1.json"),roomText); File.WriteAllText(Path.Combine(directory,"rules.v1.json"),ruleText);
            var roomStore = new RoomStorage(directory); var loaded = roomStore.Load(out _); Assert.That(loaded.version,Is.EqualTo(2)); Assert.That(loaded.objects[1].walkClip,Is.EqualTo(2));
            var ruleStore = new RuleStorage(directory); var loadedRules = ruleStore.Load(out _); Assert.That(loadedRules.version,Is.EqualTo(2)); Assert.That(loadedRules.sequences[0].steps[0].clipIndex,Is.EqualTo(1)); Assert.That(loadedRules.buttons[0].mount,Is.EqualTo(ButtonMount.RightController));
            Assert.That(roomStore.Save(loaded,out _),Is.True); Assert.That(ruleStore.Save(loadedRules,out _),Is.True);
            Assert.That(File.ReadAllText(Path.Combine(directory,"room.v1.json")),Is.EqualTo(roomText)); Assert.That(File.ReadAllText(Path.Combine(directory,"rules.v1.json")),Is.EqualTo(ruleText));
            Assert.That(File.Exists(Path.Combine(directory,"room.v2.json")),Is.True); Assert.That(File.Exists(Path.Combine(directory,"rules.v2.json")),Is.True);
            // Corrupt current storage never resurrects a stale, still-valid v1 file.
            File.WriteAllText(Path.Combine(directory,"room.v2.json"),"broken"); var blocked = new RoomStorage(directory); Assert.That(blocked.Load(out _),Is.Null); Assert.That(blocked.ReadOnly,Is.True); Assert.That(blocked.Save(room,out _),Is.False);
            Assert.That(ruleStore.Save(loadedRules,out _),Is.True); Assert.That(File.Exists(Path.Combine(directory,"rules.v2.json.backup")),Is.True);
            File.WriteAllText(Path.Combine(directory,"rules.v2.json"),"{\"version\":3}"); var blockedRules = new RuleStorage(directory); Assert.That(blockedRules.Load(out _).sequences,Is.Empty); Assert.That(blockedRules.ReadOnly,Is.True); Assert.That(blockedRules.Save(rules,out _),Is.False);
        }
        [Test] public void StableMotionIdsPersistThroughCopyUndoAndReloadAndRejectPathsAndWrongTargets()
        {
            string id = Guid.NewGuid().ToString("N"); var journal = new RoomJournal(Room()); var data = journal.Read("maestro"); data.walkMotionId = id;
            Assert.That(journal.Apply(new[] { data },Array.Empty<string>(),out _),Is.True); Assert.That(journal.Snapshot().Copy().objects.Single(x => x.id == "maestro").walkMotionId,Is.EqualTo(id));
            Assert.That(journal.Undo(),Is.True); Assert.That(journal.Read("maestro").walkMotionId,Is.Null); Assert.That(journal.Redo(),Is.True);
            var storage = new RoomStorage(directory); Assert.That(storage.Save(journal.Snapshot(),out _),Is.True); Assert.That(storage.Load(out _).objects.Single(x => x.id == "maestro").walkMotionId,Is.EqualTo(id));
            var book = journal.Read("book"); book.walkMotionId = id; Assert.That(journal.Apply(new[] { book },Array.Empty<string>(),out _),Is.False);
            data.walkMotionId = "../clip.glb"; Assert.That(journal.Apply(new[] { data },Array.Empty<string>(),out _),Is.False);
            var sequence = new RuleSequence { id = Guid.NewGuid().ToString("N"),name = "Saved motion",steps = new[] { new RuleStep { action = RuleActionKind.LibraryMotion,motionId = id } } };
            var rules = new RuleDocument { sequences = new[] { sequence } }; Assert.That(rules.Validate(out _),Is.True);
            var ruleStorage = new RuleStorage(directory); Assert.That(ruleStorage.Save(rules,out _),Is.True); Assert.That(ruleStorage.Load(out _).sequences[0].steps[0].motionId,Is.EqualTo(id));
            rules.sequences[0].steps[0].motionId = "../motion"; Assert.That(rules.Validate(out _),Is.False);
        }
    }
}
