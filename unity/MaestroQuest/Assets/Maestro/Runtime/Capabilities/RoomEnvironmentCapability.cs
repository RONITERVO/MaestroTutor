// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class RoomEnvironmentCapability:CapabilityModule
    {
        public override string Id=>"room.environment.set";
        public override string Label=>"Load, scan or show the room";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[]{"room.setup.current","room.active","workspace.available"};
        public override string Description=>"Uses the physical Load room/Scan room/Show room service. Read room.environment for an exact stateId. Load requests permission and loads an existing saved Meta room; it never opens scanning automatically when no data exists. Scan explicitly opens Meta room setup, then loads the result. The user must handle system permission/setup screens and check real floor/wall alignment. Success acknowledges a request, not completed scanning or alignment: observe requestId, phase and busy in the fact. Physics pauses and never starts itself. Show/hide changes scan-surface visibility intent only; visible means requested or virtual-view-forced visibility, not proof of rendered geometry. Virtual view keeps surfaces visible. Cancel binds the exact current requestId and abandons its continuation; it cannot close a system screen or undo a room scan already saved by Meta. New requests remain blocked until the actual platform task drains, including after timeout or cancellation. Permission/setup focus handoffs may finish on return; focus loss during loading, disable, virtual view or workspace holds cancel continuation. Stop on a completed receipt does not cancel a request; use explicit cancel. Duplicate receipts never reopen system screens. No Maestro room save or Undo, no geometry sent to the agent, no automatic retry or physics restart.";
        static JObject Variant(bool cancel)
        {
            var fields=new JObject {["operation"]=cancel?Choice("cancel"):Choice("load","scan","show","hide"),["stateId"]=Text("^[a-f0-9]{32}$",32)};
            if(cancel)fields["requestId"]=Text("^(|[a-f0-9]{32})$",32);
            var schema=CurrentInputs(Object(fields),"room.environment","stateId",null,cancel?new[]{"requestId"}:System.Array.Empty<string>());
            if(cancel)((JArray)schema["x-current"]["guards"]).Add("requestId");
            schema["x-features"]=new JArray("roomEnvironment.v1");schema["title"]=cancel?"Cancel current setup":"Room setup and visibility";return schema;
        }
        public override JObject InputSchema=>new() {["type"]="object",["x-discriminators"]=new JArray("operation"),["oneOf"]=new JArray(Variant(false),Variant(true))};
        static JObject Flags(params string[] names){var fields=new JObject();foreach(var name in names)fields[name]=new JObject {["type"]="boolean"};return Object(fields);}
        public override JObject OutputSchema=>Object(new JObject {
            ["stateId"]=Text("^[a-f0-9]{32}$",32),["requestId"]=Text("^(|[a-f0-9]{32})$",32),["phase"]=Choice("idle","permission","scan","returning","loading","loaded","failed","cancelled"),
            ["busy"]=new JObject {["type"]="boolean"},["status"]=new JObject {["type"]="string",["maxLength"]=128},["reason"]=new JObject {["type"]="string",["maxLength"]=128},
            ["availability"]=Flags("supported","active","virtualView","canLoad","canScan","canCancel"),["surfaces"]=Flags("showing","visible","ready","physicsRunning")});
        public override JObject Example=>new() {["operation"]="load",["stateId"]=new string('0',32)};
        static ScannedRoom Room(CapabilityContext context)=>context.Editor&&context.Editor.PhysicsWorld?context.Editor.PhysicsWorld.GetComponent<ScannedRoom>():null;
        public override bool CanRun(CapabilityContext context,JObject args,out string error)
        {
            var room=Room(context);error="The scanned-room service is unavailable";if(!room)return false;
            if(context.Editor.WriteGate.Frozen){error="Finish the workspace boundary before changing room setup";return false;}
            return room.CanSetup((string)args["stateId"],(string)args["operation"],(string)args["requestId"],out error);
        }
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(context,args,out error)||!Room(context).SetSetup((string)args["stateId"],(string)args["operation"],(string)args["requestId"],out var result,out error))return false;
            operation=new CompletedCapability(result);return true;
        }
        internal static BehaviourCatalog.FactDefinition Fact()=>new("room.environment",ProgramDataType.Read(JObject.Parse("{\"record\":{\"stateId\":\"text\",\"requestId\":\"text\",\"phase\":\"text\",\"busy\":\"boolean\",\"status\":\"text\",\"reason\":\"text\",\"availability\":{\"record\":{\"supported\":\"boolean\",\"active\":\"boolean\",\"virtualView\":\"boolean\",\"canLoad\":\"boolean\",\"canScan\":\"boolean\",\"canCancel\":\"boolean\"}},\"surfaces\":{\"record\":{\"showing\":\"boolean\",\"visible\":\"boolean\",\"ready\":\"boolean\",\"physicsRunning\":\"boolean\"}}}}")),"Room setup state","Native room request and visibility state; readiness never verifies physical alignment. busy includes a cancelled platform worker until it drains. canLoad/canScan need an active MR room, available platform and no workspace hold. canCancel identifies an uncancelled worker, not permission to bypass lifecycle gates. A completed action receipt only acknowledges intent; inspect this fact for progress. No room geometry is included.",null,null,(context,args)=>{var room=context.Editor&&context.Editor.PhysicsWorld?context.Editor.PhysicsWorld.GetComponent<ScannedRoom>():null;return room?ProgramValue.Literal(room.ObserveSetup()):null;});
    }
}
