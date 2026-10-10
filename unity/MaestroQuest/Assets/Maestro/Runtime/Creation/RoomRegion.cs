// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // Regions partition authored ownership, not object payloads. Portable models
    // and blueprints do not carry references to another world's region IDs.
    [Serializable] public sealed class RoomRegion
    {
        public const int MaximumRegions=32;
        public int version=1;
        public string id,name;
        public string[] members=Array.Empty<string>();
        public RoomRegion Copy()=>new(){version=version,id=id,name=name,members=members==null?null:members.OrderBy(x=>x,StringComparer.Ordinal).ToArray()};
        public bool Validate(out string error) {
            error="A region needs version 1, a stable ID, a readable name and distinct creation IDs";
            if(version!=1||!RoomWorldIdentity.Id(id)||string.IsNullOrWhiteSpace(name)||name.Length>80||name.Any(char.IsControl)||members==null||members.Length>RoomDocument.MaximumObjects)return false;
            if(members.Any(x=>x?.Length!=32||!Guid.TryParseExact(x,"N",out _))||members.Distinct(StringComparer.Ordinal).Count()!=members.Length)return false;
            error=null;return true;
        }
        internal static bool ValidateCollection(RoomRegion[] regions,RoomObjectData[] objects,RoomWorldIdentity world,int version,out string error) {
            error="Regions require the current format and at most 32 distinct definitions";
            if(regions==null||regions.Length>MaximumRegions||version<36&&regions.Length>0)return false;
            var ids=new HashSet<string>(StringComparer.Ordinal);var assigned=new HashSet<string>(StringComparer.Ordinal);
            var creations=objects.Where(x=>!x.IsBuiltIn).Select(x=>x.id).ToHashSet(StringComparer.Ordinal);
            foreach(var region in regions) {
                if(region==null||!region.Validate(out error))return false;
                error="Region identities must be unique and distinct from the world and home region";
                if(!ids.Add(region.id)||region.id==world?.worldId||region.id==world?.regionId)return false;
                error="A creation must exist and belong to at most one authored region";
                foreach(var member in region.members)if(!creations.Contains(member)||!assigned.Add(member))return false;
            }
            error=null;return true;
        }
        internal static bool ValidWire(JObject room) {
            if(room["version"]?.Type!=JTokenType.Integer)return false;
            if((int)room["version"]<36&&!room.ContainsKey("regions"))return true;
            if(room["regions"] is not JArray regions||regions.Count>MaximumRegions)return false;
            foreach(var token in regions) {
                if(token is not JObject value||value.Count!=4||value["version"]?.Type!=JTokenType.Integer||value["id"]?.Type!=JTokenType.String||value["name"]?.Type!=JTokenType.String||value["members"] is not JArray members||members.Any(x=>x.Type!=JTokenType.String))return false;
                if(!new RoomRegion{version=(int)value["version"],id=(string)value["id"],name=(string)value["name"],members=members.Values<string>().ToArray()}.Validate(out _))return false;
            }
            return true;
        }
    }
    public sealed class RegionEdits
    {
        public RoomRegion[] Replacements=Array.Empty<RoomRegion>();
        public string[] Removals=Array.Empty<string>();
        public bool Validate(out string error) {
            error="Region edits need distinct valid definitions and removals";
            if(Replacements==null||Removals==null||Replacements.Length>RoomRegion.MaximumRegions||Removals.Length>RoomRegion.MaximumRegions)return false;
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var value in Replacements) {
                if(value==null)return false;
                if(!value.Validate(out error))return false;
                if(!ids.Add(value.id)){error="Region edits contain a repeated identity";return false;}
            }
            foreach(var id in Removals)if(!RoomWorldIdentity.Id(id)||!ids.Add(id)){error="Region removals contain an invalid or repeated identity";return false;}
            error=null;return true;
        }
        internal RoomRegion[] Apply(RoomRegion[] current) {
            var values=current.ToDictionary(x=>x.id,x=>x.Copy(),StringComparer.Ordinal);
            foreach(var id in Removals)values.Remove(id);
            foreach(var value in Replacements)values[value.id]=value.Copy();
            return values.Values.OrderBy(x=>x.id,StringComparer.Ordinal).ToArray();
        }
    }
    public sealed partial class RoomJournal
    {
        readonly Dictionary<string,RoomRegion> regions=new(StringComparer.Ordinal);
        readonly Dictionary<string,int> regionRevisions=new(StringComparer.Ordinal);
        public int RegionRevision(string id)=>id!=null&&regionRevisions.TryGetValue(id,out var value)?value:0;
        public RoomRegion ReadRegion(string id)=>id!=null&&regions.TryGetValue(id,out var value)?value.Copy():null;
        public RoomRegion[] RegionSnapshot()=>regions.Values.OrderBy(x=>x.id,StringComparer.Ordinal).Select(x=>x.Copy()).ToArray();
        public string RegionFor(string target)=>regions.Values.FirstOrDefault(x=>x.members.Contains(target,StringComparer.Ordinal))?.id??"";
        void SetRegions(RoomRegion[] before,RoomRegion[] after) {
            foreach(var value in before){regions.Remove(value.id);regionRevisions.Remove(value.id);}
            foreach(var value in after){regions[value.id]=value.Copy();regionRevisions[value.id]=clock.Next++;}
            foreach(var id in before.Concat(after).SelectMany(x=>x.members).Distinct(StringComparer.Ordinal))if(items.ContainsKey(id))revisions[id]=clock.Next++;
        }
        [Serializable] sealed class RegionDelta {public RoomRegion[] regions;}
        static bool EquivalentRegions(RoomRegion[] a,RoomRegion[] b)=>JsonUtility.ToJson(new RegionDelta{regions=a.OrderBy(x=>x.id,StringComparer.Ordinal).ToArray()})==JsonUtility.ToJson(new RegionDelta{regions=b.OrderBy(x=>x.id,StringComparer.Ordinal).ToArray()});
    }
}
