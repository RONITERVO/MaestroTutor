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
        public override string Duration=>"completion";
        internal override int MaximumCreatedObjects(JObject args)=>0;
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.exists","target.unheld","authoring.inactive","environment.revision.current","storage.writable"};
        public override BehaviourCatalog.Claim[] Claims(JObject args)=>CapabilityArguments.Resources(args,InputSchema).Select(id=>new BehaviourCatalog.Claim(id,"wholeTarget")).ToArray();
        protected const string Rules=" Stable exact IDs are references; names are display text. Changes use the saved room journal with one Undo and temporary-play Keep/Discard semantics. Already-identical values do not add Undo. All affected targets must be released and free of authoring or competing motion. The entire edit refuses on stale revision, changed membership, missing target, unavailable support during running physics, or new scanned-surface penetration. It never moves an object. Changed targets lose previous throw speeds. Visual depth/opacity and acoustics remain independent; this setting changes collision participation only. The global real-room switch is an upper bound: a profile cannot re-enable it. Virtual collisions remain active. Virtual-only simulation needs accepted authored ground; current bounds are columns up to 16 metres above and 0.25 metres below those surfaces, not unlimited space."+PersistedEditOperation.Persistence;
        protected static bool StartProfile(CapabilityContext context,RoomEnvironmentProfile definition,string id,int revision,string[] members,
            out CapabilityOperation operation,out string error)
        {
            var editor=context.Editor;bool temporary=editor.TemporaryRoom;RoomEditor.PendingPersistedEdit pending=null;
            return PersistedEditOperation.Start(editor,null,
                (out RoomEditor.PendingPersistedEdit edit,out string issue)=>{
                    bool ready=editor.BeginEnvironment(definition,id,revision,members,out edit,out issue);pending=edit;return ready;
                },out operation,out error,validate:(out string issue)=>editor.CanEditEnvironment(definition,id,revision,members,out issue),
                output:()=>new JObject{["id"]=id,["revision"]=pending.AcceptedEnvironmentRevision(id),["temporary"]=temporary});
        }
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
            var p=Definition(a);return c.Editor.CanEditEnvironment(p,p.id,(int)a["revision"],((JArray)a["members"]).Values<string>().ToArray(),out error);
        }
        public override bool Start(CapabilityContext c,string runId,JObject a,out CapabilityOperation operation,out string error) {
            operation=null;if(!CanRun(c,a,out error))return false;var p=Definition(a);
            return StartProfile(c,p,p.id,(int)a["revision"],((JArray)a["members"]).Values<string>().ToArray(),out operation,out error);
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
        public override bool CanRun(CapabilityContext c,JObject a,out string error) {error="Room editor unavailable";return c.Editor&&c.Editor.CanEditEnvironment(null,(string)a["id"],(int)a["revision"],Array.Empty<string>(),out error);}
        public override bool Start(CapabilityContext c,string runId,JObject a,out CapabilityOperation operation,out string error) {
            operation=null;if(!CanRun(c,a,out error))return false;
            return StartProfile(c,null,(string)a["id"],(int)a["revision"],Array.Empty<string>(),out operation,out error);
        }
    }
    internal sealed class EnvironmentAssignCapability:EnvironmentProfileCapability
    {
        internal override IEnumerable<string> NativeEntities(JObject args)=>CapabilityArguments.Resources(args,InputSchema);
        public override string Id=>"object.environment.assign";
        public override string Label=>"Choose object environment";
        public override string Description=>"Assign a saved environment profile to the book, Maestro or a creation, including all its assembly/import child colliders. Read object.environment.settings for the saved target assignment/revision and environment.profile for the selected profile revision. Saved settings are readable while unloaded, but assignment still requires native readiness. An empty profileId with profileRevision 0 explicitly restores inherited real-room participation. One object's assignment does not edit the reusable profile or other members. It does not start physics or walking."+Rules;
        public override JObject InputSchema=>ResourceChoice(CurrentInputs(Featured(Object(new JObject{["target"]=TargetSchema(),["revision"]=Revision(),["profileId"]=ProfileId(true),["profileRevision"]=Revision(true)})),"object.environment.settings","revision",new JObject{["target"]="target"},"profileId","profileRevision"),"Collision profile","environment.profiles","profileId","profileRevision","Inherit room collisions");
        public override JObject OutputSchema=>Object(new JObject{["target"]=TargetSchema(),["revision"]=Revision(),["temporary"]=Bool()});
        public override JObject Example=>new(){["target"]="maestro",["revision"]=1,["profileId"]="",["profileRevision"]=0};
        public override bool CanRun(CapabilityContext c,JObject a,out string error) {error="Room editor unavailable";return c.Editor&&c.Editor.CanBindEnvironment((string)a["target"],(int)a["revision"],(string)a["profileId"],(int)a["profileRevision"],out error);}
        public override bool Start(CapabilityContext c,string runId,JObject a,out CapabilityOperation operation,out string error) {
            operation=null;if(!CanRun(c,a,out error))return false;
            var editor=c.Editor;string target=(string)a["target"],profile=(string)a["profileId"];
            int revision=(int)a["revision"],profileRevision=(int)a["profileRevision"];bool temporary=editor.TemporaryRoom;
            RoomEditor.PendingPersistedEdit pending=null;
            return PersistedEditOperation.Start(editor,target,
                (out RoomEditor.PendingPersistedEdit edit,out string issue)=>{
                    bool ready=editor.BeginEnvironmentBinding(target,revision,profile,profileRevision,out edit,out issue);pending=edit;return ready;
                },out operation,out error,validate:(out string issue)=>editor.CanBindEnvironment(target,revision,profile,profileRevision,out issue),
                output:()=>new JObject{["target"]=target,["revision"]=pending.AcceptedObjectRevision(target),["temporary"]=temporary});
        }
    }
    internal static class EnvironmentProfileFacts
    {
        static ProgramDataType SettingsType()=>OutputType(Object(new JObject{["target"]=EnvironmentProfileCapability.TargetSchema(),
            ["revision"]=Revision(),["profileId"]=EnvironmentProfileCapability.ProfileId(true),["profileRevision"]=Revision(true),
            ["realCollisions"]=EnvironmentProfileCapability.Bool(),["temporary"]=EnvironmentProfileCapability.Bool()}));
        static ProgramDataType BindingType()=>Type("{\"record\":{\"target\":\"text\",\"revision\":\"number\",\"profileId\":\"text\",\"profileRevision\":\"number\",\"realCollisions\":\"boolean\",\"state\":{\"record\":{\"effectiveRealCollisions\":\"boolean\",\"ready\":\"boolean\",\"inside\":\"boolean\",\"admitted\":\"boolean\"}},\"temporary\":\"boolean\"}}");
        static ProgramDataType ProfileType()=>Type("{\"record\":{\"id\":\"text\",\"revision\":\"number\",\"name\":\"text\",\"realCollisions\":\"boolean\",\"members\":{\"list\":\"text\"},\"temporary\":\"boolean\"}}");
        static ProgramDataType ProfilesType()=>Type("{\"record\":{\"offset\":\"number\",\"total\":\"number\",\"pageSize\":\"number\",\"entries\":{\"list\":{\"record\":{\"id\":\"text\",\"revision\":\"number\",\"name\":\"text\",\"realCollisions\":\"boolean\"}}}}}");
        static ProgramDataType Type(string json)=>ProgramDataType.Read(JObject.Parse(json));
        internal static BehaviourCatalog.FactDefinition Settings()=>new("object.environment.settings",SettingsType(),
            "Saved object environment","Read canonical collision-profile assignment and exact saved object/profile revisions, including the temporary fork. realCollisions is the saved request: an empty profileId inherits real-room participation, subject to the global switch. It is not effective collision, ground availability, admission or permission to move. This read does not load native objects, reserve resources or change the room. It remains available for deliberately unloaded, inactive or missing native views; a missing saved target is unavailable. Use object.presence for native lifecycle and object.environment for live readiness. Assignment still requires the existing physical admission checks.",Object(new JObject{["target"]=EnvironmentProfileCapability.TargetSchema()}),new JObject{["target"]="maestro"},
            (c,a)=>c.Editor?.ObserveEnvironmentSettings((string)a["target"]) is JObject value?ProgramValue.Literal(value,SettingsType()):null,features:new[]{EnvironmentProfileCapability.Feature});
        internal static BehaviourCatalog.FactDefinition Binding()=>new("object.environment",BindingType(),
            "Object environment","Live profile binding and effective real-room participation. Unloaded or inactive native views are unavailable; read object.environment.settings for their saved assignment. Empty profileId inherits the global switch. ready checks this actor's environment; inside checks its current centre against that environment's bounds. admitted means environment admission and running physics, not that this body's geometry, ownership or dynamic mode permits motion. Does not test a complete walking route or overlap.",Object(new JObject{["target"]=EnvironmentProfileCapability.TargetSchema()}),new JObject{["target"]="maestro"},
            (c,a)=>c.Editor?.ObserveEnvironmentBinding((string)a["target"]) is JObject value?ProgramValue.Literal(value,BindingType()):null,features:new[]{EnvironmentProfileCapability.Feature});
        internal static BehaviourCatalog.FactDefinition Profile()=>new("environment.profile",ProfileType(),
            "Environment profile","Read the exact saved profile revision and all currently bound object IDs. Names are display metadata. Shared changes require every listed member as an explicit claim. Missing profiles are unavailable; never substitute by name.",Object(new JObject{["id"]=EnvironmentProfileCapability.ProfileId()}),new JObject{["id"]=new string('0',32)},
            (c,a)=>c.Editor?.ObserveEnvironmentProfile((string)a["id"]) is JObject value?ProgramValue.Literal(value,ProfileType()):null,features:new[]{EnvironmentProfileCapability.Feature});
        internal static BehaviourCatalog.FactDefinition Profiles()=>new("environment.profiles",ProfilesType(),
            "Environment profiles","Read three reusable environment profiles at a time, ordered by stable ID. Start offset 0 and advance by pageSize until total. Definition pages do not claim targets or change the room.",Object(new JObject{["offset"]=Number(0,16,true)}),new JObject{["offset"]=0},
            (c,a)=>c.Editor?.ObserveEnvironmentProfiles((int)a["offset"]) is JObject value?ProgramValue.Literal(value,ProfilesType()):null,features:new[]{EnvironmentProfileCapability.Feature});
    }
}
