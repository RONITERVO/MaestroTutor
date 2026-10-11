// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    internal sealed partial class WorkspaceViewpoint
    {
        string stateId=Guid.NewGuid().ToString("N");
        (Vector3 position,Quaternion rotation,string temporary,bool tracked,bool pending) signature;
        internal string PlacementState()
        {
            var current=(view.WorldPosition,view.WorldRotation,editor.TemporarySessionId,HeadReady,Pending);
            if(current!=signature){signature=current;stateId=Guid.NewGuid().ToString("N");}
            return stateId;
        }
        internal JObject ObservePlacement()
        {
            string id=PlacementState();RoomViewpoint current=null;bool located=HeadReady&&view.ReadViewpoint(out current);
            bool ready=CanPlace(id,null,false,out var error);
            return new JObject {["stateId"]=id,["ready"]=ready,["reason"]=error??"",["located"]=located,
                ["position"]=Point(located?current.position:Vector3.zero),["yaw"]=located?current.yaw:0};
        }
        internal bool CanPlace(string expected,RoomViewpoint target,bool manual,out string error)
        {
            error="World location changed; read world.viewpoint again";
            if(!manual&&expected!=PlacementState())return false;
            if(!HeadReady||Pending){error="Wait for head tracking, focus and saved world restoration";return false;}
            if(!editor||!editor.isActiveAndEnabled||editor.WriteGate.Frozen||editor.RuntimeGate.Held||!editor.CanSaveRoom){
                error="Finish the workspace operation before placing the world";return false;
            }
            var controls=editor.GetComponent<MovementControls>();
            if(controls&&!controls.CanPlaceWorld(manual,out error))return false;
            if(!view.CanEnter){error="Wait for the room view and scan to be ready";return false;}
            if(target!=null&&(!target.Valid||!target.active)){error="Choose a finite world location within 25 metres and a heading from -180 to 180";return false;}
            error=null;return true;
        }
        internal bool PreparePlacement(RoomViewpoint target,out string error)=>view.PrepareViewpoint(target,out error);
        internal void ApplyPlacement()
        {
            view.ApplyPreparedViewpoint();recording=true;
            editor.GetComponent<MovementControls>()?.WorldPlaced();
            PlacementState();stateId=Guid.NewGuid().ToString("N");
        }
        internal static JObject Point(Vector3 p)=>new(){["x"]=p.x,["y"]=p.y,["z"]=p.z};
    }
    public sealed partial class MovementControls
    {
        internal bool CanPlaceWorld(bool manual,out string error)
        {
            error=null;
            if(!RecoveryHeadReady){error="Wait for head tracking and focus before placing the world";return false;}
            var frame=sample==null?default:sample();
            if((manual?frame.manipulating:frame.busy)||room.AnyHeld||rules&&rules.AnyButtonHeld){
                error="Release held items and controls before placing the world";return false;
            }
            return true;
        }
        // Preserve Maestro's independent binding and actor, and both movement opt-ins.
        internal void WorldPlaced(){userGate.Reset();Array.Clear(buttonReady,0,buttonReady.Length);}
        public void ReturnToWorldOrigin()
        {
            var target=new RoomViewpoint{active=true};
            if(!editor.SetWorldViewpoint(null,target,true,out _,out var error))Say(error);
            else Say("World origin — activity kept; center your stick before moving");
        }
    }
}
