// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Art;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    /// <summary>Native ownership of bounded Unity point/spot lights. Reads never create or advance them.</summary>
    public sealed class RoomLightView:MonoBehaviour,INativeResourceOwner
    {
        sealed class Entry {internal RoomLight Data;internal Light Light;internal string Reason="Waiting for the world frame";}
        readonly List<Entry> entries=new();static readonly HashSet<RoomLightView> activeViews=new();
        RoomEditor editor;string target,encoded;bool released;
        internal bool Active(string id)=>entries.Any(e=>e.Data.id==id&&e.Light&&e.Light.isActiveAndEnabled);
        internal string Reason(string id)=>entries.FirstOrDefault(e=>e.Data.id==id)?.Reason??"The native lamp is unavailable";
        internal void Apply(RoomEditor owner,string identity,RoomLight[] definitions){
            editor=owner;target=identity;string next=string.Join("|",definitions.Select(l=>JsonUtility.ToJson(l)));
            if(encoded!=next||entries.Any(e=>!e.Light)){
                Clear();encoded=next;released=false;
                foreach(var definition in definitions){var go=new GameObject("Lamp "+definition.id);go.transform.SetParent(transform,false);var light=go.AddComponent<Light>();light.enabled=false;
                    light.type=definition.kind=="point"?LightType.Point:LightType.Spot;ColorUtility.TryParseHtmlString(definition.color,out var color);light.color=color;light.intensity=definition.intensity;
                    light.spotAngle=definition.angle;light.innerSpotAngle=definition.angle*.75f;light.renderMode=LightRenderMode.ForcePixel;light.shadows=definition.shadows=="hard"?LightShadows.Hard:LightShadows.None;
                    light.shadowCustomResolution=512;light.shadowBias=.01f;light.shadowNormalBias=.03f;light.bounceIntensity=0;light.cullingMask=~(1<<Maestro.Quest.Interaction.RoomPhysicsLayers.Scanned);
                    entries.Add(new Entry{Data=definition.Copy(),Light=light});}
            }
            Refresh();
        }
        internal void Refresh(){
            bool any=false;var recipe=GetComponent<RecipeObject>();
            foreach(var e in entries){
                if(!e.Light)continue;var anchor=e.Data.part==""?transform:recipe?recipe.Part(e.Data.part):null;
                var state=editor?editor.LightState(target,e.Data.id):null;
                bool ready=isActiveAndEnabled&&editor&&editor.isActiveAndEnabled&&!editor.Ownership.Suspended&&!editor.RuntimeGate.Held&&editor.Frame.Valid&&state!=null&&anchor&&anchor.gameObject.activeInHierarchy;
                bool emitting=state!=null&&state.Enabled&&state.Intensity>0;
                e.Reason=!emitting?"Lamp is switched off":!ready?"Waiting for an active object, attachment and valid world frame":"";
                e.Light.enabled=ready&&emitting;any|=e.Light.enabled;
                if(state!=null){e.Light.color=state.Pigment;e.Light.intensity=state.Intensity;}
                if(!ready)continue;
                e.Light.transform.SetPositionAndRotation(anchor.TransformPoint(e.Data.position),anchor.rotation*e.Data.rotation);
                e.Light.range=e.Data.range*Mathf.Abs(anchor.lossyScale.x);
            }
            if(any)activeViews.Add(this);else activeViews.Remove(this);Publish();
        }
        static void Publish()=>Shader.SetGlobalFloat("_MaestroLocalLightingEnabled",activeViews.Count>0?1:0);
        void LateUpdate()=>Refresh();
        void OnEnable(){if(editor)Refresh();}
        void OnDisable(){foreach(var e in entries)if(e.Light)e.Light.enabled=false;activeViews.Remove(this);Publish();}
        void Clear(){foreach(var e in entries)if(e.Light){e.Light.enabled=false;ArtResources.Release(e.Light.gameObject);}entries.Clear();activeViews.Remove(this);Publish();}
        void OnDestroy()=>ReleaseNativeResources();
        void INativeResourceOwner.ReleaseNativeResources()=>ReleaseNativeResources();
        void ReleaseNativeResources(){if(released)return;released=true;Clear();}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){activeViews.Clear();Publish();}
    }
}
