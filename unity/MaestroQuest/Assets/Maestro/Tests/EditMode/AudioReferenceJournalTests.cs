// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Creation;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class AudioReferenceJournalTests
    {
        const string Hash="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",Other="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        const string Id="cccccccccccccccccccccccccccccccc";
        static RoomAudioDefinition Clip(string hash)=>new(){id=Id,name="Unbound clip",kind="clip",assetHash=hash,seconds=.5f};
        static RoomJournal Journal(string hash=null)=>new(new RoomDocument{version=RoomDocument.CurrentVersion,
            objects=new[]{new RoomObjectData{id="book",kind=RoomObjectKind.Book},new RoomObjectData{id="maestro",kind=RoomObjectKind.Maestro}},
            audioSources=hash==null?Array.Empty<RoomAudioDefinition>():new[]{Clip(hash)}});
        static void Replace(RoomJournal journal,string hash){Assert.That(journal.Apply(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out var error,
            audioEdits:hash==null?new AudioDefinitionEdits{Removals=new[]{Id}}:new AudioDefinitionEdits{Replacements=new[]{Clip(hash)}}),Is.True,error);}
        [Test]public void UnboundClipsRetainOnlyTheirExactPayloadAndDetachedCopiesCannotChangeIt(){
            var journal=Journal(Hash);Assert.That(journal.UsesAudio(Hash),Is.True);Assert.That(journal.UsesAudio(Other),Is.False);Assert.That(journal.HistoricalAudio(Hash),Is.False);
            journal.Snapshot().audioSources[0].assetHash=Other;journal.ReadAudio(Id).assetHash=Other;
            Assert.That(journal.UsesAudio(Hash),Is.True);Assert.That(journal.UsesAudio(Other),Is.False);
        }
        [Test]public void BeforeAndAfterAudioRemainRetainedInBothUndoAndRedo(){
            var journal=Journal(Hash);Replace(journal,Other);Assert.That(journal.UsesAudio(Hash),Is.False);Assert.That(journal.HistoricalAudio(Hash),Is.True);Assert.That(journal.HistoricalAudio(Other),Is.True);
            Assert.That(journal.Undo(),Is.True);Assert.That(journal.UsesAudio(Hash),Is.True);Assert.That(journal.UsesAudio(Other),Is.False);Assert.That(journal.HistoricalAudio(Hash),Is.True);Assert.That(journal.HistoricalAudio(Other),Is.True);
            Assert.That(journal.Redo(),Is.True);Replace(journal,null);Assert.That(journal.UsesAudio(Other),Is.False);Assert.That(journal.HistoricalAudio(Other),Is.True);
            Assert.That(journal.Undo(),Is.True);Assert.That(journal.UsesAudio(Other),Is.True);
        }
        [Test]public void TemporaryForkAndSavedSnapshotKeepTheirOwnAudioHistory(){
            var saved=Journal();Replace(saved,Hash);var live=saved.Fork();Replace(live,Other);
            Assert.That(saved.UsesAudio(Hash),Is.True);Assert.That(saved.UsesAudio(Other),Is.False);Assert.That(live.UsesAudio(Other),Is.True);
            Assert.That(saved.ApplySnapshot(live.Snapshot(),out var error),Is.True,error);Assert.That(saved.UsesAudio(Other),Is.True);Assert.That(saved.HistoricalAudio(Hash),Is.True);
            Assert.That(saved.Undo(),Is.True);Assert.That(saved.UsesAudio(Hash),Is.True);Assert.That(saved.HistoricalAudio(Other),Is.True);Assert.That(live.UsesAudio(Other),Is.True);
        }
    }
}
