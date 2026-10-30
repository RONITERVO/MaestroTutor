// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using Maestro.Quest.Art;
using Maestro.Quest.Imports;
using Maestro.Quest.Persistence;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        void AssertImageInUse(string hash)
        {
            Assert.That(editor.WriteGate.Assets.Pinned(ImportedAssetKind.Image,hash),Is.GreaterThan(0));
            using var retirement=editor.WriteGate.Assets.TryRetire(ImportedAssetKind.Image,hash,out var error);
            Assert.That(retirement,Is.Null);Assert.That(error,Does.Contain("in use"));
        }
        [UnityTest] public IEnumerator ImageAssetPinsProtectLoadingCachedAndFailedOwnersUntilLastRelease()
        {
            var asset=ImageLibrary.Inspect("Pinned image",ImageFiles.Png());yield return ImageTask(editor.Images.SaveAsync(asset));
            var cache=editor.ImageTextures;var first=cache.Acquire(asset.Hash);AppearanceImages.Lease second=null;
            try{
                AssertImageInUse(asset.Hash);yield return ImageState(first);Assert.That(first.State,Is.EqualTo("ready"));
                second=cache.Acquire(asset.Hash);Assert.That(second.Texture,Is.SameAs(first.Texture));first.Dispose();AssertImageInUse(asset.Hash);
                second.Dispose();Assert.That(editor.WriteGate.Assets.Pinned(ImportedAssetKind.Image,asset.Hash),Is.Zero);
                using(var retirement=editor.WriteGate.Assets.TryRetire(ImportedAssetKind.Image,asset.Hash,out var error)){
                    Assert.That(retirement,Is.Not.Null,error);Assert.Throws<InvalidOperationException>(()=>cache.Acquire(asset.Hash));Assert.That(cache.Count,Is.Zero);
                }
                using var reopened=cache.Acquire(asset.Hash);yield return ImageState(reopened);Assert.That(reopened.State,Is.EqualTo("ready"),"Scoped retirement leaves the normal read/reimport path available");
            }finally{first.Dispose();second?.Dispose();}
            string missing=new string('e',64);var failed=cache.Acquire(missing);
            try{yield return ImageState(failed);Assert.That(failed.State,Is.EqualTo("failed"));AssertImageInUse(missing);}
            finally{failed.Dispose();}
            using var available=editor.WriteGate.Assets.TryRetire(ImportedAssetKind.Image,missing,out var reason);Assert.That(available,Is.Not.Null,reason);
        }
        [UnityTest] public IEnumerator ImageAssetPinSurvivesRequestedCancellationUntilTheHeldReadActuallyDrains()
        {
            var asset=ImageLibrary.Inspect("Pending image pin",ImageFiles.Png());yield return ImageTask(editor.Images.SaveAsync(asset));
            var cache=editor.ImageTextures;int baseline=(int)ImageResources.ObserveBudget()["payloadReads"];
            var entered=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            editor.Images.ReadCheckpoint=phase=>{if(phase!="reading")return;entered.TrySetResult(true);if(!release.Task.Wait(TimeSpan.FromSeconds(15)))throw new IOException("Image read test checkpoint timed out");};
            AppearanceImages.Lease lease=null;
            try{
                lease=cache.Acquire(asset.Hash);
                yield return PhysicalCreationTestWait.Until(()=>entered.Task.IsCompleted,"The actual image read worker did not enter");
                Assert.That(lease.State,Is.EqualTo("loading"));AssertImageInUse(asset.Hash);
                lease.Dispose();Assert.That(cache.Count,Is.Zero);AssertImageInUse(asset.Hash);
                Assert.That(editor.WriteGate.CanFreeze(out _),Is.False,"Cancellation cannot release a worker that is still inside the read checkpoint");
                release.TrySetResult(true);
                yield return PhysicalCreationTestWait.Until(()=>editor.WriteGate.Assets.Pinned(ImportedAssetKind.Image,asset.Hash)==0,"The cancelled image source remained pinned after work drained");
                Assert.That((int)ImageResources.ObserveBudget()["payloadReads"],Is.EqualTo(baseline));Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);
                using var retirement=editor.WriteGate.Assets.TryRetire(ImportedAssetKind.Image,asset.Hash,out var error);Assert.That(retirement,Is.Not.Null,error);
                Assert.Throws<InvalidOperationException>(()=>cache.Acquire(asset.Hash));Assert.That(lease.Texture,Is.Null);
            }finally{release.TrySetResult(true);editor.Images.ReadCheckpoint=null;lease?.Dispose();}
            yield return null;Assert.That(cache.Count,Is.Zero);Assert.That(cache.ResidentBytes,Is.Zero);
        }
        [UnityTest] public IEnumerator ImageCachedAdmissionStopsAtWorkspaceRetirementAndDestructionReleasesItsPin()
        {
            var gate=new WorkspaceWriteGate();var library=new ImageLibrary(Path.Combine(directory,"retiring-images"),gate);
            var asset=ImageLibrary.Inspect("Retiring image",ImageFiles.Png());yield return ImageTask(library.SaveAsync(asset));
            var images=new GameObject("Retiring image cache");var cache=images.AddComponent<AppearanceImages>();cache.Initialize(library);AppearanceImages.Lease lease=null;
            try{
                lease=cache.Acquire(asset.Hash);yield return ImageState(lease);Assert.That(lease.State,Is.EqualTo("ready"));
                var retiring=gate.Retire();Assert.That(retiring.IsCompleted,Is.False,"The cached image retains its workspace");
                Assert.Throws<InvalidOperationException>(()=>cache.Acquire(asset.Hash),"Cached reuse must still pass workspace admission");
                Assert.Throws<InvalidOperationException>(()=>cache.Acquire(new string('f',64)));
                UnityEngine.Object.Destroy(images);yield return null;
                yield return PhysicalCreationTestWait.Until(()=>retiring.IsCompleted,"Workspace retirement did not drain the image cache");
                Assert.That(retiring.Exception,Is.Null);Assert.That(gate.Assets.Pinned(ImportedAssetKind.Image,asset.Hash),Is.Zero);Assert.That(lease.Texture,Is.Null);Assert.That(lease.State,Is.EqualTo("released"));
            }finally{lease?.Dispose();if(images)UnityEngine.Object.Destroy(images);}
            yield return null;
        }
    }
}
