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
    public sealed class ImageLibraryTests {
        string directory;ImageLibrary library;WorkspaceWriteGate gate;
        [SetUp] public void Setup(){directory=Path.Combine(Path.GetTempPath(),"MaestroImageLibrary-"+Guid.NewGuid().ToString("N"));gate=new();library=new(directory,gate);}
        [TearDown] public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        [Test] public void SavedAssetIsAnImmutablePrivateCopy(){
            var bytes=ImageFiles.Png();var asset=ImageLibrary.Inspect("<my>\nbark.image",bytes);string hash=asset.Hash;bytes[44]=99;
            library.SaveAsync(asset).GetAwaiter().GetResult();var restored=new ImageLibrary(directory).ReadAsync(hash).GetAwaiter().GetResult();
            Assert.AreEqual(hash,restored.Hash);Assert.AreEqual("mybark.image",restored.Name);CollectionAssert.AreEqual(asset.Content,restored.Content);
            var entry=library.ListAsync().GetAwaiter().GetResult().Single();Assert.AreEqual(hash,entry.Hash);Assert.AreEqual(3,entry.Width);Assert.AreEqual(2,entry.Height);Assert.AreEqual("png",entry.Format);
            Assert.That(Directory.GetFiles(directory,"*.part"),Is.Empty);
        }
        [Test] public void ExactHashRejectsDamageAndReimportRepairsOnlyThatAsset(){
            var asset=ImageLibrary.Inspect("Bark",ImageFiles.Png());library.SaveAsync(asset).GetAwaiter().GetResult();string path=Path.Combine(directory,asset.Hash+".image");
            var changed=File.ReadAllBytes(path);changed[44]=123;File.WriteAllBytes(path,changed);
            Assert.Throws<InvalidDataException>(()=>library.ReadAsync(asset.Hash).GetAwaiter().GetResult());
            library.SaveAsync(asset).GetAwaiter().GetResult();Assert.AreEqual(asset.Hash,library.ReadAsync(asset.Hash).GetAwaiter().GetResult().Hash);
            Assert.Throws<InvalidDataException>(()=>library.ReadAsync("../other").GetAwaiter().GetResult());
            Assert.Throws<FileNotFoundException>(()=>library.ReadAsync(new string('a',64)).GetAwaiter().GetResult());
        }
        [Test] public void FrozenAndCancelledWorkDoesNotPublish(){
            var asset=ImageLibrary.Inspect("Bark",ImageFiles.Png());
            using(var hold=gate.TryFreeze(out var error)){Assert.IsNull(error);Assert.Throws<InvalidOperationException>(()=>library.SaveAsync(asset).GetAwaiter().GetResult());}
            using var cancel=new CancellationTokenSource();cancel.Cancel();Assert.Catch<OperationCanceledException>(()=>library.SaveAsync(asset,cancel.Token).GetAwaiter().GetResult());
            Assert.IsFalse(Directory.Exists(directory));Assert.IsTrue(gate.CanFreeze(out _));
        }
        [Test] public void CaptureBlocksWritesAndKeepsExactPayloadPaths(){
            var first=ImageLibrary.Inspect("First",ImageFiles.Png());var second=ImageLibrary.Inspect("Second",ImageFiles.Png(shade:1));library.SaveAsync(first).GetAwaiter().GetResult();
            Assert.IsTrue(library.TryCaptureArchive(out var capture));
            var pending=library.SaveAsync(second);Assert.IsFalse(pending.IsCompleted);
            try{var documents=new Dictionary<string,byte[]>();var assets=new Dictionary<string,Func<Stream>>();capture.Collect(documents,assets);Assert.AreEqual("images/"+first.Hash+".image",assets.Keys.Single());Assert.AreEqual("images/"+first.Hash+".txt",documents.Keys.Single());using var stream=assets.Values.Single()();Assert.AreEqual(first.Content.Length,stream.Length);}
            finally{capture.Dispose();pending.GetAwaiter().GetResult();}
            Assert.AreEqual(2,library.ListAsync().GetAwaiter().GetResult().Length);Assert.IsTrue(gate.CanFreeze(out _));
        }
        [Test] public void LibraryBudgetDoesNotDiscardExistingImages(){
            for(int i=0;i<ImageLibrary.MaximumFiles;i++){int n=i;library.SaveAsync(ImageLibrary.Inspect("Image "+i,ImageFiles.Png(shade:n))).GetAwaiter().GetResult();}
            var extra=ImageLibrary.Inspect("Extra",ImageFiles.Png(shade:80));Assert.Throws<IOException>(()=>library.SaveAsync(extra).GetAwaiter().GetResult());
            Assert.AreEqual(ImageLibrary.MaximumFiles,library.ListAsync().GetAwaiter().GetResult().Length);Assert.IsFalse(File.Exists(Path.Combine(directory,extra.Hash+".image")));
        }
    }
}
