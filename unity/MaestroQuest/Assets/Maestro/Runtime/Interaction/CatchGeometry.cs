// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    public static class CatchGeometry
    {
        public static float SegmentDistance(Vector3 point,Vector3 a,Vector3 b){var d=b-a;return Vector3.Distance(point,a+d*(d.sqrMagnitude>1e-12f?Mathf.Clamp01(Vector3.Dot(point-a,d)/d.sqrMagnitude):0));}
        // Relative swept motion detects a crossing even when neither endpoint is inside.
        public static bool Crosses(Vector3 oldBall,Vector3 ball,Vector3 oldSocket,Vector3 socket,float radius)=>
            float.IsFinite(oldBall.sqrMagnitude)&&float.IsFinite(ball.sqrMagnitude)&&float.IsFinite(oldSocket.sqrMagnitude)&&float.IsFinite(socket.sqrMagnitude)&&float.IsFinite(radius)&&radius>0&&SegmentDistance(Vector3.zero,oldBall-oldSocket,ball-socket)<=radius;
        public static bool TwoBone(Vector3 shoulder,Vector3 elbow,Vector3 hand,Vector3 goal,Vector3 pole,out Vector3 fittedElbow,out Vector3 fittedHand){
            fittedElbow=elbow;fittedHand=hand;
            if(!float.IsFinite(shoulder.sqrMagnitude)||!float.IsFinite(elbow.sqrMagnitude)||!float.IsFinite(hand.sqrMagnitude)||!float.IsFinite(goal.sqrMagnitude)||!float.IsFinite(pole.sqrMagnitude))return false;
            float a=Vector3.Distance(shoulder,elbow),b=Vector3.Distance(elbow,hand);if(a<.005f||b<.005f||a+b>3)return false;
            var direction=goal-shoulder;if(direction.sqrMagnitude<1e-10f)direction=hand-shoulder;if(direction.sqrMagnitude<1e-10f)direction=Vector3.forward;direction.Normalize();
            float distance=Mathf.Clamp(Vector3.Distance(goal,shoulder),Mathf.Abs(a-b)+.0001f,a+b-.0001f);
            var bend=Vector3.ProjectOnPlane(pole,direction);if(bend.sqrMagnitude<1e-8f)bend=Vector3.ProjectOnPlane(elbow-shoulder,direction);
            if(bend.sqrMagnitude<1e-8f)bend=Vector3.Cross(direction,Mathf.Abs(direction.y)<.9f?Vector3.up:Vector3.right);bend.Normalize();
            float along=(a*a+distance*distance-b*b)/(2*distance),height=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            fittedElbow=shoulder+direction*along+bend*height;fittedHand=shoulder+direction*distance;return true;
        }
    }
}
