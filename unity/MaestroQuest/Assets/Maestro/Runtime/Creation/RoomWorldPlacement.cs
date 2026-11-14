// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        internal JObject ObserveWorldViewpoint()=>GetComponent<WorkspaceViewpoint>()?.ObservePlacement();
        internal bool CanPrepareWorldViewpoint(string expected,RoomViewpoint target,bool manual,out string error)
        {
            error="World placement is unavailable";
            var placement=GetComponent<WorkspaceViewpoint>();
            if(!placement||target==null||!placement.CanPlace(expected,target,manual,out error))return false;
            if(room.AnyHeld){error="Release held items before placing the world";return false;}
            error=null;return true;
        }
        internal bool CanSetWorldViewpoint(string expected,RoomViewpoint target,bool manual,out string error)=>
            WorldPlacementReadiness(expected,target,manual,out error)==RoomSpatialReadiness.Ready;
        internal bool CanBeginWorldViewpoint(string expected,RoomViewpoint target,bool manual,out string error)
        {
            var state=WorldPlacementReadiness(expected,target,manual,out error);
            if(state==RoomSpatialReadiness.Unavailable)return false;
            error=null;return true;
        }
        internal RoomSpatialReadiness WorldPlacementReadiness(string expected,RoomViewpoint target,bool manual,out string error)
        {
            if(!CanPrepareWorldViewpoint(expected,target,manual,out error))return RoomSpatialReadiness.Unavailable;
            var targets=WorldPlacementTargets(target);
            if(!CanAcquireNativeEntities(targets,out bool loading,out error))return RoomSpatialReadiness.Unavailable;
            if(loading){error="Prepare the destination ground and obstacles before placing the world";return RoomSpatialReadiness.Preparing;}
            UnityEngine.Bounds? ground=GetComponent<WorkspaceViewpoint>().NeedsAuthoredGround?
                RoomGroundQuery.SupportEnvelope(target.position,.2f,.025f,.025f):null;
            var state=SpatialMovementReady(targets,ground,out error);
            if(state!=RoomSpatialReadiness.Ready)return state;
            // Preparation only samples and validates; observation never moves bodies.
            return GetComponent<WorkspaceViewpoint>().PreparePlacement(target,out error)?RoomSpatialReadiness.Ready:RoomSpatialReadiness.Unavailable;
        }
        internal bool BeginWorldViewpoint(string expected,RoomViewpoint target,bool manual,
            out PendingWorldPlacement pending,out string error,System.Action<string> checkpoint=null)
        {
            pending=null;
            if(PendingEditSave){error="Wait for the pending room edit before placing the world";return false;}
            if(!FinishLiquidPour(out error)||!CanBeginWorldViewpoint(expected,target,manual,out error))return false;
            var write=WriteGate.TryWrite(out error);if(write==null)return false;
            RoomNativeDemand demand=null;var captured=target.Copy();
            try{
                demand=DemandNativeEntities(WorldPlacementTargets(captured),()=>WorldPlacementTargets(captured),
                    ()=>CanPrepareWorldViewpoint(expected,captured,manual,out _),followPhysicsPlacements:true);
                pendingEdit=pending=new PendingWorldPlacement(this,expected,captured,manual,write,demand,checkpoint);
                write=null;demand=null;return true;
            }finally{demand?.Dispose();write?.Dispose();}
        }
    }
}
