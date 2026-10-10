// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class PreparedRoomEditTests
    {
        static RoomJournal Room()=>new(new RoomDocument{version=2,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}}});
        static RoomObjectData Block()=>new(){id=Guid.NewGuid().ToString("N"),kind=RoomObjectKind.Block};
        static string State(RoomJournal journal)=>JsonUtility.ToJson(journal.Snapshot());
        [Test] public void PreparationCopiesInputsAndReturnedCandidatesWithoutMutatingSourceOrRevisions()
        {
            var journal=Room();var block=Block();var time=new RoomWorldTime{settings=new(){running=true,frames=new[]{new WorldLightKeyframe{second=1},new WorldLightKeyframe{second=2}}}};
            string before=State(journal);int revision=journal.ObjectRevision("book");
            Assert.IsTrue(journal.Prepare(new[]{block},Array.Empty<string>(),out var edit,out var error,worldTime:time),error);
            using(edit){string expected=JsonUtility.ToJson(edit.Snapshot());block.color=Color.red;time.settings.frames[0].second=300;
                var mutable=edit.Snapshot();mutable.objects.Single(x=>x.id==block.id).position=Vector3.one*100;mutable.worldTime.settings.running=false;
                Assert.AreEqual(before,State(journal));Assert.AreEqual(revision,journal.ObjectRevision("book"));Assert.IsFalse(journal.CanUndo);Assert.AreEqual(expected,JsonUtility.ToJson(edit.Snapshot()));
                Assert.IsTrue(edit.Accept(journal,out error),error);Assert.AreEqual(expected,State(journal));Assert.IsTrue(journal.CanUndo);Assert.IsFalse(edit.Accept(journal,out _));}
        }
        [Test] public void DisposalRejectsPublicationAndLeavesExistingRedoAvailable()
        {
            var journal=Room();journal.Apply(new[]{Block()},Array.Empty<string>(),out _);journal.Undo();string before=State(journal);
            Assert.IsTrue(journal.Prepare(new[]{Block()},Array.Empty<string>(),out var edit,out _));edit.Dispose();edit.Dispose();
            Assert.IsFalse(edit.Accept(journal,out _));Assert.AreEqual(before,State(journal));Assert.IsTrue(journal.CanRedo);Assert.IsFalse(journal.CanUndo);
        }
        [Test] public void LivePlacementInvalidatesAnOlderPreparedEdit()
        {
            var journal=Room();var block=Block();journal.Apply(new[]{block},Array.Empty<string>(),out _);block.color=Color.blue;
            Assert.IsTrue(journal.Prepare(new[]{block},Array.Empty<string>(),out var edit,out _));using(edit){journal.UpdatePlacement(block.id,Vector3.up,Quaternion.identity);string moved=State(journal);Assert.IsFalse(edit.Accept(journal,out _));Assert.AreEqual(moved,State(journal));}
        }
        [Test] public void PreparedEditBelongsToOneJournalEvenWhenAForkSharesTheWorld()
        {
            var journal=Room();var fork=journal.Fork();Assert.IsTrue(journal.Prepare(new[]{Block()},Array.Empty<string>(),out var edit,out _));
            using(edit){Assert.IsFalse(edit.Accept(fork,out _));Assert.IsTrue(edit.Current(journal));Assert.IsTrue(edit.Accept(journal,out _));Assert.AreEqual(2,fork.Snapshot().objects.Length);}
        }
        [Test] public void ViewpointMutationInvalidatesPreparationWithoutConsumingHistory()
        {
            var journal=Room();Assert.IsTrue(journal.Prepare(new[]{Block()},Array.Empty<string>(),out var edit,out _));
            using(edit){Assert.IsTrue(journal.UpdateViewpoint(new RoomViewpoint{active=true,position=Vector3.right},exact:true));string actual=State(journal);Assert.IsFalse(edit.Accept(journal,out _));Assert.AreEqual(actual,State(journal));Assert.IsFalse(journal.CanUndo);}
        }
        [Test] public void RuntimeTimeProgressInvalidatesPreparationWithoutChangingSettingsRevision()
        {
            var journal=Room();Assert.IsTrue(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out _,worldTime:new RoomWorldTime{settings=new(){running=true}}));
            int revision=journal.WorldTimeRevision;Assert.IsTrue(journal.Prepare(new[]{Block()},Array.Empty<string>(),out var edit,out _));
            using(edit){Assert.IsTrue(journal.AdvanceWorldTime(.2));Assert.AreEqual(revision,journal.WorldTimeRevision);string actual=State(journal);Assert.IsFalse(edit.Accept(journal,out _));Assert.AreEqual(actual,State(journal));}
        }
        [Test] public void UndoAndRedoPreviewDoNotMoveHistoryUntilTheExactCandidateIsAccepted()
        {
            var journal=Room();string empty=State(journal);var block=Block();journal.Apply(new[]{block},Array.Empty<string>(),out _);string populated=State(journal);
            Assert.IsTrue(journal.PrepareHistory(true,out var abandoned,out _));Assert.AreEqual(empty,JsonUtility.ToJson(abandoned.Snapshot()));Assert.AreEqual(populated,State(journal));abandoned.Dispose();Assert.IsTrue(journal.CanUndo);Assert.IsFalse(journal.CanRedo);
            Assert.IsTrue(journal.PrepareHistory(true,out var undo,out _));using(undo){Assert.IsTrue(undo.Accept(journal,out _));Assert.IsFalse(undo.Accept(journal,out _));}Assert.AreEqual(empty,State(journal));Assert.IsTrue(journal.CanRedo);
            Assert.IsTrue(journal.PrepareHistory(false,out var redo,out _));using(redo){var mutable=redo.Snapshot();mutable.objects.Single(x=>x.id==block.id).color=Color.red;Assert.AreEqual(empty,State(journal));Assert.IsTrue(journal.CanRedo);Assert.IsTrue(redo.Accept(journal,out _));}Assert.AreEqual(populated,State(journal));
        }
        [Test] public void LaterHistoryChangesInvalidateAFormerUndoPreview()
        {
            var journal=Room();journal.Apply(new[]{Block()},Array.Empty<string>(),out _);Assert.IsTrue(journal.PrepareHistory(true,out var undo,out _));
            using(undo){journal.Apply(new[]{Block()},Array.Empty<string>(),out _);string actual=State(journal);Assert.IsFalse(undo.Accept(journal,out _));Assert.AreEqual(actual,State(journal));Assert.IsTrue(journal.Undo());Assert.AreEqual(3,journal.Snapshot().objects.Length);}
        }
        [Test] public void AcceptedNoOpPreservesRedoButCannotBeAcceptedAgain()
        {
            var journal=Room();journal.Apply(new[]{Block()},Array.Empty<string>(),out _);journal.Undo();Assert.IsTrue(journal.Prepare(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out var edit,out _));
            using(edit){Assert.IsTrue(edit.Accept(journal,out _));Assert.IsFalse(edit.Accept(journal,out _));}Assert.IsTrue(journal.CanRedo);Assert.IsFalse(journal.CanUndo);
        }
        [Test] public void InvalidOrDuplicateInputsNeverCreateAPreparedCandidate()
        {
            var journal=Room();var block=Block();string before=State(journal);
            Assert.IsFalse(journal.Prepare(new[]{block,block.Copy()},Array.Empty<string>(),out var duplicate,out _));Assert.IsNull(duplicate);
            Assert.IsFalse(journal.Prepare(new RoomObjectData[]{null},Array.Empty<string>(),out var missing,out _));Assert.IsNull(missing);
            Assert.IsFalse(journal.Prepare(Array.Empty<RoomObjectData>(),new[]{"book"},out var invalid,out _));Assert.IsNull(invalid);Assert.AreEqual(before,State(journal));Assert.IsFalse(journal.CanUndo);
        }
    }
}
