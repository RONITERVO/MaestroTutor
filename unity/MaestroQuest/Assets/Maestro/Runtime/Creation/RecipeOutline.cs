// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;
namespace Maestro.Quest.Creation {
    internal static class RecipeOutline {
        internal static bool Valid(Vector2[] p,float minimumX,int maximumPoints) {
            if(p==null||p.Length<3||p.Length>maximumPoints)return false;
            double area=0;
            for(int i=0;i<p.Length;i++) {
                var a=p[i];var b=p[(i+1)%p.Length];
                if(!float.IsFinite(a.x)||!float.IsFinite(a.y)||a.x<minimumX||a.x>.5f||a.y<-.5f||a.y>.5f||(b-a).sqrMagnitude<.000001f)return false;
                area+=(double)a.x*b.y-(double)b.x*a.y;
                for(int j=i+1;j<p.Length;j++) {
                    if(j==i+1||i==0&&j==p.Length-1)continue;
                    if(Intersects(a,b,p[j],p[(j+1)%p.Length]))return false;
                }
            }
            // Counter-clockwise, nonzero solid cross section; touching and self intersections are refused.
            return area>=.0002;
        }
        static double Cross(Vector2 a,Vector2 b,Vector2 c)=>(double)(b.x-a.x)*(c.y-a.y)-(double)(b.y-a.y)*(c.x-a.x);
        static bool Intersects(Vector2 a,Vector2 b,Vector2 c,Vector2 d) {
            const double e=1e-8;
            double u=Cross(a,b,c),v=Cross(a,b,d),w=Cross(c,d,a),x=Cross(c,d,b);
            if((u>e&&v< -e||u< -e&&v>e)&&(w>e&&x< -e||w< -e&&x>e))return true;
            bool On(Vector2 p,Vector2 q,Vector2 r)=>r.x>=Math.Min(p.x,q.x)-e&&r.x<=Math.Max(p.x,q.x)+e&&r.y>=Math.Min(p.y,q.y)-e&&r.y<=Math.Max(p.y,q.y)+e;
            return Math.Abs(u)<=e&&On(a,b,c)||Math.Abs(v)<=e&&On(a,b,d)||Math.Abs(w)<=e&&On(c,d,a)||Math.Abs(x)<=e&&On(c,d,b);
        }
    }
}
