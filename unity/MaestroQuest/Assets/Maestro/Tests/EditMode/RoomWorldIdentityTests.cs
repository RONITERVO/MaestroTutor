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
    public sealed class RoomWorldIdentityTests
    {
        string directory;
        [SetUp]public void Setup(){directory=Path.Combine(Path.GetTempPath(),"wi-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);}
        [TearDown]public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        static RoomDocument Room()=>new(){version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}}};
        static string Identity(RoomDocument room)=>JsonUtility.ToJson(room.world);
        [Test]public void NewWorldsHaveIndependentScopeAndCopiesCannotChangeTheirSource()
        {
            var a=Room();var b=Room();Assert.That(a.world.Valid&&b.world.Valid,Is.True);
            Assert.That(a.world.worldId,Is.Not.EqualTo(b.world.worldId));Assert.That(a.world.regionId,Is.Not.EqualTo(b.world.regionId));
            var copy=a.Copy();Assert.That(Identity(copy),Is.EqualTo(Identity(a)));copy.world.worldId=b.world.worldId;
            Assert.That(Identity(copy),Is.Not.EqualTo(Identity(a)));
        }
        [Test]public void EditsNoOpsUndoAndTemporaryJournalsPreserveScopeWithoutConsumingHistory()
        {
            var doc=Room();doc.structures=new[]{RoomStructureTests.Structure()};var journal=new RoomJournal(doc);string identity=Identity(doc);
            Assert.That(journal.ApplySnapshot(journal.Snapshot(),out var error),Is.True,error);Assert.That(journal.CanUndo,Is.False);
            var book=journal.Read("book");book.color=Color.blue;Assert.That(journal.Apply(new[]{book},Array.Empty<string>(),out error),Is.True,error);
            Assert.That(journal.Undo(),Is.True);Assert.That(journal.CanUndo,Is.False);
            Assert.That(journal.ApplySnapshot(journal.Fork().Snapshot(),out error),Is.True,error);Assert.That(journal.CanUndo,Is.False);
            Assert.That(journal.Redo(),Is.True);Assert.That(journal.Read("book").color,Is.EqualTo(Color.blue));
            var fork=journal.Fork();fork.UpdateViewpoint(new RoomViewpoint{active=true,position=Vector3.one,yaw=45});
            Assert.That(journal.ApplySnapshot(fork.Snapshot(),out error),Is.True,error);
            Assert.That(Identity(journal.Snapshot()),Is.EqualTo(identity));Assert.That(Identity(fork.Snapshot()),Is.EqualTo(identity));
            Assert.That(journal.Undo(),Is.True);Assert.That(journal.CanUndo,Is.False);Assert.That(journal.Viewpoint.position,Is.EqualTo(Vector3.one));
            Assert.That(Identity(journal.Snapshot()),Is.EqualTo(identity));
        }
        [TestCase(true)][TestCase(false)]public void SnapshotCannotSubstituteAnotherWorldOrRegion(bool world)
        {
            var journal=new RoomJournal(Room());var before=JsonUtility.ToJson(journal.Snapshot());var changed=journal.Snapshot();
            if(world)changed.world.worldId=Guid.NewGuid().ToString("N");else changed.world.regionId=Guid.NewGuid().ToString("N");
            changed.objects[0].color=Color.blue;Assert.That(journal.ApplySnapshot(changed,out var error),Is.False);Assert.That(error,Does.Contain("different world or region"));
            Assert.That(JsonUtility.ToJson(journal.Snapshot()),Is.EqualTo(before));Assert.That(journal.CanUndo,Is.False);
        }
        [Test]public void LegacyReadIsPassiveAndExplicitIdentityCheckpointIsDurable()
        {
            var doc=Room();doc.version=22;var wire=JObject.Parse(JsonUtility.ToJson(doc));wire.Remove("world");
            string path=Path.Combine(directory,"room.v22.json"),text=wire.ToString();File.WriteAllText(path,text);
            var storage=new RoomStorage(directory);var loaded=storage.Load(out var error);Assert.That(loaded,Is.Not.Null,error);
            Assert.That(loaded.world.Valid,Is.True);Assert.That(loaded.WorldNeedsSave,Is.True);Assert.That(File.Exists(Path.Combine(directory,RoomStorage.FileName)),Is.False);
            Assert.That(storage.PinWorldIdentity(loaded,out error),Is.True,error);Assert.That(File.ReadAllText(path),Is.EqualTo(text));
            var reopened=new RoomStorage(directory).Load(out error);Assert.That(reopened,Is.Not.Null,error);Assert.That(reopened.WorldNeedsSave,Is.False);Assert.That(Identity(reopened),Is.EqualTo(Identity(loaded)));
        }
        [TestCase("world")][TestCase("version")][TestCase("worldId")][TestCase("regionId")][TestCase("extra")][TestCase("duplicate")][TestCase("upper")][TestCase("future")]
        public void InvalidOrMissingCurrentIdentityNeverInventsAReplacement(string change)
        {
            var wire=JObject.Parse(JsonUtility.ToJson(Room()));var world=(JObject)wire["world"];
            if(change=="world")wire.Remove("world");else if(change=="extra")world["unknown"]=true;
            else if(change=="duplicate")world["regionId"]=world["worldId"].DeepClone();else if(change=="upper")world["worldId"]=new string('A',32);
            else if(change=="future")world["version"]=2;else world.Remove(change);
            string path=Path.Combine(directory,RoomStorage.FileName),text=wire.ToString();File.WriteAllText(path,text);
            var storage=new RoomStorage(directory);Assert.That(storage.Load(out _),Is.Null);Assert.That(storage.ReadOnly,Is.True);
            Assert.That(storage.PinWorldIdentity(Room(),out _),Is.False);Assert.That(File.ReadAllText(path),Is.EqualTo(text));
        }
        [Test]public void FutureIdentityDoesNotFallBackToAnOlderValidBackup()
        {
            var doc=Room();var storage=new RoomStorage(directory);Assert.That(storage.Save(doc,out var error),Is.True,error);
            string path=Path.Combine(directory,RoomStorage.FileName),before=File.ReadAllText(path);File.WriteAllText(path+".backup",before);
            var wire=JObject.Parse(before);wire["world"]["version"]=2;string future=wire.ToString();File.WriteAllText(path,future);
            var reopened=new RoomStorage(directory);Assert.That(reopened.Load(out _),Is.Null);Assert.That(reopened.ReadOnly,Is.True);
            Assert.That(File.ReadAllText(path),Is.EqualTo(future));Assert.That(File.ReadAllText(path+".backup"),Is.EqualTo(before));
        }
        [Test]public void PairedRoomAndMemorySnapshotRetainsExactAuthoredScope()
        {
            var before=RoomSnapshotTransaction.Capture(directory);var doc=Room();
            var next=RoomSnapshotTransaction.FromDocuments(doc,ProgramMemoryDocument.Empty());RoomSnapshotTransaction.Publish(directory,before,next);
            var loaded=new RoomStorage(directory).Load(out var error);Assert.That(loaded,Is.Not.Null,error);Assert.That(Identity(loaded),Is.EqualTo(Identity(doc)));
            Assert.That(RoomSnapshotTransaction.Capture(directory).Identity,Is.EqualTo(next.Identity));
        }
    }
}
