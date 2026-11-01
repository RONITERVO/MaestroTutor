// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Persistence;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class WorkspaceAssetFenceTests
    {
        const ImportedAssetKind Image=ImportedAssetKind.Image;
        static string Hash(char value='a')=>new(value,64);
        [Test] public void PinsAndRetirementsAreExactScopedAndReleasedOnlyByTheirOwnLease()
        {
            var first=new WorkspaceWriteGate();var second=new WorkspaceWriteGate();string hash=Hash();
            var one=first.Assets.Pin(Image,hash);var two=first.Assets.Pin(Image,hash);
            try{
                Assert.That(first.Assets.Pinned(Image,hash),Is.EqualTo(2));
                using var blocked=first.Assets.TryRetire(Image,hash,out var error);Assert.That(blocked,Is.Null);Assert.That(error,Does.Contain("in use"));
                using var otherWorkspace=second.Assets.TryRetire(Image,hash,out error);Assert.That(otherWorkspace,Is.Not.Null,error);
                using var otherKind=first.Assets.TryRetire(ImportedAssetKind.Audio,hash,out error);Assert.That(otherKind,Is.Not.Null,error);
                using var otherHash=first.Assets.TryRetire(Image,Hash('b'),out error);Assert.That(otherHash,Is.Not.Null,error);
                one.Dispose();one.Dispose();Assert.That(first.Assets.Pinned(Image,hash),Is.EqualTo(1));two.Dispose();
                var retirement=first.Assets.TryRetire(Image,hash,out error);Assert.That(retirement,Is.Not.Null,error);
                try{
                    using var refused=first.Assets.TryPin(Image,hash,out error);Assert.That(refused,Is.Null);Assert.That(error,Does.Contain("being removed"));
                    using var duplicate=first.Assets.TryRetire(Image,hash,out _);Assert.That(duplicate,Is.Null);
                }finally{retirement.Dispose();}
                using var next=first.Assets.TryRetire(Image,hash,out error);Assert.That(next,Is.Not.Null,error);
                retirement.Dispose();using var stillRefused=first.Assets.TryPin(Image,hash,out _);Assert.That(stillRefused,Is.Null,"An old lease cannot release another transaction");
            }finally{one.Dispose();two.Dispose();}
            using var reopened=first.Assets.Pin(Image,hash);Assert.That(first.Assets.Pinned(Image,hash),Is.EqualTo(1),"A completed or failed scoped transaction leaves no tombstone");
        }
        [Test] public async Task ConcurrentPinAndRetirementAdmissionCannotBothWin()
        {
            for(int attempt=0;attempt<32;attempt++){
                var gate=new WorkspaceWriteGate();using var start=new ManualResetEventSlim(false);IDisposable pin=null,retirement=null;
                try{
                    var reader=Task.Run(()=>{start.Wait();pin=gate.Assets.TryPin(Image,Hash(),out _);});
                    var remover=Task.Run(()=>{start.Wait();retirement=gate.Assets.TryRetire(Image,Hash(),out _);});
                    start.Set();await Task.WhenAll(reader,remover);
                    Assert.That((pin!=null)^(retirement!=null),Is.True,"Exactly one admission wins while its lease remains held");
                    Assert.That(gate.Assets.Pinned(Image,Hash()),Is.EqualTo(pin==null?0:1));
                }finally{start.Set();pin?.Dispose();retirement?.Dispose();}
                using var next=gate.Assets.TryRetire(Image,Hash(),out var error);Assert.That(next,Is.Not.Null,error);
            }
        }
        [Test] public async Task PreservationAllowsPinsButWorkspaceRetirementWaitsForTheirActualRelease()
        {
            var gate=new WorkspaceWriteGate();var pin=gate.Assets.Pin(Image,Hash());
            try{
                using(var frozen=gate.TryFreeze(out var error)){
                    Assert.That(frozen,Is.Not.Null,error);using var cached=gate.Assets.Pin(Image,Hash());Assert.That(gate.Assets.Pinned(Image,Hash()),Is.EqualTo(2));
                }
                var draining=gate.Retire();Assert.That(draining.IsCompleted,Is.False);
                using var refused=gate.Assets.TryPin(Image,Hash(),out var reason);Assert.That(refused,Is.Null);Assert.That(reason,Does.Contain("closing"));
                pin.Dispose();await draining;Assert.That(gate.Assets.Pinned(Image,Hash()),Is.Zero);
            }finally{pin.Dispose();}
        }
        [Test] public void FailedTransactionReleasesItsAdmissionFenceAndInvalidKeysCannotConsumePins()
        {
            var gate=new WorkspaceWriteGate();
            Assert.Throws<InvalidOperationException>(()=>{using var retirement=gate.Assets.TryRetire(Image,Hash(),out _);throw new InvalidOperationException("No file was removed");});
            using var recovered=gate.Assets.Pin(Image,Hash());Assert.That(gate.Assets.Pinned(Image,Hash()),Is.EqualTo(1));
            Assert.Throws<ArgumentException>(()=>gate.Assets.Pin(Image,"not-a-hash"));Assert.Throws<ArgumentException>(()=>gate.Assets.TryRetire((ImportedAssetKind)99,Hash(),out _));
            Assert.That(gate.Assets.Pinned(Image,Hash()),Is.EqualTo(1));
        }
    }
}
