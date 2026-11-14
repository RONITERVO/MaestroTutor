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
        JObject SoundArgs(string id="",bool remove=false)=>new() {
            ["operation"]=remove?"remove":"save",["id"]=id,["revision"]=editor.AudioRevision(id),
            ["definition"]=AudioSchema.Encode(string.IsNullOrEmpty(id)?new RoomAudioDefinition{name="Robot greeting"}:editor.ReadAudio(id))};
        JObject EmitterArgs(string source,string target="book")=>new() {
            ["operation"]="configure",["target"]=target,["revision"]=editor.ObjectRevision(target),["emitter"]="sound",
            ["definition"]=AudioSchema.Encode(new RoomAudioEmitter{source=source,spatial=false,gain=0})};
        [UnityTest] public IEnumerator PendingAudioQueueCapturesInputsAndKeepsWaitingSourcesSilent()
        {
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);
            CapabilityOperation first=null,second=null;
            try {
                yield return Prepared(blocker);var context=new CapabilityContext(editor,animations);var module=new AudioSourceCapability();
                Assert.AreEqual("completion",module.Duration);var args=module.Example;
                Assert.IsTrue(module.Start(context,"first",args,out first,out error),error);args["definition"]["name"]="Second source";
                Assert.IsTrue(module.Start(context,"second",args,out second,out error),error);
                Assert.IsEmpty(first.Result);Assert.IsEmpty(editor.AudioSources());Assert.IsNull(editor.GetComponent<WorldAudio>());
                blocker.Dispose();yield return Drained(blocker);
                Assert.AreEqual(RuleActionState.Preparing,second.State(out error),error);Assert.IsEmpty(editor.AudioSources());
                yield return PlacementOperationDone(first);yield return PlacementOperationDone(second);
                string id=(string)first.Result["id"],other=(string)second.Result["id"];Assert.AreNotEqual(id,other);
                Assert.AreEqual("Robot greeting",editor.ReadAudio(id).name);Assert.AreEqual("Second source",editor.ReadAudio(other).name);
                Assert.AreEqual(2,new RoomStorage(directory).Load(out error).audioSources.Length,error);
                int revision=(int)first.Result["revision"];first.Result["revision"]=-1;Assert.AreEqual(revision,(int)first.Result["revision"]);
                Assert.IsNull(editor.GetComponent<WorldAudio>());editor.Undo();Assert.IsNull(editor.ReadAudio(other));
            }finally{first?.Stop(false);second?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingAudioRevalidatesReferencesCapacityAndQueuedTargets()
        {
            string id=WorldSound();var context=new CapabilityContext(editor,animations);var source=new AudioSourceCapability();
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation operation=null;
            try {
                yield return Prepared(blocker);var args=SoundArgs(id,true);args.Remove("definition");
                Assert.IsTrue(source.Start(context,"remove",args,out operation,out error),error);
                WorldEmitter("book",id);blocker.Dispose();yield return Drained(blocker);
                Assert.AreEqual(RuleActionState.Failed,operation.State(out error));StringAssert.Contains("emitters",error);Assert.IsNotNull(editor.ReadAudio(id));
            }finally{operation?.Stop(false);blocker.Dispose();}
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out blocker,out error),error);operation=null;
            try {
                yield return Prepared(blocker);Assert.IsTrue(source.Start(context,"create",source.Example,out operation,out error),error);
                for(int i=editor.AudioSources().Length;i<RoomAudioDefinition.MaximumSources;i++)WorldSound();
                blocker.Dispose();yield return Drained(blocker);Assert.AreEqual(RuleActionState.Failed,operation.State(out error));StringAssert.Contains("maximum",error);
                Assert.AreEqual(RoomAudioDefinition.MaximumSources,editor.AudioSources().Length);Assert.IsEmpty(operation.Result);
            }finally{operation?.Stop(false);blocker.Dispose();}
            string target=editor.Identity(block);Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out blocker,out error),error);operation=null;
            try {
                yield return Prepared(blocker);Assert.IsTrue(new AudioEmitterCapability().Start(context,"attach",EmitterArgs(id,target),out operation,out error),error);
                Assert.IsTrue(editor.PaintObject(target,Color.red,out error),error);blocker.Dispose();yield return Drained(blocker);
                Assert.AreEqual(RuleActionState.Failed,operation.State(out error));StringAssert.Contains("target changed",error);Assert.IsNull(editor.ReadAudioEmitter(target,"sound"));
            }finally{operation?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingAudioEmitterCapturesInputsAndCancellationLeavesNoAttachment()
        {
            string id=WorldSound();var config=new RoomAudioEmitter{source=id,spatial=false,gain=0};
            Assert.IsTrue(editor.BeginAudioEmitter("book",editor.ObjectRevision("book"),"sound",config,out var cancelled,out var error),error);
            using(cancelled){yield return Prepared(cancelled);cancelled.Dispose();yield return Drained(cancelled);Assert.IsNull(editor.ReadAudioEmitter("book","sound"));}
            var args=EmitterArgs(id);Assert.IsTrue(new AudioEmitterCapability().Start(new CapabilityContext(editor,animations),"attach",args,out var operation,out error),error);
            args["definition"]["gain"]=1;args["definition"]["source"]=new string('0',32);
            try {yield return PlacementOperationDone(operation);Assert.IsEmpty(operation.Result);Assert.AreEqual(0,editor.ReadAudioEmitter("book","sound").gain);
                Assert.AreEqual(id,new RoomStorage(directory).Load(out error).objects.Single(x=>x.id=="book").audioEmitters.Single().source,error);
                editor.Undo();Assert.IsNull(editor.ReadAudioEmitter("book","sound"));
            }finally{operation.Stop(false);}
            Assert.IsNull(editor.GetComponent<WorldAudio>());
        }
        [UnityTest] public IEnumerator PendingAudioEmitterRefusesAHiddenOrNewlyAuthoredTargetBeforeAcceptance()
        {
            string id=WorldSound(),target=editor.Identity(block);
            foreach(bool authoring in new[]{false,true}) {
                var emitter=new RoomAudioEmitter{source=id,spatial=false,gain=0};
                Assert.IsTrue(editor.BeginAudioEmitter(target,editor.ObjectRevision(target),"sound",emitter,out var pending,out var error),error);
                using(pending) {
                    yield return Prepared(pending);
                    if(authoring){animations.StepFrame(1);Assert.IsTrue(animations.ControlsTarget(target),animations.Status);}
                    else block.gameObject.SetActive(false);
                    Assert.IsFalse(pending.Poll(out _,out error));Assert.IsNotEmpty(error);yield return Drained(pending);
                    Assert.IsNull(editor.ReadAudioEmitter(target,"sound"));
                    if(authoring)animations.Stop();else block.gameObject.SetActive(true);
                }
            }
        }
        [UnityTest] public IEnumerator PendingAudioSourceKeepsPlayingRevisionAndOnlyAcceptedEmitterChangeStopsItsVoice()
        {
            string id=WorldSound(5);WorldEmitter("book",id);WorldEmitter("maestro",id);var audio=WorldAudio.For(editor);
            Assert.IsTrue(audio.Begin("book","sound",out var voice,out var error,true),error);
            Assert.IsTrue(audio.Begin("maestro","sound",out var other,out error,true),error);
            try {
                int captured=voice.SourceRevision;var source=editor.ReadAudio(id);source.frequency=700;
                Assert.IsTrue(editor.BeginAudio(id,editor.AudioRevision(id),source,out var pending,out error),error);
                using(pending){yield return Completed(pending);}
                audio.Tick(voice);audio.Tick(other);Assert.IsFalse(voice.Closed,voice.Error);Assert.IsFalse(other.Closed,other.Error);
                Assert.AreEqual(captured,voice.SourceRevision);Assert.Greater(editor.AudioRevision(id),captured);
                var emitter=editor.ReadAudioEmitter("book","sound");emitter.position=Vector3.up*.02f;
                Assert.IsTrue(editor.BeginAudioEmitter("book",editor.ObjectRevision("book"),"sound",emitter,out pending,out error),error);
                using(pending){yield return Prepared(pending);audio.Tick(voice);Assert.IsFalse(voice.Closed,voice.Error);pending.Dispose();yield return Drained(pending);}
                audio.Tick(voice);Assert.IsFalse(voice.Closed,voice.Error);
                Assert.IsTrue(editor.BeginAudioEmitter("book",editor.ObjectRevision("book"),"sound",emitter,out pending,out error),error);
                using(pending){yield return Completed(pending);}
                audio.Tick(voice);audio.Tick(other);Assert.IsTrue(voice.Closed);Assert.IsFalse(voice.Complete);StringAssert.Contains("emitter changed",voice.Error);
                Assert.IsFalse(other.Closed,other.Error);Assert.AreEqual(captured,other.SourceRevision);
            }finally{audio.Cancel(voice);audio.Cancel(other);audio.enabled=false;}
        }
        [UnityTest] public IEnumerator PendingAudioReceiptIsAcceptedRevisionNotALaterHumanEdit()
        {
            var module=new AudioSourceCapability();Assert.IsTrue(module.Start(new CapabilityContext(editor,animations),"create",module.Example,out var operation,out var error),error);
            try {
                float end=Time.realtimeSinceStartup+20;
                while(operation.State(out error)==RuleActionState.Preparing){Assert.Less(Time.realtimeSinceStartup,end,error);yield return null;}
                Assert.AreEqual(RuleActionState.Ready,operation.State(out error),error);var source=editor.AudioSources().Single();int revision=editor.AudioRevision(source.id);
                source.name="Human edit";Assert.IsTrue(editor.EditAudio(source.id,revision,source,out error),error);
                Assert.IsTrue(operation.Complete(out error),error);Assert.AreEqual(revision,(int)operation.Result["revision"]);
                Assert.AreEqual("Human edit",editor.ReadAudio(source.id).name);Assert.AreNotEqual(revision,editor.AudioRevision(source.id));
            }finally{operation.Stop(false);}
        }
        [UnityTest] public IEnumerator PendingAudioAcceptedStopReportsSourceAndDrainsItsSave()
        {
            string id=Guid.NewGuid().ToString("N");var source=new RoomAudioDefinition{id=id};
            using var pause=new PublicationPause();RoomEditor.PendingPersistedEdit pending=null;
            Assert.IsTrue(PersistedEditOperation.Start(editor,null,
                (out RoomEditor.PendingPersistedEdit edit,out string issue)=>{bool ok=editor.BeginAudio(id,0,source,out edit,out issue,pause.Check);pending=edit;return ok;},
                out var operation,out var error,validate:(out string issue)=>editor.CanEditAudio(id,0,source,out issue),
                output:()=>new JObject{["id"]=id,["revision"]=pending.AcceptedAudioRevision(id)}),error);
            try {
                yield return Prepared(pending);Assert.AreEqual(RuleActionState.Preparing,operation.State(out error),error);Assert.IsTrue(pending.Accepted);
                operation.Stop(false);Assert.IsEmpty(operation.Result);StringAssert.Contains(id,operation.InterruptionStatus);StringAssert.Contains("Saving is still pending",operation.InterruptionStatus);
                Assert.IsFalse(operation.Complete(out _));Assert.IsNotNull(editor.ReadAudio(id));
            }finally{pause.Release();operation.Stop(false);}
            yield return Drained(pending);Assert.IsTrue(pending.Published);Assert.AreEqual(id,new RoomStorage(directory).Load(out error).audioSources.Single().id,error);
            editor.Undo();Assert.IsNull(editor.ReadAudio(id));
        }
        [UnityTest] public IEnumerator PendingAudioNoopRemovalAndTemporaryKeepDiscardShareUndo()
        {
            string id=WorldSound();int revision=editor.AudioRevision(id);var context=new CapabilityContext(editor,animations);var module=new AudioSourceCapability();
            Assert.IsTrue(module.Start(context,"same",SoundArgs(id),out var same,out var error),error);yield return PlacementOperationDone(same);
            Assert.AreEqual(revision,(int)same.Result["revision"]);editor.Undo();Assert.IsNull(editor.ReadAudio(id));editor.Redo();
            var remove=SoundArgs(id,true);remove.Remove("definition");Assert.IsTrue(module.Start(context,"remove",remove,out var operation,out error),error);
            yield return PlacementOperationDone(operation);Assert.AreEqual(0,(int)operation.Result["revision"]);Assert.IsNull(editor.ReadAudio(id));
            editor.Undo();Assert.IsNotNull(editor.ReadAudio(id));Assert.IsTrue(operation.Complete(out error),error);Assert.IsNotNull(editor.ReadAudio(id));
            foreach(bool keep in new[]{false,true}) {
                Assert.IsTrue(editor.BeginTemporaryRoom(out error),error);while(editor.TemporarySavePending)yield return null;
                Assert.IsTrue(module.Start(context,"temporary",module.Example,out operation,out error),error);yield return PlacementOperationDone(operation);
                string created=(string)operation.Result["id"];Assert.IsTrue((bool)operation.Result["temporary"]);
                Assert.IsFalse(new RoomStorage(directory).Load(out error).audioSources.Any(s=>s.id==created),error);
                if(keep)Assert.IsTrue(editor.KeepTemporaryRoom(out error),error);else Assert.IsTrue(editor.DiscardTemporaryRoom(out error),error);
                while(editor.TemporarySavePending)yield return null;
                Assert.AreEqual(keep,editor.ReadAudio(created)!=null);Assert.AreEqual(keep,new RoomStorage(directory).Load(out error).audioSources.Any(s=>s.id==created),error);
            }
        }
        [UnityTest] public IEnumerator PendingAudioStreamSaveAndAttachmentDoNotOpenNetworkOrPlayback()
        {
            using var network=new LiveAudioTestNetwork();var context=new CapabilityContext(editor,animations);var module=new AudioSourceCapability();var args=module.Example;
            args["definition"]=AudioSchema.Encode(new RoomAudioDefinition{version=2,kind="stream",name="Radio",endpoint="https://radio.example.invalid/live.mp3"});
            Assert.IsTrue(module.Start(context,"radio",args,out var saved,out var error),error);yield return PlacementOperationDone(saved);
            Assert.IsTrue(new AudioEmitterCapability().Start(context,"attach",EmitterArgs((string)saved.Result["id"]),out var attached,out error),error);yield return PlacementOperationDone(attached);
            Assert.AreEqual(0,network.Requests);Assert.IsNull(editor.GetComponent<WorldAudio>());
        }
    }
}
