// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class RoomToolsCapability:CapabilityModule
    {
        internal const string Feature="physicalTools.v1";
        public override string Id=>"room.tools.set";
        public override string Label=>"Show or hide physical tools";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[]{"tools.state.current","workspace.available","tools.released"};
        public override string Description=>"Show or hide a named solid 3D authoring tray, or all trays, using the exact stateId from room.tools. Creation, animation, behaviours, imports, physics, avatar movement and controller bindings start hidden so conversation stays central. Showing retains the tray's current position; use the existing book/palm Recall if it is out of reach. Hiding changes only the tray's presentation and input surfaces: room objects, drawing modes, poses, animations, programs, imports and physics continue. Release a held tray and finish/cancel armed physical surface placement before hiding it; hide-all refuses atomically if any requested tray cannot hide. Book controls and palm Recall stay available. This is transient workspace view state, not a room edit: no save or Undo, no automatic action on show, and a new workspace/relaunch starts hidden. A completed receipt describes that moment; Stop does not reverse a completed visibility change. Replayed receipts never reopen tools. Discover/run the same action from chat, book or a program; no separate provider tool is needed.";
        public override JObject InputSchema {get{
            var schema=CurrentInputs(Object(new JObject {["stateId"]=Text("^[a-f0-9]{32}$",32),["tray"]=Choice(RoomToolVisibility.Names.Concat(new[]{"all"}).ToArray()),["visible"]=new JObject {["type"]="boolean"}}),"room.tools","stateId");schema["x-features"]=new JArray(Feature);return schema;
        }}
        static JObject Flags(){var fields=new JObject();foreach(var name in RoomToolVisibility.Names)fields[name]=new JObject {["type"]="boolean"};return Object(fields);}
        public override JObject OutputSchema=>Object(new JObject {["stateId"]=Text("^[a-f0-9]{32}$",32),["visible"]=Flags(),["held"]=Flags()});
        public override JObject Example=>new(){["stateId"]=new string('0',32),["tray"]="creation",["visible"]=true};
        static RoomToolVisibility Tools(CapabilityContext context)=>context.Editor?context.Editor.GetComponent<RoomToolVisibility>():null;
        public override bool CanRun(CapabilityContext context,JObject args,out string error){var tools=Tools(context);error="Physical tools are unavailable";return tools&&tools.CanSet((string)args["stateId"],(string)args["tray"],(bool)args["visible"],out error);}
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error){
            operation=null;if(!CanRun(context,args,out error)||!Tools(context).Set((string)args["stateId"],(string)args["tray"],(bool)args["visible"],out var result,out error))return false;
            operation=new CompletedCapability(result);return true;
        }
        internal static BehaviourCatalog.FactDefinition Fact(){
            var flags=new JObject();foreach(var name in RoomToolVisibility.Names)flags[name]="boolean";
            var type=ProgramDataType.Read(new JObject {["record"]=new JObject {["stateId"]="text",["visible"]=new JObject {["record"]=flags},["held"]=new JObject {["record"]=flags.DeepClone()}}});
            return new("room.tools",type,"Physical tool visibility","Read the current visibility and actual grip ownership of the seven optional authoring trays. This read does not open tools or start their actions. stateId protects visibility intent, not future grip state; native execution checks holding and placement again. Visibility is transient and independent of room/program data.",null,null,(context,args)=>{var tools=context.Editor?context.Editor.GetComponent<RoomToolVisibility>():null;return tools?ProgramValue.Literal(tools.Observe(),type):null;},features:new[]{Feature});
        }
    }
}
