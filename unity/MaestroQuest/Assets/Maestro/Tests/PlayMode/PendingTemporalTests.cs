// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        [UnityTest] public IEnumerator PendingTemporalConfigurationSavesAcceptedTimeWhileWorldKeepsRunning(){
            Assert.IsTrue(editor.ConfigureWorldTime(editor.WorldTimeRevision,new(){running=true},out var error),error);
            Assert.IsTrue(editor.CreatePrimitive(RoomObjectKind.Ball,"Independent ball",new Vector3(0,1.8f,1),1,Color.white,out var moving,out error),error);physics.SetSurfaces(true,"Ready");physics.StartPhysics();
            using var pause=new PublicationPause();Assert.IsTrue(editor.BeginWorldTime(editor.WorldTimeRevision,new(){running=true,rate=2},0,0,false,out var pending,out error,pause.Check),error);
            try {
                yield return Prepared(pending);editor.TickWorldTime(.2);editor.TickWorldTime(.2);double before=editor.WorldSecond;
                Assert.IsTrue(pending.Poll(out var ready,out error),error);Assert.IsFalse(ready);Assert.IsTrue(pending.Accepted);Assert.IsFalse(pending.Complete(out _));
                var receipt=pending.AcceptedWorldTimeObservation;Assert.AreEqual(before,(double)receipt["second"]);float y=editor.Find(moving).transform.position.y;
                for(int i=0;i<4;i++)yield return new WaitForFixedUpdate();
                Assert.IsTrue(pause.Entered.IsSet);Assert.Less(editor.Find(moving).transform.position.y,y);Assert.Greater(editor.WorldSecond,before);Assert.AreEqual(receipt,pending.AcceptedWorldTimeObservation);
            }finally{pause.Release();}
            yield return Completed(pending);var disk=new RoomStorage(directory).Load(out error);Assert.AreEqual((double)pending.AcceptedWorldTimeObservation["second"],disk.worldTime.second,error);Assert.AreEqual(2,disk.worldTime.settings.rate);Assert.IsTrue(editor.HasUnsavedChanges);
            double now=editor.WorldSecond;editor.Undo();Assert.AreEqual(now,editor.WorldSecond);Assert.AreEqual(1,editor.WorldTime.settings.rate);editor.Redo();Assert.AreEqual(now,editor.WorldSecond);Assert.AreEqual(2,editor.WorldTime.settings.rate);
        }
        [UnityTest] public IEnumerator PendingTemporalSeekCapturesAcceptanceOriginAndStopsOnlyObservationAfterAcceptance(){
            Assert.IsTrue(editor.ConfigureWorldTime(editor.WorldTimeRevision,new(){running=true},out var error),error);using var pause=new PublicationPause();
            Assert.IsTrue(editor.BeginWorldTime(editor.WorldTimeRevision,null,4,600,true,out var pending,out error,pause.Check),error);double origin;
            try{yield return Prepared(pending);editor.TickWorldTime(.2);editor.TickWorldTime(.2);origin=editor.WorldSecond;Assert.IsTrue(pending.Poll(out _,out error),error);Assert.IsTrue(pending.Accepted);pending.Dispose();Assert.IsFalse(pending.Complete(out _));Assert.IsFalse(editor.WriteGate.CanFreeze(out _));}
            finally{pause.Release();}
            yield return Drained(pending);Assert.IsTrue(pending.Published);Assert.AreEqual(4,new RoomStorage(directory).Load(out error).worldTime.day,error);editor.Undo();Assert.AreEqual(0,editor.WorldTime.day);Assert.AreEqual(origin,editor.WorldSecond);
        }
        [UnityTest] public IEnumerator PendingTemporalWeatherSamplesAcceptanceAndKeepsItsReceiptAfterLaterEdits(){
            Assert.IsTrue(editor.ConfigureWorldTime(editor.WorldTimeRevision,new(){running=true},out var error),error);
            Assert.IsTrue(editor.SetWeather(editor.WeatherRevision,1,new(){rainMmPerHour=100},10,out error),error);
            using var pause=new PublicationPause();Assert.IsTrue(editor.BeginWeather(editor.WeatherRevision,2,new(){rainMmPerHour=20},5,out var pending,out error,pause.Check),error);
            try{yield return Prepared(pending);editor.TickWorldTime(.5);editor.TickWorldTime(.5);double now=editor.WorldSecond;float rain=editor.CurrentWeather.Rain;Assert.IsTrue(pending.Poll(out _,out error),error);Assert.AreEqual(now,editor.Weather.startSecond);Assert.AreEqual(rain,editor.Weather.from.rainMmPerHour,.0001);editor.TickWorldTime(.5);}
            finally{pause.Release();}
            yield return Completed(pending);var receipt=pending.AcceptedWeatherObservation;var disk=new RoomStorage(directory).Load(out error);Assert.AreEqual((double)receipt["transition"]["startSecond"],disk.weather.startSecond,error);Assert.AreEqual(0,(double)receipt["transition"]["progress"]);Assert.AreEqual(2,disk.weather.seed);
            Assert.IsTrue(editor.SetWeather(editor.WeatherRevision,3,new(){rainMmPerHour=30},0,out error),error);Assert.AreEqual(receipt,pending.AcceptedWeatherObservation);pending.AcceptedWeatherObservation["seed"]=99;Assert.AreEqual(2,(int)pending.AcceptedWeatherObservation["seed"]);
        }
        [UnityTest] public IEnumerator PendingTemporalQueueCapturesArgumentsAndRejectsAStaleWaitingClock(){
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation weather=null,time=null;
            try{yield return Prepared(blocker);var c=new CapabilityContext(editor,animations);var w=new WorldWeatherCapability();var args=w.Example;args["revision"]=editor.WeatherRevision;
                Assert.AreEqual("completion",w.Duration);Assert.IsTrue(w.Start(c,"weather",args,out weather,out error),error);args["settings"]["rainMmPerHour"]=99;
                var t=new WorldTimeCapability();args=t.Example;args["revision"]=editor.WorldTimeRevision;Assert.IsTrue(t.Start(c,"clock",args,out time,out error),error);
                Assert.IsTrue(editor.ConfigureWorldTime(editor.WorldTimeRevision,new(){rate=3},out error),error);blocker.Dispose();yield return Drained(blocker);yield return PlacementOperationDone(weather);Assert.AreEqual(8,editor.Weather.settings.rainMmPerHour);
                Assert.AreEqual(RuleActionState.Failed,time.State(out error));Assert.IsEmpty(time.Result);Assert.AreEqual(3,editor.WorldTime.settings.rate);
            }finally{blocker.Dispose();weather?.Stop(false);time?.Stop(false);}
        }
        [UnityTest] public IEnumerator PendingTemporalCancellationLeavesClockAndWeatherUnchanged(){
            foreach(bool weather in new[]{false,true}){
                var time=editor.WorldTime;var previous=editor.Weather;RoomEditor.PendingPersistedEdit pending;string error;
                Assert.IsTrue(weather?editor.BeginWeather(editor.WeatherRevision,2,new(){rainMmPerHour=20},0,out pending,out error):editor.BeginWorldTime(editor.WorldTimeRevision,null,2,500,true,out pending,out error),error);
                yield return Prepared(pending);pending.Dispose();yield return Drained(pending);Assert.IsFalse(pending.Accepted);Assert.IsTrue(time.Same(editor.WorldTime));Assert.IsTrue(previous.Same(editor.Weather));
            }
        }
        [UnityTest] public IEnumerator PendingTemporalFailedSavePreservesDiskAndReportsAcceptedButUnconfirmed(){
            Assert.IsTrue(editor.TryFlush(out var error),error);string file=Path.Combine(directory,RoomStorage.FileName),before=File.ReadAllText(file);
            using(var held=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read)){
                Assert.IsTrue(editor.BeginWorldTime(editor.WorldTimeRevision,null,4,100,true,out var pending,out error),error);
                using(pending){yield return Rejected(pending);yield return Drained(pending);Assert.IsTrue(pending.Accepted);Assert.IsFalse(pending.Published);Assert.AreEqual(4,editor.WorldTime.day);StringAssert.Contains("unconfirmed",pending.InterruptionStatus);Assert.IsFalse(editor.CanSaveRoom);}
                Assert.AreEqual(before,File.ReadAllText(file));
            }
        }
        [UnityTest] public IEnumerator PendingTemporalWeatherSealsRainBeforeAdmissionAndReleasesFlowOnCancel(){
            string id=RainCup();editor.Liquids.Tick(.1f);double amount=(double)editor.Liquids.ObserveRain(id)["collectedMl"];Assert.Greater(amount,0);int events=0;
            editor.ContainerRainCollected+=(target,value)=>{events++;if(events==1){editor.Liquids.Tick(.1f);Assert.IsFalse(editor.Liquids.Active,"A publication callback must not reopen flow across the weather boundary");}};
            Assert.IsTrue(editor.BeginWeather(editor.WeatherRevision,2,new(){rainMmPerHour=0},0,out var pending,out var error),error);
            yield return LiquidSaved();Assert.AreEqual(1,events);Assert.AreEqual(amount,editor.Read(id).containers[0].amountMl,.000001);Assert.IsFalse(editor.Liquids.FlowWorkEligible);Assert.IsFalse(editor.Liquids.Active);
            pending.Dispose();yield return Drained(pending);Assert.IsTrue(editor.Liquids.FlowWorkEligible);Assert.AreEqual(60,editor.Weather.settings.rainMmPerHour);
            editor.Liquids.Tick(.1f);Assert.Greater((double)editor.Liquids.Observe(id)["contents"]["amountMl"],amount);physics.PausePhysics();
        }
        [UnityTest] public IEnumerator PendingTemporalTemporaryKeepAndDiscardHaveMatchingReceipts(){
            foreach(bool keep in new[]{false,true}){
                Assert.IsTrue(editor.BeginTemporaryRoom(out var error),error);yield return PendingGroupTemporaryDone();var module=new WorldTimeCapability(true);var args=module.Example;args["revision"]=editor.WorldTimeRevision;args["day"]=8;
                Assert.IsTrue(module.Start(new CapabilityContext(editor,animations),"temporary-time",args,out var action,out error),error);
                try{yield return PlacementOperationDone(action);Assert.IsTrue((bool)action.Result["temporary"]);Assert.AreEqual(8,(int)action.Result["day"]);}finally{action.Stop(false);}
                Assert.AreNotEqual(8,new RoomStorage(directory).Load(out error).worldTime.day);
                Assert.IsTrue(keep?editor.KeepTemporaryRoom(out error):editor.DiscardTemporaryRoom(out error),error);yield return PendingGroupTemporaryDone();Assert.AreEqual(keep?8:0,new RoomStorage(directory).Load(out error).worldTime.day);
            }
        }
    }
}
