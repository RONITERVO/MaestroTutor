// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;
namespace Maestro.Quest.Creation {
    // Bounded open-cylinder approximation shared by rendering and physical pouring.
    internal static class ContainerFlowGeometry {
        internal static double FractionBelow(Vector3 normal,double radius,double height,double level) {
            double a=radius*Math.Sqrt(normal.x*normal.x+normal.z*normal.z),b=height*.5*Math.Abs(normal.y),cdf;
            if(a<1e-8)cdf=b<1e-8?0:(level+b)/(2*b);
            else if(b<1e-8)cdf=Disk(level/a);
            else cdf=a*(Integral((level+b)/a)-Integral((level-b)/a))/(2*b);
            return Math.Clamp(cdf,0,1);
        }
        static double Disk(double x){if(x<=-1)return 0;if(x>=1)return 1;return .5+(Math.Asin(x)+x*Math.Sqrt(1-x*x))/Math.PI;}
        static double Integral(double x){if(x<=-1)return 0;if(x>=1)return x;double root=Math.Sqrt(1-x*x);return x*.5+(x*Math.Asin(x)+root-root*root*root/3)/Math.PI;}
        internal static double Excess(RoomContainer container,Quaternion worldRotation,Vector3 up) {
            var n=Quaternion.Inverse(worldRotation*container.frame.rotation)*up;
            if(n.y<=0)return container.amountMl;
            double lip=n.y*container.height*.5-container.radius*Math.Sqrt(n.x*n.x+n.z*n.z);
            return Math.Max(0,container.amountMl-container.capacityMl*FractionBelow(n,container.radius,container.height,lip));
        }
        internal static Vector3 Lip(RoomContainer container,Transform root,Vector3 up,out Vector3 outward) {
            var rotation=root.rotation*container.frame.rotation;var n=Quaternion.Inverse(rotation)*up;
            var radial=new Vector3(-n.x,0,-n.z);radial=radial.sqrMagnitude>1e-8f?radial.normalized:Vector3.right;
            outward=rotation*radial;
            return root.TransformPoint(container.frame.position+container.frame.rotation*(Vector3.up*container.height+radial*container.radius));
        }
        internal readonly struct Opening {
            readonly Vector3 mouth,normal;readonly float radius;
            internal Opening(RoomContainer container,Transform root){var rotation=root.rotation*container.frame.rotation;normal=rotation*Vector3.up;mouth=root.TransformPoint(container.frame.position+container.frame.rotation*(Vector3.up*container.height));radius=container.radius*Mathf.Abs(root.lossyScale.x)*.97f;}
            internal bool Enters(Vector3 from,Vector3 to,Vector3 up,out float fraction){
                fraction=0;if(Vector3.Dot(normal,up)<.15f)return false;
                float a=Vector3.Dot(from-mouth,normal),b=Vector3.Dot(to-mouth,normal);
                if(a<0||b>=0||a-b<1e-7f)return false;
                fraction=a/(a-b);var radial=Vector3.LerpUnclamped(from,to,fraction)-mouth;return radial.sqrMagnitude<=radius*radius;
            }
        }
        internal static bool Enters(RoomContainer container,Transform root,Vector3 from,Vector3 to,Vector3 up,out float fraction)=>new Opening(container,root).Enters(from,to,up,out fraction);
    }
}
