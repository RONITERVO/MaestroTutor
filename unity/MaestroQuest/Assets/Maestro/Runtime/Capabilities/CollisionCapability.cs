// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class CollisionCapability:CapabilityModule
    {
        public override string Id=>"object.collision.edit";
        public override string Label=>"Edit collision shapes";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"target.creation","target.unheld","object.revision.current","authoring.inactive","storage.writable"};
        public override string Description=>"Replace a creation's independent root-local collision recipe and select automatic collision mode. Works for recipe objects, drawings and imported models. Box, sphere, cylinder and ring shapes form one rigid body; their centres/dimensions are local metres and rotate with the whole object. Ring walls leave an open centre; combine with a cylinder floor for a cup. Collision shapes do not follow animated visual parts or change when visual geometry is edited: edit both explicitly. Empty shapes clears the recipe and restores the object's default automatic collider. Existing box/sphere overrides retain this recipe but temporarily bypass it. Read object.collision and every object.collision.shape before replacing anything unrequested. Up to 16 uniquely named shapes/64 convex pieces per object; the room reserves at most 512 pieces including stored inactive recipes. Saved/temporary edits, Undo, ownership and historical receipt replay use the normal shared path. Mass, physics mode, visuals and live pose are preserved. This neither starts physics nor makes a fluid container.";
        public static JObject RecipeSchema() {
            JObject Triple(double min,double max)=>Object(new JObject {["x"]=Number(min,max),["y"]=Number(min,max),["z"]=Number(min,max)});
            JObject Shape(string shape) {
                var fields=new JObject {["id"]=Text("^[a-zA-Z0-9_]{1,32}$",32),["shape"]=Choice(shape),["position"]=Triple(-2,2),["size"]=Triple(.005,2),["rotation"]=Vector(true)};
                fields["segments"]=shape=="ring"||shape=="cylinder"?Number(8,24,true):Number(0,0,true);
                fields["innerRadius"]=shape=="ring"?Number(.05,.45):Number(0,0);
                var variant=Object(fields,shape=="box"||shape=="sphere"?new[]{"segments","innerRadius"}:shape=="cylinder"?new[]{"innerRadius"}:System.Array.Empty<string>());variant["title"]=char.ToUpperInvariant(shape[0])+shape.Substring(1);return variant;
            }
            var variants=new JObject {["oneOf"]=new JArray(Shape("box"),Shape("sphere"),Shape("cylinder"),Shape("ring")),["x-discriminators"]=new JArray("shape")};
            var schema=Object(new JObject {["version"]=Number(1,1,true),["shapes"]=List(variants,0,16)});schema["format"]="collisionRecipe";schema["x-features"]=new JArray(CollisionRecipe.Feature);
            schema["description"]="Root-local proxies, independent of visuals. Sphere dimensions must match. Cylinder/ring axes are Y. Ring innerRadius is normalized to outer radius 0.5; wall thickness must be at least .005 local metres. Positions within 2m and total proxy extent within 3m. 64 pieces per recipe; a ring costs segments pieces, other shapes cost one.";return schema;
        }
        public override JObject InputSchema=>CurrentInputs(Object(new JObject {["target"]=RecipeEditCapability.Target(),["revision"]=Revision(),["collision"]=RecipeSchema()}),"object.collision","revision",new JObject {["target"]="target"});
        public override JObject OutputSchema=>Object(new JObject {["target"]=Text("^[a-fA-F0-9]{32}$",32),["revision"]=Revision(),["shapes"]=Number(0,16,true),["pieces"]=Number(1,64,true),["customActive"]=new JObject {["type"]="boolean"},["temporary"]=new JObject {["type"]="boolean"}});
        public override JObject Example=>new() {["target"]=new string('0',32),["revision"]=1,["collision"]=new JObject {["version"]=1,["shapes"]=new JArray(new JObject {["id"]="Body",["shape"]="box",["position"]=new JObject {["x"]=0,["y"]=0,["z"]=0},["size"]=new JObject {["x"]=.1,["y"]=.1,["z"]=.1},["rotation"]=new JObject {["x"]=0,["y"]=0,["z"]=0,["w"]=1}})}};
        static CollisionRecipe Recipe(JObject args)=>JsonUtility.FromJson<CollisionRecipe>(args["collision"].ToString());
        public override bool CanRun(CapabilityContext context,JObject args,out string error)=>context.Target(args,out _,out error)&&context.Editor.PrepareCollisionEdit((string)args["target"],(int)args["revision"],Recipe(args),out _,out error);
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error){operation=null;if(!CanRun(context,args,out error)||!context.Editor.EditCollision((string)args["target"],(int)args["revision"],Recipe(args),out error))return false;operation=new CompletedCapability(Summary(context.Editor,(string)args["target"]));return true;}
        static RoomObjectData Read(BehaviourCatalog.FactContext c,JObject a){var data=c.Editor?c.Editor.Read((string)a["target"]):null;return data!=null&&!data.IsBuiltIn?data:null;}
        static JObject Summary(RoomEditor editor,string target){var data=editor.Read(target);return new JObject {["target"]=target,["revision"]=editor.ObjectRevision(target),["shapes"]=data.collision?.shapes.Length??0,["pieces"]=CollisionRecipe.ReservedPieces(data),["customActive"]=data.collision?.shapes.Length>0&&data.collisionShape==ItemCollider.Automatic,["temporary"]=editor.TemporaryRoom};}
        internal static BehaviourCatalog.FactDefinition Overview()=>new("object.collision",ProgramDataType.Read(JObject.Parse("{\"record\":{\"target\":\"text\",\"revision\":\"number\",\"shapes\":\"number\",\"pieces\":\"number\",\"customActive\":\"boolean\",\"temporary\":\"boolean\"}}")),"Collision shape summary","Exact saved/temporary collision recipe counts and revision. pieces is the reserved compound-piece budget, even while an explicit box/sphere override bypasses the recipe. customActive means automatic mode selects the recipe; this does not imply running physics or loaded imported geometry. Missing/built-in targets are unavailable.",Object(new JObject {["target"]=RecipeEditCapability.Target()}),new JObject {["target"]=new string('0',32)},(context,args)=>Read(context,args)==null?null:ProgramValue.Literal(Summary(context.Editor,(string)args["target"])),features:new[]{CollisionRecipe.Feature});
        internal static BehaviourCatalog.FactDefinition ShapeFact() {
            JObject VectorType(bool rotation=false){var p=new JObject {["x"]="number",["y"]="number",["z"]="number"};if(rotation)p["w"]="number";return new JObject {["record"]=p};}
            var shape=new JObject {["record"]=new JObject {["id"]="text",["shape"]="text",["position"]=VectorType(),["size"]=VectorType(),["rotation"]=VectorType(true),["segments"]="number",["innerRadius"]="number"}};
            return new("object.collision.shape",ProgramDataType.Read(new JObject {["record"]=new JObject {["revision"]="number",["index"]="number",["total"]="number",["shape"]=shape}}),"Collision shape source","One exact root-local proxy at a fixed revision and index. All saved shapes remain readable; stale revisions, missing targets and indexes at/after count are unavailable. Read object.collision first.",Object(new JObject {["target"]=RecipeEditCapability.Target(),["revision"]=Revision(),["index"]=Number(0,15,true)}),new JObject {["target"]=new string('0',32),["revision"]=1,["index"]=0},(c,a)=>{var data=Read(c,a);int index=(int)a["index"];if(data?.collision==null||c.Editor.ObjectRevision(data.id)!=(int)a["revision"]||index>=data.collision.shapes.Length)return null;return ProgramValue.Literal(new JObject {["revision"]=(int)a["revision"],["index"]=index,["total"]=data.collision.shapes.Length,["shape"]=JObject.Parse(JsonUtility.ToJson(data.collision.shapes[index]))});},features:new[]{CollisionRecipe.Feature});
        }
    }
}
