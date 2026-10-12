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
        internal RoomSpatialReadiness SpatialMovementReady(string[] targets,Bounds? ground,out string error)
        {
            error="The movement workspace is unavailable";if(!this||!isActiveAndEnabled||journal==null)return RoomSpatialReadiness.Unavailable;
            foreach(var target in targets){
                if(NativeEntityDormant(target)){error="Preparing ground and obstacles";return RoomSpatialReadiness.Preparing;}
                var item=Find(target);
                if(!item||!item.isActiveAndEnabled){error="Required movement geometry is unavailable";return RoomSpatialReadiness.Unavailable;}
                if(!journal.ImportedEntity(target))continue;
                var model=item.GetComponent<CreatedRoomObject>();
                if(model&&model.ModelGeometryReady)continue;
                error="Required imported movement geometry is unavailable";
                if(model&&model.ModelLoading){error="Preparing imported ground and obstacles";return RoomSpatialReadiness.Preparing;}
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
