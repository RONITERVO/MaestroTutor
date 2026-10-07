// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
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
        string WorldSound(float seconds=.25f)
        {
            var module=new AudioSourceCapability();var args=module.Example;args["definition"]["tone"]["seconds"]=seconds;
            Assert.IsTrue(module.Start(new CapabilityContext(editor,animations),"sound-definition",args,out var op,out var error),error);return (string)op.Result["id"];
        }
        void WorldEmitter(string target,string source,string key="sound",bool spatial=false,float gain=0)
        {
            var module=new AudioEmitterCapability();var args=module.Example;args["target"]=target;args["revision"]=editor.ObjectRevision(target);args["emitter"]=key;
            args["definition"]=AudioSchema.Encode(new RoomAudioEmitter {source=source,spatial=spatial,gain=gain});Assert.IsTrue(module.Start(new CapabilityContext(editor,animations),"sound-emitter",args,out _,out var error),error);
        }
        [UnityTest] public IEnumerator WorldAudioDefinitionsUseSharedFactsUndoAndTemporaryDiscard()
        {
            string id=WorldSound();WorldEmitter("book",id);Assert.IsTrue(BehaviourCatalog.TryRead("audio.source.definition",1,new JObject {["id"]=id},new BehaviourCatalog.FactContext(editor:editor),out var fact));Assert.AreEqual(id,(string)((JObject)fact.Value)["id"]);
            Assert.IsTrue(BehaviourCatalog.TryRead("object.audioEmitter",1,new JObject {["target"]="book",["emitter"]="sound"},new BehaviourCatalog.FactContext(editor:editor),out fact));Assert.IsTrue((bool)((JObject)fact.Value)["configured"]);
            var definition=editor.ReadAudio(id);definition.name="Edited";int revision=editor.AudioRevision(id);Assert.IsTrue(editor.EditAudio(id,revision,definition,out var error),error);Assert.IsFalse(editor.EditAudio(id,revision,definition,out _));editor.Undo();Assert.AreEqual("Robot greeting",editor.ReadAudio(id).name);
            Assert.IsTrue(editor.BeginTemporaryRoom(out error),error);while(editor.TemporarySavePending)yield return null;string temporary=WorldSound();Assert.AreEqual(2,editor.AudioSources().Length);Assert.IsTrue(editor.DiscardTemporaryRoom(out error),error);Assert.IsNull(editor.ReadAudio(temporary));Assert.AreEqual(id,editor.Read("book").audioEmitters.Single().source);
            Assert.IsFalse(editor.EditAudio(id,editor.AudioRevision(id),null,out _),"Referenced sounds cannot be removed");
        }
        [UnityTest] public IEnumerator WorldAudioNativePlaybackHasOwnedReceiptsAndFollowsMovedObjects()
        {
            var listener=new GameObject("Muted world audio listener",typeof(AudioListener));var all=UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);var enabled=all.Select(x=>x.enabled).ToArray();for(int i=0;i<all.Length;i++)all[i].enabled=all[i].gameObject==listener;
            float volume=AudioListener.volume;bool pause=AudioListener.pause;AudioListener.volume=1;AudioListener.pause=false;var probe=listener.AddComponent<SpeechListenerProbe>();
            try {
                string id=WorldSound(1.1f),target=editor.Identity(block);WorldEmitter(target,id,gain:.25f);WorldEmitter("book",id,gain:.25f);
                var audio=WorldAudio.For(editor);Assert.IsTrue(audio.Begin(target,"sound",out var first,out var error),error);Assert.IsTrue(audio.Begin("book","sound",out var second,out error),error);
                Assert.IsFalse(audio.Begin(target,"sound",out _,out _),"A second instance cannot silently replace the first");
                double preparation=Time.realtimeSinceStartupAsDouble+3;while((!first.Output||!second.Output)&&!first.Closed&&!second.Closed&&Time.realtimeSinceStartupAsDouble<preparation)yield return null;Assert.IsNotNull(first.Output,first.Error);Assert.IsNotNull(second.Output,second.Error);
                block.transform.localPosition+=Vector3.right*.3f;audio.Tick(first);Assert.Less(Vector3.Distance(first.Output.transform.position,block.transform.position),.001f);
                yield return new WaitForSecondsRealtime(.3f);Assert.Greater(first.Cursor,0);var rendered=probe.Read();Assert.Greater(rendered.Left+rendered.Right,.00001,"The native PCM mix did not render");
                audio.Close(first);Assert.IsTrue(first.Closed);Assert.IsFalse(first.Complete);Assert.IsFalse(second.Closed);
                var changed=editor.ReadAudio(id);changed.frequency=700;Assert.IsTrue(editor.EditAudio(id,editor.AudioRevision(id),changed,out error),error);Assert.Less(second.SourceRevision,editor.AudioRevision(id),"In-flight audio must retain its original source revision");
                double deadline=Time.realtimeSinceStartupAsDouble+4;while(!second.Closed&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                Assert.IsTrue(second.Complete,second.Error);Assert.AreEqual(1.1,second.Cursor,.0001);Assert.IsNull(second.Error);
                var module=new AudioPlayCapability();Assert.IsTrue(module.Start(new CapabilityContext(editor,animations),"play-test",new JObject {["target"]="book",["emitter"]="sound"},out var operation,out error),error);Assert.AreEqual(RuleActionState.Preparing,operation.State(out error));Assert.IsTrue(operation.WaitsThroughGrab("book"));operation.Stop(false);Assert.AreEqual(RuleActionState.Failed,operation.State(out error));
            } finally {
                var audio=editor.GetComponent<WorldAudio>();if(audio)audio.enabled=false;UnityEngine.Object.Destroy(listener);for(int i=0;i<all.Length;i++)if(all[i])all[i].enabled=enabled[i];AudioListener.volume=volume;AudioListener.pause=pause;
            }
            yield return null;
        }
        [UnityTest] public IEnumerator WorldAudioDeletionAndRoomBoundaryCancelWithoutReplay()
        {
            string id=WorldSound(5),target=editor.Identity(block);WorldEmitter(target,id);WorldEmitter("book",id);var audio=WorldAudio.For(editor);
            Assert.IsTrue(audio.Begin(target,"sound",out var voice,out var error),error);Assert.IsTrue(editor.DeleteObject(target,out error),error);audio.Tick(voice);Assert.IsTrue(voice.Closed);Assert.IsFalse(voice.Complete);
            Assert.IsTrue(audio.Begin("book","sound",out voice,out error),error);Assert.IsTrue(editor.BeginTemporaryRoom(out error),error);while(editor.TemporarySavePending)yield return null;audio.Tick(voice);Assert.IsTrue(voice.Closed);Assert.IsFalse(voice.Complete);
            Assert.IsTrue(editor.DiscardTemporaryRoom(out error),error);Assert.IsNull(audio.Current("book","sound"));audio.enabled=false;yield return null;
        }
    }
}
