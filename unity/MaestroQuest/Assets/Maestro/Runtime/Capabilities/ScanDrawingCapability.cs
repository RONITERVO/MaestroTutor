// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class ScanDrawingCapability:NativeResourceInputsCapability
    {
        internal const string Feature="scanDrawingLayers.v1";
        public override string Id=>"drawing.layer.edit";
        public override string Label=>"Create or rebind scanned ink";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[]{"room.scan.current","room.active","storage.writable","drawing.inactive"};
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override BehaviourCatalog.Claim[] Claims(JObject args)=>(string)args["operation"]=="rebind"?base.Claims(args):Array.Empty<BehaviourCatalog.Claim>();
        internal override int MaximumCreatedObjects(JObject args)=>(string)args["operation"]=="rebind"?0:1;
        public override string Description=>"Create a surface layer for ink or a passthrough opening on an exact loaded scanned plane, or explicitly rebind a saved layer. Read room.scan for stateId. create selects anchorId and plane-local x/y; atGaze samples the tracked viewer's current ray and picks its first front-facing scanned plane within 4 m, never skipping a hit that is too small to find a farther wall. The user should look at the desired wall when invoking it. Width/height are .02–4 metres; angle is roll about the scan's outward normal. The layer faces outward 6 mm above the plane and must fit the loaded rectangle/polygon boundary. This is a virtual overlay, not exact paint on arbitrary triangles, glass, doors, moving surfaces or openings absent from the scan. It creates no background panel or solid collision surface. object.window.edit can attach a passthrough mask to Canvas independently of ink; missing anchors hide both, and rebind preserves both. Use object.surface.edit on its Canvas surface for the same drawing, erasing, clear, dimensions and Undo as other ink. The Canvas stays a root plane. Scanned layers cannot be moved/grabbed/scaled/animated as loose objects or configured for physics; use rebind to change exact anchor/x/y/angle and object.delete to remove a layer. Unknown/missing/wrong-room anchors hide ink and disable contact while preserving saved source. Loaded matching anchors restore visibility if the whole layer still fits; no label/proximity substitution. Active drawing is retained on anchor loss or changed geometry. Rebind preserves ink, requires the exact object revision and adds one saved Undo; create also adds one Undo. No permission, scanning, physics startup or provider request occurs. Temporary edits stay in the fork. Success means source saved, not verified real-room alignment or future tracking.";
        static JObject Variant(string operation)
        {
            bool rebind=operation=="rebind";var fields=new JObject{["operation"]=Choice(operation),["stateId"]=Text("^[a-f0-9]{32}$",32),["angle"]=Number(-180,180)};
            if(rebind){fields["target"]=DrawingData.Target();fields["revision"]=Revision();}else{fields["name"]=Text("^.{0,80}$",80);fields["width"]=Number(.02,4);fields["height"]=Number(.02,4);}
            if(operation!="atGaze"){fields["anchorId"]=Text("^[a-f0-9]{32}$",32);fields["x"]=Number(-25,25);fields["y"]=Number(-25,25);}
            var schema=rebind?CurrentInputs(Object(fields),"object.scanDrawing","revision",new JObject{["target"]="target"},"stateId"):CurrentInputs(Object(fields),"room.scan","stateId");if(rebind)((JArray)schema["x-current"]["guards"]).Add("stateId");schema["x-features"]=new JArray(Feature);schema["title"]=operation=="atGaze"?"Create where you look":rebind?"Rebind saved ink":"Create on an exact plane";return schema;
        }
        public override JObject InputSchema=>new(){["type"]="object",["x-discriminators"]=new JArray("operation"),["oneOf"]=new JArray(Variant("create"),Variant("atGaze"),Variant("rebind"))};
        public override JObject OutputSchema=>Object(new JObject{["objectId"]=DrawingData.Target(),["revision"]=Revision(),["surface"]=Choice("Canvas"),["roomId"]=Text("^[a-f0-9]{32}$",32),["anchorId"]=Text("^[a-f0-9]{32}$",32),["temporary"]=new JObject{["type"]="boolean"}});
        public override JObject Example=>new(){["operation"]="atGaze",["stateId"]=new string('0',32),["name"]="Wall drawing",["width"]=.6f,["height"]=.4f,["angle"]=0};
        public override bool CanRun(CapabilityContext c,JObject a,out string error)=>c.Editor.PrepareScanDrawing(a,out _,out error);
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation operation,out string error){operation=null;if(!c.Editor.EditScanDrawing(a,out var result,out error))return false;operation=new CompletedCapability(result);return true;}
        internal static BehaviourCatalog.FactDefinition Fact()=>new("object.scanDrawing",ProgramDataType.Read(JObject.Parse("{\"record\":{\"stateId\":\"text\",\"revision\":\"number\",\"anchor\":{\"record\":{\"roomId\":\"text\",\"anchorId\":\"text\",\"x\":\"number\",\"y\":\"number\",\"angle\":\"number\"}},\"width\":\"number\",\"height\":\"number\",\"visible\":\"boolean\",\"reason\":\"text\"}}")),"Saved scanned ink anchor","Current scan stateId (empty when unavailable), exact saved Meta identities and plane-local placement, layer size, current object revision and live availability. Missing/wrong-room/untracked anchors hide ink without deleting it. visible means the view is attached, not proof of headset rendering, physical alignment, no occlusion or intact real walls. Only explicit rebind changes saved identities. Ordinary drawings are unavailable for this fact; use object.surfaces for their ink.",Object(new JObject{["target"]=DrawingData.Target()}),new JObject{["target"]=new string('0',32)},(c,a)=>{var v=c.Editor?c.Editor.ObserveScanDrawing((string)a["target"]):null;return v==null?null:ProgramValue.Literal(v);},features:new[]{Feature});
    }
}
