// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class SurfacePlacementCapability:NativeTargetCapability
    {
        public override string Id=>"object.surface.place";
        public override string Label=>"Place on a detected surface";
        public override string Duration=>"completion";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.exists","target.unheld","authoring.inactive","storage.writable","room.setup.current","surface.supported"};
        public override string Description=>"Use the physical Place surface service to move one object onto a live detected floor/table. Read room.environment for stateId. below casts downward from just above the target's collision bounds; gaze captures the user's current head ray. The ray is sampled once at invocation, not continuously steered. Detection is limited to four metres and upward surfaces (normal.y >= 0.7). The same collision-bounds support offset leaves a 1 cm gap and preserves rotation/scale; this does not prove the whole object fits or avoids other geometry. Preparation may request room permission; a system focus handoff cancels this action, so finish permission or Load room first and explicitly request placement again. Before native acceptance, Stop, lifecycle interruption, changed room identity, relocated room coordinates or changed target revision prevent a late placement. A missed ray never substitutes guessed coordinates or retries. Completion means the native placement was saved with one Undo (local only in temporary play), not settled physics. Velocity resets; existing gravity may continue, but this never starts physics. Result position/point/normal use room axes. Other objects keep running; an agent cannot steal another actor's target."+PersistedEditOperation.PlacementPersistence;
        public override JObject InputSchema {get {var schema=CurrentInputs(Object(new JObject {["target"]=Resource(Text("^(maestro|book|[a-fA-F0-9]{32})$",32)),["stateId"]=Text("^[a-f0-9]{32}$",32),["direction"]=Choice("below","gaze")}),"room.environment","stateId");schema["x-features"]=new JArray("surfacePlacement.v1");return schema;}}
        static JObject Triple(double bound)=>Object(new JObject {["x"]=Number(-bound,bound),["y"]=Number(-bound,bound),["z"]=Number(-bound,bound)});
        public override JObject OutputSchema=>Object(new JObject {["target"]=Text("^(maestro|book|[a-fA-F0-9]{32})$",32),["revision"]=Revision(),["position"]=Triple(25),["point"]=Triple(128),["normal"]=Triple(1),["temporary"]=new JObject {["type"]="boolean"}});
        public override JObject Example=>new() {["target"]="book",["stateId"]=new string('0',32),["direction"]="below"};
        static ScannedRoom Room(CapabilityContext context)=>context.Editor&&context.Editor.PhysicsWorld?context.Editor.PhysicsWorld.GetComponent<ScannedRoom>():null;
        public override bool CanRun(CapabilityContext context,JObject args,out string error)
        {
            error="The surface placement service is unavailable";var room=Room(context);if(!room)return false;
            if(!room.CanPlace((string)args["stateId"],out error)||!context.Target(args,out var item,out error)||!context.Editor.CanEditObject((string)args["target"],false,out error))return false;
            if(!ScannedRoom.Bounds(item,out _)){error="This object has no usable collision bounds";return false;}
            if((string)args["direction"]=="gaze"&&(!context.Editor.Viewer||!context.Editor.Viewer.gameObject.activeInHierarchy)){error="The user's tracked view is unavailable";return false;}
            return true;
        }
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(context,args,out error))return false;
            var item=context.Editor.Find((string)args["target"]);Physics.SyncTransforms();ScannedRoom.Bounds(item,out var bounds);
            var viewer=context.Editor.Viewer;var ray=(string)args["direction"]=="gaze"?new Ray(viewer.position,viewer.forward):new Ray(new Vector3(bounds.center.x,bounds.max.y+.02f,bounds.center.z),Vector3.down);
            return context.Editor.StartSurfacePlacement(Room(context),(string)args["target"],context.Editor.ObjectRevision((string)args["target"]),
                (string)args["stateId"],ray,out operation,out error);
        }
    }
}
