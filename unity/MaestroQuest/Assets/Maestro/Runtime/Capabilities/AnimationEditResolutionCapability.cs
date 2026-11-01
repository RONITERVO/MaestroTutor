// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class AnimationEditResolutionCapability:CapabilityModule
    {
        internal const string Feature="animationEditResolution.v1";
        internal override IEnumerable<string> NativeEntities(JObject arguments){yield break;}
        public override string Id=>"animation.edit.resolve";
        public override string Label=>"Resolve a pending keyframe edit";
        public override string Duration=>"completion";
        public override string Ownership=>"authoringSession";
        public override IReadOnlyList<string> Requirements=>new[]{"authoring.session.current"};
        public override string Description=>"Inspect animation.edit.pending to resolve the exact edit captured by physical Save frame, Replace, Remove, Loop, speed or Auto gestures. retry observes a running save or retries its frozen values at the original room/object revision. It never captures a different frame or interrupts another actor. Completion confirms native application and publication; temporary edits remain in the fork until Keep. Stopping observation does not stop the session-owned save. discard cancels unaccepted work and drops the retained buffer; an accepted publication must settle first. Applied/unconfirmed failures require room recovery and must never be replayed. Discard does not undo accepted edits or repair storage. Selecting another object cannot redirect this edit. Live posing can continue while its earlier captured keyframe saves; newer pose edits remain separate. Paused preparation rejoins after resume; accepted publication drains. A disabled workshop retains a failed buffer in memory. Process termination loses unsaved buffers. Use animation.edit to author saved keyframes directly.";
        public override JObject InputSchema {get{
            var schema=Object(new JObject {["operation"]=Choice("retry","discard"),["sessionId"]=Text("^[a-f0-9]{32}$",32)});
            schema["x-features"]=new JArray(Feature);return schema;
        }}
        public override JObject OutputSchema=>Object(new JObject {["sessionId"]=Text("^[a-f0-9]{32}$",32),
            ["target"]=Text("^(maestro|book|[a-fA-F0-9]{32})$",32),["phase"]=Choice("saved","discarded"),
            ["revision"]=Revision(true),["temporary"]=new JObject {["type"]="boolean"}});
        public override JObject Example=>new() {["operation"]="retry",["sessionId"]=new string('0',32)};
        public override bool CanRun(CapabilityContext context,JObject args,out string error)
        {
            error="Animation workshop is unavailable";return context.Workshop&&
                context.Workshop.CanResolveFrameEdit((string)args["sessionId"],(string)args["operation"]=="discard",out error);
        }
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(context,args,out error))return false;
            string session=(string)args["sessionId"];
            if((string)args["operation"]=="discard"){
                if(!context.Workshop.DiscardFrameEdit(session,out var result,out error))return false;
                operation=new CompletedCapability(result);return true;
            }
            if(!context.Workshop.BeginFrameEditSave(session,out var attempt,out error))return false;
            operation=new Completion(attempt);return true;
        }
        sealed class Completion:CapabilityOperation
        {
            readonly AnimationWorkshop.FrameSaveAttempt attempt;bool stopped;
            internal Completion(AnimationWorkshop.FrameSaveAttempt attempt)=>this.attempt=attempt;
            public override float Seconds=>0;
            public override Rules.RuleActionState State(out string error)
            {
                if(stopped){error="Animation edit observation stopped";return Rules.RuleActionState.Failed;}
                return !attempt.Poll(out var ready,out error)?Rules.RuleActionState.Failed:ready?Rules.RuleActionState.Ready:Rules.RuleActionState.Preparing;
            }
            public override bool Complete(out string error)
            {
                if(stopped){error="Animation edit observation stopped";return false;}
                if(!attempt.Poll(out var ready,out error))return false;
                if(!ready){error="Animation edit is still saving";return false;}return true;
            }
            public override JObject Result=>attempt.Result??new JObject();
            public override void Stop(bool preservePlacement)=>stopped=true;
            public override string InterruptionStatus=>attempt.InterruptionStatus;
        }
        public static BehaviourCatalog.FactDefinition Fact()=>new("animation.edit.pending",
            ProgramDataType.Read(JObject.Parse("{\"record\":{\"sessionId\":\"text\",\"target\":\"text\",\"phase\":\"text\",\"revision\":\"number\",\"frames\":\"number\",\"duration\":\"number\",\"loop\":\"boolean\",\"writesPose\":\"boolean\",\"poseSaved\":\"boolean\",\"temporary\":\"boolean\",\"applied\":\"boolean\",\"error\":\"text\"}}")),
            "Pending physical keyframe edit","One immutable physical keyframe edit, independently of selection and live posing. saving means queued/preparing/publishing; unsaved retains a failed buffer for exact retry or discard; idle has no buffer. revision is the captured target revision. writesPose indicates whether this operation changes the saved pose; poseSaved indicates a non-null captured pose. applied never means saving is confirmed. Error text is bounded. animation.edit.resolve uses this exact sessionId; idle cannot be resolved.",
            null,null,(context,args)=>context.Editor&&context.Editor.GetComponent<AnimationWorkshop>() is AnimationWorkshop workshop&&workshop?
                ProgramValue.Literal(workshop.ObserveFrameEdit()):null);
    }
}
