// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    /// <summary>Fixed-step contact capture, with a late-frame arm overlay and the shared prop carrier.</summary>
    [DefaultExecutionOrder(240)]
    public sealed class RoomCatch:MonoBehaviour
    {
        RoomEditor editor;RoomItem item,holder;Transform socket;RoomPropAnchor anchor;RigidRoomItem rigid,holderRigid;Rigidbody body;
        RoomCollisionVolume volume;Vector3 offset,previousBall,previousSocket,holdCentre;uint motionRevision,holderRevision;float sampleAt,deadline,holdSeconds,gripRadius,maxSpeed;bool sampled,stopped;
        HeldRoomProp prop;AvatarCatchReach reach;RoomOwnership.Lease lease;string ownerId;
        readonly CatchClearance clearance=new();
        public string TargetId {get;private set;} public string HolderId=>anchor?.HolderId??"";public string Part=>anchor?.Part??"";
        public string Phase {get;private set;}="waiting";public string Reason {get;private set;}="Waiting for the object to reach the selected socket";
        public string Error {get;private set;} public bool Caught {get;private set;} public bool Dropped=>prop&&prop.Released;
        public bool Finished=>Phase is "missed" or "dropped";
        public bool WaitsThroughGrab(string target)=>!stopped&&Phase=="waiting"&&target==TargetId;
        public static bool Check(RoomEditor editor,string target,RoomPropAnchor anchor,out string error){
            error="Choose a loaded solid or bouncy creation";var item=editor?editor.Find(target):null;
            if(!item||!item.isActiveAndEnabled||editor.Read(target)?.IsBuiltIn!=false)return false;
            var rigid=item.GetComponent<RigidRoomItem>();if(!rigid||!rigid.isActiveAndEnabled||!rigid.Dynamic||!rigid.GeometryReady)return false;
            if(!anchor.Resolve(editor,out var holder,out _,out error))return false;
            if(item==holder){error="An object cannot catch itself";return false;}
            if(!editor.PhysicsWorld||!editor.PhysicsWorld.CanSimulate(item.transform.position)||!editor.PhysicsWorld.CanSimulate(holder.transform.position)){error="Catching needs running, aligned room physics";return false;}
            if(!editor.Viewer||!editor.Viewer.gameObject.activeInHierarchy){error="Return to the active room view before catching";return false;}
            return RoomCollisionVolume.Read(item,.3f,out _,out error);
        }
        public static RoomCatch Begin(RoomEditor editor,string runId,string target,RoomPropAnchor anchor,Vector3 offset,float timeout,float holdSeconds,float gripRadius,float maxSpeed,out string error){
            if(!Check(editor,target,anchor,out error))return null;
            if(editor.Ownership.Covers(runId,new[]{new BehaviourCatalog.Claim(target,"wholeTarget")})){error="Catching must leave the incoming object free; use a version 3 program or a one-off action";return null;}
            anchor.Resolve(editor,out var holder,out var socket,out _);var value=holder.gameObject.AddComponent<RoomCatch>();
            value.editor=editor;value.item=editor.Find(target);value.TargetId=target;value.holder=holder;value.socket=socket;value.anchor=anchor;value.offset=offset;value.deadline=Time.unscaledTime+timeout;value.holdSeconds=holdSeconds;value.gripRadius=gripRadius;value.maxSpeed=maxSpeed;value.ownerId="catch:"+runId;
            value.rigid=value.item.GetComponent<RigidRoomItem>();value.body=value.item.GetComponent<Rigidbody>();value.holderRigid=holder.GetComponent<RigidRoomItem>();
            RoomCollisionVolume.Read(value.item,.3f,out value.volume,out _);
            if(anchor.Kind=="avatarHand"){value.reach=AvatarCatchReach.Begin(holder.GetComponent<MaestroAvatar>().PoseRig,anchor.Part=="left");if(!value.reach){error="This avatar needs a usable shoulder, elbow and hand chain";value.End();return null;}}
            return value;
        }
        Vector3 Centre=>socket.position+socket.rotation*(offset*holder.transform.lossyScale.y);
        bool Valid(){
            if(stopped)return false;
            if(!editor||editor.RuntimeGate.Held||!item||!item.isActiveAndEnabled||editor.Find(TargetId)!=item||!rigid||!rigid.isActiveAndEnabled||!rigid.GeometryReady||!body){Fail("The catch object changed or room actions were paused");return false;}
            if(!anchor.Matches(editor,holder,socket,out var error)){Fail(error);return false;}
            if(!editor.PhysicsWorld||!editor.PhysicsWorld.CanSimulate(item.transform.position)||!editor.PhysicsWorld.CanSimulate(holder.transform.position)||!editor.Viewer||!editor.Viewer.gameObject.activeInHierarchy){Fail("Catching stopped; restore room physics and the active room view");return false;}
            if(reach&&!reach.Valid){Fail("The avatar pose or model changed during the catch");return false;}
            return true;
        }
        void Fail(string error){Error=Caught?"The object was caught; "+error+". Its current placement was kept":error;Reason=Error;Phase="failed";End();}
        void Update(){if(!Finished&&Error==null)Valid();}
        void FixedUpdate(){
            if(Phase!="waiting"||!Valid())return;
            if(Time.unscaledTime>=deadline){Phase="missed";return;}
            float dt=Time.fixedDeltaTime,now=Time.fixedUnscaledTime;
            if(dt<.005f||dt>.05f){Fail("The physics time step is outside catch limits");return;}
            if(!sampled||motionRevision!=rigid.MotionRevision){
                if(!RoomCollisionVolume.Read(item,.3f,out volume,out var geometryError)){Fail(geometryError);return;}
            }
            var position=volume.Centre(item,body);var centre=Centre;uint revision=holderRigid?holderRigid.MotionRevision:0;
            if(!rigid.TryReadMotion(out bool available,out float speed,out _)||!available){sampled=false;Reason="Waiting for the user or other action to release the object";return;}
            if(speed>maxSpeed){sampled=false;Reason="The object exceeds the selected catch speed";return;}
            if(!sampled||motionRevision!=rigid.MotionRevision||holderRevision!=revision||now-sampleAt>.1f||now<=sampleAt){Remember(position,centre,now,revision);return;}
            float elapsed=now-sampleAt;
            // Reject discontinuities instead of treating a teleport as an incoming throw.
            if(Vector3.Distance(position,previousBall)>maxSpeed*elapsed+.03f||Vector3.Distance(centre,previousSocket)>4*elapsed+.03f){Remember(position,centre,now,revision);Reason="Motion changed too quickly to verify contact";return;}
            bool crossing=CatchGeometry.Crosses(previousBall,position,previousSocket,centre,volume.Radius+gripRadius);
            var before=previousBall;Remember(position,centre,now,revision);
            if(!crossing){Reason="Waiting for physical contact with the selected socket";return;}
            if(!clearance.Segment(item,holder,editor.Viewer,before,position,volume.Radius,editor.PhysicsWorld)||!clearance.Segment(item,holder,editor.Viewer,position,centre,volume.Radius,editor.PhysicsWorld)){Reason="The catch path is blocked by a surface, object or the user's head";return;}
            Capture(centre,speed);
        }
        void Remember(Vector3 position,Vector3 centre,float now,uint revision){sampled=true;previousBall=position;previousSocket=centre;sampleAt=now;motionRevision=rigid.MotionRevision;holderRevision=revision;}
        void Capture(Vector3 centre,float speed){
            if(!rigid.CanReceivePhysicsAction(out var error)){sampled=false;Reason=error;return;}
            if(!editor.Ownership.TryAcquire(ownerId,"Physical catch",RoomActorRole.Reflex,new[]{new BehaviourCatalog.Claim(TargetId,"wholeTarget")},notice=>Fail(notice.Message),out var acquired,out error,true)){Reason=error;return;}
            // Taking another actor's lease can synchronously cancel a parent program.
            if(stopped||!Valid()){acquired.Dispose();return;}lease=acquired;
            var fittedOrigin=centre-body.rotation*Vector3.Scale(volume.LocalCentre,item.transform.lossyScale);
            var fittedOffset=Quaternion.Inverse(socket.rotation)*(fittedOrigin-socket.position)/holder.transform.lossyScale.y;
            var attachment=new PropAttachment(TargetId,"",PropHand.Right,PropRelease.Drop,fittedOffset,Quaternion.Inverse(socket.rotation)*body.rotation,1);
            var fresh=new RoomPropAnchor(anchor.Kind,anchor.HolderId,anchor.Part,editor.ObjectRevision(anchor.HolderId),anchor.AvatarHash);
            prop=HeldRoomProp.Begin(editor,attachment,fresh,holdSeconds,out error);
            if(!prop){lease.Dispose();lease=null;Reason=error;sampled=false;return;}
            Caught=true;Phase="holding";Reason="Physical contact captured; holding before drop";holdCentre=holder.transform.InverseTransformPoint(centre);
            editor.ReportCatch(TargetId,HolderId,Part,centre,speed);
        }
        void LateUpdate(){
            if(Finished||Error!=null||!Valid())return;
            if(Phase=="holding"){
                string error=null;if(!prop||!prop.Valid(out error)){Fail(error??"The caught prop was removed");return;}
                if(prop.Released){Phase="dropped";Reason="Caught and dropped through room physics";return;}
            }
            if(!reach)return;
            bool available=rigid.TryReadMotion(out var free,out var speed,out _)&&free&&speed<=maxSpeed;
            if(Phase=="waiting"&&!available)return;
            var goal=Phase=="holding"?holder.transform.TransformPoint(holdCentre):volume.Centre(item,body)+body.linearVelocity*.08f;
            goal-=socket.rotation*(offset*holder.transform.lossyScale.y);
            if(!reach.Apply(goal,item,holder,editor.Viewer,clearance,Time.unscaledDeltaTime)&&Phase=="waiting")Reason="The arm cannot reach the incoming object along a clear path";
        }
        public void End(){if(stopped)return;stopped=true;if(prop)prop.End(true);if(reach)reach.End();lease?.Dispose();lease=null;enabled=false;Destroy(this);}
        void OnDisable()=>End();void OnDestroy()=>End();
    }
}
