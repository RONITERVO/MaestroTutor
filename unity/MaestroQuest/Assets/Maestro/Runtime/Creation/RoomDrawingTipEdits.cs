// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        internal bool PrepareDrawingTip(string target,int revision,DrawingTip tip,out RoomObjectData data,out string error)
        {
            data=null;if(!CanEditObject(target,true,out error))return false;
            if(ObjectRevision(target)!=revision){error="The object changed; inspect its current drawing-tip revision";return false;}
            if(GetComponent<AnimationWorkshop>()?.ControlsTarget(target)==true){error="Finish authoring this object first";return false;}
            if(tip!=null){tip=tip.Copy();tip.version=tip.Mode=="erase"?2:1;}
            data=Pose(Read(target),Find(target).transform);data.drawingTips=tip==null?Array.Empty<DrawingTip>():new[]{tip.Copy()};
            var candidate=Snapshot();var next=data;candidate.objects=candidate.objects.Select(x=>x.id==target?next:x).ToArray();return candidate.Validate(out error);
        }
        internal bool EditDrawingTip(string target,int revision,DrawingTip tip,out string error)
        {
            if(!PrepareDrawingTip(target,revision,tip,out var data,out error))return false;
            return CommitPersisted(new[]{data},Array.Empty<string>(),tip==null?"Drawing tip removed":"Drawing tip saved",false,out error);
        }
    }
}
