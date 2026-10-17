// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        internal bool PrepareWindowEdit(string target,int revision,JObject args,out RoomObjectData data,out string error) {
            data=null;if(!CanEditObject(target,true,out error,true))return false;
            if(ObjectRevision(target)!=revision){error="The object changed; read object.windows again";return false;}
            data=Read(target);string id=(string)args["window"];var windows=data.windows.ToList();
            if((string)args["operation"]=="remove"){
                if(windows.RemoveAll(w=>w.id==id)==0){error="This window was removed";return false;}
            }else{
                windows.RemoveAll(w=>w.id==id);windows.Add(new RoomWindow{id=id,surface=(string)args["surface"],shape=(string)args["shape"],reveal=(float)args["reveal"]});
            }
            data.windows=windows.OrderBy(w=>w.id,StringComparer.Ordinal).ToArray();var candidate=Snapshot();var next=data;
            candidate.objects=candidate.objects.Select(o=>o.id==target?next:o).ToArray();return candidate.Validate(out error);
        }
        internal bool EditWindow(string target,int revision,JObject args,out string error) {
            if(!PrepareWindowEdit(target,revision,args,out var data,out error))return false;
            return CommitPersisted(new[]{data},Array.Empty<string>(),"Passthrough window saved",false,out error,visualOnly:true);
        }
        internal JObject ObserveWindows(string target) {
            var data=Read(target);if(data==null)return null;var item=Find(target);var view=item?item.GetComponent<RoomWindowView>():null;view?.Refresh();
            return new JObject{["target"]=target,["revision"]=ObjectRevision(target),["physicalAnchor"]=ScanDrawingAnchor.Has(data),["requested"]=view&&view.Requested,["renderingReady"]=view&&view.RenderingReady,["reason"]=view?view.UnavailableReason:"No window component",["windows"]=new JArray(data.windows.Select(w=>new JObject{["id"]=w.id,["surface"]=w.surface,["shape"]=w.shape,["reveal"]=w.reveal}))};
        }
    }
}
