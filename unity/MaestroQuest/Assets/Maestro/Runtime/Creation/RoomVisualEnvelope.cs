// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;
namespace Maestro.Quest.Creation {
    // Query-only outer envelope. It is never reported as authored geometry or
    // used as collision proof. Direct extrema preserve enclosing tree branches.
    internal static class RoomVisualEnvelope {
        internal static RoomSpatialExtent Project(Vector3 min,Vector3 max,Matrix4x4 mapping){
            if(!RoomRecipe.Finite(min)||!RoomRecipe.Finite(max)||min.x>max.x||min.y>max.y||min.z>max.z)return default;
            for(int i=0;i<16;i++)if(!float.IsFinite(mapping[i]))return default;
            var center=Vector3.zero;var extents=Vector3.zero;
            for(int axis=0;axis<3;axis++){
                double low=mapping[axis,3],high=low,magnitude=Math.Abs(low);
                for(int component=0;component<3;component++){
                    double a=(double)mapping[axis,component]*min[component],b=(double)mapping[axis,component]*max[component];
                    low+=Math.Min(a,b);high+=Math.Max(a,b);magnitude+=Math.Max(Math.Abs(a),Math.Abs(b));
                }
                // Cover float multiplication, cancellation and repeated Bounds
                // encapsulation in the exact leaf path. Degenerate/overflowing
                // projection stays unknown and can never reject a branch.
                // Mono may retain wider intermediates than IL2CPP. A bound
                // outside float range is uncertain even if double terms cancel.
                if(magnitude*1.000008>float.MaxValue)return default;
                double padding=Math.Max(.00001,magnitude*.000008);
                center[axis]=(float)(low*.5+high*.5);
                extents[axis]=(float)(Math.Max(center[axis]-(low-padding),(high+padding)-center[axis])*1.000001);
            }
            var bounds=new Bounds{center=center,extents=extents};
            return RoomSpatialLookup.Valid(bounds)&&RoomRecipe.Finite(bounds.size)?new RoomSpatialExtent{Known=true,HasBounds=true,Bounds=bounds}:default;
        }
    }
}
