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
    internal abstract class EnvironmentProfileCapability:CapabilityModule
    {
        internal const string Feature="environmentProfiles.v1";
        internal static JObject ProfileId(bool empty=false)=>Text(empty?"^(|[a-f0-9]{32})$":"^[a-f0-9]{32}$",32);
        internal static JObject TargetSchema()=>Resource(Text("^(book|maestro|[a-fA-F0-9]{32})$",32));
        internal static JObject Bool()=>new(){["type"]="boolean"};
        internal static JObject Featured(JObject schema){schema["x-features"]=new JArray(Feature);return schema;}
        public override string Duration=>"instant";
        internal override int MaximumCreatedObjects(JObject args)=>0;
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.exists","target.unheld","authoring.inactive","environment.revision.current","storage.writable"};
        public override BehaviourCatalog.Claim[] Claims(JObject args)=>CapabilityArguments.Resources(args,InputSchema).Select(id=>new BehaviourCatalog.Claim(id,"wholeTarget")).ToArray();
        protected const string Rules=" Stable exact IDs are references; names are display text. Changes use the saved room journal with one Undo and temporary-play Keep/Discard semantics. Already-identical values do not add Undo. All affected targets must be released and free of authoring or competing motion. The entire edit refuses on stale revision, changed membership, missing target, unavailable support during running physics, or new scanned-surface penetration. It never moves an object. Changed targets lose previous throw speeds. Visual depth/opacity and acoustics remain independent; this setting changes collision participation only. The global real-room switch is an upper bound: a profile cannot re-enable it. Virtual collisions remain active. Virtual-only simulation needs accepted authored ground; current bounds are columns up to 16 metres above and 0.25 metres below those surfaces, not unlimited space.";
        internal static JObject Receipt(RoomEditor editor,string id)=>new(){["id"]=id,["revision"]=editor.EnvironmentRevision(id),["temporary"]=editor.TemporaryRoom};
    }
    internal sealed class EnvironmentProfileSaveCapability:EnvironmentProfileCapability
    {
        internal override IEnumerable<string> NativeEntities(JObject args)=>CapabilityArguments.Resources(args,InputSchema);
        public override string Id=>"environment.profile.save";
        public override string Label=>"Save environment profile";
        public override string Description=>"Create or edit one of up to 16 reusable environment profiles, each shared by at most 16 objects. Empty id and revision 0 create a new stable ID. Otherwise read environment.profile and supply its exact revision and every bound member. realCollisions=false lets those objects use virtual terrain below or beyond the real floor/walls. realCollisions=true inherits the global real-room switch. Read environment.profiles to find definitions. Saving does not assign new members; use object.environment.assign."+Rules;
        public override JObject InputSchema=>Featured(Object(new JObject{["id"]=ProfileId(true),["revision"]=Revision(true),["name"]=Text("^.{1,80}$",80),["realCollisions"]=Bool(),["members"]=List(TargetSchema(),0,16)}));
        public override JObject OutputSchema=>Object(new JObject{["id"]=ProfileId(),["revision"]=Revision(),["temporary"]=Bool()});
        public override JObject Example=>new(){["id"]="",["revision"]=0,["name"]="Virtual terrain characters",["realCollisions"]=false,["members"]=new JArray()};
        RoomEnvironmentProfile Definition(JObject a)=>new(){id=string.IsNullOrEmpty((string)a["id"])?Guid.NewGuid().ToString("N"):(string)a["id"],name=(string)a["name"],realCollisions=(bool)a["realCollisions"]};
        public override bool CanRun(CapabilityContext c,JObject a,out string error) {
            error="Use empty id with revision 0 to create, or an existing profile ID and current revision";
            if(string.IsNullOrEmpty((string)a["id"])!=((int)a["revision"]==0)||!c.Editor)return false;
            var p=Definition(a);return c.Editor.PrepareEnvironment(p,p.id,(int)a["revision"],((JArray)a["members"]).Values<string>().ToArray(),out _,out error);
        }
        public override bool Start(CapabilityContext c,string runId,JObject a,out CapabilityOperation operation,out string error) {
            operation=null;if(!CanRun(c,a,out error))return false;var p=Definition(a);
            if(!c.Editor.EditEnvironment(p,p.id,(int)a["revision"],((JArray)a["members"]).Values<string>().ToArray(),out error))return false;
            operation=new CompletedCapability(Receipt(c.Editor,p.id));return true;
        }
    }
    internal sealed class EnvironmentProfileRemoveCapability:EnvironmentProfileCapability
    {
        public override string Id=>"environment.profile.remove";
        public override string Label=>"Remove unused environment profile";
        public override string Description=>"Remove an unused environment profile by exact ID/revision from environment.profile. Refuses while any object still refers to it; explicitly reassign those objects first. Objects are never silently returned to physical-room collision."+Rules;
        public override JObject InputSchema=>Featured(Object(new JObject{["id"]=ProfileId(),["revision"]=Revision()}));
        public override JObject OutputSchema=>Object(new JObject{["id"]=ProfileId(),["revision"]=Revision(true),["temporary"]=Bool()});
        public override JObject Example=>new(){["id"]=new string('0',32),["revision"]=1};
        public override bool CanRun(CapabilityContext c,JObject a,out string error) {error="Room editor unavailable";return c.Editor&&c.Editor.PrepareEnvironment(null,(string)a["id"],(int)a["revision"],Array.Empty<string>(),out _,out error);}
        public override bool Start(CapabilityContext c,string runId,JObject a,out CapabilityOperation operation,out string error) {
            operation=null;if(!CanRun(c,a,out error)||!c.Editor.EditEnvironment(null,(string)a["id"],(int)a["revision"],Array.Empty<string>(),out error))return false;
            operation=new CompletedCapability(Receipt(c.Editor,(string)a["id"]));return true;
        }
    }
    internal sealed class EnvironmentAssignCapability:EnvironmentProfileCapability
    {
        internal override IEnumerable<string> NativeEntities(JObject args)=>CapabilityArguments.Resources(args,InputSchema);
        public override string Id=>"object.environment.assign";
        public override string Label=>"Choose object environment";
        public override string Description=>"Assign a saved environment profile to the book, Maestro or a creation, including all its assembly/import child colliders. Read object.environment for target revision and environment.profile for selected profile revision. An empty profileId with profileRevision 0 explicitly restores inherited real-room participation. One object's assignment does not edit the reusable profile or other members. It does not start physics or walking."+Rules;
        public override JObject InputSchema=>ResourceChoice(CurrentInputs(Featured(Object(new JObject{["target"]=TargetSchema(),["revision"]=Revision(),["profileId"]=ProfileId(true),["profileRevision"]=Revision(true)})),"object.environment","revision",new JObject{["target"]="target"},"profileId","profileRevision"),"Collision profile","environment.profiles","profileId","profileRevision","Inherit room collisions");
        public override JObject OutputSchema=>Object(new JObject{["target"]=TargetSchema(),["revision"]=Revision(),["temporary"]=Bool()});
        public override JObject Example=>new(){["target"]="maestro",["revision"]=1,["profileId"]="",["profileRevision"]=0};
        public override bool CanRun(CapabilityContext c,JObject a,out string error) {error="Room editor unavailable";return c.Editor&&c.Editor.PrepareEnvironmentBinding((string)a["target"],(int)a["revision"],(string)a["profileId"],(int)a["profileRevision"],out _,out error);}
        public override bool Start(CapabilityContext c,string runId,JObject a,out CapabilityOperation operation,out string error) {
            operation=null;if(!CanRun(c,a,out error)||!c.Editor.BindEnvironment((string)a["target"],(int)a["revision"],(string)a["profileId"],(int)a["profileRevision"],out error))return false;
            operation=new CompletedCapability(new JObject{["target"]=a["target"].DeepClone(),["revision"]=c.Editor.ObjectRevision((string)a["target"]),["temporary"]=c.Editor.TemporaryRoom});return true;
        }
    }
    internal static class EnvironmentProfileFacts
    {
        static ProgramDataType BindingType()=>Type("{\"record\":{\"target\":\"text\",\"revision\":\"number\",\"profileId\":\"text\",\"profileRevision\":\"number\",\"realCollisions\":\"boolean\",\"state\":{\"record\":{\"effectiveRealCollisions\":\"boolean\",\"ready\":\"boolean\",\"inside\":\"boolean\",\"admitted\":\"boolean\"}},\"temporary\":\"boolean\"}}");
        static ProgramDataType ProfileType()=>Type("{\"record\":{\"id\":\"text\",\"revision\":\"number\",\"name\":\"text\",\"realCollisions\":\"boolean\",\"members\":{\"list\":\"text\"},\"temporary\":\"boolean\"}}");
        static ProgramDataType ProfilesType()=>Type("{\"record\":{\"offset\":\"number\",\"total\":\"number\",\"pageSize\":\"number\",\"entries\":{\"list\":{\"record\":{\"id\":\"text\",\"revision\":\"number\",\"name\":\"text\",\"realCollisions\":\"boolean\"}}}}}");
        static ProgramDataType Type(string json)=>ProgramDataType.Read(JObject.Parse(json));
        internal static BehaviourCatalog.FactDefinition Binding()=>new("object.environment",BindingType(),
            "Object environment","Saved profile binding and effective real-room participation. Empty profileId inherits the global switch. ready checks this actor's environment; inside checks its current centre against that environment's bounds. admitted means environment admission and running physics, not that this body's geometry, ownership or dynamic mode permits motion. Does not test a complete walking route or overlap.",Object(new JObject{["target"]=EnvironmentProfileCapability.TargetSchema()}),new JObject{["target"]="maestro"},
            (c,a)=>c.Editor?.ObserveEnvironmentBinding((string)a["target"]) is JObject value?ProgramValue.Literal(value,BindingType()):null,features:new[]{EnvironmentProfileCapability.Feature});
        internal static BehaviourCatalog.FactDefinition Profile()=>new("environment.profile",ProfileType(),
            "Environment profile","Read the exact saved profile revision and all currently bound object IDs. Names are display metadata. Shared changes require every listed member as an explicit claim. Missing profiles are unavailable; never substitute by name.",Object(new JObject{["id"]=EnvironmentProfileCapability.ProfileId()}),new JObject{["id"]=new string('0',32)},
            (c,a)=>c.Editor?.ObserveEnvironmentProfile((string)a["id"]) is JObject value?ProgramValue.Literal(value,ProfileType()):null,features:new[]{EnvironmentProfileCapability.Feature});
        internal static BehaviourCatalog.FactDefinition Profiles()=>new("environment.profiles",ProfilesType(),
            "Environment profiles","Read three reusable environment profiles at a time, ordered by stable ID. Start offset 0 and advance by pageSize until total. Definition pages do not claim targets or change the room.",Object(new JObject{["offset"]=Number(0,16,true)}),new JObject{["offset"]=0},
            (c,a)=>c.Editor?.ObserveEnvironmentProfiles((int)a["offset"]) is JObject value?ProgramValue.Literal(value,ProfilesType()):null,features:new[]{EnvironmentProfileCapability.Feature});
    }
}
