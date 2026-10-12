// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Interaction {
    // Local preparation covers the full rotating collider envelope, bounded
    // launch speed, gravity and both bounded liquid accelerations. This is input
    // ownership, not permission to remove the whole-world gate for constraints,
    // tracked contacts or other solver impulses.
    internal static class RoomPhysicsSweep {
        internal const float LookAhead=.2f;
        internal static bool Bounds(RoomItem item,Rigidbody body,float seconds,out Bounds motion,out Bounds ground){
            motion=ground=default;
            if(!item||!item.Grab||!body||!float.IsFinite(seconds)||seconds<=0||!float.IsFinite(Physics.gravity.sqrMagnitude))return false;
            foreach(var collider in item.Grab.colliders){
                if(!collider)return false;
                if(!collider.isTrigger&&(!collider.enabled||!collider.gameObject.activeInHierarchy))return false;
            }
            var shape=RoomSpatialBounds.CaptureCollision(item,RoomObjectKind.Block).Transform(item.transform.localToWorldMatrix);
            if(!shape.Known||!shape.HasBounds)return false;
            var centre=body.worldCenterOfMass;
            float radius=(shape.Bounds.center-centre).magnitude+shape.Bounds.extents.magnitude;
            float time=Mathf.Max(LookAhead,seconds),distance=15*time+(Physics.gravity.magnitude+60)*time*time;
            if(!float.IsFinite(radius)||!float.IsFinite(distance)||!float.IsFinite(centre.sqrMagnitude))return false;
            motion=new Bounds(centre,Vector3.one*(2*(radius+distance+.002f)));
            ground=motion;ground.Encapsulate(motion.min-Vector3.up*16);
            return float.IsFinite(motion.size.sqrMagnitude)&&float.IsFinite(ground.size.sqrMagnitude);
        }
    }
    public sealed partial class RigidRoomItem {
        RoomSpatialInputs physicsInputs;
        bool inputsReady=true,preparedMomentum,shapeFailed,refreshAfterPublication;
        Vector3 preparedVelocity,preparedSpin;
        string inputStatus;
        RoomEditor InputEditor=>GetComponentInParent<RoomEditor>();
        bool InputOwner=>isActiveAndEnabled&&item&&item.isActiveAndEnabled&&item.Grab&&!item.Grab.isSelected&&BaseAllowed;
        bool InputsCurrent=>CurrentInputs(out _);
        bool CurrentInputs(out string error){
            error=inputStatus;var editor=InputEditor;if(!editor)return true;
            if(editor.SpatialInputsChanging){error="The authored geometry is being reconciled";return false;}
            if(!inputsReady||physicsInputs==null||physicsInputs.SourceChanged||editor.RuntimeGate.Held||editor.WriteGate.Frozen)return false;
            if(!RoomPhysicsSweep.Bounds(item,body,Time.fixedDeltaTime,out var motion,out var ground)){error="Object collision bounds are unavailable";return false;}
            var actor=editor.Identity(item);var targets=editor.SpatialInputTargets(motion,ground,true,actor);
            return editor.SpatialInputsReady(targets,motion,ground,true,actor,out error)==RoomSpatialReadiness.Ready;
        }
        internal string PhysicsInputStatus {
            get {CurrentInputs(out var error);return error??"Physics inputs changed; wait for the next step or restart physics after fixing unavailable inputs";}
        }
        internal void RefreshAfterPublication(){if(refreshAfterPublication)Refresh();}
        void SetInputsReady(bool value){if(inputsReady!=value)MotionRevision++;inputsReady=value;}
        void PreparePhysicsInputs(){
            var editor=InputEditor;
            if(!editor){SetInputsReady(true);return;}
            if(!InputOwner||editor.RuntimeGate.Held||editor.WriteGate.Frozen){ReleasePhysicsInputs();return;}
            if(shapeFailed)return;
            // A changed accepted source starts a new observation. Do not restore
            // momentum captured against the old geometry or workspace.
            if(physicsInputs?.SourceChanged==true)ReleasePhysicsInputs();
            if(!RoomPhysicsSweep.Bounds(item,body,Time.fixedDeltaTime,out var motion,out var ground)){
                ReleasePhysicsInputs();shapeFailed=true;inputStatus="Object collision bounds are unavailable";return;
            }
            physicsInputs??=new RoomSpatialInputs(editor,editor.Identity(item),()=>InputOwner,false,purpose:"Physics");
            bool ready=physicsInputs.Require(motion,ground,true,out inputStatus);
            if(!ready&&physicsInputs.Pending){
                if(!preparedMomentum&&!body.isKinematic){preparedVelocity=body.linearVelocity;preparedSpin=body.angularVelocity;preparedMomentum=true;}
            }else if(!ready)DiscardPreparedMotion();
            SetInputsReady(ready);
        }
        void DiscardPreparedMotion(){preparedMomentum=false;preparedVelocity=preparedSpin=Vector3.zero;}
        void RestorePreparedMotion(){
            if(!preparedMomentum)return;
            body.linearVelocity=Vector3.ClampMagnitude(preparedVelocity,15);body.angularVelocity=Vector3.ClampMagnitude(preparedSpin,30);
            DiscardPreparedMotion();body.WakeUp();
        }
        void ReleasePhysicsInputs(){physicsInputs?.Dispose();physicsInputs=null;shapeFailed=false;DiscardPreparedMotion();inputStatus=null;SetInputsReady(false);}
        internal JObject ObservePhysicsInputs(){
            bool active=InputOwner,ready=active&&InputsCurrent;
            string state=!active?"inactive":ready?"ready":physicsInputs?.Pending==true?"pending":"unavailable";
            var targets=physicsInputs?.Targets??Array.Empty<string>();
            return new JObject{["state"]=state,["ready"]=ready,["reason"]=ImportObservation.Text(ready?null:active?PhysicsInputStatus:"Physics, geometry or object ownership is unavailable"),
                ["dependencyCount"]=targets.Length,["dependencies"]=new JArray(targets.Take(4)),["momentumHeld"]=active&&preparedMomentum};
        }
    }
}
