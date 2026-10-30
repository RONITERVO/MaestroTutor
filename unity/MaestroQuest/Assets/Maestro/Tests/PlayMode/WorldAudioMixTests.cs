// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Book;
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
        JObject MixArguments(float master=1,float effects=1,float media=1,float ambience=1)=>new() {
            ["operation"]="set",["stateId"]=editor.ObserveAudioMix()["stateId"],
            ["master"]=master,["effects"]=effects,["media"]=media,["ambience"]=ambience};
        void SetMix(float master=1,float effects=1,float media=1,float ambience=1)
        {
            var module=new AudioMixCapability();var args=MixArguments(master,effects,media,ambience);
            Assert.IsTrue(BehaviourCatalog.TryCall(module.Id,1,args,out _,out var error),error);
            Assert.IsTrue(module.Start(new CapabilityContext(editor,animations),"mix",args,out var operation,out error),error);
            Assert.IsTrue(JToken.DeepEquals(editor.ObserveAudioMix(),operation.Result));
        }
        void MixEmitter(string target,string source,string role,float gain=.8f)
        {
            Assert.IsTrue(editor.EditAudioEmitter(target,editor.ObjectRevision(target),"sound",new RoomAudioEmitter{source=source,role=role,spatial=false,gain=gain},out var error),error);
        }
        [Test] public void WorldAudioMixReadsAreDetachedPassiveAndControlsAreGuarded()
        {
            var before=editor.ObserveAudioMix();int revision=editor.Revision;
            Assert.IsTrue(AudioMixCapability.Fact().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,null,out _));
            Assert.IsNull(editor.GetComponent<WorldAudio>());Assert.AreEqual(revision,editor.Revision);
            before["master"]=0;Assert.AreEqual(1,(float)editor.ObserveAudioMix()["master"]);
            string id=(string)before["stateId"];SetMix(.5f,.2f,.3f,.4f);
            Assert.IsFalse(editor.ControlAudioMix(id,1,1,1,1,out _));
            var current=editor.ObserveAudioMix();SetMix(.5f,.2f,.3f,.4f);Assert.IsTrue(JToken.DeepEquals(current,editor.ObserveAudioMix()),"Same values are idempotent");
            foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,-.1f,1.1f}) {
                Assert.IsFalse(editor.ControlAudioMix((string)current["stateId"],invalid,1,1,1,out _));
                Assert.IsFalse(editor.ControlAudioMix((string)current["stateId"],1,invalid,1,1,out _));
                Assert.IsFalse(editor.ControlAudioMix((string)current["stateId"],1,1,invalid,1,out _));
                Assert.IsFalse(editor.ControlAudioMix((string)current["stateId"],1,1,1,invalid,out _));
                Assert.IsTrue(JToken.DeepEquals(current,editor.ObserveAudioMix()));
            }
            Assert.IsNull(editor.GetComponent<WorldAudio>(),"Controls without sounds cannot open playback");Assert.AreEqual(revision,editor.Revision);
            var extra=MixArguments();extra["speech"]=0;Assert.IsFalse(BehaviourCatalog.TryCall("audio.mix.control",1,extra,out _,out _));
        }
        [Test] public void WorldAudioMixSchedulerReplayDoesNotSaveOrApplyTheChangeTwice()
        {
            Assert.IsTrue(editor.TryFlush(out var error),error);var file=Path.Combine(directory,RoomStorage.FileName);var bytes=File.ReadAllBytes(file);int revision=editor.Revision;
            var args=MixArguments(.7f,.8f,.4f,.1f);var request=ObjectEditRequest(new JObject{["id"]="audio.mix.control",["version"]=1,["arguments"]=args});request.conditions=Array.Empty<RoomObjectCondition>();var executor=new RoomAgentExecutor(editor);
            using(var locked=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                Assert.IsTrue(executor.Execute(request,out error,out _),error);var state=editor.ObserveAudioMix();Assert.AreEqual(.1f,(float)state["ambience"]);
                Assert.IsTrue(executor.Execute(request,out error,out _),error);Assert.IsTrue(JToken.DeepEquals(state,editor.ObserveAudioMix()));
                Assert.AreEqual(revision,editor.Revision);Assert.AreEqual(bytes,File.ReadAllBytes(file));
                var reset=new JObject{["operation"]="reset",["stateId"]=state["stateId"]};
                var module=new AudioMixCapability();Assert.IsTrue(BehaviourCatalog.TryCall(module.Id,1,reset,out _,out error),error);
                Assert.IsTrue(module.Start(new CapabilityContext(editor,animations),"reset-mix",reset,out _,out error),error);
                Assert.AreEqual(1,(float)editor.ObserveAudioMix()["master"]);Assert.AreEqual(1,(float)editor.ObserveAudioMix()["ambience"]);
            }
        }
        [UnityTest] public IEnumerator WorldAudioMixResetBoundariesInvalidateEvenDefaultValuesAndCancelOldVoices()
        {
            string source=WorldSound(5);WorldEmitter("book",source);var audio=WorldAudio.For(editor);
            Assert.IsTrue(audio.Begin("book","sound",out var voice,out var error),error);SetMix(.5f,.5f,.5f,.5f);
            string previous=(string)editor.ObserveAudioMix()["stateId"];
            Assert.IsTrue(editor.BeginTemporaryRoom(out error),error);Assert.IsTrue(voice.Closed,"Boundary must cancel before resetting gain on old audio");
            while(editor.TemporarySavePending)yield return null;
            Assert.AreEqual(1,editor.AudioMixGain("effects"));Assert.IsFalse(editor.CanControlAudioMix(previous,out _));
            previous=(string)editor.ObserveAudioMix()["stateId"];Assert.IsTrue(editor.DiscardTemporaryRoom(out error),error);
            Assert.IsFalse(editor.CanControlAudioMix(previous,out _),"Default values still belong to a specific room");
            SetMix(.3f);previous=(string)editor.ObserveAudioMix()["stateId"];editor.enabled=false;editor.enabled=true;
            Assert.AreEqual(1,editor.AudioMixGain("effects"));Assert.IsFalse(editor.CanControlAudioMix(previous,out _));
        }
        [UnityTest] public IEnumerator WorldAudioMixPreservesSourceAndEmitterEditsAndFocusRecovery()
        {
            string source=WorldSound(5);WorldEmitter("book",source);SetMix(.6f,.5f,.4f,.3f);var state=editor.ObserveAudioMix();
            var changed=editor.ReadAudio(source);changed.frequency=700;Assert.IsTrue(editor.EditAudio(source,editor.AudioRevision(source),changed,out var error),error);
            MixEmitter("book",source,"media");Assert.IsTrue(JToken.DeepEquals(state,editor.ObserveAudioMix()));
            var audio=WorldAudio.For(editor);Assert.IsTrue(audio.Begin("book","sound",out var voice,out error),error);
            audio.SendMessage("OnApplicationFocus",false);Assert.IsTrue(voice.Closed);Assert.IsTrue(JToken.DeepEquals(state,editor.ObserveAudioMix()));
            audio.SendMessage("OnApplicationFocus",true);Assert.IsTrue(audio.Begin("book","sound",out voice,out error),error);audio.Cancel(voice);
            audio.enabled=false;audio.enabled=true;Assert.IsTrue(JToken.DeepEquals(state,editor.ObserveAudioMix()),"Renderer owner recreation does not reset workspace levels");yield return null;
        }
        [UnityTest] public IEnumerator WorldAudioMixUsesRolesForPendingActiveAndPausedVoicesWithoutChangingTheirIdentities()
        {
            var listener=new GameObject("Mix listener",typeof(AudioListener));listener.AddComponent<SpeechListenerProbe>();
            var listeners=UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);var enabled=listeners.Select(x=>x.enabled).ToArray();for(int i=0;i<listeners.Length;i++)listeners[i].enabled=listeners[i].gameObject==listener;
            bool pause=AudioListener.pause;AudioListener.pause=false;
            try {
                string source=WorldSound(.4f),target=editor.Identity(block);
                MixEmitter("book",source,"effects");MixEmitter("maestro",source,"media");MixEmitter(target,source,"ambience");
                var audio=WorldAudio.For(editor);Assert.IsTrue(audio.Begin("book","sound",out var effect,out var error,true),error);
                SetMix(.5f,.25f,.5f,0);Assert.IsNull(effect.Renderer,"Preparation has not opened the output");
                Assert.IsTrue(audio.Begin("maestro","sound",out var media,out error,true),error);Assert.IsTrue(audio.Begin(target,"sound",out var ambience,out error,true),error);
                double deadline=Time.realtimeSinceStartupAsDouble+4;while((effect.Cursor<=0||media.Cursor<=0||ambience.Cursor<=0)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                foreach(var voice in new[]{effect,media,ambience})Assert.IsTrue(audio.Handoff(voice,out error),error);
                Assert.AreEqual(.1f,effect.Output.volume,.0001);Assert.AreEqual(.2f,media.Output.volume,.0001);Assert.AreEqual(0,ambience.Output.volume);
                Assert.IsTrue(audio.Control("book",effect.Id,effect.Revision,"pause",0,out _,out error),error);yield return new WaitForSecondsRealtime(.15f);
                double cursor=effect.Cursor;int revision=effect.Revision;var renderer=effect.Renderer;var stream=effect.Stream;
                using(var separate=PcmOutputLease.Open(root.transform,"Independent conversation output",new SpeechPcmStream(AudioSettings.dspTime+.1,.05),false,.9f,0,1,15)) {
                    SetMix(.25f,.5f,.75f,1);yield return new WaitForSecondsRealtime(.2f);
                    Assert.AreEqual(.1f,effect.Output.volume,.0001);Assert.AreEqual(.15f,media.Output.volume,.0001);Assert.AreEqual(.2f,ambience.Output.volume,.0001);
                    Assert.AreEqual(.9f,separate.Source.volume,.0001,"World mix cannot rewrite separate conversation output");
                }
                Assert.AreEqual(cursor,effect.Cursor,.001);Assert.AreEqual(revision,effect.Revision);Assert.AreSame(renderer,effect.Renderer);Assert.AreSame(stream,effect.Stream);Assert.AreEqual("paused",effect.Phase);
                Assert.IsTrue(audio.Control("book",effect.Id,effect.Revision,"gain",.4f,out var result,out error),error);Assert.AreEqual(.4f,result.Gain);Assert.AreEqual(.05f,effect.Output.volume,.0001);Assert.AreEqual(.8f,editor.Read("book").audioEmitters.Single().gain);
                Assert.IsTrue(audio.Control("book",effect.Id,effect.Revision,"resume",0,out _,out error),error);yield return new WaitForSecondsRealtime(.2f);Assert.Greater(effect.Cursor,cursor);
                // A lost output must not prevent the other voices from receiving a mix change.
                UnityEngine.Object.DestroyImmediate(media.Output.GetComponent<SpeechPcmFilter>());UnityEngine.Object.DestroyImmediate(media.Output);SetMix(.5f,1,1,1);
                Assert.IsTrue(media.Closed);Assert.AreEqual("failed",media.Phase);Assert.AreEqual(.2f,effect.Output.volume,.0001);Assert.AreEqual(.4f,ambience.Output.volume,.0001);
                audio.Cancel(effect);audio.Cancel(ambience);
                SetMix(0);Assert.IsTrue(audio.Begin("book","sound",out var silent,out error),error);
                deadline=Time.realtimeSinceStartupAsDouble+4;while(!silent.Closed&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                Assert.IsTrue(silent.Complete,silent.Error);Assert.AreEqual(.4,silent.Cursor,.0001,"Muted finite audio still consumes and drains normally");
            } finally {
                var audio=editor.GetComponent<WorldAudio>();if(audio)audio.enabled=false;UnityEngine.Object.Destroy(listener);
                for(int i=0;i<listeners.Length;i++)if(listeners[i])listeners[i].enabled=enabled[i];AudioListener.pause=pause;
            }
            yield return null;
        }
    }
}
