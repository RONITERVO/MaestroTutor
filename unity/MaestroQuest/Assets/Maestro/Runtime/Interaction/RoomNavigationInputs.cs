// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Interaction {
    // A route frontier grows one owned input set. A literal step replaces its
    // set. Neither queues movement or starts physics; exact queries still decide.
    internal sealed class RoomNavigationInputs:IDisposable {
        readonly RoomEditor editor;readonly string actor;readonly Func<bool> admitted;
        readonly bool accumulate;readonly Func<float> clock;
        RoomNativeDemand demand;string[] targets=Array.Empty<string>();float deadline;
        string failure;bool disposed;
        internal bool Pending {get;private set;}
        internal string Status {get;private set;}
        internal string[] Targets=>targets;
        // Continuous following can plan again from a newer accepted world. A
        // failed/cancelled load is different and still requires a fresh owner.
        internal bool SourceChanged=>demand!=null&&demand.Preparation.IsCompleted&&!demand.Preparation.IsFaulted&&!demand.Preparation.IsCanceled&&demand.Preparation.Result==null&&demand.Error!=null;
        internal RoomNavigationInputs(RoomEditor editor,string actor,Func<bool> admitted,bool accumulate,Func<float> clock=null){
            this.editor=editor;this.actor=actor;this.admitted=admitted;this.accumulate=accumulate;this.clock=clock??(()=>Time.unscaledTime);
        }
        internal bool Require(Bounds body,Bounds ground,bool water,out string error){
            Pending=false;error=Status=failure;if(failure!=null)return false;
            if(disposed||!editor||!editor.isActiveAndEnabled||editor.RuntimeGate.Held||editor.WriteGate.Frozen||!editor.Frame.Valid||!admitted())return Fail("Navigation inputs are no longer owned by the active world",out error);
            var next=editor.NavigationTargets(body,ground,water,actor);
            if(accumulate)next=next.Concat(targets).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            if(demand==null||!next.SequenceEqual(targets)){
                var previous=demand;targets=next;
                demand=editor.DemandNativeEntities(targets,()=>targets,()=>!disposed&&editor&&editor.isActiveAndEnabled&&editor.Frame.Valid&&admitted());
                deadline=clock()+30;previous?.Dispose();
            }
            if(!demand.Preparation.IsCompleted){
                if(clock()>=deadline)return Fail("Navigation input preparation timed out",out error);
                Pending=true;error=Status="Preparing the route's ground, obstacles and water";return false;
            }
            if(demand.Error is string issue)return Fail(issue,out error);
            var state=editor.NavigationReady(targets,body,ground,water,actor,out error);
            if(state==RoomSpatialReadiness.Unavailable)return Fail(error,out error);
            if(state==RoomSpatialReadiness.Preparing){
                if(clock()>=deadline)return Fail("Navigation input preparation timed out",out error);
                Pending=true;Status=error;return false;
            }
            Status=null;return true;
        }
        bool Fail(string reason,out string error){demand?.Dispose();demand=null;failure=error=Status=reason;Pending=false;return false;}
        internal void Retain(RoomRetentionGraph graph){if(!disposed&&failure==null)foreach(var id in targets)graph.Retain(id,RoomRetentionReason.Navigation);}
        public void Dispose(){if(disposed)return;disposed=true;demand?.Dispose();demand=null;targets=Array.Empty<string>();Pending=false;}
    }
    public sealed partial class RoomNavigation {
        RoomNavigationInputs stepInputs,routeInputs;
        Vector3 inputsGoal;bool inputsGoalKnown;
        RoomEditor InputEditor=>actor?actor.GetComponentInParent<RoomEditor>():null;
        internal void ReleaseMovementInputs(){stepInputs?.Dispose();stepInputs=null;CancelFollowRoute();}
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
            var targets=editor.NavigationTargets(body,ground,water,editor.Identity(actor));
            return editor.NavigationReady(targets,body,ground,water,editor.Identity(actor),out _)==RoomSpatialReadiness.Ready;
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
