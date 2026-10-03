// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class ConstructionSelectionCapability:CapabilityModule {
        internal const string Feature="constructionSelection.v1";
        public override string Id=>"room.selection.set";
        public override string Label=>"Choose construction pieces";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[]{"selection.state.current","members.exist","room.interaction.ready"};
        public override string Description=>"Replace the shared transient construction selection with 0–16 distinct creation IDs in explicit order. Read room.selection first and pass its exact stateId. collecting=true makes controller/hand taps add or remove pieces and suppresses their ordinary item-tapped behaviours until finished; grips still move objects. Put drawing tools away first. Selected pieces are outlined; the first member is the origin when preparing a reusable construction. This does not move objects, save geometry, authorize later edits or capture a module. Use program.module.captureConstruction with explicit current member revisions when ready. Changed membership/order/mode gets a fresh identity. Deleted pieces are removed, Undo does not silently select them again, temporary-room boundaries and workspace reload clear the selection, and application/runtime suspension ends picking. Selection has no saved-room Undo and is not restored after reload. Finishing picking retains members; an empty list clears them. Book and Maestro cannot be members. Stop cannot undo a completed selection change.";
        public override JObject InputSchema {get{
            var schema=CurrentInputs(Object(new JObject {["stateId"]=Text("^[a-f0-9]{32}$",32),["members"]=List(Resource(Text("^[a-fA-F0-9]{32}$",32)),0,16),["collecting"]=new JObject {["type"]="boolean"}}),"room.selection","stateId");schema["format"]="constructionSelection";schema["x-features"]=new JArray(Feature);return schema;
        }}
        public override JObject OutputSchema=>Object(new JObject {["stateId"]=Text("^[a-f0-9]{32}$",32),["members"]=List(Text("^[a-f0-9]{32}$",32),0,16),["collecting"]=new JObject {["type"]="boolean"}});
        public override JObject Example=>new(){["stateId"]=new string('0',32),["members"]=new JArray(),["collecting"]=true};
                public override bool CanRun(CapabilityContext context,JObject args,out string error)=>context.Editor.CanSetConstructionSelection((string)args["stateId"],((JArray)args["members"]).Values<string>().ToArray(),(bool)args["collecting"],out error);
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error){
            operation=null;if(!context.Editor.SetConstructionSelection((string)args["stateId"],((JArray)args["members"]).Values<string>().ToArray(),(bool)args["collecting"],out error))return false;
            operation=new CompletedCapability(View(context.Editor));return true;
        }
        static JObject View(RoomEditor editor){var view=editor.ObserveConstructionSelection();return new JObject {["stateId"]=view.stateId,["members"]=new JArray(view.members),["collecting"]=view.collecting};}
        internal static BehaviourCatalog.FactDefinition Fact(){var type=ProgramDataType.Read(JObject.Parse("{\"record\":{\"stateId\":\"text\",\"members\":{\"list\":\"text\"},\"collecting\":\"boolean\"}}"));return new("room.selection",type,"Construction selection","Current shared ordered construction members and physical picking mode. Transient view state, not saved structure membership, a revision reservation or permission to edit. Read current object.definition revisions before capture.",null,null,(context,args)=>context.Editor&&!context.Editor.RuntimeGate.Held?ProgramValue.Literal(View(context.Editor),type):null,features:new[]{Feature});}
        internal static bool RunManual(RoomEditor editor,string[] members,bool collecting,out string status) {
            var view=editor.ObserveConstructionSelection();return new RoomExecutions(editor).Execute(new JObject {["operation"]="start",["call"]=new JObject {["id"]="room.selection.set",["version"]=1,["arguments"]=new JObject {["stateId"]=view.stateId,["members"]=new JArray(members),["collecting"]=collecting}}},out status);
        }
    }
}
