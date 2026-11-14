// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class RoomStructureTests
    {
        static readonly string Piece=new string('a',32),Group=new string('b',32);
        internal static RoomStructure Structure(string target=null)=>new() {id=Group,name="Castle",slots=new[]{new StructureSlot {slot="left",placement=new ObjectPlacement {target=target??Piece,position=new Vector3(.3f,1,.5f)}}}};
        static RoomDocument Room()=>new() {version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData {id="book",kind=RoomObjectKind.Book},new RoomObjectData {id="maestro",kind=RoomObjectKind.Maestro},new RoomObjectData {id=Piece,kind=RoomObjectKind.Block}}};
        [Test] public void SharedProgramKeepsNativeStructureResultsSeparateFromMemberEditAuthority()
        {
            string text=File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-structure-reset.json"));Assert.That(BehaviourProgram.TryParse(text,out var program,out var error),Is.True,error);var machine=new ProgramMachine(program,null);
            Assert.That(machine.Advance(out var call),Is.EqualTo(ProgramYield.Action),machine.Error);Assert.That(call.Definition.Id,Is.EqualTo("structure.save"));Assert.That(call.Resources,Is.EquivalentTo(new[]{new string('a',32),new string('b',32)}));
            Assert.That(machine.CompleteAction(new JObject {["structureId"]=new string('f',32),["revision"]=7,["temporary"]=false},out error),Is.True,error);
            Assert.That(machine.Advance(out call),Is.EqualTo(ProgramYield.Action),machine.Error);Assert.That((string)call.Arguments["id"],Is.EqualTo(new string('f',32)));Assert.That((int)call.Arguments["revision"],Is.EqualTo(7));Assert.That(call.Resources.Length,Is.EqualTo(2));
            Assert.That(machine.CompleteAction(new JObject {["count"]=2,["temporary"]=false},out error),Is.True,error);Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Completed),machine.Error);
        }
        [Test] public void StructureMetadataIsDetachedJournalledAndIncludedInTemporarySnapshotUndo()
        {
            var journal=new RoomJournal(Room());var definition=Structure();Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out var error,structureEdits:new StructureEdits {Replacements=new[]{definition}}),Is.True,error);
            int first=journal.StructureRevision(Group);definition.slots[0].placement.position=Vector3.one*9;Assert.That(journal.ReadStructure(Group).slots[0].placement.position.x,Is.EqualTo(.3f));
            var fork=journal.Fork();var changed=fork.ReadStructure(Group);changed.name="Rebuilt castle";Assert.That(fork.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out error,structureEdits:new StructureEdits {Replacements=new[]{changed}}),Is.True,error);Assert.That(journal.ReadStructure(Group).name,Is.EqualTo("Castle"));
            Assert.That(journal.ApplySnapshot(fork.Snapshot(),out error),Is.True,error);Assert.That(journal.ReadStructure(Group).name,Is.EqualTo("Rebuilt castle"));Assert.That(journal.Undo(),Is.True);Assert.That(journal.ReadStructure(Group).name,Is.EqualTo("Castle"));Assert.That(journal.StructureRevision(Group),Is.GreaterThan(first));Assert.That(journal.Redo(),Is.True);Assert.That(journal.ReadStructure(Group).name,Is.EqualTo("Rebuilt castle"));
        }
        [Test] public void MissingMembersRemainIdentifiableAndInvalidEditsDoNotChangeTheRoom()
        {
            var room=Room();room.structures=new[]{Structure()};var journal=new RoomJournal(room);Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),new[]{Piece},out var error),Is.True,error);Assert.That(journal.Snapshot().Validate(out error),Is.True,error);Assert.That(journal.ReadStructure(Group).slots[0].placement.target,Is.EqualTo(Piece));
            var before=JsonUtility.ToJson(journal.Snapshot());var bad=Structure();bad.slots=new[]{bad.slots[0],bad.slots[0].Copy()};Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out _,structureEdits:new StructureEdits {Replacements=new[]{bad}}),Is.False);Assert.That(JsonUtility.ToJson(journal.Snapshot()),Is.EqualTo(before));
            Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out _,structureEdits:new StructureEdits {Replacements=new[]{Structure()},Removals=new[]{Group}}),Is.False);Assert.That(journal.Undo(),Is.True);Assert.That(journal.Read(Piece),Is.Not.Null);
        }
        [Test] public void StructuresRoundTripAndFutureDefinitionsCannotFallBackToAnOlderBackup()
        {
            string directory=Path.Combine(Path.GetTempPath(),"MaestroStructures-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try {
                var room=Room();room.structures=new[]{Structure()};var storage=new RoomStorage(directory);Assert.That(storage.Save(room,out var error),Is.True,error);var loaded=storage.Load(out error);Assert.That(loaded.structures.Single().slots.Single().placement.target,Is.EqualTo(Piece));
                Assert.That(storage.Save(room,out error),Is.True,error);room.structures[0].version=2;string future=JsonUtility.ToJson(room);File.WriteAllText(Path.Combine(directory,RoomStorage.FileName),future);
                storage=new RoomStorage(directory);Assert.That(storage.Load(out error),Is.Null);Assert.That(storage.ReadOnly,Is.True);Assert.That(error,Does.Contain("different app version"));Assert.That(File.ReadAllText(Path.Combine(directory,RoomStorage.FileName)),Is.EqualTo(future));
            }finally{Directory.Delete(directory,true);}
        }
    }
}
