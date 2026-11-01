// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests {
    public sealed class LiveAudioIntegrationTests {
        const string Endpoint="https://radio.example.invalid/live.mp3";
        static RoomAudioDefinition Source()=>new(){version=2,id=new string('a',32),name="Radio",kind="stream",endpoint=Endpoint};
        static async Task Until(Func<bool> predicate,int seconds=5){var end=DateTime.UtcNow.AddSeconds(seconds);while(!predicate()&&DateTime.UtcNow<end)await Task.Delay(5);Assert.IsTrue(predicate());}
        [TestCase("http://radio.example/live")][TestCase("https://name:secret@radio.example/live")][TestCase("https://radio.example/live?key=secret")][TestCase("https://radio.example/live#token")][TestCase("https://radio.example/live\n")][TestCase("https:\\radio.example/live")]
        public void LiveEndpointRejectsCredentialsAndNonDirectUrls(string endpoint){var s=Source();s.endpoint=endpoint;Assert.IsFalse(s.Validate(out _));Assert.IsFalse(CapabilityArguments.Validate(AudioSchema.Encode(s),AudioSchema.Source(),out _));}
        [Test] public void LiveDefinitionRoundTripCopyUndoAndFixedFactsDoNotConnect(){
            using var net=new LiveAudioTestNetwork();var s=Source();Assert.IsTrue(s.Validate(out var error),error);
            var wire=AudioSchema.Encode(s);Assert.IsTrue(CapabilityArguments.Validate(wire,AudioSchema.Source(),out error),error);var decoded=AudioSchema.ReadSource(wire);decoded.id=s.id;Assert.AreEqual(JsonUtility.ToJson(s),JsonUtility.ToJson(decoded));
            var fact=AudioSchema.ObserveSource(s);CollectionAssert.AreEquivalent(new[]{"name","kind","tone","clip"},fact.Properties().Select(p=>p.Name));Assert.AreEqual("stream",(string)fact["kind"]);
            var item=new RoomObjectData{id=new string('b',32),kind=RoomObjectKind.Block,audioEmitters=new[]{new RoomAudioEmitter{source=s.id}}};
            var room=new RoomDocument{version=RoomDocument.CurrentVersion,audioSources=new[]{s},objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro},item}};
            Assert.IsTrue(room.Validate(out error),error);var journal=new RoomJournal(room);var changed=s.Copy();changed.endpoint="https://other.example.invalid/live";
            Assert.IsTrue(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out error,audioEdits:new AudioDefinitionEdits{Replacements=new[]{changed}}),error);Assert.IsTrue(journal.Undo());Assert.AreEqual(Endpoint,journal.ReadAudio(s.id).endpoint);
            var objects=new[]{item.Copy()};var bundle=CreationResources.Capture(objects,room);Assert.IsTrue(bundle.Validate(objects,out error),error);var copied=bundle.Instantiate(objects);Assert.IsTrue(copied.Validate(objects,out error),error);Assert.AreNotEqual(s.id,copied.audioSources[0].id);Assert.AreEqual(Endpoint,copied.audioSources[0].endpoint);Assert.AreEqual(copied.audioSources[0].id,objects[0].audioEmitters[0].source);
            string directory=Path.Combine(Path.GetTempPath(),"LiveSourceRoundTrip-"+Guid.NewGuid().ToString("N"));
            try{var store=new RoomStorage(directory);Assert.IsTrue(store.Save(room,out error),error);var round=store.Load(out error);Assert.IsNotNull(round,error);Assert.IsTrue(round.Validate(out error),error);Assert.AreEqual(Endpoint,round.audioSources[0].endpoint);}finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
            room.version=39;Assert.IsFalse(room.Validate(out _));s.version=1;Assert.IsFalse(s.Validate(out _));Assert.AreEqual(0,net.Requests);Assert.AreEqual(0,LiveMp3Source.ActiveSources);
        }
        [Test] public void LongLiveEndpointsRemainFullyDiscoverableWithinProgramValueBounds(){
            var source=Source();source.endpoint="https://radio.example.invalid/"+new string('x',2048-"https://radio.example.invalid/".Length);Assert.AreEqual(2048,source.endpoint.Length);Assert.IsTrue(source.Validate(out var error),error);
            string restored="";int offset=0;
            do{var page=(JObject)LiveAudioFacts.SourcePage(source,7,offset).Value.Value;Assert.AreEqual(7,(int)page["revision"]);Assert.AreEqual(2048,(int)page["total"]);Assert.LessOrEqual(((string)page["endpoint"]).Length,128);restored+=(string)page["endpoint"];offset=(int)page["next"];}while(offset>=0);
            Assert.AreEqual(source.endpoint,restored);source.endpoint+="x";Assert.IsFalse(source.Validate(out _));
        }
        [TestCase("redirect")][TestCase("type")][TestCase("metadata")][TestCase("encoding")]
        public async Task LiveHttpRejectsUnsupportedResponseAndReleasesItsOwners(string bad){
            using var net=new LiveAudioTestNetwork();if(bad=="redirect")net.Status=HttpStatusCode.Redirect;if(bad=="type")net.MediaType="text/html";if(bad=="metadata")net.Metadata="16000";if(bad=="encoding")net.Encoding="gzip";
            try{await LiveAudioHttp.Open(Endpoint,CancellationToken.None);Assert.Fail("Unsupported response accepted");}catch(InvalidDataException e){Assert.That(e.Message,Does.Not.Contain(Endpoint));}
            Assert.AreEqual(1,net.Requests);Assert.AreEqual(1,net.DisposedHandlers);Assert.AreEqual(1,net.Bodies.Single().Disposals);Assert.AreEqual(0,LiveMp3Source.ActiveSources);Assert.IsNull(net.Authentication);Assert.IsNull(net.Cookie);
        }
        [TestCase("audio/mpeg")][TestCase("Audio/MPEG")][TestCase("Audio/MP3")] public async Task LiveHttpCancellationClosesBlockedReadAndDoesNotReconnect(string media){
            using var net=new LiveAudioTestNetwork{Metadata="0",MediaType=media};using var stop=new CancellationTokenSource();var asset=(LivePcmAsset)await LiveAudioHttp.Open(Endpoint,stop.Token);
            await Until(()=>asset.State.Produced>2400);net.Bodies[0].Starve=true;stop.Cancel();await asset.Close();Assert.AreEqual("closed",asset.State.Phase);Assert.AreEqual(0,asset.State.Buffered);Assert.AreEqual(1,net.Requests);Assert.AreEqual(1,net.DisposedHandlers);Assert.AreEqual(0,LiveMp3Source.ActiveSources);
        }
        [Test] public async Task LiveHttpCancellationDuringHeadersOwnsNoDecoder(){
            using var net=new LiveAudioTestNetwork();using var stop=new CancellationTokenSource();net.Headers=async cancel=>{await Task.Delay(30000,cancel);return new HttpResponseMessage();};
            var task=LiveAudioHttp.Open(Endpoint,stop.Token);await Until(()=>net.Requests==1);stop.Cancel();try{await task;Assert.Fail();}catch(OperationCanceledException){}Assert.AreEqual(1,net.DisposedHandlers);Assert.AreEqual(0,LiveMp3Source.ActiveSources);
        }
        [Test] public async Task LiveHttpHeaderDeadlineIsBounded(){
            using var net=new LiveAudioTestNetwork();net.Headers=async cancel=>{await Task.Delay(30000,cancel);return new HttpResponseMessage();};var task=LiveAudioHttp.Open(Endpoint,CancellationToken.None);
            await Until(()=>task.IsCompleted,13);try{await task;Assert.Fail();}catch(OperationCanceledException){}Assert.AreEqual(1,net.Requests);Assert.AreEqual(1,net.DisposedHandlers);
        }
        [Test] public async Task LiveHttpStalledReadFailsWithinDeadline(){
            using var net=new LiveAudioTestNetwork();var asset=(LivePcmAsset)await LiveAudioHttp.Open(Endpoint,CancellationToken.None);
            try{await Until(()=>asset.State.Produced>2400);net.Bodies[0].Starve=true;await Until(()=>asset.State.Phase=="failed",18);Assert.IsNotEmpty(asset.State.Error);Assert.AreEqual(1,net.Requests);Assert.AreEqual(1,net.Bodies[0].Disposals);}finally{await asset.Close();}Assert.AreEqual(0,LiveMp3Source.ActiveSources);
        }
    }
}
