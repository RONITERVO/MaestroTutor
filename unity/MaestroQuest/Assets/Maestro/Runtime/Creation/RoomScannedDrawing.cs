// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        internal bool PrepareScanDrawing(JObject args,out RoomObjectData data,out string error)
        {
            data=null;error=null;bool create=(string)args["operation"]!="rebind";
            if(DrawingInProgress){error="Finish or discard the current drawing gesture before changing a scanned layer";return false;}
            if(Ownership.Suspended){error="Room actions are paused";return false;}
            if(create){if(!CanCreatePrimitive(out error))return false;data=new RoomObjectData{id=Guid.NewGuid().ToString("N"),name=(string)args["name"],kind=RoomObjectKind.Drawing,points=Array.Empty<Vector3>(),surfaces=new[]{new DrawingSurface{id="Canvas",width=(float)args["width"],height=(float)args["height"]}}};}
            else {
                string target=(string)args["target"];if(!CanEditObject(target,true,out error,true))return false;
                if(ObjectRevision(target)!=(int)args["revision"]){error="The layer changed; read its current revision";return false;}
                data=Read(target);if(!ScanDrawingAnchor.Has(data)){error="Choose a scanned drawing layer";return false;}
            }
            var scan=PhysicsWorld?PhysicsWorld.GetComponent<ScannedRoom>():null;if(!scan){error="The scanned-room service is unavailable";return false;}
            var canvas=data.surfaces[0];ScanDrawingAnchor binding;
            if((string)args["operation"]=="atGaze"){
                if(!Viewer){error="The tracked view is unavailable";return false;}
                if(!scan.DrawingAtGaze(transform,new Ray(Viewer.position,Viewer.forward),(string)args["stateId"],(float)args["angle"],canvas.width,canvas.height,out binding,out error))return false;
            }else if(!scan.PrepareDrawing(transform,(string)args["stateId"],(string)args["anchorId"],(float)args["x"],(float)args["y"],(float)args["angle"],canvas.width,canvas.height,out binding,out error))return false;
            data.scanAnchors=new[]{binding};var candidate=Snapshot();var next=data;candidate.objects=create?candidate.objects.Append(next).ToArray():candidate.objects.Select(d=>d.id==next.id?next:d).ToArray();return candidate.Validate(out error);
        }
        internal bool EditScanDrawing(JObject args,out JObject result,out string error)
        {
            result=null;if(!PrepareScanDrawing(args,out var data,out error)||!CommitPersisted(new[]{data},Array.Empty<string>(),"Scanned ink layer saved",false,out error))return false;
            Select(Find(data.id));result=new JObject{["objectId"]=data.id,["revision"]=ObjectRevision(data.id),["surface"]="Canvas",["roomId"]=data.scanAnchors[0].roomId,["anchorId"]=data.scanAnchors[0].anchorId,["temporary"]=TemporaryRoom};return true;
        }
        internal JObject ObserveScanDrawing(string target)
        {
            var data=Read(target);if(!ScanDrawingAnchor.Has(data))return null;var view=Find(target)?.GetComponent<ScannedDrawingView>();view?.Sync();var a=data.scanAnchors[0];
            var scan=PhysicsWorld?PhysicsWorld.GetComponent<ScannedRoom>():null;
            return new JObject{["stateId"]=scan?(string)scan.ObserveLayout(transform)["stateId"]:"",["revision"]=ObjectRevision(target),["anchor"]=new JObject{["roomId"]=a.roomId,["anchorId"]=a.anchorId,["x"]=a.x,["y"]=a.y,["angle"]=a.angle},["width"]=data.surfaces[0].width,["height"]=data.surfaces[0].height,["visible"]=view&&view.Visible,["reason"]=view?view.Status:"The layer view is unavailable"};
        }
    }
}
