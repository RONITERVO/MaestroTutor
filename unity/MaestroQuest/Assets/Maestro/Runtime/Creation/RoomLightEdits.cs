// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        internal bool PrepareLightEdit(string target,int revision,JObject args,out RoomObjectData data,out string error){
            data=null;if(!CanEditObject(target,true,out error,true))return false;
            if(ObjectRevision(target)!=revision){error="The object changed; read object.lights again";return false;}
            data=Pose(Read(target),Find(target).transform);string id=(string)args["light"];var lights=data.lights.ToList();
            if((string)args["operation"]=="remove") {if(lights.RemoveAll(l=>l.id==id)==0){error="This lamp was removed";return false;}}
            else {var light=JsonUtility.FromJson<RoomLight>(args["settings"].ToString());light.id=id;lights.RemoveAll(l=>l.id==id);lights.Add(light);}
            data.lights=lights.OrderBy(l=>l.id,StringComparer.Ordinal).ToArray();var candidate=Snapshot();var next=data;
            candidate.objects=candidate.objects.Select(o=>o.id==target?next:o).ToArray();return candidate.Validate(out error);
        }
        internal bool EditLight(string target,int revision,JObject args,out string error){
            if(!PrepareLightEdit(target,revision,args,out var data,out error))return false;
            return CommitPersisted(new[]{data},Array.Empty<string>(),"Object light saved",false,out error,visualOnly:true);
        }
        internal JObject ObserveLights(string target){
            var data=Read(target);if(data==null)return null;var item=Find(target);var view=item?item.GetComponent<RoomLightView>():null;
            return new JObject{["target"]=target,["revision"]=ObjectRevision(target),["lights"]=new JArray(data.lights.Select(l=>new JObject{["id"]=l.id,["overridden"]=LightState(target,l.id)?.Overridden??false,["active"]=view&&view.Active(l.id),["reason"]=view?view.Reason(l.id):"The object's native light view is unavailable"}))};
        }
        void ApplyLights(RoomObjectData data,Maestro.Quest.Interaction.RoomItem item,RoomEditPreparation preparation){
            using var candidate=preparation?.TakeLights(data);var view=item.GetComponent<RoomLightView>();
            if(candidate!=null){if(!view)view=item.gameObject.AddComponent<RoomLightView>();view.Apply(this,data.id,data.lights,candidate);}
            else if(view)view.Refresh(); // Unrelated reconciliation cannot allocate after acceptance.
        }
    }
}
