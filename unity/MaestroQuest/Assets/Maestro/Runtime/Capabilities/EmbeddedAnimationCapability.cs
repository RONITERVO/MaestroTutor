// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Avatar;
using Maestro.Quest.Imports;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class EmbeddedAnimationCapability : FullBodyCapability
    {
        public override string Id=>"animation.embedded.play";
        public override string Label=>"Imported clip";
        public override IReadOnlyList<string> Requirements=>new[] {"target.exists","target.unheld","authoring.inactive","model.loaded","embeddedClip.available"};
        public override JObject InputSchema=>Object(new JObject {
            ["target"]=AnimationTargets.TargetSchema(),["seconds"]=Number(0,30),["loop"]=new JObject {["type"]="boolean"},
            ["modelHash"]=Text("^(|[a-f0-9]{64})$",64),["clipIndex"]=Number(0,31,true),["prop"]=AnimationTargets.PropSchema(),["movement"]=AnimationTargets.MovementSchema()
        },"prop","movement");
        public override bool CanRun(CapabilityContext context,JObject arguments,out string error) {
            if(!base.CanRun(context,arguments,out error)||!AnimationTargets.MovementReady(context,arguments,out error))return false;
            string id=(string)arguments["target"],hash=(string)arguments["modelHash"];int index=(int)arguments["clipIndex"];
            var target=context.Editor.Find(id);var model=AnimationTargets.ClipModel(target);var avatar=target.GetComponent<MaestroAvatar>();
            if(string.IsNullOrEmpty(hash)||context.Editor.Read(id).modelHash!=hash||!model||!model.Ready||
                avatar&&(avatar.ModelBusy||avatar.ModelHash!=hash)||index<0||index>=model.ClipCount||model.ClipDuration(index)<=0)
            {error="Choose a clip from the target's current loaded model using Motion";return false;}
            if(model.PlaybackIssue!=null){error=model.PlaybackIssue;return false;}
            if((float)arguments["seconds"]==0&&model.ClipDuration(index)>30) {error="Choose an explicit duration for clips longer than 30 seconds";return false;}
            return true;
        }
        public override bool Start(CapabilityContext context,string runId,JObject arguments,out CapabilityOperation operation,out string error) {
            var value=new EmbeddedOperation(context,arguments);operation=value;return value.Begin((int)arguments["clipIndex"],(bool)arguments["loop"],out error);
        }
        sealed class EmbeddedOperation : FullBodyOperation {
            readonly bool authored;
            ImportedModel model;
            public EmbeddedOperation(CapabilityContext context,JObject arguments):base(context,arguments) {authored=(string)arguments["movement"]=="authored";}
            protected override bool RetainPlacement=>authored;
            public override bool Complete(out string error){if(authored&&Avatar&&!Avatar.FinishImportedAt(Duration,out error))return false;return base.Complete(out error);}
            public bool Begin(int index,bool loop,out string error) {
                if(!Acquire(out error))return false;model=AnimationTargets.ClipModel(Target);
                if(Duration==0)Duration=Mathf.Max(.1f,model.ClipDuration(index));
                if(Avatar) {
                    if(Avatar.PlayImportedClip(index,loop,authored))return BeginProp(out error);
                    error=Avatar.ImportedPlaybackError??"This motion is unavailable; choose a loaded clip";return false;
                }
                model.Play(index,loop);return true;
            }
            public override Maestro.Quest.Rules.RuleActionState State(out string error){if(base.State(out error)==Maestro.Quest.Rules.RuleActionState.Failed)return Maestro.Quest.Rules.RuleActionState.Failed;if(authored&&Avatar&&Avatar.ImportedPlaybackError!=null){error=Avatar.ImportedPlaybackError;return Maestro.Quest.Rules.RuleActionState.Failed;}return Maestro.Quest.Rules.RuleActionState.Ready;}
            protected override void ReleasePlayback() {if(model)model.Stop();}
        }
    }
}
