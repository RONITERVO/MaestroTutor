// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Linq;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    internal enum RoomSpatialReadiness { Ready,Preparing,Unavailable }
    public sealed partial class RoomJournal
    {
        // Metadata reads must not clone an object's potentially large authored payload.
        internal bool ImportedEntity(string target)=>items.TryGetValue(target,out var data)&&data.kind==RoomObjectKind.ImportedModel;
    }
    public sealed partial class RoomEditor
    {
        // Passive proof for collision consumers after broad-phase discovery.
        // Residency/bounds alone do not prove that registered native shapes can
        // currently answer PhysX queries. Never activate or retry on this path.
        internal RoomSpatialReadiness SpatialCollisionReady(string[] targets,out string error)
        {
            var state=SpatialMovementReady(targets,null,out error);if(state!=RoomSpatialReadiness.Ready)return state;
            foreach(var target in targets)if(!AuthoredCollisionReady(Find(target))){error="Required collision geometry is unavailable";return RoomSpatialReadiness.Unavailable;}
            error=null;return RoomSpatialReadiness.Ready;
        }
        internal bool AuthoredCollisionReady(RoomItem item)=>AuthoredCollisionReadiness(item,out _)==RoomSpatialReadiness.Ready;
        RoomSpatialReadiness AuthoredCollisionReadiness(RoomItem item,out string reason)
        {
            reason="The native object is unavailable";
            if(!item)return RoomSpatialReadiness.Unavailable;
            reason="The native object is inactive";
            if(!item.isActiveAndEnabled)return RoomSpatialReadiness.Unavailable;
            reason="Registered collision geometry is unavailable";
            if(!item.Grab)return RoomSpatialReadiness.Unavailable;
            string id=Identity(item);
            if(id!=null&&journal?.ImportedEntity(id)==true){
                var model=item.GetComponent<CreatedRoomObject>();
                if(!ResidentGeometryReady(item,model)){
                    reason="Imported collision geometry is unavailable";
                    if(!ResidentGeometryLost(id)&&model&&model.isActiveAndEnabled&&model.ModelLoading){
                        reason="Preparing imported collision geometry";return RoomSpatialReadiness.Preparing;
                    }
                    return RoomSpatialReadiness.Unavailable;
                }
            }
            var rigid=item.GetComponent<RigidRoomItem>();
            reason="Registered collision geometry is incomplete";
            if(rigid&&!rigid.GeometryReady)return RoomSpatialReadiness.Unavailable;
            foreach(var collider in item.Grab.colliders){
                if(!collider)return RoomSpatialReadiness.Unavailable;if(collider.isTrigger)continue;
                int layer=collider.gameObject.layer;
                if(!collider.enabled||!collider.gameObject.activeInHierarchy||layer!=RoomPhysicsLayers.Environment&&layer!=RoomPhysicsLayers.Item)return RoomSpatialReadiness.Unavailable;
            }
            // An accepted empty solid set is complete. A restoration hold or
            // paused physics freezes movement, not complete query geometry.
            reason="";return RoomSpatialReadiness.Ready;
        }
        internal JObject ObserveCollisionAvailability(string target)
        {
            if(!this||!isActiveAndEnabled||journal==null||!HasSavedObject(target))return null;
            RoomSpatialReadiness state;string reason;
            if(NativeEntityDormant(target)){
                bool pending=NativeContentPending(target);
                state=pending?RoomSpatialReadiness.Preparing:RoomSpatialReadiness.Unavailable;
                reason=pending?"Native object loading is requested":"The saved object is not loaded";
            }else state=AuthoredCollisionReadiness(Find(target),out reason);
            return new JObject{["target"]=target,["state"]=state==RoomSpatialReadiness.Ready?"ready":state==RoomSpatialReadiness.Preparing?"pending":"unavailable",["reason"]=reason};
        }
        internal RoomSpatialReadiness SpatialMovementReady(string[] targets,Bounds? ground,out string error)
        {
            error="The movement workspace is unavailable";if(!this||!isActiveAndEnabled||journal==null)return RoomSpatialReadiness.Unavailable;
            foreach(var target in targets){
                if(NativeEntityDormant(target)){error="Preparing ground and obstacles";return RoomSpatialReadiness.Preparing;}
                var item=Find(target);
                if(!item||!item.isActiveAndEnabled){error="Required movement geometry is unavailable";return RoomSpatialReadiness.Unavailable;}
                if(!journal.ImportedEntity(target))continue;
                var model=item.GetComponent<CreatedRoomObject>();
                if(model&&model.isActiveAndEnabled&&model.ModelGeometryReady)continue;
                error="Required imported movement geometry is unavailable";
                if(!ResidentGeometryLost(target)&&model&&model.isActiveAndEnabled&&model.ModelLoading){error="Preparing imported ground and obstacles";return RoomSpatialReadiness.Preparing;}
                return RoomSpatialReadiness.Unavailable;
            }
            if(ground.HasValue){
                var missing=GroundDependencies(ground.Value);
                if(missing.Count>0){
                    bool pending=missing.All(x=>(string)x["state"]=="pending");
                    error=pending?"Preparing accepted ground":"Required accepted ground is unavailable";
                    return pending?RoomSpatialReadiness.Preparing:RoomSpatialReadiness.Unavailable;
                }
            }
            error=null;return RoomSpatialReadiness.Ready;
        }
    }
}
