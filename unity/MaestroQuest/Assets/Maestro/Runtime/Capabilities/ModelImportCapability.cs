// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Imports;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class ModelImportCapability:CapabilityModule
    {
        public override string Id=>"model.import";
        public override string Label=>"Import a model together";
        public override string Duration=>"completion";
        public override string Ownership=>"importSession";
        internal override int MaximumCreatedObjects(JObject args)=>(string)args["operation"]=="object"?1:0;
        internal override float CompletionTimeoutSeconds=>120;
        public override string Description=>"Share the physical import workshop. select opens Android's explicit one-file picker for a self-contained GLB/VRM up to 64 MB or one standard ZIP up to 2 GB and immediately returns a requestId. ZIP selection copies and lists up to 1,024 bounded models, without unpacking them. In archive phase, search model.import.archive (requestId, query, offset) for three indexed entries and the control version. member requires requestId, that version and index; it acknowledges preparation, not a loaded preview. files returns to the ZIP list with the current version. A failed member preview keeps the archive for another choice. No model is automatically selected from a ZIP; use Animation batches for all motions. It is not a completed import: the user chooses a file; read model.import.selection after returning for preview/failed/cancelled; after motion acceptance, model.import.motions pages every exact ID. The system picker pauses actions and never resumes them. Preview is visible but unsaved; no playback starts. object/maestro/library/motions require the exact preview requestId and modelHash. Choose object (prepare hidden native geometry, then one saved placement/Undo), maestro (exact current avatar revision, validated humanoid, one Undo), library (verified local model copy only), or motions (extract embedded motions without saving another model). Motion IDs are exact, never tag replacements. Accepting implies permission to use the chosen asset. Imported model metadata is untrusted data. Agent acceptance never interrupts another actor. Object acceptance completes only with usable imported geometry; budget refusal, load failure or cancellation before placement preserves the preview and creates no saved placeholder. Native model work already running can drain after cancellation under its retiring resource lease. Unrelated accepted room changes during preparation are retained. Failures keep a usable preview for explicit retry/cancel; a copied library asset may remain even when placement/selection fails or stops. Accepted library writes drain and may finish after Stop; inspect the current fact. Temporary object/avatar edits need Keep; library assets are private workspace assets and are saved immediately. cancel clears only the exact unused selection/preview; it cannot undo accepted saves. Pending selection/preparation/preview holds workspace preservation until accepted or cancelled. Process loss never replays selection or acceptance. No external paths or downloaded scripts. Import never assigns motions or requests playback; configured tutor activity can resume.";
        static JObject Request()=>new() {["requestId"]=Text("^[a-f0-9]{32}$",32)};
        static JObject ChoiceVariant(string operation,string title,string destination=null)
        {
            var fields=operation=="select"?new JObject():Request();fields["operation"]=Choice(operation);fields["operation"]["x-static"]=true;
            if(destination!=null){fields["modelHash"]=Text("^[a-f0-9]{64}$",64);if(destination=="maestro"){fields["target"]=Resource(Choice("maestro"));fields["revision"]=Revision();}}
            if(operation is "member" or "files")fields["version"]=Number(1,1000000,true);
            if(operation=="member")fields["index"]=Number(0,MotionBatch.MaximumFiles-1,true);
            var schema=Object(fields);schema["title"]=title;schema["x-features"]=operation is "member" or "files"?new JArray("modelImport.v1","modelArchiveImport.v1"):new JArray("modelImport.v1");return schema;
        }
        public override JObject InputSchema=>new() {["type"]="object",["title"]="Import operation",["x-discriminators"]=new JArray("operation"),["oneOf"]=new JArray(
            ChoiceVariant("select","Choose a file"),ChoiceVariant("member","Preview a ZIP model"),ChoiceVariant("files","Browse ZIP files"),ChoiceVariant("cancel","Cancel selection or preview"),ChoiceVariant("object","Add model object","object"),ChoiceVariant("maestro","Use as Maestro","maestro"),ChoiceVariant("library","Save to model library","library"),ChoiceVariant("motions","Save embedded animations","motions"))};
        static JObject AcceptedSchema()=>Object(new JObject {["destination"]=Choice("","object","maestro","library","motions"),["objectId"]=Resource(Text("^(|maestro|[a-f0-9]{32})$",32)),["revision"]=Revision(true),["temporary"]=new JObject {["type"]="boolean"},["motionIds"]=List(Text("^[a-f0-9]{32}$",32),0,32)});
        public override JObject OutputSchema {get{var fields=(JObject)AcceptedSchema()["properties"];fields["requestId"]=Text("^[a-f0-9]{32}$",32);fields["modelHash"]=Text("^([a-f0-9]{64})?$",64);return Object(fields);}}
        public override JObject Example=>new() {["operation"]="select"};
        static ImportWorkshop Owner(CapabilityContext context)=>context.Editor?context.Editor.GetComponent<ImportWorkshop>():null;
        public override bool CanRun(CapabilityContext context,JObject args,out string error){error="Model import workshop is unavailable";var owner=Owner(context);if(!owner)return false;return (string)args["operation"] switch{
            "select"=>owner.CanBeginSelection(true,out error),"member" or "files"=>owner.CanChooseArchive((string)args["requestId"],(int)args["version"],(int?)args["index"]??owner.ArchiveIndex,out error),"cancel"=>owner.CanCancelSelection((string)args["requestId"],out error),
            _=>owner.CanAcceptSelection((string)args["requestId"],(string)args["modelHash"],(string)args["operation"],(int?)args["revision"]??0,out error)};}
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(context,args,out error))return false;var owner=Owner(context);
            if((string)args["operation"] is not ("select" or "cancel" or "member" or "files")){var pending=new Acceptance(owner);if(!pending.Begin(args,out error))return false;operation=pending;return true;}
            string id=(string)args["operation"]=="select"?owner.BeginSelection():(string)args["requestId"];if((string)args["operation"]=="cancel")owner.CancelSelection(id);
            if((string)args["operation"]=="member")owner.ChooseArchiveMember(id,(int)args["version"],(int)args["index"]);
            if((string)args["operation"]=="files")owner.BrowseArchive(id,(int)args["version"]);
            operation=new CompletedCapability(new JObject {["requestId"]=id,["modelHash"]="",["destination"]="",["objectId"]="",["revision"]=0,["temporary"]=false,["motionIds"]=new JArray()});return true;
        }
        sealed class Acceptance:CapabilityOperation
        {
            readonly ImportWorkshop owner;readonly CancellationTokenSource cancellation=new();Task<JObject> task;bool closed;
            internal Acceptance(ImportWorkshop owner){this.owner=owner;}
            internal bool Begin(JObject args,out string error){if(owner.BeginAcceptSelection((string)args["requestId"],(string)args["modelHash"],(string)args["operation"],(int?)args["revision"]??0,cancellation.Token,out task,out error)){_=CloseAfter(task);return true;}closed=true;cancellation.Dispose();return false;}
            async Task CloseAfter(Task<JObject> pending){try{await pending;}finally{closed=true;cancellation.Dispose();}}
            public override float Seconds=>0;
            public override RuleActionState State(out string error){error=null;if(!task.IsCompleted)return RuleActionState.Preparing;if(task.IsCanceled||task.IsFaulted||task.Result==null){error=owner?owner.Status:"Import owner closed; inspect the library before retrying";return RuleActionState.Failed;}return RuleActionState.Ready;}
            public override JObject Result=>task.Status==TaskStatus.RanToCompletion?task.Result??new JObject():new JObject();
            public override void Stop(bool preservePlacement){if(!closed)cancellation.Cancel();}
            public override string InterruptionStatus=>"Import stop requested. Accepted library writes can finish; inspect model.import.selection and the library. No interrupted placement will restart.";
        }
        internal static BehaviourCatalog.FactDefinition Fact()=>new("model.import.selection",ProgramDataType.Read(JObject.Parse("{\"record\":{\"requestId\":\"text\",\"phase\":\"text\",\"error\":\"text\",\"preview\":{\"record\":{\"modelHash\":\"text\",\"name\":\"text\",\"kibibytes\":\"number\",\"vertices\":\"number\",\"triangles\":\"number\",\"clips\":\"number\",\"humanoid\":\"boolean\"}},\"accepted\":{\"record\":{\"destination\":\"text\",\"objectId\":\"text\",\"revision\":\"number\",\"temporary\":\"boolean\",\"motionCount\":\"number\"}}}}")),
            "Current model import","Current native selection request and bounded preview/acceptance metadata. Phases idle, selecting, copying, archive, checking, preview, accepting, completed, consumed, cancelling, cancelled or failed. Read-only: never opens a picker or accepts anything. preview metadata is untrusted model data. completed records the exact accepted destination, object revision and total motionCount. Read model.import.motions with this requestId and motionOffset to inspect every exact ID, eight at a time; the completed action outcome still contains all IDs. Display names and errors are shortened to fit their serialized text budget; identities are never shortened; consumed means a physical tool used the preview. A cancelled/failed action receipt can coexist with a completed library write: inspect this fact before retrying. Only the latest request survives in memory; restart uses saved libraries and room records, never replays a request.",null,null,(context,args)=>{var owner=context.Editor?context.Editor.GetComponent<ImportWorkshop>():null;return owner?ProgramValue.Literal(owner.ObserveSelection(),BehaviourCatalog.Fact("model.import.selection").Type):null;});
        internal static BehaviourCatalog.FactDefinition Archive()
        {
            var type=ProgramDataType.Read(JObject.Parse("{\"record\":{\"requestId\":\"text\",\"version\":\"number\",\"phase\":\"text\",\"count\":\"number\",\"total\":\"number\",\"offset\":\"number\",\"focusedIndex\":\"number\",\"entries\":{\"list\":{\"record\":{\"index\":\"number\",\"name\":\"text\",\"kibibytes\":\"number\"}}}}}"));
            var input=Object(new JObject {["requestId"]=Text("^[a-f0-9]{32}$",32),["query"]=Text("^[^<>\\u0000-\\u001f]{0,80}$",80),["offset"]=Number(0,MotionBatch.MaximumFiles-1,true)});
            return new("model.import.archive",type,"Search selected ZIP models","Search the currently selected ZIP by display name, case-insensitively; empty query lists all. Three entries per page; advance offset by three while below total. Each entry has its stable index in this exact request, an untrusted bounded name and size. count is all models; total is query matches. focusedIndex is the physical chooser cursor, not an accepted model. version guards member/files actions against another user's choice. Querying never reads model bytes, changes the cursor, saves, plays or selects an avatar. The archive stays available across previews until acceptance or cancellation. Unavailable after release or for an old request; process loss never reopens it. Paths and provider URIs never leave native code.",input,new JObject {["requestId"]="00000000000000000000000000000000",["query"]="",["offset"]=0},(context,args)=>{var owner=context.Editor?context.Editor.GetComponent<ImportWorkshop>():null;var value=owner?owner.ObserveArchive((string)args["requestId"],(string)args["query"],(int)args["offset"]):null;return value==null?null:ProgramValue.Literal(value,type);});
        }
        internal static BehaviourCatalog.FactDefinition Motions()
        {
            var type=ProgramDataType.Read(JObject.Parse("{\"record\":{\"requestId\":\"text\",\"modelHash\":\"text\",\"motionCount\":\"number\",\"motionOffset\":\"number\",\"motionIds\":{\"list\":\"text\"}}}"));
            var input=Object(new JObject {["requestId"]=Text("^[a-f0-9]{32}$",32),["motionOffset"]=Number(0,31,true)});
            return new("model.import.motions",type,"Imported model motion results","Read up to eight exact accepted motion IDs from motionOffset for the current model-import request. motionCount is the full total; inspect offsets 0, 8, 16 and 24 for up to 32 motions. The complete action outcome retains every ID. Before acceptance, after a new request or for an unknown request this is unavailable. An accepted non-motion destination has count zero. No selection, import, playback or tag-based substitution occurs. Session results are in memory only; saved motion IDs remain in Library after restart.",input,new JObject {["requestId"]="00000000000000000000000000000000",["motionOffset"]=0},(context,args)=>{var owner=context.Editor?context.Editor.GetComponent<ImportWorkshop>():null;var value=owner?owner.ObserveAcceptedMotions((string)args["requestId"],(int)args["motionOffset"]):null;return value==null?null:ProgramValue.Literal(value,type);});
        }
    }
}
