// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public readonly struct StructureState
    {
        public readonly int Total,Present,Displaced,Missing,Held;
        public readonly bool Available;
        public StructureState(int total,int present,int displaced,int missing,int held,bool available){Total=total;Present=present;Displaced=displaced;Missing=missing;Held=held;Available=available;}
    }
    public sealed partial class RoomEditor
    {
        public RoomStructure[] Structures()=>journal.StructureSnapshot();
        public RoomStructure ReadStructure(string id)=>journal.ReadStructure(id);
        public int StructureRevision(string id)=>journal.StructureRevision(id);
        bool CanEditStructures(out string error)
        {
            error=null;
            if(WriteGate.Frozen){error=Persistence.WorkspaceWriteGate.FrozenReason;return false;}
            if(journal==null||storage.ReadOnly||RuntimeGate.Held){error="Wait until the room is available for editing";return false;}
            return true;
        }
        public bool CanSaveStructure(RoomStructure definition,int expectedRevision,out string error)
        {
            if(!CanEditStructures(out error))return false;
            if(definition==null){error="Provide a structure definition";return false;}
            if(!definition.Validate(out error))return false;
            if(StructureRevision(definition.id)!=expectedRevision){error="The structure changed; read its current definition before saving";return false;}
            if(!CanApplyLayout(new RoomLayout {placements=definition.slots.Select(x=>x.placement).ToArray()},out error))return false;
            error="The room already has the maximum number of structures";
            if(ReadStructure(definition.id)==null&&journal.StructureCount>=RoomStructure.MaximumStructures)return false;
            error="A structure identity cannot identify an object";if(HasSavedObject(definition.id))return false;
            error=null;return true;
        }
        bool PrepareStructure(RoomStructure definition,string id,int revision,out StructureEdits edits,out string error) {
            edits=null;if(definition!=null&&definition.id!=id){error="The structure identity changed";return false;}
            if(definition!=null? !CanSaveStructure(definition,revision,out error): !CanForgetStructure(id,revision,out error))return false;
            edits=definition==null?new StructureEdits {Removals=new[]{id}}:new StructureEdits {Replacements=new[]{definition.Copy()}};
            if(!edits.Validate(out error))return false;
            var candidate=Snapshot();candidate.structures=edits.Apply(candidate.structures);return candidate.Validate(out error);
        }
        internal bool BeginStructure(RoomStructure definition,string id,int revision,out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null) {
            var captured=definition?.Copy();
            return BeginPreparedEdit(()=>{string issue;return (captured==null?CanForgetStructure(id,revision,out issue):CanSaveStructure(captured,revision,out issue))?null:issue;},
                (out RoomJournal.PreparedEdit edit,out string issue)=>{edit=null;return PrepareStructure(captured,id,revision,out var edits,out issue)&&journal.Prepare(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out edit,out issue,structureEdits:edits);},
                captured==null?"Structure definition removed — its objects remain":"Structure saved — its pieces stay independent",out pending,out error,checkpoint);
        }
        public bool SaveStructure(RoomStructure definition,int expectedRevision,out string error)
        {
            if(!PrepareStructure(definition,definition?.id,expectedRevision,out var edits,out error))return false;
            return CommitPersisted(Array.Empty<RoomObjectData>(),Array.Empty<string>(),"Structure saved — its pieces stay independent",false,out error,structureEdits:edits);
        }
        bool CurrentStructure(string id,int revision,out RoomStructure definition,out string error)
        {
            definition=ReadStructure(id);error="The structure is missing or changed; read its current definition";
            return definition!=null&&revision>0&&StructureRevision(id)==revision;
        }
        public bool CanResetStructure(string id,int revision,string[] members,out string error,object physicsOwner=null)
        {
            if(!CanEditStructures(out error)||!CurrentStructure(id,revision,out var definition,out error))return false;
            if(members==null||members.Length!=definition.slots.Length||members.Distinct().Count()!=members.Length||!members.ToHashSet().SetEquals(definition.slots.Select(x=>x.placement.target))){error="Reset must name exactly the structure's current members";return false;}
            if(!CanApplyLayout(new RoomLayout {placements=definition.slots.Select(x=>x.placement).ToArray()},physicsOwner,out error)){error="Structure reset refused: "+error;return false;}
            return true;
        }
        public bool ResetStructure(string id,int revision,string[] members,out string error)
        {
            if(!CanResetStructure(id,revision,members,out error))return false;
            return ApplyLayout(new RoomLayout {placements=ReadStructure(id).slots.Select(x=>x.placement).ToArray()},out error);
        }
        public bool CanForgetStructure(string id,int revision,out string error)=>CanEditStructures(out error)&&CurrentStructure(id,revision,out _,out error);
        public bool ForgetStructure(string id,int revision,out string error)
        {
            if(!PrepareStructure(null,id,revision,out var edits,out error))return false;
            return CommitPersisted(Array.Empty<RoomObjectData>(),Array.Empty<string>(),"Structure definition removed — its objects remain",false,out error,structureEdits:edits);
        }
        public StructureState ObserveStructure(RoomStructure definition)
        {
            int present=0,displaced=0,missing=0,held=0;bool available=!RuntimeGate.Held;
            foreach(var slot in definition.slots){
                var expected=slot.placement;var data=Read(expected.target);var item=Find(expected.target);
                if(data==null){missing++;continue;}
                present++;
                if(!item||!item.isActiveAndEnabled){available=false;continue;}
                if(item.Grab&&item.Grab.isSelected)held++;
                var live=Frame.Placement(expected.target,item.transform);
                if(!new RoomLayout {placements=new[]{live}}.Validate(out _)){available=false;continue;}
                if(Vector3.Distance(live.position,expected.position)>definition.positionTolerance||Quaternion.Angle(live.rotation,expected.rotation)>definition.rotationTolerance||Mathf.Abs(live.scale-expected.scale)>definition.scaleTolerance)displaced++;
            }
            return new StructureState(definition.slots.Length,present,displaced,missing,held,available);
        }
    }
}
