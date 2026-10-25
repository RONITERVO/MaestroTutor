// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Avatar {
    // Transient native focus. Object identity is pinned; an ID reuse cannot
    // silently redirect an existing attention owner to a replacement instance.
    internal sealed class AvatarAttentionTarget {
        internal readonly string Kind, ObjectId;
        readonly RoomItem item; readonly Vector3 point;
        internal AvatarAttentionTarget(RoomEditor editor,JObject value){
            Kind=(string)value["kind"];ObjectId=Kind=="object"?(string)value["objectId"]:"";
            item=Kind=="object"?editor.Find(ObjectId):null;
            point=Kind=="point"?new Vector3((float)value["point"]["x"],(float)value["point"]["y"],(float)value["point"]["z"]):default;
        }
        internal bool Read(RoomEditor editor,Transform viewer,out Vector3 value,out string error){
            value=default;error="The attention target or authored world is unavailable";
            if(!editor||!editor.Frame.Valid)return false;
            if(Kind=="user"){if(!viewer||!viewer.gameObject.activeInHierarchy)return false;value=viewer.position;}
            else if(Kind=="point")value=editor.Frame.PointToWorld(point);
            else if(Kind=="object"){
                if(!item||!item.isActiveAndEnabled||editor.Find(ObjectId)!=item||!item.Grab)return false;
                var avatar=item.GetComponent<MaestroAvatar>();if(avatar&&avatar.ModelBusy)return false;
                bool found=false;Bounds bounds=default;
                foreach(var c in item.SolidColliders){
                    if(!c||!c.enabled||c.isTrigger||!c.gameObject.activeInHierarchy||c.gameObject.layer==RoomPhysicsLayers.Pending)continue;
                    if(!found){bounds=c.bounds;found=true;}else bounds.Encapsulate(c.bounds);
                }
                if(!found){error="The attention object needs available collision bounds";return false;}value=bounds.center;
            }else return false;
            if(!RoomRecipe.Finite(value))return false;error=null;return true;
        }
    }
    public sealed partial class AvatarSpatialMotion {
        AvatarAttentionTarget attention;
        Vector3 attentionPoint;bool attentionSampled,attentionLimited,attentionApplied;
        void ResetAttention(){attention=null;attentionPoint=default;attentionSampled=attentionLimited=attentionApplied=false;}
        internal bool AttentionReady(out string error){
            if(!isActiveAndEnabled||!Active||mode!=AvatarSpatialMode.Look){error=Status;return false;}
            return ReadAttention(out _,out error);
        }
        internal bool BeginAttention(string identity,AvatarAttentionTarget target,out string error){
            error="An attention target is required";
            if(target==null||!target.Read(editor,room?.Viewer,out _,out error))return false;
            if(!Begin(identity,AvatarSpatialMode.Look,out error,RoomActorRole.Program))return false;
            attention=target;
            if(ReadAttention(out _,out error)){Say(target.Kind=="user"?"Looking at you — Stop or pose Maestro to end":target.Kind=="object"?"Looking toward the object — Stop or pose Maestro to end":"Looking toward the chosen place — Stop or pose Maestro to end");return true;}
            Stop();Say(error);return false;
        }
        bool ReadAttention(out Vector3 point,out string error){
            if(attention!=null)return attention.Read(editor,room?.Viewer,out point,out error);
            point=room&&room.Viewer?room.Viewer.position:default;
            error=room&&room.Viewer&&room.Viewer.gameObject.activeInHierarchy&&RoomRecipe.Finite(point)?null:"The tracked user is unavailable";
            return error==null;
        }
        internal JObject ObserveAttention(){
            bool active=Active&&mode!=AvatarSpatialMode.Manual;
            var point=active&&attentionSampled?attentionPoint:Vector3.zero;
            return new JObject{["active"]=active,["focus"]=active?attention?.Kind??"user":"none",["objectId"]=active?attention?.ObjectId??"":"",
                ["point"]=new JObject{["x"]=point.x,["y"]=point.y,["z"]=point.z},
                ["yaw"]=active?yaw:0,["pitch"]=active?pitch:0,["limited"]=active&&attentionLimited,["applied"]=active&&attentionSampled&&attentionApplied};
        }
    }
}
