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
        public override string Duration=>"completion";
        public override string Ownership=>"nativeWorldPlacement";
        public override IReadOnlyList<string> Requirements=>new[]{"world.viewpoint.current","workspace.available","head.tracked","destination.clear"};
        public override string Description=>"Explicitly relocate the user's viewpoint to an authored floor position and horizontal heading by placing virtual content around the current physical view. Use only when the user intends to relocate themselves or recenter the world; moving Maestro is a different action. Read world.viewpoint and pass its stateId. (0,0,0), yaw 0 is the fixed authored origin, not a user-defined spawn. Coordinates are currently limited to a 25-metre sphere. This preserves entity poses, ongoing programs, motion, physics velocities, view and movement opt-ins, and physical head/room tracking. It does not align the real room, recall tools, change scale, reset activity, create ground, or navigate a path. Relevant native ground and destination obstacles are prepared and retained before placement. Resident imported objects may still be loading their actual geometry; the action waits for that geometry rather than treating its placeholder as usable or as a permanent failure. Unknown collision extents require conservative preparation; missing assets, cancellation or changed dependencies leave placement unchanged. Destination clearance is rechecked; virtual view additionally needs accepted authored ground within the shared slope policy. Read world.ground to inspect support; it does not reserve clearance. Held items, workspace boundaries, tracking loss, crossing physical/virtual connections, blocked or unverifiable physical transfer paths during active physics, and unavailable storage refuse placement before acceptance. Swept conservative collider envelopes respect each object's environment profile; irregular shapes can require extra clearance. Real participants must remain inside an aligned room. Virtual-only objects can move through scanned floors and walls. Uses the shared ordered edit queue without blocking on disk. At acceptance the latest tracked head pose, ground, clearance and physical transfer are rechecked and the world moves once; the accepted bookmark then saves in the background. Completion confirms native placement and saving and returns that immutable acceptance-time location. Before acceptance Stop leaves placement unchanged. After acceptance Stop cannot undo or repeat movement; a failed save leaves the live location unconfirmed and requires shared storage recovery. Later movement remains independent and is saved separately. Temporary rooms retain a temporary bookmark. Personal navigation creates no geometry Undo or scene revision. Center the user's movement stick after relocation. Completed receipts never repeat movement.";
        static JObject Position()=>Object(new JObject {["x"]=Number(-25,25),["y"]=Number(-25,25),["z"]=Number(-25,25)});
        public override JObject InputSchema {get{var s=CurrentInputs(Object(new JObject {["stateId"]=Text("^[a-f0-9]{32}$",32),["position"]=Position(),["yaw"]=Number(-180,180)}),"world.viewpoint","stateId");s["x-features"]=new JArray(Feature);return s;}}
        public override JObject OutputSchema=>Object(new JObject {["stateId"]=Text("^[a-f0-9]{32}$",32),["position"]=Position(),["yaw"]=Number(-180,180),["temporary"]=new JObject {["type"]="boolean"}});
        public override JObject Example=>new(){["stateId"]=new string('0',32),["position"]=new JObject {["x"]=0,["y"]=0,["z"]=0},["yaw"]=0};
        static RoomViewpoint Target(JObject args)=>new(){active=true,position=new Vector3((float)args["position"]["x"],(float)args["position"]["y"],(float)args["position"]["z"]),yaw=(float)args["yaw"]};
        public override bool Validate(JObject args,out string error){error="Choose a world viewpoint within 25 metres";if(!Target(args).Valid)return false;error=null;return true;}
        internal override IEnumerable<string> NativeEntities(CapabilityContext context,JObject args)=>context.Editor?context.Editor.WorldPlacementTargets(Target(args)):System.Array.Empty<string>();
        internal override bool CanPrepare(CapabilityContext context,JObject args,out string error){error="World placement is unavailable";return context.Editor&&context.Editor.CanPrepareWorldViewpoint((string)args["stateId"],Target(args),false,out error);}
        public override bool CanRun(CapabilityContext context,JObject args,out string error){error="World placement is unavailable";return context.Editor&&context.Editor.CanBeginWorldViewpoint((string)args["stateId"],Target(args),false,out error);}
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)=>
            StartPlacement(context.Editor,(string)args["stateId"],Target(args),false,out operation,out error);
        internal static bool StartPlacement(RoomEditor editor,string expected,RoomViewpoint target,bool manual,
            out CapabilityOperation operation,out string error,System.Action<string> checkpoint=null)
        {
            operation=null;error="World placement is unavailable";if(!editor||target==null)return false;
            var captured=target.Copy();expected??=(string)editor.ObserveWorldViewpoint()?["stateId"];
            string guard=expected;RoomEditor.PendingWorldPlacement pending=null;
            return PersistedEditOperation.StartStateChange(editor,
                (out IPersistedRoomEdit edit,out string issue)=>{
                    bool started=editor.BeginWorldViewpoint(guard,captured,manual,out pending,out issue,checkpoint);edit=pending;return started;
                },
                (out string issue)=>editor.CanBeginWorldViewpoint(guard,captured,manual,out issue),
                ()=>pending?.Result??new JObject(),out operation,out error);
        }
        internal static BehaviourCatalog.FactDefinition Fact()=>new("world.viewpoint",ProgramDataType.Read(JObject.Parse("{\"record\":{\"stateId\":\"text\",\"ready\":\"boolean\",\"reason\":\"text\",\"located\":\"boolean\",\"position\":{\"record\":{\"x\":\"number\",\"y\":\"number\",\"z\":\"number\"}},\"yaw\":\"number\"}}")),"Your world location","Read the tracked authored floor position, heading and current placement guard. located=false means the position/yaw placeholders are unavailable. ready is basic workspace/tracking readiness only; requested destination clearance, ground and frame transfer are checked at execution. IDs change on world movement, temporary boundaries, observed tracking changes and successful relocation; they are workspace-local guards, not durable world or region IDs. Observation has no world effect.",null,null,(context,args)=>context.Editor?.ObserveWorldViewpoint() is JObject value?ProgramValue.Literal(value):null,features:new[]{Feature});
    }
}
