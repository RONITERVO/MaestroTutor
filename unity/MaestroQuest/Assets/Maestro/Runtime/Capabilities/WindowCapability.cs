// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class WindowCapability:NativeTargetCapability
    {
        internal const string Feature="passthroughWindows.v1";
        public override string Id=>"object.window.edit";
        public override string Label=>"Edit a passthrough opening";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.created","target.unheld","source.revision.current","storage.writable"};
        public override string Description=>"Save or remove a rectangular/elliptical passthrough opening on an existing plane surface of a created object. Inspect object.surfaces and object.surface; configure a plane with object.surface.edit first if needed. The window uses that plane's position, rotation, width and height, follows its object or recipe part, and is visible from either side. For an exact physical wall/floor attachment create a scanned layer with drawing.layer.edit, then attach this component to its Canvas: no ink is required. Loaded exact anchors keep it physical during virtual movement; missing anchors hide it without deleting the source. reveal=0 closes the opening; 1 fully reveals physical passthrough where foreground virtual depth permits, multiplied by the host's visual-layer opacity. Surface drawing enabled and ordinary material opacity do not control this mask. Keep a frame's opening free of opaque geometry; this is a plane mask, not a hole cut into the model. Mask depth is the plane's depth, not measured real-world depth. Front opaque geometry and sorted translucent surfaces can cover the opening. Normal transparent sorting limitations apply to intersecting translucent geometry. No colliders, navigation, environment profiles, camera sharing, scan permission, physics or programs are changed. An active headset passthrough subsystem is required for rendering; desktop/headless readback proves saved source only. Four windows per object, one per plane, 16 per room; repeated overlapping masks multiply remaining coverage. Virtual-only camera captures omit masks, while a shared composed headset view includes what is actually rendered. Saves one Undo; copied objects and construction blueprints retain this component. Changing/removing a referenced plane is validated; remove the window before deleting/curving its plane. Temporary edits remain in the fork.";
        internal static JObject SavedSchema()=>Object(new JObject{["version"]=Number(1,1,true),["id"]=Name(),["surface"]=Name(),["shape"]=Choice("rectangle","ellipse"),["reveal"]=Number(0,1)});
        static JObject Name()=>Text("^[a-zA-Z][a-zA-Z0-9_]{0,31}$",32);
        static JObject Variant(string operation){
            var fields=new JObject{["operation"]=Choice(operation),["target"]=DrawingData.Target(),["revision"]=Revision(),["window"]=Name()};
            if(operation=="save"){fields["surface"]=Name();fields["shape"]=Choice("rectangle","ellipse");fields["reveal"]=Number(0,1);}
            fields["operation"]["x-static"]=true;var schema=CurrentInputs(Object(fields),"object.windows","revision",new JObject{["target"]="target"});schema["title"]=operation=="save"?"Save an opening":"Remove an opening";schema["x-features"]=new JArray(Feature);return schema;
        }
        public override JObject InputSchema=>new(){["type"]="object",["x-discriminators"]=new JArray("operation"),["oneOf"]=new JArray(Variant("save"),Variant("remove"))};
        public override JObject OutputSchema=>Object(new JObject{["target"]=DrawingData.Target(),["revision"]=Revision(),["window"]=Name()});
        public override JObject Example=>new(){["operation"]="save",["target"]=new string('0',32),["revision"]=1,["window"]="Window",["surface"]="Canvas",["shape"]="rectangle",["reveal"]=1};
        public override bool CanRun(CapabilityContext c,JObject a,out string error)=>c.Target(a,out _,out error)&&c.Editor.PrepareWindowEdit((string)a["target"],(int)a["revision"],a,out _,out error);
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation operation,out string error){operation=null;if(!CanRun(c,a,out error)||!c.Editor.EditWindow((string)a["target"],(int)a["revision"],a,out error))return false;operation=new CompletedCapability(new JObject{["target"]=(string)a["target"],["revision"]=c.Editor.ObjectRevision((string)a["target"]),["window"]=(string)a["window"]});return true;}
        internal static BehaviourCatalog.FactDefinition Fact(){var type=ProgramDataType.Read(JObject.Parse("{\"record\":{\"target\":\"text\",\"revision\":\"number\",\"physicalAnchor\":\"boolean\",\"requested\":\"boolean\",\"renderingReady\":\"boolean\",\"reason\":\"text\",\"windows\":{\"list\":{\"record\":{\"id\":\"text\",\"surface\":\"text\",\"shape\":\"text\",\"reveal\":\"number\"}}}}}"));return new("object.windows",type,
            "Object passthrough openings","All saved windows (at most four) and the current object revision. requested means a visible opening is requested; renderingReady means the local passthrough subsystem is running, not proof of pixels, real alignment or headset acceptance. physicalAnchor identifies exact scanned attachment; observe object.scanDrawing for its availability. Read object.surface for the referenced plane's pose and dimensions. Layer opacity multiplies reveal independently of material opacity. Reads do not request camera sharing, scanning or permission.",Object(new JObject{["target"]=DrawingData.Target()}),new JObject{["target"]=new string('0',32)},(c,a)=>{var value=c.Editor?c.Editor.ObserveWindows((string)a["target"]):null;return value==null?null:ProgramValue.Literal(value,type);},features:new[]{Feature});}
    }
}
