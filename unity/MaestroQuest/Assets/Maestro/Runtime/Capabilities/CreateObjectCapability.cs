// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    /// <summary>One creation verb. Kinds keep their own validation, capacity checks and native editor operations.</summary>
    internal sealed class CreateObjectCapability : CapabilityModule
    {
        internal sealed class Kind {
            public readonly string Name;public readonly CapabilityModule Provider;public readonly CapabilityStepAdapter Adapter;readonly string[] features;
            public Kind(string name,CapabilityModule provider,params string[] features) {Name=name;Provider=provider;this.features=features;Adapter=new("object.create",provider,new JObject {["kind"]=name});}
            public JObject Schema {
                get {
                    var schema=Provider.InputSchema;var properties=(JObject)schema["properties"];
                    properties["kind"]=Choice(Name);properties["kind"]["x-static"]=true;((JArray)schema["required"]).Add("kind");
                    schema["title"]=Provider.Label;schema["description"]=Provider.Description;
                    schema["examples"]=new JArray(Adapter.Public(Provider.Example));schema["x-channels"]=new JArray(Provider.Channels);
                    schema["x-requirements"]=new JArray(Provider.Requirements);
                    schema["x-features"]=new JArray(new[]{"actionResults.v1"}.Concat(features));return schema;
                }
            }
        }
        internal static readonly Kind[] Kinds={new("primitive",new CreatePrimitiveCapability()),new("recipe",new CreateRecipeCapability(),"recipeCreation.v1"),new("copy",new CopyObjectCapability(),"objectCopy.v1"),new("drawing",new CreateDrawingCapability(),"drawingEdits.v1"),new("template",new CreateTemplateCapability(),Creation.CreationTemplates.Feature)};
        internal override IEnumerable<CapabilityStepAdapter> StepAdapters=>Kinds.Where(k=>k.Name=="primitive"||k.Name=="recipe").Select(k=>k.Adapter);
        Kind Selected(JObject args)=>Kinds.Single(k=>k.Adapter.Matches(args));
        public override string Id=>"object.create";
        public override string Label=>"Create object";
        public override string Description=>"Choose a creation kind: a physical shape, an editable recipe with optional animation tracks, a copy of an existing creation, an editable pencil stroke, or a bundled starter template. Inspect the selected kind's example, fields and requirements. All kinds return the exact new objectId. Outside temporary play it is saved with one Undo; inside temporary play it stays unsaved until room.session keep completes. Stop leaves created objects in the room. Imported models still use the existing asset import workflow.";
        public override string Duration=>"instant";
        public override string Ownership=>"kindChannels";
        public override IReadOnlyList<string> Channels=>Kinds.SelectMany(k=>k.Provider.Channels).Distinct().ToArray();
        internal override IEnumerable<string> NativeEntities(JObject args){var kind=Selected(args);return kind.Provider.NativeEntities(kind.Adapter.Native(args));}
        public override BehaviourCatalog.Claim[] Claims(JObject args){var kind=Selected(args);return kind.Provider.Claims(kind.Adapter.Native(args));}
        public override IReadOnlyList<string> Requirements=>new[] {"room.capacity","storage.writable","kind.valid"};
        public override JObject InputSchema=>new() {["type"]="object",["title"]="Creation kind",["oneOf"]=new JArray(Kinds.Select(k=>k.Schema)),["x-discriminators"]=new JArray("kind")};
        public override JObject OutputSchema=>Kinds[0].Provider.OutputSchema;
        public override JObject Example=>Kinds[0].Adapter.Public(Kinds[0].Provider.Example);
        public override bool Validate(JObject args,out string error) {var kind=Selected(args);return kind.Provider.Validate(kind.Adapter.Native(args),out error);}
        public override bool CanRun(CapabilityContext context,JObject args,out string error) {var kind=Selected(args);return kind.Provider.CanRun(context,kind.Adapter.Native(args),out error);}
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error) {
            var kind=Selected(args);return kind.Provider.Start(context,runId,kind.Adapter.Native(args),out operation,out error);
        }
    }
}
