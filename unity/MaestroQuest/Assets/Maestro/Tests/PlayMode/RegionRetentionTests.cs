// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
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
        bool RetainedFor(string target,RoomRetentionReason reason)=>(editor.ReadRetention().Reasons(target)&reason)!=0;
        [UnityTest] public IEnumerator RegionRetentionUsesActualActionAndManualOwnershipLifetimes()
        {
            string target=editor.Identity(block),area=NewRegion();Assert.That(AssignRegion(target,area,out var error),Is.True,error);
            Assert.That(RetainedFor(target,RoomRetentionReason.Ownership),Is.False);
            Assert.That(runtime.Trigger(sequenceId),Is.True);Assert.That(RetainedFor(target,RoomRetentionReason.Ownership),Is.True);
            runtime.StopAll();Assert.That(RetainedFor(target,RoomRetentionReason.Ownership),Is.False);
            Assert.That(editor.Ownership.TryAcquire("retention-pose","Pose",RoomActorRole.Control,new[]{new BehaviourCatalog.Claim(target,"wholeTarget")},null,out var lease,out error),Is.True,error);
            Assert.That(RetainedFor(target,RoomRetentionReason.Ownership),Is.True);lease.Dispose();Assert.That(RetainedFor(target,RoomRetentionReason.Ownership),Is.False);yield return null;
        }
        [UnityTest] public IEnumerator RegionRetentionKeepsPendingPausedAndIndependentAudioUntilClosed()
        {
            string target=editor.Identity(block),source=WorldSound(5),area=NewRegion();WorldEmitter(target,source);Assert.That(AssignRegion(target,area,out var error),Is.True,error);
            var listener=new GameObject("Retention audio listener",typeof(AudioListener));var listeners=UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);var enabled=listeners.Select(x=>x.enabled).ToArray();for(int i=0;i<listeners.Length;i++)listeners[i].enabled=listeners[i].gameObject==listener;
            bool pause=AudioListener.pause;AudioListener.pause=false;
            try {
                var audio=WorldAudio.For(editor);Assert.That(audio.Begin(target,"sound",out var voice,out error,true),Is.True,error);
                Assert.That(RetainedFor(target,RoomRetentionReason.Audio),Is.True,"Preparing decode already needs the emitter");
                audio.Cancel(voice);Assert.That(RetainedFor(target,RoomRetentionReason.Audio),Is.False);
                AudioInstanceSample sample=default;yield return IndependentSound(new RoomRuleActions(editor,animations),target,true,value=>sample=value);
                Assert.That(audio.Control(target,sample.Instance,sample.Revision,"pause",0,out sample,out error),Is.True,error);
                runtime.StopAll();Assert.That(RetainedFor(target,RoomRetentionReason.Audio),Is.True,"Detached paused audio survives scheduler completion");
                Assert.That(audio.Control(target,sample.Instance,sample.Revision,"stop",0,out sample,out error),Is.True,error);
                Assert.That(RetainedFor(target,RoomRetentionReason.Audio),Is.False);
                Assert.That(audio.Begin(target,"sound",out voice,out error,true),Is.True,error);audio.enabled=false;
                Assert.That(RetainedFor(target,RoomRetentionReason.Audio),Is.False,"Disable closes every instance, including pending decode");
            } finally {var audio=editor.GetComponent<WorldAudio>();if(audio)audio.enabled=false;UnityEngine.Object.Destroy(listener);for(int i=0;i<listeners.Length;i++)if(listeners[i])listeners[i].enabled=enabled[i];AudioListener.pause=pause;}
            yield return null;
        }
        [UnityTest] public IEnumerator RegionRetentionReportsUnavailableNativeObjectsWithoutDeletingSavedEntities()
        {
            string target=editor.Identity(block),area=NewRegion();Assert.That(AssignRegion(target,area,out var error),Is.True,error);
            string saved=JsonUtility.ToJson(editor.Snapshot());int revision=editor.ObjectRevision(target);block.gameObject.SetActive(false);
            Assert.That(RegionFacts.Retention().TryRead(new BehaviourCatalog.FactContext(editor:editor),1,new JObject{["id"]=area},out var value),Is.True);
            var fact=(JObject)value.Value;Assert.That((int)fact["memberCount"],Is.EqualTo(1));Assert.That((int)fact["residentCount"],Is.Zero);Assert.That((int)fact["retainedCount"],Is.EqualTo(1));Assert.That(fact["reasons"].Values<string>(),Does.Contain("unavailable"));Assert.That((bool)fact["unloadingSupported"],Is.False);
            Assert.That(editor.Read(target),Is.Not.Null);Assert.That(editor.ObjectRevision(target),Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));
            block.gameObject.SetActive(true);Assert.That((int)editor.ObserveRegionRetention(area)["residentCount"],Is.EqualTo(1));Assert.That(RetainedFor(target,RoomRetentionReason.Unavailable),Is.False);yield return null;
        }
        [UnityTest] public IEnumerator RegionRetentionReportsLoadingAndFailedModelGeometryAsUnavailable()
        {
            Assert.That(editor.CreateImportedModel(new string('f',64),out var target,out var error),Is.True,error);
            string area=NewRegion();Assert.That(AssignRegion(target,area,out error),Is.True,error);
            Assert.That(RetainedFor(target,RoomRetentionReason.Unavailable),Is.True);
            var view=editor.Find(target).GetComponent<CreatedRoomObject>();float deadline=Time.realtimeSinceStartup+5;
            while(view.ModelStatus=="Loading local model…"&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(view.ModelGeometryReady,Is.False);Assert.That(view.ModelStatus,Is.Not.EqualTo("Loading local model…"));
            var fact=editor.ObserveRegionRetention(area);Assert.That((int)fact["residentCount"],Is.EqualTo(1));Assert.That(fact["reasons"].Values<string>(),Does.Contain("unavailable"));
            Assert.That(editor.DeleteObject(target,out error),Is.True,error);Assert.That((int)editor.ObserveRegionRetention(area)["retainedCount"],Is.Zero);yield return null;
        }
        [UnityTest] public IEnumerator RegionRetentionHonoursWholeWorldCollisionAndWorkspaceGatesWithoutPersistingThem()
        {
            string target=editor.Identity(block),area=NewRegion();Assert.That(AssignRegion(target,area,out var error),Is.True,error);string saved=JsonUtility.ToJson(editor.Snapshot());
            using(var hold=editor.WriteGate.TryFreeze(out error)){Assert.That(hold,Is.Not.Null,error);Assert.That(RetainedFor(target,RoomRetentionReason.WorkspaceBusy),Is.True);}
            Assert.That(RetainedFor(target,RoomRetentionReason.WorkspaceBusy),Is.False);
            physics.SetSurfaces(true,"Test room");physics.StartPhysics();Assert.That(physics.Running,Is.True);Assert.That(RetainedFor(target,RoomRetentionReason.CollisionEnvironment),Is.True);
            physics.PausePhysics();Assert.That(RetainedFor(target,RoomRetentionReason.CollisionEnvironment),Is.False);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(saved));yield return null;
        }
    }
}
