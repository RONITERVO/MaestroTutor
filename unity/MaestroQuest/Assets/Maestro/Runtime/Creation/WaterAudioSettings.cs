// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation {
    // Presentation of the containing water body, independent of its surface
    // material, fluid identity, buoyancy, visibility layer and real-room policy.
    [Serializable] public sealed class WaterAudioSettings {
        public int version=1;
        public bool enabled;
        public float attenuationDbPerMetre=1.5f;
        public float mufflingPerMetre=1;
        internal bool Absent=>version==0&&!enabled&&attenuationDbPerMetre==0&&mufflingPerMetre==0;
        internal bool Valid=>version==1&&float.IsFinite(attenuationDbPerMetre)&&attenuationDbPerMetre>=0&&attenuationDbPerMetre<=24&&float.IsFinite(mufflingPerMetre)&&mufflingPerMetre>=0&&mufflingPerMetre<=8;
        internal WaterAudioSettings Copy()=>new(){version=version,enabled=enabled,attenuationDbPerMetre=attenuationDbPerMetre,mufflingPerMetre=mufflingPerMetre};
        internal static readonly WaterAudioSettings Default=new();
        internal static WaterAudioSettings Effective(WaterAudioSettings value)=>value==null||value.Absent?Default:value;
        internal static bool Wire(JToken token){
            if(token is not JObject v||!AppearanceStyle.Exact(v,"version","enabled","attenuationDbPerMetre","mufflingPerMetre")||v["version"]?.Type!=JTokenType.Integer||v["enabled"]?.Type!=JTokenType.Boolean||v["attenuationDbPerMetre"]?.Type is not (JTokenType.Integer or JTokenType.Float)||v["mufflingPerMetre"]?.Type is not (JTokenType.Integer or JTokenType.Float))return false;
            try{return v.ToObject<WaterAudioSettings>().Valid;}catch{return false;}
        }
        internal static bool ValidWire(JObject room){
            if(room["objects"] is not JArray objects)return false;
            int version=(int?)room["version"]??0;
            foreach(var c in objects.OfType<JObject>().SelectMany(o=>(o["containers"] as JArray??new JArray()).OfType<JObject>())){
                if(c["waterAudio"]==null||c["waterAudio"].Type==JTokenType.Null){if(version>=46)return false;continue;}
                if(!Wire(c["waterAudio"]))return false;
            }
            return true;
        }
    }
}
