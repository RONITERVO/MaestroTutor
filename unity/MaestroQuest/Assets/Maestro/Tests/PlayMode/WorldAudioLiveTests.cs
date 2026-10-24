// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        string LiveSound(){
            string id=Guid.NewGuid().ToString("N");
            Assert.IsTrue(editor.EditAudio(id,0,new RoomAudioDefinition{id=id,version=2,name="Radio",kind="stream",endpoint="https://radio.example.invalid/live.mp3"},out var error),error);return id;
        }
        static IEnumerator LiveUntil(Func<bool> predicate,float seconds=5){double end=Time.realtimeSinceStartupAsDouble+seconds;while(!predicate()&&Time.realtimeSinceStartupAsDouble<end)yield return null;Assert.IsTrue(predicate(),"Live audio did not reach its expected state");}
        [UnityTest] public IEnumerator WorldAudioLiveIndependentInstancesPauseCurrentFeedAndEndHonestly(){
            using var net=new LiveAudioTestNetwork();var audio=WorldAudio.For(editor);var listener=new GameObject("Live audio test listener",typeof(AudioListener));
            var listeners=UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);var enabled=listeners.Select(l=>l.enabled).ToArray();for(int i=0;i<listeners.Length;i++)listeners[i].enabled=listeners[i].gameObject==listener;
            float volume=AudioListener.volume;bool paused=AudioListener.pause;AudioListener.volume=1;AudioListener.pause=false;
            try{
                var source=LiveSound();WorldEmitter("book",source);WorldEmitter("maestro",source);WorldEmitter(editor.Identity(block),source);Assert.AreEqual(0,net.Requests);
                var context=new CapabilityContext(editor,animations);Assert.IsFalse(new AudioPlayCapability().CanRun(context,new JObject{["target"]="book",["emitter"]="sound"},out _));
                Assert.IsFalse(audio.Begin("book","sound",out _,out _,true));Assert.IsTrue(audio.Begin("book","sound",out var first,out var error),error);Assert.IsTrue(audio.Begin("maestro","sound",out var other,out error),error);
                Assert.IsFalse(audio.Begin(editor.Identity(block),"sound",out _,out _));Assert.AreEqual(2,net.Requests);
                yield return LiveUntil(()=>first.Cursor>0&&other.Cursor>0);Assert.IsTrue(audio.Handoff(first,out error),error);Assert.IsTrue(audio.Handoff(other,out error),error);
                Assert.LessOrEqual(first.Stream.Read(AudioSettings.dspTime).Submitted-first.Stream.Read(AudioSettings.dspTime).Played,2400);
                Assert.IsTrue(audio.Control("book",first.Id,first.Revision,"pause",0,out var receipt,out error),error);int pauseRevision=receipt.Revision;double cursor=first.Cursor;
                yield return new WaitForSecondsRealtime(2.5f);Assert.AreEqual("paused",first.Phase);Assert.Greater(other.Cursor,cursor);Assert.IsTrue(audio.Control("book",first.Id,first.Revision,"resume",0,out receipt,out error),error);
                Assert.IsFalse(audio.Control("book",first.Id,pauseRevision,"stop",0,out _,out _));yield return LiveUntil(()=>first.Cursor>cursor+.05);
                var live=audio.ObserveLiveInstance("book",first.Id);Assert.Greater((double)live["skippedSeconds"],1.5);Assert.AreEqual(2,net.Requests,"Resume must not reconnect");
                Assert.IsTrue(BehaviourCatalog.TryRead("audio.source.stream",1,new JObject{["id"]=source,["offset"]=0},new BehaviourCatalog.FactContext(editor:editor),out var fact));Assert.AreEqual("https://radio.example.invalid/live.mp3",(string)((JObject)fact.Value)["endpoint"]);
                Assert.IsTrue(BehaviourCatalog.TryRead("audio.live",1,new JObject{["target"]="book",["instance"]=first.Id},new BehaviourCatalog.FactContext(editor:editor),out _));
                net.Bodies[0].Starve=true;yield return LiveUntil(()=>first.Phase=="buffering");Assert.IsFalse(first.Complete);net.Bodies[0].Starve=false;yield return LiveUntil(()=>first.Phase=="playing");
                Assert.IsTrue(audio.Control("book",first.Id,first.Revision,"stop",0,out receipt,out error),error);Assert.AreEqual("stopped",receipt.Phase);Assert.AreEqual("closed",(string)audio.ObserveLiveInstance("book",first.Id)["sourcePhase"]);Assert.IsFalse(other.Closed);
                net.Bodies[1].End=true;yield return LiveUntil(()=>other.Closed);Assert.AreEqual("ended",other.Phase,other.Error);Assert.IsTrue(other.Complete);Assert.IsNull(other.Error);Assert.AreEqual("ended",(string)audio.ObserveLiveInstance("maestro",other.Id)["sourcePhase"]);
            }finally{audio.enabled=false;UnityEngine.Object.Destroy(listener);for(int i=0;i<listeners.Length;i++)if(listeners[i])listeners[i].enabled=enabled[i];AudioListener.volume=volume;AudioListener.pause=paused;}
            yield return LiveUntil(()=>LiveMp3Source.ActiveSources==0&&(int)AudioReservations.ObserveBudget()["sources"]==0);
        }
        [UnityTest] public IEnumerator WorldAudioLiveLateHeadersRetainBudgetUntilCancelledPreparationDrains(){
            using var net=new LiveAudioTestNetwork();var gates=new System.Collections.Generic.List<TaskCompletionSource<HttpResponseMessage>>();net.Headers=_=>{var gate=new TaskCompletionSource<HttpResponseMessage>();gates.Add(gate);return gate.Task;};
            var source=LiveSound();WorldEmitter("book",source);WorldEmitter("maestro",source);WorldEmitter(editor.Identity(block),source);var audio=WorldAudio.For(editor);
            try{
                Assert.IsTrue(audio.Begin("book","sound",out var first,out var error),error);Assert.IsTrue(audio.Begin("maestro","sound",out var other,out error),error);audio.Cancel(first);audio.Cancel(other);
                Assert.IsFalse(audio.Begin(editor.Identity(block),"sound",out _,out _),"Retiring connections retain their two slots");Assert.AreEqual(0,LiveMp3Source.ActiveSources);
                foreach(var gate in gates){var body=new LiveAudioTestNetwork.PacedStream();net.Bodies.Add(body);var response=new HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new StreamContent(body)};response.Content.Headers.ContentType=new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg");gate.SetResult(response);}
                yield return LiveUntil(()=>(int)AudioReservations.ObserveBudget()["sources"]==0);Assert.IsNull(audio.Current("book","sound"));Assert.AreEqual(0,LiveMp3Source.ActiveSources);Assert.AreEqual(2,net.Requests);Assert.IsTrue(net.Bodies.All(b=>b.Disposals==1),"Late successful responses must be disposed after cancellation");
            }finally{foreach(var gate in gates)gate.TrySetCanceled();audio.enabled=false;}
        }
        [UnityTest] public IEnumerator WorldAudioLiveDecodeFailureStopsOnlyItsInstanceAndPublishesFailure(){
            using var net=new LiveAudioTestNetwork();var source=LiveSound();WorldEmitter("book",source);var tone=WorldSound(1);WorldEmitter("maestro",tone);var audio=WorldAudio.For(editor);
            try{
                Assert.IsTrue(audio.Begin("book","sound",out var live,out var error),error);Assert.IsTrue(audio.Begin("maestro","sound",out var other,out error,loop:true),error);
                yield return LiveUntil(()=>live.Cursor>0&&other.Cursor>0);Assert.IsTrue(audio.Handoff(live,out error),error);int after=live.Revision;
                net.Bodies[0].Corrupt=true;yield return LiveUntil(()=>live.Closed);Assert.AreEqual("failed",live.Phase);Assert.IsFalse(live.Complete);Assert.IsNotEmpty(live.Error);Assert.IsFalse(other.Closed);
                Assert.IsTrue(audio.NextNotice("book",live.Id,after,out var notice,out error),error);while(notice.Phase!="failed")Assert.IsTrue(audio.NextNotice("book",live.Id,notice.Revision,out notice,out error),error);
                Assert.AreEqual("failed",(string)audio.ObserveLiveInstance("book",live.Id)["sourcePhase"]);double cursor=other.Cursor;yield return new WaitForSecondsRealtime(.15f);Assert.Greater(other.Cursor,cursor);Assert.AreEqual(1,net.Requests,"Failure must not reconnect");
            }finally{audio.enabled=false;}
            yield return LiveUntil(()=>LiveMp3Source.ActiveSources==0&&(int)AudioReservations.ObserveBudget()["sources"]==0);
        }
        [UnityTest] public IEnumerator WorldAudioLiveRoomBoundaryStopsConnectionsAndDoesNotRestorePlayback(){
            using var net=new LiveAudioTestNetwork();var source=LiveSound();WorldEmitter("book",source);var audio=WorldAudio.For(editor);Assert.IsTrue(audio.Begin("book","sound",out var voice,out var error),error);
            yield return LiveUntil(()=>LiveMp3Source.ActiveSources==1);Assert.IsTrue(editor.BeginTemporaryRoom(out error),error);while(editor.TemporarySavePending)yield return null;
            yield return LiveUntil(()=>voice.Closed&&LiveMp3Source.ActiveSources==0);Assert.IsTrue(editor.DiscardTemporaryRoom(out error),error);Assert.IsNull(audio.Current("book","sound"));Assert.AreEqual(source,editor.Read("book").audioEmitters.Single().source);Assert.AreEqual(1,net.Requests);audio.enabled=false;
        }
    }
}
#endif
