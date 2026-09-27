// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Avatar;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class GestureCapability : FullBodyCapability
    {
        public override string Id=>"avatar.gesture.play";
        public override string Label=>"Gesture";
        public override IReadOnlyList<string> Requirements=>new[] {"target.exists","target.unheld","authoring.inactive","avatar.available"};
        public override JObject InputSchema=>Object(new JObject {
            ["target"]=AnimationTargets.AvatarSchema(),["seconds"]=Number(.1,30),
            ["gesture"]=Choice("greeting","pointing","listening","speaking","idle","walk"),["prop"]=AnimationTargets.PropSchema()
        },"prop");
        public override bool CanRun(CapabilityContext context,JObject arguments,out string error) {
            if(!base.CanRun(context,arguments,out error))return false;
            if(context.Editor.Find((string)arguments["target"]).GetComponent<MaestroAvatar>())return true;
            error="Gestures need a compatible Maestro avatar";return false;
        }
        public override bool Start(CapabilityContext context,string runId,JObject arguments,out CapabilityOperation operation,out string error) {
            var value=new GestureOperation(context,arguments);operation=value;return value.Begin(AnimationTargets.GestureName(arguments),out error);
        }
        sealed class GestureOperation : FullBodyOperation {
            public GestureOperation(CapabilityContext context,JObject arguments):base(context,arguments) {}
            public bool Begin(string gesture,out string error) {if(!Acquire(out error))return false;Avatar.Gesture(gesture);return BeginProp(out error);}
        }
    }
    internal sealed class UpperBodyGestureCapability : CapabilityModule
    {
        public override string Id=>"avatar.gesture.upperBody";
        public override string Label=>"Upper-body gesture";
        public override string Duration=>"timed";
        public override string Ownership=>"exclusiveChannels";
        public override IReadOnlyList<string> Channels=>new[] {"upperBody"};
        public override IReadOnlyList<string> Requirements=>new[] {"target.exists","target.unheld","authoring.inactive","avatar.available"};
        public override JObject InputSchema=>Object(new JObject {
            ["target"]=AnimationTargets.AvatarSchema(),["seconds"]=Number(.1,30),
            ["gesture"]=Choice("greeting","pointing","listening","speaking","idle")
        },"prop");
        public override bool CanRun(CapabilityContext context,JObject arguments,out string error) {
            if(!context.Target(arguments,out var target,out error,allowSpatial:true))return false;
            if(target.GetComponent<MaestroAvatar>())return true;
            error="Gestures need a compatible Maestro avatar";return false;
        }
        public override bool Start(CapabilityContext context,string runId,JObject arguments,out CapabilityOperation operation,out string error) {
            var value=new UpperBodyOperation(context.Editor.Find((string)arguments["target"]).GetComponent<MaestroAvatar>(),runId,(float)arguments["seconds"]);
            operation=value;return value.Begin(AnimationTargets.GestureName(arguments),out error);
        }
        sealed class UpperBodyOperation : CapabilityOperation {
            readonly MaestroAvatar avatar;readonly string runId;readonly float seconds;bool stopped;
            public UpperBodyOperation(MaestroAvatar avatar,string runId,float seconds) {this.avatar=avatar;this.runId=runId;this.seconds=seconds;}
            public override float Seconds=>seconds;
            public bool Begin(string gesture,out string error) {error=null;if(avatar.BeginUpperBody(runId,gesture))return true;error="This upper-body gesture is unavailable";return false;}
            public override RuleActionState State(out string error) {
                error=null;if(avatar&&avatar.UpperBodyOwnedBy(runId))return RuleActionState.Ready;
                error="The upper-body gesture was interrupted";return RuleActionState.Failed;
            }
            // Never restore the root or stop a clip owned by walking.
            public override void Stop(bool preservePlacement) {if(stopped)return;stopped=true;if(avatar)avatar.EndUpperBody(runId);}
        }
    }
}
