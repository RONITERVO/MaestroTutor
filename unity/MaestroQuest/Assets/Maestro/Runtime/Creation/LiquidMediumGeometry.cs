// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation {
    // One geometric interpretation of the live measured quantity; no extra inventory.
    internal readonly struct LiquidMediumGeometry {
        internal readonly string Id;internal readonly RoomItem Item;internal readonly RoomContainer Contents;
        internal readonly float Level,Volume;internal readonly Vector3 Up;readonly Matrix4x4 inverse;
        internal LiquidMediumGeometry(string id,RoomItem item,RoomContainer contents,Vector3 up){
            Id=id;Item=item;Contents=contents;Up=up;
            var t=item.transform;float scale=Mathf.Abs(t.lossyScale.x);
            var rotation=t.rotation*contents.frame.rotation;
            inverse=(t.localToWorldMatrix*Matrix4x4.TRS(contents.frame.position,contents.frame.rotation,Vector3.one)).inverse;
            var centre=t.TransformPoint(contents.frame.position+contents.frame.rotation*(Vector3.up*contents.height*.5f));
            Level=Vector3.Dot(centre,up)+ContainerFlowGeometry.Level(contents,Quaternion.Inverse(rotation)*up,contents.amountMl/contents.capacityMl)*scale;
            Volume=contents.FootprintArea*contents.height*scale*scale*scale;
        }
        internal bool Sample(Vector3 point,out float depth){
            depth=Level-Vector3.Dot(point,Up);var local=inverse.MultiplyPoint3x4(point);
            return Contents.amountMl>0&&depth>0&&local.y>=0&&local.y<=Contents.height&&Contents.ContainsHorizontal(local);
        }
    }
}
