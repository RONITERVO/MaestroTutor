// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Imports
{
    /// <summary>Explicit scan evidence. Missing historical receipt evidence stays unknown.</summary>
    internal static class ImportedInventoryObservation
    {
        internal static JObject Schema()=>Object(new JObject {
            ["ready"]=new JObject {["type"]="boolean"},
            ["complete"]=new JObject {["type"]="boolean"},
            ["enumerationComplete"]=new JObject {["type"]="boolean"},
            ["issueCount"]=Number(0,ImportedAssetInventory.MaximumVisitedEntries+2,true),
            ["error"]=Text("^.{0,128}$",128)
        });
        internal static JObject Summary<TEntry>(ImportedAssetInventory<TEntry> inventory,string unavailable="Refresh the library to inspect saved files.")=>new() {
            ["ready"]=inventory?.Ready??false,
            ["complete"]=inventory?.Complete??false,
            ["enumerationComplete"]=inventory?.EnumerationComplete??false,
            ["issueCount"]=inventory?.IssueCount??0,
            ["error"]=ImportObservation.Text(inventory?.Error??unavailable)
        };
    }
}
