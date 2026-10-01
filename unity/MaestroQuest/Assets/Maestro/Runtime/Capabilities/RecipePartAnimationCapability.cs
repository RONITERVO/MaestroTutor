// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class RecipePartAnimationCapability:CapabilityModule
    {
        public override string Id=>"animation.recipe.part";
        public override string Label=>"Recipe part animation";
        public override string Duration=>"timed";
        public override string Ownership=>"exclusiveChannels";
        public override IReadOnlyList<string> Channels=>new[]{"recipePart"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.recipe","target.unheld","authoring.inactive","recipe.part.trackAvailable"};
        internal static JObject PartId()=>Text("^[a-zA-Z0-9_]{1,32}$",32);
        public override JObject InputSchema {get{var schema=Object(new JObject{["target"]=RecipeEditCapability.Target(),["part"]=PartId(),["seconds"]=Number(0,30),["loop"]=new JObject{["type"]="boolean"}});schema["x-features"]=new JArray("recipePartPlayback.v1");return schema;}}
        public override BehaviourCatalog.Claim[] Claims(JObject args)=>new[]{new BehaviourCatalog.Claim((string)args["target"],"recipePart:"+(string)args["part"])};
        public override bool CanRun(CapabilityContext context,JObject args,out string error)
        {
            if(!context.Target(args,out var item,out error))return false;
            var recipe=item.GetComponent<RecipeObject>();
            if(!recipe||!recipe.isActiveAndEnabled||!recipe.HasTrack((string)args["part"])) {error="This recipe part has no saved animation track";return false;}
            return true;
        }
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(context,args,out error))return false;
            var target=context.Editor.Find((string)args["target"]);var recipe=target.GetComponent<RecipeObject>();
            if(!recipe.BeginPart((string)args["part"],(bool)args["loop"],out var playback,out error))return false;
            float seconds=(float)args["seconds"];if(seconds==0)seconds=context.Editor.Read((string)args["target"]).recipe.duration;
            operation=new PartOperation(context,target,(string)args["target"],recipe,playback,seconds);return true;
        }
        sealed class PartOperation:CapabilityOperation
        {
            readonly CapabilityContext context;readonly RoomItem target;readonly string targetId;readonly RecipeObject recipe;readonly RecipeObject.PartPlayback playback;readonly float seconds;
            public PartOperation(CapabilityContext context,RoomItem target,string targetId,RecipeObject recipe,RecipeObject.PartPlayback playback,float seconds){this.context=context;this.target=target;this.targetId=targetId;this.recipe=recipe;this.playback=playback;this.seconds=seconds;}
            public override float Seconds=>seconds;
            public override RuleActionState State(out string error){error=null;if(context.Editor&&context.Editor.Find(targetId)==target&&recipe&&recipe.OwnsPart(playback))return RuleActionState.Ready;error="Recipe part playback was interrupted or its object changed";return RuleActionState.Failed;}
            public override bool Complete(out string error){if(State(out error)!=RuleActionState.Ready)return false;return recipe.FinishPart(playback,seconds);}
            public override void Stop(bool preservePlacement){if(recipe)recipe.EndPart(playback);}
        }
    }
}
