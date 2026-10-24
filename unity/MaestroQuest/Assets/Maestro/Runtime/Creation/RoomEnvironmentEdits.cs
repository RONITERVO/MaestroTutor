// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        public RoomEnvironmentProfile[] EnvironmentProfiles()=>journal.EnvironmentSnapshot();
        public int EnvironmentRevision(string id)=>journal.EnvironmentRevision(id);
        public RoomEnvironmentProfile ReadEnvironment(string id)=>journal.ReadEnvironment(id);
        internal string[] EnvironmentMembers(string id)=>journal.EnvironmentMembers(id);
        readonly Collider[] environmentOverlaps=new Collider[64];
        bool CanChangeEnvironment(string id,bool real,out string error) {
            if(!CanEditStructures(out error)||!CanEditObject(id,false,out error))return false;
            if(!new CapabilityContext(this,GetComponent<AnimationWorkshop>()).Target(new JObject{["target"]=id},out var item,out error))return false;
            var world=PhysicsWorld;if(!world)return true;
            bool next=world.RealCollisions&&real;
            if(next==world.IncludesRealRoom(item))return true;
            error="Pause physics or place this object within its selected ground before changing its environment";
            if(world.Running&&!world.ContainsEnvironment(item.transform.position,next))return false;
            if(next&&world.SurfacesReady) {
                Physics.SyncTransforms();
                foreach(var own in item.Grab.colliders) {
                    if(!own||!own.enabled||own.isTrigger)continue;
                    var bounds=own.bounds;
                    int count=Physics.OverlapBoxNonAlloc(bounds.center,bounds.extents,environmentOverlaps,Quaternion.identity,1<<RoomPhysicsLayers.Scanned,QueryTriggerInteraction.Ignore);
                    error="Move the object clear of scanned surfaces before enabling real-room collisions";
                    if(count==environmentOverlaps.Length)return false;
                    for(int i=0;i<count;i++) {
                        var other=environmentOverlaps[i];if(!other||other.transform.IsChildOf(item.transform))continue;
                        if(own is MeshCollider mesh&&!mesh.convex)return false; // Unsupported penetration proof, never guess clear.
                        if(Physics.ComputePenetration(own,own.transform.position,own.transform.rotation,other,other.transform.position,other.transform.rotation,out _,out float depth)&&depth>.003f)return false;
                    }
                }
            }
            error=null;return true;
        }
        internal bool CanEditEnvironment(RoomEnvironmentProfile definition,string id,int revision,string[] members,out string error) {
            if(!CanEditStructures(out error))return false;
            var current=ReadEnvironment(id);
            error="The environment profile changed; read its current revision and members";
            if(EnvironmentRevision(id)!=revision||current==null&&revision!=0||definition==null&&current==null)return false;
            if(definition!=null&&(definition.id!=id||!definition.Validate(out error)))return false;
            var expected=current==null?Array.Empty<string>():EnvironmentMembers(id);
            error="Include every currently bound object exactly once before changing a shared profile";
            if(members==null||members.Length!=members.Distinct().Count()||!expected.ToHashSet().SetEquals(members))return false;
            if(definition==null&&members.Length>0){error="Reassign the bound objects before removing this profile";return false;}
            foreach(string member in members)if(!CanChangeEnvironment(member,definition.realCollisions,out error))return false;
            error="The room already has the maximum number of environment profiles";
            if(current==null&&journal.EnvironmentCount>=RoomEnvironmentProfile.MaximumProfiles)return false;
            error=null;return true;
        }
        internal bool PrepareEnvironment(RoomEnvironmentProfile definition,string id,int revision,string[] members,out EnvironmentProfileEdits edits,out string error) {
            edits=null;if(!CanEditEnvironment(definition,id,revision,members,out error))return false;
            edits=definition==null?new EnvironmentProfileEdits{Removals=new[]{id}}:new EnvironmentProfileEdits{Replacements=new[]{definition.Copy()}};
            if(!edits.Validate(out error))return false;
            var candidate=Snapshot();candidate.environmentProfiles=edits.Apply(candidate.environmentProfiles);return candidate.Validate(out error);
        }
        internal bool EditEnvironment(RoomEnvironmentProfile definition,string id,int revision,string[] members,out string error) {
            if(!PrepareEnvironment(definition,id,revision,members,out var edits,out error))return false;
            return CommitPersisted(Array.Empty<RoomObjectData>(),Array.Empty<string>(),definition==null?"Environment profile removed":"Environment profile saved",false,out error,environmentEdits:edits);
        }
        internal bool CanBindEnvironment(string target,int revision,string profile,int profileRevision,out string error) {
            var definition=string.IsNullOrEmpty(profile)?null:ReadEnvironment(profile);
            error="Read the exact object and selected profile revisions before assigning an environment";
            if(ObjectRevision(target)!=revision||profile==null||profile.Length==0&&profileRevision!=0||profile.Length>0&&(definition==null||EnvironmentRevision(profile)!=profileRevision))return false;
            if(!CanChangeEnvironment(target,definition?.realCollisions??true,out error))return false;
            error="A shared environment profile supports at most 16 bound objects; use another profile for additional objects";
            if(profile.Length>0&&Read(target).environmentProfile!=profile&&EnvironmentMembers(profile).Length>=16)return false;
            error=null;return true;
        }
        internal bool PrepareEnvironmentBinding(string target,int revision,string profile,int profileRevision,out RoomObjectData data,out string error) {
            data=null;if(!CanBindEnvironment(target,revision,profile,profileRevision,out error))return false;
            data=Pose(Read(target),Find(target).transform);data.environmentProfile=profile;
            var candidate=Snapshot();var replacement=data;candidate.objects=candidate.objects.Select(x=>x.id==target?replacement:x).ToArray();return candidate.Validate(out error);
        }
        internal bool BindEnvironment(string target,int revision,string profile,int profileRevision,out string error) {
            if(!PrepareEnvironmentBinding(target,revision,profile,profileRevision,out var data,out error))return false;
            return CommitPersisted(new[]{data},Array.Empty<string>(),"Object environment saved",false,out error);
        }
        internal bool BeginEnvironment(RoomEnvironmentProfile definition,string id,int revision,string[] members,
            out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null)
        {
            var captured=definition?.Copy();var affected=members==null?null:(string[])members.Clone();
            return BeginPreparedEdit(()=>CanEditEnvironment(captured,id,revision,affected,out var issue)?null:issue,
                (out RoomJournal.PreparedEdit edit,out string issue)=>{
                    edit=null;
                    return PrepareEnvironment(captured,id,revision,affected,out var edits,out issue)&&
                        journal.Prepare(Array.Empty<RoomObjectData>(),Array.Empty<string>(),out edit,out issue,environmentEdits:edits);
                },captured==null?"Environment profile removed":"Environment profile saved",out pending,out error,checkpoint);
        }
        internal bool BeginEnvironmentBinding(string target,int revision,string profile,int profileRevision,
            out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null)=>
            BeginContentEdit(()=>CanBindEnvironment(target,revision,profile,profileRevision,out var issue)?null:issue,
                (out RoomObjectData[] values,out string issue)=>{
                    bool ready=PrepareEnvironmentBinding(target,revision,profile,profileRevision,out var data,out issue);
                    values=ready?new[]{data}:null;return ready;
                },Array.Empty<string>(),"Object environment saved",out pending,out error,checkpoint);
        internal JObject ObserveEnvironmentSettings(string target) {
            var data=Read(target);if(data==null)return null;
            string id=data.environmentProfile??"";
            return new JObject{["target"]=target,["revision"]=ObjectRevision(target),["profileId"]=id,
                ["profileRevision"]=EnvironmentRevision(id),["realCollisions"]=ReadEnvironment(id)?.realCollisions??true,
                ["temporary"]=TemporaryRoom};
        }
        internal JObject ObserveEnvironmentBinding(string target) {
            var value=ObserveEnvironmentSettings(target);
            if(value==null||!TryGetLiveObject(target,out var item,out _))return null;
            var world=PhysicsWorld;
            value["state"]=new JObject{["effectiveRealCollisions"]=world&&world.IncludesRealRoom(item),
                ["ready"]=world&&world.EnvironmentReady(item),["inside"]=world&&world.ContainsSimulation(item.transform.position,item),
                ["admitted"]=world&&world.CanSimulate(item.transform.position,item)};
            return value;
        }
        internal JObject ObserveEnvironmentProfile(string id) {
            var p=ReadEnvironment(id);return p==null?null:new JObject{["id"]=id,["revision"]=EnvironmentRevision(id),["name"]=p.name,
                ["realCollisions"]=p.realCollisions,["members"]=new JArray(EnvironmentMembers(id)),["temporary"]=TemporaryRoom};
        }
        internal JObject ObserveEnvironmentProfiles(int offset) {
            if(offset<0||offset>RoomEnvironmentProfile.MaximumProfiles)return null;
            var profiles=EnvironmentProfiles();
            return new JObject{["offset"]=offset,["total"]=profiles.Length,["pageSize"]=3,
                ["entries"]=new JArray(profiles.Skip(offset).Take(3).Select(p=>new JObject{["id"]=p.id,["revision"]=EnvironmentRevision(p.id),["name"]=p.name,["realCollisions"]=p.realCollisions}))};
        }
    }
}
