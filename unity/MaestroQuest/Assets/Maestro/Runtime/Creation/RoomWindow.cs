// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    /// <summary>A visual opening on an existing explicit plane. No collision or tracking data.</summary>
    [Serializable] public sealed class RoomWindow
    {
        public const int MaximumPerObject=4,MaximumPerRoom=16;
        public int version=1;
        public string id="Window",surface="Canvas",shape="rectangle";
        public float reveal=1;
        public RoomWindow Copy()=>(RoomWindow)MemberwiseClone();
        internal static bool ValidateCollection(RoomObjectData owner,out string error) {
            error="Windows need distinct IDs on at most four distinct plane surfaces per created object";
            var all=owner.windows;
            if(all==null||all.Length>MaximumPerObject||owner.IsBuiltIn&&all.Length>0||all.Any(w=>w==null)||all.Select(w=>w.id).Distinct().Count()!=all.Length||all.Select(w=>w.surface).Distinct().Count()!=all.Length)return false;
            foreach(var w in all){
                if(w.version!=1||!DrawingSurface.Name(w.id)||!DrawingSurface.Name(w.surface)||w.shape is not ("rectangle" or "ellipse")||!float.IsFinite(w.reveal)||w.reveal<0||w.reveal>1)return false;
                error="A passthrough opening must reference an existing plane; remove its window before removing or curving that surface";
                if(owner.surfaces?.Any(s=>s!=null&&s.id==w.surface&&s.Kind=="plane")!=true)return false;
            }
            error=null;return true;
        }
        internal static bool ValidWire(JObject root) {
            if(root["objects"] is not JArray objects)return false;
            foreach(var obj in objects.OfType<JObject>()){
                if(obj["windows"]==null){if((int?)root["version"]>=34)return false;continue;}
                if(obj["windows"] is not JArray all)return false;
                foreach(var value in all){
                    if(value is not JObject w||w.Count!=5||w["version"]?.Type!=JTokenType.Integer||w["id"]?.Type!=JTokenType.String||w["surface"]?.Type!=JTokenType.String||w["shape"]?.Type!=JTokenType.String||w["reveal"]?.Type is not (JTokenType.Float or JTokenType.Integer))return false;
                }
            }
            return true;
        }
    }
}
