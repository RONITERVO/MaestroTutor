// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class RecipeAnimationCapability : FullBodyCapability
    {
        public override string Id=>"animation.recipe.play";
        public override string Label=>"Recipe animation";
        public override IReadOnlyList<string> Requirements=>new[] {"target.exists","target.unheld","authoring.inactive","recipe.tracksAvailable"};
        public override JObject InputSchema=>Object(new JObject {
            ["target"]=AnimationTargets.TargetSchema(),["seconds"]=Number(0,30),["loop"]=new JObject {["type"]="boolean"}
        },"prop");
        public override bool CanRun(CapabilityContext context,JObject arguments,out string error) {
            if(!base.CanRun(context,arguments,out error))return false;string id=(string)arguments["target"];
            var recipe=context.Editor.Read(id)?.recipe;
            if(recipe!=null&&recipe.tracks.Length>0&&context.Editor.Find(id).GetComponent<RecipeObject>())return true;
            error="This object has no recipe animation";return false;
        }
        public override bool Start(CapabilityContext context,string runId,JObject arguments,out CapabilityOperation operation,out string error) {
            var value=new RecipeOperation(context,arguments);operation=value;return value.Begin((bool)arguments["loop"],out error);
        }
        sealed class RecipeOperation : FullBodyOperation {
            RecipeObject recipe;
            public RecipeOperation(CapabilityContext context,JObject arguments):base(context,arguments) {}
            public bool Begin(bool loop,out string error) {
                if(!Acquire(out error))return false;recipe=Target.GetComponent<RecipeObject>();
                if(Duration==0)Duration=Context.Editor.Read(TargetId).recipe.duration;recipe.StartRule(loop);return true;
            }
            protected override void ReleasePlayback() {if(recipe)recipe.StopRule();}
        }
    }
}
