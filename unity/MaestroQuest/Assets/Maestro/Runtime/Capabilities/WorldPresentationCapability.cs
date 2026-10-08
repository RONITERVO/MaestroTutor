// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class WorldPresentationCapability:CapabilityModule
    {
        internal const string Feature="worldPresentation.v1";
        public override string Id=>"world.presentation.set";
        public override string Label=>"Blend the backdrop and real occlusion";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[]{"controls.mode.current","tracking.available","manual.released"};
        public override bool RequiresQuietRoom(JObject args)=>false;
        public override string Description=>"Change the viewer's neutral backdrop opacity (0 reveals passthrough in empty areas, 1 covers it, intermediate values blend it) and independent real-depth occlusion preference. Only change the view when requested by the user. Read world.presentation and pass its exact stateId. The book and authored objects retain their own materials/opacity: this is not an opacity slider for imported surroundings or a passthrough window. Full opacity temporarily makes real depth ineligible to prevent unintended room-shaped holes; a selected realDepth=true becomes eligible again below 1. Eligibility is not evidence of device support, permission, available depth, or alignment. Does not change real-room collisions, create terrain, stop autonomous activity, move tracking/world, share cameras, or save the room. Both stick opt-ins are retained at every opacity; user movement still requires accepted ground and complete-frame clearance. Sticks must return to neutral after changing the presentation. Explicit Virtual/MR controls choose endpoint presets. Recall, focus/tracking loss and restart restore ordinary MR with depth preferred. These live presentation preferences are not saved or undoable; use a fresh observation to reverse a completed action. Replayed receipts never reapply it.";
        public override JObject InputSchema {get{
            var schema=Object(new JObject{["backdropOpacity"]=Number(0,1),["realDepth"]=new JObject{["type"]="boolean"},["stateId"]=Text("^[a-f0-9]{32}$",32)});
            schema["x-features"]=new JArray(Feature);return CurrentInputs(schema,"world.presentation","stateId");
        }}
        public override JObject OutputSchema=>Object(new JObject{
            ["stateId"]=Text("^[a-f0-9]{32}$",32),["backdropOpacity"]=Number(0,1),["realDepth"]=new JObject{["type"]="boolean"},
            ["depthEligible"]=new JObject{["type"]="boolean"},["virtualView"]=new JObject{["type"]="boolean"},
            ["headTracked"]=new JObject{["type"]="boolean"},["focused"]=new JObject{["type"]="boolean"}
        });
        public override JObject Example=>new(){["backdropOpacity"]=.5,["realDepth"]=true,["stateId"]=new string('0',32)};
        static MovementControls Owner(CapabilityContext context)=>context.Editor?context.Editor.GetComponent<MovementControls>():null;
        public override bool CanRun(CapabilityContext context,JObject args,out string error){
            var owner=Owner(context);error="Room view is unavailable";return owner&&owner.CanSetPresentation((string)args["stateId"],(float)args["backdropOpacity"],out error);
        }
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error){
            operation=null;if(!CanRun(context,args,out error)||!Owner(context).SetPresentation((string)args["stateId"],(float)args["backdropOpacity"],(bool)args["realDepth"],out var result,out error))return false;
            operation=new CompletedCapability(result);return true;
        }
        internal static BehaviourCatalog.FactDefinition Fact()=>new("world.presentation",
            ProgramDataType.Read(JObject.Parse("{\"record\":{\"stateId\":\"text\",\"backdropOpacity\":\"number\",\"realDepth\":\"boolean\",\"depthEligible\":\"boolean\",\"virtualView\":\"boolean\",\"headTracked\":\"boolean\",\"focused\":\"boolean\"}}")),
            "Backdrop and real occlusion","Current transient visual settings. stateId also changes on manual mode/settings edits, focus/tracking changes and recovery. depthEligible means the view permits depth, not that the device has supplied it. Read physics.environment and object.environment for independent collisions. No read side effects.",
            null,null,(context,args)=>{var owner=context.Editor?context.Editor.GetComponent<MovementControls>():null;return !owner||!owner.ConfigurationInitialized?null:ProgramValue.Literal(owner.ObservePresentation());});
    }
}
