// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        internal JObject ObserveWorldViewpoint()=>GetComponent<WorkspaceViewpoint>()?.ObservePlacement();
        internal bool CanSetWorldViewpoint(string expected,RoomViewpoint target,bool manual,out string error)
        {
            error="World placement is unavailable";
            var placement=GetComponent<WorkspaceViewpoint>();
            if(!placement||target==null||!placement.CanPlace(expected,target,manual,out error))return false;
            if(room.AnyHeld){error="Release held items before placing the world";return false;}
            // Preparation only samples and validates; observation never moves bodies.
            return placement.PreparePlacement(target,out error);
        }
        internal bool SetWorldViewpoint(string expected,RoomViewpoint target,bool manual,out JObject result,out string error)
        {
            result=null;if(!CanSetWorldViewpoint(expected,target,manual,out error))return false;
            using var write=WriteGate.TryWrite(out error);if(write==null)return false;
            CompleteSave(wait:true);
            // Completing an earlier save can notify observers. Revalidate after that
            // callback, then keep persistence and prepared native transfer contiguous.
            if(!CanSetWorldViewpoint(expected,target,manual,out error))return false;
            var candidate=journal.Snapshot();candidate.viewpoint=target.Copy();
            if(!candidate.Validate(out error))return false;
            if(!TemporaryRoom&&!storage.Save(candidate,out error))return false;
            journal.UpdateViewpoint(target,exact:true);
            if(!TemporaryRoom){dirty=false;lastSaveError=null;}
            var placement=GetComponent<WorkspaceViewpoint>();placement.ApplyPlacement();
            result=new JObject {["stateId"]=placement.PlacementState(),["position"]=WorkspaceViewpoint.Point(target.position),["yaw"]=target.yaw,["temporary"]=TemporaryRoom};
            SetStatus("World location saved — activity and real-room tracking retained");
            return true;
        }
    }
}
