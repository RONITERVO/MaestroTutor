// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;
namespace Maestro.Quest.Creation {
    // Bounded open-vessel geometry shared by rendering, receiving and pouring.
    internal static class ContainerFlowGeometry {
        internal static double FractionBelow(Vector3 normal,double radius,double height,double level) {
            double a=radius*Math.Sqrt(normal.x*normal.x+normal.z*normal.z),b=height*.5*Math.Abs(normal.y),cdf;
            if(a<1e-8)cdf=b<1e-8?0:(level+b)/(2*b);
            else if(b<1e-8)cdf=Disk(level/a);
            else cdf=a*(Integral((level+b)/a)-Integral((level-b)/a))/(2*b);
            return Math.Clamp(cdf,0,1);
        }
        // Shared free-surface plane for display and immersion. Quantities remain doubles.
        internal static float Level(Vector3 normal,float radius,float height,double fraction){
            double extent=radius*Math.Sqrt(normal.x*normal.x+normal.z*normal.z)+height*.5*Math.Abs(normal.y),low=-extent,high=extent;
            for(int pass=0;pass<24;pass++){double mid=(low+high)*.5;if(FractionBelow(normal,radius,height,mid)<fraction)low=mid;else high=mid;}
            return (float)((low+high)*.5);
        }
        // CDF of the sum of three centred uniforms (a plane through a box).
        // Pair the smallest-width polynomial differences before dividing: direct
        // inclusion/exclusion of eight cubes loses precision near axis alignment.
        internal static double BoxFractionBelow(Vector3 normal,double width,double height,double depth,double level) {
            double a=Math.Abs(normal.x)*width,b=Math.Abs(normal.y)*height,c=Math.Abs(normal.z)*depth;
            if(a<b)(a,b)=(b,a);if(b<c)(b,c)=(c,b);if(a<b)(a,b)=(b,a);
            if(a==0)return level>=0?1:0;
            if(level>0)return 1-BoxFractionBelow(normal,width,height,depth,-level);
            double x=level+(a+b+c)*.5;
            if(x<=0)return 0;
            if(b==0)return Math.Clamp(x/a,0,1);
            if(c==0)return Math.Clamp(x>=b?(x-b*.5)/a:x*x/(2*a*b),0,1);
            if(a>=b+c&&x>=b+c)return Math.Clamp((x-(b+c)*.5)/a,0,1);
            double Paired(double u)=>u<=0?0:u<c?u*u*(u/c):3*u*u-3*u*c+c*c;
            return Math.Clamp((Paired(x)-Paired(x-b)-Paired(x-a))/(6*a*b),0,1);
        }
        internal static double FractionBelow(RoomContainer c,Vector3 normal,double level)=>c.IsRectangular
            ? BoxFractionBelow(normal,c.rectangle.width,c.height,c.rectangle.depth,level)
            : FractionBelow(normal,c.radius,c.height,level);
        internal static float Level(RoomContainer c,Vector3 normal,double fraction){
            if(!c.IsRectangular)return Level(normal,c.radius,c.height,fraction);
            double extent=c.HorizontalExtent(normal)+c.height*.5*Math.Abs(normal.y),low=-extent,high=extent;
            for(int pass=0;pass<24;pass++){double mid=(low+high)*.5;if(FractionBelow(c,normal,mid)<fraction)low=mid;else high=mid;}
            return (float)((low+high)*.5);
        }
        static double Disk(double x){if(x<=-1)return 0;if(x>=1)return 1;return .5+(Math.Asin(x)+x*Math.Sqrt(1-x*x))/Math.PI;}
        static double Integral(double x){if(x<=-1)return 0;if(x>=1)return x;double root=Math.Sqrt(1-x*x);return x*.5+(x*Math.Asin(x)+root-root*root*root/3)/Math.PI;}
        internal static double Excess(RoomContainer container,Quaternion worldRotation,Vector3 up) {
            var n=Quaternion.Inverse(worldRotation*container.frame.rotation)*up;
            if(n.y<=0)return container.amountMl;
            double lip=n.y*container.height*.5-container.HorizontalExtent(n);
            return Math.Max(0,container.amountMl-container.capacityMl*FractionBelow(container,n,lip));
        }
        internal static Vector3 Lip(RoomContainer container,Transform root,Vector3 up,out Vector3 outward) {
            var rotation=root.rotation*container.frame.rotation;var n=Quaternion.Inverse(rotation)*up;
            var radial=new Vector3(-n.x,0,-n.z);radial=radial.sqrMagnitude>1e-8f?radial.normalized:Vector3.right;
            var lip=container.IsRectangular
                ? new Vector3(Mathf.Abs(n.x)<1e-8f?0:-Mathf.Sign(n.x)*container.rectangle.width*.5f,0,Mathf.Abs(n.z)<1e-8f?0:-Mathf.Sign(n.z)*container.rectangle.depth*.5f)
                : radial*container.radius;
            outward=rotation*(lip.sqrMagnitude>1e-12f?lip.normalized:radial);
            return root.TransformPoint(container.frame.position+container.frame.rotation*(Vector3.up*container.height+lip));
        }
        internal readonly struct Opening {
            readonly Vector3 mouth,normal;readonly Quaternion inverse;readonly float radius,halfWidth,halfDepth;readonly bool rectangle;
            internal Opening(RoomContainer container,Transform root){var rotation=root.rotation*container.frame.rotation;normal=rotation*Vector3.up;mouth=root.TransformPoint(container.frame.position+container.frame.rotation*(Vector3.up*container.height));float scale=Mathf.Abs(root.lossyScale.x)*.97f;radius=container.radius*scale;rectangle=container.IsRectangular;halfWidth=rectangle?container.rectangle.width*.5f*scale:0;halfDepth=rectangle?container.rectangle.depth*.5f*scale:0;inverse=Quaternion.Inverse(rotation);}
            internal bool Enters(Vector3 from,Vector3 to,Vector3 up,out float fraction){
                fraction=0;if(Vector3.Dot(normal,up)<.15f)return false;
                float a=Vector3.Dot(from-mouth,normal),b=Vector3.Dot(to-mouth,normal);
                if(a<0||b>=0||a-b<1e-7f)return false;
                fraction=a/(a-b);var radial=Vector3.LerpUnclamped(from,to,fraction)-mouth;var local=inverse*radial;return rectangle?Mathf.Abs(local.x)<=halfWidth&&Mathf.Abs(local.z)<=halfDepth:radial.sqrMagnitude<=radius*radius;
            }
        }
        internal static bool Enters(RoomContainer container,Transform root,Vector3 from,Vector3 to,Vector3 up,out float fraction)=>new Opening(container,root).Enters(from,to,up,out fraction);
    }
}
