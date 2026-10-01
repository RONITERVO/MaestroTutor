// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Imports;
using NUnit.Framework;

namespace Maestro.Quest.Tests
{
    public sealed class MotionBatchTests
    {
        string directory;
        [SetUp] public void Setup() => directory=Path.Combine(Path.GetTempPath(),"MaestroBatch-"+Guid.NewGuid().ToString("N"));
        [TearDown] public void Cleanup() { if (Directory.Exists(directory)) Directory.Delete(directory,true); }
        sealed class Source : IMotionBatchSource
        {
            public int Count { get; }
            public string Name(int index) => "selected-"+index+".glb";
            public readonly int[] Reads;
            public Func<int,CancellationToken,Task<MotionBatchInput>> Read;
            public MotionBatchInput Previous;
            public bool Disposed;
            public Source(int count) { Count=count; Reads=new int[count]; }
            public async Task<MotionBatchInput> ReadAsync(int index,CancellationToken cancellation)
            {
                Assert.That(Disposed,Is.False);
                Assert.That(Previous?.Bytes,Is.Null,"A previous source payload must be released before the next is opened");
                Reads[index]++; Previous=await Read(index,cancellation); return Previous;
            }
            public void Dispose() => Disposed=true;
        }
        static MotionBatchInput Input(string name,byte[] bytes) => new(name,bytes);
        [Test] public void MixedBatchKeepsGoodFilesDeduplicatesAndRetriesOnlyFailures() => Task.Run(async () =>
        {
            using var library=new MotionLibrary(directory);
            using var source=new Source(3); var original=ModelFixture.TranslationMotion("LINEAR"); var before=original.ToArray(); bool fixedExport=false;
            source.Read=(i,_) => Task.FromResult(Input("selected-"+i+".glb",i == 1 ? fixedExport ? ModelFixture.TranslationMotion("STEP") : new byte[8] : original));
            using var batch=new MotionBatch(library,source); batch.SetCategory("gesture");
            Assert.That(source.Reads.Sum(),Is.Zero,"Selecting files is not confirmation to read or save");
            await batch.RunAsync(); Assert.That(batch.Saved,Is.EqualTo(2)); Assert.That(batch.Failed,Is.EqualTo(1));
            var first=library.List().Single(); Assert.That(first.tags,Contains.Item("gesture")); Assert.That(batch.Results[0].MotionIds,Is.EqualTo(batch.Results[2].MotionIds));
            Assert.That(library.ResidentClipCount,Is.Zero,"Imports must not compile hundreds of resident clips"); Assert.That(original,Is.EqualTo(before));
            await library.UpdateAsync(first.id,"User label",new[] { "my category" },true);
            batch.SetCategory("different"); Assert.That(batch.Category,Is.EqualTo("gesture"));
            await batch.RunAsync(); Assert.That(source.Reads,Is.EqualTo(new[] {1,1,1}));
            fixedExport=true; await batch.RunAsync(true);
            Assert.That(source.Reads,Is.EqualTo(new[] {1,2,1})); Assert.That(batch.Failed,Is.Zero); Assert.That(batch.Saved,Is.EqualTo(3));
            Assert.That(library.List().Length,Is.EqualTo(2)); Assert.That(library.Find(first.id).name,Is.EqualTo("User label")); Assert.That(library.Find(first.id).favourite,Is.True);
            Assert.That(source.Previous.Bytes,Is.Null);
        }).GetAwaiter().GetResult();
        [Test] public void StopDuringReadLeavesTheFileWaitingAndResumeDoesNotReplayCompletedFiles() => Task.Run(async () =>
        {
            using var library=new MotionLibrary(directory); using var source=new Source(3); using var batch=new MotionBatch(library,source); bool stop=true;
            source.Read=(i,token) => {
                if (i == 1 && stop) { stop=false; batch.Stop(); token.ThrowIfCancellationRequested(); }
                return Task.FromResult(Input(i+".glb",ModelFixture.TranslationMotion("LINEAR",i+1)));
            };
            await batch.RunAsync(); Assert.That(batch.Saved,Is.EqualTo(1)); Assert.That(batch.Pending,Is.EqualTo(2)); Assert.That(batch.Failed,Is.Zero);
            Assert.That(source.Reads,Is.EqualTo(new[] {1,1,0})); string id=batch.Results[0].MotionIds.Single();
            await batch.RunAsync(); Assert.That(batch.Saved,Is.EqualTo(3)); Assert.That(source.Reads,Is.EqualTo(new[] {1,2,1}));
            Assert.That(batch.Results[0].MotionIds.Single(),Is.EqualTo(id));
        }).GetAwaiter().GetResult();
        [Test] public void StopDuringSaveFinishesExactlyThatFileAndDisposalCannotStartAnother() => Task.Run(async () =>
        {
            using var library=new MotionLibrary(directory); using var source=new Source(3); using var batch=new MotionBatch(library,source);
            source.Read=(i,_) => Task.FromResult(Input(i+".glb",ModelFixture.Create()));
            bool stopped=false;
            batch.Changed+=() => { if (!stopped && batch.Results[0].State == MotionBatchState.Importing) { stopped=true; batch.Stop(); } };
            await batch.RunAsync(); Assert.That(batch.Saved,Is.EqualTo(1)); Assert.That(batch.Pending,Is.EqualTo(2)); Assert.That(source.Reads,Is.EqualTo(new[] {1,0,0}));
            batch.Dispose(); await batch.RunAsync(); Assert.That(source.Disposed,Is.True); Assert.That(source.Reads,Is.EqualTo(new[] {1,0,0}));
        }).GetAwaiter().GetResult();
        [Test] public void RejectedAndOversizedLocalFilesDoNotBlockLaterFilesOrAlterOriginals() => Task.Run(async () =>
        {
            Directory.CreateDirectory(directory); string large=Path.Combine(directory,"large.glb"),good=Path.Combine(directory,"good.glb");
            using (var file=File.Create(large)) file.SetLength(64L*1024*1024+1);
            byte[] bytes=ModelFixture.Mixamo(); File.WriteAllBytes(good,bytes);
            using var library=new MotionLibrary(Path.Combine(directory,"library")); using var source=new LocalMotionBatchSource(new[] {large,good}); using var batch=new MotionBatch(library,source);
            await batch.RunAsync(); Assert.That(batch.Failed,Is.EqualTo(1)); Assert.That(batch.Saved,Is.EqualTo(1)); Assert.That(batch.Results[0].Error,Is.Not.Empty); Assert.That(batch.Results[0].Name,Is.EqualTo("large.glb"));
            Assert.That(new FileInfo(large).Length,Is.EqualTo(64L*1024*1024+1)); Assert.That(File.ReadAllBytes(good),Is.EqualTo(bytes));
            using var restart=new MotionLibrary(Path.Combine(directory,"library")); Assert.That(restart.List().Single().id,Is.EqualTo(batch.Results[1].MotionIds.Single()));
        }).GetAwaiter().GetResult();
        [Test] public void DisposeKeepsSourceUntilAnAcceptedReadHasDrained()=>Task.Run(async()=>
        {
            using var library=new MotionLibrary(directory);var source=new Source(1);var gate=new TaskCompletionSource<bool>();using var batch=new MotionBatch(library,source);
            source.Read=async(i,token)=>{await gate.Task;Assert.That(source.Disposed,Is.False);return Input("held.glb",ModelFixture.Mixamo());};
            var run=batch.RunAsync();Assert.That(source.Reads[0],Is.EqualTo(1));batch.Dispose();Assert.That(source.Disposed,Is.False);Assert.That(run.IsCompleted,Is.False);
            gate.SetResult(true);await run;Assert.That(source.Disposed,Is.True);Assert.That(source.Previous.Bytes,Is.Null);Assert.That(library.List(),Is.Empty);
        }).GetAwaiter().GetResult();
        [Test] public void SelectionLimitsAndCancelledRetryKeepPriorFailure() => Task.Run(async () =>
        {
            using var library=new MotionLibrary(directory);
            using var empty=new Source(0); using var huge=new Source(129);
            Assert.Throws<ModelImportException>(() => new MotionBatch(library,empty)); Assert.Throws<ModelImportException>(() => new MotionBatch(library,huge));
            using var source=new Source(1); using var batch=new MotionBatch(library,source); bool retry=false;
            source.Read=(i,token) => { if (retry) { batch.Stop(); token.ThrowIfCancellationRequested(); } throw new ModelImportException("Export was incomplete."); };
            await batch.RunAsync(); string error=batch.Results[0].Error; retry=true; await batch.RunAsync(true);
            Assert.That(batch.Failed,Is.EqualTo(1)); Assert.That(batch.Results[0].Error,Is.EqualTo(error)); Assert.That(batch.Pending,Is.Zero);
        }).GetAwaiter().GetResult();
    }
}
