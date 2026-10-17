// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class CreatedRoomObject
    {
        Vector3[] drawingPoints;
        float drawingRadius;
        internal bool DrawingMatches(RoomObjectData data)=>drawing&&drawing.GeometryReady&&drawingPoints!=null&&drawingRadius==data.radius&&drawingPoints.SequenceEqual(data.points);
        public void ApplyDrawing(RoomObjectData data)=>ApplyDrawing(data,null);
        internal void ApplyDrawing(RoomObjectData data,RoomEditPreparation preparation)
        {
            if(data.kind!=RoomObjectKind.Drawing||ScanDrawingAnchor.Has(data)||DrawingMatches(data))return;
            var prepared=preparation?.TakeDrawing(data);if(preparation!=null&&prepared==null)return;
            using var candidate=prepared??PencilGeometry.Prepare(new[]{data.points},data.radius,drawingOwner,"spatial");
            if(!drawing)drawing=GetComponent<PencilMarks>()??gameObject.AddComponent<PencilMarks>();drawing.SetPrepared(candidate);drawing.SetColor(tint);
            drawingPoints=(Vector3[])data.points.Clone();drawingRadius=data.radius;
            geometryBounds=GetComponent<MeshFilter>().sharedMesh.bounds;
            var box=(BoxCollider)originalCollider;box.center=geometryBounds.center;box.size=geometryBounds.size+Vector3.one*.018f;
            bool selected=selection&&selection.activeSelf;if(selection){selection.SetActive(false);Destroy(selection);}
            BuildSelection(geometryBounds);SetSelected(selected);SetCollisionShape(collisionShape,true);
        }
    }
}
