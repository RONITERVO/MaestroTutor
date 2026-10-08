// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    /// <summary>Authored illumination of this region. Independent of viewer presentation and physical-room policy.</summary>
    [Serializable] public sealed class RoomLighting
    {
        public int version=1;
        public bool enabled;
        public string ambientColor="#FFFFFF",sunColor="#FFF4D6";
        public float ambientIntensity=.35f,sunIntensity=.65f,azimuth=-150,elevation=55;
        public RoomLighting Copy()=>(RoomLighting)MemberwiseClone();
        internal static bool ColorValid(string v)=>v?.Length==7&&Regex.IsMatch(v,"^#[0-9A-Fa-f]{6}$");
        internal static bool Range(float v,float min,float max)=>float.IsFinite(v)&&v>=min&&v<=max;
        public bool Valid=>version==1&&ColorValid(ambientColor)&&ColorValid(sunColor)&&Range(ambientIntensity,0,2)&&Range(sunIntensity,0,2)&&Range(azimuth,-180,180)&&Range(elevation,-90,90);
        internal bool Same(RoomLighting other)=>other!=null&&JsonUtility.ToJson(this)==JsonUtility.ToJson(other);
        // Direction TOWARDS the sun: +Z at azimuth zero, +X at +90. Not a camera or device pose.
        internal Vector3 SunDirection=>new(Mathf.Sin(azimuth*Mathf.Deg2Rad)*Mathf.Cos(elevation*Mathf.Deg2Rad),Mathf.Sin(elevation*Mathf.Deg2Rad),Mathf.Cos(azimuth*Mathf.Deg2Rad)*Mathf.Cos(elevation*Mathf.Deg2Rad));
        internal static bool ValidWire(JObject room) {
            if(room["version"]?.Type!=JTokenType.Integer)return false;
            if((int)room["version"]<27&&room["lighting"]==null)return true;
            if(room["lighting"] is not JObject p||p.Count!=8||p["version"]?.Type!=JTokenType.Integer||p["enabled"]?.Type!=JTokenType.Boolean||p["ambientColor"]?.Type!=JTokenType.String||p["sunColor"]?.Type!=JTokenType.String)return false;
            return new[]{"ambientIntensity","sunIntensity","azimuth","elevation"}.All(k=>p[k]?.Type is JTokenType.Integer or JTokenType.Float);
        }
    }
    public sealed partial class RoomJournal
    {
        RoomLighting lighting=new();int lightingRevision;
        internal RoomLighting Lighting=>lighting.Copy();
        internal int LightingRevision=>lightingRevision;
        void SetLighting(RoomLighting value){if(value==null)return;lighting=value.Copy();lightingRevision=clock.Next++;}
    }
    public sealed partial class RoomEditor
    {
        internal RoomLighting Lighting=>journal?.Lighting;
        internal int LightingRevision=>journal?.LightingRevision??0;
        internal bool CanSetLighting(int revision,RoomLighting value,out string error) {
            if(!CanEditStructures(out error))return false;
            if(!WorldIdentityReady||revision!=LightingRevision){error="Read the current world lighting before changing it";return false;}
            if(DayCycleEnabled){error="Disable the world.time lighting cycle before editing static lighting";return false;}
            if(value==null||!value.Valid){error="Choose valid lighting colours, intensities and sun angles";return false;}
            return true;
        }
        internal bool SetLighting(int revision,RoomLighting value,out string error) {
            if(!CanSetLighting(revision,value,out error))return false;
            return CommitPersisted(Array.Empty<RoomObjectData>(),Array.Empty<string>(),"World lighting saved",false,out error,visualOnly:true,lighting:value);
        }
        internal JObject ObserveLighting()=>journal==null?null:new JObject{["revision"]=LightingRevision,["worldId"]=WorldIdentity.worldId,["regionId"]=WorldIdentity.regionId,["settings"]=JObject.Parse(JsonUtility.ToJson(Lighting)),["temporary"]=TemporaryRoom};
    }
}
