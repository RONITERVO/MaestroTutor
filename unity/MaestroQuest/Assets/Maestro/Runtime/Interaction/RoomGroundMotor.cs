// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    /// <summary>Bounded swept walking over the shared accepted ground. The caller
    /// publishes the final pose only after every support and clearance query passes.</summary>
    internal sealed class RoomGroundMotor
    {
        enum Clearance { Clear, Ground, Blocked }
        internal Collider Blocker {get;private set;}
        internal void Clear(){Blocker=null;Array.Clear(hits,0,hits.Length);Array.Clear(overlaps,0,overlaps.Length);}
        readonly RaycastHit[] hits=new RaycastHit[64];
        readonly Collider[] overlaps=new Collider[64];
        Clearance Include(Collider collider,RoomGroundQuery ground,Func<Collider,bool> obstacle,Clearance state)
        {
            if(!collider||!obstacle(collider))return state;
            if(!Blocker||ground==null||!ground.Contains(collider))Blocker=collider;
            return ground!=null&&ground.Contains(collider)&&state!=Clearance.Blocked?Clearance.Ground:Clearance.Blocked;
        }
        Clearance At(Vector3 foot,float radius,float height,int mask,RoomGroundQuery ground,Func<Collider,bool> obstacle)
        {
            int count=Physics.OverlapCapsuleNonAlloc(foot+Vector3.up*(radius+RoomGroundQuery.Skin),foot+Vector3.up*(height-radius),radius,overlaps,mask,QueryTriggerInteraction.Ignore);
            if(count==overlaps.Length)return Clearance.Blocked;
            var result=Clearance.Clear;
            for(int i=0;i<count;i++)result=Include(overlaps[i],ground,obstacle,result);
            return result;
        }
        Clearance Sweep(Vector3 from,Vector3 to,float radius,float height,int mask,RoomGroundQuery ground,Func<Collider,bool> obstacle)
        {
            var step=to-from;var result=Clearance.Clear;
            if(step.sqrMagnitude>.00000001f){
                int count=Physics.CapsuleCastNonAlloc(from+Vector3.up*(radius+RoomGroundQuery.Skin),from+Vector3.up*(height-radius),radius,step.normalized,hits,step.magnitude+.001f,mask,QueryTriggerInteraction.Ignore);
                if(count==hits.Length)return Clearance.Blocked;
                for(int i=0;i<count;i++)result=Include(hits[i].collider,ground,obstacle,result);
            }
            var end=At(to,radius,height,mask,ground,obstacle);
            return (Clearance)Math.Max((int)result,(int)end);
        }
        internal bool ClearExact(Vector3 from,Vector3 to,float radius,float height,int mask,Func<Collider,bool> obstacle)
            {Blocker=null;return At(from,radius,height,mask,null,obstacle)==Clearance.Clear&&Sweep(from,to,radius,height,mask,null,obstacle)==Clearance.Clear;}
        internal bool Travel(RoomGroundQuery ground,Vector3 from,Vector3 planar,float radius,float height,int mask,
            Func<Collider,bool> obstacle,Func<Vector3,bool> permitted,out Vector3 destination,out string error)
        {
            Blocker=null;destination=from;error="The walking path is blocked or exceeds supported traversal";
            if(ground==null||!Creation.RoomRecipe.Finite(from)||!Creation.RoomRecipe.Finite(planar)||!float.IsFinite(radius)||radius<=0||
                !float.IsFinite(height)||height<radius*2+RoomGroundQuery.Skin||obstacle==null)return false;
            planar.y=0;float distance=planar.magnitude;
            // Bound query work for malformed or discontinuous requests.
            if(!float.IsFinite(distance)||distance>2.56f)return false;
            int count=Mathf.CeilToInt(distance/.04f);if(count>64)return false;
            if(!ground.Support(from,radius,RoomGroundQuery.MaximumStep+.001f,RoomGroundQuery.MaximumStep+.001f,out _,out _,out error))return false;
            if(At(from,radius,height,mask,ground,obstacle)!=Clearance.Clear){error="Your starting body space is occupied; choose a clear supported location";return false;}
            var current=from;
            for(int i=1;i<=count;i++){
                var target=from+planar*((float)i/count);target.y=current.y;
                if(!ground.Support(target,radius,RoomGroundQuery.MaximumStep+.001f,RoomGroundQuery.MaximumStep+.001f,out var floor,out _,out error))return false;
                target.y=floor.y;var end=At(target,radius,height,mask,ground,obstacle);
                if(end==Clearance.Ground){
                    // A capsule starts climbing before its centre reaches a step.
                    // Find the lowest clear foot, bounded above this same support.
                    float low=target.y,high=Mathf.Min(current.y,floor.y)+RoomGroundQuery.MaximumStep;
                    var raised=target;raised.y=high;
                    if(high<=low||At(raised,radius,height,mask,ground,obstacle)!=Clearance.Clear){error="The accepted ground is too high or crowded for this step";return false;}
                    for(int trial=0;trial<9;trial++){
                        raised.y=(low+high)*.5f;
                        if(At(raised,radius,height,mask,ground,obstacle)==Clearance.Clear)high=raised.y;else low=raised.y;
                    }
                    // Keep a small separation from a step edge; a cast must not
                    // end on the overlap threshold found by the binary search.
                    target.y=high+.003f;
                    if(target.y>Mathf.Min(current.y,floor.y)+RoomGroundQuery.MaximumStep){error="The step exceeds supported height";return false;}
                }else if(end==Clearance.Blocked){error="The destination body space is occupied";return false;}
                if(permitted!=null&&!permitted(target)){error="Movement would leave the supported world coordinates";return false;}
                var clear=Sweep(current,target,radius,height,mask,ground,obstacle);
                if(clear!=Clearance.Clear){
                    if(clear!=Clearance.Ground){error="The walking path is blocked";return false;}
                    // Only accepted terrain may be stepped over. Arbitrary props,
                    // overhead obstacles and unknown/saturated queries remain blocking.
                    var raisedFrom=current+Vector3.up*RoomGroundQuery.MaximumStep;
                    var raisedTo=new Vector3(target.x,raisedFrom.y,target.z);
                    if(raisedTo.y<target.y){error="The step exceeds supported height";return false;}
                    if(Sweep(current,raisedFrom,radius,height,mask,ground,obstacle)!=Clearance.Clear){error="The step has insufficient upward clearance";return false;}
                    if(Sweep(raisedFrom,raisedTo,radius,height,mask,ground,obstacle)!=Clearance.Clear){error="The step has insufficient forward clearance";return false;}
                    var landing=Sweep(raisedTo,target,radius,height,mask,ground,obstacle);
                    if(landing!=Clearance.Clear){
                        if(landing!=Clearance.Ground){error="The step landing is occupied";return false;}
                        // Use the swept landing itself as the authority. PhysX
                        // casts can reach a step edge before overlap queries do.
                        float low=target.y,high=Mathf.Min(current.y,floor.y)+RoomGroundQuery.MaximumStep;
                        var candidate=target;candidate.y=high;
                        if(high<low||Sweep(raisedTo,candidate,radius,height,mask,ground,obstacle)!=Clearance.Clear){error="The step has insufficient landing clearance";return false;}
                        for(int trial=0;trial<9;trial++){
                            candidate.y=(low+high)*.5f;
                            if(Sweep(raisedTo,candidate,radius,height,mask,ground,obstacle)==Clearance.Clear)high=candidate.y;else low=candidate.y;
                        }
                        target.y=high;
                        if(permitted!=null&&!permitted(target)){error="Movement would leave the supported world coordinates";return false;}
                    }
                }
                current=target;
            }
            destination=current;error=null;return true;
        }
    }
}
