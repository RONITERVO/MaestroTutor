// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Quest.Diagnostics;
using Maestro.Quest.Imports;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        [UnityTest] public IEnumerator WorldAudioDisabledDropsDecodedBuffersWithoutAnotherUpdate()
        {
            string source=WorldSound(5);WorldEmitter("book",source);var audio=WorldAudio.For(editor);
            Assert.IsTrue(audio.Begin("book","sound",out var voice,out var error),error);
            double end=Time.realtimeSinceStartupAsDouble+5;while(voice.Samples==null&&!voice.Closed&&Time.realtimeSinceStartupAsDouble<end)yield return null;
            Assert.IsNotNull(voice.Samples,voice.Error);Assert.Greater(voice.Samples.Length,0);
            var cache=(IDictionary)typeof(WorldAudio).GetField("decoded",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(audio);
            Assert.AreEqual(1,cache.Count);audio.enabled=false;
            Assert.IsTrue(voice.Closed);Assert.IsNull(voice.Samples);
            Assert.AreEqual(0,cache.Count,"Disabled audio must release its decoded cache without relying on a later frame");
            yield return null;
        }
        JObject AudioFact(string name,JObject args=null)
        {
            if(!BehaviourCatalog.TryRead(name,1,args,new BehaviourCatalog.FactContext(editor:editor),out var value))return null;
            Assert.LessOrEqual(value.Characters,1024);return JObject.FromObject(value.Value);
        }
        IEnumerator AudioDrained()
        {
            double end=Time.realtimeSinceStartupAsDouble+5;
            while((int)AudioReservations.ObserveBudget()["sources"]!=0&&Time.realtimeSinceStartupAsDouble<end)yield return null;
            Assert.AreEqual(0,(int)AudioReservations.ObserveBudget()["sources"],"Retired decoders must drain independently of the old component");
        }
        [UnityTest] public IEnumerator AudioResidencyRealVoicesSharePcmAndRetainCopiedOwners()
        {
            yield return AudioDrained();root.AddComponent<RuntimeDiagnostics>();
            string source=WorldSound(5),target=editor.Identity(block);WorldEmitter("book",source);WorldEmitter(target,source);
            var audio=WorldAudio.For(editor);Assert.IsTrue(audio.Begin("book","sound",out var first,out var error),error);Assert.IsTrue(audio.Begin(target,"sound",out var second,out error),error);
            try{
                Assert.AreSame(first.Resource.Resource,second.Resource.Resource);Assert.AreEqual(1,(int)AudioFact("runtime.audioBudget")["sources"]);
                var reservation=AudioFact("runtime.audioReservation",new JObject{["index"]=0});string id=(string)reservation["reservationId"];
                var owners=new JArray(AudioFact("runtime.audioOwner",new JObject{["reservationId"]=id,["index"]=0}),AudioFact("runtime.audioOwner",new JObject{["reservationId"]=id,["index"]=1}));
                CollectionAssert.AreEquivalent(new[]{first.Id,second.Id},owners.Select(o=>(string)o["instanceId"]));CollectionAssert.AreEquivalent(new[]{"book",target},owners.Select(o=>(string)o["target"]));
                foreach(var owner in owners){Assert.AreEqual(editor.WorldIdentity.worldId,(string)owner["worldId"]);Assert.AreEqual(editor.WorldIdentity.regionId,(string)owner["regionId"]);Assert.AreEqual("audio",(string)owner["role"]);Assert.AreEqual(source,(string)owner["sourceId"]);}
                double end=Time.realtimeSinceStartupAsDouble+5;while((first.Samples==null||second.Samples==null)&&!first.Closed&&!second.Closed&&Time.realtimeSinceStartupAsDouble<end)yield return null;
                Assert.IsNotNull(first.Samples,first.Error);Assert.AreSame(first.Samples,second.Samples);
                var ready=AudioFact("runtime.audioReservation",new JObject{["index"]=0});var budget=AudioFact("runtime.audioBudget");Assert.AreEqual(240000,(long)ready["readyPcmBytes"]);Assert.AreEqual(240008,(long)ready["reservedPcmBytes"]);Assert.AreEqual(2,(int)ready["owners"]);
                int revision=editor.Revision;ready["owners"]=-1;Assert.AreEqual(2,(int)AudioFact("runtime.audioReservation",new JObject{["index"]=0})["owners"]);Assert.AreEqual(revision,editor.Revision);
                string path=Environment.GetEnvironmentVariable("MAESTRO_AUDIO_RESIDENCY_EVIDENCE");if(!string.IsNullOrEmpty(path)){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,new JObject{["ready"]=AudioFact("runtime.audioReservation",new JObject{["index"]=0}),["budget"]=budget,["owners"]=owners}.ToString());}
                audio.Cancel(first);Assert.IsNull(first.Resource);Assert.IsNull(first.Producer);Assert.IsFalse(second.Closed);Assert.AreEqual(1,(int)AudioFact("runtime.audioReservation",new JObject{["index"]=0})["owners"]);
                Assert.AreEqual(second.Id,(string)AudioFact("runtime.audioOwner",new JObject{["reservationId"]=id,["index"]=0})["instanceId"]);
                audio.Cancel(second);Assert.IsNull(AudioFact("runtime.audioOwner",new JObject{["reservationId"]=id,["index"]=0}));
            }finally{audio.enabled=false;}
            yield return AudioDrained();
        }
        [UnityTest] public IEnumerator AudioResidencyRetiringDecodersKeepAdmissionAcrossWorldReplacement()
        {
            yield return AudioDrained();var pending=new List<TaskCompletionSource<short[]>>();var leases=new List<AudioReservations.Lease>();
            var source=new RoomAudioDefinition{id=Guid.NewGuid().ToString("N")};string retired=null;AudioReservations.Lease replacement=null;
            var nextWorld=RoomWorldIdentity.Create();var nextOwner=new RoomResourceOwner(nextWorld,"book","audio");int newDecodes=0;
            try{
                for(int i=0;i<AudioReservations.MaximumSources;i++){
                    var work=new TaskCompletionSource<short[]>(TaskCreationOptions.RunContinuationsAsynchronously);pending.Add(work);
                    leases.Add(AudioReservations.Acquire(null,source,new RoomResourceOwner(RoomWorldIdentity.Create(),"book","audio"),Guid.NewGuid().ToString("N"),"sound",1,_=>work.Task));
                }
                Assert.Throws<InvalidOperationException>(()=>AudioReservations.Acquire(null,source,nextOwner,Guid.NewGuid().ToString("N"),"sound",1,_=>{newDecodes++;return Task.FromResult(new short[10]);}));
                retired=leases[0].Resource.Id;foreach(var lease in leases)lease.Dispose();
                var budget=AudioReservations.ObserveBudget();Assert.AreEqual(0,(int)budget["owners"]);Assert.AreEqual(AudioReservations.MaximumSources,(int)budget["retiring"]);Assert.Greater((long)budget["reservedPcmBytes"],0);
                Assert.Throws<InvalidOperationException>(()=>AudioReservations.Acquire(null,source,nextOwner,Guid.NewGuid().ToString("N"),"sound",1,_=>{newDecodes++;return Task.FromResult(new short[10]);}));Assert.AreEqual(0,newDecodes);
                pending[0].SetResult(new short[10]);double end=Time.realtimeSinceStartupAsDouble+5;while((int)AudioReservations.ObserveBudget()["sources"]==AudioReservations.MaximumSources&&Time.realtimeSinceStartupAsDouble<end)yield return null;
                Assert.AreEqual(AudioReservations.MaximumSources-1,(int)AudioReservations.ObserveBudget()["sources"]);
                replacement=AudioReservations.Acquire(null,source,nextOwner,Guid.NewGuid().ToString("N"),"sound",1,_=>{newDecodes++;return Task.FromResult(new short[10]);});Assert.AreEqual(1,newDecodes);Assert.AreNotEqual(retired,replacement.Resource.Id);Assert.IsNull(AudioReservations.ObserveOwner(retired,0));
                nextWorld.worldId=new string('a',32);Assert.AreNotEqual(nextWorld.worldId,(string)AudioReservations.ObserveOwner(replacement.Resource.Id,0)["worldId"]);
            }finally{replacement?.Dispose();foreach(var lease in leases)lease.Dispose();foreach(var work in pending)work.TrySetCanceled();}
            yield return AudioDrained();
        }
        [UnityTest] public IEnumerator AudioResidencyWorkspaceDestructionCancelsActualImportedRead()
        {
            yield return AudioDrained();var asset=AudioLibrary.Inspect("Held WAV",SoundFile());var save=editor.Sounds.SaveAsync(asset);yield return new WaitUntil(()=>save.IsCompleted);Assert.IsNull(save.Exception);
            var source=new RoomAudioDefinition{id=Guid.NewGuid().ToString("N"),kind="clip",assetHash=asset.Hash,seconds=1};Assert.IsTrue(editor.EditAudio(source.id,0,source,out var error),error);WorldEmitter("book",source.id);
            Assert.IsTrue(editor.Sounds.TryCaptureArchive(out var capture));var audio=WorldAudio.For(editor);var gate=editor.WriteGate;
            try{
                Assert.IsTrue(audio.Begin("book","sound",out var voice,out error),error);Assert.IsFalse(gate.CanFreeze(out _));string id=voice.Resource.Resource.Id;
                Assert.AreEqual("loading",(string)AudioReservations.ObserveReservation(0)["state"]);
                UnityEngine.Object.Destroy(audio);yield return null;Assert.IsTrue(voice.Closed);Assert.IsNull(voice.Resource);Assert.IsNull(voice.Samples);Assert.IsNull(AudioReservations.ObserveOwner(id,0));
                yield return AudioDrained();Assert.IsTrue(gate.CanFreeze(out _));
            }finally{capture.Dispose();if(audio)audio.enabled=false;}
        }
        [UnityTest] public IEnumerator AudioResidencyFailuresAndStaleOwnersCannotReuseReleasedAdmission()
        {
            yield return AudioDrained();root.AddComponent<RuntimeDiagnostics>();var source=new RoomAudioDefinition{id=Guid.NewGuid().ToString("N")};var owner=new RoomResourceOwner(editor.WorldIdentity,"book","audio");
            using(var failed=AudioReservations.Acquire(null,source,owner,Guid.NewGuid().ToString("N"),"sound",1,_=>Task.FromException<short[]>(new IOException("Expected decoder failure")))){
                Assert.AreEqual("failed",(string)AudioFact("runtime.audioReservation",new JObject{["index"]=0})["state"]);Assert.AreEqual(0,(long)AudioFact("runtime.audioBudget")["readyPcmBytes"]);
            }
            yield return AudioDrained();
            string old;using(var valid=AudioReservations.Acquire(null,source,owner,Guid.NewGuid().ToString("N"),"sound",1,_=>Task.FromResult(new short[10]))){
                old=valid.Resource.Id;Assert.Throws<InvalidOperationException>(()=>AudioReservations.Acquire(valid.Resource,source,owner,valid.Instance,"sound",1,_=>Task.FromResult(new short[10])));
                Assert.IsNull(AudioFact("runtime.audioOwner",new JObject{["reservationId"]=old,["index"]=0,["stop"]=true}));Assert.IsNull(AudioFact("runtime.audioReservation",new JObject{["index"]=AudioReservations.MaximumSources}));
                Assert.IsNull(AudioFact("runtime.audioOwner",new JObject{["reservationId"]=old,["index"]=AudioReservations.MaximumOwners}));
            }
            yield return AudioDrained();Assert.IsNull(AudioFact("runtime.audioOwner",new JObject{["reservationId"]=old,["index"]=0}));
            using(var excessive=AudioReservations.Acquire(null,source,owner,Guid.NewGuid().ToString("N"),"sound",1,_=>Task.FromResult(new short[24000]))){Assert.AreEqual("failed",(string)AudioReservations.ObserveReservation(0)["state"]);Assert.AreEqual(0,(long)AudioReservations.ObserveBudget()["readyPcmBytes"]);}
            yield return AudioDrained();
        }
    }
}
