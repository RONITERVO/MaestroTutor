// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Maestro.Quest.Programs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    // A derived, immutable directory of searchable saved metadata. No geometry or
    // native objects are copied or loaded. Movement does not invalidate paging.
    internal sealed class RoomObjectSearch
    {
        internal const int PageSize=3;
        readonly string generation=Guid.NewGuid().ToString("N");
        readonly Entry[] entries;
        string cachedSession,cachedQuery,cachedKind,cachedCursor;
        JObject cached;
        sealed class Entry
        {
            internal readonly string Id,Name,Kind;
            internal Entry(RoomObjectData item){Id=item.id;Name=NameOf(item);Kind=item.kind.ToString();}
            internal JObject Observe()=>new(){["id"]=Id,["name"]=Name,["kind"]=Kind};
        }
        internal static string NameOf(RoomObjectData item)=>item.name??item.kind.ToString();
        internal RoomObjectSearch(System.Collections.Generic.IEnumerable<RoomObjectData> objects)=>entries=objects.OrderBy(x=>x.id,StringComparer.Ordinal).Select(x=>new Entry(x)).ToArray();
        internal static bool Changed(RoomObjectData[] before,RoomObjectData[] after)
        {
            if(before.Length!=after.Length)return true;
            foreach(var previous in before){var next=Array.Find(after,x=>x.id==previous.id);if(next==null||next.kind!=previous.kind||NameOf(next)!=NameOf(previous))return true;}
            return false;
        }
        string Cursor(string session,string query,string kind,string last)
        {
            // Bind continuation to this metadata generation, workspace/temporary
            // session and exact normalized filter. This is a read-only cursor,
            // never an object revision or authorization to perform an edit.
            using var hash=SHA256.Create();var bytes=hash.ComputeHash(Encoding.UTF8.GetBytes(new JArray(generation,session,query,kind,last).ToString(Formatting.None)));
            return last+"."+BitConverter.ToString(bytes,0,16).Replace("-","").ToLowerInvariant();
        }
        internal JObject Read(string worldId,string session,string query,string kind,string cursor)
        {
            if(query==null||query.Length>80||query.Any(char.IsControl)||cursor==null||cursor.Length>65||kind!="all"&&!Enum.GetNames(typeof(RoomObjectKind)).Contains(kind))return null;
            var terms=query.Split(' ',StringSplitOptions.RemoveEmptyEntries);query=string.Join(" ",terms);
            if(cached!=null&&session==cachedSession&&query==cachedQuery&&kind==cachedKind&&cursor==cachedCursor)return (JObject)cached.DeepClone();
            string last="";
            if(cursor.Length>0){int dot=cursor.IndexOf('.');if(dot<1)return null;last=cursor.Substring(0,dot);if(cursor!=Cursor(session,query,kind,last))return null;}
            bool Matches(Entry e)=> (kind=="all"||e.Kind==kind)&&terms.All(term=>e.Id.IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0||e.Name.IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0||e.Kind.IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0);
            // Retain only one result page; the immutable directory is reused.
            int total=0,offset=0;bool found=last.Length==0;var page=new JArray();
            foreach(var entry in entries){
                if(!Matches(entry))continue;total++;
                if(last.Length>0&&StringComparer.Ordinal.Compare(entry.Id,last)<=0){offset++;if(entry.Id==last)found=true;continue;}
                if(page.Count<PageSize)page.Add(entry.Observe());
            }
            if(!found)return null;
            var result=new JObject{["worldId"]=worldId,["offset"]=offset,["total"]=total,["next"]="",["entries"]=page};
            // JSON escaping can make a valid 80-character name much larger on the
            // wire. Reduce the page, never the saved name, to the shared value budget.
            while(true){
                result["next"]=offset+page.Count<total?Cursor(session,query,kind,(string)page.Last["id"]):"";
                if(ProgramValue.MeasureCharacters(result)<=ProgramValue.MaximumCharacters)break;
                if(page.Count<=1)throw new InvalidOperationException("Object metadata exceeds the shared value budget");
                page.RemoveAt(page.Count-1);
            }
            cachedSession=session;cachedQuery=query;cachedKind=kind;cachedCursor=cursor;cached=(JObject)result.DeepClone();return result;
        }
    }
    public sealed partial class RoomJournal
    {
        RoomObjectSearch objectSearch;
        internal JObject SearchObjects(string session,string query,string kind,string cursor)=>(objectSearch??=new RoomObjectSearch(items.Values)).Read(world.worldId,session,query,kind,cursor);
        void InvalidateObjectSearch(RoomObjectData[] before,RoomObjectData[] after){if(objectSearch!=null&&RoomObjectSearch.Changed(before,after))objectSearch=null;}
    }
    public sealed partial class RoomEditor
    {
        internal JObject SearchObjects(string query,string kind,string cursor)=>journal?.SearchObjects(TemporarySessionId,query,kind,cursor);
    }
}
