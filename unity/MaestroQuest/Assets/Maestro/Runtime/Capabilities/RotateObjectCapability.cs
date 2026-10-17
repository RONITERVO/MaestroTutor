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
    internal sealed class RotateObjectCapability : NativeTargetCapability
    {
        public override string Id=>"object.rotation.set";
        public override string Label=>"Rotate object";
        public override string Description=>"Set orientation in room axes using pitch, yaw and roll in degrees, each -180 to 180 (Unity Euler rotation). Keeps current position and scale. Outside temporary play, prepares and publishes its save in the background with one Undo edit. Completion confirms native application and saving. Before native acceptance, newer authored changes or cancellation preserve the original pose. After acceptance, stopping cannot retract the edit or its pending save. A save conflict preserves files, blocks further saves and requires inspection; never blindly replay it. Runtime clock and viewer movement continue. Only the target body is held during preparation; it resumes after application. In a temporary room, changes live placement and local Undo; a kept snapshot makes it durable. Rotation resets velocity, then normal gravity resumes. Other objects keep running.";
        public override string Duration=>"completion";
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
            if(!context.Editor.BeginRotation((string)arguments["target"],rotation,out var pending,out error))return false;
            operation=new Saving(pending);return true;
        }
        sealed class Saving:CapabilityOperation
        {
            readonly Maestro.Quest.Creation.RoomEditor.PendingPersistedEdit pending;
            internal Saving(Maestro.Quest.Creation.RoomEditor.PendingPersistedEdit pending){this.pending=pending;}
            public override float Seconds=>0;
            public override RuleActionState State(out string error)=>!pending.Poll(out var ready,out error)?RuleActionState.Failed:ready?RuleActionState.Ready:RuleActionState.Preparing;
            public override bool Complete(out string error)=>pending.Complete(out error);
            public override void Stop(bool preservePlacement)=>pending.Dispose();
            public override string InterruptionStatus=>pending.InterruptionStatus;
        }
    }
}
