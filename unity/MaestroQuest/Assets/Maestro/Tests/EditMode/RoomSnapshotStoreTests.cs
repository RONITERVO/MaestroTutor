// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class RoomSnapshotStoreTests
    {
        const string Program="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",Cell="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        string directory;readonly List<ProgramMemoryStore> stores=new();
        string PathFor(string name)=>Path.Combine(directory,name);
        static RoomDocument Room(float x)=>new(){version=2,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book,position=new Vector3(x,1,0)},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}}};
        static Dictionary<string,ProgramMemoryDocument.Cell> Values(double n)=>new(){{Cell,new("count",new ProgramValue(n))}};
        static ProgramMemoryDocument Memory(double n)=>ProgramMemoryDocument.Empty().WithValues(Program,Values(n));
        static RoomSnapshotTransaction.Snapshot Pair(int n)=>RoomSnapshotTransaction.FromDocuments(Room(n),Memory(n));
        static double Count(ProgramMemoryDocument memory)=>memory.Programs[Program][Cell].Value.Number;
        ProgramMemoryStore Open(Action<string> fault=null){var memory=new ProgramMemoryStore(directory,new(),fault);stores.Add(memory);memory.Initialization.GetAwaiter().GetResult();return memory;}
        [SetUp]public void Setup(){directory=Path.Combine(Path.GetTempPath(),"maestro-snapshot-store-"+Guid.NewGuid().ToString("N"));}
        [TearDown]public void Cleanup(){foreach(var memory in stores)memory.Drain().GetAwaiter().GetResult();stores.Clear();if(Directory.Exists(directory))Directory.Delete(directory,true);}
        RoomSnapshotTransaction.Snapshot Seed(){var pair=Pair(1);RoomSnapshotTransaction.Publish(directory,RoomSnapshotTransaction.Capture(directory),pair);return pair;}
        void Interrupted(RoomSnapshotTransaction.Snapshot before,string phase){Assert.Throws<IOException>(()=>RoomSnapshotTransaction.Publish(directory,before,Pair(2),stage=>{if(stage==phase)throw new IOException("Interrupted");}));}
        Dictionary<string,string> Evidence()=>Directory.GetFiles(directory).ToDictionary(Path.GetFileName,p=>Convert.ToBase64String(File.ReadAllBytes(p)));
        [TestCase("room",1)][TestCase("memory",1)][TestCase("committed",2)]
        public void RoomThenMemoryStartupLoadsTheRecoveredPair(string phase,int expected)
        {
            Interrupted(Seed(),phase);var room=new RoomStorage(directory);var loaded=room.Load(out var error);Assert.That(error,Is.Null);Assert.That(room.ReadOnly,Is.False);
            Assert.That(loaded.objects.Single(x=>x.id=="book").position.x,Is.EqualTo(expected));var memory=Open();Assert.That(memory.Error,Is.Null);Assert.That(Count(memory.Snapshot()),Is.EqualTo(expected));
        }
        [TestCase("room",1)][TestCase("committed",2)]
        public void MemoryThenRoomStartupLoadsTheRecoveredPair(string phase,int expected)
        {
            Interrupted(Seed(),phase);var memory=Open();Assert.That(memory.Error,Is.Null);Assert.That(Count(memory.Snapshot()),Is.EqualTo(expected));
            var loaded=new RoomStorage(directory).Load(out var error);Assert.That(error,Is.Null);Assert.That(loaded.objects[0].position.x,Is.EqualTo(expected));
        }
        [TestCase("room")][TestCase("committed")]
        public void LiveSavesAndRetentionCannotRecoverOrOverwriteAnUnresolvedPair(string phase)
        {
            var before=Seed();var room=new RoomStorage(directory);room.Load(out _);var memory=Open();Interrupted(before,phase);var evidence=Evidence();
            Assert.That(room.Save(Room(7),out var issue),Is.False);Assert.That(issue,Does.Contain("recovery"));Assert.That(room.ReadOnly,Is.True);
            var write=memory.Write(memory.Snapshot().Revision,Program,Values(7)).GetAwaiter().GetResult();Assert.That(write.Error,Is.Not.Null);Assert.That(memory.Error,Is.Not.Null);
            Assert.Throws<InvalidDataException>(()=>ProgramMemoryStore.ReadSaved(directory));
            room.RetainsMotion(new string('a',32),out bool roomUncertain);memory.Retains(new string('a',32),out bool memoryUncertain);
            Assert.That(roomUncertain&&memoryUncertain,Is.True);Assert.That(Evidence(),Is.EquivalentTo(evidence));
        }
        [Test]public void OrdinarySavesWaitForAcceptedMemoryWritesWithoutFailingOrOverwritingThem()
        {
            Seed();using var reached=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
            var memory=Open(stage=>{if(stage=="written"){reached.Set();if(!release.Wait(TimeSpan.FromSeconds(15)))throw new TimeoutException();}});
            var writing=memory.Write(memory.Snapshot().Revision,Program,Values(2));Task<bool> saving=null;Task<ProgramMemoryDocument> reading=null;
            try {
                Assert.That(reached.Wait(TimeSpan.FromSeconds(10)),Is.True);
                saving=Task.Run(()=>new RoomStorage(directory).Save(Room(3),out _));reading=Task.Run(()=>ProgramMemoryStore.ReadSaved(directory));
                Assert.That(saving.Wait(100),Is.False);Assert.That(reading.Wait(100),Is.False);
            }finally{release.Set();writing.GetAwaiter().GetResult();saving?.GetAwaiter().GetResult();reading?.GetAwaiter().GetResult();}
            Assert.That(writing.Result.Error,Is.Null);Assert.That(saving.Result,Is.True);Assert.That(Count(reading.Result),Is.EqualTo(2));Assert.That(memory.Error,Is.Null);
            Assert.That(new RoomStorage(directory).Load(out _).objects[0].position.x,Is.EqualTo(3));
        }
        [Test]public void ReadOnlyInspectionDoesNotCreateDirectoriesOrLockFiles()
        {
            Assert.That(ProgramMemoryStore.ReadSaved(directory).Programs,Is.Empty);Assert.That(Directory.Exists(directory),Is.False);
            Directory.CreateDirectory(directory);File.WriteAllBytes(PathFor(ProgramMemoryStore.FileName),Memory(4).Encode());var evidence=Evidence();
            Assert.That(Count(ProgramMemoryStore.ReadSaved(directory)),Is.EqualTo(4));Assert.That(Evidence(),Is.EquivalentTo(evidence));
        }
        [Test]public void ChangedRecoveryEvidenceLeavesBothLiveStoresUnavailableAndUnchanged()
        {
            Interrupted(Seed(),"room");File.WriteAllBytes(PathFor(ProgramMemoryStore.FileName),Memory(9).Encode());var evidence=Evidence();
            var room=new RoomStorage(directory);Assert.That(room.Load(out var error),Is.Null);Assert.That(error,Does.Contain("preserved"));Assert.That(room.ReadOnly,Is.True);
            var memory=Open();Assert.That(memory.Ready,Is.True);Assert.That(memory.Error,Does.Contain("preserved"));Assert.That(Evidence(),Is.EquivalentTo(evidence));
        }
        [Test]public void UnfinishedOldMemoryWritesRemainReadableButCannotBecomePairedSnapshots()
        {
            var before=Seed();File.WriteAllBytes(PathFor(ProgramMemoryStore.FileName+".pending."+new string('c',32)),Memory(9).Encode());
            Assert.That(Count(ProgramMemoryStore.ReadSaved(directory)),Is.EqualTo(1));
            Assert.Throws<InvalidDataException>(()=>RoomSnapshotTransaction.Publish(directory,before,Pair(2)));
        }
    }
}
