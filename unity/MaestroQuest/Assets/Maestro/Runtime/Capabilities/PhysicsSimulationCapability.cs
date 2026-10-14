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
        public override string Duration=>"completion";
        internal override float CompletionTimeoutSeconds=>120;
        public override IReadOnlyList<string> Requirements=>new[]{"physics.state.current","room.active","workspace.available"};
        public override string Description=>"Explicitly start or pause gravity and object physics through the physical Start physics/Pause controls. Read physics.simulation and pass its exact stateId. Manual Start/Pause, scan changes, focus/lifecycle transitions and workspace holds invalidate older intents, even after returning to the same mode. Start prepares currently resident free dynamic actors and their connected, nearby collision, selected ground and water dependencies, then rechecks the selected environment. Unused dormant content stays unloaded; newly active actors prepare their own inputs before gravity. Fixed, held and animated objects do not initiate preparation, but may be dependencies of a free actor. In a cold virtual-only world, Start can also prepare local ground and collision inputs at the tracked user viewpoint. That request is cancelled if tracking is lost, the world is placed elsewhere or the user moves into different required inputs before completion; it never loads distant known areas merely because they exist. During preparation it can be cancelled; manual Pause, another accepted start, scan/environment changes, focus loss, workspace changes or changes to captured saved inputs invalidate it. Unrelated saved edits do not cancel preparation. Native readiness changes alone do not create a new user intent. Failed/cancelled preparation never starts physics, even if its assets finish later. Start needs the selected environment ready (aligned scan when real collisions are on, accepted virtual ground when off) and an active room with provable imported-collision bounds and placement. Unavailable resident imports with previously accepted collision bounds use conservative native guards and stop dependent actors locally; unrelated actors can continue. Unknown bounds or placement still pause this bounded authored region. Explicit Start fails if a required selected dependency cannot prepare. Repair never silently resumes failed motion, including a movable imported object itself. This action cannot scan, grant room permission or verify alignment. Pause freezes dynamic bodies and clears throw velocities; it can stop physics-dependent walking or actions, but does not stop unrelated gestures, programs or authoring. Held/carried/animated objects keep their own existing owners when physics starts. These world transitions may run alongside other actions; they do not acquire those actors or clear the scheduler. Returning focus/alignment never automatically restarts physics or cancelled movement. No save or Undo; pausing cannot reverse a throw. A completed result describes that moment only; read the fact for current state. Duplicate receipts never restart physics. Stop on a completed receipt does not pause simulation: issue explicit pause with a fresh stateId.";
        public override JObject InputSchema {get{var schema=Object(new JObject {["operation"]=Choice("start","pause"),["stateId"]=Text("^[a-f0-9]{32}$",32)});schema["x-features"]=new JArray("physicsSimulation.v1");return CurrentInputs(schema,"physics.simulation","stateId");}}
        public override JObject OutputSchema=>Object(new JObject {["stateId"]=Text("^[a-f0-9]{32}$",32),["ready"]=new JObject {["type"]="boolean"},["running"]=new JObject {["type"]="boolean"},["active"]=new JObject {["type"]="boolean"},["held"]=new JObject {["type"]="boolean"},["canStart"]=new JObject {["type"]="boolean"},["status"]=new JObject {["type"]="string",["maxLength"]=128},["reason"]=new JObject {["type"]="string",["maxLength"]=128}});
        public override JObject Example=>new() {["operation"]="start",["stateId"]=new string('0',32)};
        static RoomPhysicsWorld World(CapabilityContext context)=>context.Editor?context.Editor.PhysicsWorld:null;
        public override bool CanRun(CapabilityContext context,JObject args,out string error)
        {
            var world=World(context);error="Room physics is unavailable";if(!world)return false;
            if(context.Editor.WriteGate.Frozen){error="Finish the workspace boundary before changing room physics";return false;}
            if((string)args["operation"]!="start")return world.CanSetSimulation((string)args["stateId"],false,out error);
            if(!world.CanPrepareSimulation((string)args["stateId"],out error))return false;
            if(!context.Editor.CanAcquireNativeEntities(context.Editor.NativeSimulationTargets(),out var loading,out error))return false;
            return loading||world.CanSetSimulation((string)args["stateId"],true,out error);
        }
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(context,args,out error))return false;
            var world=World(context);
            if((string)args["operation"]=="start"){
                var targets=context.Editor.NativeSimulationTargets(out var startCurrent);
                if(!context.Editor.CanAcquireNativeEntities(targets,out var loading,out error))return false;
                if(loading){
                    if(!world.BeginPreparedSimulation((string)args["stateId"],out var intent,out error))return false;
                    operation=new PhysicsStartOperation(context,targets,intent,startCurrent);return true;
                }
            }
            if(!world.SetSimulation((string)args["stateId"],(string)args["operation"]=="start",out var result,out error))return false;
            operation=new CompletedCapability(result);return true;
        }
        internal static BehaviourCatalog.FactDefinition Fact()=>new("physics.simulation",ProgramDataType.Read(JObject.Parse("{\"record\":{\"stateId\":\"text\",\"ready\":\"boolean\",\"running\":\"boolean\",\"active\":\"boolean\",\"held\":\"boolean\",\"canStart\":\"boolean\",\"status\":\"text\",\"reason\":\"text\"}}")),"Room physics state","Current native simulation state and exact stateId for physics.simulation.set. canStart describes immediate selected-ground, room collision and lifecycle admission; an explicit Start must still prepare the required dependencies of resident free dynamic actors, or local tracked-view ground in a cold virtual-only world. It does not activate every saved object. It is not permission to act, alignment approval or complete resource admission. ready requires selected ground and no unresolved imported-collision bounds or placement in the current authored region. Known unavailable geometry stops dependent actors through local input checks, so room readiness does not guarantee every actor can move. Read object.physics.inputs for individual physics readiness and physics.environment for separate scan/virtual observations; neither room fact guarantees a clear navigation path. Running does not mean held/animated/fixed objects simulate. This read never starts, pauses or saves anything.",null,null,(context,args)=>{var world=context.Editor?context.Editor.PhysicsWorld:null;return world?ProgramValue.Literal(world.ObserveSimulation()):null;});
    }
}
