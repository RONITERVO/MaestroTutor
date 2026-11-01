// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
namespace Maestro.Quest.Creation {
    internal static class RoomGroundBounds {
        // Prepared metadata can bound a missing surface before native collision
        // exists. Retained/native geometry must also account for its colliders.
        internal static RoomSpatialExtent ForReadiness(RoomSpatialBounds spatial,bool authored){
            var extent=spatial.Visual;
            if(!authored){
                if(!spatial.Collision.Known)extent.Known=false;
                else if(spatial.Collision.HasBounds)extent.Include(spatial.Collision.Bounds,Matrix4x4.identity);
            }
            // An empty surface is not evidence of complete ground. Keep it in
            // the unknown partition, exactly as the readiness leaf treats it.
            return extent.Known&&extent.HasBounds?extent:default;
        }
    }
}
