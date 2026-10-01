// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Imports
{
    internal interface IMotionBatchPicker
    {
        bool ReadyToStart {get;}
        void Start(string id);
        AndroidMotionBatchSource.Selection Read(string id);
        IMotionBatchSource Source(int count,string id);
        void Release(string id);
    }
    public sealed partial class ImportBatchWorkshop
    {
        IMotionBatchPicker picker;
        string sessionId="",phase="idle",errorText="";
        int version;
        IDisposable write;
        Task running;
        bool paused,focused=true,closing,stopRequested,categoryLocked;
        float selectionStarted,nextPoll;
        public bool HasSession=>write!=null||Busy;
        internal string SessionId=>sessionId;
        internal void SetPickerForTests(IMotionBatchPicker value)=>picker=value;
        void InitializeShared()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            picker=new NativeMotionBatchPicker();
#endif
            editor.RuntimeGate.Changed+=RuntimeChanged;
        }
        void RuntimeChanged(){if(editor.RuntimeGate.Held&&!picking)StopShared();}
        internal bool CanSelect(bool requirePicker,out string error)
        {
            error="Clear the current batch results before choosing files";
            if(disposed||!isActiveAndEnabled||HasSession)return false;
            if(paused||!focused){error="Resume Maestro before choosing files";return false;}
            if(!imports.CanBeginSelection(false,out error,ignoreBatch:true))return false;
            if(requirePicker)try{if(picker==null||!picker.ReadyToStart){error="The file chooser is busy or still closing";return false;}}catch(Exception){error="Resume Maestro before choosing files";return false;}
            error=null;return true;
        }
        void BeginSession(){write=editor.WriteGate.Write();sessionId=Guid.NewGuid().ToString("N");version=1;phase="selecting";errorText="";closing=false;stopRequested=false;categoryLocked=false;selectionStarted=Time.realtimeSinceStartup;}
        internal string SelectFiles()
        {
            if(!CanSelect(true,out var error))throw new InvalidOperationException(error);
            BeginSession();picking=true;
            try{picker.Start(sessionId);Say("Choose up to 128 animated GLB or VRM files");}
            catch(Exception){FailSelection("The file chooser could not open. Resume Maestro and choose again.");}
            return sessionId;
        }
        bool Adopt(IMotionBatchSource source)
        {
            try{batch=new MotionBatch(editor.Motions,source);batch.Changed+=Refresh;selected=0;page=0;category=0;phase="ready";picking=false;Refresh();return true;}
            catch(Exception){source?.Dispose();FailSelection("Choose between 1 and 128 readable animation files.");return false;}
        }
        void PollShared()
        {
            if(!picking||Time.realtimeSinceStartup<nextPoll)return;nextPoll=Time.realtimeSinceStartup+.1f;
            if(Time.realtimeSinceStartup-selectionStarted>310){FailSelection("File selection timed out. Choose files again.");return;}
            try{
                var value=picker.Read(sessionId);if(value==null)return;
                if(value.session!=sessionId){FailSelection("The file chooser returned a different batch identity");return;}
                if(value.kind!="ready"||!string.IsNullOrEmpty(value.error)){FailSelection("File selection was cancelled or unavailable. Choose files again.");return;}
                if(paused||!focused||editor.RuntimeGate.Held)return;
                if(value.count<1||value.count>MotionBatch.MaximumFiles){FailSelection("Choose between 1 and 128 animation files.");return;}
                Adopt(picker.Source(value.count,sessionId));
            }catch(Exception){FailSelection("The selected files are unavailable. Choose local files again.");}
        }
        void FailSelection(string text){errorText=Bounded(text);phase="failed";ReleaseSession();Say(errorText);}
        internal bool CanChange(string id,int expectedVersion,string operation,out string error)
        {
            error="Inspect the current batch request before changing it";
            if(disposed||!isActiveAndEnabled||id!=sessionId||string.IsNullOrEmpty(id)||!HasSession)return false;
            if(operation=="stop"){error=null;return true;}
            if(operation=="clear"){if(Busy){error="Stop the batch and wait for the current read or save to finish";return false;}error=null;return true;}
            if(expectedVersion!=version){error="The batch changed; inspect its current version";return false;}
            if(Busy||batch==null||closing){error="Wait for the current batch operation";return false;}
            if(paused||!focused){error="Resume Maestro before importing";return false;}
            if(!imports.CanBeginSelection(false,out error,ignoreBatch:true))return false;
            if(version>=1000000){error="Clear this batch and choose files again";return false;}
            if(operation=="category"&&categoryLocked){error="The category is fixed once import starts; edit saved tags in Library";return false;}
            if(operation=="start"&&batch.Pending==0||operation=="retry"&&batch.Pending+batch.Failed==0){error="No selected files are waiting for this operation";return false;}
            error=null;return true;
        }
        internal void SetCategory(string value){version++;batch.SetCategory(value);}
        internal Task StartShared(bool retry)
        {
            version++;categoryLocked=true;phase="running";errorText="";stopRequested=false;page=0;running=RunShared(retry);return running;
        }
        async Task RunShared(bool retry)
        {
            try{await Task.Yield();if(!stopRequested&&!closing&&!paused&&focused&&!editor.RuntimeGate.Held)await batch.RunAsync(retry);}
            catch(Exception){errorText="Batch import stopped unexpectedly; inspect file results before retrying.";}
            finally{
                running=null;version=Math.Min(1000000,version+1);
                if(closing||disposed||!this||!isActiveAndEnabled){phase="cleared";ReleaseSession();}
                else {phase=errorText!=""?"failed":batch.Pending>0||batch.Failed>0?"partial":"completed";imports.RefreshLibraryDetails();Refresh();}
            }
        }
        internal void StopShared()
        {
            if(picking){phase="cancelled";ReleaseSession();Say("File selection cancelled; saved motions remain");return;}
            if(running==null&&batch?.Running!=true)return;
            stopRequested=true;phase="stopping";batch?.Stop();
        }
        internal void ClearShared(){phase="cleared";ReleaseSession();Details="Animation collections\nChoose up to 128 GLB / VRM files.\nSaved motions remain in Library.";Say("Batch results cleared; original files and saved motions remain");}
        void CloseShared(){closing=true;stopRequested=true;batch?.Stop();if(running==null&&batch?.Running!=true){phase="cleared";ReleaseSession();}}
        void ReleaseSession()
        {
            picking=false;
            if(batch!=null){batch.Changed-=Refresh;batch.Dispose();batch=null;}
            try{picker?.Release(sessionId);}catch(Exception){errorText="The selected stream is still closing. Resume Maestro before choosing files.";}
            write?.Dispose();write=null;
        }
        static string Bounded(string text){var clean=new string((text??"").Where(c=>!char.IsControl(c)&&c!='<'&&c!='>').Take(128).ToArray());return clean;}
        internal JObject ObserveSession()=>new() {["requestId"]=sessionId,["version"]=version,["phase"]=phase,["category"]=batch?.Category??"",["error"]=errorText,["counts"]=new JObject {["files"]=batch?.Count??0,["saved"]=batch?.Saved??0,["failed"]=batch?.Failed??0,["waiting"]=batch?.Pending??0}};
        internal JObject ObserveFile(string id,int index,int offset)
        {
            if(id!=sessionId||batch==null||index<0||index>=batch.Count||offset<0||offset>31)return null;var item=batch.Results[index];
            return new JObject {["requestId"]=sessionId,["index"]=index,["name"]=Bounded(item.Name),["state"]=item.State.ToString().ToLowerInvariant(),["error"]=Bounded(item.Error),["motionCount"]=item.MotionIds.Length,["motionOffset"]=offset,["motionIds"]=new JArray(item.MotionIds.Skip(offset).Take(8))};
        }
        internal JObject Receipt()=>new() {["requestId"]=sessionId,["version"]=version,["phase"]=phase};
    }
#if UNITY_ANDROID && !UNITY_EDITOR
    internal sealed class NativeMotionBatchPicker:IMotionBatchPicker
    {
        public bool ReadyToStart=>AndroidMotionBatchSource.ReadyToStart;
        public void Start(string id)=>AndroidMotionBatchSource.Open(id);
        public AndroidMotionBatchSource.Selection Read(string id)=>AndroidMotionBatchSource.Poll(id);
        public IMotionBatchSource Source(int count,string id)=>new AndroidMotionBatchSource(count,id);
        public void Release(string id)=>AndroidMotionBatchSource.ClosePicker(id);
    }
#endif
}
