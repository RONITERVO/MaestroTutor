// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // A bounded look-ahead policy, not proof of future tracking. Discontinuities
    // have unknown demand until two fresh samples agree on a finite motion rate.
    // The frame owner samples; diagnostic reads never advance this history.
    internal sealed class RoomObservationPrediction
    {
        internal const double MaximumGap=.25;
        internal const float Horizon=.35f, MinimumTranslation=.15f, MinimumAngle=12;
        Vector3 position;Quaternion rotation;double sampledAt;
        bool sampled,ready;float translation,angle;
        internal void Reset(){sampled=ready=false;}
        internal void Sample(Vector3 point,Quaternion orientation,double now)
        {
            if(!double.IsFinite(now)||!RoomRecipe.Finite(point)||!Finite(orientation)){Reset();return;}
            orientation=orientation.normalized;
            double elapsed=now-sampledAt;
            // Multiple samplers in the same frame must not erase a valid rate.
            if(sampled&&elapsed==0&&point==position&&orientation==rotation)return;
            ready=false;
            if(sampled&&elapsed>0&&elapsed<=MaximumGap){
                float speed=Vector3.Distance(point,position)/(float)elapsed;
                float turn=Quaternion.Angle(rotation,orientation)/(float)elapsed;
                // A relocation or tracking jump is not a velocity to extrapolate.
                if(float.IsFinite(speed)&&speed<=10&&float.IsFinite(turn)&&turn<=1080){
                    translation=MinimumTranslation+speed*Horizon;
                    angle=Mathf.Min(180,MinimumAngle+turn*Horizon);ready=true;
                }
            }
            position=point;rotation=orientation;sampledAt=now;sampled=true;
        }
        internal bool Read(double now,out float distance,out float degrees)
        {
            distance=translation;degrees=angle;
            return ready&&double.IsFinite(now)&&now>=sampledAt&&now-sampledAt<=MaximumGap;
        }
        static bool Finite(Quaternion value)=>float.IsFinite(value.x)&&float.IsFinite(value.y)&&float.IsFinite(value.z)&&float.IsFinite(value.w)&&float.IsFinite(Quaternion.Dot(value,value))&&Quaternion.Dot(value,value)>1e-8f;
    }
}
