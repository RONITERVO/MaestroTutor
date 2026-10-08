// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation {
    // One geometric interpretation of the live measured quantity; no extra inventory.
    internal readonly struct LiquidMediumGeometry {
        internal readonly string Id;internal readonly RoomItem Item;internal readonly RoomContainer Contents;
        internal readonly Rigidbody Body;
        internal readonly float Level,Volume;internal readonly Vector3 Up;readonly Matrix4x4 inverse;
        internal LiquidMediumGeometry(string id,RoomItem item,RoomContainer contents,Vector3 up,Rigidbody body){
            Id=id;Item=item;Contents=contents;Up=up;Body=body;
            var t=item.transform;float scale=Mathf.Abs(t.lossyScale.x);
            var rotation=t.rotation*contents.frame.rotation;
            inverse=(t.localToWorldMatrix*Matrix4x4.TRS(contents.frame.position,contents.frame.rotation,Vector3.one)).inverse;
            var centre=t.TransformPoint(contents.frame.position+contents.frame.rotation*(Vector3.up*contents.height*.5f));
            Level=Vector3.Dot(centre,up)+ContainerFlowGeometry.Level(contents,Quaternion.Inverse(rotation)*up,contents.amountMl/contents.capacityMl)*scale;
            Volume=contents.FootprintArea*contents.height*scale*scale*scale;
        }
        // Clip the whole swept upright body against the convex liquid cavity.
        // The cylinder uses sixteen circumscribed planes (at most 2% wider at a
        // corner). Expanded planes conservatively include the body footprint:
        // a narrow cavity cannot be skipped between movement frames.
        internal bool Sweep(Vector3 from,Vector3 to,float radius,float height,out float depth,out Vector3 contact){
            depth=0;contact=default;if(Contents.amountMl<=0)return false;
            var a=inverse.MultiplyPoint3x4(from);var d=inverse.MultiplyVector(to-from);float enter=0,exit=1;
            if(!Plane(Vector3.down,0,a,d,radius,height,ref enter,ref exit)||
                !Plane(Vector3.up,Contents.height,a,d,radius,height,ref enter,ref exit))return false;
            if(Contents.IsRectangular){
                if(!Plane(Vector3.right,Contents.rectangle.width*.5f,a,d,radius,height,ref enter,ref exit)||
                    !Plane(Vector3.left,Contents.rectangle.width*.5f,a,d,radius,height,ref enter,ref exit)||
                    !Plane(Vector3.forward,Contents.rectangle.depth*.5f,a,d,radius,height,ref enter,ref exit)||
                    !Plane(Vector3.back,Contents.rectangle.depth*.5f,a,d,radius,height,ref enter,ref exit))return false;
            }else for(int i=0;i<16;i++){
                float angle=i*Mathf.PI/8;
                if(!Plane(new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle)),Contents.radius,a,d,radius,height,ref enter,ref exit))return false;
            }
            // The liquid surface stays perpendicular to gravity, including tilted vessels.
            float support=radius*Mathf.Sqrt(Up.x*Up.x+Up.z*Up.z)-Mathf.Min(0,Up.y*height);
            if(!Clip(Vector3.Dot(Up,from),Vector3.Dot(Up,to-from),Level+support,ref enter,ref exit))return false;
            var foot=Vector3.Lerp(from,to,Vector3.Dot(Up,to-from)<0?exit:enter);
            depth=Mathf.Max(0,Level-Vector3.Dot(Up,foot));
            contact=foot+Vector3.up*Mathf.Clamp(depth*.5f,.001f,height);
            return depth>.0001f;
        }
        bool Plane(Vector3 normal,float limit,Vector3 a,Vector3 d,float radius,float height,ref float enter,ref float exit){
            var worldNormal=inverse.transpose.MultiplyVector(normal);
            float support=radius*Mathf.Sqrt(worldNormal.x*worldNormal.x+worldNormal.z*worldNormal.z)-Mathf.Min(0,worldNormal.y*height);
            return Clip(Vector3.Dot(normal,a),Vector3.Dot(normal,d),limit+support,ref enter,ref exit);
        }
        static bool Clip(float at,float change,float limit,ref float enter,ref float exit){
            if(Mathf.Abs(change)<1e-8f)return at<=limit;
            float t=(limit-at)/change;if(change>0)exit=Mathf.Min(exit,t);else enter=Mathf.Max(enter,t);
            return enter<=exit;
        }
        internal bool Sample(Vector3 point,out float depth){
            depth=Level-Vector3.Dot(point,Up);var local=inverse.MultiplyPoint3x4(point);
            return Contents.amountMl>0&&depth>0&&local.y>=0&&local.y<=Contents.height&&Contents.ContainsHorizontal(local);
        }
    }
}
