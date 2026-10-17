// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    public sealed partial class ScannedRoom
    {
        internal bool PrepareObjectOnScan(RoomEditor editor,string target,string expected,string anchorId,float x,float y,
            out Vector3 position,out Vector3 point,out Vector3 normal,out string error)
        {
            position=point=normal=default;error="The scanned placement service is unavailable";
            if(!editor||!editor.CanEditObject(target,false,out error))return false;
            if(!ReadLayout(editor.transform)){error=layoutReason;return false;}
            if(expected!=layoutId){error="The scanned room changed; inspect room.scan again";return false;}
            var surface=layout.FirstOrDefault(s=>s.Id==anchorId);
            if(surface==null||!surface.Plane.HasValue){error="The exact scanned plane is unavailable; inspect room.scan.surfaces";return false;}
            normal=editor.transform.TransformDirection(surface.Rotation*Vector3.forward).normalized;
            if(normal.y<.7f){error="Placement requires an upward scanned floor or tabletop";return false;}
            var binding=new ScanDrawingAnchor{roomId=layoutRoom,anchorId=anchorId,x=x,y=y,angle=0};
            if(!binding.Validate()){error="Invalid scanned placement coordinates";return false;}
            var item=editor.Find(target);Physics.SyncTransforms();
            if(!Bounds(item,out var bounds)){error="This object has no usable collision bounds";return false;}
            var right=editor.transform.TransformDirection(surface.Rotation*Vector3.right);
            var up=editor.transform.TransformDirection(surface.Rotation*Vector3.up);
            // Project the complete world AABB onto the plane: conservative for rotated
            // or compound colliders, but never admit an overhanging corner or notch.
            float Support(Vector3 axis)=>Vector3.Dot(new Vector3(Mathf.Abs(axis.x),Mathf.Abs(axis.y),Mathf.Abs(axis.z)),bounds.extents);
            if(!binding.Fits(surface,2*Support(right),2*Support(up))){error="The object's collision bounds do not fit this scanned plane boundary";return false;}
            point=editor.transform.TransformPoint(surface.Position+surface.Rotation*new Vector3(x,y,0));
            position=editor.transform.InverseTransformPoint(point+normal*(Support(normal)+.01f)-(bounds.center-item.transform.position));
            if(!Finite(position)||Mathf.Abs(position.x)>25||Mathf.Abs(position.y)>25||Mathf.Abs(position.z)>25){error="Placement is outside the room's authoring range";return false;}
            error=null;return true;
        }
        internal bool PlaceObjectOnScan(RoomEditor editor,string target,string expected,string anchorId,float x,float y,out JObject result,out string error)
        {
            result=null;if(!PrepareObjectOnScan(editor,target,expected,anchorId,x,y,out var position,out var point,out var normal,out error))return false;
            if(!editor.MoveObject(target,position,out error))return false;
            result=new JObject{["target"]=target,["revision"]=editor.ObjectRevision(target),["roomId"]=layoutRoom,["anchorId"]=anchorId,
                ["position"]=Triple(position),["point"]=Triple(editor.transform.InverseTransformPoint(point)),
                ["normal"]=Triple(editor.transform.InverseTransformDirection(normal).normalized),["temporary"]=editor.TemporaryRoom};return true;
        }
    }
}
