// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    /// <summary>Authored local light, independent of collision, material and real-world presentation.</summary>
    [Serializable] public sealed class RoomLight
    {
        public const int MaximumPerObject=2,MaximumPerRoom=4,MaximumShadowFaces=6;
        public int version=1;
        public string id="Lamp",kind="point",part="",color="#FFF4D6",shadows="none";
        public Vector3 position;
        public Quaternion rotation=Quaternion.identity;
        public bool enabled=true;
        public float intensity=1,range=3,angle=60;
        internal int ShadowFaces=>shadows=="hard"?(kind=="point"?6:1):0;
        public RoomLight Copy()=>(RoomLight)MemberwiseClone();
        internal bool Valid(RoomObjectData owner)=>version==1&&DrawingSurface.Name(id)&&kind is "point" or "spot"&&part!=null&&(part==""||owner.recipe?.parts.Any(p=>p.id==part)==true)&&
            float.IsFinite(position.sqrMagnitude)&&position.sqrMagnitude<=100&&MotionFrame.ValidRotation(rotation)&&RoomLighting.ColorValid(color)&&
            RoomLighting.Range(intensity,0,4)&&RoomLighting.Range(range,.1f,8)&&RoomLighting.Range(angle,10,160)&&(kind!="point"||angle==60)&&shadows is "none" or "hard";
        internal static bool ValidateCollection(RoomObjectData owner,out string error){
            error="Keep at most two distinct valid lamps on a created object; remove a lamp before removing its recipe part";
            var all=owner.lights;if(all==null||all.Length>MaximumPerObject||owner.IsBuiltIn&&all.Length>0||all.Any(l=>l==null||!l.Valid(owner))||all.Select(l=>l.id).Distinct().Count()!=all.Length)return false;
            if(all.Sum(l=>l.ShadowFaces)>MaximumShadowFaces){error="Local shadows exceed six reserved faces (a point light uses six, a spot uses one)";return false;}
            error=null;return true;
        }
        internal static bool ValidWire(JObject root){
            if(root["objects"] is not JArray objects)return false;
            string[] fields={"version","id","kind","part","color","shadows","position","rotation","enabled","intensity","range","angle"};
            bool Number(JToken t)=>t?.Type is JTokenType.Integer or JTokenType.Float;
            bool Vector(JToken t,string[] names)=>t is JObject v&&v.Count==names.Length&&names.All(k=>Number(v[k]));
            foreach(var obj in objects.OfType<JObject>()){
                if(obj["lights"]==null){if((int?)root["version"]>=39)return false;continue;}
                if(obj["lights"] is not JArray all)return false;
                foreach(var token in all){if(token is not JObject l||l.Count!=fields.Length||fields.Any(k=>!l.ContainsKey(k))||l["version"]?.Type!=JTokenType.Integer||l["enabled"]?.Type!=JTokenType.Boolean||new[]{"id","kind","part","color","shadows"}.Any(k=>l[k]?.Type!=JTokenType.String)||new[]{"intensity","range","angle"}.Any(k=>!Number(l[k]))||!Vector(l["position"],new[]{"x","y","z"})||!Vector(l["rotation"],new[]{"x","y","z","w"}))return false;}
            }
            return true;
        }
    }
}
