// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Imports;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    /// <summary>Supported illustrated appearance values. No physical material,
    /// expiring URL or texture payload belongs in this document.</summary>
    [Serializable] public sealed class AppearanceStyle
    {
        public string tint="#FFFFFF",patternMode="inherit",renderMode="inherit",sidedness="inherit";
        public RecipePattern pattern=new();
        public Vector2 tiling=Vector2.one,offset=Vector2.zero;
        public float opacity=1,cutoff=0,grain=-1,shading=-1;
        public AppearanceStyle Copy()=>JsonUtility.FromJson<AppearanceStyle>(JsonUtility.ToJson(this));
        public bool Validate(out string error) {
            error="Choose a supported appearance with finite colour, mapping and surface settings";
            if(!ColorUtility.TryParseHtmlString(tint,out var color)||tint==null||tint.Length!=7||tint[0]!='#'||color.a!=1||
                patternMode is not ("inherit" or "replace")||pattern==null||!pattern.Valid()||
                !Finite(tiling)||tiling.x<.01f||tiling.y<.01f||tiling.x>64||tiling.y>64||!Finite(offset)||Mathf.Abs(offset.x)>64||Mathf.Abs(offset.y)>64||
                sidedness is not ("inherit" or "front" or "both")||
                !float.IsFinite(grain)||grain!=-1&&(grain<0||grain>1)||!float.IsFinite(shading)||shading!=-1&&(shading<0||shading>.3f))return false;
            if(renderMode=="inherit") {if(opacity!=1||cutoff!=0)return false;}
            else if(!IllustratedSurface.TryCreate(renderMode,opacity,cutoff,true,out _,out error)||renderMode!="cutout"&&cutoff!=0)return false;
            error=null;return true;
        }
        static bool Finite(Vector2 v)=>float.IsFinite(v.x)&&float.IsFinite(v.y);
        internal static bool Wire(JToken token) {
            if(token is not JObject a||!Exact(a,"tint","patternMode","pattern","tiling","offset","renderMode","opacity","cutoff","sidedness","grain","shading"))return false;
            if(new[]{"tint","patternMode","renderMode","sidedness"}.Any(k=>a[k]?.Type!=JTokenType.String)||
                new[]{"opacity","cutoff","grain","shading"}.Any(k=>!Number(a[k]))||!Vector(a["tiling"])||!Vector(a["offset"]))return false;
            if(a["pattern"] is not JObject p||!Exact(p,"kind","plane","secondary","columns","rows")||
                new[]{"kind","plane","secondary"}.Any(k=>p[k]?.Type!=JTokenType.String)||p["columns"]?.Type!=JTokenType.Integer||p["rows"]?.Type!=JTokenType.Integer)return false;
            try{return JsonUtility.FromJson<AppearanceStyle>(a.ToString()).Validate(out _);}catch{return false;}
        }
        internal static bool Exact(JObject o,params string[] keys)=>o.Count==keys.Length&&keys.All(o.ContainsKey);
        static bool Number(JToken v)=>v?.Type is JTokenType.Integer or JTokenType.Float;
        static bool Vector(JToken token)=>token is JObject o&&Exact(o,"x","y")&&Number(o["x"])&&Number(o["y"]);
    }
    [Serializable] public sealed class RoomAppearance
    {
        public const int MaximumDefinitions=64;
        public int version=1;
        public string id,name;
        public AppearanceStyle style=new();
        public RoomAppearance Copy()=>new(){version=version,id=id,name=name,style=style?.Copy()};
        public bool Validate(out string error) {
            error="An appearance needs version 1, a stable ID and a readable name";
            if(version!=1||!RoomWorldIdentity.Id(id)||string.IsNullOrWhiteSpace(name)||name.Length>80||name.Any(char.IsControl)||style==null)return false;
            return style.Validate(out error);
        }
        internal static bool ValidateCollection(RoomAppearance[] values,RoomObjectData[] objects,int version,out string error) {
            error="Appearances require the current format and at most 64 distinct definitions";
            if(values==null||values.Length>MaximumDefinitions||version<25&&values.Length>0)return false;
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var p in values)if(p==null||!ids.Add(p.id)||!p.Validate(out error))return false;
            foreach(var obj in objects) {
                var bindings=obj.appearanceBindings;
                if(bindings==null&&version<25)continue;
                error="An object needs at most 33 distinct valid appearance bindings to existing definitions";
                if(bindings==null||bindings.Length>33||version<25&&bindings.Length>0)return false;
                if(!ValidateBindings(obj,ids,out error))return false;
            }
            error=null;return true;
        }
        internal static bool ValidateBindings(RoomObjectData obj,HashSet<string> ids,out string error) {
            error="An object needs at most 33 distinct valid appearance bindings to existing definitions";
            var bindings=obj.appearanceBindings;if(bindings==null||bindings.Length>33)return false;
            var slots=new HashSet<string>(StringComparer.Ordinal);
            foreach(var binding in bindings)if(binding==null||!binding.Validate(obj)||!ids.Contains(binding.appearanceId)||!slots.Add(binding.Key))return false;
            error="Bound root/part tint belongs to its appearance binding, not a second source pigment";
            if(bindings.Any(b=>b.kind=="root")&&obj.color!=Color.white)return false;
            foreach(var b in bindings.Where(b=>b.kind=="part")) {
                var part=obj.recipe?.parts?.FirstOrDefault(p=>p.id==b.partId);
                if(part!=null&&part.color!=Color.white)return false;
            }
            error=null;return true;
        }
        internal static bool ValidWire(JObject room) {
            if(room["version"]?.Type!=JTokenType.Integer)return false;
            if((int)room["version"]<25&&!room.ContainsKey("appearances"))return true;
            if(room["appearances"] is not JArray values||values.Count>MaximumDefinitions)return false;
            foreach(var token in values) {
                if(token is not JObject p||!AppearanceStyle.Exact(p,"version","id","name","style")||p["version"]?.Type!=JTokenType.Integer||p["id"]?.Type!=JTokenType.String||p["name"]?.Type!=JTokenType.String||!AppearanceStyle.Wire(p["style"]))return false;
                try{if(!JsonUtility.FromJson<RoomAppearance>(p.ToString()).Validate(out _))return false;}catch{return false;}
            }
            return room["objects"] is JArray items&&items.All(x=>x is JObject&&((int)room["version"]<25&&x["appearanceBindings"]==null||x["appearanceBindings"] is JArray bindings&&bindings.Count<=33&&bindings.All(AppearanceBinding.Wire)));
        }
    }
    /// <summary>More specific part/material bindings override root bindings.
    /// Missing parts or different model hashes retain an inactive binding rather
    /// than reinterpreting its stable address against different geometry.</summary>
    [Serializable] public sealed class AppearanceBinding
    {
        public int version=1;
        public string appearanceId,kind="root",partId="",modelHash="",tint="";
        public int materialIndex=-1;
        internal string Key=>kind+":"+partId+":"+modelHash+":"+materialIndex;
        public AppearanceBinding Copy()=>(AppearanceBinding)MemberwiseClone();
        internal AppearanceStyle Effective(RoomAppearance definition) {
            var result=definition.style.Copy();if(tint!="")result.tint=tint;return result;
        }
        internal bool Validate(RoomObjectData owner) {
            if(version!=1||!RoomWorldIdentity.Id(appearanceId)||partId==null||modelHash==null||tint==null||tint!=""&&!System.Text.RegularExpressions.Regex.IsMatch(tint,"^#[a-fA-F0-9]{6}$"))return false;
            return kind switch {
                "root"=>partId==""&&modelHash==""&&materialIndex==-1,
                "part"=>owner.kind==RoomObjectKind.Assembly&&RoomRecipe.ValidId(partId)&&modelHash==""&&materialIndex==-1,
                "material"=>owner.kind is RoomObjectKind.ImportedModel or RoomObjectKind.Maestro&&partId==""&&ModelLibrary.ValidHash(modelHash)&&materialIndex>=0&&materialIndex<1024,
                _=>false
            };
        }
        internal static bool Wire(JToken token)=>token is JObject b&&AppearanceStyle.Exact(b,"version","appearanceId","kind","partId","modelHash","materialIndex","tint")&&
            b["version"]?.Type==JTokenType.Integer&&b["materialIndex"]?.Type==JTokenType.Integer&&new[]{"appearanceId","kind","partId","modelHash","tint"}.All(k=>b[k]?.Type==JTokenType.String);
    }
    public sealed class AppearanceEdits
    {
        public RoomAppearance[] Replacements=Array.Empty<RoomAppearance>();
        public string[] Removals=Array.Empty<string>();
        public bool Validate(out string error) {
            error="Appearance edits need distinct valid definitions and removals";
            if(Replacements==null||Removals==null||Replacements.Length>RoomAppearance.MaximumDefinitions||Removals.Length>RoomAppearance.MaximumDefinitions)return false;
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var p in Replacements)if(p==null||!ids.Add(p.id)||!p.Validate(out error))return false;
            foreach(var id in Removals)if(!RoomWorldIdentity.Id(id)||!ids.Add(id))return false;
            error=null;return true;
        }
        internal RoomAppearance[] Apply(RoomAppearance[] current) {
            var values=current.ToDictionary(x=>x.id,x=>x.Copy(),StringComparer.Ordinal);
            foreach(var id in Removals)values.Remove(id);
            foreach(var p in Replacements)values[p.id]=p.Copy();
            return values.Values.OrderBy(x=>x.id,StringComparer.Ordinal).ToArray();
        }
    }
    public sealed partial class RoomJournal
    {
        readonly Dictionary<string,RoomAppearance> appearances=new(StringComparer.Ordinal);
        readonly Dictionary<string,int> appearanceRevisions=new(StringComparer.Ordinal);
        public int AppearanceRevision(string id)=>id!=null&&appearanceRevisions.TryGetValue(id,out var value)?value:0;
        public RoomAppearance ReadAppearance(string id)=>id!=null&&appearances.TryGetValue(id,out var value)?value.Copy():null;
        public RoomAppearance[] AppearanceSnapshot()=>appearances.Values.OrderBy(x=>x.id,StringComparer.Ordinal).Select(x=>x.Copy()).ToArray();
        void SetAppearances(RoomAppearance[] before,RoomAppearance[] after) {
            foreach(var p in before){appearances.Remove(p.id);appearanceRevisions.Remove(p.id);}
            foreach(var p in after){appearances[p.id]=p.Copy();appearanceRevisions[p.id]=clock.Next++;}
            // A shared edit changes each member's visible definition too. Old
            // object observations cannot authorize painting over that newer state.
            var changed=before.Select(p=>p.id).Concat(after.Select(p=>p.id)).ToHashSet();
            foreach(var item in items.Values)if(item.appearanceBindings.Any(b=>changed.Contains(b.appearanceId)))revisions[item.id]=clock.Next++;
        }
        [Serializable] sealed class AppearanceDelta {public RoomAppearance[] values;}
        static bool EquivalentAppearances(RoomAppearance[] a,RoomAppearance[] b)=>JsonUtility.ToJson(new AppearanceDelta{values=a.OrderBy(x=>x.id).ToArray()})==JsonUtility.ToJson(new AppearanceDelta{values=b.OrderBy(x=>x.id).ToArray()});
    }
}
