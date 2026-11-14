// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;

namespace Maestro.Quest.Tests
{
    public sealed class RoomJournalSessionTests
    {
        static RoomJournal Room()=>new(new RoomDocument {version=2,objects=new[]{
            new RoomObjectData {id="book",kind=RoomObjectKind.Book},
            new RoomObjectData {id="maestro",kind=RoomObjectKind.Maestro}
        }});
        static RoomObjectData Block()=>new() {id=Guid.NewGuid().ToString("N"),kind=RoomObjectKind.Block};
        [Test] public void ForkKeepsSavedHistoryAndCombinesChangesIntoOneUndo()
        {
            var saved=Room();var removed=Block();var painted=Block();
            Assert.That(saved.Apply(new[]{removed,painted},Array.Empty<string>(),out _),Is.True);
            int bookRevision=saved.ObjectRevision("book");
            var live=saved.Fork();Assert.That(live.CanUndo,Is.False);
            var created=Block();painted.color=Color.blue;
            Assert.That(live.Apply(new[]{created,painted},new[]{removed.id},out _),Is.True);
            Assert.That(saved.Read(created.id),Is.Null);Assert.That(saved.Read(removed.id),Is.Not.Null);
            Assert.That(saved.Read(painted.id).color,Is.EqualTo(Color.white));
            Assert.That(saved.ApplySnapshot(live.Snapshot(),out var error),Is.True,error);
            Assert.That(saved.ObjectRevision("book"),Is.EqualTo(bookRevision),"Unchanged object observations stay valid");
            Assert.That(saved.Read(created.id),Is.Not.Null);Assert.That(saved.Read(removed.id),Is.Null);
            Assert.That(saved.Undo(),Is.True);
            Assert.That(saved.Read(created.id),Is.Null);Assert.That(saved.Read(removed.id),Is.Not.Null);
            Assert.That(saved.Read(painted.id).color,Is.EqualTo(Color.white));
            Assert.That(saved.Undo(),Is.True,"History from before the temporary room remains available");
            Assert.That(saved.Snapshot().objects.Length,Is.EqualTo(2));
        }
        [Test] public void DiscardAndUndoNeverReuseAnObjectRevision()
        {
            var saved=Room();var objectData=Block();saved.Apply(new[]{objectData},Array.Empty<string>(),out _);
            int original=saved.ObjectRevision(objectData.id);var live=saved.Fork();
            live.UpdatePlacement(objectData.id,Vector3.up,Quaternion.identity);
            int moving=live.ObjectRevision(objectData.id);Assert.That(moving,Is.GreaterThan(original));
            saved.InvalidateChangedObservations(live);
            int restored=saved.ObjectRevision(objectData.id);Assert.That(restored,Is.GreaterThan(moving));
            Assert.That(saved.Read(objectData.id).position,Is.EqualTo(Vector3.zero));
            saved.Undo();saved.Redo();Assert.That(saved.ObjectRevision(objectData.id),Is.GreaterThan(restored));
            var another=saved.Fork();another.Apply(Array.Empty<RoomObjectData>(),new[]{objectData.id},out _);
            saved.InvalidateChangedObservations(another);
            Assert.That(saved.ObjectRevision(objectData.id),Is.GreaterThan(restored));
        }
        [Test] public void InvalidOrUnchangedSnapshotDoesNotReplaceStateOrConsumeRedo()
        {
            var saved=Room();var item=Block();saved.Apply(new[]{item},Array.Empty<string>(),out _);saved.Undo();
            var original=JsonUtility.ToJson(saved.Snapshot());
            var invalid=saved.Snapshot();invalid.objects=invalid.objects.Where(x=>x.id!="book").ToArray();
            Assert.That(saved.ApplySnapshot(invalid,out _),Is.False);
            Assert.That(saved.ApplySnapshot(null,out _),Is.False);
            Assert.That(saved.ApplySnapshot(saved.Snapshot(),out _),Is.True);
            Assert.That(JsonUtility.ToJson(saved.Snapshot()),Is.EqualTo(original));
            Assert.That(saved.CanUndo,Is.False);Assert.That(saved.Redo(),Is.True);Assert.That(saved.Read(item.id),Is.Not.Null);
        }
    }
}
