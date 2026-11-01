// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Newtonsoft.Json.Linq;
using Maestro.Quest.Imports;
namespace Maestro.Quest.Creation {
    public sealed partial class RoomEditor {
        internal bool CanConfigureObjectWalk(string target,int revision,string source,string hash,string motionId,int index,out string error){
            if(!CurrentSettings(target,revision,true,out error))return false;
            var data=Read(target);error="Choose an imported character and its exact current model hash";
            if(data.kind!=RoomObjectKind.ImportedModel||data.modelHash!=hash)return false;
            if(source=="embedded")return AvatarWalkSelection.ApplyEmbedded(this,data,hash,index,out error);
            if(source is not ("none" or "library"))return false;
            return AvatarWalkSelection.Apply(this,data,source=="none"?"":motionId,out error);
        }
        internal bool ConfigureObjectWalk(string target,int revision,string source,string hash,string motionId,int index,out string error){
            if(!CanConfigureObjectWalk(target,revision,source,hash,motionId,index,out error))return false;
            return EditObject(target,true,data=>ApplyWalkChoice(data,source,motionId,hash,index,out _),"Character walking animation saved",false,out error);
        }
        internal JObject ObserveObjectWalk(string target){
            var data=Read(target);if(data?.kind!=RoomObjectKind.ImportedModel)return null;
            var view=AvatarWalkSelection.Observe(this,target);if(view==null)return null;
            return new(){["target"]=target,["revision"]=ObjectRevision(target),["temporary"]=TemporaryRoom,["selection"]=new JObject{
                ["source"]=view.source,["motionId"]=view.motionId??"",["modelHash"]=view.modelHash??"",["clipIndex"]=view.clipIndex,["available"]=view.available,
                ["name"]=ImportObservation.Text(view.name),["status"]=ImportObservation.Text(view.status),["playbackStatus"]=view.playbackStatus??""}};
        }
    }
}
