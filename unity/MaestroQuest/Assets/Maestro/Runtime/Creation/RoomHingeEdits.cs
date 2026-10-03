// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        internal bool PrepareHinge(string target,int revision,string operation,string connected,RoomHinge definition,float angle,out RoomObjectData data,out string error)
        {
            data=null;if(!CanEditObject(target,true,out error))return false;
            if(ObjectRevision(target)!=revision){error="The object changed; inspect its hinge revision";return false;}
            if(GetComponent<AnimationWorkshop>()?.ControlsTarget(target)==true){error="Finish authoring this object first";return false;}
            data=Pose(Read(target),Find(target).transform);
            if(operation=="remove")data.hinges=Array.Empty<RoomHinge>();
            else {
                if(!CanEditObject(connected,true,out error))return false;
                if(GetComponent<AnimationWorkshop>()?.ControlsTarget(connected)==true){error="Finish authoring the connected object first";return false;}
                if(operation=="configure"){if(definition==null){error="Provide a hinge definition";return false;}data.hinges=new[]{definition.Copy()};data.hinges[0].connected=connected;}
                else if(operation=="align"){
                    var h=data.hinges?.FirstOrDefault();error="Inspect an existing hinge and its exact connected object before alignment";
                    if(h==null||h.connected!=connected||!float.IsFinite(angle)||angle<-170||angle>170||h.limits.enabled&&(angle<h.limits.minimum||angle>h.limits.maximum))return false;
                    // Compute in room coordinates, without touching the live transforms during admission or a failed save.
                    var other=Pose(Read(connected),Find(connected).transform);
                    h.Align(data,other,angle);
                } else {error="Unknown hinge operation";return false;}
            }
            var candidate=Snapshot();var next=data;candidate.objects=candidate.objects.Select(x=>x.id==target?next:x).ToArray();return candidate.Validate(out error);
        }
        internal bool EditHinge(string target,int revision,string operation,string connected,RoomHinge definition,float angle,out string error)
        {
            if(!PrepareHinge(target,revision,operation,connected,definition,angle,out var data,out error))return false;
            var replacements=connected!=null?new[]{data,Pose(Read(connected),Find(connected).transform)}:new[]{data};
            var before=new RoomLayout {placements=replacements.Select(x=>ObjectPlacement.Capture(x.id,Find(x.id).transform)).ToArray()};
            return CommitPersisted(replacements,Array.Empty<string>(),"Hinge "+operation,true,out error,before);
        }
    }
}
