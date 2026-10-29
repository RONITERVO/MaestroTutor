// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Imports;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Maestro.Quest.Tests
{
    public sealed class GuardedMotionMaintenanceTests
    {
        string directory;
        [SetUp]public void Setup()=>directory=Path.Combine(Path.GetTempPath(),"MaestroGuardedMotions-"+Guid.NewGuid().ToString("N"));
        [TearDown]public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        static IEnumerator Done(Task task){double until=Time.realtimeSinceStartupAsDouble+10;while(!task.IsCompleted&&Time.realtimeSinceStartupAsDouble<until)yield return null;Assert.That(task.IsCompleted,Is.True,"Motion storage did not drain");}
        static MotionMaintenanceRequest Request(MotionLibrary library,string id,string operation)=>new(){operation=operation,motionId=id,entryRevision=library.EntryRevision(id),name="Renamed motion",tags=new[]{"calm"},favourite=true};
        [Test]public void OnlyDestructiveMaintenanceVariantsRequireHumanConfirmation()
        {
            var schema=new MotionMaintenanceCapability().InputSchema;Assert.That(schema["x-confirmation"],Is.Null);
            foreach(var variant in (JArray)schema["oneOf"]){
                string operation=(string)variant["properties"]["operation"]["enum"][0],confirmation=(string)variant["x-confirmation"];
                if(operation is "removeDownload" or "forgetMotion"){
                    Assert.That(confirmation,Does.Contain("no Undo"),operation);Assert.That(confirmation,Does.Contain("Stop cannot roll back"),operation);
                    Assert.That(confirmation,Does.Contain(operation=="removeDownload"?"local download":"identity and details"),operation);
                }else Assert.That(confirmation,Is.Null,operation);
            }
            Assert.That(new MotionMaintenanceCapability(true).InputSchema["x-confirmation"],Is.Null);
        }
        [UnityTest]public IEnumerator StaleMetadataIsRejectedAndCompletedResultKeepsItsAcceptedRevision()
        {
            using var library=new MotionLibrary(directory);var add=library.ImportAsync("motion.glb",ModelFixture.Create());yield return Done(add);var entry=add.Result.Single();
            var stale=Request(library,entry.id,"save");var work=library.EditAsync(stale);yield return Done(work);Assert.That(work.Exception,Is.Null);
            var result=work.Result;Assert.That(result.Entry.id,Is.EqualTo(entry.id));Assert.That(result.Entry.hash,Is.EqualTo(entry.hash));Assert.That(result.PreviousRevision,Is.EqualTo(stale.entryRevision));Assert.That(result.EntryRevision,Is.Not.EqualTo(stale.entryRevision));
            stale.name="Outdated replacement";work=library.EditAsync(stale);yield return Done(work);Assert.That(work.Exception?.GetBaseException().Message,Does.Contain("changed"));Assert.That(library.Inspect(entry.id).name,Is.EqualTo("Renamed motion"));
            work=library.EditAsync(Request(library,entry.id,"archive"));yield return Done(work);Assert.That(work.Exception,Is.Null);Assert.That(result.Entry.archived,Is.False,"A later edit must not rewrite the saved-completion result");
            using var reopened=new MotionLibrary(directory);Assert.That(reopened.EntryRevision(entry.id),Is.EqualTo(library.EntryRevision(entry.id)));Assert.That(reopened.Inspect(entry.id).id,Is.EqualTo(entry.id));
        }
        [UnityTest]public IEnumerator SerializedWaitRechecksRevisionAndCancellationBeforeDispatchPreservesFiles()
        {
            using var library=new MotionLibrary(directory);var add=library.ImportAsync("motion.glb",ModelFixture.Create());yield return Done(add);string id=add.Result.Single().id;
            var held=new TaskCompletionSource<string>();var first=library.EditAsync(Request(library,id,"archive"),()=>held.Task);
            var stale=library.EditAsync(Request(library,id,"save"));using var stop=new CancellationTokenSource();var cancelled=library.EditAsync(Request(library,id,"save"),cancellation:stop.Token);stop.Cancel();yield return Done(cancelled);Assert.That(cancelled.IsCanceled,Is.True);
            held.SetResult(null);yield return Done(first);yield return Done(stale);Assert.That(first.Exception,Is.Null);Assert.That(stale.Exception?.GetBaseException().Message,Does.Contain("changed"));
            string before=File.ReadAllText(Path.Combine(directory,"motions.v2.json"));using var cancelReady=new CancellationTokenSource();library.MaintenanceCheckpoint=phase=>{if(phase=="ready")cancelReady.Cancel();};
            var remove=library.EditAsync(Request(library,id,"removeDownload"),()=>Task.FromResult<string>(null),cancelReady.Token);yield return Done(remove);Assert.That(remove.IsCanceled,Is.True);Assert.That(library.Downloaded(id),Is.True);Assert.That(library.Find(id),Is.Not.Null);Assert.That(File.ReadAllText(Path.Combine(directory,"motions.v2.json")),Is.EqualTo(before));
        }
        [UnityTest]public IEnumerator StopAfterPublicationDispatchDrainsAndReturnsExactSavedRemoval()
        {
            var writes=new WorkspaceWriteGate();using var library=new MotionLibrary(directory,writes);var add=library.ImportAsync("motion.glb",ModelFixture.Create());yield return Done(add);string id=add.Result.Single().id;
            var archive=library.ArchiveAsync(id,true);yield return Done(archive);using var stop=new CancellationTokenSource();
            using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();library.MaintenanceCheckpoint=phase=>{if(phase=="publishing"){entered.Set();if(!release.Wait(10000))throw new TimeoutException();}};
            var work=library.EditAsync(Request(library,id,"removeDownload"),()=>Task.FromResult<string>(null),stop.Token);
            double until=Time.realtimeSinceStartupAsDouble+10;while(!entered.IsSet&&Time.realtimeSinceStartupAsDouble<until)yield return null;
            Task retirement=null;
            try{Assert.That(entered.IsSet,Is.True);stop.Cancel();retirement=writes.Retire();Assert.That(retirement.IsCompleted,Is.False,"Retirement must retain the old library until its dispatched IO drains");Assert.That(work.IsCompleted,Is.False);Assert.That(library.Find(id),Is.Null,"The accepted deletion retains its identity reservation while draining");Assert.That(writes.TryWrite(out _),Is.Null,"A retired workspace cannot accept another edit");}finally{release.Set();}
            yield return Done(work);yield return Done(retirement);Assert.That(work.Exception,Is.Null);Assert.That(work.IsCanceled,Is.False);Assert.That(work.Result.Entry.removed,Is.True);Assert.That(work.Result.Downloaded,Is.False);Assert.That(library.PayloadPresent(id),Is.False);
        }
        [UnityTest]public IEnumerator FavouriteOnlyEditPreservesExactImportedNameTagsAndLiteralPunctuation()
        {
            using var library=new MotionLibrary(directory);var add=library.ImportAsync("motion.glb",ModelFixture.Mixamo(json=>json["animations"][0]["name"]="  Imported motion  "));yield return Done(add);var entry=add.Result.Single();Assert.That(entry.name,Is.EqualTo("  Imported motion  "));
            var first=Request(library,entry.id,"save");first.name=entry.name;first.tags=new[]{" calm ","two,parts","<quiet>"};first.favourite=true;var work=library.EditAsync(first);yield return Done(work);Assert.That(work.Exception,Is.Null);Assert.That(work.Result.Entry.name,Is.EqualTo(entry.name));Assert.That(work.Result.Entry.tags,Is.EqualTo(first.tags));
            var reviewed=library.Inspect(entry.id);var favourite=Request(library,entry.id,"save");favourite.name=reviewed.name;favourite.tags=reviewed.tags;favourite.favourite=false;work=library.EditAsync(favourite);yield return Done(work);Assert.That(work.Exception,Is.Null);Assert.That(library.Inspect(entry.id).name,Is.EqualTo(entry.name));Assert.That(library.Inspect(entry.id).tags,Is.EqualTo(first.tags));Assert.That(library.Inspect(entry.id).favourite,Is.False);
            var invalid=Request(library,entry.id,"save");invalid.tags=new[]{"calm","CALM"};work=library.EditAsync(invalid);yield return Done(work);Assert.That(work.Exception,Is.Not.Null);Assert.That(library.Inspect(entry.id).tags,Is.EqualTo(first.tags));
        }
        [UnityTest]public IEnumerator PublicationFailurePreservesPayloadAndBlocksFurtherWritesUntilInspectionAfterRestart()
        {
            using var library=new MotionLibrary(directory);var add=library.ImportAsync("motion.glb",ModelFixture.Create());yield return Done(add);string id=add.Result.Single().id;
            var archive=library.ArchiveAsync(id,true);yield return Done(archive);string primary=Path.Combine(directory,"motions.v2.json"),before=File.ReadAllText(primary);
            Directory.CreateDirectory(primary+".pending");
            var work=library.EditAsync(Request(library,id,"removeDownload"),()=>Task.FromResult<string>(null));yield return Done(work);Assert.That(work.Exception?.GetBaseException().Message,Does.Contain("not confirmed"));Assert.That(library.ReadOnly,Is.True);Assert.That(library.PayloadPresent(id),Is.True);Assert.That(File.ReadAllText(primary),Is.EqualTo(before));
            var rejected=library.EditAsync(Request(library,id,"save"));yield return Done(rejected);Assert.That(rejected.Exception,Is.Not.Null);
            Directory.Delete(primary+".pending");using var reopened=new MotionLibrary(directory);Assert.That(reopened.ReadOnly,Is.False);Assert.That(reopened.Downloaded(id),Is.True);
        }
        [UnityTest]public IEnumerator GuardedForgetRetainsIdentityUntilTombstoneAndFinalProtectionPass()
        {
            using var library=new MotionLibrary(directory);var add=library.ImportAsync("motion.glb",ModelFixture.Create());yield return Done(add);string id=add.Result.Single().id;
            var archive=library.ArchiveAsync(id,true);yield return Done(archive);var removed=library.EditAsync(Request(library,id,"removeDownload"),()=>Task.FromResult<string>(null));yield return Done(removed);Assert.That(removed.Exception,Is.Null);
            var forgotten=library.EditAsync(Request(library,id,"forgetMotion"),()=>Task.FromResult("A saved module still references this motion"));yield return Done(forgotten);Assert.That(forgotten.Exception,Is.Not.Null);Assert.That(library.Inspect(id),Is.Not.Null);
            forgotten=library.EditAsync(Request(library,id,"forgetMotion"),()=>Task.FromResult<string>(null));yield return Done(forgotten);Assert.That(forgotten.Exception,Is.Null);Assert.That(forgotten.Result.Entry,Is.Null);Assert.That(forgotten.Result.EntryRevision,Is.Empty);Assert.That(library.Inspect(id),Is.Null);
            add=library.ImportAsync("motion.glb",ModelFixture.Create());yield return Done(add);Assert.That(add.Result.Single().id,Is.Not.EqualTo(id));
        }
    }
}
