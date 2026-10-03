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
using static Maestro.Quest.Programs.CapabilitySchema;

namespace Maestro.Quest.Programs
{
    /// <summary>Typed public arguments with a private adapter to existing native handlers.
    /// This contract is independent of RuleStep serialization and numeric enum positions.</summary>
    public static class CapabilityArguments
    {
        public static JObject Schema(RuleActionKind kind)=>BehaviourCatalog.Action(kind)?.InputSchema;
        // A bound resource placeholder is not an authorization. Computed IDs are
        // checked against declarations or native-created results at execution time.
        public static string[] LiteralResources(JObject arguments,JObject schema,JObject bindings,int version) {
            var literal=(JObject)arguments.DeepClone();
            if(version==3)foreach(var field in bindings.Properties())
                if(CapabilitySchema.BindingType(schema,field.Name,arguments)!=null)CapabilitySchema.Remove(literal,field.Name);
            return Resources(literal,schema);
        }
        public static bool Validate(JToken value,JObject schema,out string error,string path="arguments")
        {
            error=path+" does not match the capability contract";
            if(value==null||schema==null)return false;
            if(schema["oneOf"] is JArray variants) {
                var selected=CapabilitySchema.Resolve(schema,value);
                if(selected==null) {error=path+" has an unsupported variant";return false;}
                return Validate(value,selected,out error,path);
            }
            if(value.Type==JTokenType.Null)return (bool?)schema["nullable"]==true;
            switch((string)schema["type"]) {
                case "object":
                    if(value is not JObject obj)return false;
                    if((string)schema["format"]=="programModule") {if(!ProgramModuleLibrary.ValidRecord(obj))return false;break;}
                    var properties=(JObject)schema["properties"];
                    if(((JArray)schema["required"]).Any(key=>!obj.ContainsKey((string)key)) || obj.Properties().Any(p=>!properties.ContainsKey(p.Name)))return false;
                    foreach(var field in obj.Properties()) {
                        var fieldSchema=(JObject)properties[field.Name];
                        if(!Validate(field.Value,fieldSchema,out error,path+"."+field.Name))return false;
                        if(fieldSchema["x-requires"] is JObject requirements && requirements.Properties().Any(p=>!JToken.DeepEquals(obj[p.Name],p.Value))) {error=path+"."+field.Name+" has incompatible arguments";return false;}
                    }
                    if((string)schema["format"]=="roomRecipe") {
                        var recipe=JsonUtility.FromJson<RoomRecipe>(obj.ToString(Newtonsoft.Json.Formatting.None));
                        if(recipe==null||!recipe.Validate(out error)) {error??=path+" is an invalid construction recipe";return false;}
                    } else if((string)schema["format"]=="constructionSelection") {
                        var members=((JArray)obj["members"]).Values<string>().ToArray();if(members.Distinct().Count()!=members.Length){error=path+" needs distinct construction pieces";return false;}
                    } else if((string)schema["format"]=="structureSource") {
                        if(!StructureSaveCapability.ValidSource(obj,out error))return false;
                    } else if((string)schema["format"]=="hingeConfiguration") {
                        var hinge=UnityEngine.JsonUtility.FromJson<Maestro.Quest.Creation.RoomHinge>(obj["definition"].ToString());hinge.connected=(string)obj["connected"];if(!hinge.Validate((string)obj["target"],out error))return false;
                    } else if((string)schema["format"]=="creationBatch") {
                        if(!CreationBatch.Read(obj).Prepare(out _,out error))return false;
                    } else if((string)schema["format"]=="objectLayout") {
                        if(!JsonUtility.FromJson<RoomLayout>(obj.ToString()).Validate(out error))return false;
                    } else if((string)schema["format"]=="lathePart") {
                        if(!RecipeLathe.Valid(JsonUtility.FromJson<RecipePart>(obj.ToString(Newtonsoft.Json.Formatting.None)))){error=path+" needs a simple counter-clockwise lathe profile";return false;}
                    } else if((string)schema["format"]=="creationPrototype") {
                        if(!CreationPrototype.Read(obj).Validate(out error))return false;
                    } else if((string)schema["format"]=="collisionRecipe") {
                        var recipe=JsonUtility.FromJson<CollisionRecipe>(obj.ToString());if(recipe==null||!recipe.Validate(out error))return false;
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
                shape=CapabilitySchema.Resolve((JObject)shape,value);if(shape==null)return;
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
            if(step.action==RuleActionKind.MoveObject){result["x"]=step.editPosition.x;result["y"]=step.editPosition.y;result["z"]=step.editPosition.z;}
            if(step.action==RuleActionKind.ResizeObject)result["scale"]=step.editScale;
            if(step.action==RuleActionKind.PaintObject){result["red"]=step.editColor.r;result["green"]=step.editColor.g;result["blue"]=step.editColor.b;}
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
            if(arguments["collision"]!=null||arguments["physics"]!=null){error="Use capability blocks to preserve creation components";return false;}
            var result=new RuleStep {action=kind,targetId=(string)arguments["target"]??"maestro",seconds=(float?)arguments["seconds"]??0,
                loop=(bool?)arguments["loop"]??false,clipModelHash=(string)arguments["modelHash"],clipIndex=(int?)arguments["clipIndex"]??0,motionId=(string)arguments["motionId"]};
            if(kind==RuleActionKind.MoveObject)result.editPosition=new Vector3((float)arguments["x"],(float)arguments["y"],(float)arguments["z"]);
            if(kind==RuleActionKind.ResizeObject)result.editScale=(float)arguments["scale"];
            if(kind==RuleActionKind.PaintObject)result.editColor=new Color((float)arguments["red"],(float)arguments["green"],(float)arguments["blue"],1);
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
