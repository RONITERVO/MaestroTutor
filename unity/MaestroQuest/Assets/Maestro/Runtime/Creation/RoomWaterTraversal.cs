// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation {
    // Locomotion policy, independent of collision, appearance, buoyancy and grips.
    [Serializable] public sealed class RoomWaterTraversal {
        public int version=1;
        public string mode="default";
        public float maxDepthMetres=.25f;
        internal bool Absent=>version==0&&string.IsNullOrEmpty(mode)&&maxDepthMetres==0;
        internal bool Valid=>version==1&&(mode is "default" or "ignore" or "avoid" or "wade")&&RoomLighting.Range(maxDepthMetres,0,2);
        internal bool Default=>Valid&&mode=="default"&&maxDepthMetres==.25f;
        internal RoomWaterTraversal Copy()=>new(){version=version,mode=mode,maxDepthMetres=maxDepthMetres};
        internal static RoomWaterTraversal Effective(RoomObjectData data){
            var value=(data.waterTraversal??new RoomWaterTraversal()).Copy();
            if(value.mode=="default")value.mode=data.kind==RoomObjectKind.Maestro?"avoid":"ignore";
            return value;
        }
        internal static bool ValidWire(JObject room){
            int version=(int?)room["version"]??0;
            if(room["objects"] is not JArray objects)return false;
            foreach(var token in objects){
                if(token is not JObject item)return false;
                if(item["waterTraversal"]==null||item["waterTraversal"].Type==JTokenType.Null){if(version>=31)return false;continue;}
                if(item["waterTraversal"] is not JObject p||p.Count!=3||p["version"]?.Type!=JTokenType.Integer||p["mode"]?.Type!=JTokenType.String||p["maxDepthMetres"]?.Type is not (JTokenType.Float or JTokenType.Integer)||!p.ToObject<RoomWaterTraversal>().Valid)return false;
                if(version<31&&!p.ToObject<RoomWaterTraversal>().Default)return false;
            }
            return true;
        }
    }
}
