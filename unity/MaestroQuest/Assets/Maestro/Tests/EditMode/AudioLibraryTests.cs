// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Maestro.Quest.Imports;
using Maestro.Quest.Persistence;
using NUnit.Framework;
namespace Maestro.Quest.Tests {
    public sealed class AudioLibraryTests {
        string directory;AudioLibrary library;WorkspaceWriteGate gate;
        [SetUp] public void Setup(){directory=Path.Combine(Path.GetTempPath(),"MaestroAudioLibrary-"+Guid.NewGuid().ToString("N"));gate=new();library=new(directory,gate);}
        [TearDown] public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        [Test] public void SavedAssetIsAnImmutablePrivateCopy(){
            var bytes=WaveAudioTests.File();var asset=AudioLibrary.Inspect("<my>\nbark.wav",bytes);string hash=asset.Hash;bytes[44]=99;
            library.SaveAsync(asset).GetAwaiter().GetResult();var restored=new AudioLibrary(directory).ReadAsync(hash).GetAwaiter().GetResult();
            Assert.AreEqual(hash,restored.Hash);Assert.AreEqual("mybark.wav",restored.Name);Assert.AreEqual(0,restored.Content[44]);
            Assert.AreEqual(2400,library.DecodeAsync(hash).GetAwaiter().GetResult().Length);
            Assert.Throws<InvalidDataException>(()=>library.DecodeAsync(hash,expectedSeconds:.2f).GetAwaiter().GetResult());
            var entry=library.ListAsync().GetAwaiter().GetResult().Single();Assert.AreEqual(hash,entry.Hash);Assert.AreEqual(1,entry.Channels);Assert.AreEqual(24000,entry.Rate);Assert.That(entry.Seconds,Is.EqualTo(.1).Within(1e-9));
            Assert.That(Directory.GetFiles(directory,"*.part"),Is.Empty);
        }
        [Test] public void ExactHashRejectsDamageAndReimportRepairsOnlyThatAsset(){
            var asset=AudioLibrary.Inspect("Bark",WaveAudioTests.File());library.SaveAsync(asset).GetAwaiter().GetResult();string path=Path.Combine(directory,asset.Hash+".wav");
            var changed=File.ReadAllBytes(path);changed[44]=123;File.WriteAllBytes(path,changed);
            Assert.Throws<InvalidDataException>(()=>library.ReadAsync(asset.Hash).GetAwaiter().GetResult());
            library.SaveAsync(asset).GetAwaiter().GetResult();Assert.AreEqual(asset.Hash,library.ReadAsync(asset.Hash).GetAwaiter().GetResult().Hash);
            Assert.Throws<InvalidDataException>(()=>library.ReadAsync("../other").GetAwaiter().GetResult());
            Assert.Throws<FileNotFoundException>(()=>library.ReadAsync(new string('a',64)).GetAwaiter().GetResult());
        }
        [Test] public void FrozenAndCancelledWorkDoesNotPublish(){
            var asset=AudioLibrary.Inspect("Bark",WaveAudioTests.File());
            using(var hold=gate.TryFreeze(out var error)){Assert.IsNull(error);Assert.Throws<InvalidOperationException>(()=>library.SaveAsync(asset).GetAwaiter().GetResult());}
            using var cancel=new CancellationTokenSource();cancel.Cancel();Assert.Catch<OperationCanceledException>(()=>library.SaveAsync(asset,cancel.Token).GetAwaiter().GetResult());
            Assert.IsFalse(Directory.Exists(directory));Assert.IsTrue(gate.CanFreeze(out _));
        }
        [Test] public void CaptureBlocksWritesAndKeepsExactPayloadPaths(){
            var first=AudioLibrary.Inspect("First",WaveAudioTests.File());var second=AudioLibrary.Inspect("Second",WaveAudioTests.File(sample:(i,c)=>.5));library.SaveAsync(first).GetAwaiter().GetResult();
            Assert.IsTrue(library.TryCaptureArchive(out var capture));
            var pending=library.SaveAsync(second);Assert.IsFalse(pending.IsCompleted);
            try{var documents=new Dictionary<string,byte[]>();var assets=new Dictionary<string,Func<Stream>>();capture.Collect(documents,assets);Assert.AreEqual("audio/"+first.Hash+".wav",assets.Keys.Single());Assert.AreEqual("audio/"+first.Hash+".txt",documents.Keys.Single());using var stream=assets.Values.Single()();Assert.AreEqual(first.Content.Length,stream.Length);}
            finally{capture.Dispose();}
            pending.GetAwaiter().GetResult();Assert.AreEqual(2,library.ListAsync().GetAwaiter().GetResult().Length);Assert.IsTrue(gate.CanFreeze(out _));
        }
        [Test] public void LibraryBudgetDoesNotDiscardExistingSounds(){
            for(int i=0;i<AudioLibrary.MaximumFiles;i++){int n=i;library.SaveAsync(AudioLibrary.Inspect("Sound "+i,WaveAudioTests.File(sample:(f,c)=>.1+n*.001))).GetAwaiter().GetResult();}
            var extra=AudioLibrary.Inspect("Extra",WaveAudioTests.File(sample:(f,c)=>.9));Assert.Throws<IOException>(()=>library.SaveAsync(extra).GetAwaiter().GetResult());
            Assert.AreEqual(AudioLibrary.MaximumFiles,library.ListAsync().GetAwaiter().GetResult().Length);Assert.IsFalse(File.Exists(Path.Combine(directory,extra.Hash+".wav")));
        }
    }
}
