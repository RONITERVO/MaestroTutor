// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Linq;
using Maestro.Quest.Interaction;
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
        internal bool AuthoredCollisionReady(RoomItem item)
        {
            if(!item||!item.isActiveAndEnabled||!item.Grab)return false;
            string id=Identity(item);
            if(id!=null&&journal?.ImportedEntity(id)==true&&!ResidentGeometryReady(item,item.GetComponent<CreatedRoomObject>()))return false;
            var rigid=item.GetComponent<RigidRoomItem>();if(rigid&&!rigid.GeometryReady)return false;
            foreach(var collider in item.Grab.colliders){
                if(!collider)return false;if(collider.isTrigger)continue;
                int layer=collider.gameObject.layer;
                if(!collider.enabled||!collider.gameObject.activeInHierarchy||layer!=RoomPhysicsLayers.Environment&&layer!=RoomPhysicsLayers.Item)return false;
            }
            // A restoration hold freezes movement, not complete visible solids.
            return true;
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
