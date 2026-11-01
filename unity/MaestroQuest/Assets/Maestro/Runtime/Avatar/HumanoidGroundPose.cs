// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Avatar
{
    /// <summary>Presentation state only. Anchors are local to accepted support;
    /// source animation, root placement, colliders and saved poses remain owned
    /// by their existing systems.</summary>
    internal sealed class HumanoidGroundPose
    {
        internal delegate bool Probe(Vector3 point,float band,out RaycastHit hit);
        sealed class Contact
        {
            internal float Weight;
            Collider support;
            uint revision;
            Vector3 anchor;
            bool planted;
            internal void Clear(){Weight=0;support=null;planted=false;}
            internal bool Target(HumanoidFootRig.Leg leg,RaycastHit ground,float baseline,float clearance,float scale,float dt,bool walking,Probe probe,
                out Vector3 ankle,out Quaternion rotation)
            {
                var sole=leg.Contact;var surface=ground.collider.GetComponent<RoomWalkableSurface>();
                uint current=surface?surface.Revision:0;
                bool same=support==ground.collider&&revision==current;
                bool stance=sole.y-baseline<=(walking ? (planted&&same ? .075f : .035f) : .16f)*scale;
                if(!same){support=ground.collider;revision=current;planted=false;Weight=0;}
                if(stance&&!planted){anchor=support.transform.InverseTransformPoint(ground.point);planted=true;}
                var point=planted?support.transform.TransformPoint(anchor):ground.point;
                // Never drag a leg behind a teleported or badly mismatched gait.
                if(Vector3.ProjectOnPlane(point-sole,Vector3.up).magnitude>.18f*scale){stance=false;planted=false;}
                if(!stance)planted=false;
                // Resample the retained point: edits can move/remove support
                // without destroying its collider. It cannot become a fake plane.
                if(planted){
                    if(!probe(point,.15f*scale+.01f,out var hit)||hit.collider!=support){
                        planted=false;stance=false;
                    }else{point=hit.point;ground.normal=hit.normal;}
                }
                Weight=Mathf.MoveTowards(Weight,stance?1:0,Mathf.Clamp(dt,0,.05f)/.12f);
                if(Weight<=0){ankle=leg.Foot.position;rotation=leg.Foot.rotation;return false;}
                var fitted=Quaternion.FromToRotation(leg.Foot.TransformDirection(leg.Up),ground.normal)*leg.Foot.rotation;
                rotation=Quaternion.Slerp(leg.Foot.rotation,fitted,Weight);
                var target=point+Vector3.up*clearance;
                // Convert calibrated sole offset into an ankle target at the fitted rotation.
                var offset=rotation*Vector3.Scale(leg.Sole,leg.Foot.lossyScale);
                ankle=Vector3.Lerp(leg.Foot.position,target-offset,Weight);
                return true;
            }
        }
        readonly Contact left=new(),right=new();
        internal void Clear(){left.Clear();right.Clear();}
        internal bool Apply(HumanoidFootRig rig,RaycastHit leftGround,RaycastHit rightGround,float baseline,RoomFootPlacement settings,float dt,bool walking,Probe probe,out string status)
        {
            status="swing";
            float scale=rig.Scale;
            bool a=left.Target(rig.Left,leftGround,baseline,settings.leftClearance,scale,dt,walking,probe,out var l,out var lr);
            bool b=right.Target(rig.Right,rightGround,baseline,settings.rightClearance,scale,dt,walking,probe,out var rr,out var rrot);
            if(!a&&!b)return true;
            // A bounded pelvis drop makes a lower support reachable without
            // stretching either leg. Never move the actor root or its physics body.
            float drop=Mathf.Min(a?Drop(rig.Left,l):0,b?Drop(rig.Right,rr):0);
            if(drop<-.16f*scale){Clear();status="Ground exceeds the bounded leg correction";return false;}
            rig.Hips.position+=Vector3.up*drop;
            if(a&&!TwoBoneFootSolver.Solve(rig.Left.Upper,rig.Left.Lower,rig.Left.Foot,l,lr,rig.Frame.TransformDirection(rig.Left.Knee),out status)||
                b&&!TwoBoneFootSolver.Solve(rig.Right.Upper,rig.Right.Lower,rig.Right.Foot,rr,rrot,rig.Frame.TransformDirection(rig.Right.Knee),out status)){Clear();return false;}
            status=a&&b?"both feet supported":a?"left foot supported":"right foot supported";return true;
        }
        static float Drop(HumanoidFootRig.Leg leg,Vector3 target)
        {
            float length=Vector3.Distance(leg.Upper.position,leg.Lower.position)+Vector3.Distance(leg.Lower.position,leg.Foot.position);
            var delta=target-leg.Upper.position;
            float horizontal=delta.x*delta.x+delta.z*delta.z;
            float maxVertical=Mathf.Sqrt(Mathf.Max(0,length*length-horizontal))*.9999f;
            return Mathf.Min(0,delta.y+maxVertical);
        }
    }
}
