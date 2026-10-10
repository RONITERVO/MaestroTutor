// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Avatar;
using Maestro.Quest.Rules;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal abstract class SpatialCapability : NativeTargetCapability
    {
        protected abstract AvatarSpatialMode Mode {get;}
        public override string Duration=>"timed";
        public override string Ownership=>"exclusiveChannels";
        public override JObject InputSchema=>Object(new JObject {["target"]=AnimationTargets.AvatarSchema(),["seconds"]=Number(.1,30)},"prop");
        public override bool CanRun(CapabilityContext context,JObject arguments,out string error) {
            if(!context.Target(arguments,out var target,out error,allowUpperBody:true))return false;
            var spatial=target.GetComponent<AvatarSpatialMotion>();
            if(!spatial) {error="This target has no Maestro movement controls";return false;}
            return spatial.CanBegin(Mode,out error);
        }
        public override bool Start(CapabilityContext context,string runId,JObject arguments,out CapabilityOperation operation,out string error) {
            var value=new SpatialOperation(context.Editor.Find((string)arguments["target"]).GetComponent<AvatarSpatialMotion>(),runId,(float)arguments["seconds"]);
            operation=value;return value.Begin(Mode,out error);
        }
        sealed class SpatialOperation : CapabilityOperation {
            readonly AvatarSpatialMotion spatial;readonly string runId;readonly float seconds;bool stopped;
            public SpatialOperation(AvatarSpatialMotion spatial,string runId,float seconds) {this.spatial=spatial;this.runId=runId;this.seconds=seconds;}
            public override float Seconds=>seconds;
            public bool Begin(AvatarSpatialMode mode,out string error)=>spatial.Begin(runId,mode,out error,RoomActorRole.Program);
            public override RuleActionState State(out string error) {
                error=null;if(spatial&&spatial.OwnedBy(runId))return RuleActionState.Ready;
                error=spatial?spatial.Status:"Maestro movement was removed";return RuleActionState.Failed;
            }
            public override void Stop(bool preservePlacement) {if(stopped)return;stopped=true;if(spatial)spatial.End(runId);}
        }
    }
    internal sealed class LookAtUserCapability : SpatialCapability
    {
        public override string Id=>"avatar.look.user";
        public override string Label=>"Look at user";
        protected override AvatarSpatialMode Mode=>AvatarSpatialMode.Look;
        public override IReadOnlyList<string> Channels=>new[] {"gaze"};
        public override IReadOnlyList<string> Requirements=>new[] {"target.exists","target.unheld","authoring.inactive","avatar.spatialReady"};
    }
    internal sealed class FollowUserCapability : SpatialCapability
    {
        public override string Id=>"avatar.follow.user";
        public override string Label=>"Follow user";
        protected override AvatarSpatialMode Mode=>AvatarSpatialMode.Follow;
        public override IReadOnlyList<string> Channels=>new[] {"locomotion","gaze"};
        public override IReadOnlyList<string> Requirements=>new[] {"target.exists","target.unheld","authoring.inactive","avatar.spatialReady","physics.running","navigation.floorReady"};
    }
}
