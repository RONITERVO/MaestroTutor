// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class RoomRegionPersistenceTests
    {
        static readonly string Target=new string('1',32),Other=new string('2',32);
        static RoomDocument Room()=>new(){version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},new RoomObjectData{id=Target,kind=RoomObjectKind.Block},new RoomObjectData{id=Other,kind=RoomObjectKind.Block}}};
        static RoomRegion Region(char id,params string[] members)=>new(){id=new string(id,32),name="Garden",members=members};
        [Test] public void RegionMembershipRejectsDanglingDuplicateAndWorldOwnedTargets() {
            var room=Room();room.regions=new[]{Region('a',Target)};Assert.That(room.Validate(out var error),Is.True,error);
            foreach(var member in new[]{"book","maestro",new string('3',32),Target}) {
                room.regions=new[]{Region('a',Target),Region('b',member)};Assert.That(room.Validate(out _),Is.False,member);
            }
            foreach(var id in new[]{room.world.worldId,room.world.regionId}) {room.regions=new[]{new RoomRegion{id=id,name="Conflict"}};Assert.That(room.Validate(out _),Is.False);}
            room.regions=new[]{Region('a',Target,Target)};Assert.That(room.Validate(out _),Is.False);
            room.regions=new[]{Region('a')};room.regions[0].version=2;Assert.That(room.Validate(out _),Is.False);
            var edit=new RegionEdits{Replacements=new[]{Region('a')},Removals=new[]{new string('a',32)}};Assert.That(edit.Validate(out error),Is.False);Assert.That(error,Is.Not.Null.And.Not.Empty);
        }
        [Test] public void TransferDeletionUndoAndRedoPreserveIdentityAndInvalidateEarlierObservations() {
            var journal=new RoomJournal(Room());RoomRegion a=Region('a',Target),b=Region('b');
            Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out var error,regionEdits:new(){Replacements=new[]{a,b}}),Is.True,error);
            int before=journal.ObjectRevision(Target);a.members=Array.Empty<string>();b.members=new[]{Target};
            Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out error,regionEdits:new(){Replacements=new[]{a,b}}),Is.True,error);
            Assert.That(journal.RegionFor(Target),Is.EqualTo(b.id));Assert.That(journal.ObjectRevision(Target),Is.GreaterThan(before));
            var copy=journal.Snapshot().Copy();copy.regions.Single(x=>x.id==b.id).members[0]=Other;Assert.That(journal.RegionFor(Target),Is.EqualTo(b.id));
            Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),new[]{Target},out error),Is.True,error);Assert.That(journal.ReadRegion(b.id).members,Is.Empty);
            Assert.That(journal.Undo(),Is.True);Assert.That(journal.Read(Target),Is.Not.Null);Assert.That(journal.RegionFor(Target),Is.EqualTo(b.id));
            Assert.That(journal.Redo(),Is.True);Assert.That(journal.Read(Target),Is.Null);Assert.That(journal.ReadRegion(b.id).members,Is.Empty);
            Assert.That(journal.Undo(),Is.True);Assert.That(journal.Undo(),Is.True);Assert.That(journal.RegionFor(Target),Is.EqualTo(a.id));Assert.That(journal.ReadRegion(b.id).members,Is.Empty);
        }
        [Test] public void NoopKeepsHistoryAndForksKeepMembershipIsolatedUntilAccepted() {
            var room=Room();room.regions=new[]{Region('a',Target,Other)};var journal=new RoomJournal(room);var region=journal.RegionSnapshot().Single();int revision=journal.RegionRevision(region.id);
            region.members=region.members.Reverse().ToArray();Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out var error,regionEdits:new(){Replacements=new[]{region}}),Is.True,error);
            Assert.That(journal.RegionRevision(region.id),Is.EqualTo(revision));Assert.That(journal.CanUndo,Is.False);
            var fork=journal.Fork();region.name="Café garden";region.members=new[]{Other};
            Assert.That(fork.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out error,regionEdits:new(){Replacements=new[]{region}}),Is.True,error);
            Assert.That(journal.RegionFor(Target),Is.EqualTo(region.id));Assert.That(journal.ApplySnapshot(fork.Snapshot(),out error),Is.True,error);
            Assert.That(journal.RegionFor(Target),Is.Empty);Assert.That(journal.Undo(),Is.True);Assert.That(journal.RegionFor(Target),Is.EqualTo(region.id));
        }
        [Test] public void CurrentStorageAndSnapshotKeepRegionsAndRejectUnsupportedWireFields() {
            var doc=Room();doc.regions=new[]{Region('a',Target)};var wire=JObject.Parse(JsonUtility.ToJson(doc));Assert.That(RoomRegion.ValidWire(wire),Is.True);
            wire["regions"][0]["streaming"]=true;Assert.That(RoomRegion.ValidWire(wire),Is.False);
            wire=JObject.Parse(JsonUtility.ToJson(doc));wire["regions"][0]["members"]=new JArray(1);Assert.That(RoomRegion.ValidWire(wire),Is.False);
            wire=JObject.Parse(JsonUtility.ToJson(doc));wire.Remove("regions");Assert.That(RoomRegion.ValidWire(wire),Is.False);
            string path=Path.Combine(Path.GetTempPath(),"MaestroRegions-"+Guid.NewGuid().ToString("N"));
            try {var storage=new RoomStorage(path);Assert.That(storage.Save(doc,out var error),Is.True,error);var loaded=storage.Load(out error);Assert.That(loaded,Is.Not.Null,error);Assert.That(loaded.regions.Single().members,Is.EqualTo(new[]{Target}));Assert.That(loaded.world.worldId,Is.EqualTo(doc.world.worldId));Assert.That(RoomSnapshotTransaction.FromDocuments(loaded,ProgramMemoryDocument.Empty()),Is.Not.Null);}
            finally{if(Directory.Exists(path))Directory.Delete(path,true);}
        }
        [Test] public void PreviousFormatLoadsHomeMembershipWithoutRewritingAndFutureAreaIsProtected() {
            string path=Path.Combine(Path.GetTempPath(),"MaestroRegions-"+Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(path);var doc=Room();doc.version=35;var wire=JObject.Parse(JsonUtility.ToJson(doc));wire.Remove("regions");string raw=wire.ToString(),legacy=Path.Combine(path,"room.v35.json");File.WriteAllText(legacy,raw);
                var storage=new RoomStorage(path);var loaded=storage.Load(out var error);Assert.That(loaded,Is.Not.Null,error);Assert.That(loaded.regions,Is.Empty);Assert.That(File.ReadAllText(legacy),Is.EqualTo(raw));Assert.That(File.Exists(Path.Combine(path,RoomStorage.FileName)),Is.False);
                Assert.That(storage.Save(loaded,out error),Is.True,error);doc.version=RoomDocument.CurrentVersion;doc.regions=new[]{Region('a',Target)};doc.regions[0].version=2;raw=JsonUtility.ToJson(doc);string current=Path.Combine(path,RoomStorage.FileName);File.WriteAllText(current,raw);
                storage=new RoomStorage(path);Assert.That(storage.Load(out _),Is.Null);Assert.That(storage.ReadOnly,Is.True);Assert.That(File.ReadAllText(current),Is.EqualTo(raw));
            }finally{if(Directory.Exists(path))Directory.Delete(path,true);}
        }
    }
}
