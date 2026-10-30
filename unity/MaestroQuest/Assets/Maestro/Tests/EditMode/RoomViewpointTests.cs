// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using Maestro.Quest.Creation;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class RoomViewpointTests
    {
        string directory;
        [SetUp]public void Setup(){directory=Path.Combine(Path.GetTempPath(),"mv-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);}
        [TearDown]public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        static RoomDocument Room()=>new(){version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}}};
        [Test]public void LegacyRoomOpensAtEntryWithoutRewritingItsOriginal()
        {
            var doc=Room();doc.version=21;var wire=JObject.Parse(JsonUtility.ToJson(doc));wire.Remove("viewpoint");
            string path=Path.Combine(directory,"room.v21.json"),text=wire.ToString();File.WriteAllText(path,text);
            var loaded=new RoomStorage(directory).Load(out var error);Assert.That(loaded,Is.Not.Null,error);Assert.That(loaded.viewpoint.active,Is.False);
            Assert.That(File.ReadAllText(path),Is.EqualTo(text));Assert.That(File.Exists(Path.Combine(directory,RoomStorage.FileName)),Is.False);
        }
        [TestCase("viewpoint")][TestCase("version")][TestCase("active")][TestCase("position")][TestCase("yaw")][TestCase("x")][TestCase("future")]
        public void MissingOrFutureViewpointPreservesTheUnreadableSave(string missing)
        {
            var wire=JObject.Parse(JsonUtility.ToJson(Room()));var view=(JObject)wire["viewpoint"];
            if(missing=="viewpoint")wire.Remove(missing);else if(missing=="x")((JObject)view["position"]).Remove("x");else if(missing=="future")view["version"]=2;else view.Remove(missing);
            string path=Path.Combine(directory,RoomStorage.FileName),text=wire.ToString();File.WriteAllText(path,text);
            var storage=new RoomStorage(directory);Assert.That(storage.Load(out _),Is.Null);Assert.That(storage.ReadOnly,Is.True);Assert.That(File.ReadAllText(path),Is.EqualTo(text));
        }
        [Test]public void ViewpointSurvivesForkSnapshotAndGeometryUndoWithoutChangingObjectRevisions()
        {
            var journal=new RoomJournal(Room());int revision=journal.ObjectRevision("book");
            var view=new RoomViewpoint{active=true,position=new Vector3(2,3,-4),yaw=48};Assert.That(journal.UpdateViewpoint(view),Is.True);
            view.position=Vector3.zero;Assert.That(journal.Viewpoint.position,Is.EqualTo(new Vector3(2,3,-4)));Assert.That(journal.CanUndo,Is.False);Assert.That(journal.ObjectRevision("book"),Is.EqualTo(revision));
            var book=journal.Read("book");book.position=Vector3.right;Assert.That(journal.Apply(new[]{book},Array.Empty<string>(),out _),Is.True);
            var fork=journal.Fork();fork.UpdateViewpoint(new RoomViewpoint{active=true,position=Vector3.one,yaw=-90});Assert.That(journal.Viewpoint.position,Is.Not.EqualTo(Vector3.one));
            Assert.That(journal.ApplySnapshot(fork.Snapshot(),out _),Is.True);Assert.That(journal.Undo(),Is.True);Assert.That(journal.Viewpoint.position,Is.EqualTo(Vector3.one));Assert.That(journal.Read("book").position,Is.EqualTo(Vector3.zero));
            Assert.That(journal.Snapshot().Copy().viewpoint.position,Is.EqualTo(Vector3.one));
        }
        [Test]public void ViewpointAndMemoryTravelThroughTheSameAtomicSnapshot()
        {
            var before=RoomSnapshotTransaction.Capture(directory);var doc=Room();doc.viewpoint=new RoomViewpoint{active=true,position=new Vector3(4,2,6),yaw=125};
            var next=RoomSnapshotTransaction.FromDocuments(doc,ProgramMemoryDocument.Empty());RoomSnapshotTransaction.Publish(directory,before,next);
            var reloaded=new RoomStorage(directory).Load(out var error);Assert.That(reloaded,Is.Not.Null,error);Assert.That(JsonUtility.ToJson(reloaded.viewpoint),Is.EqualTo(JsonUtility.ToJson(doc.viewpoint)));
            Assert.That(RoomSnapshotTransaction.Capture(directory).Identity,Is.EqualTo(next.Identity));
        }
        [Test]public void InvalidLocationsAreRejectedWithoutLosingLastValidBookmark()
        {
            var journal=new RoomJournal(Room());var accepted=new RoomViewpoint{active=true,position=Vector3.right,yaw=30};Assert.That(journal.UpdateViewpoint(accepted),Is.True);
            foreach(var value in new[]{new RoomViewpoint{active=true,yaw=float.NaN},new RoomViewpoint{active=true,yaw=181},new RoomViewpoint{active=true,position=Vector3.right*26},new RoomViewpoint{position=Vector3.one}})
                Assert.That(journal.UpdateViewpoint(value),Is.False);
            Assert.That(JsonUtility.ToJson(journal.Viewpoint),Is.EqualTo(JsonUtility.ToJson(accepted)));
        }
    }
}
