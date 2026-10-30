// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomJournal
    {
        internal bool UsesAudio(string hash)=>audioSources.Values.Any(source=>source.kind=="clip"&&source.assetHash==hash);
        internal bool HistoricalAudio(string hash)=>undo.Concat(redo).Any(change=>
            (change.BeforeAudio??Array.Empty<RoomAudioDefinition>()).Concat(change.AfterAudio??Array.Empty<RoomAudioDefinition>())
                .Any(source=>source?.kind=="clip"&&source.assetHash==hash));
    }
    public sealed partial class RoomEditor
    {
        // Every clip definition counts, including sources without emitters.
        // Normal autosave workers own detached snapshots: the caller must hold
        // save dispatch and drain them before reading persisted references.
        // Playback/decode pins and pending/uncertain edits are separate roots.
        internal bool UsesAudio(string hash)=>journal==null||journal.UsesAudio(hash)||savedJournal?.UsesAudio(hash)==true||
            savingSnapshot?.audioSources?.Any(source=>source?.kind=="clip"&&source.assetHash==hash)==true;
        internal bool HistoricalAudio(string hash)=>journal==null||journal.HistoricalAudio(hash)||savedJournal?.HistoricalAudio(hash)==true;
    }
}
