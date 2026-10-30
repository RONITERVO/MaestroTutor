// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation {
    // Presentation of the containing water body, independent of its surface
    // material, fluid identity, buoyancy, visibility layer and real-room policy.
    [Serializable] public sealed class UnderwaterViewSettings {
        public int version=1;
        public bool enabled;
        public string tint="#285B78";
        public float densityPerMetre=.4f;
        internal bool Absent=>version==0&&!enabled&&string.IsNullOrEmpty(tint)&&densityPerMetre==0;
        internal bool Valid=>version==1&&tint!=null&&System.Text.RegularExpressions.Regex.IsMatch(tint,"^#[a-fA-F0-9]{6}$")&&float.IsFinite(densityPerMetre)&&densityPerMetre>=0&&densityPerMetre<=8;
        internal UnderwaterViewSettings Copy()=>new(){version=version,enabled=enabled,tint=tint,densityPerMetre=densityPerMetre};
        internal static readonly UnderwaterViewSettings Default=new();
        internal static UnderwaterViewSettings Effective(UnderwaterViewSettings value)=>value==null||value.Absent?Default:value;
        internal Color LinearTint {get{ColorUtility.TryParseHtmlString(tint,out var c);return QualitySettings.activeColorSpace==ColorSpace.Linear?c.linear:c;}}
        internal static bool Wire(JToken token){
            if(token is not JObject v||!AppearanceStyle.Exact(v,"version","enabled","tint","densityPerMetre")||v["version"]?.Type!=JTokenType.Integer||v["enabled"]?.Type!=JTokenType.Boolean||v["tint"]?.Type!=JTokenType.String||v["densityPerMetre"]?.Type is not (JTokenType.Integer or JTokenType.Float))return false;
            try{return v.ToObject<UnderwaterViewSettings>().Valid;}catch{return false;}
        }
        internal static bool ValidWire(JObject room){
            if(room["objects"] is not JArray objects)return false;
            int version=(int?)room["version"]??0;
            foreach(var c in objects.OfType<JObject>().SelectMany(o=>(o["containers"] as JArray??new JArray()).OfType<JObject>())){
                if(c["underwater"]==null||c["underwater"].Type==JTokenType.Null){if(version>=45)return false;continue;}
                if(!Wire(c["underwater"]))return false;
            }
            return true;
        }
    }
}
