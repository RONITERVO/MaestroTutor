// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
namespace Maestro.Quest.Creation
{
    internal static class RoomModelReferences
    {
        // Calibration and material preferences are saved against exact model
        // identities even when those models are not currently selected.
        internal static bool Uses(RoomObjectData item,string hash)=>item?.modelHash==hash||item?.footPlacement?.modelHash==hash||
            item?.appearanceBindings?.Any(binding=>binding.kind=="material"&&binding.modelHash==hash)==true;
    }
    public sealed partial class RoomJournal
    {
        internal bool UsesModel(string hash)=>items.Values.Any(item=>RoomModelReferences.Uses(item,hash));
        internal bool HistoricalModel(string hash)=>undo.Concat(redo).Any(change=>
            (change.Before??Array.Empty<RoomObjectData>()).Concat(change.After??Array.Empty<RoomObjectData>()).Any(item=>RoomModelReferences.Uses(item,hash)));
    }
    public sealed partial class RoomEditor
    {
        // Journals include dormant entities with no native GameObject. Current
        // and inactive avatar activity profiles are independent authored roots.
        // Callers must still hold/drain saves, pending edits and native leases;
        // these observations alone never authorize payload removal.
        internal bool UsesModel(string hash)=>journal==null||journal.UsesModel(hash)||savedJournal?.UsesModel(hash)==true||
            savingSnapshot?.objects?.Any(item=>RoomModelReferences.Uses(item,hash))==true||ActivityProfiles?.UsesModel(hash)==true;
        internal bool HistoricalModel(string hash)=>journal==null||journal.HistoricalModel(hash)||savedJournal?.HistoricalModel(hash)==true||ActivityProfiles?.HistoricalModel(hash)==true;
        internal bool ModelReferencesUncertain=>journal==null||ActivityProfiles==null||ActivityProfiles.ReadOnly;
    }
}
