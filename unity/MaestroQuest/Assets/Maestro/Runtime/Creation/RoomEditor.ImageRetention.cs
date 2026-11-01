// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomJournal
    {
        internal bool UsesImage(string hash)=>appearances.Values.Any(a=>a.style?.imageHash==hash);
        internal bool HistoricalImage(string hash)=>undo.Concat(redo).Any(c=>
            (c.BeforeAppearances??Array.Empty<RoomAppearance>()).Concat(c.AfterAppearances??Array.Empty<RoomAppearance>()).Any(a=>a?.style?.imageHash==hash));
    }
    public sealed partial class RoomEditor
    {
        // All definitions count, including appearances with no current bindings.
        // A save-dispatch hold must drain normal autosaves before the persisted
        // proof: those workers own detached older snapshots, not this journal.
        internal bool UsesImage(string hash)=>journal==null||journal.UsesImage(hash)||savedJournal?.UsesImage(hash)==true||
            savingSnapshot?.appearances?.Any(a=>a?.style?.imageHash==hash)==true;
        internal bool HistoricalImage(string hash)=>journal==null||journal.HistoricalImage(hash)||savedJournal?.HistoricalImage(hash)==true;
    }
}
