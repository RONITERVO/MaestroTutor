// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Art;
using Maestro.Quest.Creation;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class TemporalEditHistoryTests
    {
        static RoomJournal Journal(){var d=new RoomDocument{version=RoomDocument.CurrentVersion,objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}}};d.worldTime.settings.running=true;return new(d);}
        static RoomJournal.PreparedEdit Clock(RoomJournal j,RoomWorldTime value,WorldTimeEdit mode){Assert.IsTrue(j.Prepare(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out var p,out var e,worldTime:value,visualOnly:true,timeEdit:mode),e);return p;}
        static RoomJournal.PreparedEdit Weather(RoomJournal j,RoomWeather value){Assert.IsTrue(j.Prepare(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out var p,out var e,weather:value,visualOnly:true,weatherTransition:true),e);return p;}
        [Test] public void ConfigurationAcceptsAtCurrentPositionAndHistoryNeverRewindsIt(){
            var j=Journal();var value=j.WorldTime;value.settings.rate=20;using var p=Clock(j,value,WorldTimeEdit.Settings);
            j.AdvanceWorldTime(.5);Assert.IsTrue(p.Accept(j,out var e,true),e);Assert.AreEqual(43200.5,j.WorldSecond);Assert.AreEqual(20,j.WorldTime.settings.rate);
            j.AdvanceWorldTime(.5);j.Undo();Assert.AreEqual(43210.5,j.WorldSecond);Assert.AreEqual(1,j.WorldTime.settings.rate);
            j.AdvanceWorldTime(.5);j.Redo();Assert.AreEqual(43211,j.WorldSecond);Assert.AreEqual(20,j.WorldTime.settings.rate);
        }
        [Test] public void InitiallyEqualSeekRemembersThePositionImmediatelyBeforeAcceptance(){
            var j=Journal();using var p=Clock(j,j.WorldTime,WorldTimeEdit.Position);j.AdvanceWorldTime(.5);
            Assert.IsTrue(p.Accept(j,out var e,true),e);Assert.AreEqual(43200,j.WorldSecond);j.AdvanceWorldTime(.25);j.Undo();Assert.AreEqual(43200.5,j.WorldSecond);Assert.IsTrue(j.WorldTime.settings.running);
            j.Redo();Assert.AreEqual(43200,j.WorldSecond);
        }
        [Test] public void EqualConfigurationKeepsRedoHistoryAndRevisionEvenAfterTicking(){
            var j=Journal();var v=j.WorldTime;v.settings.rate=2;using(var p=Clock(j,v,WorldTimeEdit.Settings))Assert.IsTrue(p.Accept(j,out _));j.Undo();int revision=j.WorldTimeRevision;
            using(var p=Clock(j,j.WorldTime,WorldTimeEdit.Settings)){j.AdvanceWorldTime(.5);Assert.IsTrue(p.Accept(j,out var e,true),e);}
            Assert.AreEqual(revision,j.WorldTimeRevision);Assert.IsFalse(j.CanUndo);Assert.IsTrue(j.CanRedo);Assert.AreEqual(43200.5,j.WorldSecond);
        }
        [Test] public void WeatherStartsFromTheCurrentSampleAndHistoryRestartsFromPresentWeather(){
            var j=Journal();using(var first=Weather(j,new(){settings=new(){rainMmPerHour=100},transitionSeconds=10}))Assert.IsTrue(first.Accept(j,out _));
            j.AdvanceWorldTime(.5);using var next=Weather(j,new(){seed=2,settings=new(){rainMmPerHour=20},transitionSeconds=5});j.AdvanceWorldTime(.5);
            Assert.IsTrue(next.Accept(j,out var e,true),e);Assert.AreEqual(43201,j.Weather.startSecond);Assert.AreEqual(10,j.Weather.from.rainMmPerHour,.00001);
            j.AdvanceWorldTime(.5);float current=new WeatherProjection(j.Weather).Sample(j.WorldSeconds).Rain;
            j.Undo();Assert.AreEqual(100,j.Weather.settings.rainMmPerHour);Assert.AreEqual(current,j.Weather.from.rainMmPerHour,.00001);Assert.AreEqual(43201.5,j.Weather.startSecond);
            j.AdvanceWorldTime(.5);current=new WeatherProjection(j.Weather).Sample(j.WorldSeconds).Rain;j.Redo();Assert.AreEqual(20,j.Weather.settings.rainMmPerHour);Assert.AreEqual(current,j.Weather.from.rainMmPerHour,.00001);Assert.AreEqual(43202,j.Weather.startSecond);
        }
        [Test] public void ReapplyingWeatherDoesNotRestartItsFadeOrCreateHistory(){
            var j=Journal();var v=new RoomWeather{seed=2,settings=new(){rainMmPerHour=80},transitionSeconds=10};using(var p=Weather(j,v))Assert.IsTrue(p.Accept(j,out _));var before=j.Weather;int revision=j.WeatherRevision;
            using(var p=Weather(j,v)){j.AdvanceWorldTime(.5);Assert.IsTrue(p.Accept(j,out var e,true),e);}
            Assert.IsTrue(j.Weather.Same(before));Assert.AreEqual(revision,j.WeatherRevision);j.Undo();Assert.AreEqual(0,j.Weather.settings.rainMmPerHour);Assert.IsFalse(j.CanUndo);
        }
        [Test] public void TemporalIntentRejectsAuthoredConflictsAndClockLimitChanges(){
            var j=Journal();using(var p=Clock(j,j.WorldTime,WorldTimeEdit.Settings)){var item=j.Read("maestro");item.color=UnityEngine.Color.red;Assert.IsTrue(j.Apply(new[]{item},Array.Empty<string>(),out _));Assert.IsFalse(p.Accept(j,out _,true));}
            var value=j.WorldTime;value.day=999999;value.second=86399;Assert.IsTrue(j.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out _,worldTime:value));
            using(var p=Clock(j,value,WorldTimeEdit.Settings)){j.AdvanceWorldTime(1);Assert.IsFalse(p.Accept(j,out _,true));}
        }
        [Test] public void TemporalModeCannotHideObjectOrMixedTemporalChanges(){
            var j=Journal();Assert.IsFalse(j.Prepare(new[]{j.Read("maestro")},Array.Empty<string>(),out _,out _,worldTime:j.WorldTime,timeEdit:WorldTimeEdit.Settings,visualOnly:true));
            Assert.IsFalse(j.Prepare(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out _,out _,worldTime:j.WorldTime,weather:j.Weather,timeEdit:WorldTimeEdit.Position,visualOnly:true));
        }
        [Test] public void AcceptedSnapshotIsDetachedAndMatchesTheActualTemporalBoundary(){
            var j=Journal();var v=j.WorldTime;v.settings.rate=5;using var p=Clock(j,v,WorldTimeEdit.Settings);j.AdvanceWorldTime(.5);Assert.IsTrue(p.Accept(j,out _,true));var accepted=p.AcceptedTemporalSnapshot();
            j.AdvanceWorldTime(.5);Assert.AreEqual(43200.5,accepted.worldTime.second);accepted.worldTime.settings.rate=99;Assert.AreEqual(5,j.WorldTime.settings.rate);
        }
    }
}
