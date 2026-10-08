// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using Maestro.Quest.Creation;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class RoomLightingTests
    {
        static RoomDocument Room()=>new(){version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}}};
        [Test] public void LightingUndoIsIndependentAndForkDiscardNeverReusesAnObservation() {
            var j=new RoomJournal(Room());var light=new RoomLighting{enabled=true,ambientIntensity=.1f};
            Assert.That(j.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out var e,lighting:light),Is.True,e);int revision=j.LightingRevision;
            light.ambientIntensity=.9f;Assert.That(j.Lighting.ambientIntensity,Is.EqualTo(.1f));
            Assert.That(j.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out e,lighting:j.Lighting),Is.True,e);Assert.That(j.LightingRevision,Is.EqualTo(revision));
            var item=j.Read("maestro");item.color=Color.red;Assert.That(j.Apply(new[]{item},Array.Empty<string>(),out e),Is.True,e);j.Undo();Assert.That(j.LightingRevision,Is.EqualTo(revision));
            j.Undo();Assert.That(j.Lighting.enabled,Is.False);Assert.That(j.LightingRevision,Is.GreaterThan(revision));Assert.That(j.CanUndo,Is.False);j.Redo();
            var fork=j.Fork();Assert.That(fork.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out e,lighting:light),Is.True,e);int temporary=fork.LightingRevision;
            j.InvalidateChangedObservations(fork);Assert.That(j.Lighting.ambientIntensity,Is.EqualTo(.1f));Assert.That(j.LightingRevision,Is.GreaterThan(temporary));
            Assert.That(j.ApplySnapshot(fork.Snapshot(),out e),Is.True,e);Assert.That(j.Lighting.ambientIntensity,Is.EqualTo(.9f));j.Undo();Assert.That(j.Lighting.ambientIntensity,Is.EqualTo(.1f));
        }
        [TestCase(-.1f)] [TestCase(2.1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidEnergyNeverEntersTheJournal(float value) {
            var j=new RoomJournal(Room());var light=j.Lighting;light.sunIntensity=value;
            Assert.That(j.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out _,lighting:light),Is.False);Assert.That(j.CanUndo,Is.False);
        }
        [TestCase("#123456\n")] [TestCase("#123456\r\n")] [TestCase(" #123456")] [TestCase("#12345G")]
        public void SavedLightingAndDailyFramesRejectInvalidHexColours(string color) {
            var room=Room();room.lighting.ambientColor=color;Assert.That(room.lighting.Valid,Is.False);
            Assert.Throws<InvalidDataException>(()=>RoomSnapshotTransaction.FromDocuments(room,ProgramMemoryDocument.Empty()));
            room=Room();room.worldTime.settings.frames=new[]{new WorldLightKeyframe{sunColor=color}};
            Assert.That(room.worldTime.Valid,Is.False);
            Assert.Throws<InvalidDataException>(()=>RoomSnapshotTransaction.FromDocuments(room,ProgramMemoryDocument.Empty()));
        }
        [Test]public void DirectionUsesAuthoredAxesAndCurrentWireCannotOmitOrInventFields() {
            var l=new RoomLighting{azimuth=0,elevation=0};Assert.That(Vector3.Distance(l.SunDirection,Vector3.forward),Is.LessThan(.00001));
            l.azimuth=90;Assert.That(Vector3.Distance(l.SunDirection,Vector3.right),Is.LessThan(.00001));l.elevation=90;Assert.That(Vector3.Distance(l.SunDirection,Vector3.up),Is.LessThan(.00001));
            var wire=JObject.Parse(JsonUtility.ToJson(Room()));Assert.That(RoomLighting.ValidWire(wire),Is.True);
            ((JObject)wire["lighting"]).Remove("enabled");Assert.That(RoomLighting.ValidWire(wire),Is.False);
            wire=JObject.Parse(JsonUtility.ToJson(Room()));wire["lighting"]["futureWeather"]=true;Assert.That(RoomLighting.ValidWire(wire),Is.False);
            wire.Remove("lighting");Assert.That(RoomLighting.ValidWire(wire),Is.False);wire["version"]=26;Assert.That(RoomLighting.ValidWire(wire),Is.True);
        }
        [Test]public void LightingRoundTripsThroughStorageAndPairedSnapshotAndProtectsFutureFiles() {
            string dir=Path.Combine(Path.GetTempPath(),"MaestroLighting-"+Guid.NewGuid().ToString("N"));
            try {
                var doc=Room();doc.lighting=new(){enabled=true,ambientColor="#123456",sunColor="#ABCDEF",sunIntensity=1.7f,elevation=-20};
                var storage=new RoomStorage(dir);Assert.That(storage.Save(doc,out var e),Is.True,e);var loaded=storage.Load(out e);Assert.That(loaded,Is.Not.Null,e);Assert.That(loaded.lighting.Same(doc.lighting),Is.True);
                Assert.That(RoomSnapshotTransaction.FromDocuments(loaded,ProgramMemoryDocument.Empty()),Is.Not.Null);
                doc.lighting.version=2;string raw=JsonUtility.ToJson(doc);File.WriteAllText(Path.Combine(dir,RoomStorage.FileName),raw);storage=new RoomStorage(dir);
                Assert.That(storage.Load(out _),Is.Null);Assert.That(storage.ReadOnly,Is.True);Assert.That(File.ReadAllText(Path.Combine(dir,RoomStorage.FileName)),Is.EqualTo(raw));
            }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
        }
        [Test]public void PreLightingRoomLoadsWithOriginalIllustrationAndCurrentTruncationDoesNot() {
            string dir=Path.Combine(Path.GetTempPath(),"MaestroLighting-"+Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(dir);var wire=JObject.Parse(JsonUtility.ToJson(Room()));wire["version"]=26;wire.Remove("lighting");File.WriteAllText(Path.Combine(dir,"room.v26.json"),wire.ToString());
                var storage=new RoomStorage(dir);var loaded=storage.Load(out var e);Assert.That(loaded,Is.Not.Null,e);Assert.That(loaded.lighting.enabled,Is.False);Assert.That(storage.Save(loaded,out e),Is.True,e);
                wire=JObject.Parse(JsonUtility.ToJson(loaded));((JObject)wire["lighting"]).Remove("sunIntensity");Assert.That(RoomLighting.ValidWire(wire),Is.False);
                var invalid=Room();invalid.lighting=null;Assert.Throws<InvalidDataException>(()=>RoomSnapshotTransaction.FromDocuments(invalid,ProgramMemoryDocument.Empty()));
            }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
        }
    }
}
