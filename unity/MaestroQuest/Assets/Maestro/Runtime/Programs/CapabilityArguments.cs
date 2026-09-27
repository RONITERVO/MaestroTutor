// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Maestro.Quest.Rules;
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
            if(kind!=RuleActionKind.Wait)p["target"]=kind==RuleActionKind.Gesture||RuleDocument.IsSpatial(kind)
                ? Choice("maestro") : Text("^(maestro|book|[a-fA-F0-9]{32})$",32);
            if(kind!=RuleActionKind.ThrowRecording)p["seconds"]=Number(kind==RuleActionKind.Wait||kind==RuleActionKind.Gesture||RuleDocument.IsSpatial(kind) ? .1 : 0,30);
            if(kind==RuleActionKind.Gesture)p["gesture"]=Choice(Gestures);
            if(kind==RuleActionKind.RecordedAnimation||kind==RuleActionKind.ImportedClip||kind==RuleActionKind.LibraryMotion||kind==RuleActionKind.RecipeAnimation)
                p["loop"]=new JObject {["type"]="boolean"};
            if(kind==RuleActionKind.ImportedClip) {p["modelHash"]=Text("^(|[a-f0-9]{64})$",64);p["clipIndex"]=Number(0,31,true);}
            if(kind==RuleActionKind.LibraryMotion)p["motionId"]=Text("^(|[a-fA-F0-9]{32})$",32);
            if(kind==RuleActionKind.RecordedAnimation||kind==RuleActionKind.Gesture||kind==RuleActionKind.ImportedClip||kind==RuleActionKind.LibraryMotion)p["prop"]=Prop();
            if(p["target"] is JObject target)Resource(target);
            if(p["prop"] is JObject prop)prop["x-requires"]=new JObject {["target"]="maestro"};
            return Object(p,"prop");
        }
        public static bool Validate(JToken value,JObject schema,out string error,string path="arguments")
        {
            error=path+" does not match the capability contract";
            if(value==null)return false;
            switch((string)schema["type"]) {
                case "object":
                    if(value is not JObject obj)return false;var properties=(JObject)schema["properties"];
                    if(((JArray)schema["required"]).Any(key=>!obj.ContainsKey((string)key)) || obj.Properties().Any(p=>!properties.ContainsKey(p.Name)))return false;
                    foreach(var field in obj.Properties()) {
                        var fieldSchema=(JObject)properties[field.Name];
                        if(!Validate(field.Value,fieldSchema,out error,path+"."+field.Name))return false;
                        if(fieldSchema["x-requires"] is JObject requirements && requirements.Properties().Any(p=>!JToken.DeepEquals(obj[p.Name],p.Value))) {error=path+"."+field.Name+" has incompatible arguments";return false;}
                    }
                    if(schema["format"]!=null) {
                        double norm=obj.Properties().Sum(p=>(double)p.Value*(double)p.Value);
                        if((string)schema["format"]=="boundedOffset" && norm>1 || (string)schema["format"]=="unitQuaternion" && Math.Abs(norm-1)>=.01) {error=path+" has an invalid length";return false;}
                    }
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
                if(value is JObject obj && shape["properties"] is JObject fields)
                    foreach(var field in obj.Properties())Walk(field.Value,fields[field.Name]);
            }
            Walk(arguments,schema);return values.ToArray();
        }
        public static JObject FromStep(RuleStep step)
        {
            var result=new JObject();var fields=(JObject)Schema(step.action)["properties"];
            if(fields.ContainsKey("target"))result["target"]=step.targetId;
            if(fields.ContainsKey("seconds"))result["seconds"]=step.seconds;
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
