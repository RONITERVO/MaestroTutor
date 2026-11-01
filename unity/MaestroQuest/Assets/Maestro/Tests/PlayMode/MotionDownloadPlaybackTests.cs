// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Quest.Imports;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed class MotionDownloadPlaybackTests
    {
        string directory;
        [SetUp] public void Setup() => directory=Path.Combine(Path.GetTempPath(),"MaestroMaintenance-"+Guid.NewGuid().ToString("N"));
        [TearDown] public void Cleanup() { if (Directory.Exists(directory)) Directory.Delete(directory,true); }
        static IEnumerator Done(Task task)
        {
            double deadline=Time.realtimeSinceStartupAsDouble+10;
            while (!task.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(task.IsCompleted,Is.True,"Storage operation timed out");
        }
        [UnityTest] public IEnumerator ColdAndActiveLeasesProtectDownloadsThenEvictBeforeRemoval()
        {
            using var library=new MotionLibrary(directory); var imported=library.ImportAsync("motion.glb",ModelFixture.Create()); yield return Done(imported); Assert.That(imported.Exception,Is.Null); var entry=imported.Result.Single();
            var archive=library.ArchiveAsync(entry.id,true); yield return Done(archive); Assert.That(archive.Exception,Is.Null);
            var load=library.AcquireAsync(entry.id,entry.rigHash);
            var remove=library.RemoveDownloadAsync(entry.id,() => Task.FromResult<string>(null)); yield return Done(remove); Assert.That(remove.Exception?.GetBaseException().Message,Does.Contain("Stop"));
            yield return Done(load); Assert.That(load.Exception,Is.Null); Assert.That(library.Pinned(entry.id),Is.True);
            load.Result.Dispose(); remove=library.RemoveDownloadAsync(entry.id,() => Task.FromResult<string>(null)); yield return Done(remove); Assert.That(remove.Exception,Is.Null); Assert.That(library.ResidentClipCount,Is.Zero);
            var missing=library.AcquireAsync(entry.id,entry.rigHash); yield return Done(missing); Assert.That(missing.Exception,Is.Not.Null);
            imported=library.ImportAsync("motion.glb",ModelFixture.Create()); yield return Done(imported); Assert.That(imported.Exception,Is.Null);
            load=library.AcquireAsync(entry.id,entry.rigHash); yield return Done(load); Assert.That(load.Exception,Is.Null); Assert.That(load.Result.Id,Is.EqualTo(entry.id)); load.Result.Dispose();
        }
        [UnityTest] public IEnumerator PendingRemovalRejectsNewLeasesAndRestoresAvailabilityWhenItsGuardFails()
        {
            using var library=new MotionLibrary(directory); var imported=library.ImportAsync("motion.glb",ModelFixture.Create()); yield return Done(imported); Assert.That(imported.Exception,Is.Null); var entry=imported.Result.Single();
            var archive=library.ArchiveAsync(entry.id,true); yield return Done(archive); Assert.That(archive.Exception,Is.Null);
            var gate=new TaskCompletionSource<string>(); var remove=library.RemoveDownloadAsync(entry.id,() => gate.Task);
            Assert.That(remove.IsCompleted,Is.False); Assert.That(library.Find(entry.id),Is.Null,"Pending removal must reserve the identity before asynchronous inspection");
            var load=library.AcquireAsync(entry.id,entry.rigHash); yield return Done(load); Assert.That(load.Exception,Is.Not.Null);
            gate.SetResult("Retained history needs this motion"); yield return Done(remove); Assert.That(remove.Exception?.GetBaseException().Message,Does.Contain("history"));
            Assert.That(library.Find(entry.id),Is.Not.Null); Assert.That(library.Downloaded(entry.id),Is.True);
        }
    }
}
