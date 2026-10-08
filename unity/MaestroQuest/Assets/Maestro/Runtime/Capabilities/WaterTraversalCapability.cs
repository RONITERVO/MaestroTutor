// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal sealed class WaterTraversalCapability:SpatialSettingsCapability {
        internal const string Feature="waterTraversal.v1";
        internal static JObject SettingsSchema(bool saved=false){
            var fields=new JObject{["mode"]=Choice("default","ignore","avoid","wade"),["maxDepthMetres"]=Number(0,2)};
            if(saved)fields["version"]=Number(1,1,true);
            var schema=Object(fields);schema["x-features"]=new JArray(Feature);return schema;
        }
        static JObject Target()=>Resource(Text("^(maestro|[a-fA-F0-9]{32})$",32));
        public override string Id=>"object.water.traversal.configure";
        public override string Label=>"Configure water traversal";
        public override string Description=>"Choose how Maestro walking and recorded root movement, or an opted-in creation's recorded root movement, respond to finite liquid cavities. default means Maestro avoids water and other objects ignore it. avoid stops at water; wade permits depth up to maxDepthMetres in authored-room metres, capped at 60% of walking body height; ignore is for deliberate non-walking movement. Swimming and water-aware route detours are not provided. Swept upright body checks use live fill levels, including when forces are paused. This changes neither rigid-body buoyancy, gripping, explicit placement, real-room collision profiles nor visibility. Read object.water.traversal.settings and preserve unrequested settings."+SaveRules;
        public override JObject InputSchema{get{var fields=Fields(Target());foreach(var p in ((JObject)SettingsSchema()["properties"]).Properties())fields[p.Name]=p.Value.DeepClone();var schema=Featured(Object(fields));((JArray)schema["x-features"]).Add(Feature);return CurrentInputs(schema,"object.water.traversal.settings","revision",new JObject{["target"]="target"},"mode","maxDepthMetres");}}
        public override JObject Example=>new(){["target"]="maestro",["revision"]=1,["mode"]="wade",["maxDepthMetres"]=.2};
        static RoomWaterTraversal Settings(JObject a)=>new(){mode=(string)a["mode"],maxDepthMetres=(float)a["maxDepthMetres"]};
        protected override bool Ready(RoomEditor e,JObject a,out string error)=>e.CanConfigureWaterTraversal((string)a["target"],(int)a["revision"],Settings(a),out error);
        protected override bool Save(RoomEditor e,JObject a,out string error)=>e.ConfigureWaterTraversal((string)a["target"],(int)a["revision"],Settings(a),out error);
        internal static BehaviourCatalog.FactDefinition Fact()=>new("object.water.traversal.settings",ProgramDataType.Read(JObject.Parse("{\"record\":{\"target\":\"text\",\"revision\":\"number\",\"mode\":\"text\",\"effectiveMode\":\"text\",\"maxDepthMetres\":\"number\",\"temporary\":\"boolean\"}}")),"Water traversal settings","Accepted actor policy and exact revision. default resolves to avoid for Maestro and ignore for creations. The saved wading depth is additionally limited by actual body height. This read does not move, swim or alter physics.",Object(new JObject{["target"]=Target()}),new JObject{["target"]="maestro"},(c,a)=>c.Editor?.ObserveWaterTraversal((string)a["target"]) is JObject value?ProgramValue.Literal(value):null,features:new[]{Feature});
        internal static BehaviourCatalog.FactDefinition Path()=>new("object.water.traversal.path",ProgramDataType.Read(JObject.Parse("{\"record\":{\"allowed\":\"boolean\",\"bodyId\":\"text\",\"depthMetres\":\"number\",\"reason\":\"text\"}}")),"Water path inspection","Read-only continuous liquid check from the target's current upright walking envelope to an authored-room position. It samples current fill levels and per-actor environment admission. allowed proves only water policy, not terrain, obstacle clearance, ownership or a connected route. bodyId is the refusing vessel, or the deepest allowed vessel. No movement or physics start.",Object(new JObject{["target"]=Target(),["position"]=Object(new JObject{["x"]=Number(-25,25),["y"]=Number(-25,25),["z"]=Number(-25,25)})}),new JObject{["target"]="maestro",["position"]=new JObject{["x"]=0,["y"]=0,["z"]=0}},(c,a)=>c.Editor?.ObserveWaterPath((string)a["target"],new UnityEngine.Vector3((float)a["position"]["x"],(float)a["position"]["y"],(float)a["position"]["z"])) is JObject value?ProgramValue.Literal(value):null,features:new[]{Feature});
    }
}
