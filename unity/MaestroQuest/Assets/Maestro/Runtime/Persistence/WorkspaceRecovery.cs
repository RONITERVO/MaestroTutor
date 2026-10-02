// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Imports;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Persistence
{
    // Persistent shell operation. Opening receipts acknowledge requests, never a completed switch.
    internal sealed class WorkspaceRecovery
    {
        readonly BundledMotions includedMotions;readonly BundledAvatar includedAvatar;readonly WorkspaceHost host;readonly WorkspaceGenerationStore store;readonly string directory,path;
        JObject record;string historyError;Task<Outcome> pending;CancellationTokenSource cancellation;WorkspaceRecoveryHold hold;
        WorkspaceSelection committed;bool disposed,paused,focused=true,uncertain,opening,importBorrowed;
        internal Action<string> Fault;
        sealed class Outcome {internal JObject Record;internal WorkspaceSelection Committed;internal bool Uncertain,Saved=true;}
        internal WorkspaceRecovery(WorkspaceHost host,string applicationData)
        {
            this.host=host;includedAvatar=host.IncludedAvatar;includedMotions=host.IncludedMotions;store=new WorkspaceGenerationStore(applicationData,point=>Fault?.Invoke(point));directory=Path.Combine(applicationData,"workspace-recovery.v1");path=Path.Combine(directory,"latest.json");
            try {
                if(!Directory.Exists(directory)){if(File.Exists(directory))throw new IOException();return;}WorkspaceArchive.NoLink(directory);
                if(!File.Exists(path)){if(Directory.Exists(path))throw new IOException();return;}WorkspaceArchive.NoLink(path);
                using var input=File.OpenRead(path);if(input.Length<1||input.Length>65536)throw new InvalidDataException();using var text=new StreamReader(input,new UTF8Encoding(false,true));using var reader=new JsonTextReader(text){DateParseHandling=DateParseHandling.None,MaxDepth=8};
                record=JObject.Load(reader,new JsonLoadSettings {DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});if(reader.Read()||!Valid(record))throw new InvalidDataException();
                if((string)record["previewId"]!=""&&(string)record["phase"] is "preserving" or "committed" or "review" or "unavailable" or "failed" or "interrupted"){
                    try{committed=store.CommittedDamagedRecovery((string)record["previewId"],(string)record["manifestHash"],(string)record["originHash"]);}catch(Exception){uncertain=true;}
                }
                if(committed!=null){record["phase"]="committed";record["committedRevision"]=committed.Revision;opening=host.Selection?.Revision==committed.Revision&&!host.Current;}
                else if((string)record["phase"] is "inspecting" or "preparing" or "preserving" or "committed" or "cancelling" or "unavailable")Set(record,uncertain?"unavailable":"interrupted","Recovery was interrupted. Inspect current storage; no commit was replayed.");
            }catch(Exception){record=null;historyError="Recovery history is unavailable. Its files are preserved; storage repair is required.";}
        }
        static readonly string[] Fields={"version","requestId","phase","status","originHash","candidates","sourceId","previewId","manifestHash","summary","evidenceHash","committedRevision"};
        static bool Id(string value)=>value!=null&&System.Text.RegularExpressions.Regex.IsMatch(value,"^[a-f0-9]{32}$");
        internal static bool Valid(JObject value)
        {
            if(value==null||value.Count!=Fields.Length||!Fields.All(value.ContainsKey)||value["version"]?.Type!=JTokenType.Integer||(int)value["version"]!=1||Fields.Except(new[]{"version","candidates","summary"}).Any(k=>value[k]?.Type!=JTokenType.String||((string)value[k]).Length>128||((string)value[k]).Any(char.IsControl)))return false;
            if(!Id((string)value["requestId"])||new[]{"sourceId","previewId","committedRevision"}.Any(k=>(string)value[k]!=""&&!Id((string)value[k]))||new[]{"originHash","manifestHash","evidenceHash"}.Any(k=>(string)value[k]!=""&&!ModelLibrary.ValidHash((string)value[k])))return false;
            if(!new[]{"inspecting","inspected","preparing","prepared","preserving","committed","review","failed","cancelled","cancelling","interrupted","unavailable"}.Contains((string)value["phase"]))return false;
            if(value["candidates"] is not JArray candidates||candidates.Count>64||candidates.Any(x=>x is not JObject o||o.Count!=4||!Id((string)o["generationId"])||o["manifestHash"]?.Type!=JTokenType.String||(string)o["manifestHash"]!=""&&!ModelLibrary.ValidHash((string)o["manifestHash"])||o["available"]?.Type!=JTokenType.Boolean||o["error"]?.Type!=JTokenType.String||((string)o["error"]).Length>128))return false;
            return value["summary"] is JObject summary&&summary.Count==WorkspaceReview.Counts.Length&&WorkspaceReview.Counts.All(k=>summary[k]?.Type==JTokenType.Integer&&(long)summary[k]>=0&&(long)summary[k]<=1000000);
        }
        static void Set(JObject value,string phase,string status){value["phase"]=phase;value["status"]=status;}
        void Save(JObject value)
        {
            if(!Valid(value))throw new InvalidDataException("Invalid tracked recovery state.");Directory.CreateDirectory(directory);WorkspaceArchive.NoLink(directory);if(File.Exists(path))WorkspaceArchive.NoLink(path);
            string next=path+"."+Guid.NewGuid().ToString("N")+".pending";
            try{byte[] bytes=new UTF8Encoding(false,true).GetBytes(value.ToString(Formatting.None));using(var file=new FileStream(next,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}if(File.Exists(path))File.Replace(next,path,null);else File.Move(next,path);}finally{if(File.Exists(next))File.Delete(next);}
        }
        bool SaveQuiet(JObject value){try{Save(value);return true;}catch(Exception){return false;}}
        internal bool HistoryUnavailable=>historyError!=null;
        internal JToken HistorySnapshot()=>record?.DeepClone()??JValue.CreateNull();
        internal void ClearHistory(){if(Busy)throw new InvalidOperationException("History is still owned.");record=null;historyError=null;uncertain=false;opening=false;}
        internal bool Busy=>pending!=null||hold!=null||committed!=null;
        internal bool BlocksOtherOperations=>Busy||(string)record?["phase"]=="prepared";
        bool Ready(out string issue,bool allowPreparedImport=false)
        {
            issue=historyError;if(issue!=null)return false;
            if(disposed||paused||!focused||!host||!host.isActiveAndEnabled||!host.Ready||host.Retiring){issue="Wait for previous owners to finish and resume Maestro before recovery.";return false;}
            if(host.Retention?.Busy==true||host.Evidence?.Busy==true||host.History?.Busy==true||Busy||host.Switching||host.Activation?.Busy==true||host.Review?.Busy==true||(host.Import?.Occupied==true&&(!allowPreparedImport||host.Import.StableForRecovery!=true))||host.Export?.Busy==true){issue="Finish the current workspace operation. Restart Maestro if its outcome remains unavailable.";return false;}
            return true;
        }
        internal bool CanInspect(out string issue){if(!Ready(out issue,true))return false;if((string)record?["phase"]=="prepared"){issue="Cancel the current recovery preview before inspecting again.";return false;}return true;}
        internal string Inspect()
        {
            if(!CanInspect(out var issue))throw new InvalidOperationException(issue);uncertain=false;opening=false;
            var counts=new JObject();foreach(var key in WorkspaceReview.Counts)counts[key]=0;
            record=new JObject {["version"]=1,["requestId"]=Guid.NewGuid().ToString("N"),["phase"]="inspecting",["status"]="Inspecting retained workspace metadata",["originHash"]="",["candidates"]=new JArray(),["sourceId"]="",["previewId"]="",["manifestHash"]="",["summary"]=counts,["evidenceHash"]="",["committedRevision"]=""};
            Save(record);cancellation=new CancellationTokenSource();var copy=(JObject)record.DeepClone();var token=cancellation.Token;pending=Task.Run(()=>InspectWorker(copy,token));return (string)record["requestId"];
        }
        Outcome InspectWorker(JObject value,CancellationToken token)
        {
            try{token.ThrowIfCancellationRequested();var inspected=store.InspectRecovery();Fault?.Invoke("recovery.inspected");token.ThrowIfCancellationRequested();value["originHash"]=inspected["originHash"];value["candidates"]=inspected["candidates"];Set(value,"inspected","Inspect candidate metadata, then select an exact candidate for full verification.");}
            catch(Exception ex){Set(value,ex is OperationCanceledException?"cancelled":"failed","Recovery inspection stopped. Original workspace files are unchanged.");}
            return new Outcome {Record=value,Saved=SaveQuiet(value)};
        }
        bool Matches(string request)=>record!=null&&(string)record["requestId"]==request;
        internal bool CanSelect(JObject args,out string issue)
        {
            issue="Inspect this exact recovery request before choosing its source.";
            if(!Matches((string)args["requestId"])||(string)record["phase"]!="inspected"||(string)record["originHash"]!=(string)args["originHash"]||args["source"] is not JObject source)return false;
            if((string)source["kind"]=="fresh"){
                if(source.Count!=1)return false;
                if(host.Import?.Occupied==true){issue="Cancel or recover the selected archive before choosing a fresh workspace.";return false;}
            }else if((string)source["kind"]!="retained"||!((JArray)record["candidates"]).Any(x=>(string)x["generationId"]==(string)source["generationId"]&&(string)x["manifestHash"]==(string)source["manifestHash"]&&(bool)x["available"])||host.Import?.CanRecoverCandidate((string)source["generationId"],(string)source["manifestHash"])==false)return false;
            return Ready(out issue,true);
        }
        internal string Select(JObject args)
        {
            if(!CanSelect(args,out var issue))throw new InvalidOperationException(issue);var source=(JObject)args["source"];bool fresh=(string)source["kind"]=="fresh";
            record["sourceId"]=fresh?"":(string)source["generationId"];Set(record,"preparing",fresh?"Preparing a fresh workspace with the included book and Maestro":"Verifying and copying the selected recovery candidate");Save(record);
            if(!fresh)importBorrowed=host.Import?.BorrowForRecovery((string)source["generationId"],(string)source["manifestHash"])==true;
            cancellation=new CancellationTokenSource();var token=cancellation.Token;var copy=(JObject)record.DeepClone();string hash=(string)source["manifestHash"];pending=Task.Run(()=>PrepareWorker(copy,hash,token));return (string)record["requestId"];
        }
        void ReleaseImportedSource(){if(!importBorrowed)return;host.Import?.FinishRecovery();importBorrowed=false;}
        Outcome PrepareWorker(JObject value,string hash,CancellationToken token)
        {
            try{var prepared=(string)value["sourceId"]==""?store.PrepareFreshRecovery((string)value["originHash"],token,includedAvatar,includedMotions):store.PrepareDamagedRecovery((string)value["originHash"],(string)value["sourceId"],hash,token);value["previewId"]=prepared.Id;value["manifestHash"]=prepared.Receipt.ManifestHash;value["summary"]=Summary(prepared.Receipt.Summary);Set(value,"prepared","Inspect verified contents and missing references before explicitly committing recovery.");}
            catch(Exception ex){Set(value,ex is OperationCanceledException?"cancelled":"failed","Candidate verification stopped. Original data is preserved; inspect another candidate if needed.");}
            return new Outcome {Record=value,Saved=SaveQuiet(value)};
        }
        static JObject Summary(WorkspaceArchiveSummary s)=>new() {["files"]=s.Files,["models"]=s.Models,["motions"]=s.Motions,["modules"]=s.Modules,["unavailablePrograms"]=s.UnavailablePrograms,["missingModels"]=s.MissingModels.Length,["missingMotions"]=s.MissingMotions.Length,["missingControllerPrograms"]=s.MissingControllerPrograms.Length};
        internal bool CanCommit(JObject args,out string issue)
        {
            issue="Inspect and request this exact prepared recovery before committing it.";
            if(!Matches((string)args["requestId"])||(string)record["phase"]!="prepared"||(string)record["originHash"]!=(string)args["originHash"]||(string)record["previewId"]!=(string)args["generationId"]||(string)record["manifestHash"]!=(string)args["manifestHash"])return false;
            if(!Ready(out issue))return false;return !host.Current||WorkspaceRecoveryHold.CanAcquire(host.Current.Editor,host.Current.Rules,host.Current.Controls,out issue);
        }
        internal string Commit(JObject args)
        {
            if(!CanCommit(args,out var issue))throw new InvalidOperationException(issue);
            if(host.Current&&!WorkspaceRecoveryHold.TryAcquire(host.Current.Editor,host.Current.Rules,host.Current.Controls,out hold,out issue))throw new InvalidOperationException(issue);
            Set(record,"preserving","Preserving current accepted data before recovery; editing is held until workers finish.");cancellation=new CancellationTokenSource();var token=cancellation.Token;var copy=(JObject)record.DeepClone();
            try {
                Save(record);string output=store.RecoveryEvidenceDirectory((string)copy["previewId"],(string)copy["manifestHash"],(string)copy["originHash"]);
                Task<CapturedRecoveryEvidence> capture;
                if(hold!=null)capture=hold.Capture(output,token);
                else {
                    var empty=new JObject {["version"]=1,["available"]=new JObject {["room"]=false,["behaviours"]=false,["controls"]=false,["activities"]=false},["room"]=new JObject(),["behaviours"]=new JObject(),["controls"]=new JObject(),["activities"]=new JObject(),["temporaryRoom"]=JValue.CreateNull()};
                    var bytes=WorkspaceRecoveryEvidence.EncodeAccepted(empty);string absent=Path.Combine(directory,"no-live-owners-"+Guid.NewGuid().ToString("N"));
                    capture=Task.Run(()=>WorkspaceRecoveryEvidence.Write(absent,output,bytes,token));
                }
                pending=Task.Run(()=>CommitWorker(copy,capture,token));
            }catch(Exception){var drain=hold?.Completion??Task.CompletedTask;pending=Task.Run(async()=>{try{await drain.ConfigureAwait(false);}catch(Exception){}Set(copy,"failed","Recovery could not start. Earlier saves finished; original data is preserved.");return new Outcome {Record=copy,Saved=SaveQuiet(copy)};});}
            return (string)record["requestId"];
        }
        async Task<Outcome> CommitWorker(JObject value,Task<CapturedRecoveryEvidence> capture,CancellationToken token)
        {
            WorkspaceSelection next=null;bool unknown=false;
            try {
                var preserved=await capture.ConfigureAwait(false);value["evidenceHash"]=preserved.Sha256;Fault?.Invoke("recovery.preserved");token.ThrowIfCancellationRequested();
                store.InspectDamagedPreview((string)value["previewId"],(string)value["manifestHash"],(string)value["originHash"],token);Save(value);
                next=store.CommitDamagedRecovery((string)value["previewId"],(string)value["manifestHash"],(string)value["originHash"],preserved,token);
            }catch(Exception ex){
                try{next=store.CommittedDamagedRecovery((string)value["previewId"],(string)value["manifestHash"],(string)value["originHash"]);}catch(Exception){unknown=true;}
                if(next==null){try{unknown|=(string)store.InspectRecovery()["originHash"]!=(string)value["originHash"];}catch(Exception){unknown=true;}Set(value,unknown?"unavailable":ex is OperationCanceledException?"cancelled":"failed",unknown?"Recovery outcome is uncertain. Editing stays held; restart Maestro before another recovery.":"Recovery stopped before a confirmed switch. Preserved data remains available; nothing was replayed.");}
            }
            if(next!=null){value["committedRevision"]=next.Revision;Set(value,"committed","Recovery selection committed. Opening the recovered workspace under review.");}
            return new Outcome {Record=value,Committed=next,Uncertain=unknown,Saved=SaveQuiet(value)};
        }
        internal bool CanCancel(string request,out string issue)
        {
            if(host.Retention?.Busy==true||host.Evidence?.Busy==true||host.History?.Busy==true){issue="Wait for history preservation to finish.";return false;}
            issue="This recovery request is not cancellable.";if(!Matches(request)||committed!=null||(string)record["committedRevision"]!=""||uncertain&&hold!=null)return false;issue=null;return true;
        }
        internal void Cancel(string request)
        {
            if(!CanCancel(request,out var issue))throw new InvalidOperationException(issue);cancellation?.Cancel();if(pending!=null)return;
            if((string)record["phase"]=="prepared"){Set(record,"cancelling","Discarding only the unused recovery preview");var copy=(JObject)record.DeepClone();pending=Task.Run(()=>DiscardWorker(copy));}
            else{Set(record,"cancelled","Recovery request cancelled. Existing files and stopped activity are preserved.");if(!SaveQuiet(record))historyError="Recovery status could not be saved.";}
        }
        Outcome DiscardWorker(JObject value)
        {
            try{store.DiscardDamagedPreview((string)value["previewId"],(string)value["manifestHash"]);value["previewId"]="";value["manifestHash"]="";Set(value,"cancelled","Unused recovery preview discarded. Original files are preserved.");}
            catch(Exception){Set(value,"failed","Recovery preview contains protected or unavailable data. It is retained for inspection.");}
            return new Outcome {Record=value,Saved=SaveQuiet(value)};
        }
        internal JObject Status()=>new() {["requestId"]=(string)record?["requestId"]??"",["phase"]=historyError!=null?"unavailable":(string)record?["phase"]??"idle",["status"]=historyError??(string)record?["status"]??"Inspect recovery choices to begin.",["originHash"]=(string)record?["originHash"]??"",["candidateCount"]=(record?["candidates"] as JArray)?.Count??0,["preview"]=new JObject {["generationId"]=(string)record?["previewId"]??"",["manifestHash"]=(string)record?["manifestHash"]??""},["committedRevision"]=(string)record?["committedRevision"]??"",["evidenceHash"]=(string)record?["evidenceHash"]??""};
        internal JObject Candidate(string request,int index)=>!Matches(request)||index<0||index>=((JArray)record["candidates"]).Count?null:(JObject)record["candidates"][index].DeepClone();
        internal JObject Preview(string request)=>!Matches(request)||(string)record["previewId"]==""?null:new JObject {["requestId"]=request,["generationId"]=(string)record["previewId"],["manifestHash"]=(string)record["manifestHash"],["summary"]=record["summary"].DeepClone(),["source"]=new JObject {["kind"]=(string)record["sourceId"]==""?"fresh":"retained",["generationId"]=record["sourceId"].DeepClone()}};
        internal void Poll()
        {
            if(disposed)return;
            if(pending!=null&&pending.IsCompleted){
                var result=pending.GetAwaiter().GetResult();pending=null;ReleaseImportedSource();record=result.Record;committed=result.Committed;uncertain=result.Uncertain;if(!result.Saved)historyError="Recovery status could not be saved. Inspect current storage before another request.";
                bool cancelled=cancellation?.IsCancellationRequested==true;cancellation?.Dispose();cancellation=null;
                if(committed==null){if(!uncertain){hold?.Dispose();hold=null;}if(cancelled&&(string)record["phase"]=="prepared")Cancel((string)record["requestId"]);return;}
            }
            if(committed==null||uncertain||paused||!focused||!host.isActiveAndEnabled||host.Switching||host.Retiring)return;
            if(host.Current&&host.Selection?.Revision==committed.Revision){hold?.Dispose();hold=null;committed=null;Set(record,"review","Recovered workspace opened. Complete content review before starting activity.");SaveQuiet(record);return;}
            if(opening){committed=null;Set(record,"unavailable","The committed workspace could not open. Preserved data remains available for another recovery.");SaveQuiet(record);return;}
            if(host.ReplaceRecovered(committed,hold,out _)){hold=null;opening=true;return;}
            uncertain=true;Set(record,"unavailable","Committed selection could not replace current owners. Editing stays held; restart Maestro.");SaveQuiet(record);
        }
        internal void Pause(bool value){paused=value;if(value)cancellation?.Cancel();}
        internal void Focus(bool value){focused=value;if(!value)cancellation?.Cancel();}
        internal void Disable()=>cancellation?.Cancel();
        internal async Task Dispose()
        {
            disposed=true;cancellation?.Cancel();
            try{if(pending!=null){var result=await pending;record=result.Record;if((string)record["phase"]=="prepared")await Task.Run(()=>DiscardWorker(record));}if(hold!=null)try{await hold.Completion;}catch(Exception){}}
            catch(Exception){}finally{ReleaseImportedSource();hold?.Dispose();hold=null;cancellation?.Dispose();cancellation=null;}
        }
    }
}
