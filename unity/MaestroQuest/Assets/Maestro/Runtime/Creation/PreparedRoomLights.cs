// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // A private candidate owns only newly allocated lamps. Unchanged lamps stay
    // owned by the accepted view, including when a recipe replaces their anchor.
    internal sealed class PreparedRoomLights:IDisposable
    {
        internal sealed class Entry:IDisposable
        {
            internal RoomLight Data;
            internal LightResources.Lease Lease;
            internal Light Light=>Lease.Light;
            internal string Encoded,Reason="Waiting for the world frame";
            public void Dispose()=>Lease.Dispose();
        }
        readonly List<Entry> entries=new(),created=new();
        readonly string target,encoded;
        readonly RoomLightView previous;
        bool ended;
        internal static string Encode(RoomLight[] definitions)=>string.Join("|",definitions.Select(l=>JsonUtility.ToJson(l)));
        internal PreparedRoomLights(RoomWorldIdentity world,string identity,RoomLight[] definitions,RoomLightView view){
            target=identity;encoded=Encode(definitions);previous=view;
            try{
                foreach(var definition in definitions){
                    var reuse=view?view.Reusable(definition):null;
                    if(reuse!=null){entries.Add(reuse);continue;}
                    var entry=new Entry{Data=definition.Copy(),Encoded=JsonUtility.ToJson(definition),Lease=LightResources.Begin(definition,new RoomResourceOwner(world,identity,"lighting"))};
                    created.Add(entry);entries.Add(entry);
                    var go=new GameObject("Lamp "+definition.id);entry.Lease.Bind(go);go.SetActive(false);
                    var light=go.AddComponent<Light>();light.enabled=false;
                    light.type=definition.kind=="point"?LightType.Point:LightType.Spot;ColorUtility.TryParseHtmlString(definition.color,out var color);light.color=color;light.intensity=definition.intensity;
                    light.spotAngle=definition.angle;light.innerSpotAngle=definition.angle*.75f;light.renderMode=LightRenderMode.ForcePixel;light.shadows=definition.shadows=="hard"?LightShadows.Hard:LightShadows.None;
                    light.shadowCustomResolution=512;light.shadowBias=.01f;light.shadowNormalBias=.03f;light.bounceIntensity=0;light.cullingMask=~(1<<Maestro.Quest.Interaction.RoomPhysicsLayers.Scanned);
                    entry.Lease.Complete(light);
                }
            }catch{Dispose();throw;}
        }
        internal void Validate(string identity,RoomLight[] definitions){
            if(ended||target!=identity||encoded!=Encode(definitions)||entries.Any(e=>!e.Light))throw new InvalidOperationException("Light source changed or preparation has ended");
        }
        internal List<Entry> Take(RoomLightView view,string identity,RoomLight[] definitions){
            Validate(identity,definitions);
            if(previous&&previous!=view)throw new InvalidOperationException("Light owner changed after preparation");
            foreach(var entry in created)entry.Lease.Ready();
            ended=true;created.Clear();return entries;
        }
        public void Dispose(){if(ended)return;ended=true;foreach(var entry in created)entry.Dispose();created.Clear();entries.Clear();}
    }
}
