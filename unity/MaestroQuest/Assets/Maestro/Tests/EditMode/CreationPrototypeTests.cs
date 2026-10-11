// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.IO;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class CreationPrototypeTests
    {
        [Test] public void SharedSourceAndPlacementCasesMatchWebValidation() {
            var cases=JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/creation-prototypes-contract.json")));
            foreach(var c in cases)Assert.That(BehaviourCatalog.TryCall("object.batch.create",1,(JObject)c["arguments"],out _,out var error),Is.EqualTo((bool)c["valid"]),(string)c["name"]+": "+error);
        }
        [Test] public void CapturedMotionTransformsWithItsObjectWithoutChangingOriginalFrames() {
            var source=new RoomObjectData {id=new string('a',32),kind=RoomObjectKind.Ball,position=new Vector3(2,1,3),rotation=Quaternion.Euler(0,90,0),scale=2,
                motion=new RoomMotion {loop=true,frames=new[]{new MotionFrame {time=0,position=new Vector3(2,1,3),rotation=Quaternion.Euler(0,90,0),scale=2},new MotionFrame {time=1,position=new Vector3(4,1,3),rotation=Quaternion.Euler(0,180,0),scale=1}}}};
            string before=JsonUtility.ToJson(source);var prototype=CreationPrototype.Capture(source);Assert.That(prototype.Validate(out var error),Is.True,error);
            var copy=prototype.Instantiate("Copy",new Vector3(0,2,0),Quaternion.identity,1);
            Assert.That(Vector3.Distance(copy.motion.frames[1].position,new Vector3(0,2,1)),Is.LessThan(.0001));
            Assert.That(Quaternion.Angle(copy.motion.frames[1].rotation,Quaternion.Euler(0,90,0)),Is.LessThan(.01));Assert.That(copy.motion.frames[1].scale,Is.EqualTo(.5));
            Assert.That(copy.motion.loop,Is.True);Assert.That(JsonUtility.ToJson(source),Is.EqualTo(before));
        }
        [Test] public void CapturedConstructorIsOrdinaryEditableSourceWithNoOriginalResourceIds() {
            var original=new RoomObjectData {id=new string('a',32),name="Ball",kind=RoomObjectKind.Ball,color=Color.red};
            var batch=new CreationBatch {position=new Vector3(0,1,1),blueprint=new CreationBlueprint {pieces=new[]{new CreationPiece {slot="ball",name="Ball",source=new CreationSource {kind="prototype",prototype=CreationPrototype.Capture(original)}}}}};
            var module=ConstructionModule.Definition(batch,"Reusable ball");Assert.That(module.ToString(),Does.Not.Contain(original.id));
            ProgramModuleLibrary.Validate(module);Assert.That((string)module["program"]["functions"][1]["body"][0]["capability"],Is.EqualTo("object.batch.create"));
            Assert.That(batch.Prepare(out var first,out var error),Is.True,error);Assert.That(batch.Prepare(out var second,out error),Is.True,error);Assert.That(first[0].id,Is.Not.EqualTo(second[0].id));
            first[0].color=Color.green;Assert.That(second[0].color,Is.EqualTo(Color.red));
        }
    }
}
