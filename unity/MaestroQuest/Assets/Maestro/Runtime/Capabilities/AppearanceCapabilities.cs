// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal abstract class AppearanceCapability:CapabilityModule
    {
        internal const string Feature="appearances.v1";
        internal static JObject IdSchema(bool empty=false)=>Text(empty?"^(|[a-f0-9]{32})$":"^[a-f0-9]{32}$",32);
        internal static JObject TargetSchema()=>Resource(Text("^(book|maestro|[a-fA-F0-9]{32})$",32));
        internal static JObject Bool()=>new(){["type"]="boolean"};
        internal static JObject Featured(JObject schema){schema["x-features"]=new JArray(Feature);return schema;}
        internal static JObject StyleSchema() {var schema=Object(new JObject{
            ["tint"]=Text("^#[a-fA-F0-9]{6}$",7),["patternMode"]=Choice("inherit","replace"),
            ["pattern"]=Object(new JObject{["kind"]=Choice("solid","checker","stripes"),["plane"]=Choice("uv","xy","xz","yz"),["secondary"]=Text("^#[a-fA-F0-9]{6}$",7),["columns"]=Number(1,32,true),["rows"]=Number(1,32,true)}),
            ["tiling"]=Object(new JObject{["x"]=Number(.01,64),["y"]=Number(.01,64)}),["offset"]=Object(new JObject{["x"]=Number(-64,64),["y"]=Number(-64,64)}),
            ["renderMode"]=Choice("inherit","opaque","cutout","blend"),["opacity"]=Number(0,1),["cutoff"]=Number(0,1),["sidedness"]=Choice("inherit","front","both"),["grain"]=Number(-1,1),["shading"]=Number(-1,.3)});schema["format"]="appearanceStyle";return schema;}
        internal static JObject DefinitionSchema() {
            var fields=(JObject)StyleSchema()["properties"];
            JObject Group(params string[] names)=>Object(new JObject(names.Select(n=>new JProperty(n,fields[n].DeepClone()))));
            return Object(new JObject{["id"]=IdSchema(),["revision"]=Revision(),["source"]=Group("tint","patternMode","pattern","tiling","offset"),["surface"]=Group("renderMode","opacity","cutoff","sidedness","grain","shading")});
        }
        internal static JObject BindingSchema(){var schema=Object(new JObject{["version"]=Number(1,1,true),["appearanceId"]=IdSchema(),["kind"]=Choice("root","part","material"),
            ["partId"]=Text("^[a-zA-Z0-9_]{0,32}$",32),["modelHash"]=Text("^(|[a-f0-9]{64})$",64),["materialIndex"]=Number(-1,1023,true),["tint"]=Text("^(|#[a-fA-F0-9]{6})$",7)});schema["format"]="appearanceBinding";return schema;}
        public override string Duration=>"instant";
        internal override int MaximumCreatedObjects(JObject args)=>0;
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.exists","target.unheld","authoring.inactive","appearance.revision.current","storage.writable"};
        public override BehaviourCatalog.Claim[] Claims(JObject args)=>CapabilityArguments.Resources(args,InputSchema).Select(id=>new BehaviourCatalog.Claim(id,"wholeTarget")).ToArray();
        protected const string Rules=" Saved source uses one Undo and temporary Keep/Discard. Identical assignments do not add Undo. Stable IDs are references, names are display text. Appearance changes never select a camera, change collision participation, acoustics, physical material contents or terrain height. Browser book pages and drawing-overlay ink retain their own display materials. Inspect facts after completion; saved source is not proof of headset appearance.";
        internal static JObject Receipt(RoomEditor editor,string id)=>new(){["id"]=id,["revision"]=editor.AppearanceRevision(id),["temporary"]=editor.TemporaryRoom};
    }
    internal sealed class AppearanceSaveCapability:AppearanceCapability
    {
        public override string Id=>"appearance.save";
        public override string Label=>"Save shared appearance";
        public override string Description=>"Create or edit one of 64 reusable appearances. Empty id and revision 0 create; otherwise read appearance.definition and every page of appearance.members, supplying the exact revision and complete member list (up to the room's 66 objects). Every affected object must be available. style is the full supported definition. Tint multiplies source pigments/textures; binding-local tint overrides replace this tint, not multiply it. patternMode=inherit preserves source textures/patterns; replace clears the base image and uses the procedural pattern. UV tiling/offset compose with imported UV transforms. renderMode=inherit requires opacity=1 and cutoff=0; opaque requires opacity=1,cutoff=0; blend uses opacity with cutoff=0; cutout uses cutoff. sidedness=inherit retains the source; grain/shading=-1 inherit, otherwise grain 0–1/shading 0–0.3. No unsupported emission, PBR, texture upload or image generation is claimed. Shared edits update all bindings including dormant ones; a local tint override remains explicit. Saving alone does not bind an object."+Rules;
        public override JObject InputSchema=>Featured(Object(new JObject{["id"]=IdSchema(true),["revision"]=Revision(true),["name"]=Text("^.{1,80}$",80),["style"]=StyleSchema(),["members"]=List(TargetSchema(),0,BehaviourProgram.MaximumResources)}));
        public override JObject OutputSchema=>Object(new JObject{["id"]=IdSchema(),["revision"]=Revision(),["temporary"]=Bool()});
        public override JObject Example=>new(){["id"]="",["revision"]=0,["name"]="Warm glass",["style"]=JObject.Parse(JsonUtility.ToJson(new AppearanceStyle{tint="#DDBBAA",renderMode="blend",opacity=.4f})),["members"]=new JArray()};
        RoomAppearance Definition(JObject a)=>new(){id=string.IsNullOrEmpty((string)a["id"])?Guid.NewGuid().ToString("N"):(string)a["id"],name=(string)a["name"],style=JsonUtility.FromJson<AppearanceStyle>(a["style"].ToString())};
        public override bool CanRun(CapabilityContext c,JObject a,out string error) {
            error="Use empty id and revision 0 to create, or an existing appearance ID and exact revision";
            if(string.IsNullOrEmpty((string)a["id"])!=((int)a["revision"]==0)||!c.Editor)return false;
            var p=Definition(a);return c.Editor.PrepareAppearance(p,p.id,(int)a["revision"],((JArray)a["members"]).Values<string>().ToArray(),out _,out error);
        }
        public override bool Start(CapabilityContext c,string runId,JObject a,out CapabilityOperation operation,out string error) {
            operation=null;if(!CanRun(c,a,out error))return false;var p=Definition(a);
            if(!c.Editor.EditAppearance(p,p.id,(int)a["revision"],((JArray)a["members"]).Values<string>().ToArray(),out error))return false;
            operation=new CompletedCapability(Receipt(c.Editor,p.id));return true;
        }
    }
    internal sealed class AppearanceRemoveCapability:AppearanceCapability
    {
        public override string Id=>"appearance.remove";
        public override string Label=>"Remove unused appearance";
        public override string Description=>"Remove an unused definition using appearance.definition's exact ID/revision. Refuses while any active or dormant binding refers to it; explicitly unbind those first."+Rules;
        public override JObject InputSchema=>Featured(Object(new JObject{["id"]=IdSchema(),["revision"]=Revision()}));
        public override JObject OutputSchema=>Object(new JObject{["id"]=IdSchema(),["revision"]=Revision(true),["temporary"]=Bool()});
        public override JObject Example=>new(){["id"]=new string('0',32),["revision"]=1};
        public override bool CanRun(CapabilityContext c,JObject a,out string error){error="Room editor unavailable";return c.Editor&&c.Editor.PrepareAppearance(null,(string)a["id"],(int)a["revision"],Array.Empty<string>(),out _,out error);}
        public override bool Start(CapabilityContext c,string runId,JObject a,out CapabilityOperation operation,out string error) {
            operation=null;if(!CanRun(c,a,out error)||!c.Editor.EditAppearance(null,(string)a["id"],(int)a["revision"],Array.Empty<string>(),out error))return false;
            operation=new CompletedCapability(Receipt(c.Editor,(string)a["id"]));return true;
        }
    }
    internal sealed class AppearanceBindCapability:AppearanceCapability
    {
        public override string Id=>"object.appearance.bind";
        public override string Label=>"Choose object appearance";
        public override string Description=>"Read object.appearances for exact object revision and saved bindings, object.appearance.targets for supported addresses, and appearance.definition for its current revision. assign reuses a shared definition; copy makes an independent definition and binds it atomically; remove unbinds this exact saved address/appearance even if dormant. Root uses empty partId/modelHash and materialIndex=-1. Part uses an exact recipe partId; material uses exact loaded model hash plus source material index, never display name or traversal order. A part/material binding overrides root completely. A bound recipe part uses the appearance tint as its pigment and ignores whole-object tint; the ordinary recipe colour editor writes that binding local tint. Pattern edits that would be hidden by a replacement pattern refuse with an explanation. Missing/replaced model slots remain dormant until explicitly rebound; assignment refuses unavailable targets. tint='' inherits definition tint; an RGB hex overrides tint for this binding only. Ordinary Paint writes the root binding's local tint without editing the shared definition; assign tint='' restores shared tint. Removing root retains its effective tint as ordinary object paint; other surface values revert to source. copy includes the provided local tint in the independent definition. Bindings do not extend to selection marks or surface-drawing ink."+Rules;
        public override JObject InputSchema=>ResourceChoice(CurrentInputs(Featured(Object(new JObject{["operation"]=Choice("assign","copy","remove"),["target"]=TargetSchema(),["revision"]=Revision(),["appearanceRevision"]=Revision(),["binding"]=BindingSchema()})),"object.definition","revision",new JObject {["target"]="target"}),"Appearance","appearance.library","binding.appearanceId","appearanceRevision");
        public override JObject OutputSchema=>Object(new JObject{["target"]=TargetSchema(),["revision"]=Revision(),["appearanceId"]=IdSchema(),["appearanceRevision"]=Revision(),["temporary"]=Bool()});
        public override JObject Example=>new(){["operation"]="assign",["target"]="maestro",["revision"]=1,["appearanceRevision"]=1,["binding"]=JObject.Parse(JsonUtility.ToJson(new AppearanceBinding{appearanceId=new string('0',32)}))};
        static AppearanceBinding Binding(JObject a)=>JsonUtility.FromJson<AppearanceBinding>(a["binding"].ToString());
        public override bool CanRun(CapabilityContext c,JObject a,out string error){error="Room editor unavailable";return c.Editor&&c.Editor.PrepareAppearanceBinding((string)a["target"],(int)a["revision"],Binding(a),(int)a["appearanceRevision"],(string)a["operation"]=="remove",(string)a["operation"]=="copy",out _,out _,out error);}
        public override bool Start(CapabilityContext c,string runId,JObject a,out CapabilityOperation operation,out string error) {
            operation=null;if(!CanRun(c,a,out error))return false;var binding=Binding(a);string target=(string)a["target"];
            if(!c.Editor.BindAppearance(target,(int)a["revision"],binding,(int)a["appearanceRevision"],(string)a["operation"]=="remove",(string)a["operation"]=="copy",out error))return false;
            string id=(string)a["operation"]=="remove"?binding.appearanceId:c.Editor.Read(target).appearanceBindings.Single(b=>b.Key==binding.Key).appearanceId;
            operation=new CompletedCapability(new JObject{["target"]=target,["revision"]=c.Editor.ObjectRevision(target),["appearanceId"]=id,["appearanceRevision"]=c.Editor.AppearanceRevision(id),["temporary"]=c.Editor.TemporaryRoom});return true;
        }
    }
    internal static class AppearanceFacts
    {
        static ProgramDataType Type(string json)=>ProgramDataType.Read(JObject.Parse(json));
        static BehaviourCatalog.FactDefinition Fact(string id,ProgramDataType type,string label,string description,JObject input,JObject example,Func<RoomEditor,JObject,JObject> read)=>new(id,type,label,description,input,example,
            (c,a)=>c.Editor&&read(c.Editor,a) is JObject value?ProgramValue.Literal(value,type):null,features:new[]{AppearanceCapability.Feature});
        internal static BehaviourCatalog.FactDefinition Definitions()=>Fact("appearance.library",Type("{\"record\":{\"offset\":\"number\",\"total\":\"number\",\"pageSize\":\"number\",\"entries\":{\"list\":{\"record\":{\"id\":\"text\",\"revision\":\"number\",\"name\":\"text\"}}}}}"),"Appearances",
            "Reusable saved appearances ordered by stable ID. Three entries per page; advance offset by pageSize until total. Names are display text.",Object(new JObject{["offset"]=Number(0,64,true)}),new JObject{["offset"]=0},(e,a)=>e.ObserveAppearances((int)a["offset"]));
        internal static BehaviourCatalog.FactDefinition Definition()=>Fact("appearance.definition",OutputType(AppearanceCapability.DefinitionSchema()),"Appearance definition",
            "Read the full supported saved style and current revision. Combine the source and surface fields into style for appearance.save. The name is available from appearance.library. Members are separately paged through appearance.members to fit bounded program values.",Object(new JObject{["id"]=AppearanceCapability.IdSchema()}),new JObject{["id"]=new string('0',32)},(e,a)=>e.ObserveAppearance((string)a["id"]));
        internal static BehaviourCatalog.FactDefinition Members()=>Fact("appearance.members",Type("{\"record\":{\"id\":\"text\",\"revision\":\"number\",\"offset\":\"number\",\"total\":\"number\",\"pageSize\":\"number\",\"members\":{\"list\":\"text\"}}}"),"Appearance members",
            "Sixteen exact object IDs per page, including dormant bindings; start offset 0. Before saving a shared change read all pages at the same definition revision. Save checks current complete membership and ownership again.",Object(new JObject{["id"]=AppearanceCapability.IdSchema(),["offset"]=Number(0,66,true)}),new JObject{["id"]=new string('0',32),["offset"]=0},(e,a)=>e.ObserveAppearanceMembers((string)a["id"],(int)a["offset"]));
        internal static BehaviourCatalog.FactDefinition Bindings()=>Fact("object.appearances",Type("{\"record\":{\"target\":\"text\",\"revision\":\"number\",\"offset\":\"number\",\"total\":\"number\",\"pageSize\":\"number\",\"bindings\":{\"list\":{\"record\":{\"appearanceId\":\"text\",\"appearanceRevision\":\"number\",\"kind\":\"text\",\"partId\":\"text\",\"modelHash\":\"text\",\"materialIndex\":\"number\",\"tint\":\"text\",\"state\":\"text\"}}},\"temporary\":\"boolean\"}}"),"Object appearances",
            "Two saved bindings per page, including dormant bindings and explicit local tint overrides. A specific part/material wins over root; root tint is not applied a second time. State describes binding availability, not physical interaction or visibility through other objects.",Object(new JObject{["target"]=AppearanceCapability.TargetSchema(),["offset"]=Number(0,33,true)}),new JObject{["target"]="maestro",["offset"]=0},(e,a)=>e.ObserveObjectAppearances((string)a["target"],(int)a["offset"]));
        internal static BehaviourCatalog.FactDefinition Targets()=>Fact("object.appearance.targets",Type("{\"record\":{\"target\":\"text\",\"revision\":\"number\",\"offset\":\"number\",\"total\":\"number\",\"pageSize\":\"number\",\"targets\":{\"list\":{\"record\":{\"kind\":\"text\",\"partId\":\"text\",\"modelHash\":\"text\",\"materialIndex\":\"number\",\"state\":\"text\"}}}}}"),"Appearance targets",
            "Three stable root/part/import-material addresses per page, in that order. Source material indexes come from the loaded asset, not renderer order. Only state=active can receive a new binding. Pending/unavailable models require a later observation.",Object(new JObject{["target"]=AppearanceCapability.TargetSchema(),["offset"]=Number(0,1057,true)}),new JObject{["target"]="maestro",["offset"]=0},(e,a)=>e.ObserveAppearanceTargets((string)a["target"],(int)a["offset"]));
    }
}
