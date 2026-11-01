// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class ObjectFeetCapability:SpatialSettingsCapability
    {
        internal const string Feature="humanoidFootPlacement.v1";
        static JObject Target()=>Resource(Text("^(maestro|[a-fA-F0-9]{32})$",32));
        public override string Id=>"object.feet.configure";
        public override string Label=>"Fit character feet to ground";
        public override string Description=>"Optional, bounded foot placement for Maestro and loaded imported humanoids. Read object.feet.settings first. grounded binds calibration to the exact current modelHash; authored removes it. The rest mesh lower bound estimates shoe contact; leftClearance and rightClearance trim it in world metres. Uses accepted ground and this actor's real-room collision policy. Only idle and supported navigation gait receive a removable leg/hips correction. Explicit clips, saved poses, authoring, held or dynamic bodies retain their authored pose. Missing ground, unsupported rigs or model changes never invent a floor or stretch legs. This does not provide arbitrary stair gait, swimming, crowd routing or physically simulated ragdolls. Visibility and room collision settings stay independent."+SaveRules;
        internal static JObject SavedSchema()
        {
            var s=Object(new JObject{["version"]=Number(1,1,true),["enabled"]=new JObject{["type"]="boolean"},["modelHash"]=Text("^(|[a-f0-9]{64})$",64),
                ["leftClearance"]=Number(-.1,.1),["rightClearance"]=Number(-.1,.1)});
            s["x-features"]=new JArray(Feature);return s;
        }
        public override JObject InputSchema
        {
            get{
                var fields=Fields(Target());fields["mode"]=Choice("authored","grounded");fields["modelHash"]=Text("^(|[a-f0-9]{64})$",64);
                fields["leftClearance"]=Number(-.1,.1);fields["rightClearance"]=Number(-.1,.1);
                var schema=CurrentInputs(Object(fields),"object.feet.settings","revision",new JObject{["target"]="target"},"mode","modelHash","leftClearance","rightClearance");
                schema["x-features"]=new JArray(Feature);return schema;
            }
        }
        public override JObject Example=>new(){["target"]="maestro",["revision"]=1,["mode"]="grounded",["modelHash"]="",["leftClearance"]=0,["rightClearance"]=0};
        static RoomFootPlacement Settings(JObject a)=>(string)a["mode"]=="authored"?new():new(){enabled=true,modelHash=(string)a["modelHash"],leftClearance=(float)a["leftClearance"],rightClearance=(float)a["rightClearance"]};
        protected override bool Ready(RoomEditor e,JObject a,out string error)=>e.CanConfigureFeet((string)a["target"],(int)a["revision"],Settings(a),out error);
        protected override bool BeginSave(RoomEditor e,JObject a,out RoomEditor.PendingPersistedEdit pending,out string error)=>e.BeginConfigureFeet((string)a["target"],(int)a["revision"],Settings(a),out pending,out error);
        internal static BehaviourCatalog.FactDefinition Fact()
        {
            var type=ProgramDataType.Read(new JObject{["record"]=new JObject{
                ["target"]="text",["revision"]="number",["temporary"]="boolean",["mode"]="text",["modelHash"]="text",
                ["leftClearance"]="number",["rightClearance"]="number",["available"]="boolean",["status"]="text",
                ["calibration"]="text",["presentation"]="text"}});
            return new("object.feet.settings",type,"Character foot placement","Saved exact-model calibration, current availability and passive presentation status. Calibration uses estimated rest-mesh bounds; verify visually and trim shoe clearances. Does not load a missing model, activate areas or certify a route.",
                Object(new JObject{["target"]=Target()}),new JObject{["target"]="maestro"},
                (c,a)=>{var value=c.Editor?c.Editor.ObserveFeet((string)a["target"]):null;return value==null?null:ProgramValue.Literal(value,type);},features:new[]{Feature});
        }
    }
}
