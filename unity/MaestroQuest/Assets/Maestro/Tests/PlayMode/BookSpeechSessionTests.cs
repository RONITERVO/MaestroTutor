// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using Maestro.Quest.Book;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Maestro.Quest.Tests
{
    public sealed class BookSpeechSessionTests
    {
        GameObject root; NativeSpeechOutput output; BookSpeechSession bridge;
        double clock; string document = new('a',32), session = new('b',32);
        [SetUp] public void SetUp()
        {
            root = new GameObject("Speech mailbox test"); output = root.AddComponent<NativeSpeechOutput>();
            clock = AudioSettings.dspTime + 5; output.Clock = () => clock; output.TailOverride = .05;
            bridge = new BookSpeechSession(output);
        }
        [UnityTearDown] public IEnumerator TearDown() { bridge.Dispose(); UnityEngine.Object.Destroy(root); yield return null; }
        JObject Chunk(long sequence, int count = 4800)
        {
            var bytes = new byte[count*2]; bytes[1] = 128; bytes[2] = 255; bytes[3] = 127;
            return new JObject { ["sequence"] = sequence, ["pcm"] = Convert.ToBase64String(bytes) };
        }
        string Packet(long poll, long revision, bool open, params JObject[] chunks) => new JObject {
            ["document"] = document, ["poll"] = poll, ["payload"] = new JObject {
                ["version"] = 1, ["session"] = session, ["revision"] = revision, ["open"] = open, ["chunks"] = new JArray(chunks)
            }
        }.ToString(Newtonsoft.Json.Formatting.None);
        JObject Status => JObject.Parse(bridge.Status(clock));
        void Receive(string packet)
        {
            bridge.Receive(packet,clock);
            foreach(var source in root.GetComponentsInChildren<AudioSource>()) source.volume=0;
        }
        [UnityTest] public IEnumerator ReceiptsFollowRealPcmAndLostReceiptCannotPlayAChunkTwice()
        {
            Receive(Packet(1,0,false)); Assert.AreEqual("ready",(string)Status["status"]);
            Receive(Packet(2,1,true,Chunk(1),Chunk(2)));
            var samples = new float[4800]; Assert.IsTrue(root.GetComponentInChildren<AudioSource>().clip.GetData(samples,0));
            Assert.AreEqual(-1,samples[0]); Assert.AreEqual(32767f/32768f,samples[1]);
            Receive(Packet(3,1,true,Chunk(1),Chunk(2)));
            Assert.AreEqual(2,(long)Status["acceptedSequence"]); Assert.AreEqual(9600,(long)Status["submittedSamples"]);
            Assert.AreEqual(0,(long)Status["playedSamples"]);
            clock += .36; Assert.That((long)Status["playedSamples"],Is.InRange(5039,5041));
            clock += .20; Assert.AreEqual(9600,(long)Status["playedSamples"]);
            yield return null;
        }
        [UnityTest] public IEnumerator StopRevisionCancelsAudioAndStaleRevisionsCannotReopenIt()
        {
            Receive(Packet(1,1,true,Chunk(1)));
            Receive(Packet(2,2,false)); Assert.AreEqual(0,output.Read().submittedSamples);
            Receive(Packet(3,1,true,Chunk(2))); Assert.AreEqual("ready",(string)Status["status"]);
            Receive(Packet(4,3,true,Chunk(1,240))); Assert.AreEqual(240,output.Read().submittedSamples);
            output.Stop(); Assert.AreEqual("failed",(string)Status["status"]);
            Receive(Packet(5,3,true,Chunk(1,240))); Assert.AreEqual(0,output.Read().submittedSamples);
            yield return null;
        }
        [UnityTest] public IEnumerator CachedSnapshotDoesNotKeepAudioAliveAndNavigationStopsBeforeHandshake()
        {
            string packet=Packet(1,1,true,Chunk(1)); Receive(packet); clock+=1.51; Receive(packet);
            Assert.AreEqual("failed",(string)Status["status"]); Assert.AreEqual(0,output.Read().submittedSamples);
            Receive(Packet(2,1,true,Chunk(1))); Assert.AreEqual("failed",(string)Status["status"]);
            Receive(Packet(3,2,true,Chunk(1))); Assert.AreEqual("playing",(string)Status["status"]);
            document = new string('c',32);
            Receive(new JObject { ["document"]=document,["poll"]=0,["payload"]=null }.ToString());
            Assert.AreEqual(0,output.Read().submittedSamples);
            session = new string('d',32);
            Receive(Packet(1,0,false)); Assert.AreEqual("ready",(string)Status["status"]);
            yield return null;
        }
        [UnityTest] public IEnumerator FocusResumeCannotRestartOldVoiceEvenWhenJavaScriptMissedTheStop()
        {
            Receive(Packet(1,1,true,Chunk(1)));
            bridge.Suspend(); document = new string('e',32);
            Receive(Packet(1,1,true,Chunk(1)));
            Assert.AreEqual("failed",(string)Status["status"]); Assert.AreEqual(0,output.Read().submittedSamples);
            Receive(Packet(2,2,false)); Assert.AreEqual("ready",(string)Status["status"]);
            Receive(Packet(3,3,true,Chunk(1,240))); Assert.AreEqual(240,output.Read().submittedSamples);
            yield return null;
        }
        [UnityTest] public IEnumerator FreshBrowserMessagesCannotHideAStalledAudioDevice()
        {
            var frozen = clock; output.Clock = () => frozen;
            Receive(Packet(1,1,true,Chunk(1)));
            for (int i=2;i<40;i++) { clock += .1; Receive(Packet(i,1,true,Chunk(1))); }
            Assert.AreEqual("failed",(string)Status["status"]); Assert.AreEqual(0,output.Read().submittedSamples);
            yield return null;
        }
        [UnityTest] public IEnumerator MalformedBatchAndMissingSequenceFailWithoutPartialOrDuplicateSpeech()
        {
            var bad=Chunk(2); bad["pcm"]="invalid base64";
            Receive(Packet(1,1,true,Chunk(1),bad)); Assert.AreEqual(0,output.Read().submittedSamples);
            Receive(Packet(2,2,true,Chunk(2))); Assert.AreEqual("failed",(string)Status["status"]);
            Assert.AreEqual(0,output.Read().submittedSamples);
            Receive(Packet(3,3,true,Chunk(1))); Assert.AreEqual("playing",(string)Status["status"]);
            bridge.Bind(null); Assert.AreEqual("failed",(string)Status["status"]); Assert.AreEqual(0,output.Read().submittedSamples);
            yield return null;
        }
    }
}
