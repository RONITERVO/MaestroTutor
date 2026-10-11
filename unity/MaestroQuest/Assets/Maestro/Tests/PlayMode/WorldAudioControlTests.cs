// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
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
        IEnumerator IndependentSound(RoomRuleActions actions,string target,bool loop,Action<AudioInstanceSample> result)
        {
            string run=Guid.NewGuid().ToString("N");var module=new AudioStartCapability();var args=module.Example;args["target"]=target;args["emitter"]="sound";args["loop"]=loop;
            Assert.IsTrue(BehaviourCatalog.TryCall(module.Id,1,args,out var call,out var error),error);
            Assert.IsTrue(actions.Start(run,call,out _,out error),error);
            double deadline=Time.realtimeSinceStartupAsDouble+4;
            while(actions.State(run,out error)==RuleActionState.Preparing&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.AreEqual(RuleActionState.Ready,actions.State(run,out error),error);Assert.IsTrue(actions.Complete(run,out error),error);
            var receipt=actions.TakeResult(run);string id=(string)receipt["identity"]["instance"];
            Assert.IsTrue(actions.TryAudioInstance(target,id,out var sample));Assert.AreEqual("room",sample.Lifetime);
            actions.Stop(run,false);Assert.IsTrue(actions.TryAudioInstance(target,id,out var after));Assert.AreEqual(sample.Phase,after.Phase,"Completing the start must not cancel handed-off sound");result(after);
        }
        [UnityTest] public IEnumerator WorldAudioLoopPauseResumeAndGainUseExactOwnedInstances()
        {
            var listener=new GameObject("Independent sound listener",typeof(AudioListener));listener.AddComponent<SpeechListenerProbe>();var listeners=UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);var enabled=listeners.Select(x=>x.enabled).ToArray();for(int i=0;i<listeners.Length;i++)listeners[i].enabled=listeners[i].gameObject==listener;
            bool pause=AudioListener.pause;AudioListener.pause=false;
            try{
                string source=WorldSound(.15f);WorldEmitter("book",source);WorldEmitter("maestro",source);
                var actions=new RoomRuleActions(editor,animations);AudioInstanceSample first=default,second=default;
                yield return IndependentSound(actions,"book",true,s=>first=s);yield return IndependentSound(actions,"maestro",true,s=>second=s);
                var audio=WorldAudio.For(editor);yield return new WaitForSecondsRealtime(.4f);Assert.IsTrue(audio.ReadInstance("book",first.Instance,out first));Assert.Greater(first.Seconds,.15,"Loop must consume beyond one source duration");
                int observed=first.Revision;Assert.IsTrue(audio.Control("book",first.Instance,observed,"pause",0,out first,out var error),error);Assert.AreEqual("paused",first.Phase);
                Assert.IsFalse(audio.Control("book",first.Instance,observed,"resume",0,out _,out _),"Stale controls must not win");
                yield return new WaitForSecondsRealtime(.2f);audio.ReadInstance("book",first.Instance,out first);double stoppedAt=first.Seconds;
                yield return new WaitForSecondsRealtime(3.2f);audio.ReadInstance("book",first.Instance,out first);Assert.AreEqual(stoppedAt,first.Seconds,.001,"Pause must not consume buffered PCM");Assert.AreEqual("paused",first.Phase);
                Assert.IsTrue(audio.ReadInstance("maestro",second.Instance,out second));Assert.Greater(second.Seconds,3,"Pausing one sound must leave the other running");
                Assert.IsTrue(audio.Control("book",first.Instance,first.Revision,"gain",.2f,out first,out error),error);Assert.AreEqual(.2f,first.Gain);Assert.AreEqual(0,editor.Read("book").audioEmitters.Single().gain,"Instance gain does not edit the saved emitter");
                Assert.IsTrue(audio.Control("book",first.Instance,first.Revision,"resume",0,out first,out error),error);yield return new WaitForSecondsRealtime(.2f);audio.ReadInstance("book",first.Instance,out first);Assert.Greater(first.Seconds,stoppedAt);Assert.AreEqual("playing",first.Phase);
                Assert.IsTrue(audio.Control("book",first.Instance,first.Revision,"stop",0,out first,out error),error);Assert.AreEqual("stopped",first.Phase);Assert.IsNull(audio.Current("book","sound"));Assert.IsNotNull(audio.Current("maestro","sound"));
                Assert.IsTrue(audio.Control("book",first.Instance,first.Revision,"stop",0,out var again,out error),error);Assert.AreEqual(first.Revision,again.Revision);
                Assert.IsFalse(audio.Control("book",first.Instance,first.Revision,"resume",0,out _,out _));
            }finally{var audio=editor.GetComponent<WorldAudio>();if(audio)audio.enabled=false;UnityEngine.Object.Destroy(listener);for(int i=0;i<listeners.Length;i++)if(listeners[i])listeners[i].enabled=enabled[i];AudioListener.pause=pause;}
            yield return null;
        }
        [UnityTest] public IEnumerator WorldAudioLifecycleEventsRetainOrderAndReportMissedHistory()
        {
            var listener=new GameObject("Audio event listener",typeof(AudioListener));listener.AddComponent<SpeechListenerProbe>();var listeners=UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);var enabled=listeners.Select(x=>x.enabled).ToArray();for(int i=0;i<listeners.Length;i++)listeners[i].enabled=listeners[i].gameObject==listener;
            bool pause=AudioListener.pause;AudioListener.pause=false;
            try{
                string source=WorldSound(.2f);WorldEmitter("book",source);var actions=new RoomRuleActions(editor,animations);AudioInstanceSample sample=default;
                yield return IndependentSound(actions,"book",true,s=>sample=s);var audio=WorldAudio.For(editor);
                var args=new JObject {["target"]="book",["instance"]=sample.Instance,["after"]=sample.Revision};using var watch=new AudioInstanceSubscription(actions,args);
                Assert.IsTrue(audio.Control("book",sample.Instance,sample.Revision,"pause",0,out sample,out var error),error);
                Assert.IsTrue(audio.Control("book",sample.Instance,sample.Revision,"resume",0,out sample,out error),error);
                Assert.IsTrue(watch.Poll(0,out var value,out var fields,out error),error);Assert.AreEqual("book",value.Text);Assert.AreEqual("paused",(string)fields["phase"]);Assert.IsTrue(BehaviourCatalog.Event("audio.instance.changed").ValidFields(fields));
                Assert.IsTrue(watch.Poll(0,out _,out fields,out error),error);Assert.AreEqual("playing",(string)fields["phase"]);Assert.IsFalse(watch.Poll(0,out _,out _,out error));Assert.IsNull(error);
                int prior=sample.Revision;for(int i=0;i<WorldAudio.MaximumNotices+1;i++)Assert.IsTrue(audio.Control("book",sample.Instance,sample.Revision,"gain",i%2==0?.1f:.2f,out sample,out error),error);
                Assert.IsFalse(audio.NextNotice("book",sample.Instance,prior,out _,out error));StringAssert.Contains("retained event window",error);
                Assert.IsFalse(audio.Control("maestro",sample.Instance,sample.Revision,"stop",0,out _,out _),"An instance does not authorize another target");
                audio.enabled=false;Assert.IsTrue(audio.ReadInstance("book",sample.Instance,out sample));Assert.AreEqual("cancelled",sample.Phase);StringAssert.Contains("disabled",sample.Error);
                Assert.IsTrue(audio.NextNotice("book",sample.Instance,sample.Revision-1,out var terminal,out error),error);Assert.AreEqual("cancelled",terminal.Phase);
            }finally{var audio=editor.GetComponent<WorldAudio>();if(audio)audio.enabled=false;UnityEngine.Object.Destroy(listener);for(int i=0;i<listeners.Length;i++)if(listeners[i])listeners[i].enabled=enabled[i];AudioListener.pause=pause;}
            yield return null;
        }
        [UnityTest] public IEnumerator WorldAudioStartCancellationCannotHandOffOrRevivePendingSound()
        {
            string source=WorldSound();WorldEmitter("book",source);var module=new AudioStartCapability();var args=module.Example;args["target"]="book";args["emitter"]="sound";
            Assert.IsTrue(module.Start(new CapabilityContext(editor,animations),"pending-audio",args,out var operation,out var error),error);
            operation.Stop(false);yield return null;Assert.AreEqual(RuleActionState.Failed,operation.State(out error));Assert.IsFalse(operation.Complete(out _));
            Assert.IsNull(WorldAudio.For(editor).Current("book","sound"));WorldAudio.For(editor).enabled=false;
        }
    }
}
