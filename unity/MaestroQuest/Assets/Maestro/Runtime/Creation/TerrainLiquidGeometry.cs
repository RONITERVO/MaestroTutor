// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Maestro.Quest.Creation {
    // Immutable geometry over the SAME triangles as HeightFieldGeometry. There is
    // no liquid inventory here: callers supply the authoritative fill fraction.
    internal sealed class TerrainLiquidGeometry {
        readonly struct Tetrahedron {
            internal readonly int A,B,C,D;internal readonly double Volume;
            internal Tetrahedron(int a,int b,int c,int d,Vector3[] points){A=a;B=b;C=c;D=d;Volume=VolumeOf(points[a],points[b],points[c],points[d]);}
        }
        readonly RoomHeightField bed;
        readonly Vector3[] points;
        readonly int[] triangles;
        readonly Tetrahedron[] tetrahedra;
        readonly double[] projection;
        readonly List<Vector3> polygon=new(6);
        Vector3 projectedNormal;bool projected,levelCached;
        double minimum,maximum,lastFraction;float lastLevel;
        internal readonly float Ceiling;
        internal double Volume {get;}
        internal TerrainLiquidGeometry(RoomHeightField field,float ceiling){
            if(field==null||!field.Validate(out _)||!float.IsFinite(ceiling)||ceiling<=0)throw new ArgumentException("Invalid terrain liquid cavity");
            bed=field.Copy();Ceiling=ceiling;int n=bed.heights.Length;
            points=new Vector3[n*2];projection=new double[n*2];
            for(int z=0;z<=bed.cells;z++)for(int x=0;x<=bed.cells;x++){
                int i=z*(bed.cells+1)+x;var p=bed.Vertex(x,z);if(p.y>ceiling)throw new ArgumentException("The opening must be at or above every terrain vertex");
                points[i]=p;points[i+n]=new Vector3(p.x,ceiling,p.z);
            }
            triangles=new int[bed.cells*bed.cells*6];tetrahedra=new Tetrahedron[bed.cells*bed.cells*6];int triangle=0,tetra=0;double volume=0;
            void Add(int a,int b,int c){
                triangles[triangle++]=a;triangles[triangle++]=b;triangles[triangle++]=c;
                var first=new Tetrahedron(a,b,c,a+n,points);var second=new Tetrahedron(b,c,a+n,b+n,points);var third=new Tetrahedron(c,a+n,b+n,c+n,points);
                tetrahedra[tetra++]=first;tetrahedra[tetra++]=second;tetrahedra[tetra++]=third;volume+=first.Volume+second.Volume+third.Volume;
            }
            for(int z=0;z<bed.cells;z++)for(int x=0;x<bed.cells;x++){int a=z*(bed.cells+1)+x,b=a+1,c=a+bed.cells+1,d=c+1;Add(a,c,b);Add(b,c,d);}
            if(!double.IsFinite(volume)||volume<=1e-12)throw new ArgumentException("The terrain leaves no liquid capacity below its opening");Volume=volume;
        }
        internal static double Capacity(RoomHeightField field,float ceiling){
            double volume=0;
            double Part(Vector3 a,Vector3 b,Vector3 c)=>Math.Abs(((double)b.x-a.x)*((double)c.z-a.z)-((double)b.z-a.z)*((double)c.x-a.x))*(3.0*ceiling-a.y-b.y-c.y)/6;
            for(int z=0;z<field.cells;z++)for(int x=0;x<field.cells;x++){
                var a=field.Vertex(x,z);var b=field.Vertex(x+1,z);var c=field.Vertex(x,z+1);var d=field.Vertex(x+1,z+1);volume+=Part(a,c,b)+Part(b,c,d);
            }
            return volume;
        }
        static double VolumeOf(Vector3 a,Vector3 b,Vector3 c,Vector3 d){
            double ax=(double)b.x-a.x,ay=(double)b.y-a.y,az=(double)b.z-a.z;
            double bx=(double)c.x-a.x,by=(double)c.y-a.y,bz=(double)c.z-a.z;
            double cx=(double)d.x-a.x,cy=(double)d.y-a.y,cz=(double)d.z-a.z;
            return Math.Abs(ax*(by*cz-bz*cy)-ay*(bx*cz-bz*cx)+az*(bx*cy-by*cx))/6;
        }
        void Project(Vector3 normal){
            if(!RoomRecipe.Finite(normal)||normal.sqrMagnitude<1e-12f)throw new ArgumentException("A known plane normal is required");
            if(projected&&normal.Equals(projectedNormal))return;
            projected=true;projectedNormal=normal;levelCached=false;minimum=double.PositiveInfinity;maximum=double.NegativeInfinity;
            for(int i=0;i<points.Length;i++){var p=points[i];double value=(double)p.x*normal.x+(double)p.y*normal.y+(double)p.z*normal.z;projection[i]=value;minimum=Math.Min(minimum,value);maximum=Math.Max(maximum,value);}
        }
        // Clipping a tetrahedron needs at most three smaller tetrahedra. Products
        // of edge fractions avoid divided differences at repeated vertex heights.
        internal static double ClippedFraction(double a,double b,double c,double d){
            Span<double> distance=stackalloc double[]{a,b,c,d};int count=0,first=-1,second=-1,outside=-1,otherOutside=-1;
            for(int i=0;i<4;i++)if(distance[i]>=0){count++;if(first<0)first=i;else second=i;}else if(outside<0)outside=i;else otherOutside=i;
            if(count==0)return 0;if(count==4)return 1;
            if(count==1){double value=1;for(int i=0;i<4;i++)if(i!=first)value*=distance[first]/(distance[first]-distance[i]);return value;}
            if(count==3){double value=1;for(int i=0;i<4;i++)if(i!=outside)value*=-distance[outside]/(distance[i]-distance[outside]);return 1-value;}
            double p=distance[first]/(distance[first]-distance[outside]),q=distance[first]/(distance[first]-distance[otherOutside]);
            double r=distance[second]/(distance[second]-distance[outside]),s=distance[second]/(distance[second]-distance[otherOutside]);
            return Math.Clamp(p*q+q*r*(1-p)+r*s*(1-q),0,1);
        }
        double Below(double level){
            double volume=0;foreach(var t in tetrahedra)if(t.Volume>0)volume+=t.Volume*ClippedFraction(level-projection[t.A],level-projection[t.B],level-projection[t.C],level-projection[t.D]);
            return Math.Clamp(volume,0,Volume);
        }
        internal double VolumeBelow(Vector3 normal,double level){Project(normal);return Below(level);}
        internal float Level(Vector3 normal,double fraction){
            if(!double.IsFinite(fraction)||fraction<0||fraction>1)throw new ArgumentException("Use a finite fill fraction between zero and one");
            Project(normal);if(levelCached&&fraction==lastFraction)return lastLevel;
            double lo=minimum,hi=maximum,wanted=Volume*fraction;
            if(fraction==0)hi=lo;else if(fraction==1)lo=hi;
            else for(int i=0;i<24;i++){double middle=(lo+hi)*.5;if(Below(middle)<wanted)lo=middle;else hi=middle;}
            lastFraction=fraction;lastLevel=(float)((lo+hi)*.5);levelCached=true;return lastLevel;
        }
        internal float MaximumBedHeight(float minX,float maxX,float minZ,float maxZ){
            int x0=Mathf.Clamp(Mathf.FloorToInt((minX/bed.width+.5f)*bed.cells),0,bed.cells-1),x1=Mathf.Clamp(Mathf.CeilToInt((maxX/bed.width+.5f)*bed.cells),1,bed.cells);
            int z0=Mathf.Clamp(Mathf.FloorToInt((minZ/bed.depth+.5f)*bed.cells),0,bed.cells-1),z1=Mathf.Clamp(Mathf.CeilToInt((maxZ/bed.depth+.5f)*bed.cells),1,bed.cells);
            float value=0;for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)value=Mathf.Max(value,bed.heights[z*(bed.cells+1)+x]);return value;
        }
        double TriangleBelow(int first,double level){double volume=0;for(int i=first;i<first+3;i++){var t=tetrahedra[i];if(t.Volume>0)volume+=t.Volume*ClippedFraction(level-projection[t.A],level-projection[t.B],level-projection[t.C],level-projection[t.D]);}return volume;}
        internal bool Sweep(Matrix4x4 inverse,Vector3 from,Vector3 to,float radius,float height,Vector3 localNormal,float localLevel,Vector3 up,float level,out float depth,out Vector3 foot){
            depth=0;foot=default;Project(localNormal);var start=inverse.MultiplyPoint3x4(from);var change=inverse.MultiplyVector(to-from);var normals=inverse.transpose;
            bool Plane(Vector3 normal,float limit,ref float enter,ref float exit)=>LiquidMediumGeometry.Clip(Vector3.Dot(normal,start),Vector3.Dot(normal,change),limit+LiquidMediumGeometry.Support(normals.MultiplyVector(normal),radius,height),ref enter,ref exit);
            for(int i=0;i<triangles.Length;i+=3){
                if(TriangleBelow(i,localLevel)<=1e-15)continue;
                var a=points[triangles[i]];var b=points[triangles[i+1]];var c=points[triangles[i+2]];float enter=0,exit=1;
                if(!Plane(Vector3.up,Ceiling,ref enter,ref exit))continue;
                var floor=-Vector3.Cross(b-a,c-a).normalized;if(!Plane(floor,Vector3.Dot(floor,a),ref enter,ref exit))continue;
                var side=Vector3.Cross(b-a,Vector3.up).normalized;if(!Plane(side,Vector3.Dot(side,a),ref enter,ref exit))continue;
                side=Vector3.Cross(c-b,Vector3.up).normalized;if(!Plane(side,Vector3.Dot(side,b),ref enter,ref exit))continue;
                side=Vector3.Cross(a-c,Vector3.up).normalized;if(!Plane(side,Vector3.Dot(side,c),ref enter,ref exit))continue;
                if(!LiquidMediumGeometry.Clip(Vector3.Dot(up,from),Vector3.Dot(up,to-from),level+LiquidMediumGeometry.Support(up,radius,height),ref enter,ref exit))continue;
                var at=Vector3.Lerp(from,to,Vector3.Dot(up,to-from)<0?exit:enter);float current=Mathf.Max(0,level-Vector3.Dot(up,at));
                if(current>depth){depth=current;foot=at;}
            }
            return depth>.0001f;
        }
        internal bool Contains(Vector3 local)=>Mathf.Abs(local.x)<=bed.width*.5f&&Mathf.Abs(local.z)<=bed.depth*.5f&&local.y<=Ceiling&&local.y>=bed.HeightAt(new Vector2(local.x,local.z));
        // Surface polygons are intersections with each actual terrain column,
        // rather than an uncut rectangle covering dry hills and shores.
        internal void Surface(Vector3 normal,float level,List<Vector3> vertices,List<int> indices){
            Project(normal);vertices.Clear();indices.Clear();int n=bed.heights.Length;
            var axis=Vector3.Cross(normal,Mathf.Abs(normal.normalized.y)<.9f?Vector3.up:Vector3.right).normalized;var other=Vector3.Cross(normal.normalized,axis);
            Span<float> angles=stackalloc float[6];
            for(int t=0;t<triangles.Length;t+=3){
                if(TriangleBelow(t,level)<=1e-15)continue;
                int a=triangles[t],b=triangles[t+1],c=triangles[t+2];polygon.Clear();
                Edge(a,b,level);Edge(b,c,level);Edge(c,a,level);Edge(a+n,b+n,level);Edge(b+n,c+n,level);Edge(c+n,a+n,level);Edge(a,a+n,level);Edge(b,b+n,level);Edge(c,c+n,level);
                if(polygon.Count<3)continue;var centre=Vector3.zero;foreach(var v in polygon)centre+=v;centre/=polygon.Count;
                for(int i=0;i<polygon.Count;i++)angles[i]=Mathf.Atan2(Vector3.Dot(polygon[i]-centre,other),Vector3.Dot(polygon[i]-centre,axis));
                for(int i=1;i<polygon.Count;i++){int j=i;while(j>0&&angles[j]<angles[j-1]){(angles[j],angles[j-1])=(angles[j-1],angles[j]);(polygon[j],polygon[j-1])=(polygon[j-1],polygon[j]);j--;}}
                int start=vertices.Count;vertices.AddRange(polygon);for(int i=1;i<polygon.Count-1;i++){indices.Add(start);indices.Add(start+i);indices.Add(start+i+1);}
            }
        }
        void Edge(int a,int b,float level){
            double da=projection[a]-level,db=projection[b]-level;if(da*db>0)return;
            if(Math.Abs(da-db)<1e-12){if(Math.Abs(da)<1e-9){Point(points[a]);Point(points[b]);}return;}
            Point(Vector3.LerpUnclamped(points[a],points[b],(float)(da/(da-db))));
        }
        void Point(Vector3 point){foreach(var old in polygon)if((old-point).sqrMagnitude<1e-14f)return;polygon.Add(point);}
    }
}
