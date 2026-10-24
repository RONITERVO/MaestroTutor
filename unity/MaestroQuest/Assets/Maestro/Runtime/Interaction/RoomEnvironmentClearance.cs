// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    // Geometry proof only. Callers retain edit ownership, revision and publication
    // guards. The same proof can inspect active items and unpublished native roots.
    internal sealed class RoomEnvironmentClearance
    {
        readonly Collider[] overlaps=new Collider[64];
        internal bool Check(RoomPhysicsWorld world,RoomItem item,bool real,bool requireGround,out string error)
        {
            error="Object collision geometry is unavailable";
            if(!item||!item.Grab)return false;
            if(!world){error=requireGround?"The physics environment is unavailable":null;return error==null;}
            bool physical=world.RealCollisions&&real;
            error="The selected ground is unavailable or this object is outside its accepted bounds";
            if(requireGround&&!world.ContainsEnvironment(item.transform.position,physical))return false;
            if(!physical||!world.SurfacesReady){error=null;return true;}
            Physics.SyncTransforms();
            foreach(var own in item.Grab.colliders){
                if(!own){error="Object collision geometry is unavailable";return false;}
                if(!own.transform.IsChildOf(item.transform)){error="A collision shape is not owned by this object";return false;}
                if(!own.enabled||own.isTrigger||!ChildEnabled(own.transform,item.transform))continue;
                var extent=Envelope(own);
                error="Object collision geometry cannot prove clearance from scanned surfaces";
                if(!extent.Known||!extent.HasBounds)return false;
                var bounds=extent.Bounds;
                int count=Physics.OverlapBoxNonAlloc(bounds.center,bounds.extents,overlaps,Quaternion.identity,1<<RoomPhysicsLayers.Scanned,QueryTriggerInteraction.Ignore);
                try{
                    error="Move the object clear of scanned surfaces before enabling real-room collisions";
                    if(count==overlaps.Length)return false;
                    for(int i=0;i<count;i++){
                        var other=overlaps[i];if(!other||other.transform.IsChildOf(item.transform))continue;
                        // Pinned Unity returns false from ComputePenetration for
                        // inactive shapes, even when they intersect. A conservative
                        // empty envelope can prove clearance; a candidate hit cannot.
                        if(!own.gameObject.activeInHierarchy){
                            error="Collision clearance is not ready; keep this object still while its geometry is prepared";
                            return false;
                        }
                        if(own is MeshCollider mesh&&!mesh.convex)return false;
                        if(Physics.ComputePenetration(own,own.transform.position,own.transform.rotation,other,other.transform.position,other.transform.rotation,out _,out float depth)&&depth>.003f)return false;
                    }
                }finally{Array.Clear(overlaps,0,count);}
            }
            error=null;return true;
        }
        static bool ChildEnabled(Transform shape,Transform item){
            for(var value=shape;value&&value!=item;value=value.parent)if(!value.gameObject.activeSelf)return false;
            return true;
        }
        internal static RoomSpatialExtent Envelope(Collider shape){
            Vector3 center;float radius;
            if(shape is BoxCollider box){center=box.center;radius=box.size.magnitude*.5f;}
            else if(shape is SphereCollider sphere){center=sphere.center;radius=sphere.radius;}
            else if(shape is CapsuleCollider capsule){center=capsule.center;radius=Mathf.Max(capsule.radius,capsule.height*.5f);}
            else if(shape is MeshCollider mesh&&mesh.sharedMesh){center=mesh.sharedMesh.bounds.center;radius=mesh.sharedMesh.bounds.extents.magnitude;}
            else return default;
            if(!RoomRecipe.Finite(center)||!float.IsFinite(radius)||radius<0)return default;
            var result=RoomSpatialExtent.Empty;
            if(shape.enabled&&shape.gameObject.activeInHierarchy){
                result.Include(shape.bounds,Matrix4x4.identity);return result;
            }
            // Inactive colliders report empty bounds. Enclose their declared shape
            // around its origin; Frobenius scale bounds nonuniform/sheared parents
            // and Unity's largest-axis round shapes without publishing a candidate.
            var mapping=shape.transform.localToWorldMatrix;
            float scale=Mathf.Sqrt(((Vector3)mapping.GetColumn(0)).sqrMagnitude+((Vector3)mapping.GetColumn(1)).sqrMagnitude+((Vector3)mapping.GetColumn(2)).sqrMagnitude);
            result.Include(new Bounds(shape.transform.position,Vector3.one*(2*(center.magnitude+radius)*scale)),Matrix4x4.identity);
            return result;
        }
    }
}
