// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class LayoutCapability:NativeResourceInputsCapability
    {
        internal const string Feature="layoutEdits.v1";
        public override string Id=>"object.layout.apply";
        public override string Label=>"Arrange or reset objects";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.exists","target.unheld","authoring.inactive","storage.writable"};
        public override string Description=>"Apply 1–16 explicit placements together: target ID, room-local position in metres, unit-quaternion rotation and uniform scale. Useful for arranging/resetting a castle or board. Read object.placement to capture each live baseline; save chosen values in a reusable program. All members must exist, be released and idle; a missing or conflicting member refuses the entire edit. Saves once with one Undo capturing all targets' actual pre-edit poses; temporary play stays in its fork. Resets member velocities, preserves geometry/settings/animations and leaves other objects running. Completion means placement was applied, not that the structure is stable. Does not create replacements, connect/snap parts, avoid overlaps, start physics or move the book/Maestro. These are room axes, not durable scanned-room anchors. Programs use explicit literal member IDs; one-off calls require the normal current object conditions. Receipt replay cannot apply it twice; Stop does not undo a completed edit.";
        internal static JObject PositionSchema()=>Object(new JObject { ["x"]=Number(-25,25),["y"]=Number(-25,25),["z"]=Number(-25,25)});
        public override JObject InputSchema {get{
            var item=Object(new JObject { ["target"]=Resource(Text("^[a-fA-F0-9]{32}$",32)),["position"]=PositionSchema(),["rotation"]=Vector(true),["scale"]=Number(.1,4)});
            var schema=Object(new JObject { ["placements"]=List(item,1,RoomLayout.MaximumPlacements)});schema["format"]="objectLayout";schema["x-features"]=new JArray(Feature);return schema;
        }}
        public override JObject OutputSchema=>Object(new JObject { ["count"]=Number(1,RoomLayout.MaximumPlacements,true),["temporary"]=new JObject { ["type"]="boolean"}});
        public override JObject Example=>new() { ["placements"]=new JArray(new JObject { ["target"]=new string('0',32),["position"]=new JObject { ["x"]=.3,["y"]=1,["z"]=.6},["rotation"]=new JObject { ["x"]=0,["y"]=0,["z"]=0,["w"]=1},["scale"]=1})};
        static RoomLayout Layout(JObject args)=>JsonUtility.FromJson<RoomLayout>(args.ToString());
        public override BehaviourCatalog.Claim[] Claims(JObject args)=>Layout(args).placements.Select(p=>new BehaviourCatalog.Claim(p.target,"wholeTarget")).ToArray();
        public override bool CanRun(CapabilityContext context,JObject args,out string error) {
            foreach(var p in Layout(args).placements)if(!context.Target(new JObject { ["target"]=p.target},out _,out error))return false;
            return context.Editor.CanApplyLayout(Layout(args),out error);
        }
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error) {
            operation=null;if(!CanRun(context,args,out error)||!context.Editor.ApplyLayout(Layout(args),out error))return false;
            operation=new CompletedCapability(new JObject { ["count"]=((JArray)args["placements"]).Count,["temporary"]=context.Editor.TemporaryRoom});return true;
        }
        internal static BehaviourCatalog.FactDefinition Placement()=>new("object.placement",ProgramDataType.Read(JObject.Parse("{\"record\":{\"target\":\"text\",\"revision\":\"number\",\"position\":{\"record\":{\"x\":\"number\",\"y\":\"number\",\"z\":\"number\"}},\"rotation\":{\"record\":{\"x\":\"number\",\"y\":\"number\",\"z\":\"number\",\"w\":\"number\"}},\"scale\":\"number\",\"temporary\":\"boolean\"}}")),"Live object placement",
            "Current room-local position, unit rotation and uniform scale of a creation, suitable for an explicit layout baseline. Includes the last authored revision, which does not identify every live physics frame. A read does not save or reserve the object. Unlike object.definition these are live values; unlike object.position these are room-local axes. Unavailable for missing/disabled targets, paused runtime or out-of-bounds poses. Do not treat room axes as durable scan anchors.",Object(new JObject { ["target"]=Resource(Text("^[a-fA-F0-9]{32}$",32))}),new JObject { ["target"]=new string('0',32)},(context,args)=>{
                var editor=context.Editor;string id=(string)args["target"];var item=editor?editor.Find(id):null;
                if(!item||!item.isActiveAndEnabled||editor.RuntimeGate.Held)return null;
                var pose=editor.Frame.Placement(id,item.transform);if(!new RoomLayout {placements=new[]{pose}}.Validate(out _))return null;
                var value=JObject.Parse(JsonUtility.ToJson(pose));value["revision"]=editor.ObjectRevision(id);value["temporary"]=editor.TemporaryRoom;return ProgramValue.Literal(value);
            },features:new[]{Feature});
    }
}
