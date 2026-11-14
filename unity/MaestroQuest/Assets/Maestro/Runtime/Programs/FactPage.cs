// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Programs
{
    /// <summary>Exact ordered prefixes within the typed value budget. Never skips an entry.</summary>
    internal static class FactPage
    {
        internal static JObject Prefix(int total,int offset,IEnumerable<JObject> candidates,JObject page=null)
        {
            page??=new JObject();var entries=new JArray();
            page["total"]=total;page["next"]=-1;page["entries"]=entries;
            foreach(var entry in candidates){
                entries.Add(entry);int next=offset+entries.Count;
                page["next"]=next<total?next:-1;
                if(ProgramValue.MeasureCharacters(page)<=ProgramValue.MaximumCharacters)continue;
                entries.RemoveAt(entries.Count-1);
                if(entries.Count==0)throw new ProgramFault("An exact fact entry exceeds the typed value budget");
                page["next"]=offset+entries.Count;break;
            }
            return page;
        }
    }
}
