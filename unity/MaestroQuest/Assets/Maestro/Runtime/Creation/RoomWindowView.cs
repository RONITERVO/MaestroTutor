// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Interaction;
using UnityEngine;
using UnityEngine.Rendering;
namespace Maestro.Quest.Creation
{
    /// <summary>Owns bounded mask renderers; source is shared by humans, programs and agents.</summary>
    public sealed class RoomWindowView:MonoBehaviour
    {
        sealed class Mask { internal RoomWindow Data;internal GameObject Object;internal MeshRenderer Renderer; }
        readonly List<Mask> masks=new();
        MaterialPropertyBlock properties;
        Material material;Mesh mesh;string encoded;
        VisibilityState visibility;VirtualRoomView view;
        internal bool Requested {get;private set;}
        internal bool RenderingReady=>Requested&&view&&view.WindowRenderingReady;
        internal string UnavailableReason=>!Requested?"No visible opening is requested":!view?"The physical viewing service is unavailable":view.WindowRenderingReady?"":"Waiting for active headset passthrough";
        internal static bool IsMask(Renderer renderer)=>renderer&&renderer.sharedMaterial&&renderer.sharedMaterial.shader&&renderer.sharedMaterial.shader.name=="Maestro/PassthroughWindow";
        internal void ConfigureVisibility(VisibilityState state){visibility=state;Refresh();}
        internal void Apply(RoomObjectData owner,VirtualRoomView presentation) {
            var surfaces=GetComponent<DrawingSurfaceView>();
            string next=string.Join("|",owner.windows.Select(w=>JsonUtility.ToJson(w)));
            if(encoded==next&&view==presentation&&masks.Count==owner.windows.Length&&masks.All(m=>m.Object&&surfaces&&m.Object.transform.parent==surfaces.Surface(m.Data.surface)))return;
            ClearMasks();encoded=next;if(view&&view!=presentation)view.SetWindowRequest(this,false);view=presentation;
            foreach(var data in owner.windows){
                var surface=owner.surfaces.First(s=>s.id==data.surface);var anchor=surfaces?surfaces.Surface(data.surface):null;
                if(!anchor)continue;
                if(!material){var source=Resources.Load<Material>("PassthroughWindow");var shader=source?source.shader:Shader.Find("Maestro/PassthroughWindow");if(!shader)throw new System.InvalidOperationException("Missing passthrough window shader");material=source?new Material(source):new Material(shader);material.name="Room passthrough windows";material.enableInstancing=true;}
                if(!mesh){mesh=new Mesh{name="Passthrough plane"};mesh.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0),new Vector3(.5f,-.5f,0)};mesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();}
                var go=new GameObject("Passthrough "+data.id);go.transform.SetParent(anchor,false);
                // Plane front is -Z. Put the opening behind its ink, never in front.
                go.transform.localPosition=new Vector3(0,0,.001f);go.transform.localScale=new Vector3(surface.width,surface.height,1);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                masks.Add(new Mask{Data=data.Copy(),Object=go,Renderer=renderer});
            }
            Refresh();
        }
        internal void Refresh() {
            Requested=false;
            if(isActiveAndEnabled)foreach(var m in masks)if(m.Object&&m.Object.activeInHierarchy&&m.Data.reveal*(visibility?.Opacity??1)>0){Requested=true;break;}
            if(view)view.SetWindowRequest(this,Requested);
            foreach(var m in masks){if(!m.Renderer)continue;m.Renderer.enabled=RenderingReady;properties??=new MaterialPropertyBlock();properties.Clear();properties.SetFloat("_Reveal",m.Data.reveal*(visibility?.Opacity??1));properties.SetFloat("_Ellipse",m.Data.shape=="ellipse"?1:0);m.Renderer.SetPropertyBlock(properties);}
        }
        void LateUpdate()=>Refresh();
        void OnEnable()=>Refresh();
        void OnDisable(){Requested=false;if(view)view.SetWindowRequest(this,false);foreach(var m in masks)if(m.Renderer)m.Renderer.enabled=false;}
        void ClearMasks(){foreach(var m in masks)if(m.Object){m.Object.SetActive(false);ArtResources.Release(m.Object);}masks.Clear();}
        void OnDestroy(){if(view)view.SetWindowRequest(this,false);ClearMasks();ArtResources.Release(material);ArtResources.Release(mesh);}
    }
}
