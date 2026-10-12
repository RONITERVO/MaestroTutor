// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Interaction {
    // A route frontier grows one owned input set. A literal step replaces its
    // set. Neither queues movement or starts physics; exact queries still decide.
    internal delegate RoomSpatialReadiness SpatialInputCheck(string[] targets,out string error);
    internal sealed class RoomSpatialInputs:IDisposable {
        readonly RoomEditor editor;readonly string actor;readonly Func<bool> admitted;
        readonly bool accumulate;readonly Func<float> clock;readonly string purpose;
        RoomNativeDemand demand;string[] targets=Array.Empty<string>();float deadline;
        string failure;bool disposed;
        internal bool Pending {get;private set;}
        internal string Status {get;private set;}
        internal string[] Targets=>targets;
        // Continuous following can plan again from a newer accepted world. A
        // failed/cancelled load is different and still requires a fresh owner.
        internal bool SourceChanged=>demand!=null&&demand.Preparation.IsCompleted&&!demand.Preparation.IsFaulted&&!demand.Preparation.IsCanceled&&demand.Preparation.Result==null&&demand.Error!=null;
        internal RoomSpatialInputs(RoomEditor editor,string actor,Func<bool> admitted,bool accumulate,Func<float> clock=null,string purpose="Navigation"){
            this.purpose=purpose;this.editor=editor;this.actor=actor;this.admitted=admitted;this.accumulate=accumulate;this.clock=clock??(()=>Time.unscaledTime);
        }
        bool Owned=>!disposed&&editor&&editor.isActiveAndEnabled&&!editor.RuntimeGate.Held&&!editor.WriteGate.Frozen&&editor.Frame.Valid&&admitted();
        internal bool Require(Bounds body,Bounds ground,bool water,out string error){
            if(!Owned)return Fail(purpose+" inputs are no longer owned by the active world",out error);
            return Require(editor.SpatialInputTargets(body,ground,water,actor),
                (string[] ids,out string issue)=>editor.SpatialInputsReady(ids,body,ground,water,actor,out issue),out error);
        }
        internal bool Require(string[] next,SpatialInputCheck check,out string error){
            bool wasPending=Pending;Pending=false;error=Status=failure;if(failure!=null)return false;
            if(wasPending&&clock()>=deadline)return Fail(purpose+" input preparation timed out",out error);
            if(!Owned)return Fail(purpose+" inputs are no longer owned by the active world",out error);
            if(accumulate)next=next.Concat(targets).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            if(demand==null||!next.SequenceEqual(targets)){
                var previous=demand;targets=next;
                demand=editor.DemandNativeEntities(targets,()=>targets,()=>!disposed&&editor&&editor.isActiveAndEnabled&&editor.Frame.Valid&&admitted());
                if(!wasPending)deadline=clock()+30;previous?.Dispose();
            }
            if(!demand.Preparation.IsCompleted){
                if(clock()>=deadline)return Fail(purpose+" input preparation timed out",out error);
                Pending=true;error=Status="Preparing "+purpose.ToLowerInvariant()+" ground, obstacles and water";return false;
            }
            if(demand.Error is string issue)return Fail(issue,out error);
            var state=check(targets,out error);
            if(state==RoomSpatialReadiness.Unavailable)return Fail(error,out error);
            if(state==RoomSpatialReadiness.Preparing){
                if(clock()>=deadline)return Fail(purpose+" input preparation timed out",out error);
                Pending=true;Status=error;return false;
            }
            Status=null;return true;
        }
        bool Fail(string reason,out string error){demand?.Dispose();demand=null;failure=error=Status=reason;Pending=false;return false;}
        internal void Retain(RoomRetentionGraph graph){if(!disposed&&failure==null)foreach(var id in targets)graph.Retain(id,RoomRetentionReason.Navigation);}
        public void Dispose(){if(disposed)return;disposed=true;demand?.Dispose();demand=null;targets=Array.Empty<string>();Pending=false;}
    }
}
