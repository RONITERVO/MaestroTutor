// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
namespace Maestro.Quest.Creation
{
    /// <summary>Exact retained expansion identities; names and string literals are never remapped.</summary>
    public sealed class CreationBatchMapping
    {
        public IReadOnlyDictionary<string,string> Objects {get;}
        public IReadOnlyList<CreationResourceSlot> Resources {get;}
        internal CreationBatchMapping(Dictionary<string,string> objects,CreationResourceSlot[] resources) {
            Objects=new ReadOnlyDictionary<string,string>(new Dictionary<string,string>(objects,StringComparer.Ordinal));
            Resources=Array.AsReadOnly(resources.ToArray());
        }
    }
    public sealed class CreationResourceSlot
    {
        public string Kind {get;} public string Slot {get;} public string Id {get;}
        internal CreationResourceSlot(string kind,string slot,string id){Kind=kind;Slot=slot;Id=id;}
    }
}
