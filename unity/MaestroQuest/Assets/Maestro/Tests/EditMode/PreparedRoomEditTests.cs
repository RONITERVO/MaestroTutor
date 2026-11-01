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
        [Test] public void ContentEditsPreserveMovingPlacementThroughAcceptanceUndoAndRedo()
        {
            var journal=Room();var block=Block();Assert.IsTrue(journal.Apply(new[]{block},Array.Empty<string>(),out var error),error);
            var requested=journal.Read(block.id);requested.color=Color.red;requested.position=Vector3.one*10;
            Assert.IsTrue(journal.Prepare(new[]{requested},Array.Empty<string>(),out var edit,out error,scope:RoomEditScope.Content),error);
            using(edit){
                Assert.AreEqual(Vector3.zero,edit.ReadObject(block.id).position);Assert.IsTrue(edit.PreservesPlacement);
                Assert.IsTrue(journal.UpdatePlacement(block.id,Vector3.right,Quaternion.Euler(0,30,0),runtime:true));
                Assert.IsTrue(edit.Accept(journal,out error,allowRuntimeProgress:true),error);
            }
            Assert.AreEqual(Vector3.right,journal.Read(block.id).position);Assert.AreEqual(Color.red,journal.Read(block.id).color);
            Assert.IsTrue(journal.UpdatePlacement(block.id,Vector3.up,Quaternion.identity,runtime:true));
            Assert.IsTrue(journal.Undo());Assert.AreEqual(block.color,journal.Read(block.id).color);Assert.AreEqual(Vector3.up,journal.Read(block.id).position);
            Assert.IsTrue(journal.Redo());Assert.AreEqual(Color.red,journal.Read(block.id).color);Assert.AreEqual(Vector3.up,journal.Read(block.id).position);
        }
        [Test] public void ContentScopeRefusesIdentityMembershipMetadataAndConcurrentAuthoredChanges()
        {
            var journal=Room();var block=Block();Assert.IsTrue(journal.Apply(new[]{block},Array.Empty<string>(),out var error),error);
            Assert.IsFalse(journal.Prepare(new[]{Block()},Array.Empty<string>(),out _,out _,scope:RoomEditScope.Content));
            Assert.IsFalse(journal.Prepare(Array.Empty<RoomObjectData>(),new[]{block.id},out _,out _,scope:RoomEditScope.Content));
            var changed=block.Copy();changed.kind=RoomObjectKind.Ball;
            Assert.IsFalse(journal.Prepare(new[]{changed},Array.Empty<string>(),out _,out _,scope:RoomEditScope.Content));
            Assert.IsFalse(journal.Prepare(new[]{block},Array.Empty<string>(),out _,out _,scope:RoomEditScope.Content,lighting:new RoomLighting()));
            changed=block.Copy();changed.color=Color.red;
            Assert.IsTrue(journal.Prepare(new[]{changed},Array.Empty<string>(),out var edit,out error,scope:RoomEditScope.Content),error);
            using(edit){var newer=block.Copy();newer.color=Color.blue;Assert.IsTrue(journal.Apply(new[]{newer},Array.Empty<string>(),out error),error);
                Assert.IsFalse(edit.Accept(journal,out _,allowRuntimeProgress:true));Assert.AreEqual(Color.blue,journal.Read(block.id).color);}
        }
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
        [Test] public void TargetedReadsKeepDeepCopiesAndFollowEditUndoRedoCandidates()
        {
            var journal=Room();
            var drawing=new RoomObjectData{id=Guid.NewGuid().ToString("N"),kind=RoomObjectKind.Drawing,points=new[]{Vector3.zero,Vector3.right*.1f}};
            var removed=Block();var added=Block();
            Assert.That(journal.Apply(new[]{drawing,removed},Array.Empty<string>(),out var error),Is.True,error);
            drawing.points[1]=Vector3.up*.2f;
            Assert.That(journal.Prepare(new[]{drawing,added},new[]{removed.id},out var edit,out error),Is.True,error);
            using(edit) {
                var read=edit.ReadObject(drawing.id);
                Assert.That(read.points[1],Is.EqualTo(Vector3.up*.2f));
                read.points[1]=Vector3.one*20;
                drawing.points[1]=Vector3.one*10;
                Assert.That(edit.ReadObject(drawing.id).points[1],Is.EqualTo(Vector3.up*.2f));
                Assert.That(edit.Snapshot().objects.Single(x=>x.id==read.id).points[1],Is.EqualTo(Vector3.up*.2f));
                Assert.That(journal.Read(read.id).points[1],Is.EqualTo(Vector3.right*.1f));
                Assert.That(edit.ReadObject(removed.id),Is.Null);
                Assert.That(edit.ReadObject(added.id).id,Is.EqualTo(added.id));
                Assert.That(edit.ReadObject("book").kind,Is.EqualTo(RoomObjectKind.Book));
                Assert.That(edit.ReadObject(null),Is.Null);
                Assert.That(edit.ReadObject(Guid.NewGuid().ToString("N")),Is.Null);
                Assert.That(edit.Accept(journal,out error),Is.True,error);
            }
            Assert.That(journal.PrepareHistory(true,out var undo,out error),Is.True,error);
            using(undo) {
                Assert.That(undo.ReadObject(drawing.id).points[1],Is.EqualTo(Vector3.right*.1f));
                Assert.That(undo.ReadObject(removed.id).id,Is.EqualTo(removed.id));
                Assert.That(undo.ReadObject(added.id),Is.Null);
                undo.ReadObject(drawing.id).points[1]=Vector3.one*5;
                Assert.That(undo.Accept(journal,out error),Is.True,error);
            }
            Assert.That(journal.Read(drawing.id).points[1],Is.EqualTo(Vector3.right*.1f));
            Assert.That(journal.PrepareHistory(false,out var redo,out error),Is.True,error);
            using(redo) {
                Assert.That(redo.ReadObject(drawing.id).points[1],Is.EqualTo(Vector3.up*.2f));
                Assert.That(redo.ReadObject(removed.id),Is.Null);
                Assert.That(redo.ReadObject(added.id).id,Is.EqualTo(added.id));
                Assert.That(redo.Accept(journal,out error),Is.True,error);
            }
            Assert.That(journal.Read(drawing.id).points[1],Is.EqualTo(Vector3.up*.2f));
        }
        [Test] public void BackgroundCandidatePreservesNewRuntimeTimeAndViewpointWithoutChangingItsCheckpoint()
        {
            var journal=Room();Assert.IsTrue(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out _,worldTime:new RoomWorldTime{settings=new(){running=true}}));
            var added=Block();Assert.IsTrue(journal.Prepare(new[]{added},Array.Empty<string>(),out var edit,out _));
            using(edit){
                string checkpoint=JsonUtility.ToJson(edit.Snapshot());
                journal.AdvanceWorldTime(.2);journal.UpdateViewpoint(new RoomViewpoint{active=true,position=Vector3.left},exact:true);
                double current=journal.WorldSecond;Assert.IsTrue(edit.RuntimeProgressChanged);
                Assert.IsFalse(edit.Current(journal));Assert.IsTrue(edit.Current(journal,allowRuntimeProgress:true));
                Assert.AreEqual(checkpoint,JsonUtility.ToJson(edit.Snapshot()));
                Assert.IsTrue(edit.Accept(journal,out var error,allowRuntimeProgress:true),error);
                Assert.AreEqual(current,journal.WorldSecond);Assert.AreEqual(Vector3.left,journal.Viewpoint.position);
                Assert.IsTrue(journal.Undo());Assert.IsNull(journal.Read(added.id));Assert.AreEqual(current,journal.WorldSecond);
            }
        }
        [Test] public void BackgroundToleranceNeverAllowsAuthoredChangesOrClockSeeks()
        {
            var journal=Room();var block=Block();journal.Apply(new[]{block},Array.Empty<string>(),out _);
            Assert.IsTrue(journal.Prepare(new[]{Block()},Array.Empty<string>(),out var first,out _));
            using(first){journal.UpdatePlacement(block.id,Vector3.up,Quaternion.identity);Assert.IsFalse(first.Accept(journal,out _,allowRuntimeProgress:true));}
            journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out _,worldTime:new RoomWorldTime{settings=new(){running=true}});
            Assert.IsTrue(journal.Prepare(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out var seek,out _,worldTime:new RoomWorldTime{second=10}));
            using(seek){journal.AdvanceWorldTime(.2);Assert.IsFalse(seek.Accept(journal,out _,allowRuntimeProgress:true));}
            Assert.IsTrue(journal.Prepare(new[]{Block()},Array.Empty<string>(),out var other,out _));
            using(other){journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out _,worldTime:new RoomWorldTime{second=20});Assert.IsFalse(other.Accept(journal,out _,allowRuntimeProgress:true));}
        }
        [Test] public void BackgroundCandidatePreservesUnrelatedRuntimePlacementAndItsUnsavedCheckpoint()
        {
            var journal=Room();var block=Block();journal.Apply(new[]{block},Array.Empty<string>(),out _);
            block.color=Color.blue;Assert.IsTrue(journal.Prepare(new[]{block},Array.Empty<string>(),out var edit,out _));
            using(edit){
                string checkpoint=JsonUtility.ToJson(edit.Snapshot());
                Assert.IsTrue(journal.UpdatePlacement("maestro",Vector3.right,Quaternion.identity,runtime:true));
                Assert.IsTrue(journal.UpdatePlacement("maestro",Vector3.right*2,Quaternion.identity,runtime:true));
                Assert.IsTrue(edit.RuntimeProgressChanged);Assert.IsFalse(edit.Current(journal));Assert.IsTrue(edit.Current(journal,allowRuntimeProgress:true));
                Assert.AreEqual(checkpoint,JsonUtility.ToJson(edit.Snapshot()));
                Assert.IsTrue(edit.Accept(journal,out var error,allowRuntimeProgress:true),error);
                Assert.AreEqual(Vector3.right*2,journal.Read("maestro").position);Assert.AreEqual(Color.blue,journal.Read(block.id).color);
                journal.Undo();Assert.AreEqual(Vector3.right*2,journal.Read("maestro").position);Assert.AreEqual(Color.white,journal.Read(block.id).color);
            }
        }
        [Test] public void RuntimePlacementToleranceRejectsTargetChangesAndAuthoredChangesAndOtherForks()
        {
            foreach(string kind in new[]{"target","authored","fork"}){
                var journal=Room();var block=Block();journal.Apply(new[]{block},Array.Empty<string>(),out _);block.color=Color.blue;
                Assert.IsTrue(journal.Prepare(new[]{block},Array.Empty<string>(),out var edit,out _));
                using(edit){
                    journal.UpdatePlacement("maestro",Vector3.right,Quaternion.identity,runtime:true);
                    if(kind=="target")journal.UpdatePlacement(block.id,Vector3.up,Quaternion.identity,runtime:true);
                    if(kind=="authored"){var changed=journal.Read("maestro");changed.color=Color.red;journal.Apply(new[]{changed},Array.Empty<string>(),out _);}
                    if(kind=="fork")journal.Fork().UpdatePlacement("book",Vector3.left,Quaternion.identity,runtime:true);
                    string before=State(journal);Assert.IsFalse(edit.Accept(journal,out _,allowRuntimeProgress:true),kind);Assert.AreEqual(before,State(journal));
                }
            }
        }
        [TestCase("create")][TestCase("update")][TestCase("same")][TestCase("remove")]
        public void AcceptedEnvironmentRevisionBelongsToThisEditAfterLaterMutations(string action)
        {
            var journal=Room();string id=Guid.NewGuid().ToString("N");var profile=new RoomEnvironmentProfile{id=id,name="Virtual actors",realCollisions=false};
            if(action!="create")Assert.IsTrue(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out _,environmentEdits:new(){Replacements=new[]{profile}}));
            if(action=="update")profile.realCollisions=true;
            var delta=action=="remove"?new EnvironmentProfileEdits{Removals=new[]{id}}:new EnvironmentProfileEdits{Replacements=new[]{profile}};
            Assert.IsTrue(journal.Prepare(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out var edit,out var error,environmentEdits:delta),error);
            using(edit){
                Assert.AreEqual(0,edit.AcceptedEnvironmentRevision(id));Assert.IsTrue(edit.Accept(journal,out error),error);int accepted=journal.EnvironmentRevision(id);
                Assert.AreEqual(accepted,edit.AcceptedEnvironmentRevision(id));Assert.AreEqual(action=="remove",accepted==0);
                profile.name="Later human edit";Assert.IsTrue(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out error,environmentEdits:new(){Replacements=new[]{profile}}),error);
                Assert.AreNotEqual(accepted,journal.EnvironmentRevision(id));Assert.AreEqual(accepted,edit.AcceptedEnvironmentRevision(id));
            }
        }
        [TestCase("create")][TestCase("update")][TestCase("same")][TestCase("remove")]
        public void AcceptedAudioRevisionBelongsToThisEditAfterLaterMutations(string action)
        {
            var journal=Room();string id=Guid.NewGuid().ToString("N");var profile=new RoomAudioDefinition{id=id,name="Reusable sound"};
            if(action!="create")Assert.IsTrue(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out _,audioEdits:new(){Replacements=new[]{profile}}));
            if(action=="update")profile.frequency=880;
            var delta=action=="remove"?new AudioDefinitionEdits{Removals=new[]{id}}:new AudioDefinitionEdits{Replacements=new[]{profile}};
            Assert.IsTrue(journal.Prepare(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out var edit,out var error,audioEdits:delta),error);
            using(edit){
                Assert.AreEqual(0,edit.AcceptedAudioRevision(id));Assert.IsTrue(edit.Accept(journal,out error),error);int accepted=journal.AudioRevision(id);
                Assert.AreEqual(accepted,edit.AcceptedAudioRevision(id));Assert.AreEqual(action=="remove",accepted==0);
                profile.name="Later human edit";Assert.IsTrue(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out error,audioEdits:new(){Replacements=new[]{profile}}),error);
                Assert.AreNotEqual(accepted,journal.AudioRevision(id));Assert.AreEqual(accepted,edit.AcceptedAudioRevision(id));
            }
        }
        [TestCase("create")][TestCase("update")][TestCase("same")][TestCase("remove")]
        public void AcceptedAppearanceRevisionBelongsToThisEditAfterLaterMutations(string action)
        {
            var journal=Room();string id=Guid.NewGuid().ToString("N");var profile=new RoomAppearance{id=id,name="Reusable appearance"};
            if(action!="create")Assert.IsTrue(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out _,appearanceEdits:new(){Replacements=new[]{profile}}));
            if(action=="update"){profile.style.opacity=.4f;profile.style.renderMode="blend";}
            var delta=action=="remove"?new AppearanceEdits{Removals=new[]{id}}:new AppearanceEdits{Replacements=new[]{profile}};
            Assert.IsTrue(journal.Prepare(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out var edit,out var error,appearanceEdits:delta),error);
            using(edit){
                Assert.AreEqual(0,edit.AcceptedAppearanceRevision(id));Assert.IsTrue(edit.Accept(journal,out error),error);int accepted=journal.AppearanceRevision(id);
                Assert.AreEqual(accepted,edit.AcceptedAppearanceRevision(id));Assert.AreEqual(action=="remove",accepted==0);
                profile.name="Later human edit";Assert.IsTrue(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out error,appearanceEdits:new(){Replacements=new[]{profile}}),error);
                Assert.AreNotEqual(accepted,journal.AppearanceRevision(id));Assert.AreEqual(accepted,edit.AcceptedAppearanceRevision(id));
            }
        }
        [TestCase("create")][TestCase("update")][TestCase("same")][TestCase("remove")]
        public void AcceptedRegionRevisionBelongsToThisEditAfterLaterMutations(string action)
        {
            var journal=new RoomJournal(new RoomDocument{version=RoomDocument.CurrentVersion,objects=Room().Snapshot().objects});
            string id=Guid.NewGuid().ToString("N");var region=new RoomRegion{id=id,name="Garden"};
            if(action!="create")Assert.IsTrue(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out _,regionEdits:new(){Replacements=new[]{region}}));
            if(action=="update")region.name="Café garden";
            var delta=action=="remove"?new RegionEdits{Removals=new[]{id}}:new RegionEdits{Replacements=new[]{region}};
            Assert.IsTrue(journal.Prepare(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out var edit,out var error,regionEdits:delta),error);
            using(edit){
                Assert.AreEqual(0,edit.AcceptedRegionRevision(id));Assert.IsTrue(edit.Accept(journal,out error),error);int accepted=journal.RegionRevision(id);
                Assert.AreEqual(accepted,edit.AcceptedRegionRevision(id));Assert.AreEqual(action=="remove",accepted==0);
                region.name="Later human edit";Assert.IsTrue(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out error,regionEdits:new(){Replacements=new[]{region}}),error);
                Assert.AreNotEqual(accepted,journal.RegionRevision(id));Assert.AreEqual(accepted,edit.AcceptedRegionRevision(id));
            }
        }
        [TestCase("create")][TestCase("update")][TestCase("same")][TestCase("remove")]
        public void AcceptedStructureRevisionBelongsToThisEditAfterLaterMutations(string action)
        {
            var journal=Room();var block=Block();Assert.IsTrue(journal.Apply(new[]{block},Array.Empty<string>(),out _));
            string id=Guid.NewGuid().ToString("N");var structure=new RoomStructure{id=id,name="Castle",slots=new[]{new StructureSlot{slot="first",placement=new ObjectPlacement{target=block.id}}}};
            if(action!="create")Assert.IsTrue(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out _,structureEdits:new(){Replacements=new[]{structure}}));
            if(action=="update")structure.name="Garden castle";
            var delta=action=="remove"?new StructureEdits{Removals=new[]{id}}:new StructureEdits{Replacements=new[]{structure}};
            Assert.IsTrue(journal.Prepare(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out var edit,out var error,structureEdits:delta),error);
            using(edit){
                Assert.AreEqual(0,edit.AcceptedStructureRevision(id));Assert.IsTrue(edit.Accept(journal,out error),error);int accepted=journal.StructureRevision(id);
                Assert.AreEqual(accepted,edit.AcceptedStructureRevision(id));Assert.AreEqual(action=="remove",accepted==0);
                structure.name="Later human edit";Assert.IsTrue(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out error,structureEdits:new(){Replacements=new[]{structure}}),error);
                Assert.AreNotEqual(accepted,journal.StructureRevision(id));Assert.AreEqual(accepted,edit.AcceptedStructureRevision(id));
            }
        }
        [Test] public void AcceptedMembershipRevisionCapturesPublicationWithoutRewindingMovingMembers()
        {
            var block=Block();var journal=new RoomJournal(new RoomDocument{version=RoomDocument.CurrentVersion,objects=Room().Snapshot().objects.Concat(new[]{block}).ToArray()});
            var region=new RoomRegion{id=Guid.NewGuid().ToString("N"),name="Garden",members=new[]{block.id}};
            Assert.IsTrue(journal.Prepare(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out var edit,out var error,regionEdits:new(){Replacements=new[]{region}}),error);
            using(edit){
                Assert.IsTrue(journal.UpdatePlacement(block.id,Vector3.right*2,Quaternion.identity,runtime:true));
                Assert.IsTrue(edit.Current(journal,allowRuntimeProgress:true));Assert.IsTrue(edit.Accept(journal,out error,allowRuntimeProgress:true),error);
                int accepted=journal.ObjectRevision(block.id);Assert.AreEqual(accepted,edit.AcceptedObjectRevision(block.id));Assert.AreEqual(Vector3.right*2,journal.Read(block.id).position);
                Assert.IsTrue(journal.UpdatePlacement(block.id,Vector3.right*3,Quaternion.identity,runtime:true));Assert.AreNotEqual(accepted,journal.ObjectRevision(block.id));Assert.AreEqual(accepted,edit.AcceptedObjectRevision(block.id));
                journal.Undo();Assert.AreEqual(Vector3.right*3,journal.Read(block.id).position);Assert.IsNull(journal.ReadRegion(region.id));
            }
        }

        [Test] public void SelectionPublicationReservationBlocksAuthoredEditsButPreservesRuntimePlacement()
        {
            var journal=Room();var block=Block();Assert.IsTrue(journal.Apply(new[]{block},Array.Empty<string>(),out var error),error);
            var previous=journal.Read("book");previous.position=Vector3.up;
            Assert.IsTrue(journal.Prepare(new[]{previous},Array.Empty<string>(),out var earlier,out error),error);
            var next=journal.Read("maestro");next.modelHash=new string('a',64);
            Assert.IsTrue(journal.Prepare(new[]{next},Array.Empty<string>(),out var selected,out error,scope:RoomEditScope.Content),error);
            using(earlier)using(selected){
                Assert.IsTrue(selected.ReservePublication(out error),error);
                Assert.IsFalse(earlier.Accept(journal,out _,allowRuntimeProgress:true));
                Assert.IsFalse(journal.Prepare(new[]{block},Array.Empty<string>(),out _,out error));
                Assert.That(error,Does.Contain("publishing"));Assert.IsFalse(journal.Undo());
                Assert.IsFalse(journal.ApplySnapshot(journal.Snapshot(),out _));
                Assert.IsFalse(journal.UpdatePlacement(block.id,Vector3.right,Quaternion.identity));
                Assert.IsTrue(journal.UpdatePlacement(block.id,Vector3.up,Quaternion.identity,runtime:true));
                Assert.IsTrue(selected.Accept(journal,out error,allowRuntimeProgress:true),error);
            }
            Assert.IsFalse(journal.PublicationReserved);
            Assert.AreEqual(next.modelHash,journal.Read("maestro").modelHash);Assert.AreEqual(Vector3.up,journal.Read(block.id).position);
            Assert.IsTrue(journal.Undo());Assert.IsNull(journal.Read("maestro").modelHash);Assert.AreEqual(Vector3.up,journal.Read(block.id).position);
        }
        [Test] public void CancelledSelectionReservationReleasesWithoutChangingStateOrHistory()
        {
            var journal=Room();string before=State(journal);var next=journal.Read("maestro");next.modelHash=new string('b',64);
            Assert.IsTrue(journal.Prepare(new[]{next},Array.Empty<string>(),out var selected,out var error,scope:RoomEditScope.Content),error);
            Assert.IsTrue(selected.ReservePublication(out error),error);
            selected.Dispose();selected.Dispose();
            Assert.AreEqual(before,State(journal));Assert.IsFalse(journal.CanUndo);Assert.IsFalse(journal.PublicationReserved);
            Assert.IsTrue(journal.Apply(new[]{Block()},Array.Empty<string>(),out error),error);
        }
        [Test] public void SelectionReservationRefusesStaleOrAlreadyReservedCandidates()
        {
            var journal=Room();var next=journal.Read("maestro");next.modelHash=new string('b',64);
            Assert.IsTrue(journal.Prepare(new[]{next},Array.Empty<string>(),out var first,out var error,scope:RoomEditScope.Content),error);
            Assert.IsTrue(journal.Prepare(new[]{next},Array.Empty<string>(),out var second,out error,scope:RoomEditScope.Content),error);
            using(first)using(second){Assert.IsTrue(first.ReservePublication(out error),error);Assert.IsFalse(second.ReservePublication(out _));}
            Assert.IsTrue(journal.Prepare(new[]{next},Array.Empty<string>(),out var stale,out error,scope:RoomEditScope.Content),error);
            using(stale){Assert.IsTrue(journal.Apply(new[]{Block()},Array.Empty<string>(),out error),error);Assert.IsFalse(stale.ReservePublication(out _));}
            Assert.IsFalse(journal.PublicationReserved);
        }
    }
}
