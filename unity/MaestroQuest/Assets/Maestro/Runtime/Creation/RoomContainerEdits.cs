// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation {
    public sealed partial class RoomEditor {
        internal bool PrepareContainer(string target,int revision,RoomContainer definition,out RoomObjectData data,out string error){
            data=null;if(!ContainerSource(target,revision,out data,out error))return false;
            data.containers=definition==null?Array.Empty<RoomContainer>():new[]{definition.Copy()};return ContainerCandidate(new[]{data},out error);
        }
        bool ContainerSource(string target,int revision,out RoomObjectData data,out string error){
            data=null;if(!CanEditObject(target,true,out error))return false;
            if(Ownership.Suspended){error="Room actions are paused";return false;}
            if(ObjectRevision(target)!=revision){error="The object changed; read its current container revision";return false;}
            if(GetComponent<AnimationWorkshop>()?.ControlsTarget(target)==true){error="Finish authoring this object first";return false;}
            data=Pose(Read(target),Find(target).transform);return true;
        }
        bool ContainerCandidate(RoomObjectData[] replacements,out string error){var next=replacements.ToDictionary(x=>x.id);var candidate=Snapshot();candidate.objects=candidate.objects.Select(x=>next.TryGetValue(x.id,out var replacement)?replacement:x).ToArray();return candidate.Validate(out error);}
        internal bool EditContainer(string target,int revision,RoomContainer definition,out string error){
            if(!PrepareContainer(target,revision,definition,out var data,out error))return false;
            return CommitPersisted(new[]{data},Array.Empty<string>(),definition==null?"Container removed":"Container saved",false,out error,ContainerBefore(new[]{data}));
        }
        static RoomLayout ContainerBefore(RoomObjectData[] data)=>new(){placements=data.Select(d=>new ObjectPlacement{target=d.id,position=d.position,rotation=d.rotation,scale=d.scale}).ToArray()};
        internal bool PrepareContainerTransfer(string source,int sourceRevision,string destination,int destinationRevision,double requested,out RoomObjectData[] data,out double moved,out string error){
            data=null;moved=0;error="Choose different source and destination objects";if(source==destination)return false;
            if(!ContainerSource(source,sourceRevision,out var from,out error)||!ContainerSource(destination,destinationRevision,out var to,out error))return false;
            if(!RoomContainer.Transfer(from.containers?.FirstOrDefault(),to.containers?.FirstOrDefault(),requested,out moved,out error))return false;
            data=new[]{from,to};return ContainerCandidate(data,out error);
        }
        internal bool TransferContainer(string source,int sourceRevision,string destination,int destinationRevision,double requested,out double moved,out string error){
            if(!PrepareContainerTransfer(source,sourceRevision,destination,destinationRevision,requested,out var data,out moved,out error))return false;
            if(CommitPersisted(data,Array.Empty<string>(),"Liquid transferred — one Undo restores both containers",false,out error,ContainerBefore(data)))return true;
            moved=0;return false;
        }
    }
}
