// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation {
    public sealed partial class RoomEditor {
        internal bool CanConfigureStaticSupport(string target,int revision,RoomStaticSupport settings,out string error){
            if(!CurrentSettings(target,revision,true,out error))return false;
            error="Choose valid static support settings";if(settings==null||!settings.Valid)return false;
            var data=Read(target);if(data==null)return false;data.staticSupport=settings.Copy();
            return RoomStaticSupport.Validate(data,out error);
        }
        internal bool BeginConfigureStaticSupport(string target,int revision,RoomStaticSupport settings,
            out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null){
            var captured=settings?.Copy();
            return BeginSettingsEdit(target,()=>CanConfigureStaticSupport(target,revision,captured,out var issue)?null:issue,
                (RoomObjectData data,out string issue)=>{data.staticSupport=captured.Copy();return RoomStaticSupport.Validate(data,out issue);},
                "Static support settings saved",out pending,out error,checkpoint);
        }
        internal JObject ObserveStaticSupport(string target){
            var data=Read(target);if(data==null||data.IsBuiltIn)return null;
            var native=Find(target)?.GetComponent<CreatedRoomObject>();
            bool enabled=data.staticSupport.enabled,ready=enabled&&native&&native.StaticSupportReady;
            string reason=!enabled?"Static support is disabled":native?native.StaticSupportIssue:"Static support geometry is unavailable";
            return new JObject{["target"]=target,["revision"]=ObjectRevision(target),["enabled"]=enabled,["ready"]=ready,
                ["reason"]=reason??"",["temporary"]=TemporaryRoom};
        }
    }
}
