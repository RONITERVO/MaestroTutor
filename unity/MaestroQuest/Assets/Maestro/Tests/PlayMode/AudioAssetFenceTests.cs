// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Persistence;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        void AssertAudioPinned(string hash)
        {
            Assert.That(editor.WriteGate.Assets.Pinned(ImportedAssetKind.Audio,hash),Is.GreaterThan(0));
            using var retirement=editor.WriteGate.Assets.TryRetire(ImportedAssetKind.Audio,hash,out var error);
            Assert.That(retirement,Is.Null);Assert.That(error,Does.Contain("in use"));
        }
        static IEnumerator AudioFenceTask(Task task)
        {
            yield return PhysicalCreationTestWait.Until(()=>task.IsCompleted,"Audio fence operation did not drain");
            Assert.That(task.IsCanceled,Is.False);Assert.That(task.Exception,Is.Null,task.Exception?.ToString());
        }
        [UnityTest] public IEnumerator AudioPreparedResidentAndStreamSourcesStayPinnedThroughClose()
        {
            foreach(bool streaming in new[]{false,true}){
                var asset=AudioLibrary.Inspect("Pinned canonical sound",streaming?LongSoundFile():SoundFile());
                yield return AudioFenceTask(editor.Sounds.SaveAsync(asset));AudioPcmAsset prepared=null;
                try{
                    var task=editor.Sounds.PrepareAsync(asset.Hash,streaming?32:1);yield return AudioFenceTask(task);prepared=task.Result;
                    AssertAudioPinned(asset.Hash);Assert.That(prepared.Frames,Is.EqualTo((streaming?32:1)*WaveAudio.OutputRate));
                    if(streaming){Assert.That(prepared.ResidentSamples,Is.Null);Assert.That(prepared.CapacityBytes,Is.LessThan(prepared.Frames*2));}
                    else Assert.That(prepared.ResidentSamples,Has.Length.EqualTo(WaveAudio.OutputRate));
                    Assert.That(editor.WriteGate.CanFreeze(out _),Is.True,"Preservation can coexist with imported sound readers");
                    using(var freeze=editor.WriteGate.TryFreeze(out var error))Assert.That(freeze,Is.Not.Null,error);
                    yield return AudioFenceTask(prepared.Close());prepared=null;
                    Assert.That(editor.WriteGate.Assets.Pinned(ImportedAssetKind.Audio,asset.Hash),Is.Zero);
                    using var retirement=editor.WriteGate.Assets.TryRetire(ImportedAssetKind.Audio,asset.Hash,out var reason);
                    Assert.That(retirement,Is.Not.Null,reason);var refused=editor.Sounds.PrepareAsync(asset.Hash,streaming?32:1);
                    Assert.That(refused.IsCompleted,Is.True);Assert.Throws<InvalidOperationException>(()=>refused.GetAwaiter().GetResult());
                }finally{if(prepared!=null)_=prepared.Close();}
            }
        }
        [UnityTest] public IEnumerator AudioCancellationDoesNotReleaseAWorkerStillReadingItsPayload()
        {
            var asset=AudioLibrary.Inspect("Draining sound",SoundFile());yield return AudioFenceTask(editor.Sounds.SaveAsync(asset));
            var entered=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);using var cancel=new CancellationTokenSource();
            editor.Sounds.ReadCheckpoint=phase=>{if(phase!="decoding")return;entered.TrySetResult(true);if(!release.Task.Wait(TimeSpan.FromSeconds(15)))throw new IOException("Audio worker checkpoint timed out");};
            Task<AudioPcmAsset> task=null;
            try{
                task=editor.Sounds.PrepareAsync(asset.Hash,1,cancel.Token);yield return PhysicalCreationTestWait.Until(()=>entered.Task.IsCompleted,"Audio decode did not start");
                cancel.Cancel();Assert.That(task.IsCompleted,Is.False);AssertAudioPinned(asset.Hash);
                Assert.That(editor.WriteGate.CanFreeze(out _),Is.False,"Actual decoding has not drained");
                release.TrySetResult(true);yield return PhysicalCreationTestWait.Until(()=>task.IsCompleted,"Cancelled audio decode did not drain");
                Assert.That(task.IsCanceled,Is.True);Assert.That(editor.WriteGate.Assets.Pinned(ImportedAssetKind.Audio,asset.Hash),Is.Zero);Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);
            }finally{release.TrySetResult(true);editor.Sounds.ReadCheckpoint=null;if(task?.Status==TaskStatus.RanToCompletion)_=task.Result.Close();}
        }
        sealed class DelayedAudioClose:AudioPcmAsset
        {
            internal readonly TaskCompletionSource<bool> Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Released=new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal override int Frames=>24000;
            internal override long BufferedBytes=>0;
            internal override long CapacityBytes=>48000;
            internal override int Read(int frame,short[] destination,int count)=>0;
            internal override Task Close(){Entered.TrySetResult(true);return Released.Task;}
        }
        [UnityTest] public IEnumerator AudioDirectRetainedSourceReleasesItsPinOnlyAfterIdempotentCloseDrains()
        {
            string hash=new string('c',64);var source=new DelayedAudioClose();var retained=AudioPcmAsset.Retained(source,editor.WriteGate.Assets.Pin(ImportedAssetKind.Audio,hash));
            try{
                var closing=retained.Close();Assert.That(retained.Close(),Is.SameAs(closing));Assert.That(source.Entered.Task.IsCompleted,Is.True);
                Assert.That(closing.IsCompleted,Is.False);AssertAudioPinned(hash);source.Released.TrySetResult(true);
                yield return AudioFenceTask(closing);Assert.That(editor.WriteGate.Assets.Pinned(ImportedAssetKind.Audio,hash),Is.Zero);
                Assert.That(retained.Close(),Is.SameAs(closing));
            }finally{source.Released.TrySetResult(true);_=retained.Close();}
        }
        [UnityTest] public IEnumerator AudioReservationPinSurvivesLastOwnerAndCancelledLoaderUntilCloseDrains()
        {
            yield return AudioDrained();string hash=new string('a',64);var source=new RoomAudioDefinition{id=Guid.NewGuid().ToString("N"),kind="clip",assetHash=hash,seconds=1};
            var work=new TaskCompletionSource<AudioPcmAsset>(TaskCreationOptions.RunContinuationsAsynchronously);var asset=new DelayedAudioClose();
            var lease=AudioReservations.AcquireSource(null,source,new RoomResourceOwner(editor.WorldIdentity,"book","audio"),Guid.NewGuid().ToString("N"),"sound",1,_=>work.Task,editor.WriteGate.Assets);
            try{
                AssertAudioPinned(hash);lease.Dispose();AssertAudioPinned(hash);
                work.TrySetResult(asset);yield return PhysicalCreationTestWait.Until(()=>asset.Entered.Task.IsCompleted,"Cancelled preparation did not close the late source");
                AssertAudioPinned(hash);Assert.That((int)AudioReservations.ObserveBudget()["owners"],Is.Zero);
                asset.Released.TrySetResult(true);yield return AudioDrained();Assert.That(editor.WriteGate.Assets.Pinned(ImportedAssetKind.Audio,hash),Is.Zero);
            }finally{lease.Dispose();work.TrySetResult(asset);asset.Released.TrySetResult(true);}
        }
        [UnityTest] public IEnumerator AudioFailedAndCachedUsersProtectTheirExactWorkspaceUntilReleased()
        {
            yield return AudioDrained();var gate=new WorkspaceWriteGate();string hash=new string('b',64);var source=new RoomAudioDefinition{id=Guid.NewGuid().ToString("N"),kind="clip",assetHash=hash,seconds=1};
            var owner=new RoomResourceOwner(editor.WorldIdentity,"book","audio");AudioReservations.Lease first=null,second=null;
            try{
                first=AudioReservations.Acquire(null,source,owner,Guid.NewGuid().ToString("N"),"sound",1,_=>Task.FromResult(new short[24000]),gate.Assets);
                second=AudioReservations.Acquire(first.Resource,source,owner,Guid.NewGuid().ToString("N"),"sound",1,_=>throw new Exception("Cached source must not decode again"),gate.Assets);
                Assert.That(second.Resource,Is.SameAs(first.Resource));var other=new WorkspaceWriteGate();
                Assert.Throws<InvalidOperationException>(()=>AudioReservations.Acquire(first.Resource,source,owner,Guid.NewGuid().ToString("N"),"sound",1,_=>Task.FromResult(new short[24000]),other.Assets));
                Assert.That(other.Assets.Pinned(ImportedAssetKind.Audio,hash),Is.Zero);
                var closing=gate.Retire();Assert.That(closing.IsCompleted,Is.False);
                Assert.Throws<InvalidOperationException>(()=>AudioReservations.Acquire(first.Resource,source,owner,Guid.NewGuid().ToString("N"),"sound",1,_=>Task.FromResult(new short[24000]),gate.Assets));
                first.Dispose();Assert.That(gate.Assets.Pinned(ImportedAssetKind.Audio,hash),Is.GreaterThan(0));second.Dispose();yield return AudioDrained();yield return AudioFenceTask(closing);
                Assert.That(gate.Assets.Pinned(ImportedAssetKind.Audio,hash),Is.Zero);
            }finally{first?.Dispose();second?.Dispose();}
            var failed=AudioReservations.Acquire(null,source,owner,Guid.NewGuid().ToString("N"),"sound",1,_=>Task.FromException<short[]>(new IOException("Expected clip failure")),editor.WriteGate.Assets);
            try{Assert.That(failed.Resource.Pending.IsFaulted,Is.True);AssertAudioPinned(hash);}finally{failed.Dispose();}
            yield return AudioDrained();Assert.That(editor.WriteGate.Assets.Pinned(ImportedAssetKind.Audio,hash),Is.Zero);
        }
        [UnityTest] public IEnumerator ImportedLoopingPausedAndRoomOwnedVoicesKeepTheirSharedPin()
        {
            yield return AudioDrained();string source=null;yield return LongSound(value=>source=value);string hash=editor.ReadAudio(source).assetHash,target=editor.Identity(block);
            WorldEmitter("book",source);WorldEmitter(target,source);var audio=WorldAudio.For(editor);
            Assert.That(audio.Begin("book","sound",out var first,out var error,true),Is.True,error);WorldAudio.Instance second=null;
            try{
                yield return PhysicalCreationTestWait.Until(()=>first.Cursor>=.1||first.Closed,"Imported sound did not consume its first samples");
                Assert.That(first.Closed,Is.False,first.Error);Assert.That(first.Cursor,Is.GreaterThan(0));Assert.That(first.Output,Is.Not.Null,first.Error);
                Assert.That(audio.Handoff(first,out error),Is.True,error);Assert.That(audio.Control("book",first.Id,first.Revision,"pause",0,out _,out error),Is.True,error);AssertAudioPinned(hash);
                Assert.That(audio.Begin(target,"sound",out second,out error,true),Is.True,error);
                yield return PhysicalCreationTestWait.Until(()=>second.Output||second.Closed,"Cached imported sound did not become ready");Assert.That(second.Output,Is.Not.Null,second.Error);
                Assert.That(second.Resource.Resource,Is.SameAs(first.Resource.Resource));AssertAudioPinned(hash);
                audio.Cancel(first);Assert.That(first.Closed,Is.True);Assert.That(second.Closed,Is.False);AssertAudioPinned(hash);
                audio.Cancel(second);yield return AudioDrained();Assert.That(editor.WriteGate.Assets.Pinned(ImportedAssetKind.Audio,hash),Is.Zero);
            }finally{audio.Cancel(first);if(second!=null)audio.Cancel(second);}
        }
    }
}
