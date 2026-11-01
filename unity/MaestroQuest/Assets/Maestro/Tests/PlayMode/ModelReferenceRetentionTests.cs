// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomEditorTests
    {
        const string RetainedModel="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",CalibrationModel="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        [UnityTest]public IEnumerator DormantModelRootsRetainTheSavedTemporaryBaseWithoutNativeObjects(){
            var saved=StartupSeparateAreas();var model=saved.saved.objects.Single(x=>x.id==saved.front);model.kind=RoomObjectKind.ImportedModel;model.modelHash=RetainedModel;
            yield return ReloadForStartup(saved.saved,observe:false);yield return StartupFinished();
            Assert.That(editor.NativeEntityDormant(saved.front),Is.True);Assert.That(editor.Find(saved.front),Is.Null);Assert.That(editor.UsesModel(RetainedModel),Is.True);Assert.That(editor.ModelReferencesUncertain,Is.False);
            Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);yield return DormantRestorationFixture.Save(editor);Assert.That(editor.DeleteObject(saved.front,out error),Is.True,error);
            Assert.That(editor.Read(saved.front),Is.Null);Assert.That(editor.UsesModel(RetainedModel),Is.True,"The saved temporary base remains a root");Assert.That(editor.HistoricalModel(RetainedModel),Is.True);
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.NativeEntityDormant(saved.front),Is.True);Assert.That(editor.Find(saved.front),Is.Null);
            Assert.That(editor.UsesModel(RetainedModel),Is.True);Assert.That(editor.HistoricalModel(RetainedModel),Is.False,"Discard removed the temporary deletion history");
        }
        [UnityTest]public IEnumerator DispatchedTemporarySnapshotRetainsModelPreferencesRemovedFromTheLiveFork(){
            var saved=StartupSeparateAreas();var model=saved.saved.objects.Single(x=>x.id==saved.front);model.kind=RoomObjectKind.ImportedModel;model.modelHash=RetainedModel;
            yield return ReloadForStartup(saved.saved,observe:false);yield return StartupFinished();
            Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);yield return DormantRestorationFixture.Save(editor);
            var value=editor.Read(saved.front);value.footPlacement=new(){enabled=true,modelHash=CalibrationModel};
            Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{value},Array.Empty<string>(),out error),Is.True,error);Assert.That(editor.KeepTemporaryRoom(out error),Is.True,error);
            value.footPlacement=new();Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{value},Array.Empty<string>(),out error),Is.True,error);
            // No frame has polled publication. This exact preference exists only
            // in the detached in-flight snapshot, not either journal's contents.
            Assert.That(editor.TemporarySavePending,Is.True);Assert.That(editor.Read(saved.front).footPlacement.modelHash,Is.Empty);
            Assert.That(editor.RecoverySavedSnapshot().objects.Single(x=>x.id==saved.front).footPlacement.modelHash,Is.Empty);
            Assert.That(editor.UsesModel(CalibrationModel),Is.True,"Dispatched snapshot retains its original model preference");
            yield return DormantRestorationFixture.Save(editor);Assert.That(editor.UsesModel(CalibrationModel),Is.True);
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.Read(saved.front).footPlacement.modelHash,Is.EqualTo(CalibrationModel));
            editor.Undo();Assert.That(editor.UsesModel(CalibrationModel),Is.False);Assert.That(editor.HistoricalModel(CalibrationModel),Is.True);Assert.That(editor.Find(saved.front),Is.Null);
            editor.Redo();Assert.That(editor.UsesModel(CalibrationModel),Is.True);
        }
        [UnityTest]public IEnumerator UnavailableModelProfileOwnerCannotProveModelAbsence(){
            Assert.That(editor.TryFlush(out var error),Is.True,error);string path=Path.Combine(directory,"avatar-activities.v999.json");File.WriteAllText(path,"{\"version\":999}");
            yield return ReloadForStartup(observe:false);Assert.That(editor.ActivityProfiles.ReadOnly,Is.True);Assert.That(editor.ModelReferencesUncertain,Is.True);
            Assert.That(File.ReadAllText(path),Is.EqualTo("{\"version\":999}"));
        }
    }
    public sealed partial class AnimationWorkshopTests
    {
        [UnityTest]public IEnumerator SelectedAndInactiveAvatarProfilesRetainModelsAcrossIndependentUndo(){
            AuthorRuntime();using var asset=ModelLibrary.Inspect("Profile reference avatar.glb",ModelFixture.Mixamo());yield return SaveModelAsset(asset);
            var imported=editor.Motions.ImportAsync("Profile reference motion.glb",asset.Bytes);yield return PhysicalCreationTestWait.Until(()=>imported.IsCompleted,"Profile motion import did not finish");
            Assert.That(imported.Exception,Is.Null);var motion=imported.Result.Single();string inactive=new string('b',64);var choice=new TutorMotionChoice{motionId=motion.id};
            Assert.That(editor.ActivityProfiles.Assign(asset.Hash,motion.rigHash,TutorMotionRole.Speaking,choice,editor.Motions,out var error),Is.True,error);
            Assert.That(editor.ActivityProfiles.Assign(inactive,motion.rigHash,TutorMotionRole.Listening,choice,editor.Motions,out error),Is.True,error);
            Assert.That(authorActions.Execute(ModelRequest(asset.Hash),out error),Is.True,error);yield return ModelFinished();
            Assert.That(avatar.ModelHash,Is.EqualTo(asset.Hash));Assert.That(editor.UsesModel(asset.Hash),Is.True);Assert.That(editor.UsesModel(inactive),Is.True);Assert.That(editor.ModelReferencesUncertain,Is.False);
            editor.Undo();yield return PhysicalCreationTestWait.Until(()=>!avatar.ModelBusy,"Avatar Undo did not finish");Assert.That(editor.Read("maestro").modelHash,Is.Null.Or.Empty);
            Assert.That(editor.UsesModel(asset.Hash),Is.True,"Inactive tutor preferences still own their exact model");
            Assert.That(editor.ActivityProfiles.Remove(asset.Hash,TutorMotionRole.Speaking,motion.id,out error),Is.True,error);Assert.That(editor.UsesModel(asset.Hash),Is.False);Assert.That(editor.HistoricalModel(asset.Hash),Is.True);
            Assert.That(editor.ActivityProfiles.Remove(inactive,TutorMotionRole.Listening,motion.id,out error),Is.True,error);Assert.That(editor.UsesModel(inactive),Is.False);Assert.That(editor.HistoricalModel(inactive),Is.True,"Profile history is independent of room history");
            Assert.That(editor.ActivityProfiles.Undo(inactive,out error),Is.True,error);Assert.That(editor.UsesModel(inactive),Is.True);
            Assert.That(editor.ActivityProfiles.Redo(inactive,out error),Is.True,error);Assert.That(editor.HistoricalModel(inactive),Is.True);Assert.That(editor.UsesModel(inactive),Is.False);
        }
    }
}
