// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Persistence;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class LatestCheckpointTests
    {
        static async Task Finish(Task task){Assert.AreSame(task,await Task.WhenAny(task,Task.Delay(5000)),"Checkpoint did not drain");await task;}
        [Test] public async Task WaitingSnapshotsCoalesceAndNeverOvertakeThePreviousWriter()
        {
            var prior=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);var saved=new List<string>();
            var save=new LatestCheckpoint<string>(prior.Task,"first",s=>{saved.Add(s);return null;});
            try{
                for(int i=0;i<100;i++)Assert.IsTrue(save.TryQueue("latest "+i));
                Assert.IsFalse(save.Completion.IsCompleted);Assert.IsEmpty(saved);
                prior.SetResult(true);await Finish(save.Completion);
                CollectionAssert.AreEqual(new[]{"latest 99"},saved);Assert.IsNull(await save.Completion);
                Assert.IsFalse(save.TryQueue("too late"));
            }finally{prior.TrySetResult(true);}
        }
        [Test] public async Task RunningSnapshotIsImmutableAndOnlyTheNewestSuccessorIsSaved()
        {
            var started=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var release=new ManualResetEventSlim();var saved=new List<string>();
            var save=new LatestCheckpoint<string>(null,"running",s=>{
                saved.Add(s);if(s=="running"){started.TrySetResult(true);if(!release.Wait(5000))return "watchdog";}return null;
            });
            try{
                await Finish(started.Task);Assert.IsTrue(save.TryQueue("superseded"));Assert.IsTrue(save.TryQueue("newest"));
                Assert.IsFalse(save.Completion.IsCompleted);release.Set();await Finish(save.Completion);
                CollectionAssert.AreEqual(new[]{"running","newest"},saved);Assert.IsNull(await save.Completion);
            }finally{release.Set();await Finish(save.Completion);}
        }
        [Test] public async Task FailureIsNotReportedAsSavedAndAClosedQueueCannotAcceptAnotherSnapshot()
        {
            var save=new LatestCheckpoint<string>(Task.FromException(new Exception("older failed")),"current",_=>"disk unavailable");
            await Finish(save.Completion);Assert.AreEqual("disk unavailable",await save.Completion);
            Assert.IsFalse(save.TryQueue("later"));
            var retry=new LatestCheckpoint<string>(save.Completion,"later",_=>null);await Finish(retry.Completion);Assert.IsNull(await retry.Completion);
        }
        [Test] public async Task UnexpectedWriterFailureBecomesAnUnconfirmedResult()
        {
            var save=new LatestCheckpoint<string>(null,"current",_=>throw new InvalidOperationException("private path"));
            await Finish(save.Completion);Assert.That(await save.Completion,Does.Contain("unconfirmed").And.Not.Contain("private path"));
        }
    }
}
