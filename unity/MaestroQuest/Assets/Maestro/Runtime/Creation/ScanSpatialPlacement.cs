// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // Detached binding values only. Never retain an SDK anchor, collider or
    // last known room pose: the exact physical plane must resolve on each read.
    internal sealed class ScanSpatialPlacement
    {
        readonly ScanDrawingAnchor anchor;
        readonly float width,height;
        ScanSpatialPlacement(RoomObjectData data){anchor=data.scanAnchors[0].Copy();width=data.surfaces[0].width;height=data.surfaces[0].height;}
        internal static ScanSpatialPlacement From(RoomObjectData data)=>ScanDrawingAnchor.Has(data)?new(data):null;
        internal RoomSpatialBounds Project(RoomEditor editor,RoomSpatialBounds local)
        {TryProject(editor,local,out var projected);return projected;}
        internal bool TryProject(RoomEditor editor,RoomSpatialBounds local,out RoomSpatialBounds projected)
        {
            projected=RoomSpatialBounds.Unknown;if(!TryPlacement(editor,out var matrix,out _))return false;
            projected=local.Transform(matrix);return true;
        }
        internal bool TryPlacement(RoomEditor editor,out Matrix4x4 matrix,out Quaternion rotation){
            matrix=Matrix4x4.identity;rotation=Quaternion.identity;
            var scan=editor&&editor.PhysicsWorld?editor.PhysicsWorld.GetComponent<ScannedRoom>():null;
            if(!scan||!scan.ResolveDrawing(editor.transform,anchor,width,height,null,out _,out var position,out rotation,out _))return false;
            matrix=Matrix4x4.TRS(position,rotation,Vector3.one);return true;
        }
    }
}
