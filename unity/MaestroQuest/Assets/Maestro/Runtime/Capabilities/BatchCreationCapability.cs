// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Maestro.Quest.Imports;
using Maestro.Quest.Rules;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class BatchCreationCapability:CapabilityModule
    {
        internal const string Feature="batchCreation.v1",ConnectedFeature="connectedBlueprints.v2";
        public override string Id=>"object.batch.create";
        public override string Label=>"Create a structure";
        public override string Duration=>"completion";
        public override IReadOnlyList<string> Requirements=>new[]{"room.capacity","storage.writable","blueprint.valid"};
        public override string Description=>"Instantiate an editable version-1 independent version-3 physically connected, or version-4 resource-bundled blueprint of 1–16 independently grabbable physical pieces. Each named slot contains an exact bundled template hash, inline recipe/collision/physics source, or version-1/2 prototype preserving created geometry, paint, surfaces/ink, drawing tips and recorded root motion and a local pose/scale. The outer pose/scale transforms the entire arrangement in room axes. All final positions must be within 25 metres and all final scales 0.1–4; recipe, vertex, collision and room-object budgets apply to the whole candidate. Reserves every creation before a program starts the call, then saves once with one Undo; failure creates no partial structure. Returns objectIds and slots in matching blueprint order. Version 3 includes 1–15 slot-to-slot connection links, each using the ordinary connection definition. One outgoing connection per piece, no cycles or external IDs; authored frames must already align at the transformed pose. Links become exact new object IDs only after all pieces are prepared, within the shared 16-connection room budget. Objects retain editable source and no longer depend on the blueprint. Save this definition in ordinary reusable programs/modules; this does not install a separate blueprint library or persistent group, create snap joints, replace missing pieces, avoid overlaps, or start physics/animations. Pieces must start idle. Prototype motion is relative to each piece pose and transforms with it; every resulting frame must satisfy room bounds. Model prototypes keep exact local model hashes: bytes are verified before any member is committed; missing/damaged files fail without partial creation. Model rendering still loads asynchronously through the usual importer. No external file or replacement model is fetched. Version 4 bundles exactly the referenced appearances, tone sound definitions and environment collision profiles, including dormant appearance bindings. Version-2 prototypes carry these local references. All definition IDs are module-local symbols, remapped to fresh IDs on every instantiation; sharing within the construction survives, but existing room definitions are never reused or overwritten. Version 4 permits zero to fifteen links. Room resource budgets are checked before the single atomic save; Undo removes the new definitions with the pieces. Emitters are saved idle. Collision profiles remain subject to the global real-room collision switch. Temporary creation stays in the fork; Stop leaves accepted pieces and duplicate receipts do not create again. Use explicit returned IDs for later actions/reset; never identify pieces by display names.";
        static JObject Triple(double bound)=>Object(new JObject { ["x"]=Number(-bound,bound),["y"]=Number(-bound,bound),["z"]=Number(-bound,bound)});
        public override JObject InputSchema {get{
            var templateFields=(JObject)new CreateTemplateCapability().InputSchema["properties"];
            var template=Object(new JObject { ["kind"]=Choice("template"),["templateHash"]=templateFields["templateHash"].DeepClone()});template["title"]="Starter template";template["properties"]["kind"]["x-static"]=true;
            var recipeFields=(JObject)new CreateRecipeCapability().InputSchema["properties"];
            var recipe=Object(new JObject { ["kind"]=Choice("recipe"),["recipe"]=recipeFields["recipe"].DeepClone(),["collision"]=recipeFields["collision"].DeepClone(),["physics"]=recipeFields["physics"].DeepClone()},"collision","physics");recipe["title"]="Editable recipe";recipe["properties"]["kind"]["x-static"]=true;
            var prototype=Object(new JObject {["kind"]=Choice("prototype"),["prototype"]=CreationPrototypeSchema.Schema()});prototype["title"]="Saved object definition";prototype["properties"]["kind"]["x-static"]=true;
            var source=new JObject { ["type"]="object",["title"]="Piece source",["oneOf"]=new JArray(template,recipe,prototype),["x-discriminators"]=new JArray("kind")};
            var piece=Object(new JObject { ["slot"]=Text("^[a-zA-Z][a-zA-Z0-9_]{0,23}$",24),["name"]=Text("^.{0,80}$",80),["position"]=Triple(10),["rotation"]=Vector(true),["scale"]=Number(.1,4),["source"]=source});
            var slot=Text("^[a-zA-Z][a-zA-Z0-9_]{0,23}$",24);
            var links=List(Object(new JObject{["owner"]=slot,["connected"]=slot.DeepClone(),["definition"]=ConnectionCapability.DefinitionSchema()}),0,15);links["x-features"]=new JArray(ConnectedFeature,ConnectionCapability.Feature);
            var blueprint=Object(new JObject { ["version"]=Number(1,4,true),["pieces"]=List(piece,1,16),["connections"]=links,["resources"]=CreationResourcesSchema.Schema()},"connections","resources");
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
        static CreationBatch Batch(JObject args)=>CreationBatch.Read(args);
        public override bool CanRun(CapabilityContext context,JObject args,out string error)=>context.Editor.CanCreateBatch(Batch(args),out error);
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error) {
            operation=null;var batch=Batch(args);if(!context.Editor.CanCreateBatch(batch,out error))return false;
            var hashes=batch.blueprint.pieces.Where(p=>p.source.kind=="prototype"&&p.source.prototype.kind=="model").Select(p=>p.source.prototype.modelHash).Distinct().ToArray();
            if(hashes.Length>0){operation=new ModelBatch(context.Editor,batch,VerifyModels(context.Editor.Models,hashes));return true;}
            if(!context.Editor.CreateBatch(batch,out var ids,out error))return false;
            operation=new CompletedCapability(Result(context.Editor,batch,ids));return true;
        }
        static async Task VerifyModels(ModelLibrary library,string[] hashes) {
            // Verify one bounded file at a time and retain no GLB byte arrays in the operation.
            foreach(var hash in hashes)await library.ReadAsync(hash).ConfigureAwait(false);
        }
        static JObject Result(RoomEditor editor,CreationBatch batch,string[] ids)=>new() {["objectIds"]=new JArray(ids),["slots"]=new JArray(batch.blueprint.pieces.Select(p=>p.slot)),["temporary"]=editor.TemporaryRoom};
        sealed class ModelBatch:CapabilityOperation
        {
            readonly RoomEditor editor;readonly CreationBatch batch;readonly Task load;readonly bool temporary;
            bool stopped,finished;string failure;JObject result;
            internal ModelBatch(RoomEditor editor,CreationBatch batch,Task load){this.editor=editor;this.batch=batch;this.load=load;temporary=editor.TemporaryRoom;_ = load.ContinueWith(t=>{_ = t.Exception;},System.Threading.CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);}
            public override float Seconds=>0;
            public override RuleActionState State(out string error) {
                error=null;
                if(stopped){error="Construction cancelled before commit";return RuleActionState.Failed;}
                if(!load.IsCompleted)return RuleActionState.Preparing;
                if(!finished){
                    finished=true;
                    if(load.IsFaulted||load.IsCanceled){_ = load.Exception;failure="A required local model is missing or damaged; import the exact original model before creating this construction";}
                    else if(!editor||editor.TemporaryRoom!=temporary)failure="The room changed while checking construction dependencies";
                    else if(editor.CreateBatch(batch,out var ids,out failure))result=BatchCreationCapability.Result(editor,batch,ids);
                }
                error=failure;return failure==null?RuleActionState.Ready:RuleActionState.Failed;
            }
            public override void Stop(bool preservePlacement){stopped=true;}
            public override JObject Result=>result??new JObject();
        }
    }
}
