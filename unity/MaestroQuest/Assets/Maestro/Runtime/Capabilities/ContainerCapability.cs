// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal sealed class ContainerCapability:NativeTargetCapability {
        internal const string Feature="containers.v1",RectangularFeature="rectangularContainers.v1",TerrainFeature="terrainReservoirs.v1",SpillFeature="terrainEdgeSpill.v1";
        public override string Id=>"object.container.edit";
        public override string Label=>"Configure or fill a liquid container";
        public override string Duration=>"completion";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.created","target.unheld","source.revision.current","storage.writable"};
        public override string Description=>"Configure one saved cylindrical or rectangular liquid store on any created object, or remove it. definition includes its contents: this authoring action can create, replace or empty liquid. Use object.container.transfer to conserve contents instead. The frame is the root-local bottom centre; +Y points toward the opening. An optional rectangle (width/depth) replaces the circular radius; removing it returns to the configured radius. Geometry describes the cavity and does not infer or modify mesh/colliders. Capacity is a logical game-rule value in millilitres, independent of visual scale; resizing a prop neither creates liquid nor changes capacity. One liquid identity and opaque display colour at a time, up to 16 containers per room. The shared saved amount survives Undo, duplication, prototypes, temporary rooms and archives. Room physics also runs bounded open-vessel pouring and immersion filling; inspect object.container.live, object.container.scooping, object.container.poured and object.container.scooped. Optional fluid properties explicitly set density (kg/m3) and linear/angular drag (per second); omitted properties use density 1000 and drag 2/1, independent of liquid name. Properties follow pouring/transfers and unlike properties cannot mix. Native medium physics applies bounded buoyancy/drag to supported free rigid props from accepted collider displacement and mass. Inspect world.medium and object.medium. Level displacement, carried liquid mass, splashes and drinking remain unsupported. Imported bowls require explicit cavity configuration. With terrainFloor=true, the same fixed object must have one accepted height surface. Its frame, rectangle and capacity are derived from that surface; those input fields are ignored for this mode. height is the opening and must reach every terrain vertex. Capacity is the local geometric volume below the opening; amountMl remains authoritative. Terrain edits reshape the water while preserving amount and refuse overflow: transfer or explicitly remove enough water first. Optional spillAtTerrainEdge=true opens the terrain perimeter: excess above its lowest gravity-height edge drains during active physics through the existing bounded collision-checked stream. The first lowest edge is a deterministic outlet; a lower configured cavity can receive matching liquid, otherwise it is reported as uncollected spill. No quantity changes merely by configuring, reading, loading or sculpting. False closes the authored perimeter; omission preserves an existing terrain policy and defaults to closed for a new cavity. Inspect object.container.configuration for complete editable settings. This is one equalized basin with bounded overflow, not river currents, connected same-level lakes or puddle simulation. Sculpting does not create water; no water volume exists inside the bed. Missing bed geometry refuses interaction. Set terrainFloor=false to deliberately return to an ordinary cavity. Use object.field and object.field.samples to inspect the bed."+PersistedEditOperation.Persistence;
        internal static JObject FluidSchema(){var schema=Object(new JObject{["version"]=Number(1,1,true),["densityKgM3"]=Number(1,20000),["linearDrag"]=Number(0,20),["angularDrag"]=Number(0,20)});schema["examples"]=new JArray(JObject.FromObject(RoomFluid.Default));return schema;}
        internal static JObject DefinitionSchema(){
            var rectangle=Object(new JObject{["width"]=Number(0,4),["depth"]=Number(0,4)});rectangle["nullable"]=true;rectangle["title"]="Rectangular cavity";rectangle["examples"]=new JArray(new JObject{["width"]=.2,["depth"]=.2});rectangle["x-features"]=new JArray(RectangularFeature);
            var terrain=new JObject{["type"]="boolean",["title"]="Use this object’s terrain as the floor",["x-features"]=new JArray(TerrainFeature)};
            var spill=new JObject{["type"]="boolean",["title"]="Spill over low terrain edges",["x-features"]=new JArray(SpillFeature)};
            var schema=Object(new JObject{["terrainFloor"]=terrain,["spillAtTerrainEdge"]=spill,["frame"]=Object(new JObject{["position"]=DrawingData.Point(),["rotation"]=Vector(true)}),["radius"]=Number(.005,1),["rectangle"]=rectangle,["height"]=Number(.01,2),["capacityMl"]=Number(1,RoomContainer.MaximumMillilitres),["amountMl"]=Number(0,RoomContainer.MaximumMillilitres),["liquid"]=Text("^[a-zA-Z][a-zA-Z0-9_]{0,31}$",32),["fluid"]=FluidSchema(),["color"]=Object(new JObject{["r"]=Number(0,1),["g"]=Number(0,1),["b"]=Number(0,1),["a"]=Number(1,1)})},"rectangle","fluid","terrainFloor","spillAtTerrainEdge");schema["format"]="containerDefinition";return schema;
        }
        internal static JObject SavedSchema(){var result=DefinitionSchema();((JObject)result["properties"])["version"]=Number(1,4,true);((JArray)result["required"]).Add("version");result["x-features"]=new JArray(Feature);return result;}
        internal static RoomContainer ReadDefinition(JObject source){var data=JsonUtility.FromJson<RoomContainer>(source.ToString());RoomLiquidCodec.Read(data,source);if(source["fluid"] is not JObject)data.fluid=new();if(source["rectangle"] is not JObject)data.rectangle=null;if(!source.ContainsKey("version"))data.version=(bool?)source["spillAtTerrainEdge"]==true?4:(bool?)source["terrainFloor"]==true?3:source["rectangle"] is JObject r&&((double)r["width"]!=0||(double)r["depth"]!=0)?2:1;return data;}
        internal static bool ValidDefinition(JObject source,out string error){
            var data=ReadDefinition(source);
            if((bool?)source["spillAtTerrainEdge"]==true&&source["version"]==null&&(bool?)source["terrainFloor"]!=true){error="Terrain edge spilling requires terrainFloor=true";return false;}
            if(source.ContainsKey("version")&&source.ContainsKey("spillAtTerrainEdge")&&(bool)source["spillAtTerrainEdge"]!=data.SpillAtTerrainEdge){error="The spill flag must match the saved cavity version";return false;}
            // Authoring a terrain reservoir derives these fields from the owner's
            // canonical terrain. Saved definitions must instead match that metadata.
            if(!source.ContainsKey("version")&&data.TerrainFloor){data.version=1;data.rectangle=null;data.capacityMl=RoomContainer.MaximumMillilitres;}
            else if(source.ContainsKey("version")&&source.ContainsKey("terrainFloor")&&(bool)source["terrainFloor"]!=data.TerrainFloor){error="The terrain flag must match the saved cavity version";return false;}
            return data.Validate(out error);
        }
        static JObject Variant(string operation){var p=new JObject{["operation"]=Choice(operation),["target"]=DrawingData.Target(),["revision"]=Revision()};p["operation"]["x-static"]=true;if(operation=="configure")p["definition"]=DefinitionSchema();var s=CurrentInputs(Object(p),"object.container.configuration","revision",new JObject{["target"]="target"},operation=="configure"?new[]{"definition"}:System.Array.Empty<string>());s["title"]=operation=="configure"?"Configure / fill":"Remove container";s["x-features"]=new JArray(Feature);return s;}
        public override JObject InputSchema=>new(){["type"]="object",["x-discriminators"]=new JArray("operation"),["oneOf"]=new JArray(Variant("configure"),Variant("remove"))};
        public override JObject Example=>new(){["operation"]="configure",["target"]=new string('0',32),["revision"]=1,["definition"]=Definition(new RoomContainer{amountMl=200},includeSpill:true)};
        internal static JObject Definition(RoomContainer data,bool complete=false,bool includeSpill=false){var j=JObject.Parse(JsonUtility.ToJson(data));RoomLiquidCodec.Write(data,j);j.Remove("version");j["terrainFloor"]=data.TerrainFloor;if(includeSpill)j["spillAtTerrainEdge"]=data.SpillAtTerrainEdge;j["fluid"]=JObject.FromObject(RoomFluid.Effective(data.fluid));if(!data.IsRectangular){if(complete)j["rectangle"]=new JObject{["width"]=0f,["depth"]=0f};else j.Remove("rectangle");}return j;}
        static RoomContainer Read(CapabilityContext c,JObject a){
            if((string)a["operation"]=="remove")return null;
            var wire=(JObject)a["definition"];var result=ReadDefinition(wire);
            // Old programs replace contents using the original typed fact. An
            // omitted new policy preserves an existing terrain outlet; explicitly
            // false closes it. Changing to an ordinary cavity still removes it.
            if(result.TerrainFloor&&!wire.ContainsKey("spillAtTerrainEdge")&&c.Editor.Read((string)a["target"])?.containers?.FirstOrDefault()?.SpillAtTerrainEdge==true)result.version=4;
            return result;
        }
        public override bool Validate(JObject a,out string error){error=null;return (string)a["operation"]=="remove"||ValidDefinition((JObject)a["definition"],out error);}
        public override bool CanRun(CapabilityContext c,JObject a,out string error)=>c.Editor.PrepareContainer((string)a["target"],(int)a["revision"],Read(c,a),out _,out error);
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation operation,out string error){
            string target=(string)a["target"];int revision=(int)a["revision"];var definition=Read(c,a);
            return PersistedEditOperation.Start(c.Editor,target,
                (out RoomEditor.PendingPersistedEdit edit,out string issue)=>c.Editor.BeginContainer(target,revision,definition,out edit,out issue),
                out operation,out error,validate:(out string issue)=>c.Editor.CanEditContainer(target,revision,out issue));
        }
        static JObject FactSchema(bool includeSpill){
            var schema=DefinitionSchema();schema["properties"]["rectangle"]["nullable"]=false;
            foreach(string key in new[]{"rectangle","fluid","terrainFloor"})((JArray)schema["required"]).Add(key);
            if(includeSpill)((JArray)schema["required"]).Add("spillAtTerrainEdge");
            else ((JObject)schema["properties"]).Remove("spillAtTerrainEdge");
            return schema;
        }
        internal static BehaviourCatalog.FactDefinition Fact(bool includeSpill=false)=>new(
            includeSpill?"object.container.configuration":"object.container",
            OutputType(Object(new JObject{["revision"]=Revision(),["configured"]=new JObject{["type"]="boolean"},["definition"]=FactSchema(includeSpill)})),
            includeSpill?"Liquid container including overflow policy":"Liquid container and contents",
            "Saved cavity, capacity, exact contents and current object revision. configured=false returns editable defaults without creating anything. terrainFloor binds the same canonical terrain; frame, rectangle and capacity are derived. Quantities are independent of display scale. "+
            (includeSpill?"spillAtTerrainEdge explicitly selects gravity-driven overflow at the lowest terrain perimeter. Reads do not simulate or publish flow.":
            "This original typed record is unchanged; inspect object.container.configuration for terrain-edge overflow policy. An edit omitting that policy preserves it on an existing terrain container."),
            Object(new JObject{["target"]=DrawingData.Target()}),new JObject{["target"]=new string('0',32)},(c,a)=>{
                var d=c.Editor?c.Editor.Read((string)a["target"]):null;if(d==null||d.IsBuiltIn)return null;
                var container=d.containers?.FirstOrDefault();
                return ProgramValue.Literal(new JObject{["revision"]=c.Editor.ObjectRevision(d.id),["configured"]=container!=null,["definition"]=Definition(container??new RoomContainer(),true,includeSpill)});
            },features:includeSpill?new[]{Feature,SpillFeature}:new[]{Feature});
    }
    internal sealed class ContainerTransferCapability:NativeResourceInputsCapability {
        public override string Id=>"object.container.transfer";
        public override string Label=>"Transfer a measured amount of liquid";
        public override string Duration=>"completion";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"targets.created","targets.unheld","source.revision.current","storage.writable"};
        public override string Description=>"Move up to amountMl between two distinct configured containers in one atomic saved edit and one Undo. The result reports actual transferredMl, capped by source contents and destination space. Empty/full or incompatible liquid identity/colour fails without changing either object; an empty destination adopts source identity/colour. Both current object.container revisions are required and both objects are exclusively owned during execution. This is a logical transfer, with no implicit movement, proximity check, pouring animation or fluid simulation. Use it as a building block for user-authored games; native physical pouring uses the same quantity arithmetic and atomic publication. It preserves current physical poses, velocities and animated parts. Completion reports the actual measured transfer only after native application and saving. It never repeats after reload on its own."+PersistedEditOperation.Persistence;
        static JObject Member()=>CurrentInputs(Object(new JObject{["target"]=DrawingData.Target(),["revision"]=Revision()}),"object.container","revision",new JObject{["target"]="target"});
        public override JObject InputSchema {get{var s=Object(new JObject{["source"]=Member(),["destination"]=Member(),["amountMl"]=Number(.001,RoomContainer.MaximumMillilitres)});s["x-features"]=new JArray(ContainerCapability.Feature);s["format"]="containerTransfer";return s;}}
        public override JObject OutputSchema=>Object(new JObject{["transferredMl"]=Number(0,RoomContainer.MaximumMillilitres)});
        public override JObject Example=>new(){["source"]=new JObject{["target"]=new string('0',32),["revision"]=1},["destination"]=new JObject{["target"]=new string('1',32),["revision"]=1},["amountMl"]=100};
        public override BehaviourCatalog.Claim[] Claims(JObject a)=>new[]{new BehaviourCatalog.Claim((string)a["source"]["target"],"wholeTarget"),new BehaviourCatalog.Claim((string)a["destination"]["target"],"wholeTarget")};
        public override bool Validate(JObject a,out string error){error="Choose different source and destination objects";if((string)a["source"]["target"]==(string)a["destination"]["target"])return false;error=null;return true;}
        public override bool CanRun(CapabilityContext c,JObject a,out string error)=>c.Editor.PrepareContainerTransfer((string)a["source"]["target"],(int)a["source"]["revision"],(string)a["destination"]["target"],(int)a["destination"]["revision"],(double)a["amountMl"],out _,out _,out error);
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation operation,out string error){
            string source=(string)a["source"]["target"],destination=(string)a["destination"]["target"];
            int sourceRevision=(int)a["source"]["revision"],destinationRevision=(int)a["destination"]["revision"];
            double requested=(double)a["amountMl"],moved=0;
            return PersistedEditOperation.Start(c.Editor,source,
                (out RoomEditor.PendingPersistedEdit edit,out string issue)=>c.Editor.BeginContainerTransfer(source,sourceRevision,destination,destinationRevision,requested,out moved,out edit,out issue),
                out operation,out error,validate:(out string issue)=>c.Editor.CanTransferContainer(source,sourceRevision,destination,destinationRevision,out issue),
                output:()=>new JObject{["transferredMl"]=moved});
        }
    }
}
