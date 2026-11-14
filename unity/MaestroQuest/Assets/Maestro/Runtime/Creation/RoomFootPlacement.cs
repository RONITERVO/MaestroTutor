// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Newtonsoft.Json.Linq;
using Maestro.Quest.Imports;
namespace Maestro.Quest.Creation
{
    // Presentation preferences never contain a sampled pose or a world position.
    // A saved calibration remains bound to the exact selected model.
    [Serializable]
    public sealed class RoomFootPlacement
    {
        public int version=1;
        public bool enabled;
        public string modelHash="";
        public float leftClearance,rightClearance;
        internal bool Absent=>version==0&&!enabled&&string.IsNullOrEmpty(modelHash)&&leftClearance==0&&rightClearance==0;
        public bool Default=>version==1&&!enabled&&modelHash==""&&leftClearance==0&&rightClearance==0;
        public bool Valid=>version==1&&modelHash!=null&&(modelHash==""||ModelLibrary.ValidHash(modelHash))&&
            float.IsFinite(leftClearance)&&float.IsFinite(rightClearance)&&
            Math.Abs(leftClearance)<=.1f&&Math.Abs(rightClearance)<=.1f&&(enabled||Default);
        public RoomFootPlacement Copy()=>new(){version=version,enabled=enabled,modelHash=modelHash,leftClearance=leftClearance,rightClearance=rightClearance};
        internal static bool ValidWire(JObject room)
        {
            int version=(int?)room["version"]??0;
            if(room["objects"] is not JArray objects)return false;
            foreach(var token in objects){
                if(token is not JObject item)return false;
                if(item["footPlacement"]==null||item["footPlacement"].Type==JTokenType.Null){if(version>=41)return false;continue;}
                if(item["footPlacement"] is not JObject p||p.Count!=5||p["version"]?.Type!=JTokenType.Integer||
                    p["enabled"]?.Type!=JTokenType.Boolean||p["modelHash"]?.Type!=JTokenType.String||
                    p["leftClearance"]?.Type is not (JTokenType.Float or JTokenType.Integer)||p["rightClearance"]?.Type is not (JTokenType.Float or JTokenType.Integer)||
                    !p.ToObject<RoomFootPlacement>().Valid||version<41&&!p.ToObject<RoomFootPlacement>().Default)return false;
            }
            return true;
        }
    }
}
