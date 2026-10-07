// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class WorldViewpointCapability:CapabilityModule
    {
        internal const string Feature="worldViewpoint.v1";
        public override string Id=>"world.viewpoint.set";
        public override string Label=>"Go to a world location";
        public override string Duration=>"instant";
        public override string Ownership=>"nativeWorldPlacement";
        public override IReadOnlyList<string> Requirements=>new[]{"world.viewpoint.current","workspace.available","head.tracked","destination.clear"};
        public override string Description=>"Explicitly relocate the user's viewpoint to an authored floor position and horizontal heading by placing virtual content around the current physical view. Use only when the user intends to relocate themselves or recenter the world; moving Maestro is a different action. Read world.viewpoint and pass its stateId. (0,0,0), yaw 0 is the fixed authored origin, not a user-defined spawn. Coordinates are currently limited to a 25-metre sphere. This preserves entity poses, ongoing programs, motion, physics velocities, view and movement opt-ins, and physical head/room tracking. It does not align the real room, recall tools, change scale, reset activity, create ground, or navigate a path. Destination clearance is checked; virtual view additionally needs accepted authored ground within the shared slope policy. Read world.ground to inspect support; it does not reserve clearance. Held items, workspace boundaries, tracking loss, crossing physical/virtual connections, active physics with real-room collisions and failed saving refuse the entire operation. Save occurs before native placement; temporary rooms retain a temporary bookmark. Personal navigation creates no geometry Undo or scene revision. Center the user's movement stick after relocation. Completed receipts never repeat movement.";
        static JObject Position()=>Object(new JObject {["x"]=Number(-25,25),["y"]=Number(-25,25),["z"]=Number(-25,25)});
        public override JObject InputSchema {get{var s=CurrentInputs(Object(new JObject {["stateId"]=Text("^[a-f0-9]{32}$",32),["position"]=Position(),["yaw"]=Number(-180,180)}),"world.viewpoint","stateId");s["x-features"]=new JArray(Feature);return s;}}
        public override JObject OutputSchema=>Object(new JObject {["stateId"]=Text("^[a-f0-9]{32}$",32),["position"]=Position(),["yaw"]=Number(-180,180),["temporary"]=new JObject {["type"]="boolean"}});
        public override JObject Example=>new(){["stateId"]=new string('0',32),["position"]=new JObject {["x"]=0,["y"]=0,["z"]=0},["yaw"]=0};
        static RoomViewpoint Target(JObject args)=>new(){active=true,position=new Vector3((float)args["position"]["x"],(float)args["position"]["y"],(float)args["position"]["z"]),yaw=(float)args["yaw"]};
        public override bool Validate(JObject args,out string error){error="Choose a world viewpoint within 25 metres";if(!Target(args).Valid)return false;error=null;return true;}
        public override bool CanRun(CapabilityContext context,JObject args,out string error){error="World placement is unavailable";return context.Editor&&context.Editor.CanSetWorldViewpoint((string)args["stateId"],Target(args),false,out error);}
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error){operation=null;error="World placement is unavailable";if(!context.Editor||!context.Editor.SetWorldViewpoint((string)args["stateId"],Target(args),false,out var result,out error))return false;operation=new CompletedCapability(result);return true;}
        internal static BehaviourCatalog.FactDefinition Fact()=>new("world.viewpoint",ProgramDataType.Read(JObject.Parse("{\"record\":{\"stateId\":\"text\",\"ready\":\"boolean\",\"reason\":\"text\",\"located\":\"boolean\",\"position\":{\"record\":{\"x\":\"number\",\"y\":\"number\",\"z\":\"number\"}},\"yaw\":\"number\"}}")),"Your world location","Read the tracked authored floor position, heading and current placement guard. located=false means the position/yaw placeholders are unavailable. ready is basic workspace/tracking readiness only; requested destination clearance, ground and frame transfer are checked at execution. IDs change on world movement, temporary boundaries, observed tracking changes and successful relocation; they are workspace-local guards, not durable world or region IDs. Observation has no world effect.",null,null,(context,args)=>context.Editor?.ObserveWorldViewpoint() is JObject value?ProgramValue.Literal(value):null,features:new[]{Feature});
    }
}
