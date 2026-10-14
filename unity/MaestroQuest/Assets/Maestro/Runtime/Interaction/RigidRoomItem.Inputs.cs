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
    // launch speed, gravity and both bounded liquid accelerations. Physical
    // islands share their full dependency closure; tracked contacts own separate
    // swept inputs. Conservative native barriers interrupt unforeseen contacts.
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
        bool collisionInterrupted;
        internal string[] CollisionInputMembers=>inputMembers.ToArray();
        internal void ClearCollisionInterruption()=>collisionInterrupted=false;
        internal void InterruptAtCollisionGuard(){
            // Keep the completed solver pose. Replaying pre-contact velocity would
            // erase real contacts or repeat a throw from the same physics step.
            ReleasePhysicsInputs();StopVelocity();collisionInterrupted=true;MotionRevision++;
        }
        bool inputsReady=true,preparedMomentum,shapeFailed,refreshAfterPublication;
        Vector3 preparedVelocity,preparedSpin;
        string inputStatus;string[] inputMembers=Array.Empty<string>();
        RoomEditor boundInputEditor;bool hasInputBinding;
        RoomEditor InputEditor=>hasInputBinding?boundInputEditor:GetComponentInParent<RoomEditor>();
        internal RoomEditor PhysicsEditor=>InputEditor;
        internal bool HasPhysicsBinding=>hasInputBinding;
        bool InputBindingAvailable=>!hasInputBinding||boundInputEditor&&boundInputEditor.isActiveAndEnabled&&boundInputEditor.Identity(item)!=null;
        internal void BindPhysicsInputs(RoomEditor editor){
            if(hasInputBinding&&ReferenceEquals(boundInputEditor,editor))return;
            grabInputs?.Suspend();InterruptPhysicsInputs();hasInputBinding=true;boundInputEditor=editor;
        }
        bool InputOwner=>isActiveAndEnabled&&item&&item.isActiveAndEnabled&&item.Grab&&!item.Grab.isSelected&&PhysicsEnabled;
        bool InputsCurrent=>InputEditor?InputEditor.PhysicsIslandReady(item,true,out _):CurrentInputs(out _);
        internal bool CurrentInputs(out string error)=>CurrentInputs(null,out error);
        internal bool CurrentInputs(PhysicsIslandQuery query,out string error){
            error=inputStatus;var editor=InputEditor;if(!editor)return true;
            if(editor.DormantRecoveryBlocked(item,out _,out error))return false;
            if(editor.SpatialInputsChanging){error="The authored geometry is being reconciled";return false;}
            if(!inputsReady||physicsInputs==null||physicsInputs.SourceChanged||editor.RuntimeGate.Held||editor.WriteGate.Frozen)return false;
            query??=editor.PhysicsInputs(item);
            if(!query.Members.SequenceEqual(inputMembers)){error="The physical connection group changed";return false;}
            if(query.Targets.Any(id=>Array.BinarySearch(physicsInputs.Targets,id,StringComparer.Ordinal)<0)){error="The physics input set changed";return false;}
            return editor.PhysicsInputsReady(query,out error)==RoomSpatialReadiness.Ready;
        }
        internal string PhysicsInputStatus {
            get {if(CurrentInputs(out var error)&&InputEditor)InputEditor.PhysicsIslandReady(item,true,out error);return error??"Physics inputs changed; wait for the next step or restart physics after fixing unavailable inputs";}
        }
        internal void RefreshAfterPublication(){if(refreshAfterPublication)Refresh();}
        void SetInputsReady(bool value){if(inputsReady!=value)MotionRevision++;inputsReady=value;}
        internal void PreparePhysicsInputs(PhysicsIslandQuery query=null){
            var editor=InputEditor;
            if(!editor){if(hasInputBinding)ReleasePhysicsInputs();else SetInputsReady(true);return;}
            if(!InputOwner||editor.RuntimeGate.Held||editor.WriteGate.Frozen||!editor.PhysicsIslandOwned(item)){ReleasePhysicsInputs();return;}
            if(editor.DormantRecoveryBlocked(item,out _,out var recoveryError)){ReleasePhysicsInputs();inputStatus=recoveryError;return;}
            query??=editor.PhysicsInputs(item);
            if(!query.Members.SequenceEqual(inputMembers)){ReleasePhysicsInputs();inputMembers=query.Members;}
            if(shapeFailed)return;
            // A changed accepted source starts a new observation. Do not restore
            // momentum captured against the old geometry or workspace.
            if(physicsInputs?.SourceChanged==true){ReleasePhysicsInputs();inputMembers=query.Members;}
            if(query.State==RoomSpatialReadiness.Unavailable){
                ReleasePhysicsInputs();inputMembers=query.Members;shapeFailed=true;inputStatus=query.Error;return;
            }
            physicsInputs??=new RoomSpatialInputs(editor,editor.Identity(item),()=>InputOwner&&editor.PhysicsIslandOwned(item),false,purpose:"Physics");
            bool ready=physicsInputs.Require(query.Targets,(string[] _,out string error)=>editor.PhysicsInputsReady(query,out error),out inputStatus);
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
        void InterruptPhysicsInputs(){InputEditor?.CancelDormantRecovery(item);InputEditor?.InterruptPhysicsIsland(item);ReleasePhysicsInputs();collisionInterrupted=false;}
        internal void ReleasePhysicsInputs(){physicsInputs?.Dispose();physicsInputs=null;inputMembers=Array.Empty<string>();shapeFailed=false;DiscardPreparedMotion();inputStatus=null;SetInputsReady(false);}
        internal JObject ObservePhysicsInputs(){
            bool active=InputOwner,ready=active&&InputsCurrent;
            bool recoveryPending=false,recovering=InputEditor&&InputEditor.DormantRecoveryBlocked(item,out recoveryPending,out _);
            string state=!active?"inactive":ready?"ready":physicsInputs?.Pending==true||recovering&&recoveryPending?"pending":"unavailable";
            var targets=InputEditor?.DormantRecoveryTargets(item)??physicsInputs?.Targets??Array.Empty<string>();
            return new JObject{["state"]=state,["ready"]=ready,["reason"]=ImportObservation.Text(ready?null:active?PhysicsInputStatus:"Physics, geometry or object ownership is unavailable"),
                ["dependencyCount"]=targets.Length,["dependencies"]=new JArray(targets.Take(4)),["momentumHeld"]=active&&preparedMomentum,["interrupted"]=collisionInterrupted};
        }
    }
}
