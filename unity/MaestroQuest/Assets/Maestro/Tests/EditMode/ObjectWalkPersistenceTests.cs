// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
    public sealed class ObjectWalkPersistenceTests {
        static RoomObjectData Character(bool library)=>new(){id=new string('a',32),kind=RoomObjectKind.ImportedModel,modelHash=new string('b',64),walkClip=library?0:2,walkMotionId=library?new string('c',32):null};
        [TestCase(false)][TestCase(true)]public void ObjectWalkingChoicesRoundTripPrototypeAndCopyWithoutRetargeting(bool library){
            var original=Character(library);var prototype=CreationPrototype.Capture(original);Assert.That(prototype.version,Is.EqualTo(7));
            var wire=CreationPrototypeSchema.Encode(prototype);Assert.That(CapabilityArguments.Validate(wire,CreationPrototypeSchema.Schema(),out var error),Is.True,error);
            var read=CreationPrototype.Read(wire);Assert.That(read.Validate(out error),Is.True,error);var copy=read.Instantiate("Copied character",Vector3.one,Quaternion.identity,1);
            Assert.That(copy.id,Is.Not.EqualTo(original.id));Assert.That(copy.walkClip,Is.EqualTo(original.walkClip));Assert.That(copy.walkMotionId,Is.EqualTo(original.walkMotionId));Assert.That(copy.modelHash,Is.EqualTo(original.modelHash));
            read.version=6;Assert.That(read.Validate(out _),Is.False);read.version=7;read.kind="block";Assert.That(read.Validate(out _),Is.False);
        }
        [TestCase(false)][TestCase(true)]public void ObjectWalkingChoicesRequireNewRoomVersionAndRetainExactIds(bool library){
            var data=Character(library);var room=new RoomDocument{version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},data}};
            Assert.That(room.Validate(out var error),Is.True,error);var copy=room.Copy();Assert.That(copy.objects[2].walkMotionId,Is.EqualTo(data.walkMotionId));Assert.That(copy.objects[2].walkClip,Is.EqualTo(data.walkClip));
            room.version=37;Assert.That(room.Validate(out _),Is.False);room.version=RoomDocument.CurrentVersion;data.walkMotionId=new string('C',32);data.walkClip=0;Assert.That(room.Validate(out _),Is.False);
        }
        [Test]public void ObjectWalkingLibraryReferenceIsRetainedByAnEditableConstructor(){
            var data=Character(true);var batch=new CreationBatch{position=Vector3.zero,blueprint=new CreationBlueprint{pieces=new[]{new CreationPiece{slot="character",name="Character",source=new CreationSource{kind="prototype",prototype=CreationPrototype.Capture(data)}}}}};
            var module=ConstructionModule.Definition(batch,"Character");ProgramModuleLibrary.Validate(module);
            Assert.That(BehaviourProgram.TryParse(module["program"].ToString(),out var program,out var error),Is.True,error);Assert.That(program.ReferencesMotion(data.walkMotionId),Is.True);
        }
    }
}
