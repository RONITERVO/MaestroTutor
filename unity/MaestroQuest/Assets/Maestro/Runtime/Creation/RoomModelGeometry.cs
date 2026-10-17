// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    [Serializable] public sealed class RoomModelGeometry
    {
        public int version=1;
        public string scaleMode="fitted",pivot="center";
        public float metresPerUnit=1;
        public bool meshCollision,walkable;
        internal bool Absent=>version==0&&string.IsNullOrEmpty(scaleMode)&&string.IsNullOrEmpty(pivot)&&metresPerUnit==0&&!meshCollision&&!walkable;
        internal bool Valid=>version==1&&(scaleMode is "fitted" or "source")&&(pivot is "center" or "base" or "source")&&
            RoomLighting.Range(metresPerUnit,.001f,100)&&(scaleMode!="fitted"||metresPerUnit==1)&&(!walkable||meshCollision);
        internal bool Default=>Valid&&scaleMode=="fitted"&&pivot=="center"&&metresPerUnit==1&&!meshCollision&&!walkable;
        internal bool Same(RoomModelGeometry other)=>other!=null&&version==other.version&&scaleMode==other.scaleMode&&pivot==other.pivot&&metresPerUnit==other.metresPerUnit&&meshCollision==other.meshCollision&&walkable==other.walkable;
        internal RoomModelGeometry Copy()=>new(){version=version,scaleMode=scaleMode,pivot=pivot,metresPerUnit=metresPerUnit,meshCollision=meshCollision,walkable=walkable};
        internal static bool Validate(RoomObjectData data,out string error)
        {
            error="Invalid imported model geometry settings";var value=data.modelGeometry;
            if(value==null||!value.Valid||data.kind!=RoomObjectKind.ImportedModel&&!value.Default)return false;
            if(value.meshCollision&&(data.physics!=ItemPhysics.Fixed||data.collisionShape!=ItemCollider.Automatic||(data.collision?.shapes?.Length??0)>0||data.motion!=null))
            {error="Rigid mesh collision needs fixed physics, automatic shape, no custom collision shapes and no recorded root animation";return false;}
            error=null;return true;
        }
        internal static bool ValidWire(JObject room)
        {
            int version=(int?)room["version"]??0;if(room["objects"] is not JArray objects)return false;
            foreach(var token in objects)
            {
                if(token is not JObject item)return false;
                var value=item["modelGeometry"];
                if(value==null||value.Type==JTokenType.Null){if(version>=35)return false;continue;}
                if(value is not JObject p||p.Count!=6||p["version"]?.Type!=JTokenType.Integer||p["scaleMode"]?.Type!=JTokenType.String||p["pivot"]?.Type!=JTokenType.String||
                    p["metresPerUnit"]?.Type is not (JTokenType.Integer or JTokenType.Float)||p["meshCollision"]?.Type!=JTokenType.Boolean||p["walkable"]?.Type!=JTokenType.Boolean)return false;
                var settings=p.ToObject<RoomModelGeometry>();if(!settings.Valid||version<35&&!settings.Default)return false;
            }
            return true;
        }
    }
}
