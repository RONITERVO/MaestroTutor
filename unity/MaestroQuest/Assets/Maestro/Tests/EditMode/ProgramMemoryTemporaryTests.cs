// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class ProgramMemoryTemporaryTests
    {
        const string Program="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",Cell="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        string directory;ProgramMemoryStore memory;WorkspaceWriteGate gate;
        static Dictionary<string,ProgramMemoryDocument.Cell> Values(double n)=>new(){{Cell,new("count",new ProgramValue(n))}};
        static RoomDocument Room(float x)=>new(){version=2,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book,position=new Vector3(x,1,0)},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}}};
        double Count(ProgramMemoryDocument doc)=>doc.Programs[Program][Cell].Value.Number;
        void Write(double n){var result=memory.Write(memory.Snapshot().Revision,Program,Values(n)).GetAwaiter().GetResult();Assert.That(result.Error,Is.Null);}
        [SetUp]public void Setup(){directory=Path.Combine(Path.GetTempPath(),"maestro-temp-memory-"+Guid.NewGuid().ToString("N"));gate=new();memory=new(directory,gate);memory.Initialization.GetAwaiter().GetResult();}
        [TearDown]public void Cleanup(){memory.Drain().GetAwaiter().GetResult();if(Directory.Exists(directory))Directory.Delete(directory,true);}
        [Test]public void ForkResetAndDiscardAreMemoryOnlyAndRejectInvalidBoundaries()
        {
            Write(1);var bytes=File.ReadAllBytes(Path.Combine(directory,ProgramMemoryStore.FileName));memory.BeginTemporary();Assert.Throws<InvalidOperationException>(()=>memory.BeginTemporary());Write(2);
            Assert.That(Count(memory.Snapshot()),Is.EqualTo(2));Assert.That(Count(memory.SavedSnapshot()),Is.EqualTo(1));Assert.That(File.ReadAllBytes(Path.Combine(directory,ProgramMemoryStore.FileName)),Is.EqualTo(bytes));
            var result=memory.Reset(memory.Snapshot().Revision,Program).GetAwaiter().GetResult();Assert.That(result.Error,Is.Null);Assert.That(memory.Snapshot().Programs,Is.Empty);memory.EndTemporary();Assert.That(Count(memory.Snapshot()),Is.EqualTo(1));Assert.Throws<InvalidOperationException>(()=>memory.EndTemporary());
        }
        [Test]public void EmptyForkDoesNotCreateAWorkspaceUntilKept()
        {
            memory.BeginTemporary();Write(2);Assert.That(Directory.Exists(directory),Is.False);memory.EndTemporary();Assert.That(memory.Snapshot().Programs,Is.Empty);Assert.That(Directory.Exists(directory),Is.False);
        }
        [Test]public void PairedKeepCapturesMemoryAndRetirementWaitsForPublication()
        {
            Write(1);memory.BeginTemporary();Write(2);using var save=new TemporaryMemorySave(memory,null,Room(2),gate);Write(3);
            var drain=gate.Retire();Assert.That(drain.IsCompleted,Is.False);Assert.That(save.Run(),Is.Null);save.Confirm();Assert.That(Count(memory.Snapshot()),Is.EqualTo(3));Assert.That(Count(memory.SavedSnapshot()),Is.EqualTo(2));Assert.That(Count(ProgramMemoryStore.ReadSaved(directory)),Is.EqualTo(2));save.Dispose();Assert.That(drain.IsCompleted,Is.True);
        }
        [TestCase("before-journal",false)][TestCase("prepared",false)][TestCase("room",false)][TestCase("memory",false)][TestCase("committed",true)][TestCase("recovered-room",true)][TestCase("recovered-memory",true)][TestCase("room-backup",true)][TestCase("memory-backup",true)]
        public void InterruptedKeepResolvesToOnePairAndNeverRewindsLaterForkEdits(string phase,bool committed)
        {
            Write(1);Assert.That(new RoomStorage(directory).Save(Room(1),out _),Is.True);var before=RoomSnapshotTransaction.Capture(directory);memory.BeginTemporary();Write(2);
            using var save=new TemporaryMemorySave(memory,before,Room(2),gate);Write(3);var error=save.Run(stage=>{if(stage==phase)throw new IOException("Injected interruption");});
            Assert.That(save.Uncertain,Is.False);Assert.That(error==null,Is.EqualTo(committed));if(committed)save.Confirm();
            Assert.That(Count(memory.Snapshot()),Is.EqualTo(3));memory.EndTemporary();Assert.That(Count(memory.Snapshot()),Is.EqualTo(committed?2:1));Assert.That(Count(ProgramMemoryStore.ReadSaved(directory)),Is.EqualTo(committed?2:1));
            var room=new RoomStorage(directory).Load(out var issue);Assert.That(issue,Is.Null);Assert.That(room.objects[0].position.x,Is.EqualTo(committed?2:1));Assert.That(File.Exists(Path.Combine(directory,RoomSnapshotTransaction.FileName)),Is.False);
        }
        [Test]public void OutsideChangesDuringPublicationArePreservedAndPreventAdoption()
        {
            Write(1);new RoomStorage(directory).Save(Room(1),out _);var before=RoomSnapshotTransaction.Capture(directory);memory.BeginTemporary();Write(2);using var save=new TemporaryMemorySave(memory,before,Room(2),gate);
            var other=ProgramMemoryDocument.Empty().WithValues(Program,Values(9)).Encode();string path=Path.Combine(directory,ProgramMemoryStore.FileName);
            Assert.That(save.Run(stage=>{if(stage=="room"){File.WriteAllBytes(path,other);throw new IOException("Outside change");}}),Is.Not.Null);Assert.That(save.Uncertain,Is.True);Assert.Throws<InvalidOperationException>(()=>save.Confirm());Assert.That(File.ReadAllBytes(path),Is.EqualTo(other));Assert.That(File.Exists(Path.Combine(directory,RoomSnapshotTransaction.FileName)),Is.True);
        }
        [Test]public void AFailedKeepCanRetryWithoutReplacingTheLiveFork()
        {
            Write(1);new RoomStorage(directory).Save(Room(1),out _);var before=RoomSnapshotTransaction.Capture(directory);memory.BeginTemporary();Write(2);
            using(var failed=new TemporaryMemorySave(memory,before,Room(2),gate))Assert.That(failed.Run(stage=>{if(stage=="memory")throw new IOException("Failure");}),Is.Not.Null);
            Write(3);using(var retry=new TemporaryMemorySave(memory,before,Room(3),gate)){Assert.That(retry.Run(),Is.Null);retry.Confirm();}memory.EndTemporary();Assert.That(Count(memory.Snapshot()),Is.EqualTo(3));
        }
        [Test]public void CapturedValuesRemainRetainedAfterTheLiveForkForgetsThem()
        {
            string motion=new string('d',64);memory.BeginTemporary();var value=new Dictionary<string,ProgramMemoryDocument.Cell>{{Cell,new("motion",new ProgramValue(motion))}};memory.Write(memory.Snapshot().Revision,Program,value).GetAwaiter().GetResult();
            using(var pin=memory.RetainSnapshot(memory.CaptureTemporary())){memory.Reset(memory.Snapshot().Revision,Program).GetAwaiter().GetResult();Assert.That(memory.Retains(motion,out _),Is.True);}
            Assert.That(memory.Retains(motion,out var uncertain),Is.False);Assert.That(uncertain,Is.False);
        }
        [Test]public void TemporaryEditsRespectWorkspaceFreeze()
        {
            memory.BeginTemporary();using var hold=gate.TryFreeze(out var issue);Assert.That(issue,Is.Null);Assert.Throws<InvalidOperationException>(()=>memory.Write(memory.Snapshot().Revision,Program,Values(2)));Assert.Throws<InvalidOperationException>(()=>memory.EndTemporary());
        }
        [Test]public void OutsideSavedMemoryChangeBeforeBaselineDoesNotBecomeTheDiscardBase()
        {
            Write(1);memory.BeginTemporary();var outside=ProgramMemoryDocument.Empty().WithValues(Program,Values(8)).Encode();File.WriteAllBytes(Path.Combine(directory,ProgramMemoryStore.FileName),outside);
            using var save=new TemporaryMemorySave(memory,null,Room(1),gate);Assert.That(save.Run(),Is.Not.Null);Assert.That(save.Uncertain,Is.True);Assert.That(File.ReadAllBytes(Path.Combine(directory,ProgramMemoryStore.FileName)),Is.EqualTo(outside));Assert.That(Count(memory.SavedSnapshot()),Is.EqualTo(1));
        }
        [Test]public void UnrelatedPendingRoomFileAllowsExplicitRetryWhenTheSavedPairIsUnchanged()
        {
            Write(1);new RoomStorage(directory).Save(Room(1),out _);var before=RoomSnapshotTransaction.Capture(directory);memory.BeginTemporary();Write(2);string pending=Path.Combine(directory,"room.v2.json.pending");Directory.CreateDirectory(pending);
            using(var failed=new TemporaryMemorySave(memory,before,Room(2),gate)){Assert.That(failed.Run(),Is.Not.Null);Assert.That(failed.Uncertain,Is.False);}Directory.Delete(pending);
            using var retry=new TemporaryMemorySave(memory,before,Room(2),gate);Assert.That(retry.Run(),Is.Null);retry.Confirm();memory.EndTemporary();Assert.That(Count(memory.Snapshot()),Is.EqualTo(2));
        }
    }
}
