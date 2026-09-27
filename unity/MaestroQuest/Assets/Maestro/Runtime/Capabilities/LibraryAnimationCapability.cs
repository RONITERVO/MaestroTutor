// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Maestro.Quest.Imports;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class LibraryAnimationCapability : FullBodyCapability
    {
        public override string Id=>"animation.library.play";
        public override string Label=>"Library motion";
        public override IReadOnlyList<string> Requirements=>new[] {"target.exists","target.unheld","authoring.inactive","model.loaded","motion.available","rig.compatible"};
        public override JObject InputSchema=>Object(new JObject {
            ["target"]=AnimationTargets.TargetSchema(),["seconds"]=Number(0,30),["loop"]=new JObject {["type"]="boolean"},
            ["motionId"]=Text("^(|[a-fA-F0-9]{32})$",32),["prop"]=AnimationTargets.PropSchema()
        },"prop");
        public override bool CanRun(CapabilityContext context,JObject arguments,out string error) {
            if(!base.CanRun(context,arguments,out error))return false;
            var model=AnimationTargets.ClipModel(context.Editor.Find((string)arguments["target"]));
            var motion=context.Editor.Motions.Find((string)arguments["motionId"]);
            if(motion==null) {error="This saved motion is missing; choose one using Motion";return false;}
            if(!model||!model.Ready||string.IsNullOrEmpty(model.MotionRigHash)||motion.rigHash!=model.MotionRigHash)
            {error="This saved motion needs a compatible loaded model and rest pose";return false;}
            if((float)arguments["seconds"]==0&&motion.duration>30) {error="Choose an explicit duration for motions longer than 30 seconds";return false;}
            return true;
        }
        public override bool Start(CapabilityContext context,string runId,JObject arguments,out CapabilityOperation operation,out string error) {
            var value=new LibraryOperation(context,arguments);operation=value;return value.Begin((string)arguments["motionId"],(bool)arguments["loop"],out error);
        }
        sealed class LibraryOperation : FullBodyOperation {
            ImportedModel model;
            MotionLibrary.Lease motion;
            Task preparation;
            string rigHash,modelHash,loadError;
            bool started,loop;
            float began;
            public LibraryOperation(CapabilityContext context,JObject arguments):base(context,arguments) {}
            public bool Begin(string motionId,bool loop,out string error) {
                if(!Acquire(out error))return false;this.loop=loop;
                model=AnimationTargets.ClipModel(Target);rigHash=model.MotionRigHash;modelHash=Context.Editor.Read(TargetId).modelHash;
                var entry=Context.Editor.Motions.Find(motionId);if(Duration==0)Duration=Mathf.Max(.1f,entry.duration);
                preparation=Prepare(entry.id);return true;
            }
            async Task Prepare(string motionId) {
                MotionLibrary.Lease lease=null;
                try {
                    lease=await Context.Editor.Motions.AcquireAsync(motionId,rigHash);
                    if(!Stopped) {motion=lease;lease=null;}
                } catch(Exception error) {
                    if(!Stopped)loadError=error is ModelImportException?error.Message:"This saved motion could not load; import the original again";
                } finally {lease?.Dispose();}
            }
            public override RuleActionState State(out string error) {
                if(base.State(out error)==RuleActionState.Failed)return RuleActionState.Failed;
                if(started)return RuleActionState.Ready;
                if(!preparation.IsCompleted)return RuleActionState.Preparing;
                if(loadError!=null) {error=loadError;return RuleActionState.Failed;}
                var current=Context.Editor?Context.Editor.Find(TargetId):null;
                if(!current||current!=Target||Target.Grab.isSelected||Context.Workshop&&Context.Workshop.ControlsTarget(TargetId)||
                    !model||AnimationTargets.ClipModel(Target)!=model||Context.Editor.Read(TargetId)?.modelHash!=modelHash||
                    Avatar&&Avatar.ModelBusy||motion==null||motion.RigHash!=model.MotionRigHash)
                {error="The motion target changed while loading; choose it again";return RuleActionState.Failed;}
                if(Avatar) {
                    if(!Avatar.PlayLibraryMotion(motion,loop)) {error="This motion no longer matches Maestro";return RuleActionState.Failed;}
                    motion=null; // Ownership transfers only after successful playback.
                } else if(!model.SampleMotion(motion,0,loop)) {error="This saved motion cannot play on this object";return RuleActionState.Failed;}
                began=Time.unscaledTime;started=true;
                return BeginProp(out error)?RuleActionState.Ready:RuleActionState.Failed;
            }
            public override void Tick() {if(started&&motion!=null&&model)model.SampleMotion(motion,Time.unscaledTime-began,loop);}
            protected override void ReleasePlayback() {motion?.Dispose();motion=null;if(model)model.Stop();}
        }
    }
}
