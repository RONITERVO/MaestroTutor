// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Avatar
{
    /// <summary>Two-link positional IK with the authored knee plane as its hint.
    /// Only rotations change; an unreachable target never stretches the skeleton.</summary>
    internal static class TwoBoneFootSolver
    {
        internal static bool Uniform(Transform bone)
        {
            if(!bone)return false;
            var scale=bone.lossyScale;
            return RoomRecipe.Finite(scale)&&scale.x>0&&scale.y>0&&scale.z>0&&
                Mathf.Abs(scale.x-scale.y)<=Mathf.Max(scale.x,scale.y)*.001f&&
                Mathf.Abs(scale.x-scale.z)<=Mathf.Max(scale.x,scale.z)*.001f;
        }
        internal static bool Solve(Transform upper,Transform lower,Transform foot,Vector3 target,
            Quaternion rotation,Vector3 fallbackKnee,out string error)
        {
            error="Foot placement needs a finite, uniformly scaled two-link leg";
            if(!upper||!lower||!foot||!lower.IsChildOf(upper)||!foot.IsChildOf(lower)||
                !Uniform(upper)||!Uniform(lower)||!Uniform(foot)||!RoomRecipe.Finite(target)||
                !RoomRecipe.Finite(fallbackKnee)||!float.IsFinite(Quaternion.Dot(rotation,rotation))||
                Quaternion.Dot(rotation,rotation)<.0001f)return false;
            var origin=upper.position;var knee=lower.position;var end=foot.position;
            float first=Vector3.Distance(origin,knee),second=Vector3.Distance(knee,end);
            var delta=target-origin;float distance=delta.magnitude;
            if(!float.IsFinite(first)||!float.IsFinite(second)||first<.005f||second<.005f||!float.IsFinite(distance))return false;
            error="The foot target is outside the leg's unchanged reach";
            if(distance<.00001f||distance>first+second+.00001f||distance<Mathf.Abs(first-second)-.00001f)return false;
            if((target-end).sqrMagnitude<1e-12f)
            {
                foot.rotation=rotation.normalized;error=null;return true;
            }
            var axis=delta/distance;
            var bend=Vector3.ProjectOnPlane(knee-origin,axis);
            if(bend.sqrMagnitude<1e-8f)bend=Vector3.ProjectOnPlane(fallbackKnee,axis);
            if(bend.sqrMagnitude<1e-8f){error="The straight leg has no stable knee direction";return false;}
            float along=(first*first+distance*distance-second*second)/(2*distance);
            float across=Mathf.Sqrt(Mathf.Max(0,first*first-along*along));
            var desiredKnee=origin+axis*along+bend.normalized*across;
            var beforeUpper=upper.localRotation;var beforeLower=lower.localRotation;var beforeFoot=foot.localRotation;
            upper.rotation=Quaternion.FromToRotation(knee-origin,desiredKnee-origin)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(foot.position-lower.position,target-lower.position)*lower.rotation;
            foot.rotation=rotation.normalized;
            if(!RoomRecipe.Finite(foot.position)||Vector3.Distance(foot.position,target)>.0005f)
            {
                upper.localRotation=beforeUpper;lower.localRotation=beforeLower;foot.localRotation=beforeFoot;
                error="This leg hierarchy cannot reach the foot target without changing its proportions";return false;
            }
            error=null;return true;
        }
    }
}
