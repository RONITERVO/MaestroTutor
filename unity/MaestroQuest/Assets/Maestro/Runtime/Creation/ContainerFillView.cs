// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Art;
using UnityEngine;
namespace Maestro.Quest.Creation {
    /// <summary>Bounded, upward-facing liquid-level presentation; never mutates quantity or physics.</summary>
    public sealed class ContainerFillView:MonoBehaviour {
        const int Segments=24;
        readonly List<Vector3> rim=new(72),vertices=new(74);
        readonly List<int> triangles=new(216);
        readonly List<Vector3> normals=new(74);
        readonly Vector3[] bottom=new Vector3[Segments],top=new Vector3[Segments];
        RoomContainer data;GameObject surface;Mesh mesh;Material material;MeshRenderer renderer;
        Quaternion lastRotation;float nextRefresh;bool dirty;
        public void Apply(RoomContainer[] containers){
            data=containers?.Length==1?containers[0].Copy():null;dirty=true;
            if(data==null||data.amountMl<=0){if(surface)surface.SetActive(false);return;}
            if(!surface){surface=new GameObject("Measured liquid surface");surface.transform.SetParent(transform,false);mesh=new Mesh{name="Bounded liquid level"};mesh.MarkDynamic();surface.AddComponent<MeshFilter>().sharedMesh=mesh;renderer=surface.AddComponent<MeshRenderer>();material=IllustratedMaterials.Create(data.color,0);renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;}
            surface.SetActive(true);surface.transform.SetLocalPositionAndRotation(data.frame.position+data.frame.rotation*(Vector3.up*data.height*.5f),data.frame.rotation);material.SetColor("_Color",data.color);
            Refresh();
        }
        void LateUpdate(){if(surface&&surface.activeSelf&&(dirty||Time.unscaledTime>=nextRefresh&&Quaternion.Angle(lastRotation,surface.transform.rotation)>.25f))Refresh();}
        internal void Refresh(){
            if(data==null||!surface||data.amountMl<=0)return;
            nextRefresh=Time.unscaledTime+.1f;lastRotation=surface.transform.rotation;dirty=false;
            Vector3 normal=surface.transform.InverseTransformDirection(Vector3.up).normalized;
            float level=Level(normal,data.radius,data.height,(float)(data.amountMl/data.capacityMl));
            for(int i=0;i<Segments;i++){float angle=i*Mathf.PI*2/Segments;bottom[i]=new Vector3(Mathf.Cos(angle)*data.radius,-data.height*.5f,Mathf.Sin(angle)*data.radius);top[i]=bottom[i]+Vector3.up*data.height;}
            float minimum=float.PositiveInfinity,maximum=float.NegativeInfinity;for(int i=0;i<Segments;i++){minimum=Mathf.Min(minimum,Mathf.Min(Vector3.Dot(normal,bottom[i]),Vector3.Dot(normal,top[i])));maximum=Mathf.Max(maximum,Mathf.Max(Vector3.Dot(normal,bottom[i]),Vector3.Dot(normal,top[i])));}
            float inset=(maximum-minimum)*.000001f;level=Mathf.Clamp(level,minimum+inset,maximum-inset);
            rim.Clear();for(int i=0;i<Segments;i++){int j=(i+1)%Segments;Edge(bottom[i],top[i],normal,level);Edge(bottom[i],bottom[j],normal,level);Edge(top[i],top[j],normal,level);}
            Vector3 axis=Vector3.Cross(normal,Mathf.Abs(normal.y)<.9f?Vector3.up:Vector3.right).normalized,other=Vector3.Cross(normal,axis),centre=Vector3.zero;
            foreach(var point in rim)centre+=point;if(rim.Count>0)centre/=rim.Count;
            rim.Sort((a,b)=>Mathf.Atan2(Vector3.Dot(a-centre,other),Vector3.Dot(a-centre,axis)).CompareTo(Mathf.Atan2(Vector3.Dot(b-centre,other),Vector3.Dot(b-centre,axis))));
            vertices.Clear();triangles.Clear();if(rim.Count>=3){Vector3 middle=Vector3.zero;foreach(var p in rim)middle+=p;vertices.Add(middle/rim.Count);vertices.AddRange(rim);for(int i=0;i<rim.Count;i++){int a=i+1,b=(i+1)%rim.Count+1;triangles.Add(0);triangles.Add(a);triangles.Add(b);}}
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);normals.Clear();for(int i=0;i<vertices.Count;i++)normals.Add(normal);mesh.SetNormals(normals);mesh.RecalculateBounds();
        }
        void Edge(Vector3 a,Vector3 b,Vector3 normal,float level){float da=Vector3.Dot(normal,a)-level,db=Vector3.Dot(normal,b)-level;if(da*db>0||Mathf.Abs(da-db)<1e-8f)return;var point=Vector3.LerpUnclamped(a,b,da/(da-db));foreach(var p in rim)if((p-point).sqrMagnitude<1e-12f)return;rim.Add(point);}
        // Integrate the circular cross-section CDF along the cylinder axis. This is
        // only a display plane; saved millilitres and transfer arithmetic never depend on it.
        internal static float Level(Vector3 normal,float radius,float height,float fraction){
            double a=radius*System.Math.Sqrt(normal.x*normal.x+normal.z*normal.z),b=height*.5*System.Math.Abs(normal.y);
            double low=-a-b,high=a+b;
            for(int pass=0;pass<18;pass++){double mid=(low+high)*.5,cdf;
                if(a<1e-8)cdf=(mid+b)/(2*b);
                else if(b<1e-8)cdf=Disk(mid/a);
                else cdf=a*(Integral((mid+b)/a)-Integral((mid-b)/a))/(2*b);
                if(cdf<fraction)low=mid;else high=mid;
            }
            return (float)((low+high)*.5);
        }
        static double Disk(double x){if(x<=-1)return 0;if(x>=1)return 1;return .5+(System.Math.Asin(x)+x*System.Math.Sqrt(1-x*x))/System.Math.PI;}
        static double Integral(double x){if(x<=-1)return 0;if(x>=1)return x;double root=System.Math.Sqrt(1-x*x);return x*.5+(x*System.Math.Asin(x)+root-root*root*root/3)/System.Math.PI;}
        void OnDestroy(){ArtResources.Release(mesh);ArtResources.Release(material);if(surface)Destroy(surface);}
    }
}
