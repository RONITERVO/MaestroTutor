// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class PhysicsEnvironmentCapability:CapabilityModule
    {
        public override string Id=>"physics.environment.set";
        public override string Label=>"Choose real-room collisions";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[]{"physics.state.current","room.active","workspace.available"};
        public override string Description=>"Set real-room collision participation independently of the visual view. Read physics.environment and pass its exact stateId. Changing this policy pauses physics and clears old throw speeds; explicitly start afterwards. On requires a loaded aligned scan and keeps simulation inside that physical room. Off uses accepted authored ground, excluding scanned geometry from contacts, navigation and motion checks. Off requires virtual ground: current room-scale support is columns above actual accepted surfaces, up to 16 metres above and 0.25 metres below; gaps and unloaded regions are unavailable. This does not delete the scan, change passthrough/depth/audio, grant permissions, move the user or create terrain. Fresh rooms default to on; this runtime setting is not saved or undone. Already-satisfied requests preserve motion. Physical surfaces still exist when virtual objects do not collide with them.";
        public override JObject InputSchema {get {var schema=Object(new JObject {["realCollisions"]=new JObject {["type"]="boolean"},["stateId"]=Text("^[a-f0-9]{32}$",32)});schema["x-features"]=new JArray("physicsEnvironment.v1");return CurrentInputs(schema,"physics.environment","stateId");}}
        public override JObject OutputSchema=>Object(new JObject {["stateId"]=Text("^[a-f0-9]{32}$",32),["realCollisions"]=new JObject {["type"]="boolean"},["scanReady"]=new JObject {["type"]="boolean"},["authoredReady"]=new JObject {["type"]="boolean"},["ready"]=new JObject {["type"]="boolean"},["scope"]=Choice("alignedPhysicalRoom","acceptedGroundColumns")});
        public override JObject Example=>new() {["realCollisions"]=false,["stateId"]=new string('0',32)};
        public override bool CanRun(CapabilityContext context,JObject args,out string error) {
            var editor=context.Editor;error="Room physics is unavailable";if(!editor||!editor.PhysicsWorld)return false;
            if(editor.WriteGate.Frozen){error="Finish the workspace boundary before changing room physics";return false;}
            return editor.PhysicsWorld.CanSetEnvironment((string)args["stateId"],out error);
        }
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error) {
            operation=null;if(!CanRun(context,args,out error)||!context.Editor.PhysicsWorld.SetEnvironment((string)args["stateId"],(bool)args["realCollisions"],out var result,out error))return false;
            operation=new CompletedCapability(result);return true;
        }
        internal static BehaviourCatalog.FactDefinition Fact()=>new("physics.environment",ProgramDataType.Read(JObject.Parse("{\"record\":{\"stateId\":\"text\",\"realCollisions\":\"boolean\",\"scanReady\":\"boolean\",\"authoredReady\":\"boolean\",\"ready\":\"boolean\",\"scope\":\"text\"}}")),"Physics environment","Read the separate physical scan, authored ground and selected simulation readiness. This does not verify physical alignment or a clear route, start physics, load a scan or change its collision/visual/acoustic geometry. Physical placement continues to use scanReady even when authored ground is ready. No side effects.",null,null,(context,args)=>{var world=context.Editor?context.Editor.PhysicsWorld:null;return world?ProgramValue.Literal(world.ObserveEnvironment()):null;});
    }
}
