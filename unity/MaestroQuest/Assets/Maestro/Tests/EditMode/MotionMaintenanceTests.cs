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
    public sealed class MotionMaintenanceTests
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
        [UnityTest] public IEnumerator ArchiveRetainsReferencesAndRemovedDownloadReimportsWithItsOriginalIdentity()
        {
            using var library=new MotionLibrary(directory); var original=ModelFixture.TranslationMotion("LINEAR"); var copy=original.ToArray();
            var import=library.ImportAsync("walk.glb",original); yield return Done(import); Assert.That(import.Exception,Is.Null); var entry=import.Result.Single();
            var edit=library.UpdateAsync(entry.id,"My walk",new[] { "calm" },true); yield return Done(edit); Assert.That(edit.Exception,Is.Null);
            var remove=library.RemoveDownloadAsync(entry.id,() => Task.FromResult<string>(null)); yield return Done(remove); Assert.That(remove.Exception?.GetBaseException().Message,Does.Contain("Archive"));
            var archive=library.ArchiveAsync(entry.id,true); yield return Done(archive); Assert.That(archive.Exception,Is.Null);
            Assert.That(library.List(),Is.Empty); Assert.That(library.List(archivedOnly:true).Single().id,Is.EqualTo(entry.id)); Assert.That(library.Find(entry.id),Is.Not.Null,"Archived references remain playable");
            remove=library.RemoveDownloadAsync(entry.id,() => Task.FromResult("Saved action needs this")); yield return Done(remove); Assert.That(remove.Exception?.GetBaseException().Message,Does.Contain("Saved action")); Assert.That(library.Downloaded(entry.id),Is.True);
            remove=library.RemoveDownloadAsync(entry.id,() => Task.FromResult<string>(null)); yield return Done(remove); Assert.That(remove.Exception,Is.Null); Assert.That(library.Find(entry.id),Is.Null); Assert.That(library.PayloadPresent(entry.id),Is.False);
            using (var reopened=new MotionLibrary(directory)) Assert.That(reopened.List(archivedOnly:true).Single().removed,Is.True);
            var restore=library.ArchiveAsync(entry.id,false); yield return Done(restore); Assert.That(restore.Exception?.GetBaseException().Message,Does.Contain("original"));
            import=library.ImportAsync("original-again.glb",original); yield return Done(import); Assert.That(import.Exception,Is.Null);
            Assert.That(import.Result.Single().id,Is.EqualTo(entry.id)); Assert.That(library.Find(entry.id).name,Is.EqualTo("My walk")); Assert.That(library.Find(entry.id).favourite,Is.True); Assert.That(library.Find(entry.id).tags,Does.Contain("calm")); Assert.That(library.List().Single().id,Is.EqualTo(entry.id));
            Assert.That(original,Is.EqualTo(copy)); Assert.That(library.Downloaded(entry.id),Is.True);
        }
        [UnityTest] public IEnumerator V1CatalogueMigratesAndNewerV2CannotBeDowngradedDuringRemoval()
        {
            string id;
            using (var first=new MotionLibrary(directory)) { var import=first.ImportAsync("motion.glb",ModelFixture.Create()); yield return Done(import); Assert.That(import.Exception,Is.Null); id=import.Result.Single().id; }
            string current=Path.Combine(directory,"motions.v2.json"),old=Path.Combine(directory,"motions.v1.json"); var document=JObject.Parse(File.ReadAllText(current)); document["version"]=1;
            string legacy=document.ToString(); File.WriteAllText(old,legacy); File.Delete(current);
            using var library=new MotionLibrary(directory); Assert.That(library.Find(id),Is.Not.Null);
            var work=library.ArchiveAsync(id,true); yield return Done(work); Assert.That(work.Exception,Is.Null); Assert.That(File.ReadAllText(old),Is.EqualTo(legacy)); Assert.That((int)JObject.Parse(File.ReadAllText(current))["version"],Is.EqualTo(2));
            File.WriteAllText(current,"{\"version\":3}");
            work=library.RemoveDownloadAsync(id,() => Task.FromResult<string>(null)); yield return Done(work); Assert.That(work.Exception,Is.Not.Null); Assert.That(library.PayloadPresent(id),Is.True);
            using var protectedLibrary=new MotionLibrary(directory); Assert.That(protectedLibrary.ReadOnly,Is.True); Assert.That(File.ReadAllText(current),Is.EqualTo("{\"version\":3}"));
        }
        [UnityTest] public IEnumerator ForgettingUnusedRemovedMetadataFreesCatalogueCapacityAndRequiresANewIdentity()
        {
            using var library=new MotionLibrary(directory); var data=ModelFixture.Create(); var imported=library.ImportAsync("motion.glb",data); yield return Done(imported); Assert.That(imported.Exception,Is.Null); var entry=imported.Result.Single();
            var work=library.ForgetAsync(entry.id,() => Task.FromResult<string>(null)); yield return Done(work); Assert.That(work.Exception,Is.Not.Null); Assert.That(library.Find(entry.id),Is.Not.Null);
            work=library.ArchiveAsync(entry.id,true); yield return Done(work); Assert.That(work.Exception,Is.Null);
            work=library.RemoveDownloadAsync(entry.id,() => Task.FromResult<string>(null)); yield return Done(work); Assert.That(work.Exception,Is.Null);
            work=library.ForgetAsync(entry.id,() => Task.FromResult("Retained assignment")); yield return Done(work); Assert.That(work.Exception,Is.Not.Null); Assert.That(library.Inspect(entry.id),Is.Not.Null);
            work=library.ForgetAsync(entry.id,() => Task.FromResult<string>(null)); yield return Done(work); Assert.That(work.Exception,Is.Null); Assert.That(library.List(archivedOnly:true),Is.Empty); Assert.That(library.Sources(),Is.Empty);
            imported=library.ImportAsync("motion.glb",data); yield return Done(imported); Assert.That(imported.Exception,Is.Null); Assert.That(imported.Result.Single().id,Is.Not.EqualTo(entry.id));
        }
        [Test] public void RetainedSaveAndRecoveryCopiesProtectReferencesEvenAfterCurrentAssignmentsChange()
        {
            string id=Guid.NewGuid().ToString("N"); var storage=new RuleStorage(directory);
            var document=new RuleDocument { sequences=new[] { new RuleSequence { id=Guid.NewGuid().ToString("N"),name="Saved action",steps=new[] { new RuleStep { action=RuleActionKind.LibraryMotion,motionId=id,seconds=0 } } } } };
            Assert.That(storage.Save(document,out var error),Is.True,error);
            Assert.That(storage.RetainsMotion(id,out bool uncertain),Is.True); Assert.That(uncertain,Is.False);
            Assert.That(storage.Save(new RuleDocument(),out error),Is.True,error);
            Assert.That(storage.RetainsMotion(id,out uncertain),Is.True,"Recovery backup keeps the earlier reference"); Assert.That(uncertain,Is.False);
            File.WriteAllText(Path.Combine(directory,"rules.v3.json.unreadable"),"damaged retained save");
            storage.RetainsMotion(Guid.NewGuid().ToString("N"),out uncertain); Assert.That(uncertain,Is.True,"Unknown retained contents must not permit deleting a possible dependency");
        }
    }
}
