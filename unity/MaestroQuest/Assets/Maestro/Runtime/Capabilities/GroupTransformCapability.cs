// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal sealed class GroupTransformCapability:CapabilityModule {
        internal const string Feature="groupTransforms.v1";
        public override string Id=>"object.layout.transform";
        public override string Label=>"Move, turn or resize a construction";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.exists","target.unheld","source.revision.current","room.physics.paused","authoring.inactive","storage.writable"};
        public override string Description=>"Transform 1–16 idle creations as one arrangement with one save/Undo. The first member's CURRENT live pose is the pivot. position is its desired room-local origin; rotation is its desired absolute unit quaternion; scale multiplies every member's current scale and its distance from the pivot. A factor of 1 preserves size. Supply current revisions and include both ends of all connections. Pause physics, drawings, playback and authoring and release members first. Every final piece must remain within 25 metres and scale 0.1–4; otherwise nothing changes. Does not alter geometry, recorded animations, hinge frames or saved structure baselines. It does not connect pieces, prevent overlaps or snap to the scan. The physical construction handle uses this same projection and action on release. Failed admission/save leaves the complete original arrangement; replay cannot move it twice. Stop cannot undo a completed transform. Temporary edits stay in their room fork.";
        public override JObject InputSchema {get{
            var member=CurrentInputs(Object(new JObject {["target"]=Resource(Text("^[a-f0-9]{32}$",32)),["revision"]=Revision()}),"object.placement","revision",new JObject {["target"]="target"});
            var schema=Object(new JObject {["members"]=List(member,1,16),["position"]=LayoutCapability.PositionSchema(),["rotation"]=Vector(true),["scale"]=Number(.1,4)});schema["format"]="groupTransform";schema["x-features"]=new JArray(Feature);return schema;
        }}
        public override JObject OutputSchema=>Object(new JObject {["count"]=Number(1,16,true),["temporary"]=new JObject {["type"]="boolean"}});
        public override JObject Example=>new() {["members"]=new JArray(new JObject {["target"]=new string('0',32),["revision"]=1}),["position"]=new JObject {["x"]=0,["y"]=1,["z"]=.7},["rotation"]=new JObject {["x"]=0,["y"]=0,["z"]=0,["w"]=1},["scale"]=1};
        static RoomGroupTransform Read(JObject args)=>JsonUtility.FromJson<RoomGroupTransform>(args.ToString());
        public override BehaviourCatalog.Claim[] Claims(JObject args)=>Read(args).members.Select(m=>new BehaviourCatalog.Claim(m.target,"wholeTarget")).ToArray();
        public override bool CanRun(CapabilityContext context,JObject args,out string error)=>context.Editor.PrepareGroupTransform(Read(args),out _,out error);
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error){operation=null;if(!context.Editor.TransformGroup(Read(args),out error))return false;operation=new CompletedCapability(new JObject {["count"]=((JArray)args["members"]).Count,["temporary"]=context.Editor.TemporaryRoom});return true;}
        internal static bool RunManual(RoomEditor editor,RoomGroupTransform request,out string status)=>new RoomExecutions(editor).Execute(new JObject {["operation"]="start",["call"]=new JObject {["id"]="object.layout.transform",["version"]=1,["arguments"]=JObject.Parse(JsonUtility.ToJson(request))}},out status);
    }
}
