// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Art;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // Independent logical admission for authored mask roots and the mesh/material
    // shared by one view's masks. Compositor and passthrough resources are excluded.
    internal static class WindowResources
    {
        internal const int MaximumMasks=32,MaximumBundles=32;
        internal sealed class Lease:IDisposable
        {
            internal readonly string Id=Guid.NewGuid().ToString("N"),Kind,Window;
            internal readonly RoomResourceOwner Owner;
            internal string Phase="preparing";
            readonly List<UnityEngine.Object> owned=new();
            UnityEngine.Object host;bool bound;
            internal Lease(string kind,string window,RoomResourceOwner owner){Kind=kind;Window=window;Owner=owner;}
            internal bool Live=>Phase is "preparing" or "ready";
            internal void Own(UnityEngine.Object value){if(Phase!="preparing"||!value||owned.Contains(value))throw new InvalidOperationException("Window preparation cannot own this resource");owned.Add(value);}
            internal void Ready(UnityEngine.Object owner){if(Phase!="preparing"||bound||!owner||!entries.Contains(this))throw new InvalidOperationException("Window preparation has ended");host=owner;bound=true;Phase="ready";}
            internal bool Active=>host is Component c?c&&c.gameObject.activeInHierarchy:host is GameObject go&&go&&go.activeInHierarchy;
            internal bool Orphan=>bound&&!host&&Phase!="retiring";
            internal bool Released=>Phase=="retiring"&&owned.All(o=>!o);
            public void Dispose(){
                if(Phase=="retiring")return;Phase="retiring";
                foreach(var value in owned){if(value is GameObject go&&go){go.SetActive(false);NativeResourceLifetime.Release(go);}ArtResources.Release(value);}Collect();
            }
        }
        static readonly List<Lease> entries=new();static bool collecting;
        internal static Lease Bundle(RoomResourceOwner owner)=>Begin("bundle","",owner);
        internal static Lease Mask(RoomResourceOwner owner,string window){if(!DrawingSurface.Name(window))throw new ArgumentException("Invalid window identity");return Begin("mask",window,owner);}
        static Lease Begin(string kind,string window,RoomResourceOwner owner){
            if(owner==null||owner.Role is not ("window" or "unscoped"))throw new ArgumentException("Invalid window owner");Collect();
            if(entries.Count(e=>e.Kind==kind)>=(kind=="bundle"?MaximumBundles:MaximumMasks))throw new RoomResourceLimitException("The live window budget is full. Release unused windows or wait for pending masks to finish releasing, then retry. Saved windows are unchanged.");
            var lease=new Lease(kind,window,owner);entries.Add(lease);return lease;
        }
        internal static void Collect(){
            if(collecting)return;collecting=true;
            try{foreach(var entry in entries.ToArray())if(entry.Orphan)entry.Dispose();entries.RemoveAll(e=>e.Released);}finally{collecting=false;}
        }
        internal static JObject ObserveBudget(){Collect();return new JObject{["masks"]=entries.Count(e=>e.Kind=="mask"),["bundles"]=entries.Count(e=>e.Kind=="bundle"),["preparing"]=entries.Count(e=>e.Phase=="preparing"),["ready"]=entries.Count(e=>e.Phase=="ready"),["retiring"]=entries.Count(e=>e.Phase=="retiring"),["maskLimit"]=MaximumMasks,["bundleLimit"]=MaximumBundles};}
        internal static JObject Observe(int index){Collect();if(index<0||index>=entries.Count)return null;var e=entries[index];return new JObject{["reservationId"]=e.Id,["worldId"]=e.Owner.World,["regionId"]=e.Owner.Region,["target"]=e.Owner.Target,["kind"]=e.Kind,["window"]=e.Window,["phase"]=e.Phase,["active"]=e.Active};}
    }
}
