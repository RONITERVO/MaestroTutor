// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Linq;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class CreatedRoomObject
    {
        Vector3[] drawingPoints;
        float drawingRadius;
        public void ApplyDrawing(RoomObjectData data)
        {
            if(!drawing||data.kind!=RoomObjectKind.Drawing)return;
            if(drawingRadius==data.radius&&drawingPoints.SequenceEqual(data.points))return;
            drawing.SetPaths(new[]{data.points},data.radius);drawing.SetColor(tint);
            drawingPoints=(Vector3[])data.points.Clone();drawingRadius=data.radius;
            geometryBounds=GetComponent<MeshFilter>().sharedMesh.bounds;
            var box=(BoxCollider)originalCollider;box.center=geometryBounds.center;box.size=geometryBounds.size+Vector3.one*.018f;
            bool selected=selection&&selection.activeSelf;if(selection){selection.SetActive(false);Destroy(selection);}
            BuildSelection(geometryBounds);SetSelected(selected);SetCollisionShape(collisionShape,true);
        }
    }
}
