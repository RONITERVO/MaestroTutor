// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Interaction {
    // One envelope for containment, discovery and solid clearance. The sagitta
    // bound includes the complete shortest quaternion arc, not just end poses.
    internal readonly struct RoomSweptBody {
        internal readonly Vector3 From,To,Extents;
        internal Bounds Bounds {get{var b=new Bounds(From,Extents*2);b.Encapsulate(new Bounds(To,Extents*2));return b;}}
        internal RoomSweptBody(Bounds local,Vector3 scale,Vector3 from,Quaternion a,Vector3 to,Quaternion b){
            var start=RoomSpatialExtent.Empty;start.Include(local,Matrix4x4.TRS(Vector3.zero,a,scale));
            var end=RoomSpatialExtent.Empty;end.Include(local,Matrix4x4.TRS(Vector3.zero,b,scale));
            var box=start.Bounds;box.Encapsulate(end.Bounds);
            float radius=Vector3.Scale(new Vector3(Mathf.Max(Mathf.Abs(local.min.x),Mathf.Abs(local.max.x)),
                Mathf.Max(Mathf.Abs(local.min.y),Mathf.Abs(local.max.y)),Mathf.Max(Mathf.Abs(local.min.z),Mathf.Abs(local.max.z))),scale).magnitude;
            float halfAngle=Quaternion.Angle(a,b)*Mathf.Deg2Rad*.5f;
            box.Expand(2*radius*(1-Mathf.Cos(halfAngle)));
            From=from+box.center;To=to+box.center;Extents=box.extents;
        }
    }
}
