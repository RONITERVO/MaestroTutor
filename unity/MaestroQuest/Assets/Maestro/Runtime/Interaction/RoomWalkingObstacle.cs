// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
namespace Maestro.Quest.Interaction {
    internal static class RoomWalkingObstacle {
        internal static bool Blocks(Collider collision,Transform actor,string identity){
            if(!collision||RoomItem.IsPointerOnly(collision)||actor&&collision.transform.IsChildOf(actor))return false;
            // Carried props own separate swept trajectories until release.
            var held=collision.GetComponentInParent<HeldRoomProp>();
            return !held||!held.Holding||identity==null||held.HolderId!=identity;
        }
    }
}
