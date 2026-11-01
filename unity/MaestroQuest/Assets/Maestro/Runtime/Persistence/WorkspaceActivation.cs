// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Persistence
{
    /// <summary>Persistent-shell operation, independent of the opening action receipt. Only the
    /// owner thread captures/replaces content. Workers retain and commit while its edit hold is owned.</summary>
    internal sealed class WorkspaceActivation
    {
        readonly WorkspaceHost host;readonly WorkspaceGenerationStore store;readonly string directory,journal;
        JObject record;string journalError;Task<Outcome> pending;CancellationTokenSource cancellation;
        WorkspaceEditHold hold;WorkspaceSelection committed;bool disposed,paused,focused=true,replacementFailed;
        internal Action<string> Fault;
        sealed class Outcome {internal JObject Record;internal WorkspaceSelection Committed;internal bool Reusable,UncertainSelection;}
        internal WorkspaceActivation(WorkspaceHost host,string applicationData)
        {
            this.host=host;store=new WorkspaceGenerationStore(applicationData,point=>Fault?.Invoke(point));
            directory=Path.Combine(applicationData,"workspace-activation.v1");journal=Path.Combine(directory,"latest.json");
            try {
                if(!Directory.Exists(directory)){if(File.Exists(directory))throw new IOException();return;}
                WorkspaceArchive.NoLink(directory);if(!File.Exists(journal)){if(Directory.Exists(journal))throw new IOException();return;}
                WorkspaceArchive.NoLink(journal);if(new FileInfo(journal).Length>8192)throw new InvalidDataException();
                using var reader=new JsonTextReader(new StringReader(File.ReadAllText(journal,new UTF8Encoding(false,true)))) {DateParseHandling=DateParseHandling.None,MaxDepth=8};
                record=JObject.Load(reader,new JsonLoadSettings {DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
                if(reader.Read()||!ValidRecord(record))throw new InvalidDataException();
                // Even a previous unavailable/failed acknowledgement can follow a durable commit.
                committed=(string)record["phase"]=="review"?null:Reconcile(record);
                if(committed!=null){record["committedRevision"]=committed.Revision;record["phase"]="committed";}
                else if((string)record["phase"] is "preserving" or "activating" or "committed" or "unavailable") {
                    record["phase"]="interrupted";record["status"]="Activation is no longer current or was interrupted. Inspect the workspace; it was not replayed.";Save(record);
                }
            }catch(Exception){record=null;journalError="Activation history is unavailable. Its original files are preserved; recovery is required.";}
        }
        static readonly string[] Keys={"version","requestId","selectionRequestId","generationId","manifestHash","originRevision","retainedId","retainedHash","committedRevision","phase","status"};
        static bool Id(string value)=>value!=null&&System.Text.RegularExpressions.Regex.IsMatch(value,"^[a-f0-9]{32}$");
        internal static bool ValidRecord(JObject value)
        {
            if(value.Count!=Keys.Length||!Keys.All(value.ContainsKey)||value["version"]?.Type!=JTokenType.Integer||(int)value["version"]!=1||Keys.Skip(1).Any(k=>value[k]?.Type!=JTokenType.String||((string)value[k]).Length>256))return false;
            return Id((string)value["requestId"])&&Id((string)value["selectionRequestId"])&&Id((string)value["generationId"])&&Imports.ModelLibrary.ValidHash((string)value["manifestHash"])&&
                ((string)value["originRevision"]=="initial"||Id((string)value["originRevision"]))&&
                (((string)value["retainedId"]==""&&(string)value["retainedHash"]=="")||Id((string)value["retainedId"])&&Imports.ModelLibrary.ValidHash((string)value["retainedHash"]))&&
                ((string)value["committedRevision"]==""||Id((string)value["committedRevision"]))&&
                new[]{"preserving","activating","committed","review","unavailable","failed","cancelled","interrupted"}.Contains((string)value["phase"]);
        }
        void Save(JObject value)
        {
            if(!ValidRecord(value))throw new InvalidDataException("Invalid activation record.");
            Directory.CreateDirectory(directory);WorkspaceArchive.NoLink(directory);if(File.Exists(journal))WorkspaceArchive.NoLink(journal);
            string next=journal+"."+Guid.NewGuid().ToString("N")+".pending";
            try {var bytes=new UTF8Encoding(false,true).GetBytes(value.ToString(Formatting.None));using(var file=new FileStream(next,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}
                if(File.Exists(journal))Maestro.Quest.Persistence.FilePublication.Replace(next,journal,null);else File.Move(next,journal);
            }finally{if(File.Exists(next))File.Delete(next);}
        }
        void SaveStatus(){try{Save(record);}catch(Exception){journalError="The latest activation status could not be saved. Inspect the current workspace before another activation.";}}
        WorkspaceSelection Reconcile(JObject value)=>string.IsNullOrEmpty((string)value["retainedId"])?null:store.CommittedActivation((string)value["generationId"],(string)value["manifestHash"],(string)value["originRevision"],(string)value["retainedId"],(string)value["retainedHash"]);
        internal bool WorkerPending=>pending!=null&&!pending.IsCompleted;
        internal bool HistoryUnavailable=>journalError!=null;
        internal JToken HistorySnapshot()=>record?.DeepClone()??JValue.CreateNull();
        internal void ClearHistory(){if(Busy)throw new InvalidOperationException("History is still owned.");record=null;journalError=null;replacementFailed=false;}
        internal bool Busy=>pending!=null||hold!=null||committed!=null;
        internal bool CanStart(JObject args,out string issue)
        {
            issue=journalError;if(issue!=null)return false;
            if(disposed||paused||!focused||!host||!host.isActiveAndEnabled){issue="Resume Maestro before activating a workspace.";return false;}
            if(host.Retention?.Busy==true||host.Evidence?.Busy==true||host.History?.Busy==true||Busy||host.Retiring||host.Switching||host.Review?.Busy==true||host.Recovery?.BlocksOtherOperations==true){issue="Wait for the current activation to finish.";return false;}
            if(!host.Current||host.Selection==null){issue="Recover the current workspace before replacing it.";return false;}
            if(host.Selection.Revision!=(string)args["expectedRevision"]){issue="The workspace changed. Read workspace.current before activating.";return false;}
            if(!host.Import.MatchesOrigin((string)args["expectedRevision"])){issue="The previous-workspace preview belongs to an earlier selection. Cancel it and inspect the current previous workspace again.";return false;}
            if(!host.Import.CanActivate((string)args["selectionRequestId"],(string)args["generationId"],(string)args["manifestHash"],out issue))return false;
            try {if(!JToken.DeepEquals(store.Load().Json(),host.Selection.Json())){issue="Workspace storage differs from the live room. Recover it before activating.";return false;}}
            catch(Exception){issue="Workspace selection is unavailable. Saved data is preserved; recovery is required.";return false;}
            return WorkspaceArchiveCapture.CanStart(host.Current.Editor,host.Current.Rules,host.Current.Controls,out issue);
        }
        internal string Start(JObject args)
        {
            if(!CanStart(args,out var issue))throw new InvalidOperationException(issue);
            var content=host.Current;
            if(!WorkspaceEditHold.TryAcquire(content.Editor,content.Rules,content.Controls,out hold,out issue))throw new InvalidOperationException(issue);
            host.Import.BorrowForActivation();
            try {
                record=new JObject {["version"]=1,["requestId"]=Guid.NewGuid().ToString("N"),["selectionRequestId"]=(string)args["selectionRequestId"],["generationId"]=(string)args["generationId"],["manifestHash"]=(string)args["manifestHash"],
                    ["originRevision"]=(string)args["expectedRevision"],["retainedId"]="",["retainedHash"]="",["committedRevision"]="",["phase"]="preserving",["status"]="Preserving the accepted current workspace before activation"};
                Save(record);Fault?.Invoke("restore.beforeCapture");cancellation=new CancellationTokenSource();
                var capture=WorkspaceArchiveCapture.Start(content.Editor,content.Rules,content.Controls,Path.Combine(directory,"captures"),cancellation.Token);
                var copy=(JObject)record.DeepClone();var token=cancellation.Token;var origin=host.Selection.Json();pending=Task.Run(()=>PreserveAndCommit(capture,copy,origin,token));
                return (string)record["requestId"];
            }catch {host.Import.FinishActivation(true,false);hold.Dispose();hold=null;cancellation?.Dispose();cancellation=null;
                if(record!=null){record["phase"]="failed";record["status"]="Preservation did not start. The current workspace remains available.";SaveStatus();}throw;}
        }
        async Task<Outcome> PreserveAndCommit(Task<CapturedWorkspaceArchive> task,JObject value,JObject origin,CancellationToken token)
        {
            CapturedWorkspaceArchive capture=null;PreparedWorkspaceGeneration retained=null;WorkspaceSelection selected=null;bool reusable=false,uncertain=false;
            try {
                capture=await task.ConfigureAwait(false);token.ThrowIfCancellationRequested();
                store.InspectPrepared((string)value["generationId"],(string)value["manifestHash"],token);Fault?.Invoke("restore.beforeRetention");token.ThrowIfCancellationRequested();
                using(var source=File.OpenRead(capture.Path))retained=store.Prepare(source,token);
                value["retainedId"]=retained.Id;value["retainedHash"]=retained.Receipt.ManifestHash;value["phase"]="activating";value["status"]="The previous workspace is retained; committing the reviewed selection";
                // Exact retained identity is durable before the activation attempt. Startup only reconciles it.
                Save(value);token.ThrowIfCancellationRequested();Fault?.Invoke("restore.beforeActivation");token.ThrowIfCancellationRequested();
                selected=store.Activate((string)value["generationId"],(string)value["manifestHash"],(string)value["originRevision"],retained.Id,retained.Receipt.ManifestHash,token);
            }catch(Exception failure) {
                // A cancelled token or a post-commit I/O failure does not prove that activation failed.
                try{selected=Reconcile(value);}catch(Exception){}
                if(selected==null){value["phase"]=failure is OperationCanceledException?"cancelled":"failed";value["status"]=failure is OperationCanceledException?"Activation stopped before a confirmed switch. Inspect the current workspace; no effects were replayed.":"Activation was not confirmed. The current owners and retained files are preserved; inspect the workspace before retrying.";}
            }finally {if(capture!=null)try{File.Delete(capture.Path);}catch(Exception){} }
            if(selected!=null){value["phase"]="committed";value["committedRevision"]=selected.Revision;value["status"]="Selection committed. Opening the imported workspace under review.";}
            else {
                // Failure to read an outcome is not evidence that the old room is still selected.
                try{uncertain=!JToken.DeepEquals(store.Load().Json(),origin);}catch(Exception){uncertain=true;}
                if(uncertain){value["phase"]="unavailable";value["status"]="The selection outcome cannot be established. Editing stays held; restart or recover the workspace before continuing.";}
                else {
                    try{store.InspectPrepared((string)value["generationId"],(string)value["manifestHash"]);reusable=true;}catch(Exception){}
                    if(retained!=null)try{store.DiscardPrepared(retained.Id,retained.Receipt.ManifestHash);}catch(Exception){}
                }
            }
            // If this fails, the pre-commit record still binds the exact pair for startup reconciliation.
            try{Save(value);}catch(Exception){}
            return new Outcome {Record=value,Committed=selected,Reusable=reusable,UncertainSelection=uncertain};
        }
        internal bool CanCancel(string id,out string issue)
        {
            if(host.Retention?.Busy==true||host.Evidence?.Busy==true||host.History?.Busy==true){issue="Wait for history preservation to finish.";return false;}
            issue=null;if(record==null||(string)record["requestId"]!=id){issue="This activation request is no longer retained.";return false;}
            if(replacementFailed){issue="The selection outcome requires recovery. Cancelling cannot establish it.";return false;}
            if(committed!=null||(string)record["committedRevision"]!=""){issue="The selection is already committed. Review it or explicitly recover the previous workspace.";return false;}
            return true;
        }
        internal void Cancel(string id){if(!CanCancel(id,out var error))throw new InvalidOperationException(error);cancellation?.Cancel();}
        internal JObject Read(string id)
        {
            if(record==null||(string)record["requestId"]!=id)return null;
            var value=new JObject();foreach(var key in new[]{"requestId","selectionRequestId","originRevision","committedRevision","phase","status"})value[key]=record[key].DeepClone();
            value["preview"]=new JObject {["generationId"]=(string)record["generationId"],["manifestHash"]=(string)record["manifestHash"]};
            value["retained"]=new JObject {["generationId"]=(string)record["retainedId"],["manifestHash"]=(string)record["retainedHash"]};
            if(journalError!=null)value["status"]=journalError;return value;
        }
        internal JObject Current()=>new JObject {["revision"]=host.Selection?.Revision??"",["generationId"]=host.Selection?.Active.Generation??"",["available"]=host.Current!=null,["reviewRequired"]=host.ReviewRequired,["changing"]=host.Retention?.Busy==true||host.Evidence?.Busy==true||host.History?.Busy==true||Busy||host.Switching||host.Retiring||host.Recovery?.Busy==true,["activationRequestId"]=(string)record?["requestId"]??"",["reviewRequestId"]=host.Review?.RequestId??"",["error"]=journalError??""};
        internal void Poll()
        {
            if(disposed)return;
            if(pending!=null&&pending.IsCompleted) {
                var outcome=pending.GetAwaiter().GetResult();pending=null;record=outcome.Record;committed=outcome.Committed;
                cancellation.Dispose();cancellation=null;host.Import.FinishActivation(outcome.Reusable,committed!=null);
                if(committed==null){if(outcome.UncertainSelection)replacementFailed=true;else{hold?.Dispose();hold=null;}SaveStatus();return;}
            }
            if(committed==null||replacementFailed||paused||!focused||!host.isActiveAndEnabled||host.Switching)return;
            if(host.Current&&host.Selection?.Revision==committed.Revision){hold?.Dispose();hold=null;record["phase"]="review";record["status"]="Imported workspace opened. Review its contents before starting activity.";committed=null;SaveStatus();return;}
            if(!host.Current){hold?.Dispose();hold=null;record["phase"]="unavailable";record["status"]="The committed workspace could not open. Its previous workspace is retained for explicit recovery.";committed=null;SaveStatus();return;}
            if(host.ReplaceCommitted(committed,hold,out var error)){hold=null;return;}
            // A committed pointer must never release stale live owners for further accepted editing.
            replacementFailed=true;record["phase"]="unavailable";record["status"]="The committed selection could not replace live content. Editing stays held; restart or recover the workspace.";SaveStatus();
        }
        internal void Pause(bool value){paused=value;if(value)cancellation?.Cancel();}
        internal void Focus(bool value){focused=value;if(!value)cancellation?.Cancel();}
        internal void Disable()=>cancellation?.Cancel();
        internal async Task Dispose()
        {
            disposed=true;cancellation?.Cancel();
            // Capture the owner synchronization context. No Unity owner is released by the worker.
            try{if(pending!=null)await pending;}catch(Exception){}
            finally{hold?.Dispose();hold=null;cancellation?.Dispose();cancellation=null;}
        }
    }
}
