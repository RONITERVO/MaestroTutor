// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Linq;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal static class CreationPrototypeSchema
    {
        internal const string Feature="creationPrototypes.v1";
        static JObject Point(double bound)=>Object(new JObject {["x"]=Number(-bound,bound),["y"]=Number(-bound,bound),["z"]=Number(-bound,bound)});
        static JObject Color()=>Object(new JObject {["r"]=Number(0,1),["g"]=Number(0,1),["b"]=Number(0,1),["a"]=Number(1,1)});
        static JObject Surface() {
            var p=(JObject)DrawingSurfaceCapability.DefinitionSchema()["properties"];
            p["version"]=Number(1,1,true);p["id"]=Text("^[a-zA-Z][a-zA-Z0-9_]{0,31}$",32);
            p["strokes"]=List(Object(new JObject {["id"]=Text("^[a-f0-9]{32}$",32),["color"]=Color(),["radius"]=Number(.001,.02),["points"]=List(Point(2),2,512)}),0,32);
            return Object(p);
        }
        internal static JObject Schema() {
            var fields=(JObject)new CreateRecipeCapability().InputSchema["properties"];
            var tip=(JObject)DrawingTipCapability.DefinitionSchema()["properties"];tip["version"]=Number(1,1,true);
            var common=new JObject {["version"]=Number(1,1,true),["color"]=Color(),["physics"]=fields["physics"].DeepClone(),
                ["collision"]=fields["collision"].DeepClone(),["surfaces"]=List(Surface(),0,4),["drawingTips"]=List(Object(tip),0,1),["snapPoints"]=List(SnapPointCapability.SavedSchema(),0,64),["containers"]=List(ContainerCapability.SavedSchema(),0,1),
                ["motion"]=Object(new JObject {["loop"]=new JObject {["type"]="boolean"},["frames"]=List(Object(new JObject {
                    ["time"]=Number(0,30),["position"]=Point(500.001),["rotation"]=Vector(true),["scale"]=Number(.024999,40.00001)}),1,301)})};
            var variants=new JArray();
            foreach(var kind in new[]{"primitive","drawing","recipe","model"}) {
                var p=new JObject {["kind"]=kind=="primitive"?Choice("block","ball","cylinder"):Choice(kind)};p["kind"]["x-static"]=true;
                if(kind=="recipe")p["recipe"]=RecipeSchema();
                if(kind=="model")p["modelHash"]=Text("^[a-f0-9]{64}$",64);
                if(kind=="drawing"){p["points"]=List(Point(10),2,2048);p["radius"]=Number(.001,.02);}
                var variant=Object(p);variant["title"]=kind;variants.Add(variant);
            }
            common["geometry"]=new JObject {["type"]="object",["oneOf"]=variants,["x-discriminators"]=new JArray("kind")};
            var schema=Object(common,"collision","motion","snapPoints","containers");schema["format"]="creationPrototype";schema["x-features"]=new JArray(Feature);return schema;
        }
        // JsonUtility emits all default fields, including fields belonging to other variants.
        // Public source includes only fields belonging to its explicit kind.
        internal static JObject Encode(Creation.CreationPrototype prototype) {
            var value=JObject.Parse(UnityEngine.JsonUtility.ToJson(prototype));
            var geometry=new JObject {["kind"]=prototype.kind};
            foreach(var key in new[]{"kind","recipe","modelHash","points","radius"}) {
                bool include=key=="kind"||key=="recipe"&&prototype.kind=="recipe"||key=="modelHash"&&prototype.kind=="model"||(key=="points"||key=="radius")&&prototype.kind=="drawing";
                if(include)geometry[key]=value[key].DeepClone();value.Remove(key);
            }
            value["geometry"]=geometry;
            if((prototype.containers?.Length??0)==0)value.Remove("containers");
            if((prototype.snapPoints?.Length??0)==0)value.Remove("snapPoints");
            if(prototype.collision==null)value.Remove("collision");if(prototype.motion==null)value.Remove("motion");
            return value;
        }
    }
}
