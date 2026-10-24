// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Interaction {
    /// <summary>Proposes supported/wading/buoyant steps using one capsule.
    /// It never changes an actor. The caller owns inputs and publishes only a
    /// complete accepted proposal; cavity admission alone is never clearance.</summary>
    internal sealed class RoomShoreMotor {
        internal delegate bool Admission(Vector3 from,Vector3 to,bool floating,out string error);
        internal readonly struct Step {
            internal readonly Vector3 Foot;internal readonly bool Floating;internal readonly float Distance;
            internal Step(Vector3 foot,bool floating,float distance){Foot=foot;Floating=floating;Distance=distance;}
        }
        readonly RoomGroundMotor motor=new();
        internal void Clear()=>motor.Clear();
        bool Buoyant(Vector3 from,Vector3 to,RoomUprightBody body,int mask,Func<Collider,bool> obstacle,Admission admission,out string error){
            if(!admission(from,to,true,out error))return false;
            error="The buoyant capsule is blocked by solid geometry or the bounded query is full";
            if(!motor.ClearExact(from,to,body.Radius,body.Height,mask,obstacle))return false;
            error=null;return true;
        }
        internal bool Travel(RoomGroundQuery ground,Vector3 from,Vector3 planar,float maximumDistance,RoomUprightBody body,
            float floatFoot,bool floating,int mask,Func<Collider,bool> obstacle,Func<Vector3,bool> permitted,Admission admission,
            out Step result,out string error){
            result=new(from,floating,0);error="Shore movement needs a finite body, bounded step, ground and selected water admission";
            if(ground==null||!body.Valid||!RoomRecipe.Finite(from)||!RoomRecipe.Finite(planar)||!float.IsFinite(floatFoot)||
                !float.IsFinite(maximumDistance)||maximumDistance<=0||maximumDistance>2.56f||obstacle==null||permitted==null||admission==null)return false;
            planar.y=0;
            if(planar.magnitude>2.56f)return false;
            if(floating){
                if(!Buoyant(from,from,body,mask,obstacle,admission,out error))return false;
            }else{
                if(!motor.Travel(ground,from,Vector3.zero,body.Radius,body.Height,mask,obstacle,permitted,out _,out error)||
                    !admission(from,from,false,out error))return false;
            }
            // A changed level or deep supported stance may need a gradual rise.
            // This consumes the step's budget without also advancing horizontally.
            if((floating&&Mathf.Abs(floatFoot-from.y)>.005f||!floating&&floatFoot>from.y+.005f)&&
                admission(from,from,true,out _)){
                var target=Vector3.MoveTowards(from,new Vector3(from.x,floatFoot,from.z),maximumDistance);
                if(!permitted(target)){error="Shore movement would leave the accepted world";return false;}
                if(!Buoyant(from,target,body,mask,obstacle,admission,out error))return false;
                result=new(target,true,Vector3.Distance(from,target));error=null;return true;
            }
            planar=Vector3.ClampMagnitude(planar,maximumDistance);
            int count=Mathf.Max(1,Mathf.CeilToInt(planar.magnitude/.04f));if(count>64)return false;
            var current=from;float distance=0;bool inWater=floating;
            for(int i=1;i<=count;i++){
                var target=from+planar*((float)i/count);target.y=current.y;
                bool supported=motor.Travel(ground,current,target-current,body.Radius,body.Height,mask,obstacle,permitted,out var floor,out var supportError);
                if(supported&&!admission(current,floor,false,out supportError))supported=false;
                // A zero ground step preserves the body foot, including a floating
                // foot near a shallow bed. Classify using the actual contact.
                bool deep=supported&&motor.SupportPoint.y<floatFoot-.001f&&admission(floor,floor,true,out _);
                Vector3 next;bool nextFloating;
                if(supported&&motor.SupportPoint.y>=floatFoot-.001f){next=floor;nextFloating=false;}
                else{
                    var afloat=new Vector3(target.x,floatFoot,target.z);
                    bool near= inWater||Mathf.Abs(afloat.y-current.y)<=RoomGroundQuery.MaximumStep+.001f;
                    bool wet=near&&permitted(afloat)&&Buoyant(current,afloat,body,mask,obstacle,admission,out _);
                    if(wet&&(inWater||deep||!supported)){next=afloat;nextFloating=true;}
                    else if(supported&&!deep&&!inWater){next=floor;nextFloating=false;}
                    else{error=supportError??"No continuously supported or clear buoyant shore step is available";return false;}
                }
                if(!permitted(next)){error="Shore movement would leave the accepted world";return false;}
                distance+=Vector3.Distance(current,next);current=next;inWater=nextFloating;
            }
            result=new(current,inWater,distance);error=null;return true;
        }
    }
}
