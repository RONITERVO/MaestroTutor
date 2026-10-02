// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    /// <summary>One public animation verb; typed sources retain their own native operations.</summary>
    internal sealed class AnimationPlayCapability : CapabilityModule
    {
        internal sealed class Source {
            public readonly string Kind,Channel;public readonly CapabilityModule Provider;public readonly string[] Fields;public readonly CapabilityStepAdapter Adapter;
            public Source(string kind,string channel,CapabilityModule provider,params string[] fields) {Kind=kind;Channel=channel;Provider=provider;Fields=fields;Adapter=new("animation.play",provider,new JObject {["source.kind"]=kind,["channel"]=channel},fields.Select(f=>(f,"source."+f)).ToArray());}
            public JObject Schema {
                get {
                    var schema=Provider.InputSchema;var properties=(JObject)schema["properties"];var required=(JArray)schema["required"];
                    var source=new JObject {["kind"]=Choice(Kind)};source["kind"]["x-static"]=true;
                    foreach(var field in Fields) {source[field]=properties[field].DeepClone();properties.Remove(field);required.Remove(required.FirstOrDefault(x=>(string)x==field));}
                    properties["source"]=Object(source);properties["channel"]=Choice(Channel);properties["channel"]["x-static"]=true;
                    required.Add("source");required.Add("channel");schema["title"]=Provider.Label;
                    schema["x-channels"]=new JArray(Provider.Channels);schema["x-requirements"]=new JArray(Provider.Requirements);return schema;
                }
            }
            public JObject Public(JObject flat)=>Adapter.Public(flat);
            public JObject Native(JObject args)=>Adapter.Native(args);
        }
        internal static readonly Source[] Sources={
            new("gesture","wholeTarget",new GestureCapability(),"gesture"),
            new("gesture","upperBody",new UpperBodyGestureCapability(),"gesture"),
            new("recording","wholeTarget",new RecordedAnimationCapability()),
            new("embedded","wholeTarget",new EmbeddedAnimationCapability(),"modelHash","clipIndex"),
            new("library","wholeTarget",new LibraryAnimationCapability(),"motionId"),
            new("recipe","wholeTarget",new RecipeAnimationCapability()),
            new("recipe","recipePart",new RecipePartAnimationCapability(),"part"),
        };
        internal static Source Find(JObject args)=>Sources.SingleOrDefault(x=>x.Kind==(string)args["source"]?["kind"]&&x.Channel==(string)args["channel"]);
        internal override IEnumerable<CapabilityStepAdapter> StepAdapters=>Sources.Select(s=>s.Adapter);
        public override string Id=>"animation.play";
        public override string Label=>"Play animation";
        public override string Description=>"Choose a typed source and channel. Library and embedded sources keep exact motion/model identities. Only built-in gestures currently support upperBody; wholeTarget owns the complete object. For recipePart choose source.part, an exact saved track ID. Different local part rotations compose, including parent/child parts; the same part conflicts. Playback starts that track at zero, seconds=0 uses its saved duration, and loop repeats it. Stop/completion holds the last pose and suppresses autoplay for that track until explicit whole-recipe Restart. Other tracks continue. Root physics and the stable whole-object collider remain unchanged; parts are visual joints, not independent rigid bodies. Read object.recipe.pose for live local/world pose. Playback never changes saved keys. For Maestro library/embedded clips, optional movement=authored preserves sampled planar body travel through the scanned room, including loop displacement; omitted/inPlace retains navigation-adjusted hips. Authored travel requires running aligned room physics, level connected floor and clear swept body space. It stops on obstacles, proximity to the user, placement changes or lost readiness, retaining the last accepted placement. Body-proxy checks do not certify individual limb/prop contacts or foot planting. Existing activity/walk assignments remain inPlace. Inspect the selected variant's prerequisites; no automatic source substitution.";
        public override string Duration=>"timed";
        public override string Ownership=>"sourceChannels";
        public override IReadOnlyList<string> Channels=>new[] {"wholeTarget","upperBody","recipePart"};
        public override IReadOnlyList<string> Requirements=>new[] {"target.exists","target.unheld","authoring.inactive","source.ready"};
        public override JObject InputSchema=>new() {["type"]="object",["title"]="Source and channel",["oneOf"]=new JArray(Sources.Select(x=>x.Schema)),["x-discriminators"]=new JArray("source.kind","channel")};
        public override JObject Example=>Sources[0].Public(new JObject {["target"]="maestro",["seconds"]=2,["gesture"]="greeting"});
        public override bool Validate(JObject arguments,out string error) {
            var source=Find(arguments);error="Choose a supported animation source and channel";
            return source!=null&&source.Provider.Validate(source.Native(arguments),out error);
        }
        public override BehaviourCatalog.Claim[] Claims(JObject arguments) {var source=Find(arguments);return source.Provider.Claims(source.Native(arguments));}
        public override bool CanRun(CapabilityContext context,JObject arguments,out string error) {var source=Find(arguments);return source.Provider.CanRun(context,source.Native(arguments),out error);}
        public override bool Start(CapabilityContext context,string runId,JObject arguments,out CapabilityOperation operation,out string error) {
            var source=Find(arguments);return source.Provider.Start(context,runId,source.Native(arguments),out operation,out error);
        }
    }
}
