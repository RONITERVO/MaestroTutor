// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    // Derived metadata only. A cursor belongs to one definition and membership
    // generation; it neither freezes object revisions nor authorizes an edit.
    internal sealed class RoomResourceMembers
    {
        internal const int PageSize=16;
        readonly string kind,id,generation=Guid.NewGuid().ToString("N");
        readonly int revision;
        internal int Revision=>revision;
        readonly string[] members;
        internal RoomResourceMembers(string kind,string id,int revision,IEnumerable<string> members) {
            this.kind=kind;this.id=id;this.revision=revision;
            this.members=members.Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        }
        string Cursor(string session,int offset) {
            using var hash=SHA256.Create();
            var bytes=hash.ComputeHash(Encoding.UTF8.GetBytes(new JArray(generation,kind,id,revision,session,offset).ToString(Formatting.None)));
            return offset.ToString(System.Globalization.CultureInfo.InvariantCulture)+"."+BitConverter.ToString(bytes,0,16).Replace("-","").ToLowerInvariant();
        }
        internal JObject Read(string session,string cursor) {
            if(cursor==null||cursor.Length>36)return null;
            int offset=0;
            if(cursor.Length>0) {
                int dot=cursor.IndexOf('.');
                if(dot<1||!int.TryParse(cursor.Substring(0,dot),out offset)||offset<=0||offset>=members.Length||cursor!=Cursor(session,offset))return null;
            }
            var page=members.Skip(offset).Take(PageSize).ToArray();
            return new JObject{["kind"]=kind,["id"]=id,["revision"]=revision,["membership"]=generation,
                ["offset"]=offset,["total"]=members.Length,["next"]=offset+page.Length<members.Length?Cursor(session,offset+page.Length):"",
                ["members"]=new JArray(page)};
        }
    }
    public sealed partial class RoomJournal
    {
        // Bounded by the live definitions, never by the number of reads/edits.
        readonly Dictionary<string,RoomResourceMembers> resourceMembers=new(StringComparer.Ordinal);
        internal JObject ReadResourceMembers(string session,string kind,string id,string cursor) {
            if(!RoomWorldIdentity.Id(id))return null;
            int revision=kind switch {
                "environment"=>EnvironmentRevision(id),"appearance"=>AppearanceRevision(id),
                "visibility"=>VisibilityRevision(id),"region"=>RegionRevision(id),_=>0
            };
            if(revision==0)return null;
            string key=kind+":"+id;
            if(!resourceMembers.TryGetValue(key,out var directory)||directory.Revision!=revision) {
                // Scope transitions can invalidate revisions without republishing
                // a definition. Never return a cached revision after that change.
                IEnumerable<string> members=kind switch {
                    "environment"=>EnvironmentMembers(id),"appearance"=>AppearanceMembers(id),
                    "visibility"=>VisibilityMembers(id),"region"=>ReadRegion(id).members,_=>Array.Empty<string>()
                };
                resourceMembers[key]=directory=new RoomResourceMembers(kind,id,revision,members);
            }
            return directory.Read(session,cursor);
        }
        void InvalidateResourceDefinition(string kind,string id)=>resourceMembers.Remove(kind+":"+id);
        static HashSet<string> ResourceBindings(RoomObjectData item) {
            var keys=new HashSet<string>(StringComparer.Ordinal);
            if(item==null)return keys;
            if(!string.IsNullOrEmpty(item.environmentProfile))keys.Add("environment:"+item.environmentProfile);
            if(!string.IsNullOrEmpty(item.visibilityLayer))keys.Add("visibility:"+item.visibilityLayer);
            foreach(var binding in item.appearanceBindings)keys.Add("appearance:"+binding.appearanceId);
            return keys;
        }
        void InvalidateResourceMembers(RoomObjectData[] before,RoomObjectData[] after) {
            if(resourceMembers.Count==0)return;
            var previous=before.ToDictionary(x=>x.id,StringComparer.Ordinal);
            var next=after.ToDictionary(x=>x.id,StringComparer.Ordinal);
            foreach(var id in previous.Keys.Concat(next.Keys).Distinct(StringComparer.Ordinal)) {
                previous.TryGetValue(id,out var oldItem);next.TryGetValue(id,out var newItem);
                var changed=ResourceBindings(oldItem);changed.SymmetricExceptWith(ResourceBindings(newItem));
                foreach(var key in changed)resourceMembers.Remove(key);
            }
        }
    }
    public sealed partial class RoomEditor
    {
        internal JObject ObserveResourceMembers(string kind,string id,string cursor)=>journal?.ReadResourceMembers(TemporarySessionId,kind,id,cursor);
    }
}
