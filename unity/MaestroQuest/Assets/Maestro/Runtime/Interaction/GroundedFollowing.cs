// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
namespace Maestro.Quest.Interaction {
    internal sealed class GroundedFollowing:FollowingOperation {
        internal GroundedFollowing(RoomEditor editor,RoomItem item,string leaderId,float distance,float speed,float seconds):
            base(editor,item,leaderId,distance,seconds,new GroundedMovement(editor,item,editor.Frame.PointToRoom(item.transform.position),speed,"follow")) {}
        protected override bool CanStart(out string error)=>CanBegin(editor,item,leaderId,distance,out error);
        internal static bool CanBegin(RoomEditor editor,RoomItem item,string leaderId,float distance,out string error){
            if(!GroundedMovement.CanBegin(editor,item,out error)||!LeaderAvailable(editor,item,leaderId,out error))return false;
            if(leaderId==null&&GroundedMovement.Body(item,out var radius,out _,out _,out error)&&distance<radius+.35f){
                error="The following distance is too small for this body's clearance from the user";return false;
            }
            error=null;return true;
        }
    }
}
