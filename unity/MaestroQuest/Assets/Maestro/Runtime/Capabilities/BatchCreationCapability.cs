// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class BatchCreationCapability:CapabilityModule
    {
        internal const string Feature="batchCreation.v1",ConnectedFeature="connectedBlueprints.v1";
        public override string Id=>"object.batch.create";
        public override string Label=>"Create a structure";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[]{"room.capacity","storage.writable","blueprint.valid"};
        public override string Description=>"Instantiate an editable version-1 independent or version-2 hinge-connected blueprint of 1–16 independently grabbable physical pieces. Each named slot contains an exact bundled template hash or inline recipe/collision/physics source and a local pose/scale. The outer pose/scale transforms the entire arrangement in room axes. All final positions must be within 25 metres and all final scales 0.1–4; recipe, vertex, collision and room-object budgets apply to the whole candidate. Reserves every creation before a program starts the call, then saves once with one Undo; failure creates no partial structure. Returns objectIds and slots in matching blueprint order. Version 2 includes 1–15 slot-to-slot hinge links, each using the ordinary hinge definition. One outgoing connection per piece, no cycles or external IDs; authored frames must already align at the transformed pose. Links become exact new object IDs only after all pieces are prepared, within the shared 16-hinge room budget. Objects retain editable source and no longer depend on the blueprint. Save this definition in ordinary reusable programs/modules; this does not install a separate blueprint library or persistent group, create snap joints, replace missing pieces, avoid overlaps, or start physics/animations. Pieces must start idle. Temporary creation stays in the fork; Stop leaves accepted pieces and duplicate receipts do not create again. Use explicit returned IDs for later actions/reset; never identify pieces by display names.";
        static JObject Triple(double bound)=>Object(new JObject { ["x"]=Number(-bound,bound),["y"]=Number(-bound,bound),["z"]=Number(-bound,bound)});
        public override JObject InputSchema {get{
            var templateFields=(JObject)new CreateTemplateCapability().InputSchema["properties"];
            var template=Object(new JObject { ["kind"]=Choice("template"),["templateHash"]=templateFields["templateHash"].DeepClone()});template["title"]="Starter template";template["properties"]["kind"]["x-static"]=true;
            var recipeFields=(JObject)new CreateRecipeCapability().InputSchema["properties"];
            var recipe=Object(new JObject { ["kind"]=Choice("recipe"),["recipe"]=recipeFields["recipe"].DeepClone(),["collision"]=recipeFields["collision"].DeepClone(),["physics"]=recipeFields["physics"].DeepClone()},"collision","physics");recipe["title"]="Editable recipe";recipe["properties"]["kind"]["x-static"]=true;
            var source=new JObject { ["type"]="object",["title"]="Piece source",["oneOf"]=new JArray(template,recipe),["x-discriminators"]=new JArray("kind")};
            var piece=Object(new JObject { ["slot"]=Text("^[a-zA-Z][a-zA-Z0-9_]{0,23}$",24),["name"]=Text("^.{0,80}$",80),["position"]=Triple(10),["rotation"]=Vector(true),["scale"]=Number(.1,4),["source"]=source});
            var slot=Text("^[a-zA-Z][a-zA-Z0-9_]{0,23}$",24);
            var links=List(Object(new JObject{["owner"]=slot,["connected"]=slot.DeepClone(),["definition"]=HingeCapability.DefinitionSchema()}),0,15);links["x-features"]=new JArray(ConnectedFeature,HingeCapability.Feature);
            var blueprint=Object(new JObject { ["version"]=Number(1,2,true),["pieces"]=List(piece,1,16),["hinges"]=links},"hinges");
            var schema=Object(new JObject { ["blueprint"]=blueprint,["position"]=Triple(25),["rotation"]=Vector(true),["scale"]=Number(.1,4)});schema["format"]="creationBatch";schema["x-features"]=new JArray(Feature,"actionResults.v1");return schema;
        }}
        public override JObject OutputSchema=>Object(new JObject { ["objectIds"]=List(Resource(Text("^[a-f0-9]{32}$",32)),1,16),["slots"]=List(Text("^[a-zA-Z][a-zA-Z0-9_]{0,23}$",24),1,16),["temporary"]=new JObject { ["type"]="boolean"}});
        internal override int MaximumCreatedObjects(JObject args)=>((JArray)args["blueprint"]["pieces"]).Count;
        public override JObject Example {get{
            var hash=CreationTemplates.All.First(e=>e.Id=="brick").Hash;
            JObject Rotation()=>new() { ["x"]=0,["y"]=0,["z"]=0,["w"]=1};
            return new JObject { ["blueprint"]=new JObject { ["version"]=1,["pieces"]=new JArray(Enumerable.Range(0,6).Select(i=>new JObject {
                ["slot"]="brick_"+(i+1),["name"]="Castle brick "+(i+1),["position"]=new JObject { ["x"]=(i%2)*.25,["y"]=(i/2)*.08,["z"]=0},["rotation"]=Rotation(),["scale"]=1,["source"]=new JObject { ["kind"]="template",["templateHash"]=hash}}))},
                ["position"]=new JObject { ["x"]=.3,["y"]=1,["z"]=.7},["rotation"]=Rotation(),["scale"]=1};
        }}
        static CreationBatch Batch(JObject args)=>JsonUtility.FromJson<CreationBatch>(args.ToString());
        public override bool CanRun(CapabilityContext context,JObject args,out string error)=>context.Editor.CanCreateBatch(Batch(args),out error);
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error) {
            operation=null;if(!context.Editor.CreateBatch(Batch(args),out var ids,out error))return false;
            operation=new CompletedCapability(new JObject { ["objectIds"]=new JArray(ids),["slots"]=new JArray(((JArray)args["blueprint"]["pieces"]).Select(p=>(string)p["slot"])),["temporary"]=context.Editor.TemporaryRoom});return true;
        }
    }
}
