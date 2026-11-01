// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
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
        [TestCase(true)][TestCase(false)] public void ExplicitSameHashSaveRepairsUnusableSidecarWithoutPassiveRepair(bool oversized){
            var asset=ImageLibrary.Inspect("Original image",ImageFiles.Png());library.SaveAsync(asset).GetAwaiter().GetResult();library.SaveAsync(ImageLibrary.Inspect("Other image",ImageFiles.Png(shade:1))).GetAwaiter().GetResult();
            string info=Path.Combine(directory,asset.Hash+".txt");byte[] damaged=oversized?new byte[1025]:new byte[]{0xc3,0x28};File.WriteAllBytes(info,damaged);
            var before=Directory.GetFiles(directory).ToDictionary(Path.GetFileName,File.ReadAllBytes);var expected=oversized?typeof(InvalidDataException):typeof(DecoderFallbackException);
            Assert.Throws(expected,()=>library.ReadAsync(asset.Hash).GetAwaiter().GetResult());Assert.Throws(expected,()=>library.ListAsync().GetAwaiter().GetResult());Assert.That(File.ReadAllBytes(info),Is.EqualTo(damaged),"Passive reads must preserve damaged metadata");
            var accepted=ImageLibrary.Inspect("Explicitly reimported image",asset.Content);Assert.That(accepted.Hash,Is.EqualTo(asset.Hash));library.SaveAsync(accepted).GetAwaiter().GetResult();
            var read=library.ReadAsync(asset.Hash).GetAwaiter().GetResult();Assert.That(read.Hash,Is.EqualTo(asset.Hash));Assert.That(read.Name,Is.EqualTo(accepted.Name));Assert.That(read.Content,Is.EqualTo(asset.Content));Assert.That(library.ListAsync().GetAwaiter().GetResult().Any(e=>e.Hash==asset.Hash),Is.True);
            Assert.That(Directory.GetFiles(directory).Select(Path.GetFileName),Is.EquivalentTo(before.Keys));foreach(var file in before)if(file.Key!=asset.Hash+".txt")Assert.That(File.ReadAllBytes(Path.Combine(directory,file.Key)),Is.EqualTo(file.Value),file.Key);
        }
        [TestCase(false)][TestCase(true)] public void SameHashSavePreservesTheEntireValidSidecar(bool utf16){
            var asset=ImageLibrary.Inspect("Original image",ImageFiles.Png());library.SaveAsync(asset).GetAwaiter().GetResult();string info=Path.Combine(directory,asset.Hash+".txt");
            Encoding encoding=utf16?Encoding.Unicode:new UTF8Encoding(true);byte[] metadata=encoding.GetPreamble().Concat(encoding.GetBytes("Saved label\r\nOrigin: original source\r\nAttribution: keep this complete line\r\n")).ToArray();File.WriteAllBytes(info,metadata);
            Assert.That(library.ReadAsync(asset.Hash).GetAwaiter().GetResult().Name,Is.EqualTo("Saved label"));library.SaveAsync(ImageLibrary.Inspect("Replacement label",asset.Content)).GetAwaiter().GetResult();
            var inventory=library.ScanAsync().GetAwaiter().GetResult();Assert.That(inventory.Complete,Is.True);Assert.That(inventory.Entries.Single().Name,Is.EqualTo("Saved label"));
            Assert.That(File.ReadAllBytes(info),Is.EqualTo(metadata));Assert.That(library.ReadAsync(asset.Hash).GetAwaiter().GetResult().Name,Is.EqualTo("Saved label"));
        }
        [Test] public void AcceptedSidecarCancellationAfterStagingPreservesOriginalBytes(){
            var asset=ImageLibrary.Inspect("Image",ImageFiles.Png());library.SaveAsync(asset).GetAwaiter().GetResult();string info=Path.Combine(directory,asset.Hash+".txt");byte[] damaged={0xc3,0x28};File.WriteAllBytes(info,damaged);
            using var stop=new CancellationTokenSource();
            var cancelled=Assert.Catch<OperationCanceledException>(()=>ImportedAssetSidecar.SaveAccepted(info,"Explicit repair",stop.Token,()=>{Assert.That(Directory.GetFiles(directory,"*.part"),Has.Length.EqualTo(1));stop.Cancel();}));
            Assert.That(cancelled.CancellationToken,Is.EqualTo(stop.Token));Assert.That(File.ReadAllBytes(info),Is.EqualTo(damaged));Assert.That(File.ReadAllBytes(Path.Combine(directory,asset.Hash+".image")),Is.EqualTo(asset.Content));Assert.That(Directory.GetFiles(directory,"*.part"),Is.Empty);
        }
        [Test] public void AcceptedSidecarReadFailureAfterStagingDoesNotPublish(){
            var asset=ImageLibrary.Inspect("Image",ImageFiles.Png());library.SaveAsync(asset).GetAwaiter().GetResult();string info=Path.Combine(directory,asset.Hash+".txt");byte[] damaged={0xc3,0x28};File.WriteAllBytes(info,damaged);FileStream held=null;
            try{Assert.Throws<IOException>(()=>ImportedAssetSidecar.SaveAccepted(info,"Explicit repair",CancellationToken.None,()=>held=new FileStream(info,FileMode.Open,FileAccess.Read,FileShare.None)));}
            finally{held?.Dispose();}
            Assert.That(File.ReadAllBytes(info),Is.EqualTo(damaged));Assert.That(File.ReadAllBytes(Path.Combine(directory,asset.Hash+".image")),Is.EqualTo(asset.Content));Assert.That(Directory.GetFiles(directory,"*.part"),Is.Empty);
        }
        [Test] public void AcceptedSidecarPreservesAValidReplacementMadeDuringPreparation(){
            var asset=ImageLibrary.Inspect("Image",ImageFiles.Png());library.SaveAsync(asset).GetAwaiter().GetResult();string info=Path.Combine(directory,asset.Hash+".txt");File.WriteAllBytes(info,new byte[]{0xc3,0x28});byte[] repaired=Encoding.UTF8.GetBytes("Other accepted label\nOriginal attribution remains\n");
            ImportedAssetSidecar.SaveAccepted(info,"Explicit repair",CancellationToken.None,()=>File.WriteAllBytes(info,repaired));Assert.That(File.ReadAllBytes(info),Is.EqualTo(repaired));Assert.That(Directory.GetFiles(directory,"*.part"),Is.Empty);
        }
        [Test] public void CancelledSameHashSaveKeepsUnusableMetadataAndReleasesItsWriteGate(){
            var asset=ImageLibrary.Inspect("Image",ImageFiles.Png());library.SaveAsync(asset).GetAwaiter().GetResult();string info=Path.Combine(directory,asset.Hash+".txt");byte[] damaged={0xc3,0x28};File.WriteAllBytes(info,damaged);using var stop=new CancellationTokenSource();stop.Cancel();
            Assert.Catch<OperationCanceledException>(()=>library.SaveAsync(asset,stop.Token).GetAwaiter().GetResult());Assert.That(File.ReadAllBytes(info),Is.EqualTo(damaged));Assert.That(gate.CanFreeze(out _),Is.True);Assert.That(Directory.GetFiles(directory,"*.part"),Is.Empty);
        }
        [TestCase(false)][TestCase(true)] public void SidecarDirectoriesAndLinksAreNotCorruptText(bool link){
            var asset=ImageLibrary.Inspect("Image",ImageFiles.Png());library.SaveAsync(asset).GetAwaiter().GetResult();string info=Path.Combine(directory,asset.Hash+".txt"),target=Path.Combine(directory,"unrelated-source.txt");byte[] original=new byte[1025];File.WriteAllBytes(target,original);File.Delete(info);
            if(link)AssetSidecarTestLinks.Create(info,target);else Directory.CreateDirectory(info);
            if(link){Assert.That((File.GetAttributes(info)&FileAttributes.ReparsePoint)!=0,Is.True);Assert.Throws<InvalidDataException>(()=>library.SaveAsync(asset).GetAwaiter().GetResult());}
            else{Assert.Throws<IOException>(()=>library.SaveAsync(asset).GetAwaiter().GetResult());Assert.That(Directory.Exists(info),Is.True);}
            Assert.That(File.ReadAllBytes(target),Is.EqualTo(original));Assert.That(File.ReadAllBytes(Path.Combine(directory,asset.Hash+".image")),Is.EqualTo(asset.Content));Assert.That(Directory.GetFiles(directory,"*.part"),Is.Empty);Assert.That(gate.CanFreeze(out _),Is.True);
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
        [TestCase("metadata")][TestCase("format")][TestCase("hash")][TestCase("locked")] public void PartialInventoryKeepsVerifiedImagesWithoutRepairingFailedEntries(string fault){
            var healthy=ImageLibrary.Inspect("Healthy",ImageFiles.Png());var damaged=ImageLibrary.Inspect("Damaged",ImageFiles.Png(shade:1));library.SaveAsync(healthy).GetAwaiter().GetResult();library.SaveAsync(damaged).GetAwaiter().GetResult();
            string payload=Path.Combine(directory,damaged.Hash+".image"),info=Path.Combine(directory,damaged.Hash+".txt");
            if(fault=="metadata")File.WriteAllBytes(info,new byte[]{0xc3,0x28});if(fault=="format")File.WriteAllBytes(payload,new byte[30]);if(fault=="hash")File.WriteAllBytes(payload,healthy.Content);
            var before=Directory.GetFiles(directory).ToDictionary(Path.GetFileName,File.ReadAllBytes);FileStream held=null;
            try{if(fault=="locked")held=new FileStream(info,FileMode.Open,FileAccess.Read,FileShare.None);var result=library.ScanAsync().GetAwaiter().GetResult();Assert.That(result.Ready&&result.EnumerationComplete,Is.True);Assert.That(result.Complete,Is.False);Assert.That(result.IssueCount,Is.EqualTo(1));Assert.That(result.Entries.Select(e=>e.Hash),Is.EqualTo(new[]{healthy.Hash}));Assert.That(result.Entries[0].Width,Is.EqualTo(3));Assert.That(result.Error,Is.Not.Empty);Assert.Catch(()=>library.ListAsync().GetAwaiter().GetResult());}
            finally{held?.Dispose();}
            foreach(var file in before)Assert.That(File.ReadAllBytes(Path.Combine(directory,file.Key)),Is.EqualTo(file.Value));Assert.That(Directory.GetFiles(directory).Length,Is.EqualTo(before.Count));Assert.That(library.ReadAsync(healthy.Hash).GetAwaiter().GetResult().Content,Is.EqualTo(healthy.Content));Assert.That(gate.CanFreeze(out _),Is.True);
        }
        [Test] public void AllDamagedImagesAreAUsableIncompleteInventoryNotAHealthyEmptyLibrary(){
            var asset=ImageLibrary.Inspect("Damaged",ImageFiles.Png());library.SaveAsync(asset).GetAwaiter().GetResult();File.WriteAllBytes(Path.Combine(directory,asset.Hash+".txt"),new byte[1025]);var result=library.ScanAsync().GetAwaiter().GetResult();Assert.That(result.Ready&&result.EnumerationComplete,Is.True);Assert.That(result.Complete,Is.False);Assert.That(result.Entries,Is.Empty);Assert.That(result.Error,Is.Not.Empty);
        }
        [Test] public void InventoryDoesNotFollowALinkedPayloadOrHideTheHealthyImage(){
            var asset=ImageLibrary.Inspect("Healthy",ImageFiles.Png());library.SaveAsync(asset).GetAwaiter().GetResult();string linked=Path.Combine(directory,new string('f',64)+".image");AssetSidecarTestLinks.Create(linked,Path.Combine(directory,asset.Hash+".image"));
            var result=library.ScanAsync().GetAwaiter().GetResult();Assert.That(result.Entries.Select(e=>e.Hash),Is.EqualTo(new[]{asset.Hash}));Assert.That(result.Complete,Is.False);Assert.That(result.IssueCount,Is.EqualTo(1));Assert.That((File.GetAttributes(linked)&FileAttributes.ReparsePoint)!=0,Is.True);Assert.That(gate.CanFreeze(out _),Is.True);
        }
        [Test] public void CancelledInventoryWaitingForArchiveReleasesItsWorkspaceLease(){
            Assert.That(library.TryCaptureArchive(out var capture),Is.True);using var stop=new CancellationTokenSource();var pending=library.ScanAsync(stop.Token);
            try{Assert.That(pending.IsCompleted,Is.False);stop.Cancel();Assert.That(SpinWait.SpinUntil(()=>pending.IsCompleted,3000),Is.True,"Cancellation must finish while archive ownership remains held");Assert.Catch<OperationCanceledException>(()=>pending.GetAwaiter().GetResult());Assert.That(gate.CanFreeze(out _),Is.True);}
            finally{capture.Dispose();}Assert.That(Directory.Exists(directory),Is.False);
        }
    }
    internal static class AssetSidecarTestLinks
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        [DllImport("kernel32.dll",EntryPoint="CreateSymbolicLinkW",CharSet=CharSet.Unicode,SetLastError=true)]
        [return:MarshalAs(UnmanagedType.I1)]static extern bool Link(string path,string target,uint flags);
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
        [DllImport("libc",EntryPoint="symlink",SetLastError=true)]static extern int Link(string target,string path);
#endif
        internal static void Create(string path,string target)
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if(!Link(path,target,2))Assert.Ignore("This host cannot create the test symlink: "+Marshal.GetLastWin32Error());
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
            if(Link(target,path)!=0)Assert.Ignore("This host cannot create the test symlink: "+Marshal.GetLastWin32Error());
#else
            Assert.Ignore("This test host has no symbolic-link fixture adapter.");
#endif
        }
    }
}
