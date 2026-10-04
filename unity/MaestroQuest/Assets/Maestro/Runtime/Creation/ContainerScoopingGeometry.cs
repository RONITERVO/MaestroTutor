// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;
namespace Maestro.Quest.Creation {
    // Conservative open-cylinder immersion. This is a bounded container model,
    // not displacement, trapped air, pressure, or a general fluid solver.
    internal static class ContainerScoopingGeometry {
        internal readonly struct Contact {
            readonly Matrix4x4 donorFrame;
            readonly float donorRadius,donorHeight,level;
            readonly Vector3 mouth,right,forward,up;
            internal readonly float DonorRadius;
            internal Contact(Matrix4x4 frame,float radius,float height,float surfaceLevel,Vector3 opening,Quaternion rotation,float openingRadius,Vector3 gravityUp,float worldRadius){
                donorFrame=frame;donorRadius=radius;donorHeight=height;level=surfaceLevel;mouth=opening;right=rotation*Vector3.right*(openingRadius*.5f);forward=rotation*Vector3.forward*(openingRadius*.5f);up=gravityUp;DonorRadius=worldRadius;
            }
            // Five possible openings keep a handle across the centre from acting
            // like a sealed lid. Every accepted path must independently be clear.
            internal bool Path(int index,out Vector3 from,out Vector3 to){
                var point=mouth+(index switch{1=>right,2=>-right,3=>forward,4=>-forward,_=>Vector3.zero});
                var surface=point+up*(level-Vector3.Dot(point,up));var local=donorFrame.MultiplyPoint3x4(surface);
                from=surface+up*.001f;to=point-up*.001f;
                return local.y>=0&&local.y<=donorHeight&&new Vector2(local.x,local.z).sqrMagnitude<=donorRadius*donorRadius;
            }
        }
        internal static bool TryContact(RoomContainer source,Transform sourceRoot,RoomContainer destination,Transform destinationRoot,Vector3 up,out Contact contact){
            contact=default;
            if(source==null||destination==null||!sourceRoot||!destinationRoot||source.amountMl<=0||destination.amountMl>=destination.capacityMl)return false;
            if(destination.amountMl>0&&(source.liquid!=destination.liquid||!source.color.Equals(destination.color)))return false;
            var sourceRotation=sourceRoot.rotation*source.frame.rotation;var destinationRotation=destinationRoot.rotation*destination.frame.rotation;
            if(Vector3.Dot(sourceRotation*Vector3.up,up)<.15f||Vector3.Dot(destinationRotation*Vector3.up,up)<.15f)return false;
            float sourceScale=Mathf.Abs(sourceRoot.lossyScale.x),destinationScale=Mathf.Abs(destinationRoot.lossyScale.x);
            float sourceRadius=source.radius*sourceScale,destinationRadius=destination.radius*destinationScale;
            if(sourceScale<=0||destinationScale<=0||destinationRadius>=sourceRadius*.95f)return false;
            var frame=(sourceRoot.localToWorldMatrix*Matrix4x4.TRS(source.frame.position,source.frame.rotation,Vector3.one)).inverse;
            var bottom=destinationRoot.TransformPoint(destination.frame.position);var mouth=destinationRoot.TransformPoint(destination.frame.position+destination.frame.rotation*(Vector3.up*destination.height));
            var axis=Quaternion.Inverse(sourceRotation)*(destinationRotation*Vector3.up);
            float radius=destinationRadius/sourceScale,vertical=radius*Mathf.Sqrt(Mathf.Max(0,1-axis.y*axis.y));
            bool Inside(Vector3 point){var local=frame.MultiplyPoint3x4(point);return local.y-vertical>=.001f/sourceScale&&local.y+vertical<=source.height-.001f/sourceScale&&new Vector2(local.x,local.z).magnitude+radius<=source.radius*.97f;}
            // The complete receiving cavity must fit: dragging it through the
            // donor's floor/wall must not produce a refill through solid material.
            if(!Inside(bottom)||!Inside(mouth))return false;
            var normal=Quaternion.Inverse(sourceRotation)*up;
            float localLevel=ContainerFlowGeometry.Level(normal,source.radius,source.height,source.amountMl/source.capacityMl);
            var centre=sourceRoot.TransformPoint(source.frame.position+source.frame.rotation*(Vector3.up*source.height*.5f));
            float worldLevel=Vector3.Dot(centre,up)+localLevel*sourceScale;
            float openingVertical=destinationRadius*Mathf.Sqrt(Mathf.Max(0,1-Mathf.Pow(Vector3.Dot(destinationRotation*Vector3.up,up),2)));
            if(Vector3.Dot(mouth,up)+openingVertical>worldLevel-.001f)return false;
            contact=new Contact(frame,source.radius,source.height,worldLevel,mouth,destinationRotation,destinationRadius,up,sourceRadius);return true;
        }
    }
}
