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
    public sealed class RoomVisibilityPersistenceTests
    {
        static RoomDocument Room()=>new(){version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}}};
        static RoomVisibilityLayer Profile()=>new(){id=new string('a',32),name="Virtual terrain",realDepth=false};
        [Test]public void SharedDefinitionAndBindingsUndoAtomicallyAndForksInvalidateOldRevisions() {
            var journal=new RoomJournal(Room());var p=Profile();var actor=journal.Read("maestro");actor.visibilityLayer=p.id;
            Assert.That(journal.Apply(new[]{actor},Array.Empty<string>(),out var error,visibilityEdits:new(){Replacements=new[]{p}}),Is.True,error);
            int revision=journal.VisibilityRevision(p.id);var copy=journal.Snapshot().Copy();copy.visibilityLayers[0].realDepth=true;
            Assert.That(journal.ReadVisibility(p.id).realDepth,Is.False);
            Assert.That(journal.Undo(),Is.True);Assert.That(journal.Read("maestro").visibilityLayer,Is.Empty);Assert.That(journal.ReadVisibility(p.id),Is.Null);
            Assert.That(journal.Redo(),Is.True);Assert.That(journal.VisibilityRevision(p.id),Is.GreaterThan(revision));
            var fork=journal.Fork();p.realDepth=true;
            Assert.That(fork.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out error,visibilityEdits:new(){Replacements=new[]{p}}),Is.True,error);
            Assert.That(journal.ReadVisibility(p.id).realDepth,Is.False);Assert.That(journal.ApplySnapshot(fork.Snapshot(),out error),Is.True,error);
            Assert.That(journal.ReadVisibility(p.id).realDepth,Is.True);Assert.That(journal.Undo(),Is.True);Assert.That(journal.ReadVisibility(p.id).realDepth,Is.False);
        }
        [Test]public void IdenticalDefinitionsDoNotAddUndoAndDanglingBindingsNeverFallBack() {
            var journal=new RoomJournal(Room());var p=Profile();var actor=journal.Read("maestro");actor.visibilityLayer=p.id;
            Assert.That(journal.Apply(new[]{actor},Array.Empty<string>(),out _,visibilityEdits:new(){Replacements=new[]{p}}),Is.True);
            int revision=journal.VisibilityRevision(p.id);
            Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out _,visibilityEdits:new(){Replacements=new[]{p.Copy()}}),Is.True);
            Assert.That(journal.VisibilityRevision(p.id),Is.EqualTo(revision));
            Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out _,visibilityEdits:new(){Removals=new[]{p.id}}),Is.False);
            Assert.That(journal.Undo(),Is.True);Assert.That(journal.CanUndo,Is.False);
            var room=Room();room.objects[1].visibilityLayer=p.id;Assert.That(room.Validate(out _),Is.False);
            room.visibilityLayers=new[]{p};Assert.That(room.Validate(out _),Is.True);
            room.visibilityLayers=new[]{p,p.Copy()};Assert.That(room.Validate(out _),Is.False);
        }
        [Test]public void WireRequiresExplicitCollisionPolicyAndBindingAndStorageRoundTripsThem() {
            var doc=Room();var p=Profile();doc.visibilityLayers=new[]{p};doc.objects[1].visibilityLayer=p.id;
            var wire=JObject.Parse(JsonUtility.ToJson(doc));Assert.That(RoomVisibilityLayer.ValidWire(wire),Is.True);
            ((JObject)wire["visibilityLayers"][0]).Remove("realDepth");Assert.That(RoomVisibilityLayer.ValidWire(wire),Is.False);
            wire=JObject.Parse(JsonUtility.ToJson(doc));((JObject)wire["objects"][0]).Remove("visibilityLayer");Assert.That(RoomVisibilityLayer.ValidWire(wire),Is.False);
            string directory=Path.Combine(Path.GetTempPath(),"MaestroEnvironment-"+Guid.NewGuid().ToString("N"));
            try {
                var storage=new RoomStorage(directory);Assert.That(storage.Save(doc,out var error),Is.True,error);
                var restored=storage.Load(out error);Assert.That(restored,Is.Not.Null,error);Assert.That(restored.visibilityLayers.Single().realDepth,Is.False);
                Assert.That(restored.objects.Single(x=>x.id=="maestro").visibilityLayer,Is.EqualTo(p.id));
                var snapshot=RoomSnapshotTransaction.FromDocuments(restored,ProgramMemoryDocument.Empty());Assert.That(snapshot,Is.Not.Null);
            }finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
        [Test]public void EveryRoomObjectCanShareALayerAndInvalidNumbersAreRejected() {
            var doc=Room();var layer=Profile();doc.visibilityLayers=new[]{layer};
            doc.objects=doc.objects.Concat(Enumerable.Range(1,64).Select(i=>new RoomObjectData{id=i.ToString("x32"),kind=RoomObjectKind.Block})).ToArray();
            foreach(var item in doc.objects)item.visibilityLayer=layer.id;
            Assert.That(doc.Validate(out var error),Is.True,error);
            foreach(var invalid in new[]{float.NaN,float.PositiveInfinity,-.01f,1.01f}){layer.opacity=invalid;Assert.That(doc.Validate(out _),Is.False);}
            layer.opacity=0;Assert.That(doc.Validate(out error),Is.True,error);
        }
        [Test]public void LegacyRoomInheritsGlobalEnvironmentAndFutureProfilesPreserveFiles() {
            string directory=Path.Combine(Path.GetTempPath(),"MaestroEnvironment-"+Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(directory);var doc=Room();doc.version=25;var wire=JObject.Parse(JsonUtility.ToJson(doc));wire.Remove("visibilityLayers");
                foreach(JObject item in (JArray)wire["objects"])item.Remove("visibilityLayer");
                File.WriteAllText(Path.Combine(directory,"room.v25.json"),wire.ToString());var storage=new RoomStorage(directory);var loaded=storage.Load(out var error);
                Assert.That(loaded,Is.Not.Null,error);Assert.That(loaded.objects.All(x=>x.visibilityLayer==""),Is.True);
                var p=Profile();p.version=2;doc.version=RoomDocument.CurrentVersion;doc.visibilityLayers=new[]{p};string raw=JsonUtility.ToJson(doc),path=Path.Combine(directory,RoomStorage.FileName);File.WriteAllText(path,raw);
                storage=new RoomStorage(directory);Assert.That(storage.Load(out _),Is.Null);Assert.That(storage.ReadOnly,Is.True);Assert.That(File.ReadAllText(path),Is.EqualTo(raw));
            }finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
    }
}
