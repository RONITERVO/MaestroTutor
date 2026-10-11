// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        internal bool PrepareModelGeometry(string target,int revision,RoomModelGeometry settings,out RoomObjectData data,out string error)
        {
            data=null;if(!ComponentSource(target,revision,out data,out error))return false;
            if(PhysicsWorld&&PhysicsWorld.Running){error="Pause physics before changing imported geometry";return false;}
            if(data.kind!=RoomObjectKind.ImportedModel){error="Choose an imported room object";return false;}
            data.modelGeometry=settings?.Copy();
            if(!ComponentCandidate(new[]{data},out error))return false;
            return Find(target).GetComponent<CreatedRoomObject>().CanConfigureModelGeometry(settings,out error);
        }
        internal bool EditModelGeometry(string target,int revision,RoomModelGeometry settings,out string error)
        {
            if(!PrepareModelGeometry(target,revision,settings,out var data,out error))return false;
            var view=Find(target).GetComponent<CreatedRoomObject>();
            try
            {
                if(!CommitPersisted(new[]{data},Array.Empty<string>(),"Imported geometry saved",false,out error,ComponentBefore(new[]{data})))return false;
                if(view.ModelGeometryReady)return true;error="Geometry settings were saved but preparation is unavailable: "+view.ModelGeometryIssue;return false;
            }
            catch(System.Exception e){error=e is Imports.ModelImportException?e.Message:"Imported geometry could not be prepared; inspect the object before retrying";return false;}
        }
        internal JObject ObserveModelGeometry(string target)
        {
            var data=Read(target);if(data==null||data.kind!=RoomObjectKind.ImportedModel)return null;
            var view=Find(target)?.GetComponent<CreatedRoomObject>();var model=view?view.Model:null;var settings=data.modelGeometry;
            var source=model&&model.Ready?model.SourceBounds:default;var current=model&&model.Ready?model.LocalBounds:default;
            return new JObject{["target"]=target,["revision"]=ObjectRevision(target),["modelHash"]=data.modelHash,["settings"]=JObject.FromObject(settings),["ready"]=view&&view.ModelGeometryReady,["reason"]=view?view.ModelGeometryIssue:"Model object unavailable",
                ["sourceSize"]=JObject.FromObject(new {x=source.size.x,y=source.size.y,z=source.size.z}),["size"]=JObject.FromObject(new{x=current.size.x,y=current.size.y,z=current.size.z})};
        }
    }
}
