// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    // Saved intent only. Native source ownership and readiness are checked separately.
    [Serializable] public sealed class RoomStaticSupport
    {
        public int version=1;
        public bool enabled;
        internal bool Valid=>version==1;
        internal bool Default=>Valid&&!enabled;
        internal bool Absent=>version==0&&!enabled;
        internal RoomStaticSupport Copy()=>new(){version=version,enabled=enabled};
        internal bool Same(RoomStaticSupport other)=>other!=null&&version==other.version&&enabled==other.enabled;
        internal static bool Validate(RoomObjectData data,out string error)
        {
            error="Invalid static support settings";
            if(data?.staticSupport==null||!data.staticSupport.Valid)return false;
            if(!data.staticSupport.enabled){error=null;return true;}
            error="Static support requires a fixed created object without scanned anchoring or recorded root motion";
            if(data.IsBuiltIn||ScanDrawingAnchor.Has(data)||data.physics!=ItemPhysics.Fixed||data.motion!=null)return false;
            bool imported=data.kind==RoomObjectKind.ImportedModel;
            error="Imported rigid geometry owns its own walkable setting; ordinary model support needs automatic explicit collision shapes";
            if(imported&&(data.modelGeometry==null||data.modelGeometry.meshCollision||data.collisionShape!=ItemCollider.Automatic))return false;
            error="Static support requires the complete selected box or compatible mesh collision source";
            if(data.collisionShape==ItemCollider.Sphere)return false;
            if(!imported&&data.collisionShape==ItemCollider.Box){error=null;return true;}
            if(data.collisionShape!=ItemCollider.Automatic)return false;
            var collision=data.collision;
            if((collision?.shapes?.Length??0)>0){
                if(!collision.Validate(out error))return false;
                if(collision.shapes.Any(shape=>shape.shape is not ("box" or "cylinder" or "ring"))){error="Every selected static support shape must be a box, cylinder or ring";return false;}
                error=null;return true;
            }
            if(imported||data.kind is not (RoomObjectKind.Block or RoomObjectKind.Drawing or RoomObjectKind.Assembly))return false;
            error=null;return true;
        }
        // Shared source format is applied after structural schema validation.
        internal static bool ValidSource(JObject source,out string error)
        {
            var owner=new RoomObjectData{kind=RoomObjectKind.Assembly,physics=ItemPhysics.Fixed,
                staticSupport=source["staticSupport"] is JObject support?UnityEngine.JsonUtility.FromJson<RoomStaticSupport>(support.ToString()):new(),
                collision=source["collision"] is JObject collision?UnityEngine.JsonUtility.FromJson<CollisionRecipe>(collision.ToString()):null};
            if(source["physics"] is JObject physics&&!RoomControls.SetPhysics(owner,UnityEngine.JsonUtility.FromJson<ObjectPhysicsSettings>(physics.ToString()),out error))return false;
            return Validate(owner,out error);
        }
        internal static bool ValidWire(JObject room)
        {
            int roomVersion=(int?)room["version"]??0;if(room["objects"] is not JArray objects)return false;
            foreach(var token in objects){
                if(token is not JObject item)return false;
                var value=item["staticSupport"];
                if(value==null||value.Type==JTokenType.Null){if(roomVersion>=47)return false;continue;}
                if(value is not JObject settings||settings.Count!=2||settings["version"]?.Type!=JTokenType.Integer||settings["enabled"]?.Type!=JTokenType.Boolean)return false;
                var support=settings.ToObject<RoomStaticSupport>();if(!support.Valid||roomVersion<47&&!support.Default)return false;
            }
            return true;
        }
    }
}
