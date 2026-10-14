// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Interaction {
    public sealed partial class RoomNavigation {
        RoomSpatialInputs stepInputs,routeInputs;
        internal bool MovementInputsPending=>stepInputs?.Pending==true;
        Vector3 inputsGoal;bool inputsGoalKnown;
        RoomEditor InputEditor=>actor?actor.GetComponentInParent<RoomEditor>():null;
        internal void ReleaseMovementInputs(){ReleaseStartingInputs();stepInputs?.Dispose();stepInputs=null;CancelFollowRoute();}
        void ReleaseRouteInputs(){routeInputs?.Dispose();routeInputs=null;inputsGoalKnown=false;}
        // World-space query envelopes are projected by the shared world owner.
        // Direct steps include the motor's raised and per-substep support bands.
        bool StepInputsReady(Vector3 from,Vector3 to,float extra,out string error){
            error=null;var editor=InputEditor;if(!editor)return !actorBound||actor;
            RoomWalkingPreparation.Envelopes(from,to,radius,height+extra,out var body,out var ground);
            stepInputs??=new(editor,editor.Identity(actor),()=>ContextAvailable,false);
            return stepInputs.Require(body,ground,actor.WaterTraversal.mode!="ignore",out error);
        }
        static void RouteEnvelopes(Vector3 from,Vector3 to,float radius,float height,float search,out Bounds body,out Bounds ground){
            body=LiquidPouring.MediumSweep(from,to,radius+.002f,height);body.Expand(Vector3.up*search*2);
            ground=RoomGroundQuery.SupportEnvelope(from,radius,search,search);ground.Encapsulate(RoomGroundQuery.SupportEnvelope(to,radius,search,search));
        }
        bool RouteInputsReady(Vector3 from,Vector3 to,float search,out string error){
            error=null;var editor=InputEditor;if(!editor)return !actorBound||actor;
            RouteEnvelopes(from,to,radius,height,search,out var body,out var ground);
            routeInputs??=new(editor,editor.Identity(actor),()=>ContextAvailable,true);
            return routeInputs.Require(body,ground,actor.WaterTraversal.mode!="ignore",out error);
        }
        bool SampleInputsReady(Vector3 point,float distance){
            RouteEnvelopes(point,point,radius,height,distance,out var body,out var ground);
            // NavMesh.SamplePosition can also snap horizontally; the possible
            // destination footprint must not borrow a different resident floor.
            var horizontal=new Vector3(distance*2,0,distance*2);body.Expand(horizontal);ground.Expand(horizontal);
            return PassiveInputsReady(body,ground,false);
        }
        bool PassiveInputsReady(Bounds body,Bounds ground,bool water){
            var editor=InputEditor;if(!editor)return !actorBound||actor;if(!editor.Frame.Valid)return false;
            var targets=editor.SpatialInputTargets(body,ground,water,editor.Identity(actor));
            return editor.SpatialInputsReady(targets,body,ground,water,editor.Identity(actor),out _)==RoomSpatialReadiness.Ready;
        }
        bool PassiveSegmentInputsReady(Vector3 from,Vector3 to,float search,bool water){
            RouteEnvelopes(from,to,radius,height,search,out var body,out var ground);
            return PassiveInputsReady(body,ground,water);
        }
        void BeginRouteInputs(Vector3 to){
            if(routeInputs?.SourceChanged==true||inputsGoalKnown&&(to-inputsGoal).sqrMagnitude>.04f){routeSearch=null;ReleaseRouteInputs();}
            if(!inputsGoalKnown){inputsGoal=to;inputsGoalKnown=true;}
        }
        bool IncompleteRouteInputs(){
            if(routeInputs?.Status==null)return false;
            // An unfinished native query is not a blocked search edge. Discard
            // search caches, preserve owned preparation, and re-query after load.
            routeSearch=null;FollowRouteStatus=routeInputs.Status;return true;
        }
    }
}
