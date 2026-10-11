// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Threading.Tasks;
using System;
using System.IO;
using Maestro.Quest.Imports;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Maestro.Quest.Persistence;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class WorkspaceWriteGateTests
    {
        [Test] public void CompleteWritesExcludeFreezeAndOnlyTheExactFreezeLeaseReleasesEditing()
        {
            var gate=new WorkspaceWriteGate();var first=gate.Write();var nested=gate.Write();
            Assert.That(gate.TryFreeze(out _),Is.Null);first.Dispose();first.Dispose();Assert.That(gate.TryFreeze(out _),Is.Null);
            nested.Dispose();var hold=gate.TryFreeze(out var error);Assert.That(hold,Is.Not.Null,error);
            Assert.That(gate.TryWrite(out error),Is.Null);StringAssert.Contains("preserved",error);Assert.That(gate.TryFreeze(out _),Is.Null);
            first.Dispose();Assert.That(gate.Frozen,Is.True);hold.Dispose();hold.Dispose();using var next=gate.Write();Assert.That(gate.Frozen,Is.False);
        }
        [Test] public async Task WorkerCompletionCannotLoseAnEditOrClearAnotherOwnersFreeze()
        {
            var gate=new WorkspaceWriteGate();var entered=new TaskCompletionSource<bool>();var finish=new TaskCompletionSource<bool>();
            var worker=Task.Run(async()=>{using var write=gate.Write();entered.SetResult(true);await finish.Task;});
            await entered.Task;Assert.That(gate.TryFreeze(out _),Is.Null);finish.SetResult(true);await worker;
            using var hold=gate.TryFreeze(out var error);Assert.That(hold,Is.Not.Null,error);
            await Task.Run(()=>Assert.That(gate.TryWrite(out _),Is.Null));Assert.That(gate.Frozen,Is.True);
        }
        [Test] public async Task QueuedLibraryWriteAndUnobservedModuleCompletionKeepTheirLease()
        {
            string directory=Path.Combine(Path.GetTempPath(),"WorkspaceWrites-"+Guid.NewGuid().ToString("N"));var gate=new WorkspaceWriteGate();
            try {
                var models=new ModelLibrary(Path.Combine(directory,"models"),gate);var asset=ModelLibrary.Inspect("Example.glb",ModelFixture.Mixamo());
                Assert.That(models.TryCaptureArchive(out var capture),Is.True);Task saving;
                try {saving=models.SaveAsync(asset);Assert.That(saving.IsCompleted,Is.False);Assert.That(gate.TryFreeze(out _),Is.Null);}
                finally {capture.Dispose();}
                await saving;Assert.That((await models.ReadAsync(asset.Hash)).Hash,Is.EqualTo(asset.Hash));using(gate.TryFreeze(out var error)){Assert.That(error,Is.Null);Assert.That(gate.Frozen,Is.True);}
                var modules=new ProgramModuleLibrary(directory,gate);modules.Flush();
                var definition=ProgramModuleLibrary.Definition(BehaviourProgram.FromSteps(new RuleStep {action=RuleActionKind.Wait,seconds=1}),"One pause",new[]{"main"});
                var publication=modules.Publish(definition);await publication.Task;
                Assert.That(publication.Pending,Is.True);Assert.That(gate.TryFreeze(out _),Is.Null,"Disk completion must not outrun owner-thread acceptance");
                modules.Poll();Assert.That(publication.Error,Is.Null);using var held=gate.TryFreeze(out var heldError);Assert.That(held,Is.Not.Null,heldError);
                Assert.Throws<ProgramFault>(()=>modules.Remove(publication.Hash));
            }finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
    }
}
