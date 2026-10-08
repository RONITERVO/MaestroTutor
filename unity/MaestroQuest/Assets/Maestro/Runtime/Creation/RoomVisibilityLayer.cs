// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    /// <summary>Saved visual grouping, independent of physical participation and acoustics.</summary>
    [Serializable] public sealed class RoomVisibilityLayer
    {
        public const int MaximumLayers=16;
        public int version=1;
        public string id,name;
        public float opacity=1;
        public bool realDepth=true;
        public RoomVisibilityLayer Copy()=>(RoomVisibilityLayer)MemberwiseClone();
        public bool Validate(out string error) {
            error="A visibility layer needs version 1, a stable ID and a readable name";
            if(!float.IsFinite(opacity)||opacity<0||opacity>1||version!=1||!RoomWorldIdentity.Id(id)||string.IsNullOrWhiteSpace(name)||name.Length>80||name.Any(char.IsControl))return false;
            error=null;return true;
        }
        internal static bool ValidateCollection(RoomVisibilityLayer[] profiles,RoomObjectData[] objects,int version,out string error) {
            error="Visibility layers require the current format and at most 16 distinct definitions";
            if(profiles==null||profiles.Length>MaximumLayers||version<26&&profiles.Length>0)return false;
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var p in profiles){if(p==null||!ids.Add(p.id)||!p.Validate(out error))return false;}
            foreach(var obj in objects) {
                error="An object refers to a missing visibility layer";
                if(!string.IsNullOrEmpty(obj.visibilityLayer)&&(version<26||!ids.Contains(obj.visibilityLayer)))return false;
                if(version>=26&&obj.visibilityLayer==null)return false;
            }
            error=null;return true;
        }
        internal static bool ValidWire(JObject room) {
            if(room["version"]?.Type!=JTokenType.Integer)return false;
            if((int)room["version"]<26&&!room.ContainsKey("visibilityLayers"))return true;
            if(room["visibilityLayers"] is not JArray profiles||profiles.Count>MaximumLayers)return false;
            foreach(var token in profiles) {
                if(token is not JObject p||p.Count!=5||p["version"]?.Type!=JTokenType.Integer||p["id"]?.Type!=JTokenType.String||
                    p["name"]?.Type!=JTokenType.String||p["opacity"]?.Type is not (JTokenType.Float or JTokenType.Integer)||p["realDepth"]?.Type!=JTokenType.Boolean)return false;
                if(!new RoomVisibilityLayer{version=(int)p["version"],id=(string)p["id"],name=(string)p["name"],opacity=(float)p["opacity"],realDepth=(bool)p["realDepth"]}.Validate(out _))return false;
            }
            return room["objects"] is JArray items&&items.All(x=>x is JObject&&((int)room["version"]<26&&x["visibilityLayer"]==null||x["visibilityLayer"]?.Type==JTokenType.String));
        }
    }
    public sealed class VisibilityLayerEdits
    {
        public RoomVisibilityLayer[] Replacements=Array.Empty<RoomVisibilityLayer>();
        public string[] Removals=Array.Empty<string>();
        public bool Validate(out string error) {
            error="Visibility edits need distinct valid definitions and removals";
            if(Replacements==null||Removals==null||Replacements.Length>16||Removals.Length>16)return false;
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var p in Replacements){if(p==null||!ids.Add(p.id)||!p.Validate(out error))return false;}
            foreach(var id in Removals)if(!RoomWorldIdentity.Id(id)||!ids.Add(id))return false;
            error=null;return true;
        }
        internal RoomVisibilityLayer[] Apply(RoomVisibilityLayer[] current) {
            var values=current.ToDictionary(x=>x.id,x=>x.Copy(),StringComparer.Ordinal);
            foreach(var id in Removals)values.Remove(id);
            foreach(var p in Replacements)values[p.id]=p.Copy();
            return values.Values.OrderBy(x=>x.id,StringComparer.Ordinal).ToArray();
        }
    }
    public sealed partial class RoomJournal
    {
        readonly Dictionary<string,RoomVisibilityLayer> visibilityLayers=new(StringComparer.Ordinal);
        readonly Dictionary<string,int> visibilityRevisions=new(StringComparer.Ordinal);
        public int VisibilityRevision(string id)=>id!=null&&visibilityRevisions.TryGetValue(id,out var value)?value:0;
        public RoomVisibilityLayer ReadVisibility(string id)=>id!=null&&visibilityLayers.TryGetValue(id,out var value)?value.Copy():null;
        public RoomVisibilityLayer[] VisibilitySnapshot()=>visibilityLayers.Values.OrderBy(x=>x.id,StringComparer.Ordinal).Select(x=>x.Copy()).ToArray();
        void SetVisibility(RoomVisibilityLayer[] before,RoomVisibilityLayer[] after) {
            foreach(var p in before){visibilityLayers.Remove(p.id);visibilityRevisions.Remove(p.id);}
            foreach(var p in after){visibilityLayers[p.id]=p.Copy();visibilityRevisions[p.id]=clock.Next++;}
            var changed=before.Select(p=>p.id).Concat(after.Select(p=>p.id)).ToHashSet();
            foreach(var item in items.Values)if(changed.Contains(item.visibilityLayer))revisions[item.id]=clock.Next++;
        }
        [Serializable] sealed class VisibilityDelta {public RoomVisibilityLayer[] profiles;}
        static bool EquivalentVisibility(RoomVisibilityLayer[] a,RoomVisibilityLayer[] b)=>JsonUtility.ToJson(new VisibilityDelta{profiles=a.OrderBy(x=>x.id).ToArray()})==JsonUtility.ToJson(new VisibilityDelta{profiles=b.OrderBy(x=>x.id).ToArray()});
    }
}
