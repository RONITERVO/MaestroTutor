// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using Maestro.Quest.Creation;
using Maestro.Quest.Book;
using Maestro.Quest.Imports;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        static byte[] LongSoundFile(){
            var one=SoundFile();int length=one.Length-44;var bytes=new byte[44+length*32];Array.Copy(one,bytes,44);
            for(int i=0;i<32;i++)Array.Copy(one,44,bytes,44+i*length,length);
            Array.Copy(BitConverter.GetBytes(bytes.Length-8),0,bytes,4,4);Array.Copy(BitConverter.GetBytes(bytes.Length-44),0,bytes,40,4);return bytes;
        }
        IEnumerator LongSound(Action<string> selected){
            var asset=AudioLibrary.Inspect("Long recording",LongSoundFile());var saving=editor.Sounds.SaveAsync(asset);while(!saving.IsCompleted)yield return null;
            Assert.IsFalse(saving.IsFaulted,saving.Exception?.ToString());var definition=new RoomAudioDefinition{id=Guid.NewGuid().ToString("N"),kind="clip",assetHash=asset.Hash,seconds=32};
            Assert.IsTrue(editor.EditAudio(definition.id,0,definition,out var error),error);selected(definition.id);
        }
        [UnityTest] public IEnumerator LongClipConsumesAllNativeSamplesWithoutRetainingTheWholeRecording(){
            yield return AudioDrained();string source=null;yield return LongSound(value=>source=value);WorldEmitter("book",source);
            var audio=WorldAudio.For(editor);Assert.IsTrue(audio.Begin("book","sound",out var voice,out var error),error);
            double deadline=Time.realtimeSinceStartupAsDouble+45;
            while(!voice.Output&&!voice.Closed&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.IsNotNull(voice.Output,voice.Error);Assert.IsNull(voice.Samples);Assert.IsInstanceOf<WavePcmAsset>(voice.Asset);
            var asset=voice.Asset;Assert.Less(asset.CapacityBytes,32*24000*2);Assert.IsTrue(editor.WriteGate.CanFreeze(out _));
            while(!voice.Closed&&Time.realtimeSinceStartupAsDouble<deadline){Assert.LessOrEqual(asset.BufferedBytes,asset.CapacityBytes);yield return null;}
            Assert.IsTrue(voice.Closed,voice.Error);Assert.IsTrue(voice.Complete,voice.Error);Assert.That(voice.Cursor,Is.EqualTo(32).Within(.001));
            Assert.IsNull(voice.Asset);yield return AudioDrained();Assert.AreEqual(0,asset.BufferedBytes);Assert.IsTrue(editor.WriteGate.CanFreeze(out _));
        }
        [UnityTest] public IEnumerator LongClipInstancesShareCacheWithIndependentPauseAndStop(){
            yield return AudioDrained();string source=null;yield return LongSound(value=>source=value);string target=editor.Identity(block);WorldEmitter("book",source);WorldEmitter(target,source);
            var audio=WorldAudio.For(editor);Assert.IsTrue(audio.Begin("book","sound",out var first,out var error),error);
            double deadline=Time.realtimeSinceStartupAsDouble+10;while(first.Cursor<.3&&!first.Closed&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.IsTrue(audio.Handoff(first,out error),error);Assert.IsTrue(audio.Begin(target,"sound",out var second,out error),error);
            while(second.Cursor<.1&&!second.Closed&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.IsTrue(audio.Handoff(second,out error),error);Assert.AreSame(first.Asset,second.Asset);Assert.AreSame(first.Resource.Resource,second.Resource.Resource);
            Assert.IsTrue(audio.Control("book",first.Id,first.Revision,"pause",0,out _,out error),error);yield return new WaitForSecondsRealtime((float)Math.Max(.2,PcmOutputLease.DeviceTail+.05));double paused=first.Cursor,other=second.Cursor;
            yield return new WaitForSecondsRealtime(.3f);Assert.That(first.Cursor,Is.EqualTo(paused).Within(.01));Assert.Greater(second.Cursor,other+.1);
            Assert.IsTrue(audio.Control("book",first.Id,first.Revision,"resume",0,out _,out error),error);yield return new WaitForSecondsRealtime(.3f);Assert.Greater(first.Cursor,paused+.1);Assert.Greater(second.Cursor,other+.2);
            Assert.IsTrue(audio.Control("book",first.Id,first.Revision,"stop",0,out _,out error),error);Assert.IsTrue(first.Closed);Assert.IsFalse(second.Closed);Assert.IsNull(first.Asset);
            Assert.AreEqual(1,(int)AudioReservations.ObserveBudget()["owners"]);Assert.IsTrue(editor.WriteGate.CanFreeze(out _));
            Assert.IsTrue(audio.Control(target,second.Id,second.Revision,"stop",0,out _,out error),error);yield return AudioDrained();
        }
    }
}
