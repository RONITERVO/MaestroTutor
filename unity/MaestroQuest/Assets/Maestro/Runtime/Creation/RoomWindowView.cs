// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    /// <summary>Owns admitted masks; saved source is shared by humans, programs and agents.</summary>
    public sealed class RoomWindowView:MonoBehaviour,INativeResourceOwner
    {
        readonly List<PreparedRoomWindows.Mask> masks=new();
        PreparedRoomWindows.Bundle bundle;
        VisibilityState visibility;VirtualRoomView view;
        internal PreparedRoomWindows.Bundle ReusableBundle=>bundle!=null&&bundle.Live?bundle:null;
        internal PreparedRoomWindows.Mask Reusable(string id,PreparedRoomWindows.Bundle geometry)=>geometry==bundle?masks.FirstOrDefault(m=>m.Data.id==id&&m.Live(geometry)):null;
        internal bool Requested {get;private set;}
        internal bool RenderingReady=>Requested&&view&&view.WindowRenderingReady;
        internal string UnavailableReason=>!Requested?"No visible opening is requested":!view?"The physical viewing service is unavailable":view.WindowRenderingReady?"":"Waiting for active headset passthrough";
        internal static bool IsMask(Renderer renderer)=>renderer&&renderer.sharedMaterial&&renderer.sharedMaterial.shader&&renderer.sharedMaterial.shader.name=="Maestro/PassthroughWindow";
        internal void ConfigureVisibility(VisibilityState state){visibility=state;Refresh();}
        internal void Apply(RoomObjectData owner,VirtualRoomView presentation,VisibilityState state,PreparedRoomWindows prepared){
            var changes=prepared.Take(this,owner);var retained=changes.Select(c=>c.Mask).ToHashSet();
            foreach(var mask in masks)if(!retained.Contains(mask))mask.Dispose();masks.Clear();
            if(bundle!=prepared.Geometry)bundle?.Dispose();bundle=prepared.Geometry;
            if(view&&view!=presentation)view.SetWindowRequest(this,false);view=presentation;visibility=state;
            foreach(var change in changes){
                var mask=change.Mask;mask.Data=change.Data;
                mask.Root.transform.SetParent(change.Anchor,false);
                // Plane front is -Z. Keep the opening behind its ink.
                mask.Root.transform.SetLocalPositionAndRotation(new Vector3(0,0,.001f),Quaternion.identity);mask.Root.transform.localScale=change.Scale;mask.Root.SetActive(true);masks.Add(mask);
            }
            Refresh();
        }
        internal void DetachAnchors(){foreach(var mask in masks)if(mask.Root)mask.Root.transform.SetParent(transform,true);}
        internal void Refresh(){
            Requested=false;
            if(isActiveAndEnabled)foreach(var mask in masks)if(mask.Live(bundle)&&mask.Root.activeInHierarchy&&mask.Data.reveal*(visibility?.Opacity??1)>0){Requested=true;break;}
            if(view)view.SetWindowRequest(this,Requested);
            foreach(var mask in masks){if(!mask.Renderer)continue;mask.Renderer.enabled=RenderingReady&&mask.Live(bundle);mask.Properties.SetFloat("_Reveal",mask.Data.reveal*(visibility?.Opacity??1));mask.Properties.SetFloat("_Ellipse",mask.Data.shape=="ellipse"?1:0);mask.Renderer.SetPropertyBlock(mask.Properties);}
        }
        void LateUpdate()=>Refresh();
        void OnEnable()=>Refresh();
        void OnDisable(){Requested=false;if(view)view.SetWindowRequest(this,false);foreach(var mask in masks)if(mask.Renderer)mask.Renderer.enabled=false;}
        void OnDestroy()=>ReleaseNativeResources();
        void INativeResourceOwner.ReleaseNativeResources()=>ReleaseNativeResources();
        void ReleaseNativeResources(){Requested=false;if(view)view.SetWindowRequest(this,false);foreach(var mask in masks)mask.Dispose();masks.Clear();bundle?.Dispose();bundle=null;}
    }
}
