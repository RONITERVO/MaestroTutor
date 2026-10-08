// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    /// <summary>Saved collision participation, independent of rendering and acoustics.</summary>
    [Serializable] public sealed class RoomEnvironmentProfile
    {
        public const int MaximumProfiles=16;
        public int version=1;
        public string id,name;
        public bool realCollisions=true;
        public RoomEnvironmentProfile Copy()=>(RoomEnvironmentProfile)MemberwiseClone();
        public bool Validate(out string error) {
            error="An environment profile needs version 1, a stable ID and a readable name";
            if(version!=1||!RoomWorldIdentity.Id(id)||string.IsNullOrWhiteSpace(name)||name.Length>80||name.Any(char.IsControl))return false;
            error=null;return true;
        }
        internal static bool ValidateCollection(RoomEnvironmentProfile[] profiles,RoomObjectData[] objects,int version,out string error) {
            error="Environment profiles require the current format and at most 16 distinct definitions";
            if(profiles==null||profiles.Length>MaximumProfiles||version<24&&profiles.Length>0)return false;
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var p in profiles){if(p==null||!ids.Add(p.id)||!p.Validate(out error))return false;}
            foreach(var obj in objects) {
                error="An object refers to a missing environment profile";
                if(!string.IsNullOrEmpty(obj.environmentProfile)&&(version<24||!ids.Contains(obj.environmentProfile)))return false;
                if(version>=24&&obj.environmentProfile==null)return false;
            }
            error="A shared environment profile supports at most 16 bound objects; use another profile for additional objects";
            if(profiles.Any(p=>objects.Count(x=>x.environmentProfile==p.id)>16))return false;
            error=null;return true;
        }
        internal static bool ValidWire(JObject room) {
            if(room["version"]?.Type!=JTokenType.Integer)return false;
            if((int)room["version"]<24&&!room.ContainsKey("environmentProfiles"))return true;
            if(room["environmentProfiles"] is not JArray profiles||profiles.Count>MaximumProfiles)return false;
            foreach(var token in profiles) {
                if(token is not JObject p||p.Count!=4||p["version"]?.Type!=JTokenType.Integer||p["id"]?.Type!=JTokenType.String||
                    p["name"]?.Type!=JTokenType.String||p["realCollisions"]?.Type!=JTokenType.Boolean)return false;
                if(!new RoomEnvironmentProfile{version=(int)p["version"],id=(string)p["id"],name=(string)p["name"],realCollisions=(bool)p["realCollisions"]}.Validate(out _))return false;
            }
            return room["objects"] is JArray items&&items.All(x=>x is JObject&&((int)room["version"]<24&&x["environmentProfile"]==null||x["environmentProfile"]?.Type==JTokenType.String));
        }
    }
    public sealed class EnvironmentProfileEdits
    {
        public RoomEnvironmentProfile[] Replacements=Array.Empty<RoomEnvironmentProfile>();
        public string[] Removals=Array.Empty<string>();
        public bool Validate(out string error) {
            error="Environment edits need distinct valid definitions and removals";
            if(Replacements==null||Removals==null||Replacements.Length>16||Removals.Length>16)return false;
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var p in Replacements){if(p==null||!ids.Add(p.id)||!p.Validate(out error))return false;}
            foreach(var id in Removals)if(!RoomWorldIdentity.Id(id)||!ids.Add(id))return false;
            error=null;return true;
        }
        internal RoomEnvironmentProfile[] Apply(RoomEnvironmentProfile[] current) {
            var values=current.ToDictionary(x=>x.id,x=>x.Copy(),StringComparer.Ordinal);
            foreach(var id in Removals)values.Remove(id);
            foreach(var p in Replacements)values[p.id]=p.Copy();
            return values.Values.OrderBy(x=>x.id,StringComparer.Ordinal).ToArray();
        }
    }
    public sealed partial class RoomJournal
    {
        readonly Dictionary<string,RoomEnvironmentProfile> environments=new(StringComparer.Ordinal);
        readonly Dictionary<string,int> environmentRevisions=new(StringComparer.Ordinal);
        public int EnvironmentRevision(string id)=>id!=null&&environmentRevisions.TryGetValue(id,out var value)?value:0;
        public RoomEnvironmentProfile ReadEnvironment(string id)=>id!=null&&environments.TryGetValue(id,out var value)?value.Copy():null;
        public RoomEnvironmentProfile[] EnvironmentSnapshot()=>environments.Values.OrderBy(x=>x.id,StringComparer.Ordinal).Select(x=>x.Copy()).ToArray();
        void SetEnvironments(RoomEnvironmentProfile[] before,RoomEnvironmentProfile[] after) {
            foreach(var p in before){environments.Remove(p.id);environmentRevisions.Remove(p.id);}
            foreach(var p in after){environments[p.id]=p.Copy();environmentRevisions[p.id]=clock.Next++;}
        }
        [Serializable] sealed class EnvironmentDelta {public RoomEnvironmentProfile[] profiles;}
        static bool EquivalentEnvironments(RoomEnvironmentProfile[] a,RoomEnvironmentProfile[] b)=>JsonUtility.ToJson(new EnvironmentDelta{profiles=a.OrderBy(x=>x.id).ToArray()})==JsonUtility.ToJson(new EnvironmentDelta{profiles=b.OrderBy(x=>x.id).ToArray()});
    }
}
