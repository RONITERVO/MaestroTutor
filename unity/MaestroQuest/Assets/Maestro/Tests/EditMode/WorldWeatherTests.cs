// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using Maestro.Quest.Art;
using Maestro.Quest.Creation;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class WorldWeatherTests
    {
        static RoomDocument Room()=>new(){version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}}};
        [Test]public void WeatherInterpolationCrossesMidnightAndSeeksWithoutMutatingTheSavedTarget(){
            var w=new RoomWeather{startSecond=86390,transitionSeconds=20,settings=new(){rainMmPerHour=100,windX=10,cloudCover=1,fogDensity=.2f}};
            var p=new WeatherProjection(w);Assert.That(p.Sample(86380).Rain,Is.Zero);Assert.That(p.Sample(86400).Rain,Is.EqualTo(50));Assert.That(p.Sample(86410).Rain,Is.EqualTo(100));Assert.That(p.Sample(86390).Rain,Is.Zero);Assert.That(w.settings.rainMmPerHour,Is.EqualTo(100));
            var light=p.Sample(86410).Apply(new WorldLightSample{Sun=Vector3.one,Ambient=Vector3.one});Assert.That(light.Sun.x,Is.EqualTo(.1f).Within(.00001));Assert.That(light.Ambient.x,Is.EqualTo(.65f).Within(.00001));
            long before=GC.GetAllocatedBytesForCurrentThread();float sum=0;for(int i=0;i<1000;i++)sum+=p.Sample(86390+i*.02).Rain;Assert.That(GC.GetAllocatedBytesForCurrentThread()-before,Is.Zero);Assert.That(sum,Is.GreaterThan(0));
        }
        [Test]public void WeatherRejectsInvalidNumbersAndCurrentWireNeverDefaultsMissingFields(){
            var doc=Room();Assert.That(doc.Validate(out var e),Is.True,e);var wire=JObject.Parse(JsonUtility.ToJson(doc));Assert.That(RoomWeather.ValidWire(wire),Is.True);
            ((JObject)wire["weather"]["settings"]).Remove("windX");Assert.That(RoomWeather.ValidWire(wire),Is.False);
            wire=JObject.Parse(JsonUtility.ToJson(doc));wire["weather"]["extra"]=true;Assert.That(RoomWeather.ValidWire(wire),Is.False);wire.Remove("weather");Assert.That(RoomWeather.ValidWire(wire),Is.False);wire["version"]=28;Assert.That(RoomWeather.ValidWire(wire),Is.True);
            foreach(float value in new[]{float.NaN,float.PositiveInfinity,-1,121}){doc.weather.settings.rainMmPerHour=value;Assert.That(doc.Validate(out _),Is.False);}
            doc.weather.settings.rainMmPerHour=1;doc.weather.settings.fogColor="#FFFFFF\n";Assert.That(doc.Validate(out _),Is.False);
        }
        [Test]public void WeatherJournalForkAndUndoKeepIndependentClockAndContainerState(){
            var j=new RoomJournal(Room());int guard=j.WeatherRevision;var w=j.Weather;w.settings.rainMmPerHour=40;
            Assert.That(j.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out var e,weather:w),Is.True,e);w.settings.rainMmPerHour=120;Assert.That(j.Weather.settings.rainMmPerHour,Is.EqualTo(40));
            var fork=j.Fork();w=fork.Weather;w.settings.rainMmPerHour=80;fork.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out e,weather:w);j.InvalidateChangedObservations(fork);Assert.That(j.WeatherRevision,Is.GreaterThan(guard));Assert.That(j.Weather.settings.rainMmPerHour,Is.EqualTo(40));
            j.Undo();Assert.That(j.Weather.settings.rainMmPerHour,Is.Zero);j.Redo();Assert.That(j.Weather.settings.rainMmPerHour,Is.EqualTo(40));Assert.That(j.WorldSecond,Is.EqualTo(43200));
        }
        [Test]public void WeatherStorageAndSnapshotRoundTripAndProtectFutureVersions(){
            string dir=Path.Combine(Path.GetTempPath(),"MaestroWeather-"+Guid.NewGuid().ToString("N"));
            try{var doc=Room();doc.weather.settings.rainMmPerHour=70;doc.weather.seed=23;var store=new RoomStorage(dir);Assert.That(store.Save(doc,out var e),Is.True,e);var loaded=store.Load(out e);Assert.That(loaded.weather.Same(doc.weather),Is.True,e);Assert.That(RoomSnapshotTransaction.FromDocuments(loaded,ProgramMemoryDocument.Empty()),Is.Not.Null);
                var wire=JObject.Parse(JsonUtility.ToJson(doc));wire["weather"]["version"]=2;string raw=wire.ToString();File.WriteAllText(Path.Combine(dir,RoomStorage.FileName),raw);store=new RoomStorage(dir);Assert.That(store.Load(out _),Is.Null);Assert.That(store.ReadOnly,Is.True);Assert.That(File.ReadAllText(Path.Combine(dir,RoomStorage.FileName)),Is.EqualTo(raw));
            }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
        }
    }
}
