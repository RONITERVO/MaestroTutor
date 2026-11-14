// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Maestro.Quest.Persistence
{
    internal interface IWorkspaceArchivePicker
    {
        string CacheRoot {get;}
        bool ReadyToStart {get;}
        void Start(string requestId);
        JObject Read(string requestId);
        // May run on a worker during owner teardown; implementations must be thread-safe.
        void Release(string requestId);
    }
    /// <summary>Selection/preparation owner, separate from an action's lifetime. A system picker pause
    /// cancels running actions, never restarts them, and never implies permission to activate a restore.</summary>
    public sealed class WorkspaceImport:MonoBehaviour
    {
        IWorkspaceArchivePicker picker;
        WorkspaceGenerationStore store;
        enum SelectionSource { File,Previous,Bundled }
        SelectionSource source;
        string requestId,phase="idle",name="",error="",releasePending,originRevision;
        WorldPackCatalog.Entry[] packs=Array.Empty<WorldPackCatalog.Entry>();
        JObject packPreview;
        sealed class PreparationResult {
            internal readonly PreparedWorkspaceGeneration Generation;internal readonly JObject PackPreview;
            internal PreparationResult(PreparedWorkspaceGeneration generation,JObject preview=null){Generation=generation;PackPreview=preview;}
        }
        string unavailable="Workspace selection requires the Quest app.";
        Task<PreparationResult> preparation;
        Task discard,retirement;
        PreparedWorkspaceGeneration prepared;
        CancellationTokenSource cancellation;
        bool cancelRequested,paused,focused=true,activationOwned;
        internal Action<string> Fault;
        float nextPoll;
        public bool Available=>picker!=null;
        internal bool SelectionAvailable=>store!=null;
        internal bool PacksAvailable=>store!=null&&packs.Length>0;
        void CapturePacks(){try{packs=WorldPackCatalog.Available.ToArray();}catch(Exception){packs=Array.Empty<WorldPackCatalog.Entry>();}}
        public void Initialize(string applicationData)
        {
            store=new WorkspaceGenerationStore(applicationData,point=>Fault?.Invoke(point));CapturePacks();
#if UNITY_ANDROID && !UNITY_EDITOR
            try{picker=new AndroidWorkspacePicker();unavailable=null;}catch(Exception){unavailable="Resume Maestro before choosing a workspace archive.";}
#endif
        }
        internal void InitializeForTests(string applicationData,IWorkspaceArchivePicker source)
        {store=new WorkspaceGenerationStore(applicationData,point=>Fault?.Invoke(point));picker=source;unavailable=null;CapturePacks();}
        internal bool Occupied=>preparation!=null||discard!=null||releasePending!=null||prepared!=null||activationOwned||phase is "selecting" or "copying" or "preparing" or "cancelling";
        internal bool StableForRecovery=>retirement==null&&preparation==null&&discard==null&&releasePending==null&&!activationOwned&&phase is not ("selecting" or "copying" or "preparing" or "cancelling");
        internal bool CanRecoverCandidate(string generation,string hash)=>StableForRecovery&&(prepared==null||phase=="prepared"&&prepared.Id==generation&&prepared.Receipt.ManifestHash==hash);
        internal bool BorrowForRecovery(string generation,string hash)
        {
            if(!CanRecoverCandidate(generation,hash))throw new InvalidOperationException("Finish the selected archive before preparing recovery.");
            if(prepared==null)return false;activationOwned=true;phase="recovering";return true;
        }
        internal void FinishRecovery(){activationOwned=false;prepared=null;phase="retained";error="Archive retained as a recovery candidate. Read workspace.recovery for the prepared copy.";}
        public bool CanSelect(out string issue)
        {
            if(GetComponent<WorkspaceHost>()?.Retention?.Busy==true||GetComponent<WorkspaceHost>()?.Evidence?.Busy==true||GetComponent<WorkspaceHost>()?.History?.Busy==true){issue="Wait for history preservation to finish.";return false;}
            issue=unavailable;if(retirement!=null){issue="The previous workspace selection owner is closing.";return false;}
            if(GetComponent<WorkspaceHost>()?.Recovery?.BlocksOtherOperations==true){issue="Finish or cancel workspace recovery before selecting an archive.";return false;}
            if(!Available){issue??="Workspace selection is unavailable.";return false;}
            if(preparation!=null||discard!=null||releasePending!=null||prepared!=null||phase is "selecting" or "copying" or "preparing" or "cancelling"){
                issue="Finish or cancel the current archive selection before choosing another.";return false;
            }
            try {if(!picker.ReadyToStart){issue="Wait for the previous selected file to close before choosing another.";return false;}}
            catch(Exception){issue="The file chooser is unavailable. Resume Maestro before choosing an archive.";return false;}
            issue=null;return true;
        }
        bool Idle(out string issue)
        {
            issue=null;if(retirement!=null||preparation!=null||discard!=null||releasePending!=null||prepared!=null||activationOwned||phase is "selecting" or "copying" or "preparing" or "cancelling") {issue="Finish or cancel the current workspace selection first.";return false;}return true;
        }
        internal JObject ReadPrevious()
        {
            var result=new JObject {["revision"]="",["generationId"]="",["manifestHash"]="",["available"]=false,["error"]=""};
            try {var value=store?.Previous();if(value==null){result["error"]="There is no verified previous workspace.";return result;}result["revision"]=value.Revision;result["generationId"]=value.Generation;result["manifestHash"]=value.ManifestHash;result["available"]=true;}
            catch(Exception){result["error"]="Previous workspace metadata is unavailable. Its files are preserved; explicit recovery is required.";}return result;
        }
        internal bool CanSelectPrevious(JObject args,out string issue)
        {
            issue=null;if(store==null||paused||!focused||!isActiveAndEnabled){issue="Resume Maestro before inspecting its previous workspace.";return false;}
            if(GetComponent<WorkspaceHost>()?.Retention?.Busy==true||GetComponent<WorkspaceHost>()?.Evidence?.Busy==true||GetComponent<WorkspaceHost>()?.History?.Busy==true){issue="Wait for history preservation to finish.";return false;}
            if(!Idle(out issue))return false;
            try {var value=store.Previous();if(value==null||value.Revision!=(string)args["expectedRevision"]||value.Generation!=(string)args["generationId"]||value.ManifestHash!=(string)args["manifestHash"]){issue="The previous workspace identity changed. Read workspace.previous again.";return false;}}
            catch(Exception){issue="Previous workspace metadata is unavailable. Original files are preserved.";return false;}
            return true;
        }
        internal string SelectPrevious(JObject args)
        {
            if(!CanSelectPrevious(args,out var issue))throw new InvalidOperationException(issue);
            requestId=Guid.NewGuid().ToString("N");phase="preparing";name="Previous workspace";error="";cancelRequested=false;source=SelectionSource.Previous;originRevision=(string)args["expectedRevision"];packPreview=null;
            cancellation=new CancellationTokenSource(TimeSpan.FromMinutes(10));var token=cancellation.Token;var owner=store;string revision=(string)args["expectedRevision"],generation=(string)args["generationId"],hash=(string)args["manifestHash"];
            preparation=Task.Run(()=>new PreparationResult(owner.PreparePrevious(revision,generation,hash,token)));return requestId;
        }
        internal JObject ReadPacks(int offset)
        {
            if(!SelectionAvailable||offset<0||offset>32)return null;
            var entries=new JArray();if(offset<packs.Length){var entry=packs[offset];entries.Add(new JObject {
                ["id"]=entry.Id,["version"]=entry.Version,["hash"]=entry.Hash,["name"]=entry.Name,["entryId"]=entry.EntryId});}
            return new JObject {["offset"]=offset,["total"]=packs.Length,["next"]=offset+1<packs.Length?offset+1:-1,["entries"]=entries};
        }
        internal bool CanSelectPack(JObject args,out string issue)
        {
            issue="Resume Maestro before preparing a bundled world.";
            var host=GetComponent<WorkspaceHost>();
            if(store==null||paused||!focused||!isActiveAndEnabled||!host||!host.isActiveAndEnabled||!host.Ready||!host.Current||host.Selection==null)return false;
            if(host.Switching||host.Retiring||host.Activation?.Busy==true||host.Review?.Busy==true||host.Recovery?.BlocksOtherOperations==true||
                host.Retention?.Busy==true||host.Evidence?.Busy==true||host.History?.Busy==true){issue="Finish the current workspace operation before preparing a bundled world.";return false;}
            if(host.Export?.Busy==true||host.Activation?.HistoryUnavailable==true||host.Review?.HistoryUnavailable==true||host.Recovery?.HistoryUnavailable==true){issue="Finish export or recover unavailable workspace history before preparing another world.";return false;}
            if(host.Selection.Revision!=(string)args["expectedRevision"]){issue="The workspace changed. Read workspace.current before preparing a world.";return false;}
            if(!Idle(out issue))return false;
            if(!packs.Any(entry=>entry.Id==(string)args["packId"]&&entry.Version==(int)args["packVersion"]&&entry.Hash==(string)args["packHash"])){
                issue="That exact bundled world is unavailable. Read workspace.packs again.";return false;
            }
            try{if(!JToken.DeepEquals(store.Load().Json(),host.Selection.Json())){issue="Workspace storage differs from the live room. Recover it before preparing another world.";return false;}}
            catch(Exception){issue="Workspace selection is unavailable. Saved data is preserved; recovery is required.";return false;}
            var content=host.Current;
            if(!WorkspaceArchiveCapture.CanStart(content.Editor,content.Rules,content.Controls,out issue)||
                !content.Editor.WriteGate.CanFreeze(out issue)||!content.Editor.CanChangeTemporaryBoundary(out issue))return false;
            if(content.Rules.Runtime&&content.Rules.Runtime.AnyButtonHeld){issue="Release action buttons before preparing another world.";return false;}
            return true;
        }
        internal string SelectPack(JObject args)
        {
            if(!CanSelectPack(args,out var issue))throw new InvalidOperationException(issue);
            var entry=packs.Single(value=>value.Id==(string)args["packId"]&&value.Version==(int)args["packVersion"]&&value.Hash==(string)args["packHash"]);
            var host=GetComponent<WorkspaceHost>();var avatar=host.IncludedAvatar;var motions=host.IncludedMotions;var owner=store;
            requestId=Guid.NewGuid().ToString("N");phase="preparing";name=entry.Name;error="";cancelRequested=false;source=SelectionSource.Bundled;
            originRevision=(string)args["expectedRevision"];packPreview=null;
            string id=requestId,origin=originRevision;cancellation=new CancellationTokenSource(TimeSpan.FromMinutes(10));var token=cancellation.Token;
            preparation=Task.Run(()=>{
                token.ThrowIfCancellationRequested();
                var result=WorldPackPreparation.Prepare(entry,WorkspaceDefaults.Snapshot(avatar,motions),token);token.ThrowIfCancellationRequested();
                var snapshot=result.Snapshot;WorldPackPreparation.RequireClosed(snapshot.Metadata.Summary);
                var provenance=WorldPackDocument.Decode(snapshot.Documents[WorldPackDocument.FileName]);var instance=provenance.instances.Single();
                var staged=owner.PrepareSnapshot(snapshot,token);
                var preview=new JObject {["requestId"]=id,["packId"]=entry.Id,["packVersion"]=entry.Version,["packHash"]=entry.Hash,["entryId"]=entry.EntryId,
                    ["originRevision"]=origin,["worldId"]=provenance.worldId,["instanceId"]=instance.instanceId,["objects"]=instance.objects.Length,["resources"]=instance.resources.Length,["recipeParts"]=result.Summary.RecipeParts,["collisionPieces"]=result.Summary.CollisionPieces,["customGeometryVertices"]=result.Summary.CustomGeometryVertices,["files"]=staged.Receipt.Summary.Files,["bytes"]=staged.Receipt.Summary.Bytes};
                return new PreparationResult(staged,preview);
            });return requestId;
        }
        internal JObject ReadPackPreview(string id)=>retirement==null&&source==SelectionSource.Bundled&&phase=="prepared"&&prepared!=null&&id==requestId?packPreview?.DeepClone() as JObject:null;
        internal string Select()
        {
            if(!CanSelect(out var issue))throw new InvalidOperationException(issue);
            requestId=Guid.NewGuid().ToString("N");phase="selecting";name="";error="";cancelRequested=false;source=SelectionSource.File;originRevision=null;packPreview=null;
            try{picker.Start(requestId);}catch(Exception){phase="failed";error="The file chooser could not open. Resume Maestro and choose the archive again.";Release();}
            // This ID acknowledges a tracked request, not a successful choice or import.
            return requestId;
        }
        internal bool CanCancel(string id,out string issue)
        {
            issue=null;
            if((!Available&&source==SelectionSource.File)||id!=requestId){issue="This archive request is no longer available. Inspect the current request.";return false;}
            if(activationOwned){issue="Cancel the tracked workspace operation before changing its archive selection.";return false;}
            return true;
        }
        internal void Cancel(string id)
        {
            if(!CanCancel(id,out var issue))throw new InvalidOperationException(issue);
            cancelRequested=true;cancellation?.Cancel();
            if(discard!=null)return;
            if(preparation!=null){phase="cancelling";return;}
            if(prepared!=null){Discard();return;}
            phase="cancelled";error="";Release();
        }
        internal bool MatchesOrigin(string revision)=>source==SelectionSource.File||originRevision==revision;
        internal bool CanActivate(string id,string generation,string hash,out string issue)
        {
            issue="Inspect the current prepared archive before activating it.";
            if(retirement!=null||activationOwned||phase!="prepared"||prepared==null||preparation!=null||discard!=null||releasePending!=null||id!=requestId||generation!=prepared.Id||hash!=prepared.Receipt.ManifestHash)return false;
            issue=null;return true;
        }
        internal void BorrowForActivation(){activationOwned=true;phase="activating";}
        internal void FinishActivation(bool reusable,bool committed)
        {
            activationOwned=false;
            if(reusable){phase="prepared";return;}
            prepared=null;phase=committed?"activated":"retained";
            error=committed?"":"The attempted generations are retained. Choose the archive again before another activation.";
        }
        void Discard()
        {
            var value=prepared;var owner=store;phase="cancelling";
            discard=Task.Run(()=>owner.DiscardPrepared(value.Id,value.Receipt.ManifestHash));
        }
        void Release()
        {
            if(source!=SelectionSource.File||picker==null||requestId==null)return;
            try{picker.Release(requestId);releasePending=null;}
            catch(Exception){releasePending=requestId;error="Selection cleanup is pending. Resume Maestro before choosing another archive.";}
        }
        // A read exposes only bounded semantic status; no URI, cache path or file access token enters the book/agent.
        internal JObject ReadSelection(string id)
        {
            if(id==null||id!=requestId)return null;
            var summary=prepared?.Receipt.Summary;
            return new JObject {["requestId"]=requestId,["phase"]=phase,["name"]=SafeText(name,48),["error"]=SafeText(releasePending!=null?"Selection cleanup is pending. Resume Maestro before choosing another archive.":error,64),
                ["generationId"]=prepared?.Id??"",["manifestHash"]=prepared?.Receipt.ManifestHash??"",
                ["summary"]=new JObject {["files"]=summary?.Files??0,["models"]=summary?.Models??0,["images"]=summary?.Images??0,["sounds"]=summary?.Sounds??0,["motions"]=summary?.Motions??0,["modules"]=summary?.Modules??0,
                    ["unavailablePrograms"]=summary?.UnavailablePrograms??0,["missingImages"]=summary?.MissingImages.Length??0,["missingSounds"]=summary?.MissingSounds.Length??0,["missingModels"]=summary?.MissingModels.Length??0,["missingMotions"]=summary?.MissingMotions.Length??0,["missingControllerPrograms"]=summary?.MissingControllerPrograms.Length??0}};
        }
        internal void Poll()
        {
            if(retirement!=null||requestId==null||picker==null&&source==SelectionSource.File)return;
            if(releasePending!=null)Release();
            if(discard!=null&&discard.IsCompleted){
                var failure=discard.Exception?.GetBaseException();discard=null;
                if(failure==null){prepared=null;phase="cancelled";error="";}
                else {phase="failed";error="The prepared archive could not be discarded. It is retained; inspect storage and cancel again.";}
            }
            if(preparation!=null){
                if(!preparation.IsCompleted)return;
                var task=preparation;preparation=null;Release();
                if(task.Status==TaskStatus.RanToCompletion){prepared=task.Result.Generation;packPreview=task.Result.PackPreview;if(cancelRequested)Discard();else{phase="prepared";error="";}}
                else {phase=cancelRequested?"cancelled":"failed";error=cancelRequested?"":task.IsCanceled?"Archive preparation was interrupted. Choose the archive again.":PreparationError(task.Exception?.GetBaseException());}
                cancellation?.Dispose();cancellation=null;return;
            }
            if(phase is not ("selecting" or "copying"))return;
            JObject result;
            try{result=picker.Read(requestId);}catch(Exception){phase="failed";error="Archive selection status is unavailable. Resume Maestro and choose again.";Release();return;}
            if(result==null||result.Count!=5||!new[]{"id","phase","name","path","error"}.All(key=>result[key]?.Type==JTokenType.String)||result.Properties().Any(p=>((string)p.Value).Length>2048)||(string)result["id"]!=requestId){phase="failed";error="Archive selection was interrupted. Choose the archive again.";Release();return;}
            string state=(string)result["phase"];
            if(state is "opening" or "selecting" or "copying"){phase=state=="copying"?"copying":"selecting";return;}
            if(state=="cancelled"||state=="failed"){
                phase=state;error=state=="cancelled"?"":SafeText((string)result["error"],256);Release();return;
            }
            if(state!="selected"){phase="failed";error="The file chooser returned an unknown state.";Release();return;}
            name=SafeText((string)result["name"],120);
            var path=(string)result["path"];var cacheRoot=picker.CacheRoot;var ownerStore=store;
            cancellation=new CancellationTokenSource(TimeSpan.FromMinutes(10));var token=cancellation.Token;
            phase="preparing";
            preparation=Task.Run(()=>{
                token.ThrowIfCancellationRequested();string file=PrivateCopy(path,cacheRoot);
                using var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read);
                if(stream.Length<1||stream.Length>WorkspaceArchive.MaximumArchiveBytes)throw new InvalidDataException("Choose a workspace ZIP no larger than 512 MB.");
                return new PreparationResult(ownerStore.PrepareImport(stream,token));
            });
        }
        static string SafeText(string value,int maximum)
        {value=new string((value??"").Where(c=>!char.IsControl(c)).Select(c=>char.IsSurrogate(c)||c=='\u2028'||c=='\u2029'?'_':c).Take(maximum).ToArray());return value;}
        static string PreparationError(Exception issue)
        {if(issue!=null)Debug.LogWarning($"Maestro workspace preparation failed ({issue.GetType().Name}, 0x{issue.HResult:X8})");return issue is InvalidDataException?SafeText(issue.Message,256):"The archive could not be prepared. Current room data is unchanged; check the file and available storage.";}
        static string PrivateCopy(string path,string root)
        {
            if(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(root)||!Path.IsPathRooted(path))throw new InvalidDataException("No private archive copy was selected.");
            string file=Path.GetFullPath(path),parent=Path.GetDirectoryName(file),cache=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            if(Path.GetDirectoryName(parent)!=cache||!Guid.TryParseExact(Path.GetFileName(parent),"D",out _)||!Guid.TryParseExact(Path.GetFileName(file),"D",out _))throw new InvalidDataException("The archive copy is outside this selection session.");
            WorkspaceArchive.NoLink(cache);WorkspaceArchive.NoLink(parent);WorkspaceArchive.NoLink(file);return file;
        }
        void Update(){if(paused||!focused||Time.unscaledTime<nextPoll)return;nextPoll=Time.unscaledTime+.2f;Poll();}
        void OnApplicationPause(bool value)=>paused=value;
        void OnApplicationFocus(bool value)=>focused=value;
        internal Task Retire()
        {
            if(retirement!=null)return retirement;
            cancellation?.Cancel();var task=preparation;var remove=discard;var backend=source==SelectionSource.File?picker:null;var id=requestId;var owner=store;var tokenOwner=cancellation;
            // No scene owner or Unity API is touched by late cleanup. Keep already prepared previews
            // retained, but never leak a newly completed preparation whose owner was destroyed.
            retirement=Task.Run(async()=>{
                try {
                    if(task!=null) {PreparedWorkspaceGeneration abandoned=null;try{abandoned=(await task.ConfigureAwait(false)).Generation;}catch(Exception){}
                        if(abandoned!=null)owner.DiscardPrepared(abandoned.Id,abandoned.Receipt.ManifestHash);}
                    if(remove!=null)await remove.ConfigureAwait(false);
                }catch(Exception){}finally{try{if(id!=null)backend?.Release(id);}catch(Exception){}tokenOwner?.Dispose();}
            });return retirement;
        }
        void OnDestroy()=>_=Retire();
    }
}
