// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal sealed class MaterialPackToolCapability:CapabilityModule {
        internal const string Feature="physicalMaterialPacking.v1";
        public override string Id=>"material.pack.tool.set";
        public override string Label=>"Choose the physical material packing tool";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[]{"authoring.idle","room.active"};
        public override string Description=>"Enable/disable touch-and-lift material packing with a local footprint radius, requested local litres and explicit ball mass. Hands: separate from the accepted surface after enabling, touch with an index fingertip, then lift. Controller: hold trigger with its pointer within 25 cm, then release or leave the surface. One contact previews one ball; dragging/waiting cannot multiply material. Uses the same object.material.pack evaluator, saved measured store, sphere recipe/collision and atomic Undo. Ball appears above the original contact with 25 mm clearance; solid obstructions refuse, including scanned geometry. It is not automatically held or thrown. Grab it normally after successful publication. Choosing settings changes no objects and never starts physics. Enabling puts away drawing, sculpting and construction selection. Failed saves/tracking loss retain the draft; inspect material.pack.capture and use object.field.resolve to retry/discard. Retry requires the original source/pose and clear ball space. Settings are session-local, quantities independent of world scale. No finger/granular/compaction physics.";
        internal static JObject Settings()=>Object(new JObject{["enabled"]=new JObject{["type"]="boolean"},["radius"]=Number(.005,2),["amountLitres"]=Number(.001,20),["mass"]=Number(.05,20)});
        public override JObject InputSchema{get{var s=Settings();s["x-features"]=new JArray(Feature);return s;}}
        public override JObject Example=>new(){["enabled"]=true,["radius"]=.12,["amountLitres"]=.25,["mass"]=.15};
        public override bool CanRun(CapabilityContext c,JObject a,out string error)=>c.Editor.Sculpting.CanConfigure(out error);
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation operation,out string error){operation=null;if(!c.Editor.Sculpting.ConfigurePacking((bool)a["enabled"],(float)a["radius"],(double)a["amountLitres"],(float)a["mass"],out error))return false;operation=new CompletedCapability();return true;}
        internal static BehaviourCatalog.FactDefinition SettingsFact()=>new("material.pack.tool",OutputType(Settings()),"Current physical material packing tool","Session-local touch-and-lift/trigger packing settings. Enabling does not create a ball or start physics. Reads do not enable input.",null,null,(c,a)=>{var t=c.Editor?c.Editor.GetComponent<SpatialSculpting>():null;return t?ProgramValue.Literal(new JObject{["enabled"]=t.PackingEnabled,["radius"]=t.PackingRadius,["amountLitres"]=t.PackingLitres,["mass"]=t.PackingMass}):null;},features:new[]{Feature});
        internal static BehaviourCatalog.FactDefinition CaptureFact()=>new("material.pack.capture",OutputType(Object(new JObject{["sessionId"]=Text("^[a-f0-9]{32}$",32),["phase"]=Choice("idle","contact","unsaved"),["field"]=Text("^(|[a-f0-9]{32})$",32),["requestedLitres"]=Number(0,20),["amountLitres"]=Number(0,20),["position"]=Object(new JObject{["x"]=Number(-25,25),["y"]=Number(-25,25),["z"]=Number(-25,25)}),["lastSaved"]=Object(new JObject{["sessionId"]=Text("^(|[a-f0-9]{32})$",32),["objectId"]=Text("^(|[a-f0-9]{32})$",32),["amountLitres"]=Number(0,20)}),["error"]=Text("^.{0,128}$",128)})),"Physical material packing draft","Frozen preview amount and room-local ball position and exact capture session. Use object.field.capture.path for its single local footprint and object.field.resolve for explicit Retry/Discard. lastSaved records the most recent successful physical packing in this live room, not current existence: Undo/deletion may remove that ball. Explicit object.material.pack calls do not set this physical observation. Reads have no effect.",null,null,(c,a)=>{var t=c.Editor?c.Editor.GetComponent<SpatialSculpting>():null;return t?ProgramValue.Literal(t.ObservePacking()):null;},features:new[]{Feature});
    }
}
