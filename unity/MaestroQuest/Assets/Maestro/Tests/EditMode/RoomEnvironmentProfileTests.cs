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
    public sealed class RoomEnvironmentProfileTests
    {
        static RoomDocument Room()=>new(){version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}}};
        static RoomEnvironmentProfile Profile()=>new(){id=new string('a',32),name="Virtual terrain",realCollisions=false};
        [Test]public void SharedDefinitionAndBindingsUndoAtomicallyAndForksInvalidateOldRevisions() {
            var journal=new RoomJournal(Room());var p=Profile();var actor=journal.Read("maestro");actor.environmentProfile=p.id;
            Assert.That(journal.Apply(new[]{actor},Array.Empty<string>(),out var error,environmentEdits:new(){Replacements=new[]{p}}),Is.True,error);
            int revision=journal.EnvironmentRevision(p.id);var copy=journal.Snapshot().Copy();copy.environmentProfiles[0].realCollisions=true;
            Assert.That(journal.ReadEnvironment(p.id).realCollisions,Is.False);
            Assert.That(journal.Undo(),Is.True);Assert.That(journal.Read("maestro").environmentProfile,Is.Empty);Assert.That(journal.ReadEnvironment(p.id),Is.Null);
            Assert.That(journal.Redo(),Is.True);Assert.That(journal.EnvironmentRevision(p.id),Is.GreaterThan(revision));
            var fork=journal.Fork();p.realCollisions=true;
            Assert.That(fork.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out error,environmentEdits:new(){Replacements=new[]{p}}),Is.True,error);
            Assert.That(journal.ReadEnvironment(p.id).realCollisions,Is.False);Assert.That(journal.ApplySnapshot(fork.Snapshot(),out error),Is.True,error);
            Assert.That(journal.ReadEnvironment(p.id).realCollisions,Is.True);Assert.That(journal.Undo(),Is.True);Assert.That(journal.ReadEnvironment(p.id).realCollisions,Is.False);
        }
        [Test]public void IdenticalDefinitionsDoNotAddUndoAndDanglingBindingsNeverFallBack() {
            var journal=new RoomJournal(Room());var p=Profile();var actor=journal.Read("maestro");actor.environmentProfile=p.id;
            Assert.That(journal.Apply(new[]{actor},Array.Empty<string>(),out _,environmentEdits:new(){Replacements=new[]{p}}),Is.True);
            int revision=journal.EnvironmentRevision(p.id);
            Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out _,environmentEdits:new(){Replacements=new[]{p.Copy()}}),Is.True);
            Assert.That(journal.EnvironmentRevision(p.id),Is.EqualTo(revision));
            Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out _,environmentEdits:new(){Removals=new[]{p.id}}),Is.False);
            Assert.That(journal.Undo(),Is.True);Assert.That(journal.CanUndo,Is.False);
            var room=Room();room.objects[1].environmentProfile=p.id;Assert.That(room.Validate(out _),Is.False);
            room.environmentProfiles=new[]{p};Assert.That(room.Validate(out _),Is.True);
            room.environmentProfiles=new[]{p,p.Copy()};Assert.That(room.Validate(out _),Is.False);
        }
        [Test]public void WireRequiresExplicitCollisionPolicyAndBindingAndStorageRoundTripsThem() {
            var doc=Room();var p=Profile();doc.environmentProfiles=new[]{p};doc.objects[1].environmentProfile=p.id;
            var wire=JObject.Parse(JsonUtility.ToJson(doc));Assert.That(RoomEnvironmentProfile.ValidWire(wire),Is.True);
            ((JObject)wire["environmentProfiles"][0]).Remove("realCollisions");Assert.That(RoomEnvironmentProfile.ValidWire(wire),Is.False);
            wire=JObject.Parse(JsonUtility.ToJson(doc));((JObject)wire["objects"][0]).Remove("environmentProfile");Assert.That(RoomEnvironmentProfile.ValidWire(wire),Is.False);
            string directory=Path.Combine(Path.GetTempPath(),"MaestroEnvironment-"+Guid.NewGuid().ToString("N"));
            try {
                var storage=new RoomStorage(directory);Assert.That(storage.Save(doc,out var error),Is.True,error);
                var restored=storage.Load(out error);Assert.That(restored,Is.Not.Null,error);Assert.That(restored.environmentProfiles.Single().realCollisions,Is.False);
                Assert.That(restored.objects.Single(x=>x.id=="maestro").environmentProfile,Is.EqualTo(p.id));
                var snapshot=RoomSnapshotTransaction.FromDocuments(restored,ProgramMemoryDocument.Empty());Assert.That(snapshot,Is.Not.Null);
            }finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
        [Test]public void WorstCaseProfileFactsFitExistingProgramBudgets() {
            string name=new string('"',80),id=new string('a',32);
            var profile=new JObject{["id"]=id,["revision"]=9007199254740991d,["name"]=name,["realCollisions"]=true,["members"]=new JArray(Enumerable.Repeat(id,16)),["temporary"]=false};
            Assert.That(EnvironmentProfileFacts.Profile().ValidValue(ProgramValue.Literal(profile,EnvironmentProfileFacts.Profile().Type)),Is.True);
            var entry=new JObject{["id"]=id,["revision"]=9007199254740991d,["name"]=name,["realCollisions"]=true};
            var page=new JObject{["offset"]=16,["total"]=16,["pageSize"]=3,["entries"]=new JArray(entry,entry.DeepClone(),entry.DeepClone())};
            Assert.That(EnvironmentProfileFacts.Profiles().ValidValue(ProgramValue.Literal(page,EnvironmentProfileFacts.Profiles().Type)),Is.True);
        }
        [Test]public void LegacyRoomInheritsGlobalEnvironmentAndFutureProfilesPreserveFiles() {
            string directory=Path.Combine(Path.GetTempPath(),"MaestroEnvironment-"+Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(directory);var doc=Room();doc.version=23;var wire=JObject.Parse(JsonUtility.ToJson(doc));wire.Remove("environmentProfiles");
                foreach(JObject item in (JArray)wire["objects"])item.Remove("environmentProfile");
                File.WriteAllText(Path.Combine(directory,"room.v23.json"),wire.ToString());var storage=new RoomStorage(directory);var loaded=storage.Load(out var error);
                Assert.That(loaded,Is.Not.Null,error);Assert.That(loaded.objects.All(x=>x.environmentProfile==""),Is.True);
                var p=Profile();p.version=2;doc.version=RoomDocument.CurrentVersion;doc.environmentProfiles=new[]{p};string raw=JsonUtility.ToJson(doc),path=Path.Combine(directory,RoomStorage.FileName);File.WriteAllText(path,raw);
                storage=new RoomStorage(directory);Assert.That(storage.Load(out _),Is.Null);Assert.That(storage.ReadOnly,Is.True);Assert.That(File.ReadAllText(path),Is.EqualTo(raw));
            }finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
    }
}
