// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace Maestro.Quest.Tests
{
    public sealed class RoomDocumentTests
    {
        [Test] public void OldRoomDefaultsRemainFixedAndPhysicsSettingsAreValidated()
        {
            var directory = Path.Combine(Path.GetTempPath(),"MaestroOldRoom-"+Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                string json = JsonUtility.ToJson(EmptyRoom()).Replace(",\"mass\":0.5","").Replace(",\"physics\":0","").Replace(",\"collisionShape\":0","");
                File.WriteAllText(Path.Combine(directory,"room.v1.json"),json);
                var loaded = new RoomStorage(directory).Load(out var error);
                Assert.That(loaded,Is.Not.Null,error); Assert.That(loaded.objects[0].physics,Is.EqualTo(ItemPhysics.Fixed));
                var data = Drawing(); data.physics = ItemPhysics.Bouncy; data.collisionShape = ItemCollider.Sphere;
                var journal = new RoomJournal(loaded); Assert.That(journal.Apply(new[] { data },Array.Empty<string>(),out _),Is.True);
                data.mass = float.NaN; Assert.That(journal.Apply(new[] { data },Array.Empty<string>(),out _),Is.False);
                data.mass = .5f; data.collisionShape = (ItemCollider)99; Assert.That(journal.Apply(new[] { data },Array.Empty<string>(),out _),Is.False);
                var book = journal.Read("book"); book.physics = ItemPhysics.Solid; Assert.That(journal.Apply(new[] { book },Array.Empty<string>(),out _),Is.False);
            }
            finally { Directory.Delete(directory,true); }
        }
        static RoomDocument EmptyRoom() => new() { version = 1, objects = new[] {
            new RoomObjectData { id = "book", kind = RoomObjectKind.Book },
            new RoomObjectData { id = "maestro", kind = RoomObjectKind.Maestro }
        } };
        [Test] public void AvatarMovementSettingsValidateAndUndoWithoutChangingOldRoomDefaults()
        {
            var journal = new RoomJournal(EmptyRoom()); Assert.That(journal.Read("maestro").walkSpeed,Is.Zero);
            var tutor = journal.Read("maestro"); tutor.walkSpeed = .65f; tutor.followDistance = 1.3f;
            Assert.That(journal.Apply(new[] { tutor },Array.Empty<string>(),out var error),Is.True,error);
            Assert.That(journal.Snapshot().Validate(out error),Is.True,error);
            foreach (float invalid in new[] { float.NaN,-1f,.1f,20f }) { tutor.walkSpeed = invalid; Assert.That(journal.Apply(new[] { tutor },Array.Empty<string>(),out _),Is.False); }
            tutor.walkSpeed = .65f; tutor.followDistance = .1f; Assert.That(journal.Apply(new[] { tutor },Array.Empty<string>(),out _),Is.False);
            var book = journal.Read("book"); book.walkSpeed = .65f; Assert.That(journal.Apply(new[] { book },Array.Empty<string>(),out _),Is.False);
            Assert.That(journal.Undo(),Is.True); Assert.That(journal.Read("maestro").walkSpeed,Is.Zero); Assert.That(journal.Read("maestro").followDistance,Is.Zero);
        }
        [Test] public void AvatarModelReferencesAreBoundedAndOnlyAllowedOnTheTutorOrImports()
        {
            var journal = new RoomJournal(EmptyRoom()); var tutor = journal.Read("maestro"); tutor.modelHash = new string('b',64);
            Assert.That(journal.Apply(new[] { tutor },Array.Empty<string>(),out var error),Is.True,error);
            tutor.modelHash = "../outside.vrm"; Assert.That(journal.Apply(new[] { tutor },Array.Empty<string>(),out _),Is.False);
            var book = journal.Read("book"); book.modelHash = new string('b',64);
            Assert.That(journal.Apply(new[] { book },Array.Empty<string>(),out _),Is.False);
            Assert.That(journal.Undo(),Is.True); Assert.That(journal.Read("maestro").modelHash,Is.Null);
        }
        [Test] public void WalkClipReferencesRequireAnAvatarModelAndSurviveCopyAndUndo()
        {
            var journal = new RoomJournal(EmptyRoom()); var data = journal.Read("maestro"); data.walkClip = 1;
            Assert.That(journal.Apply(new[] { data },Array.Empty<string>(),out _),Is.False);
            data.modelHash = new string('c',64); Assert.That(journal.Apply(new[] { data },Array.Empty<string>(),out _),Is.True);
            Assert.That(journal.Snapshot().Copy().objects.Single(x => x.id == "maestro").walkClip,Is.EqualTo(1));
            data.walkClip = 33; Assert.That(journal.Apply(new[] { data },Array.Empty<string>(),out _),Is.False);
            data.walkClip = -1; Assert.That(journal.Apply(new[] { data },Array.Empty<string>(),out _),Is.False);
            Assert.That(journal.Undo(),Is.True); Assert.That(journal.Read("maestro").walkClip,Is.Zero);
        }
        static RoomObjectData Drawing() => new() { id = Guid.NewGuid().ToString("N"), kind = RoomObjectKind.Drawing,
            points = new[] { Vector3.zero, Vector3.right * .1f, Vector3.up * .2f }, color = new Color(.2f,.4f,.8f), scale = 1.5f };

        [Test]
        public void EraseUndoRedoRestoresCompleteDrawingAndNewEditDiscardsRedo()
        {
            var journal = new RoomJournal(EmptyRoom()); var drawing = Drawing();
            Assert.That(journal.Apply(new[] { drawing },Array.Empty<string>(),out _),Is.True);
            var painted = journal.Read(drawing.id); painted.color = Color.red;
            journal.Apply(new[] { painted },Array.Empty<string>(),out _);
            journal.Apply(Array.Empty<RoomObjectData>(),new[] { drawing.id },out _);
            Assert.That(journal.Read(drawing.id),Is.Null);
            Assert.That(journal.Undo(),Is.True);
            Assert.That(journal.Read(drawing.id).points,Is.EqualTo(drawing.points));
            Assert.That(journal.Read(drawing.id).color,Is.EqualTo(Color.red));
            Assert.That(journal.Read(drawing.id).scale,Is.EqualTo(1.5f));
            Assert.That(journal.Undo(),Is.True);
            Assert.That(journal.Read(drawing.id).color,Is.EqualTo(drawing.color));
            Assert.That(journal.Redo(),Is.True);
            painted = journal.Read(drawing.id); painted.position = Vector3.up;
            journal.Apply(new[] { painted },Array.Empty<string>(),out _);
            Assert.That(journal.CanRedo,Is.False);
            drawing.points[1] = Vector3.one * 99;
            Assert.That(journal.Read(drawing.id).points[1],Is.EqualTo(Vector3.right*.1f),"History must own its stroke data.");
        }

        [Test]
        public void IncludedItemsCannotBeDeletedAndRejectedChangesDoNotAffectHistory()
        {
            var journal = new RoomJournal(EmptyRoom());
            Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),new[] { "book" },out _),Is.False);
            var invalid = Drawing(); invalid.scale = float.NaN;
            Assert.That(journal.Apply(new[] { invalid },Array.Empty<string>(),out _),Is.False);
            Assert.That(journal.Snapshot().objects.Length,Is.EqualTo(2));
            Assert.That(journal.CanUndo,Is.False);
        }

        [Test]
        public void ObjectAndStrokeBudgetsRejectBeforeChangingScene()
        {
            var journal = new RoomJournal(EmptyRoom());
            var tooMany = Enumerable.Range(0,65).Select(_ => new RoomObjectData { id = Guid.NewGuid().ToString("N"), kind = RoomObjectKind.Block }).ToArray();
            Assert.That(journal.Apply(tooMany,Array.Empty<string>(),out _),Is.False);
            var tooLong = Drawing(); tooLong.points = Enumerable.Range(0,2049).Select(i => Vector3.right * (i * .001f)).ToArray();
            Assert.That(journal.Apply(new[] { tooLong },Array.Empty<string>(),out _),Is.False);
            var flat = Drawing(); flat.points = new[] { Vector3.zero, Vector3.zero };
            Assert.That(journal.Apply(new[] { flat },Array.Empty<string>(),out _),Is.False);
            var tooDense = Enumerable.Range(0,17).Select(_ => { var item = Drawing(); item.points = tooLong.points.Take(2048).ToArray(); return item; }).ToArray();
            Assert.That(journal.Apply(tooDense,Array.Empty<string>(),out _),Is.False);
            Assert.That(journal.Snapshot().objects.Length,Is.EqualTo(2));
        }

        [Test]
        public void JournalUndoBudgetIsBounded()
        {
            var journal = new RoomJournal(EmptyRoom());
            for (int i = 0; i < 40; i++) { var item = journal.Read("book"); item.position.x = (i+1)*.01f; journal.Apply(new[] { item },Array.Empty<string>(),out _); }
            int undos = 0; while (journal.Undo()) undos++;
            Assert.That(undos,Is.EqualTo(32));
            Assert.That(journal.Read("book").position.x,Is.EqualTo(.08f).Within(.0001f));
        }

        [Test]
        public void SaveRoundTripAndDamagedPrimaryRecoverFromLastGoodBackup()
        {
            var directory = Path.Combine(Path.GetTempPath(),"MaestroRoomTests-"+Guid.NewGuid().ToString("N"));
            try
            {
                var storage = new RoomStorage(directory); var journal = new RoomJournal(EmptyRoom()); var drawing = Drawing();
                journal.Apply(new[] { drawing },Array.Empty<string>(),out _);
                Assert.That(storage.Save(journal.Snapshot(),out var error),Is.True,error);
                var changed = journal.Read(drawing.id); changed.position = Vector3.right;
                journal.Apply(new[] { changed },Array.Empty<string>(),out _);
                Assert.That(storage.Save(journal.Snapshot(),out error),Is.True,error);
                Assert.That(storage.Load(out _).objects.Single(x => x.id == drawing.id).position,Is.EqualTo(Vector3.right));
                File.WriteAllText(Path.Combine(directory,"room.v9.json"),"broken");
                var recovered = storage.Load(out var message);
                Assert.That(message,Does.Contain("backup"));
                Assert.That(recovered.objects.Single(x => x.id == drawing.id).points,Is.EqualTo(drawing.points));
                Assert.That(recovered.objects.Single(x => x.id == drawing.id).position,Is.EqualTo(Vector3.zero));
                Assert.That(storage.Save(recovered,out error),Is.True,error);
                Assert.That(File.ReadAllText(Path.Combine(directory,"room.v9.json.unreadable")),Is.EqualTo("broken"));
                Assert.That(storage.Load(out message),Is.Not.Null); Assert.That(message,Is.Null);
                var invalid = recovered.Copy(); invalid.version = RoomDocument.CurrentVersion+1;
                Assert.That(storage.Save(invalid,out _),Is.False);
                Assert.That(storage.Load(out _).version,Is.EqualTo(RoomDocument.CurrentVersion));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory,true); }
        }

        [Test]
        public void UnversionedDocumentIsRejected()
        {
            var json = JsonUtility.ToJson(EmptyRoom()).Replace("\"version\":1,","");
            Assert.That(JsonUtility.FromJson<RoomDocument>(json).Validate(out _),Is.False);
        }
    }
}
