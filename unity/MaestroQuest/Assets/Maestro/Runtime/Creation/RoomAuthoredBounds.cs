// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Imports;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // Derived from accepted values only. No Unity objects, mesh allocation, file
    // access or physics readiness. Rotation tracks use swept spheres, not sampled
    // keyframes: interpolation and stopped part poses remain inside the envelope.
    internal static class RoomAuthoredBounds
    {
        internal readonly struct PartFrame
        {
            internal readonly Matrix4x4 Matrix;
            internal readonly bool Moving;
            internal readonly Vector3 Pivot;
            internal readonly float Reach;
            internal PartFrame(Matrix4x4 matrix,bool moving=false,Vector3 pivot=default,float reach=0){Matrix=matrix;Moving=moving;Pivot=pivot;Reach=reach;}
            internal PartFrame Child(Vector3 position,Quaternion rotation,bool animated=false)
            {
                var matrix=Matrix*Matrix4x4.TRS(position,rotation.normalized,Vector3.one);
                return Moving?new(matrix,true,Pivot,Reach+position.magnitude):animated?new(matrix,true,matrix.MultiplyPoint3x4(Vector3.zero)):new(matrix);
            }
            internal void Include(ref RoomSpatialExtent extent,Bounds local)
            {
                if(!Moving){extent.Include(local,Matrix);return;}
                float radius=Reach+local.center.magnitude+local.extents.magnitude;
                extent.Include(new Bounds(Pivot,Vector3.one*(radius*2)),Matrix4x4.identity);
            }
        }
        internal static RoomSpatialBounds Derive(RoomObjectData data,ModelAssetBounds? asset=null)
        {
            if(data==null||data.IsBuiltIn||ScanDrawingAnchor.Has(data))return RoomSpatialBounds.Unknown;
            var root=new PartFrame(Matrix4x4.identity);var parts=new Dictionary<string,PartFrame>(StringComparer.Ordinal);
            var visual=RoomSpatialExtent.Empty;
            switch(data.kind){
                case RoomObjectKind.Block:case RoomObjectKind.Ball:case RoomObjectKind.Cylinder:
                    root.Include(ref visual,new Bounds(Vector3.zero,Vector3.one*.13f));break;
                case RoomObjectKind.Drawing:
                    Stroke(ref visual,root,data.points,data.radius);break;
                case RoomObjectKind.Assembly:
                    if(data.recipe==null||!data.recipe.Validate(out _))return RoomSpatialBounds.Unknown;
                    var animated=new HashSet<string>();foreach(var track in data.recipe.tracks)animated.Add(track.part);
                    foreach(var part in data.recipe.parts){
                        var parent=string.IsNullOrEmpty(part.parent)?root:parts[part.parent];
                        var frame=parent.Child(part.position,part.rotation,animated.Contains(part.id));parts.Add(part.id,frame);
                        // Every admitted primitive/lathe/extrusion/sweep is inside
                        // this normalized box, as is RecipeVisual's selection box.
                        frame.Include(ref visual,new Bounds(Vector3.zero,part.size));
                    }
                    break;
                case RoomObjectKind.ImportedModel:
                    if(!asset.HasValue||!asset.Value.Known||!asset.Value.HasBounds||
                       !ModelGeometryLayout.TryCreate(asset.Value.SourceBounds,Quaternion.identity,data.modelGeometry,out var layout,out _))return RoomSpatialBounds.Unknown;
                    root.Include(ref visual,layout.Bounds);break;
                default:return RoomSpatialBounds.Unknown;
            }
            if(!visual.Known||!visual.HasBounds)return RoomSpatialBounds.Unknown;
            // The selectable outline is generated from the primary geometry only.
            // Include it even when hidden; selecting an object must not change
            // whether a retired object is an observation candidate.
            var selection=visual.Bounds;selection.Expand(2*(CreatedRoomObject.SelectionOffset+CreatedRoomObject.SelectionRadius));visual.Include(selection,Matrix4x4.identity);
            foreach(var surface in data.surfaces??Array.Empty<DrawingSurface>()){
                if(!Anchor(surface.part,root,parts,out var anchor))return RoomSpatialBounds.Unknown;
                var frame=anchor.Child(surface.position,surface.rotation);
                foreach(var stroke in surface.strokes)Stroke(ref visual,frame,DrawingSurfaceGeometry.Path(surface,stroke.points,stroke.radius),stroke.radius);
            }
            foreach(var field in data.heightFields??Array.Empty<RoomHeightField>())
                root.Child(field.frame.position,field.frame.rotation).Include(ref visual,field.GeometryBounds);
            foreach(var liquid in data.containers??Array.Empty<RoomContainer>()){
                // The gravity-clipped fill mesh stays inside the full container
                // volume for every orientation and fill level. Ripples shade it.
                var size=new Vector3(liquid.IsRectangular?liquid.rectangle.width:liquid.radius*2,liquid.height,liquid.IsRectangular?liquid.rectangle.depth:liquid.radius*2);
                root.Child(liquid.frame.position,liquid.frame.rotation).Include(ref visual,new Bounds(Vector3.up*liquid.height*.5f,size));
            }
            if(data.sculptTips?.Length>0&&data.sculptTips[0].IsMaterial&&data.materialStores?.Length>0){
                var tip=data.sculptTips[0];if(!Anchor(tip.part,root,parts,out var anchor))return RoomSpatialBounds.Unknown;
                float radius=Mathf.Clamp(tip.radius,.01f,.15f),height=Mathf.Min(.08f,radius);
                anchor.Child(tip.position,tip.rotation).Include(ref visual,new Bounds(Vector3.forward*height*.5f,new Vector3(radius*2,radius*2,height)));
            }
            if(visual.Known&&visual.HasBounds){
                float magnitude=visual.Bounds.center.magnitude+visual.Bounds.extents.magnitude;
                var padded=visual.Bounds;padded.Expand(2*Mathf.Max(.00001f,magnitude*.00001f));visual.Include(padded,Matrix4x4.identity);
            }
            // All source layers remain candidates. Native collision envelopes are
            // intentionally absent: metadata cannot admit colliders or navigation.
            return new(){Visual=visual,VisualLayers=-1};
        }
        static bool Anchor(string part,PartFrame root,Dictionary<string,PartFrame> parts,out PartFrame result)
        {result=root;return string.IsNullOrEmpty(part)||parts.TryGetValue(part,out result);}
        static void Stroke(ref RoomSpatialExtent extent,PartFrame frame,Vector3[] points,float radius)
        {
            if(points==null||points.Length<2)return;
            bool distinct=false;foreach(var point in points)if((point-points[0]).sqrMagnitude>1e-12f){distinct=true;break;}
            if(!distinct){frame.Include(ref extent,new Bounds(Vector3.zero,Vector3.zero));return;}
            var bounds=new Bounds(points[0],Vector3.zero);foreach(var point in points)bounds.Encapsulate(point);
            // PencilStrokeMesh's pressure never exceeds the requested radius.
            bounds.Expand(radius*2);frame.Include(ref extent,bounds);
        }
    }
}
