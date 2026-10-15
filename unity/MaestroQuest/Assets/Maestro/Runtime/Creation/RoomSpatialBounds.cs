// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // Unknown and known-empty are different. A broad-phase read must not reject
    // a candidate because an importer, anchor or geometry type is unavailable.
    internal struct RoomSpatialExtent
    {
        internal bool Known, HasBounds;
        internal Bounds Bounds;
        internal static RoomSpatialExtent Empty => new() { Known=true };
        internal void Include(Bounds value,Matrix4x4 mapping)
        {
            var projected=Project(value,mapping);
            if(!projected.Known){Known=false;return;}
            if(HasBounds)Bounds.Encapsulate(projected.Bounds);else Bounds=projected.Bounds;
            HasBounds=true;
            if(!RoomRecipe.Finite(Bounds.center)||!RoomRecipe.Finite(Bounds.extents)||!RoomRecipe.Finite(Bounds.size))Known=false;
        }
        internal RoomSpatialExtent Transform(Matrix4x4 mapping)
        {
            if(!Known)return default;
            return HasBounds?Project(Bounds,mapping):Empty;
        }
        static RoomSpatialExtent Project(Bounds value,Matrix4x4 mapping)
        {
            if(!RoomRecipe.Finite(value.center)||!RoomRecipe.Finite(value.extents)||value.extents.x<0||value.extents.y<0||value.extents.z<0)return default;
            for(int i=0;i<16;i++)if(!float.IsFinite(mapping[i]))return default;
            var result=Empty;
            for(int i=0;i<8;i++){
                var corner=value.center+Vector3.Scale(value.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                corner=mapping.MultiplyPoint3x4(corner);if(!RoomRecipe.Finite(corner))return default;
                if(result.HasBounds)result.Bounds.Encapsulate(corner);else result.Bounds=new Bounds(corner,Vector3.zero);
                result.HasBounds=true;
            }
            return RoomRecipe.Finite(result.Bounds.center)&&RoomRecipe.Finite(result.Bounds.extents)&&RoomRecipe.Finite(result.Bounds.size)?result:default;
        }
    }
    // Value-only data: no Mesh, Renderer, Collider or GameObject survives native
    // retirement. Layer union and AABBs may overestimate, never claim visibility.
    internal struct RoomSpatialBounds
    {
        internal RoomSpatialExtent Visual,Collision;
        internal int VisualLayers;
        internal static RoomSpatialBounds Unknown=>new(){VisualLayers=-1};
        internal RoomSpatialBounds Transform(Matrix4x4 mapping)=>new(){Visual=Visual.Transform(mapping),Collision=Collision.Transform(mapping),VisualLayers=VisualLayers};
        internal static RoomSpatialBounds Capture(RoomItem item,RoomObjectData data)
        {
            if(!item||data==null)return Unknown;
            var result=CaptureVisual(item,data.kind==RoomObjectKind.ImportedModel,new List<Renderer>());
            result.Collision=CaptureCollision(item,data.kind);return result;
        }
        // Display admission and retention must use the same conservative envelope.
        // Otherwise hidden/rest geometry can repeatedly reload a root that the
        // active-renderer view just retired, starving other dependency groups.
        internal static RoomSpatialBounds CaptureVisual(RoomItem item,bool imported,List<Renderer> renderers)
        {
            if(!item)return Unknown;var created=item.GetComponent<CreatedRoomObject>();
            if(imported&&(!created||!created.ModelGeometryReady))return Unknown;
            var result=new RoomSpatialBounds{Visual=RoomSpatialExtent.Empty,Collision=RoomSpatialExtent.Empty};
            var inverse=item.transform.worldToLocalMatrix;
            // Include disabled children too: visibility and selection can change
            // independently of geometry. Do not read collider.bounds (empty when
            // disabled), invoke Physics.SyncTransforms, or bake meshes on a read.
            item.GetComponentsInChildren(true,renderers);
            foreach(var renderer in renderers){
                result.VisualLayers|=1<<renderer.gameObject.layer;
                if(renderer is MeshRenderer){
                    // TextMesh also uses MeshRenderer, without a MeshFilter.
                    // Renderer.localBounds is the geometry contract in both cases.
                    result.Visual.Include(renderer.localBounds,inverse*renderer.transform.localToWorldMatrix);
                }else if(renderer is SkinnedMeshRenderer&&renderer.TryGetComponent<Imports.ImportedSkinBounds>(out var skin)){
                    var deformed=skin.Read(inverse,includeRest:true);
                    if(!deformed.Known)result.Visual.Known=false;
                    else if(deformed.HasBounds)result.Visual.Include(deformed.Bounds,Matrix4x4.identity);
                }else result.Visual.Known=false;
            }
            // Imported static nodes can return to their initial transforms when
            // reloaded. Cover that rest shape as well as the captured idle pose.
            if(created&&created.Model&&created.ModelGeometryReady)
                result.Visual.Include(created.Model.LocalBounds,Matrix4x4.identity);
            renderers.Clear();return result;
        }
        // Continuous movement needs current colliders, not renderers, skin
        // bounds or a detached copy of every recipe and animation payload.
        internal static RoomSpatialExtent CaptureCollision(RoomItem item,RoomObjectKind kind)
        {
            if(!item||!item.Grab)return default;
            var created=kind==RoomObjectKind.ImportedModel?item.GetComponent<CreatedRoomObject>():null;
            if(kind==RoomObjectKind.ImportedModel&&(!created||!created.ModelGeometryReady))return default;
            var result=RoomSpatialExtent.Empty;var inverse=item.transform.worldToLocalMatrix;
            foreach(var collider in item.Grab.colliders){
                if(!collider){result.Known=false;continue;}
                if(collider.isTrigger)continue;
                var mapping=inverse*collider.transform.localToWorldMatrix;
                if(collider is BoxCollider box)result.Include(new Bounds(box.center,box.size),mapping);
                else if(collider is MeshCollider mesh&&mesh.sharedMesh)result.Include(mesh.sharedMesh.bounds,mapping);
                else if(collider is SphereCollider sphere)IncludeRound(ref result,sphere.center,sphere.radius,mapping);
                else if(collider is CapsuleCollider capsule)IncludeRound(ref result,capsule.center,Mathf.Max(capsule.radius,capsule.height*.5f),mapping);
                else result.Known=false;
            }
            return result;
        }
        static void IncludeRound(ref RoomSpatialExtent extent,Vector3 centre,float radius,Matrix4x4 mapping)
        {
            // Unity spheres/capsules use the largest axis scale, not ellipsoid
            // scaling. The Frobenius norm also conservatively handles shear.
            float scale=Mathf.Sqrt(((Vector3)mapping.GetColumn(0)).sqrMagnitude+((Vector3)mapping.GetColumn(1)).sqrMagnitude+((Vector3)mapping.GetColumn(2)).sqrMagnitude);
            extent.Include(new Bounds(mapping.MultiplyPoint3x4(centre),Vector3.one*(2*radius*scale)),Matrix4x4.identity);
        }
    }
}
