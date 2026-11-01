// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Avatar
{
    /// <summary>Rest contacts measured before animation. The fitted model's lower
    /// bound estimates the sole plane; users can trim each clearance explicitly.</summary>
    internal sealed class HumanoidFootRig
    {
        internal sealed class Leg
        {
            internal readonly Transform Upper,Lower,Foot;
            internal readonly Vector3 Sole,Up,Knee;
            internal Leg(Transform upper,Transform lower,Transform foot,Transform frame,Vector3 plane)
            {
                Upper=upper;Lower=lower;Foot=foot;
                var point=foot.position-frame.up*Vector3.Dot(foot.position-plane,frame.up);
                Sole=foot.InverseTransformPoint(point);
                Up=foot.InverseTransformDirection(frame.up).normalized;
                var axis=foot.position-upper.position;
                var knee=Vector3.ProjectOnPlane(lower.position-upper.position,axis);
                Knee=frame.InverseTransformDirection(knee.sqrMagnitude>1e-8f?knee.normalized:frame.forward);
            }
            internal Vector3 Contact=>Foot.TransformPoint(Sole);
            internal bool Valid=>Upper&&Lower&&Foot&&Lower.IsChildOf(Upper)&&Foot.IsChildOf(Lower)&&
                TwoBoneFootSolver.Uniform(Upper)&&TwoBoneFootSolver.Uniform(Lower)&&TwoBoneFootSolver.Uniform(Foot)&&
                Vector3.Distance(Upper.position,Lower.position)>.0001f&&Vector3.Distance(Lower.position,Foot.position)>.0001f;
        }
        internal readonly Transform Frame,Hips;
        internal readonly Leg Left,Right;
        internal float Scale=>(Vector3.Distance(Left.Upper.position,Left.Lower.position)+Vector3.Distance(Left.Lower.position,Left.Foot.position)+
            Vector3.Distance(Right.Upper.position,Right.Lower.position)+Vector3.Distance(Right.Lower.position,Right.Foot.position))/1.8f;
        internal bool Valid=>Frame&&Hips&&Hips.IsChildOf(Frame)&&TwoBoneFootSolver.Uniform(Hips)&&Left.Valid&&Right.Valid&&
            Left.Upper.IsChildOf(Hips)&&Right.Upper.IsChildOf(Hips)&&Left.Upper!=Right.Upper&&Left.Foot!=Right.Foot;
        HumanoidFootRig(Transform frame,Transform hips,Leg left,Leg right){Frame=frame;Hips=hips;Left=left;Right=right;}
        internal static HumanoidFootRig Measure(Transform frame,Transform hips,Transform leftUpper,Transform leftLower,
            Transform leftFoot,Transform rightUpper,Transform rightLower,Transform rightFoot,float solePlane)
        {
            if(!frame||!hips||!leftUpper||!leftLower||!leftFoot||!rightUpper||!rightLower||!rightFoot||
                !float.IsFinite(solePlane)||!TwoBoneFootSolver.Uniform(frame))return null;
            var plane=frame.TransformPoint(Vector3.up*solePlane);
            var left=new Leg(leftUpper,leftLower,leftFoot,frame,plane);
            var right=new Leg(rightUpper,rightLower,rightFoot,frame,plane);
            var result=new HumanoidFootRig(frame,hips,left,right);
            // Reject a wildly unrelated bound (for example a platform beneath an
            // imported character). Do not turn it into an enormous invisible shoe.
            if(!result.Valid)return null;
            foreach(var leg in new[]{left,right})
                if(Vector3.Distance(leg.Foot.position,leg.Contact)>Vector3.Distance(leg.Upper.position,leg.Foot.position)*.4f)return null;
            return result;
        }
        internal static HumanoidFootRig Measure(Transform frame,Animator humanoid,float solePlane)
        {
            if(!humanoid||!humanoid.avatar||!humanoid.avatar.isHuman||!humanoid.avatar.isValid)return null;
            Transform Bone(HumanBodyBones id)=>humanoid.GetBoneTransform(id);
            return Measure(frame,Bone(HumanBodyBones.Hips),Bone(HumanBodyBones.LeftUpperLeg),Bone(HumanBodyBones.LeftLowerLeg),
                Bone(HumanBodyBones.LeftFoot),Bone(HumanBodyBones.RightUpperLeg),Bone(HumanBodyBones.RightLowerLeg),Bone(HumanBodyBones.RightFoot),solePlane);
        }
        internal sealed class Pose
        {
            readonly HumanoidFootRig rig;
            Quaternion leftUpper,leftLower,leftFoot,rightUpper,rightLower,rightFoot;
            Vector3 hips;
            internal bool Captured{get;private set;}
            internal Pose(HumanoidFootRig rig)=>this.rig=rig;
            internal void Capture()
            {
                hips=rig.Hips.localPosition;
                leftUpper=rig.Left.Upper.localRotation;leftLower=rig.Left.Lower.localRotation;leftFoot=rig.Left.Foot.localRotation;
                rightUpper=rig.Right.Upper.localRotation;rightLower=rig.Right.Lower.localRotation;rightFoot=rig.Right.Foot.localRotation;
                Captured=true;
            }
            internal void Restore()
            {
                if(!Captured)return;Captured=false;
                if(rig.Hips)rig.Hips.localPosition=hips;
                if(rig.Left.Upper)rig.Left.Upper.localRotation=leftUpper;
                if(rig.Left.Lower)rig.Left.Lower.localRotation=leftLower;
                if(rig.Left.Foot)rig.Left.Foot.localRotation=leftFoot;
                if(rig.Right.Upper)rig.Right.Upper.localRotation=rightUpper;
                if(rig.Right.Lower)rig.Right.Lower.localRotation=rightLower;
                if(rig.Right.Foot)rig.Right.Foot.localRotation=rightFoot;
            }
        }
    }
}
