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
        internal string[] EnvironmentMembers(string id)=>Snapshot().objects.Where(x=>x.environmentProfile==id).Select(x=>x.id).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
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
        internal bool PrepareEnvironment(RoomEnvironmentProfile definition,string id,int revision,string[] members,out EnvironmentProfileEdits edits,out string error) {
            edits=null;if(!CanEditStructures(out error))return false;
            var current=ReadEnvironment(id);
            error="The environment profile changed; read its current revision and members";
            if(EnvironmentRevision(id)!=revision||current==null&&revision!=0||definition==null&&current==null)return false;
            if(definition!=null&&(definition.id!=id||!definition.Validate(out error)))return false;
            var expected=current==null?Array.Empty<string>():EnvironmentMembers(id);
            error="Include every currently bound object exactly once before changing a shared profile";
            if(members==null||members.Length!=members.Distinct().Count()||!expected.ToHashSet().SetEquals(members))return false;
            if(definition==null&&members.Length>0){error="Reassign the bound objects before removing this profile";return false;}
            foreach(string member in members)if(!CanChangeEnvironment(member,definition.realCollisions,out error))return false;
            edits=definition==null?new EnvironmentProfileEdits{Removals=new[]{id}}:new EnvironmentProfileEdits{Replacements=new[]{definition.Copy()}};
            if(!edits.Validate(out error))return false;
            var candidate=Snapshot();candidate.environmentProfiles=edits.Apply(candidate.environmentProfiles);return candidate.Validate(out error);
        }
        internal bool EditEnvironment(RoomEnvironmentProfile definition,string id,int revision,string[] members,out string error) {
            if(!PrepareEnvironment(definition,id,revision,members,out var edits,out error))return false;
            return CommitPersisted(Array.Empty<RoomObjectData>(),Array.Empty<string>(),definition==null?"Environment profile removed":"Environment profile saved",false,out error,environmentEdits:edits);
        }
        internal bool PrepareEnvironmentBinding(string target,int revision,string profile,int profileRevision,out RoomObjectData data,out string error) {
            data=null;var definition=string.IsNullOrEmpty(profile)?null:ReadEnvironment(profile);
            error="Read the exact object and selected profile revisions before assigning an environment";
            if(ObjectRevision(target)!=revision||profile==null||profile.Length==0&&profileRevision!=0||profile.Length>0&&(definition==null||EnvironmentRevision(profile)!=profileRevision))return false;
            if(!CanChangeEnvironment(target,definition?.realCollisions??true,out error))return false;
            data=Pose(Read(target),Find(target).transform);data.environmentProfile=profile;
            var candidate=Snapshot();var replacement=data;candidate.objects=candidate.objects.Select(x=>x.id==target?replacement:x).ToArray();return candidate.Validate(out error);
        }
        internal bool BindEnvironment(string target,int revision,string profile,int profileRevision,out string error) {
            if(!PrepareEnvironmentBinding(target,revision,profile,profileRevision,out var data,out error))return false;
            return CommitPersisted(new[]{data},Array.Empty<string>(),"Object environment saved",false,out error);
        }
        internal JObject ObserveEnvironmentBinding(string target) {
            var data=Read(target);if(data==null||!TryGetLiveObject(target,out var item,out _))return null;
            string id=data.environmentProfile??"";bool requested=ReadEnvironment(id)?.realCollisions??true;var world=PhysicsWorld;
            return new JObject{["target"]=target,["revision"]=ObjectRevision(target),["profileId"]=id,["profileRevision"]=EnvironmentRevision(id),
                ["realCollisions"]=requested,["state"]=new JObject{["effectiveRealCollisions"]=world&&world.IncludesRealRoom(item),
                ["ready"]=world&&world.EnvironmentReady(item),["inside"]=world&&world.ContainsSimulation(item.transform.position,item),
                ["admitted"]=world&&world.CanSimulate(item.transform.position,item)},["temporary"]=TemporaryRoom};
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
