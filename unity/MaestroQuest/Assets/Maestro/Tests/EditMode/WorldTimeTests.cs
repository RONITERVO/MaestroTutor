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
    public sealed class WorldTimeTests
    {
        static RoomDocument Room()=>new(){version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}}};
        internal static WorldTimeSettings Cycle()=>new(){running=true,rate=60,cycleEnabled=true,frames=new[]{
            new WorldLightKeyframe{second=21600,ambientColor="#0000FF",ambientIntensity=.6f,sunIntensity=0,azimuth=-170,elevation=-30},
            new WorldLightKeyframe{second=64800,ambientColor="#FF0000",ambientIntensity=.2f,sunIntensity=1,azimuth=170,elevation=30}}};
        [Test] public void ProgressCrossesMidnightWithoutUndoOrGuardChurnAndUnrelatedUndoKeepsTime(){
            var doc=Room();doc.worldTime.second=86390;doc.worldTime.settings.running=true;doc.worldTime.settings.rate=60;var j=new RoomJournal(doc);int guard=j.WorldTimeRevision;
            Assert.That(j.AdvanceWorldTime(.5),Is.True);Assert.That(j.WorldTime.day,Is.EqualTo(1));Assert.That(j.WorldSecond,Is.EqualTo(20));Assert.That(j.WorldTimeRevision,Is.EqualTo(guard));Assert.That(j.CanUndo,Is.False);
            var item=j.Read("maestro");item.color=Color.red;Assert.That(j.Apply(new[]{item},Array.Empty<string>(),out var e),Is.True,e);j.AdvanceWorldTime(.5);j.Undo();Assert.That(j.WorldSecond,Is.EqualTo(50));
            var changed=j.WorldTime;changed.settings.rate=120;Assert.That(j.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out e,worldTime:changed),Is.True,e);j.AdvanceWorldTime(.5);Assert.That(j.WorldSecond,Is.EqualTo(110));j.Undo();Assert.That(j.WorldSecond,Is.EqualTo(50));Assert.That(j.WorldTime.settings.rate,Is.EqualTo(60));Assert.That(j.WorldTimeRevision,Is.GreaterThan(guard));
        }
        [Test] public void ClockStopsAtItsBoundAndNeverCatchesUpAStall(){
            var doc=Room();doc.worldTime.day=999999;doc.worldTime.second=86399;doc.worldTime.settings.running=true;var j=new RoomJournal(doc);int guard=j.WorldTimeRevision;
            foreach(double dt in new[]{double.NaN,double.PositiveInfinity,-1,0,2})Assert.That(j.AdvanceWorldTime(dt),Is.False);
            Assert.That(j.WorldSecond,Is.EqualTo(86399));j.AdvanceWorldTime(1);Assert.That(j.WorldTime.Valid,Is.True);Assert.That(j.WorldTime.settings.running,Is.False);Assert.That(j.WorldTimeRevision,Is.GreaterThan(guard));Assert.That(j.WorldTime.day,Is.EqualTo(999999));
        }
        [Test] public void MidnightLightingBlendsEnergyAndShortestAnglesContinuously(){
            var p=new WorldLightingProjection(new RoomLighting(),Cycle());var midnight=p.Sample(0);Assert.That(midnight.Enabled,Is.True);Assert.That(midnight.Ambient.x,Is.EqualTo(.1f).Within(.0001));Assert.That(midnight.Ambient.z,Is.EqualTo(.3f).Within(.0001));Assert.That(midnight.Azimuth,Is.EqualTo(-180).Within(.0001));Assert.That(midnight.Elevation,Is.Zero.Within(.0001));Assert.That(midnight.Direction.sqrMagnitude,Is.EqualTo(1).Within(.0001));
            Assert.That(Vector3.Distance(p.Sample(86399.999).Ambient,midnight.Ambient),Is.LessThan(.0001));Assert.That(p.Sample(21600).Ambient.z,Is.EqualTo(.6f).Within(.0001));
            var off=Cycle();off.cycleEnabled=false;Assert.That(new WorldLightingProjection(new RoomLighting(),off).Sample(0).Enabled,Is.False);
        }
        [Test] public void CachedCycleSamplingDoesNotAllocatePerFrame(){
            var projection=new WorldLightingProjection(new RoomLighting(),Cycle());var sum=projection.Sample(0).Ambient;
            long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++)sum+=projection.Sample(i*83.5).Ambient;long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Assert.That(bytes,Is.Zero);Assert.That(sum.sqrMagnitude,Is.GreaterThan(0));
        }
        [Test] public void DefinitionsRejectUnorderedOrDuplicateFramesAndCurrentWireCannotDefaultMissingFields(){
            var doc=Room();doc.worldTime.settings=Cycle();Assert.That(doc.Validate(out var e),Is.True,e);var wire=JObject.Parse(JsonUtility.ToJson(doc));Assert.That(RoomWorldTime.ValidWire(wire),Is.True);
            ((JObject)wire["worldTime"]["settings"]["frames"][0]).Remove("sunColor");Assert.That(RoomWorldTime.ValidWire(wire),Is.False);
            wire=JObject.Parse(JsonUtility.ToJson(doc));wire["worldTime"]["extra"]=true;Assert.That(RoomWorldTime.ValidWire(wire),Is.False);wire.Remove("worldTime");Assert.That(RoomWorldTime.ValidWire(wire),Is.False);wire["version"]=27;Assert.That(RoomWorldTime.ValidWire(wire),Is.True);
            doc.worldTime.settings.frames[1].second=21600;Assert.That(doc.Validate(out _),Is.False);doc.worldTime.settings.frames[1].second=1;Assert.That(doc.Validate(out _),Is.False);
        }
        [Test] public void ForkProgressDiscardInvalidatesOnlyClockGuardsAndSnapshotKeepRetainsExactTime(){
            var doc=Room();doc.worldTime.settings.running=true;var j=new RoomJournal(doc);var fork=j.Fork();int guard=j.WorldTimeRevision;fork.AdvanceWorldTime(.5);j.InvalidateChangedObservations(fork);Assert.That(j.WorldTimeRevision,Is.GreaterThan(guard));Assert.That(j.WorldSecond,Is.EqualTo(43200));Assert.That(j.ApplySnapshot(fork.Snapshot(),out var e),Is.True,e);Assert.That(j.WorldSecond,Is.EqualTo(43200.5));j.Undo();Assert.That(j.WorldSecond,Is.EqualTo(43200));
        }
        [Test] public void StorageAndPairedSnapshotRetainClockAndProtectUnknownVersions(){
            string dir=Path.Combine(Path.GetTempPath(),"MaestroWorldTime-"+Guid.NewGuid().ToString("N"));
            try{var doc=Room();doc.worldTime.settings=Cycle();doc.worldTime.day=5;doc.worldTime.second=123.75;var store=new RoomStorage(dir);Assert.That(store.Save(doc,out var e),Is.True,e);var loaded=store.Load(out e);Assert.That(loaded.worldTime.Same(doc.worldTime),Is.True,e);Assert.That(RoomSnapshotTransaction.FromDocuments(loaded,ProgramMemoryDocument.Empty()),Is.Not.Null);
                var wire=JObject.Parse(JsonUtility.ToJson(doc));wire["worldTime"]["version"]=2;string raw=wire.ToString();File.WriteAllText(Path.Combine(dir,RoomStorage.FileName),raw);store=new RoomStorage(dir);Assert.That(store.Load(out _),Is.Null);Assert.That(store.ReadOnly,Is.True);Assert.That(File.ReadAllText(Path.Combine(dir,RoomStorage.FileName)),Is.EqualTo(raw));
            }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
        }
    }
}
