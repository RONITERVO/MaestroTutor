// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Interaction;
namespace Maestro.Quest.Creation {
    public sealed partial class RoomEditor {
        internal bool PrepareSnapPoint(string target,int revision,string point,RoomSnapPoint definition,out RoomObjectData data,out string error){
            data=null;if(!CanEditObject(target,true,out error))return false;
            if(ObjectRevision(target)!=revision){error="The object changed; read its current snap-point revision";return false;}
            if(RuntimeGate.Held||Ownership.Suspended||GetComponent<AnimationWorkshop>()?.ControlsTarget(target)==true){error="Finish authoring and resume room interaction first";return false;}
            if(!RoomSnapPoint.Identifier(point)){error="Choose a stable snap-point ID";return false;}
            data=Pose(Read(target),Find(target).transform);
            var retained=(data.snapPoints??Array.Empty<RoomSnapPoint>()).Where(p=>p.id!=point);
            if(definition!=null){definition=definition.Copy();definition.id=point;retained=retained.Append(definition);}
            data.snapPoints=retained.ToArray();var next=data;var candidate=Snapshot();candidate.objects=candidate.objects.Select(x=>x.id==target?next:x).ToArray();return candidate.Validate(out error);
        }
        internal bool EditSnapPoint(string target,int revision,string point,RoomSnapPoint definition,out string error){
            if(!PrepareSnapPoint(target,revision,point,definition,out var data,out error))return false;
            return CommitPersisted(new[]{data},Array.Empty<string>(),definition==null?"Snap point removed":"Snap point saved",false,out error);
        }
        internal bool PrepareSnap(RoomSnapPlacement request,out RoomObjectData[] replacements,out RoomLayout before,out string error){
            replacements=null;before=null;error="Provide a snap placement";
            if(request==null||!request.Validate(out error)||!GroupSources(request.members,out var source,out error))return false;
            var destination=request.destination;
            if(!CanEditObject(destination.target,true,out error))return false;
            if(ObjectRevision(destination.target)!=destination.revision){error="The destination changed; read its current revision";return false;}
            var otherItem=Find(destination.target);var other=Pose(Read(destination.target),otherItem.transform);
            if(!otherItem.isActiveAndEnabled||!otherItem.Grab.enabled||otherItem.GetComponent<RigidRoomItem>()?.GeometryReady==false||other.recipe?.playing==true||GetComponent<AnimationWorkshop>()?.ControlsTarget(destination.target)==true){error="Release the destination and stop its playback/authoring first";return false;}
            var first=Read(request.members[0].target);
            var from=first.snapPoints?.FirstOrDefault(p=>p.id==request.point);var to=other.snapPoints?.FirstOrDefault(p=>p.id==destination.point);
            if(from==null||to==null||from.family!=to.family){error="Choose existing snap points with the same exact family";return false;}
            var layout=new RoomLayout{placements=source.placements.Select(p=>new ObjectPlacement{target=p.target}).ToArray()};
            if(!request.Projection(source,from,other,to).Project(source,layout,out error)||!CanApplyLayout(layout,out error))return false;
            replacements=layout.placements.Select(p=>{var data=Read(p.target);p.Apply(data);return data;}).Append(other).ToArray();
            if(request.mode=="join"){
                var owner=replacements[0];
                if((owner.connections?.Length??0)>0){error="The first moving member already owns a connection; choose the construction's unlinked root first";return false;}
                if(owner.physics==ItemPhysics.Fixed){error="Use solid or bouncy physics on the first moving member before joining";return false;}
                // Store ordinary fixed-connection frames, including the chosen turn. Later point edits
                // do not secretly reposition or alter an already accepted joint.
                var destinationFrame=to.frame.Copy();destinationFrame.rotation=(destinationFrame.rotation*UnityEngine.Quaternion.AngleAxis(request.turn,UnityEngine.Vector3.up)).normalized;
                owner.connections=new[]{new RoomConnection{kind="fixed",connected=other.id,ownerFrame=from.frame.Copy(),connectedFrame=destinationFrame,breakForce=request.breakForce,breakTorque=request.breakTorque}};
            }
            var next=replacements.ToDictionary(x=>x.id);var candidate=Snapshot();candidate.objects=candidate.objects.Select(o=>next.TryGetValue(o.id,out var replacement)?replacement:o).ToArray();
            if(!candidate.Validate(out error))return false;
            before=new RoomLayout{placements=source.placements.Append(Frame.Placement(other.id,otherItem.transform)).ToArray()};return before.Validate(out error);
        }
        internal bool SnapConstruction(RoomSnapPlacement request,out string error){
            if(!PrepareSnap(request,out var replacements,out var before,out error))return false;
            if(!CommitPersisted(replacements,Array.Empty<string>(),request.mode=="join"?"Construction snapped and joined — one Undo restores it":"Construction snapped — one Undo restores it",true,out error,before))return false;
            if(request.mode=="join")Find(request.members[0].target)?.GetComponent<RoomConnectionView>()?.ResetBreak();return true;
        }
    }
}
