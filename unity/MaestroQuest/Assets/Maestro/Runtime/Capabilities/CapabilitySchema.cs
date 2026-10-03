// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Linq;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Programs
{
    /// <summary>Schema building blocks; capability modules own their contracts.</summary>
    public static class CapabilitySchema
    {
        // oneOf is evaluated strictly; discriminators only choose an editor view.
        public static JObject Resolve(JObject schema,JToken value) {
            if(schema?["oneOf"] is not JArray variants)return schema;
            var keys=((JArray)schema["x-discriminators"]).Values<string>().ToArray();
            var matches=variants.OfType<JObject>().Where(branch=>keys.All(path=> {
                var field=Field(branch,path);var actual=Value(value,path);
                return actual?.Type==JTokenType.String&&field?["enum"] is JArray choices&&choices.Any(x=>JToken.DeepEquals(x,actual));
            })).Take(2).ToArray();return matches.Length==1?matches[0]:null;
        }
        public static JToken Value(JToken value,string path) {foreach(var key in path.Split('.'))value=(value as JObject)?[key];return value;}
        public static JObject Field(JObject schema,string path,JToken value=null) {
            foreach(var key in path.Split('.')){schema=Resolve(schema,value)?["properties"]?[key] as JObject;value=(value as JObject)?[key];}return schema;
        }
        public static void Set(JObject value,string path,JToken next) {
            var keys=path.Split('.');for(int i=0;i<keys.Length-1;i++)value=value[keys[i]] as JObject??throw new ProgramFault("Unknown argument path");
            if(!value.ContainsKey(keys[^1]))throw new ProgramFault("Unknown argument path");value[keys[^1]]=next;
        }
        public static void Remove(JObject value,string path) {
            var keys=path.Split('.');for(int i=0;i<keys.Length-1;i++) {value=value[keys[i]] as JObject;if(value==null)return;}value.Remove(keys[^1]);
        }
        // Session-scoped native revision counters are signed 32-bit integers.
        // Do not confuse their range with geometry, value-size or instruction budgets.
        public static JObject Revision(bool allowZero=false)=>Number(allowZero?0:1,int.MaxValue,true);
        public static JObject Number(double min,double max,bool integer=false)=>new() {["type"]=integer?"integer":"number",["minimum"]=min,["maximum"]=max};
        public static JObject Text(string pattern,int max=128)=>new() {["type"]="string",["pattern"]=pattern,["maxLength"]=max};
        public static JObject Choice(params string[] values)=>new() {["type"]="string",["enum"]=new JArray(values)};
        public static JObject Object(JObject properties,params string[] optional)=>new() {
            ["type"]="object",["properties"]=properties,["required"]=new JArray(properties.Properties().Select(p=>p.Name).Except(optional)),["additionalProperties"]=false
        };
        public static JObject List(JObject items,int minimum,int maximum)=>new() {["type"]="array",["items"]=items,["minItems"]=minimum,["maxItems"]=maximum};
        public static JObject RecipeSchema() {
            JObject Triple(double min,double max)=>Object(new JObject {["x"]=Number(min,max),["y"]=Number(min,max),["z"]=Number(min,max)});
            var parent=Text("^[a-zA-Z0-9_]{0,32}$",32);parent["nullable"]=true;
            var part=Object(new JObject {["id"]=Text("^[a-zA-Z0-9_]{1,32}$",32),["parent"]=parent,["shape"]=Choice("box","sphere","cylinder"),
                ["position"]=Triple(-2,2),["size"]=Triple(.005,2),["rotation"]=Vector(true),
                ["color"]=Object(new JObject {["r"]=Number(0,1),["g"]=Number(0,1),["b"]=Number(0,1),["a"]=Number(1,1)})});
            var profilePoint=Object(new JObject {["x"]=Number(0,.5),["y"]=Number(-.5,.5)});
            var lathe=(JObject)part.DeepClone();lathe["properties"]["shape"]=Choice("lathe");
            lathe["properties"]["profile"]=List(profilePoint,3,16);lathe["properties"]["segments"]=Number(8,48,true);
            ((JArray)lathe["required"]).Add("profile");((JArray)lathe["required"]).Add("segments");lathe["format"]="lathePart";
            lathe["title"]="Lathe";lathe["description"]="Rotate a simple counter-clockwise closed profile around Y. x is radius 0–0.5; y is height -0.5–0.5, scaled by part dimensions. 3–16 distinct points, 8–48 angular segments. No self intersections or touching edges. Geometry only: the assembly still uses an approximate box collider.";
            lathe["x-features"]=new JArray("latheGeometry.v1");
            part["properties"]["profile"]=List((JObject)profilePoint.DeepClone(),0,0);part["properties"]["segments"]=Number(0,0,true);
            // Unity serializes empty arrays/default numbers for primitive parts. Old literal recipes may omit both.
            part=new JObject {["oneOf"]=new JArray(part,lathe),["x-discriminators"]=new JArray("shape")};
            var key=Object(new JObject {["time"]=Number(0,30),["rotation"]=Vector(true)});
            var track=Object(new JObject {["part"]=Text("^[a-zA-Z0-9_]{1,32}$",32),["keys"]=List(key,2,16)});
            var recipe=Object(new JObject {["version"]=Number(1,1,true),["parts"]=List(part,1,32),["tracks"]=List(track,0,17),
                ["duration"]=Number(.1,30),["playing"]=new JObject {["type"]="boolean"},["loop"]=new JObject {["type"]="boolean"}});
            recipe["format"]="roomRecipe";return recipe;
        }
        public static JObject Vector(bool rotation=false)
        {
            var fields=new JObject {["x"]=Number(-1,1),["y"]=Number(-1,1),["z"]=Number(-1,1)};
            if(rotation)fields["w"]=Number(-1,1);var schema=Object(fields);schema["format"]=rotation?"unitQuaternion":"boundedOffset";return schema;
        }
        /// <summary>Read-only fact snapshot used to fill an action draft; never an automatic refresh at execution.</summary>
        public static JObject CurrentInputs(JObject schema,string fact,string guard,JObject arguments=null,params string[] fields) {
            var mappings=new JObject {[guard]=new JArray(guard)};
            foreach(var field in fields)mappings[field]=new JArray(field);
            schema["x-current"]=new JObject {["fact"]=fact,["version"]=1,["arguments"]=arguments??new JObject(),["fields"]=mappings,["guards"]=new JArray(guard)};
            return schema;
        }
        public static ProgramDataType OutputType(JObject schema) {
            JToken Shape(JObject field,int depth) {
                if(field==null||depth>4||field["oneOf"]!=null||(bool?)field["nullable"]==true)throw new ProgramFault("Output has no fixed program type");
                switch((string)field["type"]) {
                    case "string":return new JValue("text");case "number":case "integer":return new JValue("number");case "boolean":return new JValue("boolean");
                    case "array":return new JObject { ["list"]=Shape(field["items"] as JObject,depth+1)};
                    case "object":
                        var fields=field["properties"] as JObject;var required=field["required"] as JArray;
                        if(fields==null||required==null||required.Count!=fields.Count)throw new ProgramFault("Optional record fields are not program values");
                        return new JObject { ["record"]=new JObject(fields.Properties().Select(p=>new JProperty(p.Name,Shape(p.Value as JObject,depth+1))))};
                    default:throw new ProgramFault("Output has no program type");
                }
            }
            try{return ProgramDataType.Read(Shape(schema,0));}catch(ProgramFault){return null;}
        }
        public static ProgramDataType InputType(JObject schema) {
            bool Mutable(JObject field)=>field!=null&&(bool?)field["x-static"]!=true&&field["oneOf"]==null&&(bool?)field["nullable"]!=true&&(string)field["format"]!="programModule"&&
                ((string)field["type"]=="array"?Mutable(field["items"] as JObject):(string)field["type"]!="object"||field["properties"] is JObject fields&&fields.Properties().All(p=>Mutable(p.Value as JObject)));
            return Mutable(schema)?OutputType(schema):null;
        }
        public static ProgramDataType BindingType(JObject schema,string path,JObject arguments) {
            JToken value=arguments;
            foreach(string key in path.Split('.')) {
                if((bool?)schema?["x-static"]==true)return null;
                if(schema?["x-discriminators"] is JArray selectors&&selectors.Any(x=>(string)x==key))return null;
                schema=Resolve(schema,value)?["properties"]?[key] as JObject;value=(value as JObject)?[key];
            }
            return InputType(schema);
        }
        public static bool SeparateBindings(JObject bindings) {
            var names=bindings.Properties().Select(p=>p.Name).ToArray();
            return !names.Any(path=>names.Any(parent=>path.StartsWith(parent+".",System.StringComparison.Ordinal)));
        }
        public static JObject Resource(JObject schema) {schema["x-resource"]="object";return schema;}
        public static JObject Prop()=>Object(new JObject {
            ["objectId"]=Resource(Text("^[a-fA-F0-9]{32}$",32)),["avatarHash"]=Text("^(|[a-f0-9]{64})$",64),
            ["hand"]=Choice("left","right"),["release"]=Choice("return","drop","throw"),
            ["offset"]=Vector(),["rotation"]=Vector(true),["releaseAt"]=Number(.05,1)
        });
    }
}
