// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    /// <summary>Named-only capability: no RuleActionKind value or RuleStep fields.</summary>
    internal sealed class RotateObjectCapability : CapabilityModule
    {
        public override string Id=>"object.rotation.set";
        public override string Label=>"Rotate object";
        public override string Description=>"Set orientation in room axes using pitch, yaw and roll in degrees, each -180 to 180 (Unity Euler rotation). Keeps current position and scale. Outside temporary play, saves before completion with one Undo edit. In a temporary room, changes live placement and local Undo; a kept snapshot makes it durable. Rotation resets velocity, then normal gravity resumes. Other objects keep running.";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Channels=>new[] {"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[] {"target.exists","target.unheld","authoring.inactive","storage.writable"};
        public override JObject InputSchema=>Object(new JObject {
            ["target"]=Resource(Text("^(maestro|book|[a-fA-F0-9]{32})$",32)),
            ["pitch"]=Number(-180,180),["yaw"]=Number(-180,180),["roll"]=Number(-180,180)
        });
        public override bool CanRun(CapabilityContext context,JObject arguments,out string error)=>
            context.Target(arguments,out _,out error)&&context.Editor.CanEditObject((string)arguments["target"],false,out error);
        public override bool Start(CapabilityContext context,string runId,JObject arguments,out CapabilityOperation operation,out string error) {
            operation=null;var rotation=Quaternion.Euler((float)arguments["pitch"],(float)arguments["yaw"],(float)arguments["roll"]);
            if(!context.Editor.RotateObject((string)arguments["target"],rotation,out error))return false;
            operation=new CompletedCapability();return true;
        }
    }
}
