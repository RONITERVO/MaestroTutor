// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
namespace Maestro.Quest.Interaction {
    public sealed partial class RoomNavigation {
        RoomSpatialInputs startingInputs;
        bool startingPrepared;
        internal bool StartingInputsPending=>startingInputs?.Pending==true;
        bool StartingEnvelope(out Bounds body,out Bounds ground,out string error){
            body=ground=default;error="The walking actor or physics is unavailable";
            if(!ContextOwned||!InputEditor||!InputEditor.ViewerTrackingReady||!GroundedMovement.LeaderFoot(actor,out var foot))return false;
            RoomWalkingPreparation.Envelopes(foot,foot,radius,height,out body,out ground);
            error=null;return true;
        }
        // Admission is passive. Actual I/O waits for the accepted movement owner.
        internal bool InitialInputsNeeded(out string error){
            if(!StartingEnvelope(out var body,out var ground,out error))return false;
            var editor=InputEditor;
            var targets=editor.SpatialInputTargets(body,ground,actor.WaterTraversal.mode!="ignore",editor.Identity(actor));
            if(!editor.CanAcquireNativeEntities(targets,out var loading,out error))return false;
            if(!loading)error="Place the actor's feet near accepted ground before walking";
            return loading;
        }
        internal bool PrepareStartingInputs(out string error){
            error=null;if(startingPrepared)return true;
            // Standalone geometry queries have no authored native owner to load.
            if(!InputEditor)return !actorBound||actor;
            if(!StartingEnvelope(out var body,out var ground,out error))return false;
            var editor=InputEditor;
            startingInputs??=new RoomSpatialInputs(editor,editor.Identity(actor),()=>ContextOwned&&editor.ViewerTrackingReady,false);
            if(!startingInputs.Require(body,ground,actor.WaterTraversal.mode!="ignore",out error))return false;
            startingPrepared=true;return true;
        }
        void StartingGeometryAccepted(){
            if(!startingPrepared)return;
            // Accepted/candidate map snapshots now retain the prepared geometry.
            startingInputs?.Dispose();startingInputs=null;
        }
        void ReleaseStartingInputs(){startingInputs?.Dispose();startingInputs=null;startingPrepared=false;}
    }
}
