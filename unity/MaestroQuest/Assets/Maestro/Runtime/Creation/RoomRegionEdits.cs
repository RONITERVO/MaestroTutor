// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        internal RoomRegion[] Regions()=>journal.RegionSnapshot();
        internal RoomRegion ReadRegion(string id)=>journal.ReadRegion(id);
        internal int RegionRevision(string id)=>journal.RegionRevision(id);
        internal string RegionFor(string target)=>journal.RegionFor(target);
        internal bool PrepareRegion(RoomRegion definition,string id,int revision,string[] members,out RegionEdits edits,out string error) {
            edits=null;if(!CanEditStructures(out error))return false;
            var current=ReadRegion(id);error="The area changed; read its current revision and complete membership";
            if(RegionRevision(id)!=revision||current==null&&revision!=0||definition==null&&current==null)return false;
            var expected=current?.members??Array.Empty<string>();
            if(members==null||members.Distinct(StringComparer.Ordinal).Count()!=members.Length||!expected.ToHashSet(StringComparer.Ordinal).SetEquals(members))return false;
            if(definition==null&&expected.Length>0){error="Move every creation out of this area before removing it";return false;}
            if(definition!=null&&(definition.id!=id||!definition.Validate(out error)||!expected.ToHashSet(StringComparer.Ordinal).SetEquals(definition.members)))return false;
            edits=definition==null?new RegionEdits{Removals=new[]{id}}:new RegionEdits{Replacements=new[]{definition.Copy()}};
            if(!edits.Validate(out error))return false;
            var candidate=Snapshot();candidate.regions=edits.Apply(candidate.regions);return candidate.Validate(out error);
        }
        internal bool EditRegion(RoomRegion definition,string id,int revision,string[] members,out string error) {
            if(!PrepareRegion(definition,id,revision,members,out var edits,out error))return false;
            return CommitPersisted(Array.Empty<RoomObjectData>(),Array.Empty<string>(),definition==null?"Area removed":"Area saved",false,out error,regionEdits:edits);
        }
        internal bool PrepareRegionBinding(string target,int revision,string id,int regionRevision,out RegionEdits edits,out string error) {
            edits=null;if(!CanEditStructures(out error))return false;
            var data=Read(target);var destination=string.IsNullOrEmpty(id)?null:ReadRegion(id);
            error="Read the creation and selected area revisions before assigning it";
            if(data==null||data.IsBuiltIn||ObjectRevision(target)!=revision||id==null||id.Length==0&&regionRevision!=0||id.Length>0&&(destination==null||RegionRevision(id)!=regionRevision))return false;
            string old=RegionFor(target);if(old==id){edits=new RegionEdits();error=null;return true;}
            var changed=new System.Collections.Generic.List<RoomRegion>();
            if(old.Length>0){var source=ReadRegion(old);source.members=source.members.Where(x=>x!=target).ToArray();changed.Add(source);}
            if(destination!=null){destination.members=destination.members.Append(target).OrderBy(x=>x,StringComparer.Ordinal).ToArray();changed.Add(destination);}
            edits=new RegionEdits{Replacements=changed.ToArray()};
            var candidate=Snapshot();candidate.regions=edits.Apply(candidate.regions);return candidate.Validate(out error);
        }
        internal bool BindRegion(string target,int revision,string id,int regionRevision,out string error) {
            if(!PrepareRegionBinding(target,revision,id,regionRevision,out var edits,out error))return false;
            return CommitPersisted(Array.Empty<RoomObjectData>(),Array.Empty<string>(),"Creation area assigned",false,out error,regionEdits:edits);
        }
        internal JObject ObserveRegion(string id) {
            if(!WorldIdentityReady)return null;
            var value=ReadRegion(id);return value==null?null:new JObject{["id"]=id,["revision"]=RegionRevision(id),["name"]=value.name,["memberCount"]=value.members.Length,["temporary"]=TemporaryRoom};
        }
        internal JObject ObserveRegionMembers(string id,int offset) {
            if(!WorldIdentityReady)return null;
            var value=ReadRegion(id);if(value==null||offset<0||offset>RoomDocument.MaximumObjects)return null;
            return new JObject{["id"]=id,["revision"]=RegionRevision(id),["offset"]=offset,["total"]=value.members.Length,["pageSize"]=16,["members"]=new JArray(value.members.Skip(offset).Take(16))};
        }
        internal JObject ObserveRegions(int offset) {
            if(!WorldIdentityReady)return null;
            if(offset<0||offset>RoomRegion.MaximumRegions)return null;var values=Regions();
            return new JObject{["offset"]=offset,["total"]=values.Length,["pageSize"]=3,["entries"]=new JArray(values.Skip(offset).Take(3).Select(v=>new JObject{["id"]=v.id,["revision"]=RegionRevision(v.id),["name"]=v.name}))};
        }
        internal JObject ObserveRegionBinding(string target) {
            if(!WorldIdentityReady)return null;
            var data=Read(target);if(data==null||data.IsBuiltIn)return null;string id=RegionFor(target);
            return new JObject{["target"]=target,["revision"]=ObjectRevision(target),["regionId"]=id,["regionRevision"]=RegionRevision(id),["homeRegionId"]=WorldIdentity.regionId,["temporary"]=TemporaryRoom};
        }
    }
}
