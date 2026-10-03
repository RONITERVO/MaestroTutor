// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    /// <summary>Saved hinge data drives PhysX; suspension never replays a throw or enables room simulation.</summary>
    [DefaultExecutionOrder(90)]
    public sealed class RoomHingeView:MonoBehaviour
    {
        RoomEditor editor;RoomItem item;RigidRoomItem rigid;RoomHinge definition;string encoded;
        uint ownPlacement,otherPlacement;
        HingeJoint joint,retiring;Vector3 ownScale,otherScale;RoomItem linked;
        public string Phase {get;private set;}="absent";
        public string Error {get;private set;}="";
        public bool Active=>joint;
        public float Angle=>definition!=null&&linked?definition.Angle(transform,linked.transform):0;
        public void Apply(RoomEditor source,RoomHinge[] values)
        {
            editor=source;item=GetComponent<RoomItem>();rigid=GetComponent<RigidRoomItem>();
            var next=values?.Length==1?values[0]:null;string wire=next==null?"":JsonUtility.ToJson(next);
            if(wire!=encoded){Retire();definition=next?.Copy();encoded=wire;}Refresh();
        }
        void Retire(){if(!joint)return;joint.useMotor=false;joint.useSpring=false;retiring=joint;Destroy(joint);joint=null;}
        void Suspend(string phase,string error="",bool block=true){Retire();Phase=phase;Error=error;rigid?.SetConstraintBlocked(this,block);}
        public void Refresh()
        {
            if(!rigid||!item)return;
            if(definition==null){Suspend("absent",block:false);return;}
            if(!definition.enabled){Suspend("disabled",block:false);return;}
            if(!isActiveAndEnabled||!editor||!editor.isActiveAndEnabled){Suspend("paused");return;}
            var other=editor.Find(definition.connected);
            if(!other||!other.isActiveAndEnabled){linked=null;Suspend("missing","The connected object is unavailable");return;}
            if(linked!=other){Retire();linked=other;}
            var otherRigid=other.GetComponent<RigidRoomItem>();var otherBody=other.GetComponent<Rigidbody>();
            if(!otherRigid||!otherBody||!rigid.GeometryReady||!otherRigid.GeometryReady){Suspend("loading","Wait for both collision shapes");return;}
            if(rigid.AnimationOwned||otherRigid.AnimationOwned){Suspend("owned","An animation or carried prop owns a hinge member");return;}
            if(!rigid.Dynamic){Suspend("fixed","Choose solid or bouncy physics for the moving member");return;}
            var world=editor.PhysicsWorld;
            if(!world||!world.CanSimulate(transform.position)||!world.CanSimulate(other.transform.position)){Suspend("paused");return;}
            if(ownScale!=transform.lossyScale||otherScale!=other.transform.lossyScale||ownPlacement!=rigid.PlacementRevision||otherPlacement!=otherRigid.PlacementRevision)Retire();
            if(joint){Phase="active";Error="";rigid.SetConstraintBlocked(this,false);return;}
            if(retiring){Suspend("pausing");return;} // Destroy completes at the frame boundary, before another joint is admitted.
            if(other.Grab.isSelected||item.Grab.isSelected){Suspend("held","Release both objects before admitting a changed hinge");return;}
            if(!definition.Aligned(transform,other.transform,out var error)){Suspend("misaligned",error);return;}
            // Unity's native zero is the current pose on creation. Offset limits/spring by the saved frame angle.
            float offset=definition.Angle(transform,other.transform);
            joint=gameObject.AddComponent<HingeJoint>();joint.autoConfigureConnectedAnchor=false;
            joint.anchor=definition.ownerFrame.position;joint.axis=definition.ownerFrame.rotation*Vector3.right;
            joint.connectedBody=otherBody;joint.connectedAnchor=definition.connectedFrame.position;
            joint.enableCollision=false;joint.enablePreprocessing=false;joint.extendedLimits=true;
            joint.limits=new JointLimits{min=definition.limits.minimum-offset,max=definition.limits.maximum-offset,bounciness=0,contactDistance=1};joint.useLimits=definition.limits.enabled;
            joint.spring=new JointSpring{targetPosition=definition.drive.target-offset,spring=definition.drive.spring,damper=definition.drive.damper};joint.useSpring=definition.drive.mode=="spring";
            joint.motor=new JointMotor{targetVelocity=definition.drive.speed,force=definition.drive.force,freeSpin=false};joint.useMotor=definition.drive.mode=="motor";
            ownPlacement=rigid.PlacementRevision;otherPlacement=otherRigid.PlacementRevision;ownScale=transform.lossyScale;otherScale=other.transform.lossyScale;Phase="active";Error="";rigid.SetConstraintBlocked(this,false);
        }
        void FixedUpdate()=>Refresh();
        void LateUpdate()=>Refresh();
        void OnDisable()=>Suspend("paused");
        void OnDestroy(){Retire();if(rigid)rigid.SetConstraintBlocked(this,false);}
    }
}
