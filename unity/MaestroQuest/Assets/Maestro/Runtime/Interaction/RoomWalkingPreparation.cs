// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    // A held movement input owns a changing local dependency set. It never
    // queues displacement: every effect is calculated from the latest input.
    internal sealed class RoomWalkingPreparation : IDisposable
    {
        readonly RoomEditor editor;
        readonly Func<bool> admitted;
        readonly Func<float> clock;
        RoomNativeDemand demand;
        string[] targets;
        Bounds body,ground;
        float deadline;
        string failure;
        bool disposed;
        internal RoomWalkingPreparation(RoomEditor editor,Func<bool> admitted,Func<float> clock=null)
        {this.editor=editor;this.admitted=admitted;this.clock=clock??(()=>Time.unscaledTime);}
        // The motor can climb/descend at each substep and try a raised sweep.
        // Cover that entire band, including every footprint ray's height margin.
        internal static void Envelopes(Vector3 from,Vector3 to,float radius,float height,out Bounds body,out Bounds ground)
        {
            float distance=Vector3.ProjectOnPlane(to-from,Vector3.up).magnitude;
            float reach=(Mathf.CeilToInt(distance/.04f)+1)*(RoomGroundQuery.MaximumStep+.001f);
            body=new Bounds(from+Vector3.up*(height*.5f),new Vector3((radius+.002f)*2,height+reach*2,(radius+.002f)*2));
            body.Encapsulate(new Bounds(to+Vector3.up*(height*.5f),body.size));
            ground=RoomGroundQuery.SupportEnvelope(from,radius,reach,reach);
            ground.Encapsulate(RoomGroundQuery.SupportEnvelope(to,radius,reach,reach));
        }
        internal bool Ready(Vector3 from,Vector3 to,float radius,float height,out string error)
        {
            error=failure;if(failure!=null)return false;
            if(disposed||!editor||!editor.isActiveAndEnabled||editor.RuntimeGate.Held||editor.WriteGate.Frozen||!admitted()){
                End();error="Walking needs an active tracked workspace";return false;
            }
            Envelopes(from,to,radius,height,out body,out ground);
            var next=editor.SpatialMovementTargets(body,ground);
            if(demand==null||!next.SequenceEqual(targets)){
                // Publish the new interest before releasing the previous one, so
                // overlapping connected requests can share ongoing native work.
                var previous=demand;targets=next;
                demand=editor.DemandNativeEntities(targets,()=>editor.SpatialMovementTargets(body,ground),
                    ()=>!disposed&&admitted()&&editor&&editor.isActiveAndEnabled);
                deadline=clock()+30;previous?.Dispose();
            }
            if(!demand.Preparation.IsCompleted){
                if(clock()>=deadline)return Fail("Movement preparation timed out",out error);
                error="Preparing ground and obstacles — release the stick to cancel";return false;
            }
            error=demand.Error;if(error!=null)return Fail(error,out error);
            var state=editor.SpatialMovementReady(targets,ground,out error);
            if(state==RoomSpatialReadiness.Preparing){
                if(clock()>=deadline)return Fail("Movement preparation timed out",out error);
                return false;
            }
            if(state==RoomSpatialReadiness.Unavailable)return Fail(error,out error);
            return true;
        }
        bool Fail(string reason,out string error)
        {
            demand?.Dispose();demand=null;targets=null;
            failure=error=reason+"; release the movement stick before retrying";return false;
        }
        internal void End(){demand?.Dispose();demand=null;targets=null;failure=null;}
        public void Dispose(){if(disposed)return;disposed=true;End();}
    }
}
