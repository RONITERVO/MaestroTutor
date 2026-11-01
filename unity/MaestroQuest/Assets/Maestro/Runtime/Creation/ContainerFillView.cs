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
        readonly List<Vector2> uvs=new(74);
        readonly Vector3[] bottom=new Vector3[Segments],top=new Vector3[Segments];
        RoomContainer data;GameObject surface;Mesh mesh;Material material;MeshRenderer renderer;
        LiquidResources.Lease reservation;string target="";
        internal LiquidResources.Lease ReusableReservation=>reservation!=null&&reservation.Available&&reservation.Phase=="ready"&&reservation.Host==this?reservation:null;
        internal bool NativeReady=>isActiveAndEnabled&&data!=null&&ReusableReservation!=null;
        internal string AppearancePart(Renderer value)=>ReusableReservation==null?null:
            value==reservation.Renderer?NativeAppearanceParts.LiquidSurface:
            value==reservation.Line?NativeAppearanceParts.LiquidStream:null;
        internal bool HasAppearancePart(string address)=>ReusableReservation!=null&&NativeAppearanceParts.ValidId(address);
        Quaternion lastRotation;Vector3 lastUp;float nextRefresh;bool dirty;
        // Standalone diagnostic construction uses the same bounded preparation.
        // Live quantity updates and normal reconciliation may never allocate here.
        public void Apply(RoomContainer[] containers){using var prepared=new PreparedRoomLiquid(null,target,containers,this);ApplyPrepared(target,containers,prepared);}
        internal void ApplyPrepared(string identity,RoomContainer[] containers,PreparedRoomLiquid prepared){
            var next=prepared.Take(this,identity,containers);
            if(next!=reservation){
                ClearRipples();reservation?.Dispose();reservation=next;
                surface=next?.Surface;mesh=next?.Mesh;material=next?.SurfaceMaterial;renderer=next?.Renderer;
                if(next!=null){next.Surface.transform.SetParent(transform,false);next.Stream.transform.SetParent(transform,false);next.Stream.SetActive(true);}
            }
            target=identity??"";ApplyLive(containers);
        }
        internal void ApplyLive(RoomContainer[] containers){
            data=containers?.Length==1?containers[0].Copy():null;dirty=true;
            if(ReusableReservation==null||data==null||data.amountMl<=0||data.TerrainFloor&&RoomTerrainReservoir.Native(data,transform)==null){ClearRipples();if(surface)surface.SetActive(false);return;}
            renderer.enabled=true;surface.SetActive(true);surface.transform.SetLocalPositionAndRotation(data.frame.position+data.frame.rotation*(Vector3.up*data.height*.5f),data.frame.rotation);material.SetColor("_Color",data.color);reservation.StreamMaterial.SetColor("_Color",data.color);
            Refresh();RoomAppearanceView.VisualsChanged(this);
        }
        internal void ShowStream(Vector3[] points,int count,double rate,Color color){
            if(!NativeReady||points==null||count<0||count>LiquidResources.StreamPoints||count>points.Length)return;
            var line=reservation.Line;
            if(reservation.StreamMaterial.GetColor("_Color")!=color){reservation.StreamMaterial.SetColor("_Color",color);RoomAppearanceView.VisualsChanged(this);}
            line.startColor=line.endColor=Color.white;line.startWidth=line.endWidth=Mathf.Clamp((float)System.Math.Sqrt(rate)*.0002f,.0015f,.008f);line.positionCount=count;for(int i=0;i<count;i++)line.SetPosition(i,points[i]);line.enabled=true;
        }
        internal void HideStream(){if(reservation?.Line)reservation.Line.enabled=false;}
        // This reusable renderer has no authored/rest geometry while owner-disabled.
        // Match the exact reservation owner, never an arbitrary disabled renderer.
        internal bool IsInactiveOwnedStream(Renderer value)=>value&&reservation!=null&&reservation.Host==this&&reservation.Line==value&&
            !value.enabled;
        static Vector3 GravityUp=>float.IsFinite(Physics.gravity.sqrMagnitude)&&Physics.gravity.sqrMagnitude>.01f?-Physics.gravity.normalized:Vector3.up;
        void LateUpdate(){if(surface&&surface.activeSelf&&(dirty||Time.unscaledTime>=nextRefresh&&(Quaternion.Angle(lastRotation,surface.transform.rotation)>.25f||Vector3.Angle(lastUp,GravityUp)>.25f)))Refresh();UpdateRipples();}
        internal void Refresh(){
            if(data==null||!surface||data.amountMl<=0)return;
            nextRefresh=Time.unscaledTime+.1f;lastRotation=surface.transform.rotation;dirty=false;
            lastUp=GravityUp;Vector3 normal=surface.transform.InverseTransformDirection(lastUp).normalized;
            var terrain=RoomTerrainReservoir.Native(data,transform);if(data.TerrainFloor&&terrain==null){surface.SetActive(false);ClearRipples();return;}
            float level=ContainerFlowGeometry.Level(data,normal,data.amountMl/data.capacityMl,terrain);
            if(terrain!=null){
                terrain.Surface(normal,level+normal.y*data.height*.5f,vertices,triangles);
                for(int i=0;i<vertices.Count;i++)vertices[i]-=Vector3.up*(data.height*.5f);
                PublishMesh(normal);return;
            }
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
            PublishMesh(normal);
        }
        void PublishMesh(Vector3 normal){
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);
            normals.Clear();uvs.Clear();
            // Map the gravity-level plane in the cavity's local frame. A tilted
            // vessel must not collapse one UV axis; terrain shores keep the same
            // footprint mapping as their measured level changes.
            var u=Vector3.ProjectOnPlane(Vector3.right,normal);
            if(u.sqrMagnitude<.000001f)u=Vector3.ProjectOnPlane(Vector3.forward,normal);
            u.Normalize();var v=Vector3.Cross(u,normal).normalized;
            var half=new Vector3(data.IsRectangular?data.rectangle.width*.5f:data.radius,data.height*.5f,data.IsRectangular?data.rectangle.depth*.5f:data.radius);
            float Span(Vector3 axis)=>2*(Mathf.Abs(axis.x)*half.x+Mathf.Abs(axis.y)*half.y+Mathf.Abs(axis.z)*half.z);
            float width=Span(u),depth=Span(v);
            foreach(var point in vertices){normals.Add(normal);uvs.Add(new Vector2(.5f+Vector3.Dot(point,u)/width,.5f+Vector3.Dot(point,v)/depth));}
            mesh.SetNormals(normals);mesh.SetUVs(0,uvs);mesh.RecalculateBounds();
        }
        void Edge(Vector3 a,Vector3 b,Vector3 normal,float level){float da=Vector3.Dot(normal,a)-level,db=Vector3.Dot(normal,b)-level;if(da*db>0||Mathf.Abs(da-db)<1e-8f)return;var point=Vector3.LerpUnclamped(a,b,da/(da-db));foreach(var p in rim)if((p-point).sqrMagnitude<1e-12f)return;rim.Add(point);}
        // Kept as the existing display entry point; immersion uses the identical plane.
        internal static float Level(Vector3 normal,float radius,float height,float fraction)=>ContainerFlowGeometry.Level(normal,radius,height,fraction);

        bool nativeResourcesReleased;
        void OnDestroy()=>ReleaseNativeResources();
        void Maestro.Quest.Art.INativeResourceOwner.ReleaseNativeResources()=>ReleaseNativeResources();
        void ReleaseNativeResources(){if(nativeResourcesReleased)return;nativeResourcesReleased=true;reservation?.Dispose();reservation=null;}
    }
}
