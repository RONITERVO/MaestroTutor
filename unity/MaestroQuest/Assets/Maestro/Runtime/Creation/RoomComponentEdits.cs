// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Linq;
namespace Maestro.Quest.Creation {
    public sealed partial class RoomEditor {
        bool ComponentSource(string target,int revision,out RoomObjectData data,out string error){
            data=null;if(!CanEditObject(target,true,out error))return false;
            if(Ownership.Suspended){error="Room actions are paused";return false;}
            if(ObjectRevision(target)!=revision){error="The object changed; read its current revision";return false;}
            if(GetComponent<AnimationWorkshop>()?.ControlsTarget(target)==true){error="Finish authoring this object first";return false;}
            data=Pose(Read(target),Find(target).transform);return true;
        }
        bool ComponentCandidate(RoomObjectData[] replacements,out string error){var next=replacements.ToDictionary(x=>x.id);var candidate=Snapshot();candidate.objects=candidate.objects.Select(x=>next.TryGetValue(x.id,out var replacement)?replacement:x).ToArray();return candidate.Validate(out error);}
        static RoomLayout ComponentBefore(RoomObjectData[] data)=>new(){placements=data.Select(d=>new ObjectPlacement{target=d.id,position=d.position,rotation=d.rotation,scale=d.scale}).ToArray()};
    }
}
