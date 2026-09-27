// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    /// <summary>Adapter for existing animation lifetimes while they move to individual modules.
    /// The interpreter and scheduler do not depend on this adapter or on its numeric kinds.</summary>
    internal sealed class AnimationCapability : CapabilityModule
    {
        readonly string id,label,requirements;readonly RuleActionKind kind;
        public AnimationCapability(string id,RuleActionKind kind,string label,string requirements) {this.id=id;this.kind=kind;this.label=label;this.requirements=requirements;}
        public override string Id=>id;
        public override string Label=>label;
        public override JObject InputSchema=>CapabilityArguments.AnimationSchema(kind);
        public override string Duration=>"timed";
        public override string Ownership=>kind==RuleActionKind.UpperBodyGesture||RuleDocument.IsSpatial(kind)?"exclusiveChannels":"exclusiveTargetAndProp";
        public override IReadOnlyList<string> Channels=>kind switch {
            RuleActionKind.UpperBodyGesture=>new[] {"upperBody"},RuleActionKind.LookAtUser=>new[] {"gaze"},
            RuleActionKind.FollowUser=>new[] {"locomotion","gaze"},_=>new[] {"wholeTarget"}
        };
        public override IReadOnlyList<string> Requirements=>requirements.Split(' ',StringSplitOptions.RemoveEmptyEntries);
        public override bool Validate(JObject arguments,out string error)=>CapabilityArguments.TryStep(kind,arguments,out _,out error);
        RuleStep Step(JObject arguments) {if(!CapabilityArguments.TryStep(kind,arguments,out var step,out var error))throw new ArgumentException(error);return step;}
        public override BehaviourCatalog.Claim[] Claims(JObject arguments) {
            var claims=new List<BehaviourCatalog.Claim>(base.Claims(arguments));
            if(arguments["prop"] is JObject prop)claims.Add(new BehaviourCatalog.Claim((string)prop["objectId"],"wholeTarget"));return claims.ToArray();
        }
        public override bool CanRun(CapabilityContext context,JObject arguments,out string error)=>context.Animations.CanRun(Step(arguments),out error);
        public override bool Start(CapabilityContext context,string runId,JObject arguments,out CapabilityOperation operation,out string error) {
            var animation=new AnimationOperation(context.Animations,runId);operation=animation;
            return animation.Begin(Step(arguments),out error);
        }
        sealed class AnimationOperation : CapabilityOperation {
            readonly AnimationActionRuntime runtime;readonly string id;float seconds;
            public AnimationOperation(AnimationActionRuntime runtime,string id) {this.runtime=runtime;this.id=id;}
            public bool Begin(RuleStep step,out string error)=>runtime.Start(id,step,out seconds,out error);
            public override float Seconds=>seconds;
            public override RuleActionState State(out string error)=>runtime.State(id,out error);
            public override void Tick()=>runtime.Tick(id);
            public override bool Complete(out string error)=>runtime.Complete(id,out error);
            public override void Stop(bool preservePlacement)=>runtime.Stop(id,preservePlacement);
        }
    }
}
