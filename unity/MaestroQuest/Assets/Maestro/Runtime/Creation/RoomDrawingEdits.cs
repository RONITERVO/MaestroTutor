// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        internal bool CanCreateDrawing(Vector3[] points,float radius,out string error)
        {
            if(!CanCreatePrimitive(out error)||!RoomDocument.ValidateDrawing(points,radius,out error))return false;
            if(Snapshot().objects.Sum(x=>(x.points?.Length??0)+DrawingSurface.PointCount(x))+points.Length>RoomDocument.MaximumTotalPoints){error="This room has reached its drawing limit";return false;}
            return true;
        }
        internal bool CreateDrawing(string name,Vector3 position,float scale,Color color,float radius,Vector3[] points,out string id,out string error)
        {
            id=null;if(!CanCreateDrawing(points,radius,out error))return false;
            return CommitCreatedObject(new RoomObjectData {id=Guid.NewGuid().ToString("N"),kind=RoomObjectKind.Drawing,name=name,position=position,scale=scale,color=color,radius=radius,points=(Vector3[])points.Clone()},out id,out error);
        }
        internal bool PrepareDrawingEdit(string target,int revision,int index,int remove,Vector3[] points,float? radius,out RoomObjectData data,out string error)
        {
            data=null;if(!CanEditObject(target,true,out error))return false;
            if(ObjectRevision(target)!=revision){error="The drawing changed; inspect its current revision before editing";return false;}
            if(GetComponent<AnimationWorkshop>()?.ControlsTarget(target)==true){error="Finish authoring this object before editing its drawing";return false;}
            data=Read(target);if(data.kind!=RoomObjectKind.Drawing){error="Choose an existing drawing";return false;}
            if(radius.HasValue)data.radius=radius.Value;
            else {
                if(points==null||points.Length>64||index<0||index>data.points.Length||remove<0||remove>data.points.Length-index||remove==0&&points.Length==0){error="Choose an existing point range and up to 64 replacement points";return false;}
                data.points=data.points.Take(index).Concat(points).Concat(data.points.Skip(index+remove)).ToArray();
            }
            data=Pose(data,Find(target).transform);
            var replacement=data;var candidate=Snapshot();candidate.objects=candidate.objects.Select(x=>x.id==target?replacement:x).ToArray();
            return candidate.Validate(out error);
        }
        internal bool EditDrawing(string target,int revision,int index,int remove,Vector3[] points,float? radius,out string error)
        {
            if(!PrepareDrawingEdit(target,revision,index,remove,points,radius,out var data,out error))return false;
            return CommitPersisted(new[]{data},Array.Empty<string>(),"Drawing edited",false,out error);
        }
    }
}
