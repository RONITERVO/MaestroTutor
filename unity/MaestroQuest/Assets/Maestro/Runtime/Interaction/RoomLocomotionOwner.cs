// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Interaction {
    /// <summary>One root-control lifetime, independent of its current movement solver.
    /// A phase may release queries/pose resources without releasing gravity control,
    /// the shared capacity slot, the accepted pose or the interruption history.</summary>
    internal sealed class RoomLocomotionOwner:IDisposable {
        static readonly Dictionary<RoomItem,RoomLocomotionOwner> active=new();
        readonly RoomEditor editor;readonly RoomItem item;readonly string target,session;
        readonly bool fullPose;
        RigidRoomItem rigid;MaestroAvatar avatar;object phase;Action stopPhase;
        bool owned,disposed,disposing;int revision;string failure;
        float started,rememberAt;
        Vector3 acceptedPosition,acceptedScale;Quaternion acceptedRotation;
        internal RoomLocomotionOwner(RoomEditor editor,RoomItem item,bool fullPose=false){
            this.editor=editor;this.item=item;this.fullPose=fullPose;
            target=editor.Identity(item);session=editor.TemporarySessionId;
        }
        internal bool Owns(object value)=>owned&&!disposed&&ReferenceEquals(phase,value);
        internal static bool CanBegin(RoomEditor editor,RoomItem item,RoomLocomotionOwner owner,bool needsFullPose,out string error){
            error="Movement needs an active target and room";
            if(!editor||!item||!item.isActiveAndEnabled||!editor.isActiveAndEnabled||editor.RuntimeGate.Held)return false;
            if(owner!=null){
                error="The movement lifetime belongs to another actor, phase or workspace";
                if(owner.editor!=editor||owner.item!=item||owner.session!=editor.TemporarySessionId||owner.disposed||owner.disposing||owner.phase!=null||
                    needsFullPose&&!owner.fullPose)return false;
                if(owner.failure!=null){error=owner.failure;return false;}
                if(owner.owned&&!owner.Check(out error))return false;
            }
            if((owner==null||!owner.owned)&&!RoomLocomotionBudget.Available(out error))return false;
            error="This actor already has a movement owner";
            if(active.TryGetValue(item,out var current)&&!ReferenceEquals(current,owner))return false;
            error="Start physics before movement";
            if(!editor.PhysicsWorld||!editor.PhysicsWorld.SimulationActive)return false;
            error="Movement needs a tracked viewer and an upright metre-scale world";
            if(!editor.Viewer||!editor.Viewer.gameObject.activeInHierarchy||!editor.ViewerTrackingReady||!editor.Frame.Valid||
                Mathf.Abs(editor.Frame.MetresPerUnit-1)>.0001f||Vector3.Dot(editor.transform.up,Vector3.up)<.99999f)return false;
            error="Release this object and stop authoring before movement";
            if(item.Grab.isSelected||item.PoseLocked||editor.GetComponent<AnimationWorkshop>()?.ControlsTarget(editor.Identity(item))==true)return false;
            error="A physical anchor, terrain owner or connected assembly cannot move as one actor";
            if(item.GetComponent<IPhysicalRoomBinding>()?.PhysicalFrame==true||item.GetComponentsInChildren<RoomWalkableSurface>(true).Length>0||editor.PhysicsIslandMembers(item).Length>1)return false;
            error="Stop the object's root animation or attachment before movement";
            if(item.GetComponent<RigidRoomItem>()?.AnimationOwned==true&&!(owner?.owned??false)||item.GetComponent<HeldRoomProp>()?.Holding==true)return false;
            error=null;return true;
        }
        internal bool Attach(object value,out string error,Action stop=null){
            error="A movement phase is required";if(value==null)return false;
            if(!CanBegin(editor,item,this,fullPose,out error))return false;
            if(!owned){
                if(!RoomLocomotionBudget.Acquire(this,out error))return false;
                owned=true;active.Add(item,this);started=Time.realtimeSinceStartup;rememberAt=Time.unscaledTime+1;
                revision=editor.ObjectRevision(target);AcceptPose();
                rigid=item.GetComponent<RigidRoomItem>();avatar=item.GetComponent<MaestroAvatar>();
                if(rigid){rigid.StopVelocity();rigid.SetAnimationOwner(this,true);}
            }
            // A preceding ground phase may have enabled the base walk/idle
            // animator. Reassert pose control before the next gait captures it.
            if(avatar)avatar.SetEditing(true,preserveUpperBody:!fullPose);
            phase=value;stopPhase=stop;error=null;return true;
        }
        bool Check(out string error){
            error=failure;if(error!=null)return false;
            error="The movement actor, tracking or workspace became unavailable";
            if(!owned||disposed||!editor||!editor.isActiveAndEnabled||editor.TemporarySessionId!=session||editor.RuntimeGate.Held||
                !editor.ViewerTrackingReady||!editor.Viewer||!editor.Viewer.gameObject.activeInHierarchy||
                !item||!item.isActiveAndEnabled||editor.Find(target)!=item||item.Grab.isSelected||item.PoseLocked||
                editor.GetComponent<AnimationWorkshop>()?.ControlsTarget(target)==true)return false;
            error="The movement actor changed; start a new journey from its current position";
            if(editor.ObjectRevision(target)!=revision||Vector3.Distance(item.transform.localPosition,acceptedPosition)>.001f||
                Quaternion.Angle(item.transform.localRotation,acceptedRotation)>.1f||Vector3.Distance(item.transform.localScale,acceptedScale)>.0001f||
                avatar&&avatar.ModelBusy)return false;
            error="The movement physics or world frame became unavailable";
            if(!editor.PhysicsWorld||!editor.PhysicsWorld.SimulationActive||!editor.Frame.Valid||
                Mathf.Abs(editor.Frame.MetresPerUnit-1)>.0001f||Vector3.Dot(editor.transform.up,Vector3.up)<.99999f)return false;
            error="Movement exceeded its five-minute lifetime";
            if(Time.realtimeSinceStartup-started>300)return false;
            error=null;return true;
        }
        internal bool Validate(object value,out string error){
            error="This movement phase no longer owns the actor";
            if(!Owns(value))return false;
            return Validate(out error);
        }
        internal bool Validate(out string error){
            if(Check(out error))return true;
            Invalidate(error);return false;
        }
        internal void Invalidate(string reason)=>failure??=reason??"Movement became unavailable";
        internal void AcceptPose(){
            acceptedPosition=item.transform.localPosition;acceptedRotation=item.transform.localRotation;acceptedScale=item.transform.localScale;
        }
        internal void Checkpoint(){
            if(Time.unscaledTime<rememberAt)return;
            rememberAt=Time.unscaledTime+1;Remember();
        }
        internal bool TryRemember(object value,out string error){
            // A solver's Ready state can have been cached before another action
            // edited the actor in this frame. Completion needs current authority.
            if(!Validate(value,out error))return false;
            Remember();return true;
        }
        void Remember(){
            if(owned&&editor&&item&&editor.Find(target)==item&&editor.TemporarySessionId==session){
                editor.RememberPlacement(target);revision=editor.ObjectRevision(target);
            }
        }
        internal void Detach(object value){
            if(!Owns(value))return;
            // Remembering cannot erase an edit/interruption just before handoff.
            if(!Check(out var error))Invalidate(error);
            Remember();phase=null;stopPhase=null;
        }
        public void Dispose(){
            if(disposed||disposing)return;disposing=true;
            try{
                if(owned&&!Check(out var error))Invalidate(error);
                // End the old gait/query before a new owner can attach. Otherwise
                // a late phase Stop could restore a stale pose over its successor.
                stopPhase?.Invoke();
            }finally{
                Remember();disposed=true;phase=null;stopPhase=null;disposing=false;
                if(owned){
                    owned=false;active.Remove(item);RoomLocomotionBudget.Release(this);
                    if(avatar)avatar.SetEditing(false,preserveUpperBody:!fullPose);
                    if(rigid){rigid.StopVelocity();rigid.SetAnimationOwner(this,false);}
                }
            }
        }
    }
}
