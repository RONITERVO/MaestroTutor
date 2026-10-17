// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation {
    // Terrain is canonical. The container's frame, footprint and local measured
    // capacity are derived metadata; its saved amount is never silently clamped.
    internal static class RoomTerrainReservoir {
        internal static bool Derive(RoomObjectData owner,RoomContainer container,out RoomHeightField bed,out double capacity,out string error){
            bed=null;capacity=0;error="A terrain reservoir needs one sculptable surface on the same fixed object";
            if(owner==null||owner.IsBuiltIn||owner.physics!=ItemPhysics.Fixed||owner.heightFields?.Length!=1)return false;
            bed=owner.heightFields[0];if(bed==null||!bed.Validate(out error))return false;
            error="The reservoir opening must be at or above every terrain vertex";
            if(!float.IsFinite(container.height)||container.height<.01f||container.height>2)return false;
            foreach(float value in bed.heights)if(value>container.height)return false;
            capacity=TerrainLiquidGeometry.Capacity(bed,container.height)*1000000;
            error="The terrain must leave between 1 and 32000000 local millilitres below the opening";
            if(!double.IsFinite(capacity)||capacity<1||capacity>RoomContainer.MaximumMillilitres)return false;
            error=null;return true;
        }
        internal static bool Normalize(RoomObjectData owner,out string error){
            error=null;if(owner.containers?.Length!=1||owner.containers[0]==null||!owner.containers[0].TerrainFloor)return true;
            var container=owner.containers[0];if(!Derive(owner,container,out var bed,out var capacity,out error))return false;
            if(container.amountMl>capacity){error="This terrain edit would overflow its water; transfer or explicitly remove enough liquid first";return false;}
            container.frame=bed.frame.Copy();container.rectangle=new(){width=bed.width,depth=bed.depth};container.capacityMl=capacity;return true;
        }
        [Serializable] sealed class CapacityWire { public double capacityMl; }
        // This is redundant geometry metadata. JsonUtility can parse its own
        // double encoding one ULP away from the original. Recognize only that
        // exact wire result, then restore the capacity derived from the bed.
        // Water, heights, frame and footprint are never repaired or inferred.
        internal static void NormalizeSerializedCapacity(RoomObjectData owner){
            var container=owner?.containers?.Length==1?owner.containers[0]:null;
            if(container==null||container.version!=3||!Derive(owner,container,out _,out var capacity,out _)
                ||container.capacityMl==capacity)return;
            var wire=JsonUtility.FromJson<CapacityWire>(JsonUtility.ToJson(new CapacityWire{capacityMl=capacity}));
            if(wire!=null&&container.capacityMl==wire.capacityMl)container.capacityMl=capacity;
        }
        internal static bool Validate(RoomObjectData owner,RoomContainer container,out string error){
            if(!Derive(owner,container,out var bed,out var capacity,out error))return false;
            error="Terrain reservoir geometry and capacity must match its canonical sculptable surface";
            if(!container.frame.position.Equals(bed.frame.position)||!container.frame.rotation.Equals(bed.frame.rotation)||container.rectangle.width!=bed.width||container.rectangle.depth!=bed.depth||container.capacityMl!=capacity)return false;
            error=null;return true;
        }
        internal static TerrainLiquidGeometry Native(RoomContainer container,Transform root){
            if(!container.TerrainFloor||!root)return null;var view=root.GetComponent<HeightFieldView>();var bed=view?view.Accepted:null;
            if(bed==null||!view.Collision||!view.Collision.enabled||!view.Collision.gameObject.activeInHierarchy||!container.frame.position.Equals(bed.frame.position)||!container.frame.rotation.Equals(bed.frame.rotation)||container.rectangle.width!=bed.width||container.rectangle.depth!=bed.depth)return null;
            return view.LiquidGeometry(container.height);
        }
    }
}
