// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
namespace Maestro.Quest.Creation {
    // Bounded immersion in authored circular/rectangular cavities, not fluid forces.
    internal static class ContainerScoopingGeometry {
        internal readonly struct Contact {
            readonly Matrix4x4 donorFrame;
            readonly float donorRadius,donorHeight,halfWidth,halfDepth,level;
            readonly bool rectangular;
            readonly Vector3 mouth,right,forward,up;
            internal readonly float DonorFootprint;
            internal Contact(Matrix4x4 frame,RoomContainer source,float surfaceLevel,Vector3 opening,Quaternion rotation,RoomContainer destination,float scale,Vector3 gravityUp,float worldArea){
                donorFrame=frame;donorRadius=source.radius;donorHeight=source.height;level=surfaceLevel;mouth=opening;rectangular=source.IsRectangular;
                halfWidth=rectangular?source.rectangle.width*.5f:0;halfDepth=rectangular?source.rectangle.depth*.5f:0;
                right=rotation*Vector3.right*(destination.IsRectangular?destination.rectangle.width*.25f:destination.radius*.5f)*scale;
                forward=rotation*Vector3.forward*(destination.IsRectangular?destination.rectangle.depth*.25f:destination.radius*.5f)*scale;
                up=gravityUp;DonorFootprint=worldArea;
            }
            // Five independently clear paths allow a handle across the opening.
            internal bool Path(int index,out Vector3 from,out Vector3 to){
                var point=mouth+(index switch{1=>right,2=>-right,3=>forward,4=>-forward,_=>Vector3.zero});
                var surface=point+up*(level-Vector3.Dot(point,up));var local=donorFrame.MultiplyPoint3x4(surface);
                from=surface+up*.001f;to=point-up*.001f;
                return local.y>=0&&local.y<=donorHeight&&(rectangular
                    ? Mathf.Abs(local.x)<=halfWidth&&Mathf.Abs(local.z)<=halfDepth
                    : new Vector2(local.x,local.z).sqrMagnitude<=donorRadius*donorRadius);
            }
        }
        internal static bool TryContact(RoomContainer source,Transform sourceRoot,RoomContainer destination,Transform destinationRoot,Vector3 up,out Contact contact){
            contact=default;
            return destination!=null&&destination.amountMl<destination.capacityMl&&TryImmersion(source,sourceRoot,destination,destinationRoot,up,out contact);
        }
        // Full vessels still have an immersion relationship even though they cannot take more.
        internal static bool TryImmersion(RoomContainer source,Transform sourceRoot,RoomContainer destination,Transform destinationRoot,Vector3 up,out Contact contact){
            contact=default;
            if(source==null||destination==null||!sourceRoot||!destinationRoot||source.amountMl<=0)return false;
            if(destination.amountMl>0&&!source.SameLiquid(destination))return false;
            var sourceRotation=sourceRoot.rotation*source.frame.rotation;var destinationRotation=destinationRoot.rotation*destination.frame.rotation;
            if(Vector3.Dot(sourceRotation*Vector3.up,up)<.15f||Vector3.Dot(destinationRotation*Vector3.up,up)<.15f)return false;
            float sourceScale=Mathf.Abs(sourceRoot.lossyScale.x),destinationScale=Mathf.Abs(destinationRoot.lossyScale.x);
            if(sourceScale<=0||destinationScale<=0)return false;
            float sourceArea=source.FootprintArea*sourceScale*sourceScale,destinationArea=destination.FootprintArea*destinationScale*destinationScale;
            if(destinationArea>=sourceArea*.95f*.95f)return false;
            var frame=(sourceRoot.localToWorldMatrix*Matrix4x4.TRS(source.frame.position,source.frame.rotation,Vector3.one)).inverse;
            var bottom=destinationRoot.TransformPoint(destination.frame.position);var mouth=destinationRoot.TransformPoint(destination.frame.position+destination.frame.rotation*(Vector3.up*destination.height));
            var axis=Quaternion.Inverse(sourceRotation)*(destinationRotation*Vector3.up);
            float radius=destination.radius*destinationScale/sourceScale;
            bool InsideDisk(Vector3 point){
                var local=frame.MultiplyPoint3x4(point);float vertical=radius*Mathf.Sqrt(Mathf.Max(0,1-axis.y*axis.y));
                if(local.y-vertical<.001f/sourceScale||local.y+vertical>source.height-.001f/sourceScale)return false;
                if(source.IsRectangular)return Mathf.Abs(local.x)+radius*Mathf.Sqrt(Mathf.Max(0,1-axis.x*axis.x))<=source.rectangle.width*.5f*.97f
                    &&Mathf.Abs(local.z)+radius*Mathf.Sqrt(Mathf.Max(0,1-axis.z*axis.z))<=source.rectangle.depth*.5f*.97f;
                return new Vector2(local.x,local.z).magnitude+radius<=source.radius*.97f;
            }
            float openingVertical;
            if(destination.IsRectangular){
                // A convex cylinder or box contains the receiving rectangular prism
                // iff it contains all eight vertices. No bounding-circle false fit.
                for(int i=0;i<8;i++){
                    var corner=new Vector3((i%2==0?-1:1)*destination.rectangle.width*.5f,i<4?0:destination.height,(i%4<2?-1:1)*destination.rectangle.depth*.5f);
                    var local=frame.MultiplyPoint3x4(destinationRoot.TransformPoint(destination.frame.position+destination.frame.rotation*corner));
                    if(local.y<.001f/sourceScale||local.y>source.height-.001f/sourceScale||!source.ContainsHorizontal(local,.97f))return false;
                }
                var openingNormal=Quaternion.Inverse(destinationRotation)*up;
                openingVertical=(float)destination.HorizontalExtent(openingNormal)*destinationScale;
            }else{
                if(!InsideDisk(bottom)||!InsideDisk(mouth))return false;
                openingVertical=destination.radius*destinationScale*Mathf.Sqrt(Mathf.Max(0,1-Mathf.Pow(Vector3.Dot(destinationRotation*Vector3.up,up),2)));
            }
            var normal=Quaternion.Inverse(sourceRotation)*up;
            float localLevel=ContainerFlowGeometry.Level(source,normal,source.amountMl/source.capacityMl);
            var centre=sourceRoot.TransformPoint(source.frame.position+source.frame.rotation*(Vector3.up*source.height*.5f));
            float worldLevel=Vector3.Dot(centre,up)+localLevel*sourceScale;
            if(Vector3.Dot(mouth,up)+openingVertical>worldLevel-.001f)return false;
            contact=new Contact(frame,source,worldLevel,mouth,destinationRotation,destination,destinationScale,up,sourceArea);return true;
        }
    }
}
