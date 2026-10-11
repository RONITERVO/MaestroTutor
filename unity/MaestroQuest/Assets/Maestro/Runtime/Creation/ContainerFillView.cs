// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Art;
using UnityEngine;
namespace Maestro.Quest.Creation {
    /// <summary>Bounded, upward-facing liquid-level presentation; never mutates quantity or physics.</summary>
    public sealed partial class ContainerFillView:MonoBehaviour, Maestro.Quest.Art.INativeResourceOwner {
        const int Segments=24;
        readonly List<Vector3> rim=new(72),vertices=new(74);
        readonly List<int> triangles=new(216);
        readonly List<Vector3> normals=new(74);
        readonly Vector3[] bottom=new Vector3[Segments],top=new Vector3[Segments];
        RoomContainer data;GameObject surface;Mesh mesh;Material material;MeshRenderer renderer;
        Quaternion lastRotation;Vector3 lastUp;float nextRefresh;bool dirty;
        public void Apply(RoomContainer[] containers){
            data=containers?.Length==1?containers[0].Copy():null;dirty=true;
            if(data==null||data.amountMl<=0){ClearRipples();if(surface)surface.SetActive(false);return;}
            if(!surface){surface=new GameObject("Measured liquid surface");surface.transform.SetParent(transform,false);mesh=new Mesh{name="Bounded liquid level"};mesh.MarkDynamic();surface.AddComponent<MeshFilter>().sharedMesh=mesh;renderer=surface.AddComponent<MeshRenderer>();material=IllustratedMaterials.Create(data.color,0);renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;}
            surface.SetActive(true);surface.transform.SetLocalPositionAndRotation(data.frame.position+data.frame.rotation*(Vector3.up*data.height*.5f),data.frame.rotation);material.SetColor("_Color",data.color);
            Refresh();RoomAppearanceView.VisualsChanged(this);
        }
        static Vector3 GravityUp=>float.IsFinite(Physics.gravity.sqrMagnitude)&&Physics.gravity.sqrMagnitude>.01f?-Physics.gravity.normalized:Vector3.up;
        void LateUpdate(){if(surface&&surface.activeSelf&&(dirty||Time.unscaledTime>=nextRefresh&&(Quaternion.Angle(lastRotation,surface.transform.rotation)>.25f||Vector3.Angle(lastUp,GravityUp)>.25f)))Refresh();UpdateRipples();}
        internal void Refresh(){
            if(data==null||!surface||data.amountMl<=0)return;
            nextRefresh=Time.unscaledTime+.1f;lastRotation=surface.transform.rotation;dirty=false;
            lastUp=GravityUp;Vector3 normal=surface.transform.InverseTransformDirection(lastUp).normalized;
            float level=ContainerFlowGeometry.Level(data,normal,data.amountMl/data.capacityMl);
            int count=data.IsRectangular?4:Segments;
            for(int i=0;i<count;i++){
                if(data.IsRectangular)bottom[i]=new Vector3((i==0||i==3?-1:1)*data.rectangle.width*.5f,-data.height*.5f,(i<2?-1:1)*data.rectangle.depth*.5f);
                else {float angle=i*Mathf.PI*2/Segments;bottom[i]=new Vector3(Mathf.Cos(angle)*data.radius,-data.height*.5f,Mathf.Sin(angle)*data.radius);}
                top[i]=bottom[i]+Vector3.up*data.height;
            }
            float minimum=float.PositiveInfinity,maximum=float.NegativeInfinity;for(int i=0;i<count;i++){minimum=Mathf.Min(minimum,Mathf.Min(Vector3.Dot(normal,bottom[i]),Vector3.Dot(normal,top[i])));maximum=Mathf.Max(maximum,Mathf.Max(Vector3.Dot(normal,bottom[i]),Vector3.Dot(normal,top[i])));}
            float inset=(maximum-minimum)*.000001f;level=Mathf.Clamp(level,minimum+inset,maximum-inset);
            rim.Clear();for(int i=0;i<count;i++){int j=(i+1)%count;Edge(bottom[i],top[i],normal,level);Edge(bottom[i],bottom[j],normal,level);Edge(top[i],top[j],normal,level);}
            Vector3 axis=Vector3.Cross(normal,Mathf.Abs(normal.y)<.9f?Vector3.up:Vector3.right).normalized,other=Vector3.Cross(normal,axis),centre=Vector3.zero;
            foreach(var point in rim)centre+=point;if(rim.Count>0)centre/=rim.Count;
            rim.Sort((a,b)=>Mathf.Atan2(Vector3.Dot(a-centre,other),Vector3.Dot(a-centre,axis)).CompareTo(Mathf.Atan2(Vector3.Dot(b-centre,other),Vector3.Dot(b-centre,axis))));
            vertices.Clear();triangles.Clear();if(rim.Count>=3){Vector3 middle=Vector3.zero;foreach(var p in rim)middle+=p;vertices.Add(middle/rim.Count);vertices.AddRange(rim);for(int i=0;i<rim.Count;i++){int a=i+1,b=(i+1)%rim.Count+1;triangles.Add(0);triangles.Add(a);triangles.Add(b);}}
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);normals.Clear();for(int i=0;i<vertices.Count;i++)normals.Add(normal);mesh.SetNormals(normals);mesh.RecalculateBounds();
        }
        void Edge(Vector3 a,Vector3 b,Vector3 normal,float level){float da=Vector3.Dot(normal,a)-level,db=Vector3.Dot(normal,b)-level;if(da*db>0||Mathf.Abs(da-db)<1e-8f)return;var point=Vector3.LerpUnclamped(a,b,da/(da-db));foreach(var p in rim)if((p-point).sqrMagnitude<1e-12f)return;rim.Add(point);}
        // Kept as the existing display entry point; immersion uses the identical plane.
        internal static float Level(Vector3 normal,float radius,float height,float fraction)=>ContainerFlowGeometry.Level(normal,radius,height,fraction);

        bool nativeResourcesReleased;
        void OnDestroy()=>ReleaseNativeResources();
        void Maestro.Quest.Art.INativeResourceOwner.ReleaseNativeResources()=>ReleaseNativeResources();
        void ReleaseNativeResources(){if(nativeResourcesReleased)return;nativeResourcesReleased=true;ArtResources.Release(mesh);ArtResources.Release(material);if(surface)Destroy(surface);}
    }
}
