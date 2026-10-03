// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal sealed class ConstructionManipulationCapability:CapabilityModule {
        internal const string Feature="constructionManipulation.v1";
        public override string Id=>"room.selection.manipulate";
        public override string Label=>"Show or hide the construction move handle";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[]{"selection.state.current","room.physics.paused","construction.released"};
        public override string Description=>"Show or hide a solid grab handle for the current ordered construction selection. Pass room.selection stateId and its exact member IDs. Showing requires 1–16 idle pieces, physics paused, drawing tools away and collection finished. It does not move/save anything or own objects until the handle is gripped. Grip moves/rotates the group; two hands resize it. Release applies the shared object.layout.transform action with one save/Undo. Invalid transforms stay at the last valid preview. Failed save restores all starting poses and records failure; the user can try the move again. Pausing, recall, drawing, changed selection/members or interrupted grips cancel a preview and close the handle without saving it. Direct member grips are unavailable during a group drag; release the handle first. Hiding refuses a held handle. Saved structure baselines, geometry and animations are unchanged. This is an arrangement tool, not a physical weld or socket snap. A completed show receipt cannot be cancelled to hide it: call this action with visible=false.";
        public override JObject InputSchema {get{var schema=CurrentInputs(Object(new JObject {["stateId"]=Text("^[a-f0-9]{32}$",32),["members"]=List(Resource(Text("^[a-f0-9]{32}$",32)),0,16),["visible"]=new JObject {["type"]="boolean"}}),"room.selection","stateId");schema["format"]="constructionSelection";schema["x-features"]=new JArray(Feature);return schema;}}
        public override JObject OutputSchema=>ViewSchema();
        internal static JObject ViewSchema()=>Object(new JObject {["stateId"]=Text("^[a-f0-9]{32}$",32),["visible"]=new JObject {["type"]="boolean"},["holding"]=new JObject {["type"]="boolean"},["error"]=Text("^.{0,240}$",240)});
        public override JObject Example=>new() {["stateId"]=new string('0',32),["members"]=new JArray(new string('1',32)),["visible"]=true};
        public override bool CanRun(CapabilityContext context,JObject args,out string error)=>context.Editor.CanShowConstructionManipulator((string)args["stateId"],((JArray)args["members"]).Values<string>().ToArray(),(bool)args["visible"],out error);
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error){operation=null;if(!context.Editor.ShowConstructionManipulator((string)args["stateId"],((JArray)args["members"]).Values<string>().ToArray(),(bool)args["visible"],out error))return false;operation=new CompletedCapability(JObject.FromObject(context.Editor.ObserveConstructionManipulation()));return true;}
        internal static BehaviourCatalog.FactDefinition Fact()=>new("room.selection.manipulation",OutputType(ViewSchema()),"Construction move handle","Current solid handle visibility, grip and last failure. Preview poses are live object placements; none are saved until release completes its transform action. Reading does not show or move the handle.",null,null,(context,args)=>context.Editor?ProgramValue.Literal(JObject.FromObject(context.Editor.ObserveConstructionManipulation())):null,features:new[]{Feature});
        internal static bool RunManual(RoomEditor editor,bool visible,out string status){var selection=editor.ObserveConstructionSelection();return new RoomExecutions(editor).Execute(new JObject {["operation"]="start",["call"]=new JObject {["id"]="room.selection.manipulate",["version"]=1,["arguments"]=new JObject {["stateId"]=selection.stateId,["members"]=new JArray(selection.members),["visible"]=visible}}},out status);}
    }
}
