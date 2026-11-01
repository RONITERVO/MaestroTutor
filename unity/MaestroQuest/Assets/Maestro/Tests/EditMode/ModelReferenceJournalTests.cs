// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Creation;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class ModelReferenceJournalTests
    {
        const string Hash="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",Other="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        const string Id="cccccccccccccccccccccccccccccccc";
        static RoomJournal Journal(string hash=null)=>new(new RoomDocument{version=RoomDocument.CurrentVersion,
            objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro,modelHash=hash}},
            appearances=new[]{new RoomAppearance{id=Id,name="Material preference"}}});
        static void Replace(RoomJournal journal,string hash){var value=journal.Read("maestro");value.modelHash=hash;Assert.That(journal.Apply(new[]{value},Array.Empty<string>(),out var error),Is.True,error);}
        [Test]public void CurrentModelsRetainOnlyExactIdentitiesAndDetachedCopiesCannotChangeThem(){
            var journal=Journal(Hash);Assert.That(journal.UsesModel(Hash),Is.True);Assert.That(journal.UsesModel(Other),Is.False);Assert.That(journal.HistoricalModel(Hash),Is.False);
            journal.Snapshot().objects.Single(x=>x.id=="maestro").modelHash=Other;journal.Read("maestro").modelHash=Other;
            Assert.That(journal.UsesModel(Hash),Is.True);Assert.That(journal.UsesModel(Other),Is.False);
        }
        [Test]public void BeforeAndAfterModelsRemainRetainedInBothUndoAndRedo(){
            var journal=Journal(Hash);Replace(journal,Other);Assert.That(journal.UsesModel(Hash),Is.False);Assert.That(journal.HistoricalModel(Hash),Is.True);Assert.That(journal.HistoricalModel(Other),Is.True);
            Assert.That(journal.Undo(),Is.True);Assert.That(journal.UsesModel(Hash),Is.True);Assert.That(journal.UsesModel(Other),Is.False);Assert.That(journal.HistoricalModel(Hash),Is.True);Assert.That(journal.HistoricalModel(Other),Is.True);
            Assert.That(journal.Redo(),Is.True);Replace(journal,null);Assert.That(journal.UsesModel(Other),Is.False);Assert.That(journal.HistoricalModel(Other),Is.True);
        }
        [Test]public void TemporaryForkAndSavedSnapshotKeepTheirOwnModelHistory(){
            var saved=Journal();Replace(saved,Hash);var live=saved.Fork();Replace(live,Other);
            Assert.That(saved.UsesModel(Hash),Is.True);Assert.That(saved.UsesModel(Other),Is.False);Assert.That(live.UsesModel(Other),Is.True);
            Assert.That(saved.ApplySnapshot(live.Snapshot(),out var error),Is.True,error);Assert.That(saved.UsesModel(Other),Is.True);Assert.That(saved.HistoricalModel(Hash),Is.True);
            Assert.That(saved.Undo(),Is.True);Assert.That(saved.UsesModel(Hash),Is.True);Assert.That(saved.HistoricalModel(Other),Is.True);Assert.That(live.UsesModel(Other),Is.True);
        }
        [TestCase(false)][TestCase(true)]public void InactiveExactModelPreferencesRemainRootsInCurrentAndHistory(bool material){
            var journal=Journal(Other);var value=journal.Read("maestro");
            if(material)value.appearanceBindings=new[]{new AppearanceBinding{appearanceId=Id,kind="material",modelHash=Hash,materialIndex=0}};
            else value.footPlacement=new(){enabled=true,modelHash=Hash,leftClearance=.01f};
            Assert.That(journal.Apply(new[]{value},Array.Empty<string>(),out var error),Is.True,error);Assert.That(journal.UsesModel(Hash),Is.True);Assert.That(journal.UsesModel(Other),Is.True);
            value.appearanceBindings=Array.Empty<AppearanceBinding>();value.footPlacement=new();Assert.That(journal.Apply(new[]{value},Array.Empty<string>(),out error),Is.True,error);
            Assert.That(journal.UsesModel(Hash),Is.False);Assert.That(journal.HistoricalModel(Hash),Is.True);Assert.That(journal.Undo(),Is.True);Assert.That(journal.UsesModel(Hash),Is.True);
        }
    }
}
