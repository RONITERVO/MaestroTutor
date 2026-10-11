// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation {
    // Properties travel with liquid identity, never with the vessel's paint/name.
    [Serializable] public sealed class RoomFluid {
        public int version=1;
        public float densityKgM3=1000,linearDrag=2,angularDrag=1;
        internal bool Absent=>version==0&&densityKgM3==0&&linearDrag==0&&angularDrag==0;
        internal bool Valid=>version==1&&RoomLighting.Range(densityKgM3,1,20000)&&RoomLighting.Range(linearDrag,0,20)&&RoomLighting.Range(angularDrag,0,20);
        internal RoomFluid Copy()=>new(){version=version,densityKgM3=densityKgM3,linearDrag=linearDrag,angularDrag=angularDrag};
        internal static readonly RoomFluid Default=new();
        internal static RoomFluid Effective(RoomFluid value)=>value==null||value.Absent?Default:value;
        internal static bool Same(RoomFluid a,RoomFluid b){a=Effective(a);b=Effective(b);return a.version==b.version&&a.densityKgM3==b.densityKgM3&&a.linearDrag==b.linearDrag&&a.angularDrag==b.angularDrag;}
        internal static bool ValidWire(JObject room){
            int version=(int?)room["version"]??0;
            if(room["objects"] is not JArray objects)return false;
            foreach(var container in objects.OfType<JObject>().SelectMany(o=>(o["containers"] as JArray??new JArray()).OfType<JObject>())){
                if(container["fluid"]==null||container["fluid"].Type==JTokenType.Null){if(version>=30)return false;continue;}
                if(container["fluid"] is not JObject f||f.Count!=4||f["version"]?.Type!=JTokenType.Integer||(int)f["version"]!=1)return false;
                foreach(string key in new[]{"densityKgM3","linearDrag","angularDrag"})if(f[key]?.Type is not (JTokenType.Float or JTokenType.Integer))return false;
                if(!f.ToObject<RoomFluid>().Valid)return false;
            }
            return true;
        }
    }
}
