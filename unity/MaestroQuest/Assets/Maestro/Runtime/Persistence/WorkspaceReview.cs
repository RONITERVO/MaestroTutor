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
    /// <summary>Inspects accepted contents, then independently rechecks them under an edit hold
    /// before committing review. Approval never invokes user programs or restores transient effects.</summary>
    internal sealed class WorkspaceReview
    {
        readonly WorkspaceHost host;readonly WorkspaceGenerationStore store;readonly string directory,path;
        JObject record;string journalError;WorkspaceEditHold hold;WorkspaceSelection committed;
        Task<Outcome> pending;CancellationTokenSource cancellation;bool paused,focused=true,disposed,uncertain;
        internal Action<string> Fault;
        sealed class Outcome {internal JObject Record;internal WorkspaceSelection Committed;internal bool Uncertain;}
        static readonly string[] Fields={"version","requestId","revision","generationId","manifestHash","committedRevision","phase","status","summary"};
        internal static readonly string[] Counts={"files","models","motions","modules","unavailablePrograms","missingModels","missingMotions","missingControllerPrograms"};
        static bool Id(string id)=>id!=null&&System.Text.RegularExpressions.Regex.IsMatch(id,"^[a-f0-9]{32}$");
        internal WorkspaceReview(WorkspaceHost host,string applicationData)
        {
            this.host=host;store=new WorkspaceGenerationStore(applicationData,point=>Fault?.Invoke(point));directory=Path.Combine(applicationData,"workspace-review.v1");path=Path.Combine(directory,"latest.json");
            try {
                if(!Directory.Exists(directory)){if(File.Exists(directory))throw new IOException();return;}
                WorkspaceArchive.NoLink(directory);if(!File.Exists(path)){if(Directory.Exists(path))throw new IOException();return;}
                WorkspaceArchive.NoLink(path);using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);if(stream.Length<1||stream.Length>8192)throw new InvalidDataException();
                using var text=new StreamReader(stream,new UTF8Encoding(false,true));using var reader=new JsonTextReader(text) {DateParseHandling=DateParseHandling.None,MaxDepth=8};
                record=JObject.Load(reader,new JsonLoadSettings {DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});if(reader.Read()||!Valid(record))throw new InvalidDataException();
                // A prepared candidate survives restart, but completion always captures again.
                committed=Reconcile(record);
                if(committed!=null){record["phase"]="committed";record["committedRevision"]=committed.Revision;}
                else if((string)record["phase"] is "preparing" or "completing" or "committed" or "unavailable") {record["phase"]="interrupted";record["status"]="Review was interrupted or the selection changed. Inspect current contents; completion was not replayed.";}
            }catch(Exception){record=null;journalError="Workspace review history is unavailable. Its original files are preserved; recovery is required.";}
        }
        static bool Valid(JObject value)
        {
            if(value.Count!=Fields.Length||!Fields.All(value.ContainsKey)||value["version"]?.Type!=JTokenType.Integer||(int)value["version"]!=1||Fields.Skip(1).Where(x=>x!="summary").Any(x=>value[x]?.Type!=JTokenType.String||((string)value[x]).Length>256))return false;
            if((string)value["phase"] is "prepared" or "completing" or "committed" or "completed"&&!ModelLibrary.ValidHash((string)value["manifestHash"]))return false;
            if((string)value["phase"] is "committed" or "completed"&&!Id((string)value["committedRevision"]))return false;
            return Id((string)value["requestId"])&&Id((string)value["revision"])&&((string)value["generationId"]=="original"||Id((string)value["generationId"]))&&
                ((string)value["manifestHash"]==""||ModelLibrary.ValidHash((string)value["manifestHash"]))&&((string)value["committedRevision"]==""||Id((string)value["committedRevision"]))&&
                new[]{"preparing","prepared","completing","committed","completed","stale","failed","cancelled","interrupted","unavailable"}.Contains((string)value["phase"])&&
                value["summary"] is JObject summary&&summary.Count==Counts.Length&&Counts.All(x=>summary[x]?.Type==JTokenType.Integer&&(long)summary[x]>=0&&(long)summary[x]<=1000000);
        }
        void Save(JObject value)
        {
            if(!Valid(value))throw new InvalidDataException("Invalid review record.");Directory.CreateDirectory(directory);WorkspaceArchive.NoLink(directory);if(File.Exists(path))WorkspaceArchive.NoLink(path);
            string next=path+"."+Guid.NewGuid().ToString("N")+".pending";
            try {var bytes=new UTF8Encoding(false,true).GetBytes(value.ToString(Formatting.None));using(var file=new FileStream(next,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}if(File.Exists(path))File.Replace(next,path,null);else File.Move(next,path);}
            finally{if(File.Exists(next))File.Delete(next);}
        }
        void SaveStatus(){try{Save(record);}catch(Exception){journalError="Review status could not be saved. Inspect the current workspace before another review.";}}
        WorkspaceSelection Reconcile(JObject value)=>string.IsNullOrEmpty((string)value["manifestHash"])?null:store.CommittedReview((string)value["requestId"],(string)value["generationId"],(string)value["manifestHash"],(string)value["revision"]);
        internal string RequestId=>(string)record?["requestId"]??"";
        internal bool Busy=>pending!=null||hold!=null||committed!=null;
        internal bool WorkerPending=>pending!=null&&!pending.IsCompleted;
        bool Ready(string revision,out string issue)
        {
            issue=journalError;if(issue!=null)return false;
            if(disposed||paused||!focused||!host||!host.isActiveAndEnabled){issue="Resume Maestro before reviewing a workspace.";return false;}
            if(Busy||host.Switching||host.Activation?.Busy==true){issue="Wait for the current workspace operation to finish.";return false;}
            if(!host.Current||!host.ReviewRequired||host.Selection?.Revision!=revision){issue="Read the current workspace and review its latest selected contents.";return false;}
            try{if(!JToken.DeepEquals(store.Load().Json(),host.Selection.Json())){issue="The stored workspace selection changed. Recover it before review.";return false;}}
            catch(Exception){issue="The workspace selection is unavailable. Its data is preserved; recovery is required.";return false;}
            return WorkspaceArchiveCapture.CanStart(host.Current.Editor,host.Current.Rules,host.Current.Controls,out issue);
        }
        internal bool CanPrepare(string revision,out string issue)=>Ready(revision,out issue);
        internal bool CanComplete(JObject args,out string issue)
        {
            issue="Inspect this exact prepared review before completing it.";
            if(record==null||(string)record["phase"]!="prepared"||(string)args["requestId"]!=RequestId||(string)args["manifestHash"]!=(string)record["manifestHash"]||(string)args["expectedRevision"]!=(string)record["revision"]||!ModelLibrary.ValidHash((string)record["manifestHash"]))return false;
            return Ready((string)record["revision"],out issue);
        }
        internal string Prepare(string revision)
        {
            if(!CanPrepare(revision,out var error))throw new InvalidOperationException(error);
            var counters=new JObject();foreach(string field in Counts)counters[field]=0;
            record=new JObject {["version"]=1,["requestId"]=Guid.NewGuid().ToString("N"),["revision"]=revision,["generationId"]=host.Selection.Active.Generation,["manifestHash"]="",["committedRevision"]="",["phase"]="preparing",["status"]="Inspecting accepted workspace contents",["summary"]=counters};
            Start(false);return RequestId;
        }
        internal string Complete(JObject args){if(!CanComplete(args,out var error))throw new InvalidOperationException(error);record["phase"]="completing";record["status"]="Verifying the inspected contents before completing review";Start(true);return RequestId;}
        void Start(bool complete)
        {
            var content=host.Current;
            try {
                if(!WorkspaceEditHold.TryAcquire(content.Editor,content.Rules,content.Controls,out hold,out var error))throw new InvalidOperationException(error);
                Save(record);Fault?.Invoke("review.beforeCapture");
                // These two stores debounce accepted writes. Preferences/activities/libraries commit
                // before accepting edits; their in-flight writers are excluded by the same hold.
                if(complete&&(!content.Editor.TryFlush(out error)||!content.Rules.TryFlush(out error)))throw new IOException("Accepted workspace contents could not be saved.");
                cancellation=new CancellationTokenSource();var token=cancellation.Token;var capture=WorkspaceArchiveCapture.Fingerprint(content.Editor,content.Rules,content.Controls,token);
                var value=(JObject)record.DeepClone();var origin=host.Selection.Json();pending=Task.Run(()=>Finish(capture,value,origin,complete,token));
            }catch {hold?.Dispose();hold=null;cancellation?.Dispose();cancellation=null;record["phase"]="failed";record["status"]="Review could not start. Accepted contents remain available and activity stays under review.";SaveStatus();throw;}
        }
        static JObject Summary(WorkspaceArchiveSummary summary)=>new JObject {["files"]=summary.Files,["models"]=summary.Models,["motions"]=summary.Motions,["modules"]=summary.Modules,["unavailablePrograms"]=summary.UnavailablePrograms,["missingModels"]=summary.MissingModels.Length,["missingMotions"]=summary.MissingMotions.Length,["missingControllerPrograms"]=summary.MissingControllerPrograms.Length};
        async Task<Outcome> Finish(Task<WorkspaceArchiveReceipt> capture,JObject value,JObject origin,bool complete,CancellationToken token)
        {
            WorkspaceSelection selected=null;bool unknown=false;
            try {
                var snapshot=await capture.ConfigureAwait(false);token.ThrowIfCancellationRequested();Fault?.Invoke("review.captured");token.ThrowIfCancellationRequested();
                if(!complete){value["manifestHash"]=snapshot.ManifestHash;value["summary"]=Summary(snapshot.Summary);value["phase"]="prepared";value["status"]="Inspect these exact contents and missing-reference counts before completing review.";}
                else if(snapshot.ManifestHash!=(string)value["manifestHash"]){value["phase"]="stale";value["status"]="Accepted contents changed after inspection. Prepare and inspect a new review; activity remains held.";}
                else {Fault?.Invoke("review.beforeCommit");token.ThrowIfCancellationRequested();selected=store.CompleteReview((string)value["requestId"],(string)value["generationId"],(string)value["manifestHash"],(string)value["revision"],snapshot.ManifestHash,token);}
            }catch(Exception error) {
                if(complete)try{selected=Reconcile(value);}catch(Exception){}
                if(selected==null){value["phase"]=error is OperationCanceledException?"cancelled":"failed";value["status"]=error is OperationCanceledException?"Review operation was cancelled before a confirmed completion. Inspect its current status.":"Review completion was not confirmed. Accepted contents and original files are preserved.";}
            }
            if(selected!=null){value["phase"]="committed";value["committedRevision"]=selected.Revision;value["status"]="Review approval saved. Releasing its native activity hold.";}
            else {
                try{unknown=!JToken.DeepEquals(store.Load().Json(),origin);}catch(Exception){unknown=true;}
                if(unknown){value["phase"]="unavailable";value["status"]="The review outcome cannot be established. Editing stays held until the workspace is reopened or recovered.";}
            }
            try{Save(value);}catch(Exception){}
            return new Outcome {Record=value,Committed=selected,Uncertain=unknown};
        }
        internal bool CanCancel(string id,out string issue)
        {
            issue=null;if(id!=RequestId||record==null){issue="This review request is no longer retained.";return false;}
            if(committed!=null||(string)record["committedRevision"]!=""||uncertain){issue="Review may already be committed. Inspect its outcome; cancellation cannot undo it.";return false;}return true;
        }
        internal void Cancel(string id)
        {
            if(!CanCancel(id,out var error))throw new InvalidOperationException(error);cancellation?.Cancel();
            if(pending==null){record["phase"]="cancelled";record["status"]="Review cancelled. Activity remains held.";SaveStatus();}
        }
        internal JObject Read(string id)
        {
            if(record==null||id!=RequestId)return null;
            bool same=host.Selection?.Active.Generation==(string)record["generationId"];
            return new JObject {["requestId"]=RequestId,["phase"]=(string)record["phase"],["status"]=journalError??(string)record["status"],["workspace"]=new JObject {["generationId"]=(string)record["generationId"],["revision"]=(string)record["revision"]},
                ["manifestHash"]=(string)record["manifestHash"],["committedRevision"]=(string)record["committedRevision"],["summary"]=record["summary"].DeepClone(),["activityHeld"]=!same||!host.Current||host.Current.Editor.RuntimeGate.Held};
        }
        internal void Poll()
        {
            if(disposed)return;
            if(pending!=null&&pending.IsCompleted) {
                var outcome=pending.GetAwaiter().GetResult();pending=null;record=outcome.Record;committed=outcome.Committed;uncertain=outcome.Uncertain;
                if(committed==null&&(string)record["phase"]=="prepared"&&cancellation.IsCancellationRequested){record["phase"]="cancelled";record["status"]="Review inspection cancelled. Activity remains held.";}
                cancellation.Dispose();cancellation=null;
                if(committed==null){if(!uncertain){hold?.Dispose();hold=null;}SaveStatus();return;}
            }
            if(committed==null||uncertain||paused||!focused||!host.isActiveAndEnabled||host.Switching)return;
            if(host.Current&&host.Selection?.Revision==committed.Revision&&!host.ReviewRequired){FinishRelease();return;}
            if(hold!=null&&host.ApplyReviewedSelection(committed,hold,out _)){FinishRelease();return;}
            uncertain=true;record["phase"]="unavailable";record["status"]="Review approval is saved, but the current owners could not be verified. Activity remains held; reopen or recover the workspace.";SaveStatus();
        }
        void FinishRelease()
        {
            hold?.Dispose();hold=null;committed=null;record["phase"]="completed";
            record["status"]=host.Current.Editor.RuntimeGate.Held?"Review completed. Another native hold still prevents activity.":"Review completed. Stopped programs and physics stay stopped; start desired activity explicitly.";SaveStatus();
        }
        internal void Pause(bool value){paused=value;if(value)cancellation?.Cancel();}
        internal void Focus(bool value){focused=value;if(!value)cancellation?.Cancel();}
        internal void Disable()=>cancellation?.Cancel();
        internal async void Dispose(){disposed=true;cancellation?.Cancel();try{if(pending!=null)await pending;}catch(Exception){}finally{hold?.Dispose();hold=null;cancellation?.Dispose();cancellation=null;}}
    }
}
