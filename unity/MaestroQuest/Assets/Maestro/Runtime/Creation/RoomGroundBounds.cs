// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation {
    internal static class RoomGroundBounds {
        // Enclose the complete selected ordinary primary source from accepted
        // metadata. These bounds discover missing support; they never grant it.
        internal static RoomSpatialExtent Ordinary(RoomObjectData data){
            if(data?.staticSupport?.enabled!=true||!RoomStaticSupport.Validate(data,out _))return default;
            var result=RoomSpatialExtent.Empty;
            if(data.collisionShape==ItemCollider.Automatic&&(data.collision?.shapes?.Length??0)>0){
                foreach(var shape in data.collision.shapes)
                    result.Include(new Bounds(Vector3.zero,shape.size),Matrix4x4.TRS(shape.position,shape.rotation.normalized,Vector3.one));
                return Present(result);
            }
            // Explicit Box uses the same geometryBounds as Automatic's original
            // box. Imported proxies were already required to select a recipe.
            switch(data.kind){
                case RoomObjectKind.Block:case RoomObjectKind.Ball:case RoomObjectKind.Cylinder:
                    result.Include(new Bounds(Vector3.zero,Vector3.one*.13f),Matrix4x4.identity);break;
                case RoomObjectKind.Drawing:
                    if(!RoomDocument.ValidateDrawing(data.points,data.radius,out _))return default;
                    var stroke=new Bounds(data.points[0],Vector3.zero);
                    foreach(var point in data.points)stroke.Encapsulate(point);
                    // Stroke pressure never exceeds radius; the original collider
                    // adds .018 metres to each size axis after mesh generation.
                    stroke.Expand(2*data.radius+.018f);result.Include(stroke,Matrix4x4.identity);break;
                case RoomObjectKind.Assembly:
                    if(data.recipe==null||!data.recipe.Validate(out _))return default;
                    var frames=new Dictionary<string,Matrix4x4>(System.StringComparer.Ordinal);
                    foreach(var part in data.recipe.parts){
                        var parent=string.IsNullOrEmpty(part.parent)?Matrix4x4.identity:frames[part.parent];
                        var frame=parent*Matrix4x4.TRS(part.position,part.rotation.normalized,Vector3.one);
                        frames.Add(part.id,frame);result.Include(new Bounds(Vector3.zero,part.size),frame);
                    }
                    break;
                default:return default;
            }
            return Present(result);
        }
        static RoomSpatialExtent Present(RoomSpatialExtent value)=>value.Known&&value.HasBounds&&RoomSpatialLookup.Valid(value.Bounds)?value:default;
        internal static RoomSpatialExtent Union(RoomSpatialExtent first,RoomSpatialExtent second){
            if(!first.Known||!second.Known)return default;
            if(second.HasBounds)first.Include(second.Bounds,Matrix4x4.identity);
            return Present(first);
        }
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
