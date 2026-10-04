// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal sealed class ContainerCapability:CapabilityModule {
        internal const string Feature="containers.v1",RectangularFeature="rectangularContainers.v1";
        public override string Id=>"object.container.edit";
        public override string Label=>"Configure or fill a liquid container";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.created","target.unheld","source.revision.current","storage.writable"};
        public override string Description=>"Configure one saved cylindrical or rectangular liquid store on any created object, or remove it. definition includes its contents: this authoring action can create, replace or empty liquid. Use object.container.transfer to conserve contents instead. The frame is the root-local bottom centre; +Y points toward the opening. An optional rectangle (width/depth) replaces the circular radius; removing it returns to the configured radius. Geometry describes the cavity and does not infer or modify mesh/colliders. Capacity is a logical game-rule value in millilitres, independent of visual scale; resizing a prop neither creates liquid nor changes capacity. One liquid identity and opaque display colour at a time, up to 16 containers per room. The shared saved amount survives Undo, duplication, prototypes, temporary rooms and archives. Room physics also runs bounded open-vessel pouring and immersion filling; inspect object.container.live, object.container.scooping, object.container.poured and object.container.scooped. Splashes, drinking, fluid forces and liquid mass remain unsupported. Imported bowls require explicit cavity configuration.";
        internal static JObject DefinitionSchema(){
            var rectangle=Object(new JObject{["width"]=Number(0,2),["depth"]=Number(0,2)});rectangle["nullable"]=true;rectangle["title"]="Rectangular cavity";rectangle["examples"]=new JArray(new JObject{["width"]=.2,["depth"]=.2});rectangle["x-features"]=new JArray(RectangularFeature);
            var schema=Object(new JObject{["frame"]=Object(new JObject{["position"]=DrawingData.Point(),["rotation"]=Vector(true)}),["radius"]=Number(.005,1),["rectangle"]=rectangle,["height"]=Number(.01,2),["capacityMl"]=Number(1,RoomContainer.MaximumMillilitres),["amountMl"]=Number(0,RoomContainer.MaximumMillilitres),["liquid"]=Text("^[a-zA-Z][a-zA-Z0-9_]{0,31}$",32),["color"]=Object(new JObject{["r"]=Number(0,1),["g"]=Number(0,1),["b"]=Number(0,1),["a"]=Number(1,1)})},"rectangle");schema["format"]="containerDefinition";return schema;
        }
        internal static JObject SavedSchema(){var result=DefinitionSchema();((JObject)result["properties"])["version"]=Number(1,2,true);((JArray)result["required"]).Add("version");result["x-features"]=new JArray(Feature);return result;}
        internal static RoomContainer ReadDefinition(JObject source){var data=JsonUtility.FromJson<RoomContainer>(source.ToString());if(source["rectangle"] is not JObject)data.rectangle=null;if(!source.ContainsKey("version"))data.version=source["rectangle"] is JObject r&&((double)r["width"]!=0||(double)r["depth"]!=0)?2:1;return data;}
        static JObject Variant(string operation){var p=new JObject{["operation"]=Choice(operation),["target"]=DrawingData.Target(),["revision"]=Revision()};p["operation"]["x-static"]=true;if(operation=="configure")p["definition"]=DefinitionSchema();var s=CurrentInputs(Object(p),"object.container","revision",new JObject{["target"]="target"},operation=="configure"?new[]{"definition"}:System.Array.Empty<string>());s["title"]=operation=="configure"?"Configure / fill":"Remove container";s["x-features"]=new JArray(Feature);return s;}
        public override JObject InputSchema=>new(){["type"]="object",["x-discriminators"]=new JArray("operation"),["oneOf"]=new JArray(Variant("configure"),Variant("remove"))};
        public override JObject Example=>new(){["operation"]="configure",["target"]=new string('0',32),["revision"]=1,["definition"]=Definition(new RoomContainer{amountMl=200})};
        internal static JObject Definition(RoomContainer data,bool complete=false){var j=JObject.Parse(JsonUtility.ToJson(data));j.Remove("version");if(!data.IsRectangular){if(complete)j["rectangle"]=new JObject{["width"]=0f,["depth"]=0f};else j.Remove("rectangle");}return j;}
        static RoomContainer Read(JObject a)=>(string)a["operation"]=="remove"?null:ReadDefinition((JObject)a["definition"]);
        public override bool Validate(JObject a,out string error){var d=Read(a);error=null;return d==null||d.Validate(out error);}
        public override bool CanRun(CapabilityContext c,JObject a,out string error)=>c.Editor.PrepareContainer((string)a["target"],(int)a["revision"],Read(a),out _,out error);
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation operation,out string error){operation=null;if(!c.Editor.EditContainer((string)a["target"],(int)a["revision"],Read(a),out error))return false;operation=new CompletedCapability();return true;}
        static JObject FactSchema(){var schema=DefinitionSchema();schema["properties"]["rectangle"]["nullable"]=false;((JArray)schema["required"]).Add("rectangle");return schema;}
        internal static BehaviourCatalog.FactDefinition Fact()=>new("object.container",OutputType(Object(new JObject{["revision"]=Revision(),["configured"]=new JObject{["type"]="boolean"},["definition"]=FactSchema()})),"Liquid container and contents","Saved cavity, capacity, exact contents and current object revision. configured=false returns editable defaults, without creating a component. Both rectangle dimensions are zero for a cylinder; positive dimensions replace radius for a rectangular cavity. Amount/capacity use millilitres, independent of prop scale. Reads do not refill, transfer or simulate liquid.",Object(new JObject{["target"]=DrawingData.Target()}),new JObject{["target"]=new string('0',32)},(c,a)=>{var d=c.Editor?c.Editor.Read((string)a["target"]):null;if(d==null||d.IsBuiltIn)return null;var container=d.containers?.FirstOrDefault();return ProgramValue.Literal(new JObject{["revision"]=c.Editor.ObjectRevision(d.id),["configured"]=container!=null,["definition"]=Definition(container??new RoomContainer(),true)});},features:new[]{Feature});
    }
    internal sealed class ContainerTransferCapability:CapabilityModule {
        public override string Id=>"object.container.transfer";
        public override string Label=>"Transfer a measured amount of liquid";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"targets.created","targets.unheld","source.revision.current","storage.writable"};
        public override string Description=>"Move up to amountMl between two distinct configured containers in one atomic saved edit and one Undo. The result reports actual transferredMl, capped by source contents and destination space. Empty/full or incompatible liquid identity/colour fails without changing either object; an empty destination adopts source identity/colour. Both current object.container revisions are required and both objects are exclusively owned during execution. This is a logical transfer, with no implicit movement, proximity check, pouring animation or fluid simulation. Use it as a building block for user-authored games; native physical pouring uses the same quantity arithmetic and atomic publication. Stop cannot retract an already completed saved transfer; Undo can. It never repeats after reload on its own.";
        static JObject Member()=>CurrentInputs(Object(new JObject{["target"]=DrawingData.Target(),["revision"]=Revision()}),"object.container","revision",new JObject{["target"]="target"});
        public override JObject InputSchema {get{var s=Object(new JObject{["source"]=Member(),["destination"]=Member(),["amountMl"]=Number(.001,RoomContainer.MaximumMillilitres)});s["x-features"]=new JArray(ContainerCapability.Feature);s["format"]="containerTransfer";return s;}}
        public override JObject OutputSchema=>Object(new JObject{["transferredMl"]=Number(0,RoomContainer.MaximumMillilitres)});
        public override JObject Example=>new(){["source"]=new JObject{["target"]=new string('0',32),["revision"]=1},["destination"]=new JObject{["target"]=new string('1',32),["revision"]=1},["amountMl"]=100};
        public override BehaviourCatalog.Claim[] Claims(JObject a)=>new[]{new BehaviourCatalog.Claim((string)a["source"]["target"],"wholeTarget"),new BehaviourCatalog.Claim((string)a["destination"]["target"],"wholeTarget")};
        public override bool Validate(JObject a,out string error){error="Choose different source and destination objects";if((string)a["source"]["target"]==(string)a["destination"]["target"])return false;error=null;return true;}
        public override bool CanRun(CapabilityContext c,JObject a,out string error)=>c.Editor.PrepareContainerTransfer((string)a["source"]["target"],(int)a["source"]["revision"],(string)a["destination"]["target"],(int)a["destination"]["revision"],(double)a["amountMl"],out _,out _,out error);
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation operation,out string error){operation=null;if(!c.Editor.TransferContainer((string)a["source"]["target"],(int)a["source"]["revision"],(string)a["destination"]["target"],(int)a["destination"]["revision"],(double)a["amountMl"],out var moved,out error))return false;operation=new CompletedCapability(new JObject{["transferredMl"]=moved});return true;}
    }
}
