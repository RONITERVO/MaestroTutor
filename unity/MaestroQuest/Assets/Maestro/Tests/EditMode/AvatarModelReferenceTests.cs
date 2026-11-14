// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Persistence;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class AvatarModelReferenceTests
    {
        const string Hash="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",Other="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",Rig="eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
        const string Motion="cccccccccccccccccccccccccccccccc",Appearance="dddddddddddddddddddddddddddddddd";
        string directory;
        [SetUp]public void Before(){directory=Path.Combine(Path.GetTempPath(),"MaestroAvatarModelReferences-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);}
        [TearDown]public void After(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        static AvatarActivityProfile Profile(string model,string rig=Rig)=>new(){modelHash=model,rigHash=rig,roles=new[]{new TutorRoleChoices{role=TutorMotionRole.Speaking,choices=new[]{new TutorMotionChoice{motionId=Motion}}}}};
        static byte[] Bytes(params AvatarActivityProfile[] profiles){var document=new AvatarActivityDocument{avatars=profiles};Assert.That(document.Valid(),Is.True);return Encoding.UTF8.GetBytes(JsonUtility.ToJson(document));}
        void Write(string name,byte[] bytes)=>File.WriteAllBytes(Path.Combine(directory,name),bytes);
        void Write(string name,string text)=>Write(name,Encoding.UTF8.GetBytes(text));
        Dictionary<string,string> Evidence()=>Directory.GetFiles(directory).ToDictionary(Path.GetFileName,path=>Convert.ToBase64String(File.ReadAllBytes(path)));
        SavedAssetReferenceProof Proof()=>SavedAssetReferences.ReadModels(directory,Hash);
        void Expect(bool retained,bool uncertain=false){var before=Evidence();var proof=Proof();Assert.That(proof.Ready,Is.True);Assert.That(proof.Retained,Is.EqualTo(retained));Assert.That(proof.Uncertain,Is.EqualTo(uncertain),string.Join("; ",proof.Reasons));Assert.That(Evidence(),Is.EquivalentTo(before));}
        [TestCase("")][TestCase(".backup")][TestCase(".pending")][TestCase(".unreadable")][TestCase(".prepare.cccccccccccccccccccccccccccccccc")]
        public void ValidatedRetainedProfileFilesProtectInactiveModelIdentitiesWithoutLoadingOrRecovery(string suffix){Write("avatar-activities.v2.json"+suffix,Bytes(Profile(Other),Profile(Hash)));Expect(true);}
        [Test]public void EmptyProfilesAndCompatibilityRigHashesDoNotBecomePayloadReferences(){Write("avatar-activities.v2.json",Bytes());Expect(false);Write("avatar-activities.v2.json",Bytes(Profile(Other,Hash)));Expect(false);}
        [Test]public void ProfileHistoryStillRetainsARemovedInactiveModelInUndoAndRedo(){
            Write("avatar-activities.v2.json",Bytes(Profile(Other),Profile(Hash)));var profiles=new AvatarActivityProfiles(directory);
            Assert.That(profiles.UsesModel(Hash),Is.True);Assert.That(profiles.UsesModel(Other),Is.True);Assert.That(profiles.UsesModel(Rig),Is.False);
            profiles.Snapshot().avatars.Single(x=>x.modelHash==Hash).modelHash=Rig;profiles.Find(Hash).modelHash=Rig;Assert.That(profiles.UsesModel(Hash),Is.True);
            Assert.That(profiles.Remove(Hash,TutorMotionRole.Speaking,Motion,out var error),Is.True,error);Assert.That(profiles.UsesModel(Hash),Is.False);Assert.That(profiles.HistoricalModel(Hash),Is.True);Assert.That(profiles.UsesModel(Other),Is.True);
            Assert.That(profiles.Undo(Hash,out error),Is.True,error);Assert.That(profiles.UsesModel(Hash),Is.True);Assert.That(profiles.HistoricalModel(Hash),Is.True);
            Assert.That(profiles.Redo(Hash,out error),Is.True,error);Assert.That(profiles.UsesModel(Hash),Is.False);Assert.That(profiles.HistoricalModel(Hash),Is.True);Expect(true);
        }
        [TestCase("avatar-activities.v1.json")][TestCase("avatar-activities.v3.json")][TestCase("avatar-activities.v2.json.prepare.unknown")][TestCase("Avatar-activities.v2.json")][TestCase("avatar-activities.v2.json.snapshot.cccccccccccccccccccccccccccccccc")]
        public void UnsupportedProfileEvidenceIsPreservedAndCannotProveAbsence(string name){Write(name,Bytes(Profile(Other)));Expect(false,true);}
        [TestCase(false)][TestCase(true)]public void MalformedOrOversizedProfileEvidenceIsNeverRepaired(bool oversized){Write("avatar-activities.v2.json",oversized?new byte[AvatarActivityProfiles.MaximumBytes+1]:new byte[]{0xff});Expect(false,true);}
        [Test]public void NewerProfileDocumentCannotHideReferencesEvenWhenPrimaryIsEmpty(){Write("avatar-activities.v2.json",Bytes());Write("avatar-activities.v2.json.backup","{\"version\":999,\"avatars\":[]}");Expect(false,true);}
        [Test]public void ProfileDirectoryOrSharingDenialCannotBecomeAnEmptyProfileCollection(){
            string path=Path.Combine(directory,"avatar-activities.v2.json");Directory.CreateDirectory(path);Assert.That(Proof().Uncertain,Is.True);Directory.Delete(path);
            var bytes=Bytes(Profile(Hash));Write("avatar-activities.v2.json",bytes);using(var held=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None))Assert.That(Proof().Uncertain,Is.True);
            Assert.That(File.ReadAllBytes(path),Is.EqualTo(bytes));
        }
        [Test]public void LinkedProfileEvidenceIsUncertainAndDoesNotTouchItsTarget(){
            string path=Path.Combine(directory,"avatar-activities.v2.json"),target=Path.Combine(directory,"original.bin");var bytes=Bytes(Profile(Hash));File.WriteAllBytes(target,bytes);AssetSidecarTestLinks.Create(path,target);
            try{Assert.That(Proof().Uncertain,Is.True);Assert.That(File.ReadAllBytes(target),Is.EqualTo(bytes));}finally{File.Delete(path);}
        }
        [Test]public void ModelSpecificProfileInspectionDoesNotChangeImageOrAudioProof(){
            Write("avatar-activities.v2.json","unreadable profile");var before=Evidence();
            foreach(var proof in new[]{SavedAssetReferences.ReadImages(directory,Hash),SavedAssetReferences.ReadAudio(directory,Hash)}){Assert.That(proof.Ready,Is.True);Assert.That(proof.Uncertain,Is.False);Assert.That(proof.Retained,Is.False);}
            Assert.That(Evidence(),Is.EquivalentTo(before));Expect(false,true);
        }
        [TestCase(false)][TestCase(true)]public void SavedCalibrationAndMaterialPreferencesRetainTheirExactInactiveModel(bool material){
            var maestro=new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro,modelHash=Other};
            if(material)maestro.appearanceBindings=new[]{new AppearanceBinding{appearanceId=Appearance,kind="material",modelHash=Hash,materialIndex=0}};
            else maestro.footPlacement=new(){enabled=true,modelHash=Hash,leftClearance=.01f};
            var room=new RoomDocument{version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},maestro},appearances=new[]{new RoomAppearance{id=Appearance,name="Preserved material preference"}}};
            Assert.That(room.Validate(out var error),Is.True,error);Write(RoomStorage.FileName,RoomLiquidCodec.Encode(room));Expect(true);
        }
    }
}
