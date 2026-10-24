// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Avatar;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        internal bool CanConfigureFeet(string id,int revision,RoomFootPlacement settings,out string error)
        {
            if(!CurrentSettings(id,revision,false,out error))return false;
            var data=Read(id);error="Choose Maestro or a supported imported character and finite foot clearances";
            if(data.kind is not (RoomObjectKind.Maestro or RoomObjectKind.ImportedModel)||settings?.Valid!=true)return false;
            if(!settings.enabled){error=null;return true;}
            return HumanoidFootPlacement.Available(Find(id),settings,out _,out error);
        }
        internal bool BeginConfigureFeet(string id,int revision,RoomFootPlacement settings,
            out PendingPersistedEdit pending,out string error,System.Action<string> checkpoint=null)
        {
            var captured=settings?.Copy();
            return BeginSettingsEdit(id,()=>CanConfigureFeet(id,revision,captured,out var issue)?null:issue,
                (RoomObjectData data,out string issue)=>{data.footPlacement=captured.Copy();issue=null;return true;},
                "Foot placement saved",out pending,out error,checkpoint);
        }
        internal JObject ObserveFeet(string id)
        {
            var data=Read(id);if(data?.kind is not (RoomObjectKind.Maestro or RoomObjectKind.ImportedModel))return null;
            var item=Find(id);var settings=data.footPlacement;bool available=HumanoidFootPlacement.Available(item,settings,out _,out var issue);
            return new(){["target"]=id,["revision"]=ObjectRevision(id),["temporary"]=TemporaryRoom,["mode"]=settings.enabled?"grounded":"authored",
                ["modelHash"]=settings.enabled?settings.modelHash:data.modelHash??"",["leftClearance"]=settings.leftClearance,["rightClearance"]=settings.rightClearance,
                ["available"]=available,["status"]=issue??"Ready",["calibration"]=string.IsNullOrEmpty(data.modelHash)?"includedRig":"restBounds",
                ["presentation"]=item?item.GetComponent<HumanoidFootPlacement>()?.Status??"authored":"unloaded"};
        }
    }
}
