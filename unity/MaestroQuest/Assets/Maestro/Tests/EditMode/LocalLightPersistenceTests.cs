// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class LocalLightPersistenceTests
    {
        static RoomObjectData Item()=>new(){id=new string('a',32),kind=RoomObjectKind.Block,lights=new[]{new RoomLight{kind="spot",shadows="hard",position=Vector3.up}}};
        static RoomDocument Room(params RoomObjectData[] objects)=>new(){version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}}.Concat(objects).ToArray()};
        [Test]public void LampsCopyAndRoundTripThroughTheSameReusablePrototype(){
            var data=Item();var room=Room(data);Assert.That(room.Validate(out var error),Is.True,error);var copied=data.Copy();copied.lights[0].intensity=3;Assert.That(data.lights[0].intensity,Is.EqualTo(1));
            var prototype=CreationPrototype.Capture(data);Assert.That(prototype.version,Is.EqualTo(8));var wire=CreationPrototypeSchema.Encode(prototype);
            Assert.That(CapabilityArguments.Validate(wire,CreationPrototypeSchema.Schema(),out error),Is.True,error);var read=CreationPrototype.Read(wire);Assert.That(read.Validate(out error),Is.True,error);
            var made=read.Instantiate("Lamp copy",Vector3.one,Quaternion.identity,1);Assert.That(JsonUtility.ToJson(made.lights[0]),Is.EqualTo(JsonUtility.ToJson(data.lights[0])));
            read.version=7;Assert.That(read.Validate(out _),Is.False);room.version=38;Assert.That(room.Validate(out _),Is.False);data.lights=Array.Empty<RoomLight>();Assert.That(room.Validate(out error),Is.True,error);
        }
        [Test]public void LightWireRejectsMalformedAndFutureDefinitionsWithoutErasingThem(){
            var room=Room(Item());var wire=JObject.Parse(JsonUtility.ToJson(room));Assert.That(RoomLight.ValidWire(wire),Is.True);
            var lamp=(JObject)wire["objects"][2]["lights"][0];lamp["unexpected"]=true;Assert.That(RoomLight.ValidWire(wire),Is.False);lamp.Remove("unexpected");lamp["enabled"]="yes";Assert.That(RoomLight.ValidWire(wire),Is.False);
            room.objects[2].lights[0].version=2;Assert.That(room.Validate(out _),Is.False);Assert.That(room.objects[2].lights[0].version,Is.EqualTo(2));
            room.objects[2].lights=null;Assert.That(room.Validate(out _),Is.False);
        }
        [Test]public void LightAttachmentsAndBudgetsAreValidatedBeforeAtomicRoomChanges(){
            var a=Item();var room=Room(a);a.lights[0].part="MissingArm";Assert.That(room.Validate(out _),Is.False);a.lights[0].part="";
            a.lights[0].kind="point";a.lights[0].angle=60;a.lights=new[]{a.lights[0],new RoomLight{id="Spot",kind="spot",shadows="hard"}};Assert.That(room.Validate(out _),Is.False);
            a.lights[1].shadows="none";Assert.That(room.Validate(out var error),Is.True,error);
            var b=a.Copy();b.id=new string('b',32);b.lights[0].shadows="none";var c=Item();c.id=new string('c',32);c.lights[0].shadows="none";
            Assert.That(Room(a,b).Validate(out error),Is.True,error);Assert.That(Room(a,b,c).Validate(out _),Is.False);
            a.lights[0].rotation=new Quaternion(0,0,0,2);Assert.That(Room(a).Validate(out _),Is.False);
        }
    }
}
