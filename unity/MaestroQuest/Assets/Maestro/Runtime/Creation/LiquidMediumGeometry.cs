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
        internal bool Sweep(Vector3 from,Vector3 to,float radius,float height,out float depth,out Vector3 contact)=>Sweep(from,to,radius,height,out depth,out contact,out _);
        internal bool Sweep(Vector3 from,Vector3 to,float radius,float height,out float depth,out Vector3 contact,out Vector3 foot){
            depth=0;contact=foot=default;if(Contents.amountMl<=0)return false;
            var a=inverse.MultiplyPoint3x4(from);var d=inverse.MultiplyVector(to-from);float enter=0,exit=1;
            for(int i=0;i<PlaneCount;i++){LocalPlane(i,out var normal,out float limit);if(!Plane(normal,limit,a,d,radius,height,ref enter,ref exit))return false;}
            // The liquid surface stays perpendicular to gravity, including tilted vessels.
            float support=Support(Up,radius,height);
            if(!Clip(Vector3.Dot(Up,from),Vector3.Dot(Up,to-from),Level+support,ref enter,ref exit))return false;
            foot=Vector3.Lerp(from,to,Vector3.Dot(Up,to-from)<0?exit:enter);
            depth=Mathf.Max(0,Level-Vector3.Dot(Up,foot));
            contact=foot+Vector3.up*Mathf.Clamp(depth*.5f,.001f,height);
            return depth>.0001f;
        }
        int PlaneCount=>Contents.IsRectangular?6:18;
        void LocalPlane(int index,out Vector3 normal,out float limit){
            if(index<2){normal=index==0?Vector3.down:Vector3.up;limit=index==0?0:Contents.height;return;}
            if(Contents.IsRectangular){normal=index switch{2=>Vector3.right,3=>Vector3.left,4=>Vector3.forward,_=>Vector3.back};limit=index<4?Contents.rectangle.width*.5f:Contents.rectangle.depth*.5f;return;}
            float angle=(index-2)*Mathf.PI/8;normal=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));limit=Contents.radius;
        }
        static float Support(Vector3 normal,float radius,float height)=>radius*Mathf.Sqrt(normal.x*normal.x+normal.z*normal.z)-Mathf.Min(0,normal.y*height);
        // Exactly the same expanded half-spaces as Sweep, restricted to a foot
        // height. Tilted volumes must not use a narrower rendered-volume outline.
        internal void RouteLines(float radius,float height,float footY,System.Collections.Generic.List<Vector3> lines){
            lines.Clear();var foot=new Vector3(0,footY,0);var local=inverse.MultiplyPoint3x4(foot);
            for(int i=0;i<PlaneCount;i++){
                LocalPlane(i,out var normal,out float limit);var worldNormal=inverse.transpose.MultiplyVector(normal);
                lines.Add(new Vector3(worldNormal.x,worldNormal.z,limit+Support(worldNormal,radius,height)-Vector3.Dot(normal,local)));
            }
            lines.Add(new Vector3(Up.x,Up.z,Level+Support(Up,radius,height)-Vector3.Dot(Up,foot)));
        }
        bool Plane(Vector3 normal,float limit,Vector3 a,Vector3 d,float radius,float height,ref float enter,ref float exit){
            var worldNormal=inverse.transpose.MultiplyVector(normal);
            float support=Support(worldNormal,radius,height);
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
