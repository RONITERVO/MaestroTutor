// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Persistence;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class WorkspacePackPrepareCapability:CapabilityModule
    {
        internal const string Feature="workspacePacks.v1";
        public override string Id=>"workspace.pack.prepare";
        public override string Label=>"Prepare included world";
        public override string Domain=>"workspace";
        public override string Duration=>"instant";
        public override string Description=>"Prepare an exact included world from workspace.packs as a new editable workspace. This is a new/open world operation, never an addition to the current room, an automatic installation or a recovery default. Read workspace.current and supply its revision together with exact pack id/version/hash. Returns only a tracked requestId: read workspace.archive.selection until prepared, then workspace.pack.preview for the actual expanded content counts and origin. Current content, activity and original defaults remain unchanged during preparation. Only one file/previous/bundled selection can own preparation or a preview. Cancel through workspace.archive.cancel. To open the reviewed preview, explicitly use workspace.archive.activate with its selection request/generation/manifest and the same expectedRevision; that existing path preserves accepted current edits and opens the new content under its activity review hold. A changed workspace selection invalidates this preview, even if activation supplies a newer revision. Preparation performs no provider request and starts no program, physics, sound or camera.";
        public override JObject InputSchema {get{
            var schema=CurrentInputs(Object(new JObject {["packId"]=Text("^[a-z][a-z0-9.-]*$",96),["packVersion"]=Number(1,int.MaxValue,true),
                ["packHash"]=Text("^[a-f0-9]{64}$",64),["expectedRevision"]=Text("^(initial|[a-f0-9]{32})$",32)}),"workspace.current","expectedRevision");
            schema["x-current"]["fields"]["expectedRevision"]=new JArray("revision");schema["x-features"]=new JArray(Feature);return schema;
        }}
        public override JObject Example=>new(){["packId"]="maestro.observatory",["packVersion"]=1,["packHash"]=new string('0',64),["expectedRevision"]="initial"};
        public override JObject OutputSchema=>WorkspaceSelectionFacts.RequestSchema();
        public override bool CanRun(CapabilityContext context,JObject args,out string error)
        {error="Bundled world preparation is unavailable";var owner=context.Workspace?.Import;return owner&&owner.CanSelectPack(args,out error);}
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(context,args,out error))return false;
            try{operation=new CompletedCapability(new JObject {["requestId"]=context.Workspace.Import.SelectPack(args)});return true;}
            catch(System.Exception){error="Bundled world preparation did not start. Inspect the current workspace before retrying.";return false;}
        }
    }
    internal static class WorkspacePackFacts
    {
        static ProgramDataType Type(JObject fields)=>ProgramDataType.Read(new JObject {["record"]=fields});
        static JObject IdentityFields()=>new(){["instanceId"]="text",["packId"]="text",["packVersion"]="number",["packHash"]="text",["entryId"]="text",["objects"]="number",["resources"]="number"};
        internal static BehaviourCatalog.FactDefinition Catalogue()
        {
            var type=Type(new JObject {
            ["offset"]="number",["total"]="number",["next"]="number",["entries"]=new JObject {["list"]=new JObject {["record"]=new JObject {["id"]="text",["version"]="number",["hash"]="text",["name"]="text",["entryId"]="text"}}}});
            return new("workspace.packs",type,
            "Included editable worlds","Read one exact included-world descriptor at a time. Start offset 0 and follow next until -1. These are explicit new/open workspace sources, not additions to the current room or recovery defaults. Names are display text; prepare only the exact id/version/hash. A source hash identifies its authored descriptor, not a prepared instance or measured performance. Reading never expands, saves, installs, loads native content or starts activity.",
            Object(new JObject {["offset"]=Number(0,32,true)}),new JObject {["offset"]=0},(context,args)=>{
                var value=context.Workspace?.Import?.ReadPacks((int)args["offset"]);return value==null?null:ProgramValue.Literal(value,type);
            },domain:"workspace",features:new[]{WorkspacePackPrepareCapability.Feature});
        }
        internal static BehaviourCatalog.FactDefinition Preview()
        {
            var fields=IdentityFields();foreach(string field in new[]{"requestId","originRevision","worldId"})fields[field]="text";
            foreach(string field in new[]{"recipeParts","collisionPieces","customGeometryVertices","files","bytes"})fields[field]="number";
            var type=Type(fields);return new("workspace.pack.preview",type,"Prepared included world",
                "Read the exact completed bundled-world preview, without re-expanding it. Available only while this selection is prepared and unused. objects/resources count its newly mapped authored objects and definitions; recipeParts/collisionPieces/customGeometryVertices are structural source costs, not native residency or Quest performance. files/bytes describe the verified archive including its baseline avatar and motions. Use workspace.archive.selection for generation/manifest and missing-reference summary. worldId/instanceId identify this new instance; originRevision must still match activation. No current objects, programs or settings are changed by this read.",
                WorkspaceSelectionFacts.RequestSchema(),new JObject {["requestId"]=new string('0',32)},(context,args)=>{
                    var value=context.Workspace?.Import?.ReadPackPreview((string)args["requestId"]);return value==null?null:ProgramValue.Literal(value,type);
                },domain:"workspace",features:new[]{WorkspacePackPrepareCapability.Feature});
        }
        internal static BehaviourCatalog.FactDefinition Instance()
        {
            var type=Type(new JObject {
            ["worldId"]="text",["offset"]="number",["total"]="number",["next"]="number",["entries"]=new JObject {["list"]=new JObject {["record"]=IdentityFields()}}});
            return new("workspace.pack.instance",type,
            "Installed world origins","Read one historical included-world instance from the current accepted workspace at a time. Start offset 0 and follow next until -1. The origin is immutable provenance, not a live dependency, ownership claim or proof that every mapped object still exists: user edits/deletions remain valid. objects/resources are original mapping counts. The exact mapping document remains preserved in saves and exports; this compact fact does not enumerate its slots. No file read, installation, repair or activity starts here.",
            Object(new JObject {["offset"]=Number(0,WorldPackDocument.MaximumInstances,true)}),new JObject {["offset"]=0},(context,args)=>{
                var value=context.Workspace?.ReadPackInstance((int)args["offset"]);return value==null?null:ProgramValue.Literal(value,type);
            },domain:"workspace");
        }
    }
}
