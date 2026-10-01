// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Persistence;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class WorkspaceRecoveryCapability:CapabilityModule
    {
        readonly string mode;internal WorkspaceRecoveryCapability(string mode){this.mode=mode;}
        public override string Id=>"workspace.recovery."+mode;
        public override string Domain=>"workspace";
        public override string Duration=>"instant";
        public override string Label=>mode switch {"inspect"=>"Inspect workspace recovery choices","select"=>"Prepare recovery preview","commit"=>"Recover the inspected workspace",_=>"Cancel workspace recovery"};
        public override string Description=>mode switch {
            "inspect"=>"Inspect retained workspace metadata without changing the selected root. Supports a missing/corrupt selection or unreadable current stores. Returns a tracked requestId. Read workspace.recovery until inspected, then read workspace.recovery.candidate by requestId and index. Metadata availability is not proof that content verifies. Existing content remains available during inspection. No fallback or empty room is opened. A fully prepared archive from workspace.archive.select may be inspected here as a retained candidate, including when no old candidate is usable. Cancel an unused prepared recovery preview before inspecting again. Only the latest request is retained.",
            "select"=>"Choose a source for the latest inspected request using requestId and originHash. source.kind=retained requires the exact candidate generationId and manifestHash; a prepared external archive appears in this same candidate list. source.kind=fresh explicitly requests a new workspace with only the included book and Maestro, default controls and no user models, motions or programs. Native code verifies and copies its immutable content into a private preview and preserves both original selection files. Read workspace.recovery until prepared, then workspace.recovery.preview for its new generation/hash and missing-reference counts. Read the preview source to distinguish a fresh workspace from a retained candidate. The source choice only prepares a preview; commit is a separate explicit request. Later authored changes can make an old candidate unverifiable. Selection never changes live content, starts activity or repairs original files.",
            "commit"=>"Commit only the exact verified recovery preview the user requested after inspection. Supply its requestId, originHash and preview generationId/manifestHash. Native code holds activity/edits, drains old saves and preserves original bytes plus labelled accepted state before checking the choice again. Missing live owners are explicitly labelled; existing roots remain protected. Returns the tracked requestId, not completion. Read workspace.recovery until review or failure. New content opens with fresh action history under review; complete that review separately. Cancellation cannot undo a committed selection. An uncertain result keeps old owners held and requires restart; never blindly retry an old preview/evidence pair. If no retained candidate verifies, select an external archive or explicitly prepare source.kind=fresh, inspect that new preview and then commit it.",
            _=>"Cancel the exact tracked recovery. Workers and earlier saves must finish before editing releases. An unused preview can be discarded; preserved evidence remains retained. Cancellation cannot retract a committed selection. Read workspace.recovery for its actual outcome. A stale request ID cannot cancel a newer request."};
        public override JObject InputSchema {get{
            var fields=new JObject();if(mode!="inspect")fields["requestId"]=Text("^[a-f0-9]{32}$",32);
            if(mode is "select" or "commit")fields["originHash"]=Text("^[a-f0-9]{64}$",64);
            if(mode=="commit"){fields["generationId"]=Text("^[a-f0-9]{32}$",32);fields["manifestHash"]=Text("^[a-f0-9]{64}$",64);}
            if(mode=="select"){
                JObject Variant(string kind){var input=(JObject)fields.DeepClone();var source=new JObject {["kind"]=Choice(kind)};source["kind"]["x-static"]=true;if(kind=="retained"){source["generationId"]=Text("^[a-f0-9]{32}$",32);source["manifestHash"]=Text("^[a-f0-9]{64}$",64);}input["source"]=Object(source);var variant=Object(input);variant["x-features"]=new JArray("workspaceRecovery.v1");variant["title"]=kind=="fresh"?"Fresh workspace":"Retained or imported workspace";return variant;}
                return new JObject {["type"]="object",["title"]="Recovery source",["oneOf"]=new JArray(Variant("retained"),Variant("fresh")),["x-discriminators"]=new JArray("source.kind"),["x-features"]=new JArray("workspaceRecovery.v1")};
            }
            var schema=Object(fields);schema["x-features"]=new JArray("workspaceRecovery.v1");return schema;
        }}
        public override JObject Example {get{var args=new JObject();if(mode!="inspect")args["requestId"]=new string('0',32);if(mode is "select" or "commit")args["originHash"]=new string('0',64);if(mode=="select")args["source"]=new JObject {["kind"]="retained",["generationId"]=new string('0',32),["manifestHash"]=new string('0',64)};if(mode=="commit"){args["generationId"]=new string('0',32);args["manifestHash"]=new string('0',64);}return args;}}
        public override JObject OutputSchema=>WorkspaceSelectionFacts.RequestSchema();
        public override bool CanRun(CapabilityContext context,JObject args,out string error)
        {error="Workspace recovery is unavailable";var recovery=context.Workspace?.Recovery;if(recovery==null)return false;return mode switch {"inspect"=>recovery.CanInspect(out error),"select"=>recovery.CanSelect(args,out error),"commit"=>recovery.CanCommit(args,out error),_=>recovery.CanCancel((string)args["requestId"],out error)};}
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(context,args,out error))return false;var recovery=context.Workspace.Recovery;
            try{string id;if(mode=="inspect")id=recovery.Inspect();else if(mode=="select")id=recovery.Select(args);else if(mode=="commit")id=recovery.Commit(args);else{id=(string)args["requestId"];recovery.Cancel(id);}operation=new CompletedCapability(new JObject {["requestId"]=id});return true;}
            catch(System.Exception){error="Recovery request could not start. Read workspace.recovery before another request.";return false;}
        }
    }
    internal static class WorkspaceRecoveryFacts
    {
        internal static BehaviourCatalog.FactDefinition Status()=>new("workspace.recovery",ProgramDataType.Read(new JObject {["record"]=new JObject {["requestId"]="text",["phase"]="text",["status"]="text",["originHash"]="text",["candidateCount"]="number",["preview"]=new JObject {["record"]=new JObject {["generationId"]="text",["manifestHash"]="text"}},["committedRevision"]="text",["evidenceHash"]="text"}}),"Workspace recovery status",
            "Read the latest recovery request and verify its requestId matches your operation. Inspecting/inspected only describe metadata; preparing/prepared describe a verified copy. Preserving holds editing while workers finish; committed means selection changed, and review means recovered content opened under review. Failed, cancelled, interrupted or unavailable never imply an empty-room fallback or completed switch. EvidenceHash identifies private preserved bytes, not a download. This read never starts or retries an operation.",Object(new JObject()),new JObject(),(context,args)=>context.Workspace?.Recovery==null?null:ProgramValue.Literal(context.Workspace.Recovery.Status()),domain:"workspace");
        internal static BehaviourCatalog.FactDefinition Candidate()=>new("workspace.recovery.candidate",ProgramDataType.Read(new JObject {["record"]=new JObject {["generationId"]="text",["manifestHash"]="text",["available"]="boolean",["error"]="text"}}),"Workspace recovery candidate",
            "Read one retained candidate by the exact inspection requestId and zero-based index below candidateCount. Availability means metadata only. Select the exact identity through workspace.recovery.select to verify documents and assets. Bad entries stay visible; original files are preserved. An outdated request or index is unavailable.",Object(new JObject {["requestId"]=Text("^[a-f0-9]{32}$",32),["index"]=Number(0,63,true)}),new JObject {["requestId"]=new string('0',32),["index"]=0},(context,args)=>{var value=context.Workspace?.Recovery?.Candidate((string)args["requestId"],(int)args["index"]);return value==null?null:ProgramValue.Literal(value);},domain:"workspace");
        internal static BehaviourCatalog.FactDefinition Preview()
        {
            var counts=new JObject();foreach(var key in WorkspaceReview.Counts)counts[key]="number";
            return new BehaviourCatalog.FactDefinition("workspace.recovery.preview",ProgramDataType.Read(new JObject {["record"]=new JObject {["requestId"]="text",["generationId"]="text",["manifestHash"]="text",["summary"]=new JObject {["record"]=counts},["source"]=new JObject {["record"]=new JObject {["kind"]="text",["generationId"]="text"}}}}),"Verified recovery preview",
                "Inspect the explicit source kind (fresh or retained), its source generation if retained, the new preview generation/hash and verified semantic counts for the exact recovery request. Review missing references with the user before requesting workspace.recovery.commit. These counts do not approve execution or substitute missing models/motions. Recovered activity remains held until a separate workspace review completes.",WorkspaceSelectionFacts.RequestSchema(),new JObject {["requestId"]=new string('0',32)},(context,args)=>{var value=context.Workspace?.Recovery?.Preview((string)args["requestId"]);return value==null?null:ProgramValue.Literal(value);},domain:"workspace");
        }
    }
}
