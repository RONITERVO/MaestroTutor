// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class RoomSnapshotTransactionTests
    {
        const string Program="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",Cell="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        string directory;
        string PathFor(string name)=>Path.Combine(directory,name);
        static RoomDocument Room(float x)=>new(){version=2,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book,position=new Vector3(x,1,0)},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}}};
        static ProgramMemoryDocument Memory(double n)=>ProgramMemoryDocument.Empty().WithValues(Program,new Dictionary<string,ProgramMemoryDocument.Cell>{{Cell,new("count",new ProgramValue(n))}});
        static RoomSnapshotTransaction.Snapshot Pair(int n)=>RoomSnapshotTransaction.FromDocuments(Room(n),Memory(n));
        [SetUp]public void Setup(){directory=Path.Combine(Path.GetTempPath(),"maestro-paired-snapshot-"+Guid.NewGuid().ToString("N"));}
        [TearDown]public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        RoomSnapshotTransaction.Snapshot Seed(int n=1){var next=Pair(n);RoomSnapshotTransaction.Publish(directory,RoomSnapshotTransaction.Capture(directory),next);return next;}
        static Action<string> StopAt(string stage)=>value=>{if(value==stage)throw new IOException("Injected stop at "+stage);};
        Dictionary<string,string> Evidence()=>Directory.GetFiles(directory).ToDictionary(Path.GetFileName,p=>Convert.ToBase64String(File.ReadAllBytes(p)));
        void SameEvidence(Dictionary<string,string> before){Assert.That(Evidence(),Is.EquivalentTo(before));}
        [Test]public void CapturesDetachedValuesAndPublishesTheExactPairWithPriorBackups()
        {
            var empty=RoomSnapshotTransaction.Capture(directory);Assert.That(empty.Room,Is.Null);Assert.That(empty.Memory,Is.Null);
            var room=Room(1);var memory=Memory(1);var first=RoomSnapshotTransaction.FromDocuments(room,memory);room.objects[0].position=Vector3.zero;
            byte[] copy=first.Room;copy[0]=0;
            string id=RoomSnapshotTransaction.Publish(directory,empty,first);Assert.That(id.Length,Is.EqualTo(32));
            Assert.That(RoomSnapshotTransaction.Capture(directory).Identity,Is.EqualTo(first.Identity));
            var next=Pair(2);RoomSnapshotTransaction.Publish(directory,first,next);
            Assert.That(File.ReadAllBytes(PathFor("room.v2.json.backup")),Is.EqualTo(first.Room));
            Assert.That(File.ReadAllBytes(PathFor(ProgramMemoryStore.FileName+".backup")),Is.EqualTo(first.Memory));
            Assert.That(File.Exists(PathFor(RoomSnapshotTransaction.FileName)),Is.False);
            Assert.That(RoomSnapshotTransaction.Capture(directory).Identity,Is.EqualTo(next.Identity));
        }
        [TestCase("before-journal",false)][TestCase("prepared",false)][TestCase("room",false)][TestCase("memory",false)]
        [TestCase("committed",true)][TestCase("recovered-room",true)][TestCase("recovered-memory",true)][TestCase("room-backup",true)][TestCase("memory-backup",true)]
        public void InterruptedPublicationRecoversOneWholePair(string stage,bool committed)
        {
            var earlier=Seed();var before=Seed(2);var after=Pair(3);
            Assert.Throws<IOException>(()=>RoomSnapshotTransaction.Publish(directory,before,after,StopAt(stage)));
            var recovered=RoomSnapshotTransaction.Capture(directory,out var outcome);
            Assert.That(recovered.Identity,Is.EqualTo((committed?after:before).Identity));
            Assert.That(outcome?.Committed,Is.EqualTo(stage=="before-journal"?(bool?)null:committed));
            Assert.That(File.ReadAllBytes(PathFor("room.v2.json.backup")),Is.EqualTo((committed?before:earlier).Room));
            Assert.That(File.ReadAllBytes(PathFor(ProgramMemoryStore.FileName+".backup")),Is.EqualTo((committed?before:earlier).Memory));
            Assert.That(RoomSnapshotTransaction.Capture(directory,out outcome).Identity,Is.EqualTo(recovered.Identity));Assert.That(outcome,Is.Null);
        }
        [TestCase("prepared",false)][TestCase("room",false)][TestCase("memory",false)][TestCase("committed",true)]
        public void FirstSaveRecoveryDoesNotInventMissingPrimariesOrBackups(string stage,bool committed)
        {
            var before=RoomSnapshotTransaction.Capture(directory);var after=Pair(1);
            Assert.Throws<IOException>(()=>RoomSnapshotTransaction.Publish(directory,before,after,StopAt(stage)));
            var recovered=RoomSnapshotTransaction.Capture(directory);
            Assert.That(recovered.Identity,Is.EqualTo((committed?after:before).Identity));
            Assert.That(File.Exists(PathFor("room.v2.json.backup")),Is.False);Assert.That(File.Exists(PathFor(ProgramMemoryStore.FileName+".backup")),Is.False);
        }
        [TestCase("room",false)][TestCase("committed",true)]
        public void RecoveryCanItselfBeInterruptedAndRepeated(string stage,bool committed)
        {
            var before=Seed();var after=Pair(2);
            Assert.Throws<IOException>(()=>RoomSnapshotTransaction.Publish(directory,before,after,StopAt(stage)));
            Assert.Throws<IOException>(()=>RoomSnapshotTransaction.Capture(directory,out _,StopAt("recovered-room")));
            Assert.That(File.Exists(PathFor(RoomSnapshotTransaction.FileName)),Is.True);
            Assert.That(RoomSnapshotTransaction.Capture(directory).Identity,Is.EqualTo((committed?after:before).Identity));
        }
        [TestCase("room.v2.json",false)][TestCase("program-memory.v1.json",false)][TestCase("room.v2.json.backup",true)][TestCase("program-memory.v1.json.backup",true)]
        public void RecoveryPreservesUnexpectedPrimariesAndBackupsBeforeAnyFurtherWrite(string file,bool backup)
        {
            var before=Seed();var after=Pair(2);var outside=Pair(7);
            Assert.Throws<IOException>(()=>RoomSnapshotTransaction.Publish(directory,before,after,StopAt(backup?"committed":"room")));
            File.WriteAllBytes(PathFor(file),file.StartsWith("room.",StringComparison.Ordinal)?outside.Room:outside.Memory);
            var evidence=Evidence();Assert.Throws<InvalidDataException>(()=>RoomSnapshotTransaction.Capture(directory));SameEvidence(evidence);
        }
        [Test]public void StaleSnapshotCannotOverwriteAnOutsideMemoryEdit()
        {
            var before=Seed();File.WriteAllBytes(PathFor(ProgramMemoryStore.FileName),Pair(7).Memory);var evidence=Evidence();
            Assert.Throws<InvalidDataException>(()=>RoomSnapshotTransaction.Publish(directory,before,Pair(2)));SameEvidence(evidence);
        }
        [Test]public void OutsideEditDuringDispatchIsPreservedBeforeTheNextWrite()
        {
            var before=Seed();var after=Pair(2);var outside=Pair(7);
            Assert.Throws<InvalidDataException>(()=>RoomSnapshotTransaction.Publish(directory,before,after,stage=>{if(stage=="room")File.WriteAllBytes(PathFor(ProgramMemoryStore.FileName),outside.Memory);}));
            var evidence=Evidence();Assert.Throws<InvalidDataException>(()=>RoomSnapshotTransaction.Capture(directory));SameEvidence(evidence);
        }
        [Test]public void OutsideJournalEditDuringDispatchIsNeverOverwritten()
        {
            var before=Seed();var after=Pair(2);
            Assert.Throws<InvalidDataException>(()=>RoomSnapshotTransaction.Publish(directory,before,after,stage=>{if(stage=="room")File.WriteAllText(PathFor(RoomSnapshotTransaction.FileName),"damaged");}));
            Assert.That(File.ReadAllBytes(PathFor(ProgramMemoryStore.FileName)),Is.EqualTo(before.Memory));Assert.That(File.ReadAllText(PathFor(RoomSnapshotTransaction.FileName)),Is.EqualTo("damaged"));
        }
        [Test]public void ConcurrentOwnersCannotReadTheIntermediatePair()
        {
            var before=Seed();var after=Pair(2);using var reached=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
            var worker=Task.Run(()=>RoomSnapshotTransaction.Publish(directory,before,after,stage=>{if(stage=="room"){reached.Set();if(!release.Wait(TimeSpan.FromSeconds(20)))throw new TimeoutException();}}));
            try {Assert.That(reached.Wait(TimeSpan.FromSeconds(10)),Is.True);Assert.Throws<IOException>(()=>RoomSnapshotTransaction.Capture(directory));}
            finally{release.Set();worker.GetAwaiter().GetResult();}
            Assert.That(RoomSnapshotTransaction.Capture(directory).Identity,Is.EqualTo(after.Identity));
        }
        [TestCase("room-snapshot.v2.json")][TestCase("room-snapshot.v1.json.snapshot.aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")][TestCase("program-memory.v1.json.snapshot.aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
        [TestCase("room.v3.json")][TestCase("room.v3.json.backup")][TestCase("room.v2.json.pending")]
        [TestCase("program-memory.v2.json")][TestCase("program-memory.v1.json.pending.aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
        public void UnrecognizedOrStagedEvidenceIsPreserved(string name)
        {
            var before=Seed();File.WriteAllText(PathFor(name),"unconfirmed");var evidence=Evidence();
            Assert.Throws<InvalidDataException>(()=>RoomSnapshotTransaction.Publish(directory,before,Pair(2)));SameEvidence(evidence);
        }
        [TestCase("version")][TestCase("after")][TestCase("duplicate")][TestCase("oversize")]
        public void DamagedIntentCannotSelectOrPublishEitherPair(string damage)
        {
            var before=Seed();Assert.Throws<IOException>(()=>RoomSnapshotTransaction.Publish(directory,before,Pair(2),StopAt("room")));
            string path=PathFor(RoomSnapshotTransaction.FileName);var json=JObject.Parse(File.ReadAllText(path));
            if(damage=="version")json["version"]=99;
            if(damage=="after")json["after"][1]=Convert.ToBase64String(Encoding.UTF8.GetBytes("{}"));
            if(damage=="oversize"){using var file=new FileStream(path,FileMode.Create);file.SetLength(RoomSnapshotTransaction.JournalLimit+1);}
            else File.WriteAllText(path,damage=="duplicate"?json.ToString().Replace("\"version\": 1","\"version\": 1, \"version\": 1"):json.ToString());
            var evidence=Evidence();Assert.Catch(()=>RoomSnapshotTransaction.Capture(directory));SameEvidence(evidence);
        }
        [Test]public void InvalidCandidateAndOrphanBackupNeverInitializeANewPair()
        {
            var empty=RoomSnapshotTransaction.Capture(directory);var invalid=new RoomSnapshotTransaction.Snapshot(new[]{Encoding.UTF8.GetBytes("{}"),Memory(1).Encode()});
            Assert.Throws<InvalidDataException>(()=>RoomSnapshotTransaction.Publish(directory,empty,invalid));
            File.WriteAllBytes(PathFor(ProgramMemoryStore.FileName+".backup"),Memory(2).Encode());var evidence=Evidence();
            Assert.Throws<InvalidDataException>(()=>RoomSnapshotTransaction.Capture(directory));SameEvidence(evidence);
        }
        [Test]public void ChangedIntentDuringRecoveryCannotOverwriteAnotherDocument()
        {
            var before=Seed();Assert.Throws<IOException>(()=>RoomSnapshotTransaction.Publish(directory,before,Pair(2),StopAt("memory")));
            var outside=Pair(7);
            Assert.Throws<InvalidDataException>(()=>RoomSnapshotTransaction.Capture(directory,out _,stage=>{if(stage=="recovered-room")File.WriteAllBytes(PathFor(ProgramMemoryStore.FileName),outside.Memory);}));
            Assert.That(File.ReadAllBytes(PathFor(ProgramMemoryStore.FileName)),Is.EqualTo(outside.Memory));
        }
    }
}
