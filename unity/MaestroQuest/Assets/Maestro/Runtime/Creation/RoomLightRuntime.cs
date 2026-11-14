// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    /// <summary>Workspace-owned lamp state; rendering instances never own program output.</summary>
    internal sealed class RoomLightRuntime
    {
        internal RoomLight Source;
        internal string Definition,StateId;
        internal bool Enabled,Overridden;
        internal float Intensity,AttachmentReach;
        internal ScanSpatialPlacement ScanPlacement;
        internal string Color;
        internal UnityEngine.Color Pigment;
        internal void Synchronize(RoomLight source,RoomObjectData owner) {
            AttachmentReach=RoomLightObservation.AttachmentReach(owner.recipe,source.part);
            ScanPlacement=ScanSpatialPlacement.From(owner);
            string encoded=JsonUtility.ToJson(source);if(Definition==encoded)return;
            Definition=encoded;Source=source.Copy();Reset();
        }
        internal void Reset(){Overridden=false;Assign(Source.enabled,Source.intensity,Source.color);}
        internal void Set(bool enabled,float intensity,string color){Overridden=true;Assign(enabled,intensity,color);}
        void Assign(bool enabled,float intensity,string color){Enabled=enabled;Intensity=intensity;Color=color;ColorUtility.TryParseHtmlString(color,out Pigment);StateId=Guid.NewGuid().ToString("N");}
    }
    public sealed partial class RoomEditor
    {
        readonly Dictionary<(string target,string light),RoomLightRuntime> lightRuntime=new();
        RoomJournal lightJournal;
        internal RoomLightRuntime LightState(string target,string light)=>lightRuntime.TryGetValue((target,light),out var state)?state:null;
        void SynchronizeLights(RoomDocument document){
            if(!ReferenceEquals(lightJournal,journal)){lightRuntime.Clear();lightJournal=journal;}
            var present=new HashSet<(string,string)>();
            foreach(var obj in document.objects)foreach(var light in obj.lights){
                var key=(obj.id,light.id);present.Add(key);
                if(!lightRuntime.TryGetValue(key,out var state)){state=new RoomLightRuntime();lightRuntime.Add(key,state);}state.Synchronize(light,obj);
            }
            foreach(var key in lightRuntime.Keys.Where(k=>!present.Contains(k)).ToArray())lightRuntime.Remove(key);
        }
        void RefreshLights(){foreach(var item in objects.Values)if(item)item.GetComponent<RoomLightView>()?.Refresh();}
        void ResetLightControls(){foreach(var state in lightRuntime.Values)state.Reset();RefreshLights();}
        internal bool CanControlLight(string target,string light,string expected,out string error){
            error="The room lamp is unavailable";
            if(!isActiveAndEnabled||RuntimeGate.Held||Ownership.Suspended||!Frame.Valid||!TryGetLiveObject(target,out var item,out error)||!item)return false;
            var state=LightState(target,light);error="Lamp state changed or was removed; read object.light again";
            if(state==null||state.StateId!=expected)return false;error=null;return true;
        }
        internal bool ControlLight(string target,string light,string expected,JObject args,out string error){
            if(!CanControlLight(target,light,expected,out error))return false;
            var state=LightState(target,light);
            if((string)args["operation"]=="reset")state.Reset();
            else {
                bool enabled=(bool)args["enabled"];float intensity=(float)args["intensity"];string color=(string)args["color"];
                if(!RoomLighting.Range(intensity,0,4)||!RoomLighting.ColorValid(color)){error="Use a valid lamp intensity and colour";return false;}
                state.Set(enabled,intensity,color);
            }
            Find(target)?.GetComponent<RoomLightView>()?.Refresh();return true;
        }
        internal JObject ObserveLight(string target,string light){
            var state=LightState(target,light);if(state==null)return null;var item=Find(target);var view=item?item.GetComponent<RoomLightView>():null;
            return new JObject{["target"]=target,["revision"]=ObjectRevision(target),["light"]=light,["stateId"]=state.StateId,
                ["settings"]=JObject.Parse(JsonUtility.ToJson(state.Source)),["effective"]=new JObject{["enabled"]=state.Enabled,["intensity"]=state.Intensity,["color"]=state.Color,["overridden"]=state.Overridden},
                ["active"]=view&&view.Active(light),["reason"]=view?view.Reason(light):"The object's native light view is unavailable"};
        }
    }
}
