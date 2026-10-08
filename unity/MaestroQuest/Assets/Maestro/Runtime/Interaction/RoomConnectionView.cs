// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    /// <summary>Saved physical connection data drives PhysX; suspension never replays a throw or enables room simulation.</summary>
    [DefaultExecutionOrder(90)]
    public sealed class RoomConnectionView:MonoBehaviour
    {
        RoomEditor editor;RoomItem item;RigidRoomItem rigid;RoomConnection definition;string encoded;
        uint ownPlacement,otherPlacement;
        Joint joint,retiring;bool admitted,broken;Vector3 ownScale,otherScale;RoomItem linked;
        public string Phase {get;private set;}="absent";
        public string Error {get;private set;}="";
        public bool Active=>joint;
        public bool Broken=>broken;
        public bool HasTravel=>isActiveAndEnabled&&definition?.kind=="slider"&&editor&&editor.Find(definition.connected) is RoomItem other&&other.isActiveAndEnabled;
        public float Travel=>HasTravel?definition.Travel(transform,editor.Find(definition.connected).transform):0;
        internal void ResetBreak(){broken=false;Refresh();}
        void OnJointBreak(float force){if(!admitted||definition==null)return;admitted=false;joint=null;retiring=null;broken=true;rigid?.SetConstraintBlocked(this,false);Phase="broken";Error="Connection broke; align or rearm explicitly to reconnect";editor?.NotifyConnectionBroken(item,definition);}
        public float Angle=>definition!=null&&linked?definition.Angle(transform,linked.transform):0;
        public void Apply(RoomEditor source,RoomConnection[] values)
        {
            editor=source;item=GetComponent<RoomItem>();rigid=GetComponent<RigidRoomItem>();
            var next=values?.Length==1?values[0]:null;string wire=next==null?"":JsonUtility.ToJson(next);
            if(wire!=encoded){Retire();broken=false;definition=next?.Copy();encoded=wire;}Refresh();
        }
        void Retire(){admitted=false;if(!joint)return;if(joint is HingeJoint hinge){hinge.useMotor=false;hinge.useSpring=false;}if(joint is ConfigurableJoint slider){slider.xDrive=new JointDrive();slider.targetVelocity=Vector3.zero;}retiring=joint;Destroy(joint);joint=null;}
        void Suspend(string phase,string error="",bool block=true){Retire();Phase=phase;Error=error;rigid?.SetConstraintBlocked(this,block);}
        public void Refresh()
        {
            if(!rigid||!item)return;
            if(definition==null){Suspend("absent",block:false);return;}
            if(!definition.enabled){Suspend("disabled",block:false);return;}
            if(broken){Suspend("broken","Connection broke; align or rearm explicitly to reconnect",block:false);return;}
            if(!isActiveAndEnabled||!editor||!editor.isActiveAndEnabled){Suspend("paused");return;}
            var other=editor.Find(definition.connected);
            if(!other||!other.isActiveAndEnabled){linked=null;Suspend("missing","The connected object is unavailable");return;}
            if(linked!=other){Retire();linked=other;}
            var otherRigid=other.GetComponent<RigidRoomItem>();var otherBody=other.GetComponent<Rigidbody>();
            if(!otherRigid||!otherBody||!rigid.GeometryReady||!otherRigid.GeometryReady){Suspend("loading","Wait for both collision shapes");return;}
            if(rigid.AnimationOwned||otherRigid.AnimationOwned){Suspend("owned","An animation or carried prop owns a connection member");return;}
            if(!rigid.Dynamic){Suspend("fixed","Choose solid or bouncy physics for the moving member");return;}
            var world=editor.PhysicsWorld;
            if(!world||!world.CanSimulate(transform.position,item)||!world.CanSimulate(other.transform.position,other)){Suspend("paused");return;}
            if(ownScale!=transform.lossyScale||otherScale!=other.transform.lossyScale||ownPlacement!=rigid.PlacementRevision||otherPlacement!=otherRigid.PlacementRevision)Retire();
            if(joint){Phase="active";Error="";rigid.SetConstraintBlocked(this,false);return;}
            if(retiring){Suspend("pausing");return;} // Destroy completes at the frame boundary, before another joint is admitted.
            if(other.Grab.isSelected||item.Grab.isSelected){Suspend("held","Release both objects before admitting a changed connection");return;}
            if(!definition.Aligned(transform,other.transform,out var error)){Suspend("misaligned",error);return;}
            if(definition.kind=="hinge"){
                // Unity's native zero is the current pose on creation; retain the authored reference.
                float offset=definition.Angle(transform,other.transform);var hinge=gameObject.AddComponent<HingeJoint>();joint=hinge;
                hinge.axis=definition.ownerFrame.rotation*Vector3.right;hinge.extendedLimits=true;
                hinge.limits=new JointLimits{min=definition.limits.minimum-offset,max=definition.limits.maximum-offset,bounciness=0,contactDistance=1};hinge.useLimits=definition.limits.enabled;
                hinge.spring=new JointSpring{targetPosition=definition.drive.target-offset,spring=definition.drive.spring,damper=definition.drive.damper};hinge.useSpring=definition.drive.mode=="spring";
                hinge.motor=new JointMotor{targetVelocity=definition.drive.speed,force=definition.drive.force,freeSpin=false};hinge.useMotor=definition.drive.mode=="motor";
            }else joint=definition.kind=="slider"?gameObject.AddComponent<ConfigurableJoint>():gameObject.AddComponent<FixedJoint>();
            joint.autoConfigureConnectedAnchor=false;joint.anchor=definition.ownerFrame.position;joint.connectedBody=otherBody;joint.connectedAnchor=definition.connectedFrame.position;
            if(joint is ConfigurableJoint slider){
                var settings=definition.slide;float scale=other.transform.TransformVector(definition.connectedFrame.rotation*Vector3.right).magnitude;
                float centre=(settings.minimum+settings.maximum)*.5f;
                slider.axis=definition.ownerFrame.rotation*Vector3.right;slider.secondaryAxis=definition.ownerFrame.rotation*Vector3.up;
                slider.xMotion=ConfigurableJointMotion.Limited;slider.yMotion=slider.zMotion=ConfigurableJointMotion.Locked;
                slider.angularXMotion=slider.angularYMotion=slider.angularZMotion=ConfigurableJointMotion.Locked;
                slider.configuredInWorldSpace=false;slider.swapBodies=false;
                slider.connectedAnchor=definition.connectedFrame.position+definition.connectedFrame.rotation*Vector3.right*centre;
                float half=(settings.maximum-settings.minimum)*.5f*scale;
                slider.linearLimit=new SoftJointLimit{limit=half,bounciness=0,contactDistance=Mathf.Min(.025f,half*.5f)};
                slider.linearLimitSpring=new SoftJointLimitSpring();
                // PhysX's drive displacement/velocity is the connected frame relative to the owner.
                slider.targetPosition=new Vector3((centre-settings.target)*scale,0,0);
                slider.targetVelocity=new Vector3(settings.mode=="motor"?-settings.speed*scale:0,0,0);
                slider.xDrive=new JointDrive{positionSpring=settings.mode=="spring"?settings.spring:0,positionDamper=settings.mode=="passive"?0:settings.damper,maximumForce=settings.mode=="passive"?0:settings.force};
            }
            joint.enableCollision=false;joint.enablePreprocessing=false;joint.breakForce=definition.breakForce==0?Mathf.Infinity:definition.breakForce;joint.breakTorque=definition.breakTorque==0?Mathf.Infinity:definition.breakTorque;admitted=true;
            ownPlacement=rigid.PlacementRevision;otherPlacement=otherRigid.PlacementRevision;ownScale=transform.lossyScale;otherScale=other.transform.lossyScale;Phase="active";Error="";rigid.SetConstraintBlocked(this,false);
        }
        void FixedUpdate()=>Refresh();
        void LateUpdate()=>Refresh();
        void OnDisable()=>Suspend("paused");
        void OnDestroy(){Retire();if(rigid)rigid.SetConstraintBlocked(this,false);}
    }
}
