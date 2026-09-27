// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Maestro.Quest.Rules;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Maestro.Quest.Programs
{
    /// <summary>Typed public arguments with a private adapter to existing native handlers.
    /// This contract is independent of RuleStep serialization and numeric enum positions.</summary>
    public static class CapabilityArguments
    {
        static readonly string[] Gestures={"greeting","pointing","listening","speaking","idle","walk"};
        static JObject Number(double min,double max,bool integer=false)=>new() {["type"]=integer?"integer":"number",["minimum"]=min,["maximum"]=max};
        static JObject Text(string pattern,int max=128)=>new() {["type"]="string",["pattern"]=pattern,["maxLength"]=max};
        static JObject Choice(params string[] values)=>new() {["type"]="string",["enum"]=new JArray(values)};
        static JObject Object(JObject properties,params string[] optional)=>new() {
            ["type"]="object",["properties"]=properties,["required"]=new JArray(properties.Properties().Select(p=>p.Name).Except(optional)),["additionalProperties"]=false
        };
        static JObject List(JObject items,int minimum,int maximum)=>new() {["type"]="array",["items"]=items,["minItems"]=minimum,["maxItems"]=maximum};
        static JObject RecipeSchema() {
            JObject Triple(double min,double max)=>Object(new JObject {["x"]=Number(min,max),["y"]=Number(min,max),["z"]=Number(min,max)});
            var parent=Text("^[a-zA-Z0-9_]{0,32}$",32);parent["nullable"]=true;
            var part=Object(new JObject {["id"]=Text("^[a-zA-Z0-9_]{1,32}$",32),["parent"]=parent,["shape"]=Choice("box","sphere","cylinder"),
                ["position"]=Triple(-2,2),["size"]=Triple(.005,2),["rotation"]=Vector(true),
                ["color"]=Object(new JObject {["r"]=Number(0,1),["g"]=Number(0,1),["b"]=Number(0,1),["a"]=Number(1,1)})});
            var key=Object(new JObject {["time"]=Number(0,30),["rotation"]=Vector(true)});
            var track=Object(new JObject {["part"]=Text("^[a-zA-Z0-9_]{1,32}$",32),["keys"]=List(key,2,16)});
            var recipe=Object(new JObject {["version"]=Number(1,1,true),["parts"]=List(part,1,32),["tracks"]=List(track,0,17),
                ["duration"]=Number(.1,30),["playing"]=new JObject {["type"]="boolean"},["loop"]=new JObject {["type"]="boolean"}});
            recipe["format"]="roomRecipe";return recipe;
        }
        static JObject Vector(bool rotation=false)
        {
            var fields=new JObject {["x"]=Number(-1,1),["y"]=Number(-1,1),["z"]=Number(-1,1)};
            if(rotation)fields["w"]=Number(-1,1);var schema=Object(fields);schema["format"]=rotation?"unitQuaternion":"boundedOffset";return schema;
        }
        static JObject Resource(JObject schema) {schema["x-resource"]="object";return schema;}
        static JObject Prop()=>Object(new JObject {
            ["objectId"]=Resource(Text("^[a-fA-F0-9]{32}$",32)),["avatarHash"]=Text("^(|[a-f0-9]{64})$",64),
            ["hand"]=Choice("left","right"),["release"]=Choice("return","drop","throw"),
            ["offset"]=Vector(),["rotation"]=Vector(true),["releaseAt"]=Number(.05,1)
        });
        public static JObject Schema(RuleActionKind kind)
        {
            var p=new JObject();
            if(kind==RuleActionKind.CreateRecipe)return Object(new JObject { ["name"]=Text("^.{0,80}$",80),["x"]=Number(-25,25),["y"]=Number(-25,25),["z"]=Number(-25,25),["scale"]=Number(.1,4),["recipe"]=RecipeSchema() });
            if(kind==RuleActionKind.CreatePrimitive)return Object(new JObject {
                ["shape"]=Choice("ball","block","cylinder"),["name"]=Text("^.{0,80}$",80),
                ["x"]=Number(-25,25),["y"]=Number(-25,25),["z"]=Number(-25,25),["scale"]=Number(.1,4),
                ["red"]=Number(0,1),["green"]=Number(0,1),["blue"]=Number(0,1)
            });
            if(RuleDocument.IsInstant(kind)) {
                p["target"]=Resource(Text("^[a-fA-F0-9]{32}$",32));
                if(kind==RuleActionKind.PhysicsImpulse) {p["x"]=Number(-20,20);p["y"]=Number(-20,20);p["z"]=Number(-20,20);}
                return Object(p);
            }
            if(kind!=RuleActionKind.Wait)p["target"]=kind==RuleActionKind.Gesture||kind==RuleActionKind.UpperBodyGesture||RuleDocument.IsSpatial(kind)
                ? Choice("maestro") : Text("^(maestro|book|[a-fA-F0-9]{32})$",32);
            if(kind!=RuleActionKind.ThrowRecording)p["seconds"]=Number(kind==RuleActionKind.Wait||kind==RuleActionKind.Gesture||kind==RuleActionKind.UpperBodyGesture||RuleDocument.IsSpatial(kind) ? .1 : 0,30);
            if(kind==RuleActionKind.Gesture||kind==RuleActionKind.UpperBodyGesture)p["gesture"]=Choice(kind==RuleActionKind.UpperBodyGesture?Gestures.Where(x=>x!="walk").ToArray():Gestures);
            if(kind==RuleActionKind.RecordedAnimation||kind==RuleActionKind.ImportedClip||kind==RuleActionKind.LibraryMotion||kind==RuleActionKind.RecipeAnimation)
                p["loop"]=new JObject {["type"]="boolean"};
            if(kind==RuleActionKind.ImportedClip) {p["modelHash"]=Text("^(|[a-f0-9]{64})$",64);p["clipIndex"]=Number(0,31,true);}
            if(kind==RuleActionKind.LibraryMotion)p["motionId"]=Text("^(|[a-fA-F0-9]{32})$",32);
            if(kind==RuleActionKind.RecordedAnimation||kind==RuleActionKind.Gesture||kind==RuleActionKind.ImportedClip||kind==RuleActionKind.LibraryMotion)p["prop"]=Prop();
            if(p["target"] is JObject target)Resource(target);
            if(p["prop"] is JObject prop)prop["x-requires"]=new JObject {["target"]="maestro"};
            return Object(p,"prop");
        }
        public static JObject OutputSchema(RuleActionKind kind)=>Object(RuleDocument.IsCreation(kind)
            ?new JObject {["objectId"]=Resource(Text("^[a-f0-9]{32}$",32))}:new JObject());
        // A bound resource placeholder is not an authorization. Computed IDs are
        // checked against declarations or native-created results at execution time.
        public static string[] LiteralResources(JObject arguments,JObject schema,JObject bindings,int version) {
            var literal=(JObject)arguments.DeepClone();
            if(version==3)foreach(var field in bindings.Properties())
                if((string)schema["properties"]?[field.Name]?["x-resource"]=="object")literal.Remove(field.Name);
            return Resources(literal,schema);
        }
        public static bool Validate(JToken value,JObject schema,out string error,string path="arguments")
        {
            error=path+" does not match the capability contract";
            if(value==null)return false;
            if(value.Type==JTokenType.Null)return (bool?)schema["nullable"]==true;
            switch((string)schema["type"]) {
                case "object":
                    if(value is not JObject obj)return false;var properties=(JObject)schema["properties"];
                    if(((JArray)schema["required"]).Any(key=>!obj.ContainsKey((string)key)) || obj.Properties().Any(p=>!properties.ContainsKey(p.Name)))return false;
                    foreach(var field in obj.Properties()) {
                        var fieldSchema=(JObject)properties[field.Name];
                        if(!Validate(field.Value,fieldSchema,out error,path+"."+field.Name))return false;
                        if(fieldSchema["x-requires"] is JObject requirements && requirements.Properties().Any(p=>!JToken.DeepEquals(obj[p.Name],p.Value))) {error=path+"."+field.Name+" has incompatible arguments";return false;}
                    }
                    if((string)schema["format"]=="roomRecipe") {
                        var recipe=JsonUtility.FromJson<RoomRecipe>(obj.ToString(Newtonsoft.Json.Formatting.None));
                        if(recipe==null||!recipe.Validate(out error)) {error??=path+" is an invalid construction recipe";return false;}
                    } else if(schema["format"]!=null) {
                        double norm=obj.Properties().Sum(p=>(double)p.Value*(double)p.Value);
                        if((string)schema["format"]=="boundedOffset" && norm>1 || (string)schema["format"]=="unitQuaternion" && Math.Abs(norm-1)>=.01) {error=path+" has an invalid length";return false;}
                    }
                    break;
                case "array":
                    if(value is not JArray array||array.Count<(int)schema["minItems"]||array.Count>(int)schema["maxItems"])return false;
                    for(int i=0;i<array.Count;i++)if(!Validate(array[i],(JObject)schema["items"],out error,path+"["+i+"]"))return false;
                    break;
                case "string":
                    if(value.Type!=JTokenType.String)return false;string text=(string)value;
                    if(text.Any(char.IsControl) || schema["maxLength"]!=null && text.Length>(int)schema["maxLength"] ||
                        schema["pattern"]!=null && !Regex.IsMatch(text,(string)schema["pattern"]) ||
                        schema["enum"] is JArray choices && !choices.Any(x=>(string)x==text))return false;
                    break;
                case "boolean":if(value.Type!=JTokenType.Boolean)return false;break;
                case "number":case "integer":
                    if(value.Type!=JTokenType.Integer && value.Type!=JTokenType.Float)return false;double number=(double)value;
                    if(!double.IsFinite(number) || number<(double)schema["minimum"] || number>(double)schema["maximum"] || (string)schema["type"]=="integer" && Math.Truncate(number)!=number)return false;
                    break;
                default:return false;
            }
            error=null;return true;
        }
        public static string[] Resources(JObject arguments,JObject schema)
        {
            var values=new HashSet<string>();
            void Walk(JToken value,JToken shape) {
                if(value==null||shape==null)return;
                if((string)shape["x-resource"]=="object" && value.Type==JTokenType.String)values.Add((string)value);
                if(value is JArray array && shape["items"] is JObject itemSchema)foreach(var item in array)Walk(item,itemSchema);
                if(value is JObject obj && shape["properties"] is JObject fields)
                    foreach(var field in obj.Properties())Walk(field.Value,fields[field.Name]);
            }
            Walk(arguments,schema);return values.ToArray();
        }
        public static JObject FromStep(RuleStep step)
        {
            var result=new JObject();var fields=(JObject)Schema(step.action)["properties"];
            if(step.action==RuleActionKind.CreateRecipe) {
                result["name"]=step.objectName;result["x"]=step.creationPosition.x;result["y"]=step.creationPosition.y;result["z"]=step.creationPosition.z;result["scale"]=step.creationScale;
                result["recipe"]=step.creationRecipe==null?JValue.CreateNull():JObject.Parse(JsonUtility.ToJson(step.creationRecipe));
            }
            if(step.action==RuleActionKind.CreatePrimitive) {
                result["shape"]=step.shape;result["name"]=step.objectName;result["x"]=step.creationPosition.x;result["y"]=step.creationPosition.y;result["z"]=step.creationPosition.z;
                result["scale"]=step.creationScale;result["red"]=step.creationColor.r;result["green"]=step.creationColor.g;result["blue"]=step.creationColor.b;
            }
            if(fields.ContainsKey("target"))result["target"]=step.targetId;
            if(fields.ContainsKey("seconds"))result["seconds"]=step.seconds;
            if(step.action==RuleActionKind.PhysicsImpulse) {result["x"]=step.impulse.x;result["y"]=step.impulse.y;result["z"]=step.impulse.z;}
            if(fields.ContainsKey("gesture"))result["gesture"]=Enum.IsDefined(typeof(RuleGesture),step.gesture)?step.gesture.ToString().ToLowerInvariant():"invalid";
            if(fields.ContainsKey("loop"))result["loop"]=step.loop;
            if(fields.ContainsKey("modelHash")) {result["modelHash"]=step.clipModelHash??"";result["clipIndex"]=step.clipIndex;}
            if(fields.ContainsKey("motionId"))result["motionId"]=step.motionId??"";
            if(!string.IsNullOrEmpty(step.propId))result["prop"]=new JObject {
                ["objectId"]=step.propId,["avatarHash"]=step.propAvatarHash??"",["hand"]=step.propHand.ToString().ToLowerInvariant(),
                ["release"]=step.propRelease.ToString().ToLowerInvariant(),["releaseAt"]=step.propReleaseAt,
                ["offset"]=JObject.Parse(JsonUtility.ToJson(step.propOffset)),["rotation"]=JObject.Parse(JsonUtility.ToJson(step.propRotation))
            };
            return result;
        }
        public static bool TryStep(RuleActionKind kind,JObject arguments,out RuleStep step,out string error)
        {
            step=null;error=null;if(!BehaviourCatalog.HasAction(kind) || !Validate(arguments,Schema(kind),out error)) {error??="Unknown capability";return false;}
            var result=new RuleStep {action=kind,targetId=(string)arguments["target"]??"maestro",seconds=(float?)arguments["seconds"]??0,
                loop=(bool?)arguments["loop"]??false,clipModelHash=(string)arguments["modelHash"],clipIndex=(int?)arguments["clipIndex"]??0,motionId=(string)arguments["motionId"]};
            if(kind==RuleActionKind.CreateRecipe) {result.objectName=(string)arguments["name"];result.creationPosition=new Vector3((float)arguments["x"],(float)arguments["y"],(float)arguments["z"]);result.creationScale=(float)arguments["scale"];result.creationRecipe=JsonUtility.FromJson<RoomRecipe>(arguments["recipe"].ToString());}
            if(kind==RuleActionKind.CreatePrimitive) {
                result.shape=(string)arguments["shape"];result.objectName=(string)arguments["name"];result.creationPosition=new Vector3((float)arguments["x"],(float)arguments["y"],(float)arguments["z"]);
                result.creationScale=(float)arguments["scale"];result.creationColor=new Color((float)arguments["red"],(float)arguments["green"],(float)arguments["blue"],1);
            }
            if(kind==RuleActionKind.PhysicsImpulse)result.impulse=new Vector3((float)arguments["x"],(float)arguments["y"],(float)arguments["z"]);
            if(arguments["gesture"]!=null)result.gesture=Enum.Parse<RuleGesture>((string)arguments["gesture"],true);
            if(arguments["prop"] is JObject prop) {
                result.propId=(string)prop["objectId"];result.propAvatarHash=(string)prop["avatarHash"];
                result.propHand=Enum.Parse<PropHand>((string)prop["hand"],true);result.propRelease=Enum.Parse<PropRelease>((string)prop["release"],true);
                result.propReleaseAt=(float)prop["releaseAt"];result.propOffset=JsonUtility.FromJson<Vector3>(prop["offset"].ToString());result.propRotation=JsonUtility.FromJson<Quaternion>(prop["rotation"].ToString());
            }
            if(!RuleDocument.ValidStep(result,out error))return false;step=result;return true;
        }
    }
}
