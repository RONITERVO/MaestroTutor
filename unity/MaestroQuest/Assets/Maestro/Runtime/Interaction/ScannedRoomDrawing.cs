// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Linq;
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    public sealed partial class ScannedRoom
    {
        internal bool PrepareDrawing(Transform frame,string expected,string anchorId,float x,float y,float angle,float width,float height,out ScanDrawingAnchor binding,out string error)
        {
            binding=null;error="Inspect the current scanned room first";if(!ReadLayout(frame)){error=layoutReason;return false;}if(expected!=layoutId)return false;
            var next=new ScanDrawingAnchor{roomId=layoutRoom,anchorId=anchorId,x=x,y=y,angle=angle};
            if(!ResolveDrawing(frame,next,width,height,expected,out _,out _,out _,out error))return false;binding=next;return true;
        }
        internal bool DrawingAtGaze(Transform frame,Ray worldRay,string expected,float angle,float width,float height,out ScanDrawingAnchor binding,out string error)
        {
            binding=null;error="Inspect the current scanned room first";if(!ReadLayout(frame)||expected!=layoutId)return false;
            if(!Finite(worldRay.origin)||!Finite(worldRay.direction)||worldRay.direction.sqrMagnitude<.001f)return false;
            var origin=frame.InverseTransformPoint(worldRay.origin);var direction=frame.InverseTransformDirection(worldRay.direction.normalized);float nearest=4;ScannedSurface selected=null;Vector3 contact=default;
            foreach(var surface in layout){if(!surface.Plane.HasValue)continue;var inverse=Quaternion.Inverse(surface.Rotation);var o=inverse*(origin-surface.Position);var d=inverse*direction;if(d.z>=-.00001f)continue;float distance=-o.z/d.z;if(distance<0||distance>nearest)continue;var point=o+d*distance;
                var probe=new ScanDrawingAnchor{roomId=layoutRoom,anchorId=surface.Id,x=point.x,y=point.y,angle=angle};if(!probe.Fits(surface,.0001f,.0001f))continue;nearest=distance;contact=point;selected=surface;
            }
            if(selected==null){error="No front-facing scanned plane within four metres; look at a clear part of the wall and explicitly try again";return false;}
            return PrepareDrawing(frame,expected,selected.Id,contact.x,contact.y,angle,width,height,out binding,out error);
        }
        internal bool ResolveDrawing(Transform frame,ScanDrawingAnchor binding,float width,float height,string expected,out ScannedSurface surface,out Vector3 position,out Quaternion rotation,out string error)
        {
            surface=null;position=default;rotation=Quaternion.identity;error="The scanned drawing anchor is invalid";
            if(binding==null||!binding.Validate())return false;
            if(!ReadLayout(frame)){error=layoutReason;return false;}
            if(expected!=null&&expected!=layoutId){error="The scanned room changed; inspect room.scan again";return false;}
            if(binding.roomId!=layoutRoom){error="This ink belongs to a different scanned room; explicitly rebind it";return false;}
            surface=layout.FirstOrDefault(s=>s.Id==binding.anchorId);
            if(surface==null){error="The saved surface is missing; ink is preserved until the exact anchor returns or you rebind it";return false;}
            if(!binding.Fits(surface,width,height)){error="The ink layer does not fit this scanned plane boundary";return false;}
            position=surface.Position+surface.Rotation*new Vector3(binding.x,binding.y,ScanDrawingAnchor.Offset);
            rotation=surface.Rotation*Quaternion.AngleAxis(binding.angle,Vector3.forward)*Quaternion.AngleAxis(180,Vector3.up);
            if(position.sqrMagnitude>625){error="The ink layer is outside the room's 25-metre authoring range";return false;}
            error=null;return true;
        }
    }
}
