// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class CaptureConstructionCapability:CapabilityModule
    {
        internal const string Feature="constructionCapture.v1";
        public override string Id=>"program.module.captureConstruction";
        public override string Label=>"Save construction as reusable module";
        public override string Duration=>"completion";
        public override IReadOnlyList<string> Requirements=>new[]{"moduleLibrary.ready","room.physics.paused","source.revision.current","storage.writable"};
        public override string Description=>"Capture 1–16 created objects into the existing reusable program library. Give every exact member ID, its current revision and a distinct slot name. Pause physics, recipe playback and animation authoring, finish drawing, and release all members first. Captures current poses and saved geometry, paint, collision/physics, drawing patches/ink/tips and recorded root motion. Imported models retain their exact content hashes; a module file does not bundle model bytes. Include both ends of every hinge; external links are refused. Book/Maestro, structure monitors, running behaviours, buttons and unsaved drafts are not object geometry and are not captured. Appearance bindings, saved sound emitters and explicit real-room collision profiles travel with their exact definitions in a closed local resource bundle. Sharing among selected members is preserved; each constructor call creates independent definitions without changing unrelated room resources. Dormant appearance bindings stay explicit. Global collision switches still apply; saved emitters do not auto-play. Nothing moves or starts. The first listed member defines the constructor origin/orientation; its existing scale remains part of the piece. Publishes ordinary editable program source exporting create(position,rotation,scale), returning fresh member IDs in slot order; deleting originals does not invalidate that source. Standard source size, 16-piece and room budgets apply; oversized captures fail explicitly. Model files stay retained while the library references them. Existing module pins never change. A dispatched library write may finish after Stop; inspect its receipt/library instead of retrying uncertainty.";
        public override JObject InputSchema {get {
            var member=CurrentInputs(Object(new JObject {["target"]=Resource(Text("^[a-f0-9]{32}$",32)),["revision"]=Revision(),["slot"]=Text("^[a-zA-Z][a-zA-Z0-9_]{0,23}$",24)}),"object.definition","revision",new JObject {["target"]="target"});
            var schema=Object(new JObject {["name"]=Text("^.{1,64}$",64),["members"]=List(member,1,16)});schema["x-features"]=new JArray(Feature,"moduleLibrary.v1",CreationPrototypeSchema.Feature);return schema;
        }}
        public override JObject OutputSchema=>ModuleWrite.ResultSchema;
        public override JObject Example=>new() {["name"]="My construction",["members"]=new JArray(new JObject {["target"]=new string('0',32),["revision"]=1,["slot"]="base"})};
        [Serializable] sealed class Request {public ConstructionMember[] members;}
        static bool Prepare(CapabilityContext context,JObject args,out ProgramModuleLibrary library,out JObject definition,out string error) {
            definition=null;library=context.Editor?context.Editor.GetComponent<RuleWorkshop>()?.Modules:null;error="The reusable module library is not ready";
            if(library==null||!library.CanWrite(out error)||!context.Editor.CaptureConstruction(JsonUtility.FromJson<Request>(args.ToString()).members,out var batch,out error))return false;
            try{definition=ConstructionModule.Definition(batch,(string)args["name"]);return library.CanPublish(definition,out error);}catch(Exception ex){error=ex.Message;return false;}
        }
        public override bool CanRun(CapabilityContext context,JObject args,out string error)=>Prepare(context,args,out _,out _,out error);
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error) {
            operation=null;if(!Prepare(context,args,out var library,out var definition,out error))return false;
            try{operation=new ModuleWrite(library,library.Publish(definition));return true;}catch(Exception ex){error=ex.Message;return false;}
        }
    }
}
