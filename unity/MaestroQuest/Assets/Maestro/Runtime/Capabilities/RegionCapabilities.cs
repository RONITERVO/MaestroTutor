// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal abstract class RegionCapability:CapabilityModule
    {
        internal const string Feature="worldRegions.v1";
        internal static JObject RegionId(bool empty=false)=>Text(empty?"^(|[a-f0-9]{32})$":"^[a-f0-9]{32}$",32);
        internal static JObject Target()=>Resource(Text("^[a-fA-F0-9]{32}$",32));
        internal static JObject Bool()=>new(){["type"]="boolean"};
        internal static JObject Featured(JObject schema){schema["x-features"]=new JArray(Feature);return schema;}
        public override string Duration=>"instant";
        internal override int MaximumCreatedObjects(JObject args)=>0;
        public override IReadOnlyList<string> Channels=>new[]{"regionMembership"};
        public override IReadOnlyList<string> Requirements=>new[]{"workspace.available","region.revision.current","storage.writable"};
        public override BehaviourCatalog.Claim[] Claims(JObject a)=>CapabilityArguments.Resources(a,InputSchema).Select(id=>new BehaviourCatalog.Claim(id,"regionMembership")).ToArray();
        protected const string Rules=" Areas partition saved creation ownership within one world. Definitions and membership share Undo, temporary Keep/Discard, save and workspace export/restore. Assignment does not move objects, change their components or make a copy. The book and Maestro remain world-owned. Current runtime still uses the whole bounded world: these actions do not independently load/unload areas, change native resource residency, expand capacity or promise offscreen simulation. Physics, visibility and sound policies remain separate.";
        internal static JObject Receipt(RoomEditor e,string id)=>new(){["id"]=id,["revision"]=e.RegionRevision(id),["temporary"]=e.TemporaryRoom};
    }
    internal sealed class RegionSaveCapability:RegionCapability
    {
        public override string Id=>"world.region.save";
        public override string Label=>"Save world area";
        public override string Description=>"Create or rename one of up to 32 authored areas. Empty id, revision 0 and empty members create. To rename, read world.region and every world.region.members page at its revision and supply the complete existing member set. Saving does not assign objects; use object.region.assign."+Rules;
        public override JObject InputSchema=>Featured(Object(new JObject{["id"]=RegionId(true),["revision"]=Revision(true),["name"]=Text("^.{1,80}$",80),["members"]=List(Target(),0,RoomDocument.MaximumObjects)}));
        public override JObject OutputSchema=>Object(new JObject{["id"]=RegionId(),["revision"]=Revision(),["temporary"]=Bool()});
        public override JObject Example=>new(){["id"]="",["revision"]=0,["name"]="Café garden",["members"]=new JArray()};
        RoomRegion Definition(RoomEditor e,JObject a) {string id=(string)a["id"];return new(){id=string.IsNullOrEmpty(id)?Guid.NewGuid().ToString("N"):id,name=(string)a["name"],members=e.ReadRegion(id)?.members??Array.Empty<string>()};}
        public override bool CanRun(CapabilityContext c,JObject a,out string error) {
            error="Use empty id with revision 0 to create, or an existing area ID and current revision";
            if(!c.Editor||string.IsNullOrEmpty((string)a["id"])!=((int)a["revision"]==0))return false;
            var value=Definition(c.Editor,a);return c.Editor.PrepareRegion(value,value.id,(int)a["revision"],((JArray)a["members"]).Values<string>().ToArray(),out _,out error);
        }
        public override bool Start(CapabilityContext c,string runId,JObject a,out CapabilityOperation operation,out string error) {
            operation=null;if(!CanRun(c,a,out error))return false;var value=Definition(c.Editor,a);
            if(!c.Editor.EditRegion(value,value.id,(int)a["revision"],((JArray)a["members"]).Values<string>().ToArray(),out error))return false;
            operation=new CompletedCapability(Receipt(c.Editor,value.id));return true;
        }
    }
    internal sealed class RegionRemoveCapability:RegionCapability
    {
        public override string Id=>"world.region.remove";
        public override string Label=>"Remove unused world area";
        public override string Description=>"Remove an empty authored area using its exact ID/revision. Move creations to another area or home first. Removing an area never deletes its objects implicitly."+Rules;
        public override JObject InputSchema=>Featured(Object(new JObject{["id"]=RegionId(),["revision"]=Revision()}));
        public override JObject OutputSchema=>Object(new JObject{["id"]=RegionId(),["revision"]=Revision(true),["temporary"]=Bool()});
        public override JObject Example=>new(){["id"]=new string('a',32),["revision"]=1};
        public override bool CanRun(CapabilityContext c,JObject a,out string error){error="Room editor unavailable";return c.Editor&&c.Editor.PrepareRegion(null,(string)a["id"],(int)a["revision"],Array.Empty<string>(),out _,out error);}
        public override bool Start(CapabilityContext c,string runId,JObject a,out CapabilityOperation operation,out string error) {
            operation=null;if(!CanRun(c,a,out error)||!c.Editor.EditRegion(null,(string)a["id"],(int)a["revision"],Array.Empty<string>(),out error))return false;
            operation=new CompletedCapability(Receipt(c.Editor,(string)a["id"]));return true;
        }
    }
    internal sealed class RegionAssignCapability:RegionCapability
    {
        public override string Id=>"object.region.assign";
        public override string Label=>"Choose creation area";
        public override string Description=>"Move a creation's saved membership to one area atomically. Read object.region for its current object revision and world.region for the destination revision. Empty regionId with regionRevision 0 returns it to the home region. Deleting an object removes its membership in the same Undo operation; copying a creation or importing a portable blueprint places the new IDs in home."+Rules;
        public override JObject InputSchema=>ResourceChoice(CurrentInputs(Featured(Object(new JObject{["target"]=Target(),["revision"]=Revision(),["regionId"]=RegionId(true),["regionRevision"]=Revision(true)})),"object.region","revision",new JObject{["target"]="target"},"regionId","regionRevision"),"Area","world.regions","regionId","regionRevision","Home area");
        public override JObject OutputSchema=>Object(new JObject{["target"]=Target(),["revision"]=Revision(),["temporary"]=Bool()});
        public override JObject Example=>new(){["target"]=new string('0',32),["revision"]=1,["regionId"]="",["regionRevision"]=0};
        public override bool CanRun(CapabilityContext c,JObject a,out string error){error="Room editor unavailable";return c.Editor&&c.Editor.PrepareRegionBinding((string)a["target"],(int)a["revision"],(string)a["regionId"],(int)a["regionRevision"],out _,out error);}
        public override bool Start(CapabilityContext c,string runId,JObject a,out CapabilityOperation operation,out string error) {
            operation=null;if(!CanRun(c,a,out error)||!c.Editor.BindRegion((string)a["target"],(int)a["revision"],(string)a["regionId"],(int)a["regionRevision"],out error))return false;
            operation=new CompletedCapability(new JObject{["target"]=a["target"].DeepClone(),["revision"]=c.Editor.ObjectRevision((string)a["target"]),["temporary"]=c.Editor.TemporaryRoom});return true;
        }
    }
    internal static class RegionFacts
    {
        static BehaviourCatalog.FactDefinition Fact(string id,JObject output,string label,string description,JObject input,JObject example,Func<RoomEditor,JObject,JObject> read)=>new(id,OutputType(output),label,description,input,example,
            (c,a)=>c.Editor&&read(c.Editor,a) is JObject value?ProgramValue.Literal(value,OutputType(output)):null,features:new[]{RegionCapability.Feature});
        static JObject Id()=>RegionCapability.RegionId();
        static JObject Bool()=>RegionCapability.Bool();
        internal static BehaviourCatalog.FactDefinition Region()=>Fact("world.region",Object(new JObject{["id"]=Id(),["revision"]=Revision(),["name"]=Text(null,80),["memberCount"]=Number(0,64,true),["temporary"]=Bool()}),"World area",
            "Saved area definition and current revision. Read every world.region.members page at this revision before renaming. This is authored membership, not a regional runtime readiness report.",Object(new JObject{["id"]=Id()}),new JObject{["id"]=new string('a',32)},(e,a)=>e.ObserveRegion((string)a["id"]));
        internal static BehaviourCatalog.FactDefinition Regions()=>Fact("world.regions",Object(new JObject{["offset"]=Number(0,32,true),["total"]=Number(0,32,true),["pageSize"]=Number(3,3,true),["entries"]=List(Object(new JObject{["id"]=Id(),["revision"]=Revision(),["name"]=Text(null,80)}),0,3)}),"World areas",
            "Named authored areas ordered by stable ID, three per page. The implicit home region is available through world.identity and the empty membership choice. Advance offset by pageSize until total. Does not report loaded scene regions.",Object(new JObject{["offset"]=Number(0,32,true)}),new JObject{["offset"]=0},(e,a)=>e.ObserveRegions((int)a["offset"]));
        internal static BehaviourCatalog.FactDefinition Members()=>Fact("world.region.members",Object(new JObject{["id"]=Id(),["revision"]=Revision(),["offset"]=Number(0,64,true),["total"]=Number(0,64,true),["pageSize"]=Number(16,16,true),["members"]=List(Text(null,32),0,16)}),"Area creations",
            "Sixteen exact creation IDs per page at the area's revision. Membership is durable authored state, independent of current scene transforms.",Object(new JObject{["id"]=Id(),["offset"]=Number(0,64,true)}),new JObject{["id"]=new string('a',32),["offset"]=0},(e,a)=>e.ObserveRegionMembers((string)a["id"],(int)a["offset"]));
        internal static BehaviourCatalog.FactDefinition Retention()=>Fact("world.region.retention",Object(new JObject{["id"]=RegionCapability.RegionId(true),["memberCount"]=Number(0,64,true),["residentCount"]=Number(0,64,true),["retainedCount"]=Number(0,64,true),["missingDependencyCount"]=Number(0,64,true),["reasons"]=List(Text("^(worldOwned|held|ownership|audio|animation|physics|collisionEnvironment|unavailable|workspaceBusy|navigation|waterRoute)$",32),0,11),["unloadingSupported"]=Bool()}),"Area runtime dependencies",
            "Current native dependencies for one authored area; empty id selects home creations. Counts distinguish saved members, active native objects and objects retained by current work. Reasons include dependencies inherited through area membership and enabled physical connections, in both directions. Pending or paused audio retains its emitter. Navigation retains the authored ground sources held by its accepted or pending map; pending water detours retain discovered liquid sources until completion or cancellation. Reading does not rebuild navigation. Sleeping physics still needs its collision environment. Unavailable includes model geometry that is loading or failed. Saved structure arrangements alone do not connect objects. This is a transient diagnostic, not a saved revision or permission to unload: unloadingSupported is currently false, all accepted creations still use the bounded whole-world runtime, and observer/ground/water admission must be implemented before independent residency. Missing objects are never treated as deleted by this report.",Object(new JObject{["id"]=RegionCapability.RegionId(true)}),new JObject{["id"]=""},(e,a)=>e.ObserveRegionRetention((string)a["id"]));
        internal static BehaviourCatalog.FactDefinition Binding()=>Fact("object.region",Object(new JObject{["target"]=RegionCapability.Target(),["revision"]=Revision(),["regionId"]=RegionCapability.RegionId(true),["regionRevision"]=Revision(true),["homeRegionId"]=Id(),["temporary"]=Bool()}),"Creation area",
            "Saved region membership for one creation. Empty regionId means the home region. The book and Maestro are world-owned. A membership edit neither moves nor unloads the object.",Object(new JObject{["target"]=RegionCapability.Target()}),new JObject{["target"]=new string('0',32)},(e,a)=>e.ObserveRegionBinding((string)a["target"]));
    }
}
