// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class PhysicsSimulationCapability:CapabilityModule
    {
        public override string Id=>"physics.simulation.set";
        public override string Label=>"Start or pause room physics";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[]{"physics.state.current","room.active","workspace.available"};
        public override string Description=>"Explicitly start or pause gravity and object physics through the physical Start physics/Pause controls. Read physics.simulation and pass its exact stateId. Manual Start/Pause, scan changes, focus/lifecycle transitions and workspace holds invalidate older intents, even after returning to the same mode. Start needs the selected environment ready (aligned scan when real collisions are on, accepted virtual ground when off) and an active room with every accepted imported object’s collision geometry ready. Pending, failed or lost imported geometry pauses this bounded authored region, even when its scan remains aligned; finishing the load never silently resumes physics. This action cannot scan, grant room permission or verify alignment. Pause freezes dynamic bodies and clears throw velocities; it can stop physics-dependent walking or actions, but does not stop unrelated gestures, programs or authoring. Held/carried/animated objects keep their own existing owners when physics starts. These world transitions may run alongside other actions; they do not acquire those actors or clear the scheduler. Returning focus/alignment never automatically restarts physics or cancelled movement. No save or Undo; pausing cannot reverse a throw. A completed result describes that moment only; read the fact for current state. Duplicate receipts never restart physics. Stop on a completed receipt does not pause simulation: issue explicit pause with a fresh stateId.";
        public override JObject InputSchema {get{var schema=Object(new JObject {["operation"]=Choice("start","pause"),["stateId"]=Text("^[a-f0-9]{32}$",32)});schema["x-features"]=new JArray("physicsSimulation.v1");return CurrentInputs(schema,"physics.simulation","stateId");}}
        public override JObject OutputSchema=>Object(new JObject {["stateId"]=Text("^[a-f0-9]{32}$",32),["ready"]=new JObject {["type"]="boolean"},["running"]=new JObject {["type"]="boolean"},["active"]=new JObject {["type"]="boolean"},["held"]=new JObject {["type"]="boolean"},["canStart"]=new JObject {["type"]="boolean"},["status"]=new JObject {["type"]="string",["maxLength"]=128},["reason"]=new JObject {["type"]="string",["maxLength"]=128}});
        public override JObject Example=>new() {["operation"]="start",["stateId"]=new string('0',32)};
        static RoomPhysicsWorld World(CapabilityContext context)=>context.Editor?context.Editor.PhysicsWorld:null;
        public override bool CanRun(CapabilityContext context,JObject args,out string error)
        {
            var world=World(context);error="Room physics is unavailable";if(!world)return false;
            if(context.Editor.WriteGate.Frozen){error="Finish the workspace boundary before changing room physics";return false;}
            return world.CanSetSimulation((string)args["stateId"],(string)args["operation"]=="start",out error);
        }
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(context,args,out error)||!World(context).SetSimulation((string)args["stateId"],(string)args["operation"]=="start",out var result,out error))return false;
            operation=new CompletedCapability(result);return true;
        }
        internal static BehaviourCatalog.FactDefinition Fact()=>new("physics.simulation",ProgramDataType.Read(JObject.Parse("{\"record\":{\"stateId\":\"text\",\"ready\":\"boolean\",\"running\":\"boolean\",\"active\":\"boolean\",\"held\":\"boolean\",\"canStart\":\"boolean\",\"status\":\"text\",\"reason\":\"text\"}}")),"Room physics state","Current native simulation state and exact stateId for physics.simulation.set. canStart describes selected-ground, imported-collision and lifecycle prerequisites, not permission to act, alignment approval or complete resource admission. ready requires selected ground and ready imported collision in the current authored region; read physics.environment for separate scan/virtual observations, not a guarantee of a clear navigation path. Running does not mean held/animated/fixed objects simulate. This read never starts, pauses or saves anything.",null,null,(context,args)=>{var world=context.Editor?context.Editor.PhysicsWorld:null;return world?ProgramValue.Literal(world.ObserveSimulation()):null;});
    }
}
