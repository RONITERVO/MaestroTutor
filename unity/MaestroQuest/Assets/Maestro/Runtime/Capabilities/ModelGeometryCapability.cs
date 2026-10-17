// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class ModelGeometryCapability:NativeTargetCapability
    {
        internal const string Feature="modelGeometry.v1";
        public override string Id=>"object.model.geometry.set";
        public override string Label=>"Set imported model geometry";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.created","target.unheld","source.revision.current","model.loaded","storage.writable"};
        public override string Description=>"Set the scale, pivot and collision geometry of an imported room object. Read object.model.geometry for its current settings, exact model hash, source dimensions, local dimensions and revision. Pause physics and stop animation first. fitted keeps the usual 0.35m largest dimension and requires metresPerUnit=1; source uses explicit metres per source unit (glTF is normally metres). center centers geometry, base puts its bottom at the object origin, and source retains its imported origin. Object placement/rotation/scale and separately authored components remain unchanged. Local geometry is limited to 12.5m before the object scale, and a source pivot may be at most 12.5m from its centre. meshCollision preserves exact rigid mesh holes/interiors, at most 32 meshes and 50,000 triangles; it requires fixed physics, automatic shape, no custom collision recipe and no recorded root motion. Skins/blend shapes are refused. Embedded/library playback is disabled while rigid mesh collision is enabled. walkable additionally publishes these colliders as accepted ground; slope, step, body clearance and water policies still apply. Turning mesh collision off restores the ordinary bounds/custom-shape path. Real-room participation, visibility, sound and camera sharing remain independent. One saved edit and Undo, retained by copies/blueprints/archives. These are bounded active-region settings, not streamed-world support.";
        internal static JObject SettingsSchema(bool version=true)
        {
            var p=new JObject{["scaleMode"]=Choice("fitted","source"),["metresPerUnit"]=Number(.001,100),["pivot"]=Choice("center","base","source"),["meshCollision"]=new JObject{["type"]="boolean"},["walkable"]=new JObject{["type"]="boolean"}};
            if(version)p["version"]=Number(1,1,true);var schema=Object(p);schema["x-features"]=new JArray(Feature);return schema;
        }
        public override JObject InputSchema=>CurrentInputs(Object(new JObject{["target"]=DrawingData.Target(),["revision"]=Revision(),["settings"]=SettingsSchema()}),"object.model.geometry","revision",new JObject{["target"]="target"},"settings");
        public override JObject OutputSchema=>Object(new JObject{["target"]=DrawingData.Target(),["revision"]=Revision()});
        public override JObject Example=>new(){["target"]=new string('0',32),["revision"]=1,["settings"]=new JObject{["version"]=1,["scaleMode"]="source",["metresPerUnit"]=1,["pivot"]="base",["meshCollision"]=true,["walkable"]=true}};
        public override bool CanRun(CapabilityContext c,JObject a,out string error)=>c.Target(a,out _,out error)&&c.Editor.PrepareModelGeometry((string)a["target"],(int)a["revision"],a["settings"].ToObject<RoomModelGeometry>(),out _,out error);
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(c,a,out error)||!c.Editor.EditModelGeometry((string)a["target"],(int)a["revision"],a["settings"].ToObject<RoomModelGeometry>(),out error))return false;
            operation=new CompletedCapability(new JObject{["target"]=(string)a["target"],["revision"]=c.Editor.ObjectRevision((string)a["target"])});return true;
        }
        internal static BehaviourCatalog.FactDefinition Fact()
        {
            var vector=new JObject{["record"]=new JObject{["x"]="number",["y"]="number",["z"]="number"}};
            var type=ProgramDataType.Read(new JObject{["record"]=new JObject{["target"]="text",["revision"]="number",["modelHash"]="text",["settings"]=new JObject{["record"]=new JObject{["version"]="number",["scaleMode"]="text",["metresPerUnit"]="number",["pivot"]="text",["meshCollision"]="boolean",["walkable"]="boolean"}},["ready"]="boolean",["reason"]="text",["sourceSize"]=vector.DeepClone(),["size"]=vector.DeepClone()}});
            return new("object.model.geometry",type,"Imported model geometry","Saved model settings and actual load/preparation readiness. sourceSize is in source units; size is local metres before the owning object's scale. ready does not certify a walking route or headset performance. Null for non-model objects. Read the current revision before editing.",Object(new JObject{["target"]=DrawingData.Target()}),new JObject{["target"]=new string('0',32)},(c,a)=>{var value=c.Editor?c.Editor.ObserveModelGeometry((string)a["target"]):null;return value==null?null:ProgramValue.Literal(value,type);},features:new[]{Feature});
        }
    }
}
