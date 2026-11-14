// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal sealed class StaticSupportCapability:SpatialSettingsCapability {
        internal const string Feature="staticSupport.v1";
        static JObject Target()=>Resource(Text("^[a-fA-F0-9]{32}$",32));
        public override string Id=>"object.support.set";
        public override string Label=>"Use as walking support";
        public override string Description=>"Choose whether a fixed creation's complete selected primary collision supports walking and virtual-world physics. Uses its real box or compatible compound mesh colliders, including their holes; never adds an invisible floor. Requires fixed physics and no saved root recording. A sphere/capsule or mixed unsupported collision set is refused. Imported models need an explicit compatible automatic collision recipe; imported rigid meshes retain their separate walkable setting. For stairs author separate tread collision boxes; a decorative assembly's enclosing box is not a staircase. Grip, carried motion, manipulation and restoration suspend availability without deleting the setting. Reading object.support exposes saved intent and current readiness. This does not change real-room collision policy, visibility, terrain, physics playback or collision shapes."+SaveRules;
        public override JObject InputSchema {get{
            var fields=Fields(Target());fields["enabled"]=new JObject{["type"]="boolean"};
            var schema=Featured(Object(fields));((JArray)schema["x-features"]).Add(Feature);
            return CurrentInputs(schema,"object.support","revision",new JObject{["target"]="target"},"enabled");
        }}
        public override JObject Example=>new(){["target"]=new string('0',32),["revision"]=1,["enabled"]=true};
        static RoomStaticSupport Settings(JObject args)=>new(){enabled=(bool)args["enabled"]};
        protected override bool Ready(RoomEditor editor,JObject args,out string error)=>editor.CanConfigureStaticSupport((string)args["target"],(int)args["revision"],Settings(args),out error);
        protected override bool BeginSave(RoomEditor editor,JObject args,out RoomEditor.PendingPersistedEdit pending,out string error)=>
            editor.BeginConfigureStaticSupport((string)args["target"],(int)args["revision"],Settings(args),out pending,out error);
        static ProgramDataType Type()=>OutputType(Object(new JObject{["target"]=Target(),["revision"]=Revision(),
            ["enabled"]=new JObject{["type"]="boolean"},["ready"]=new JObject{["type"]="boolean"},["reason"]=Text(null,128),["temporary"]=new JObject{["type"]="boolean"}}));
        internal static BehaviourCatalog.FactDefinition Fact()=>new("object.support",Type(),"Walking support",
            "Saved primary static support intent and exact object revision, plus live readiness and bounded reason. Readable while native content is unloaded; never activates geometry, changes collision or starts physics. ready does not prove a route, clearance, supported slope or step. Terrain and imported rigid mesh support keep their own settings.",
            Object(new JObject{["target"]=Target()}),new JObject{["target"]=new string('0',32)},
            (context,args)=>context.Editor?.ObserveStaticSupport((string)args["target"]) is JObject value?ProgramValue.Literal(value,Type()):null,features:new[]{Feature});
    }
}
