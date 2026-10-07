// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal static class RecipePartAnimationFacts
    {
        static JObject Vector(bool rotation=false){var fields=new JObject{["x"]="number",["y"]="number",["z"]="number"};if(rotation)fields["w"]="number";return new JObject{["record"]=fields};}
        public static BehaviourCatalog.FactDefinition Pose()
        {
            var type=new JObject{["record"]=new JObject{["target"]="text",["part"]="text",["parent"]="text",["revision"]="number",["local"]=new JObject{["record"]=new JObject{["position"]=Vector(),["rotation"]=Vector(true)}},["room"]=new JObject{["record"]=new JObject{["position"]=Vector(),["rotation"]=Vector(true)}},["playing"]="boolean"}};
            return new("object.recipe.pose",ProgramDataType.Read(type),"Live recipe part pose","Read one named part's current local/room position and unit rotation, exact saved object revision, parent and whether that track is playing. Local coordinates are relative to its parent; room coordinates use the authored room frame, independent of virtual-world translation or yaw. Parent motion carries descendants. Reads neither start playback nor grant edit authority. Missing, inactive or non-recipe parts are unavailable; parts without tracks are still readable. This is a live observation, never a saved key or independent physical body.",Object(new JObject{["target"]=RecipeEditCapability.Target(),["part"]=RecipePartAnimationCapability.PartId()}),new JObject{["target"]=new string('0',32),["part"]="Head"},(context,args)=>{
                string target=(string)args["target"],part=(string)args["part"];var item=context.Editor?context.Editor.Find(target):null;var recipe=item?item.GetComponent<RecipeObject>():null;var node=recipe?recipe.Part(part):null;
                if(!recipe||!recipe.isActiveAndEnabled||!node)return null;
                var frame=context.Editor.Frame;if(!frame.Valid)return null;
                var definition=context.Editor.Read(target)?.recipe;var saved=definition==null?null:System.Array.Find(definition.parts,p=>p.id==part);if(saved==null)return null;
                return ProgramValue.Literal(new JObject{["target"]=target,["part"]=part,["parent"]=saved.parent??"",["revision"]=context.Editor.ObjectRevision(target),["local"]=new JObject{["position"]=JObject.Parse(JsonUtility.ToJson(node.localPosition)),["rotation"]=JObject.Parse(JsonUtility.ToJson(node.localRotation))},["room"]=new JObject{["position"]=JObject.Parse(JsonUtility.ToJson(frame.PointToRoom(node.position))),["rotation"]=JObject.Parse(JsonUtility.ToJson(frame.RotationToRoom(node.rotation)))},["playing"]=recipe.PartPlaying(part)});
            },version:2);
        }
    }
}
