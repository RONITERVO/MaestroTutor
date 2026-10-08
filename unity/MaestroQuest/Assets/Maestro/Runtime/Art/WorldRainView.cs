// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Art
{
    /// <summary>Bounded local rain presentation. Liquid input never depends on these streaks or visibility.</summary>
    public sealed class WorldRainView:MonoBehaviour
    {
        internal const int MaximumDrops=64,QueriesPerTick=16;
        readonly Vector3[] positions=new Vector3[MaximumDrops],vertices=new Vector3[MaximumDrops*4],normals=new Vector3[MaximumDrops*4];
        readonly bool[] alive=new bool[MaximumDrops];readonly float[] sampled=new float[MaximumDrops];
        readonly WeatherCover cover=new();RoomEditor editor;Transform observer;Mesh mesh;MeshRenderer view;Material material;
        uint random;int revision=-1,cursor;float next;bool paused,focused=true;
        internal int VisibleDrops{get;private set;}
        internal void Initialize(RoomEditor owner,Transform viewer=null){
            editor=owner;observer=viewer?viewer:owner.Viewer;
            var go=new GameObject("Local rain");go.transform.SetParent(transform,false);mesh=new Mesh{name="Bounded rain streaks"};mesh.MarkDynamic();
            var filter=go.AddComponent<MeshFilter>();filter.sharedMesh=mesh;view=go.AddComponent<MeshRenderer>();
            material=IllustratedMaterials.Create(new Color(.55f,.7f,.9f,1),0);view.sharedMaterial=material;
            view.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;view.receiveShadows=false;
            var colors=new Color[vertices.Length];var indices=new int[MaximumDrops*6];
            for(int i=0;i<colors.Length;i++){colors[i]=Color.white;normals[i]=Vector3.back;}
            for(int i=0;i<MaximumDrops;i++){int v=i*4,k=i*6;indices[k]=v;indices[k+1]=v+1;indices[k+2]=v+2;indices[k+3]=v;indices[k+4]=v+2;indices[k+5]=v+3;}
            mesh.vertices=vertices;mesh.normals=normals;mesh.colors=colors;mesh.triangles=indices;view.enabled=false;
        }
        float Next(){random^=random<<13;random^=random>>17;random^=random<<5;return (random&0xffffff)/16777216f;}
        void LateUpdate(){float now=Time.unscaledTime;if(now<next)return;next=now+.05f;Sample(now);}
        internal void Sample(float now){
            if(!editor||!observer||!view)return;
            var frame=editor.Frame;var weather=editor.CurrentWeather;var world=editor.PhysicsWorld;
            if(paused||!focused||!frame.Valid||editor.Ownership.Suspended||editor.RuntimeGate.Held||!world||weather.Rain<=0){Hide();return;}
            if(revision!=editor.WeatherRevision){revision=editor.WeatherRevision;random=(uint)editor.Weather.seed;Hide();}
            bool real=world.IncludesRealRoom(null);if(real&&!world.SurfacesReady){Hide();return;}
            int count=Mathf.Clamp(Mathf.CeilToInt(weather.Rain/120*MaximumDrops),1,MaximumDrops);
            var velocity=WeatherCover.Velocity(editor);var centre=observer.position;var up=frame.DirectionToWorld(Vector3.up);var right=frame.DirectionToWorld(Vector3.right);var forward=frame.DirectionToWorld(Vector3.forward);
            Physics.SyncTransforms();
            for(int n=0;n<QueriesPerTick;n++){
                int i=cursor%count;cursor=(i+1)%count;
                if(!alive[i]||now-sampled[i]>.4f||Vector3.Distance(positions[i],centre)>8){
                    positions[i]=centre+right*((Next()-.5f)*8)+forward*((Next()-.5f)*8)+up*(2+Next()*3);sampled[i]=now;
                    alive[i]=cover.Exposure(editor,positions[i],velocity)==RainExposure.Open;
                }else{
                    var end=positions[i]+velocity*Mathf.Clamp(now-sampled[i],0,.4f);
                    alive[i]=Vector3.Dot(end-centre,up)>-3&&cover.Segment(editor,positions[i],end,real)==RainExposure.Open;
                    positions[i]=end;sampled[i]=now;
                }
            }
            VisibleDrops=0;
            for(int i=0;i<MaximumDrops;i++){
                int v=i*4;if(i>=count||!alive[i]){for(int j=0;j<4;j++)vertices[v+j]=Vector3.zero;continue;}
                VisibleDrops++;
                var tail=positions[i]-velocity*.018f;var side=Vector3.Cross(velocity.normalized,(observer.position-positions[i]).normalized).normalized*.003f;
                vertices[v]=transform.InverseTransformPoint(positions[i]-side);vertices[v+1]=transform.InverseTransformPoint(positions[i]+side);vertices[v+2]=transform.InverseTransformPoint(tail+side);vertices[v+3]=transform.InverseTransformPoint(tail-side);
            }
            mesh.vertices=vertices;mesh.RecalculateBounds();view.enabled=VisibleDrops>0;
        }
        void Hide(){VisibleDrops=0;System.Array.Clear(alive,0,alive.Length);if(view)view.enabled=false;}
        void OnApplicationPause(bool value){paused=value;if(value)Hide();}
        void OnApplicationFocus(bool value){focused=value;if(!value)Hide();}
        void OnDisable()=>Hide();
        void OnDestroy(){if(view)Destroy(view.gameObject);ArtResources.Release(mesh);ArtResources.Release(material);}
    }
}
