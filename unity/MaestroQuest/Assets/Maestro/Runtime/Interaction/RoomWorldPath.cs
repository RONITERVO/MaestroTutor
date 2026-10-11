// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    /// <summary>A bounded upright rigid path. Yaw uses its fixed horizontal pivot,
    /// so snap turns follow the arc about the viewer instead of a chord through it.</summary>
    internal readonly struct RoomWorldPath
    {
        readonly Vector3 translation;
        readonly double pivotX,pivotZ;
        readonly float angle;
        readonly bool turning;
        internal readonly bool Valid,Unchanged;
        internal readonly int Steps;
        internal RoomWorldPath(Vector3 from,Quaternion facing,Vector3 to,Quaternion target) {
            translation=to-from;pivotX=pivotZ=0;angle=Mathf.DeltaAngle(facing.eulerAngles.y,target.eulerAngles.y);
            turning=Mathf.Abs(angle)>.0001f;Steps=turning?Mathf.CeilToInt(Mathf.Abs(angle)/2):1;
            Valid=Creation.RoomRecipe.Finite(from)&&Creation.RoomRecipe.Finite(to)&&Creation.MotionFrame.ValidRotation(facing)&&Creation.MotionFrame.ValidRotation(target)&&
                Quaternion.Angle(target*Quaternion.Inverse(facing),Quaternion.AngleAxis(angle,Vector3.up))<.01f;
            Unchanged=translation.sqrMagnitude<1e-12f&&Mathf.Abs(angle)<.0001f;
            if(turning) {
                double radians=angle*Math.PI/180,c=Math.Cos(radians),s=Math.Sin(radians),a=1-c,d=a*a+s*s;
                double x=to.x-c*from.x-s*from.z,z=to.z+s*from.x-c*from.z;
                pivotX=(a*x+s*z)/d;pivotZ=(-s*x+a*z)/d;
                Valid=Valid&&double.IsFinite(pivotX)&&double.IsFinite(pivotZ);
            }
        }
        internal Vector3 Point(Vector3 value,float time) {
            if(!turning)return value+translation*time;
            // A tiny yaw plus translation puts the mathematical pivot far away.
            // Keep cancellation in double precision, never in Unity float vectors.
            double radians=angle*Math.PI/180*time,c=Math.Cos(radians),s=Math.Sin(radians),x=value.x-pivotX,z=value.z-pivotZ;
            return new Vector3((float)(pivotX+c*x+s*z),value.y+translation.y*time,(float)(pivotZ-s*x+c*z));
        }
        Vector3 Extents(Vector3 value,float time) {
            float radians=angle*time*Mathf.Deg2Rad,c=Mathf.Abs(Mathf.Cos(radians)),s=Mathf.Abs(Mathf.Sin(radians));
            return new Vector3(c*value.x+s*value.z,value.y,s*value.x+c*value.z);
        }
        internal void Envelope(Bounds bounds,int step,out Vector3 from,out Vector3 to,out Vector3 extents) {
            float a=(float)step/Steps,b=(float)(step+1)/Steps;
            from=Point(bounds.center,a);to=Point(bounds.center,b);
            if(!turning){extents=bounds.extents;return;}
            extents=Vector3.Max(Extents(bounds.extents,a),Extents(bounds.extents,b));
            // Every corner's arc lies within its endpoint chord plus this sagitta.
            // Yaw never changes height; do not inflate Y and invent floor contacts.
            double x=bounds.center.x-pivotX,z=bounds.center.z-pivotZ;
            double radius=Math.Sqrt(x*x+z*z)+new Vector2(bounds.extents.x,bounds.extents.z).magnitude;
            float pad=(float)(radius*(1-Math.Cos(Math.Abs(angle)*Math.PI/180/(2*Steps))))+.000001f;
            extents+=new Vector3(pad,0,pad);
        }
    }
}
