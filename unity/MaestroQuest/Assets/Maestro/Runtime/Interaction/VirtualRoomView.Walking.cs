// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    public sealed partial class VirtualRoomView
    {
        RoomEditor walkingEditor;
        RoomWalkingPreparation walkingPreparation;
        bool walkingBound;
        internal void BindWalking(RoomEditor editor,Func<bool> tracked)
        {
            walkingPreparation?.Dispose();walkingEditor=editor;walkingBound=true;
            walkingPreparation=new RoomWalkingPreparation(editor,()=>this&&CanEnter&&tracked());
        }
        internal void UnbindWalking(RoomEditor editor)
        {
            if(!ReferenceEquals(walkingEditor,editor))return;
            walkingPreparation?.Dispose();walkingPreparation=null;walkingEditor=null;
            // A persistent view with no current workspace must not fall back to
            // the unbound standalone geometry path used by isolated scene tests.
        }
        internal void EndWalking(RoomEditor owner=null)
        {
            if(!ReferenceEquals(owner,null)&&!ReferenceEquals(owner,walkingEditor))return;
            walkingPreparation?.End();
        }
        bool WalkingReady(Vector3 foot,Vector3 delta,float height,out string error)
        {
            error=null;if(!walkingBound)return true;
            if(walkingPreparation==null){error="The walking workspace is unavailable";return false;}
            var frame=new RoomFrame(content);
            return walkingPreparation.Ready(frame.PointToRoom(foot),frame.PointToRoom(foot+delta),.2f,height,out error);
        }
    }
}
