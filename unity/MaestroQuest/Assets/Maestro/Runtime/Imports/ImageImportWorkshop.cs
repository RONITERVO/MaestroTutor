// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Imports {
    /// <summary>One explicit selected-file owner. File bytes stay private; only bounded
    /// inspection and accepted identities cross the shared capability interface.</summary>
    public sealed class ImageImportWorkshop:MonoBehaviour {
        RoomEditor editor;IModelPicker picker;ImageAsset asset;IDisposable lease;CancellationTokenSource cancel;
        string request="",phase="idle",error="",source="";int revision;bool paused,focused=true,disposed,working;float began,nextPoll;
        ImageLibrary.Entry[] entries=Array.Empty<ImageLibrary.Entry>();bool listed;string listError="";
        internal void Initialize(RoomEditor value){editor=value;
#if UNITY_ANDROID && !UNITY_EDITOR
            try{picker=new AndroidImagePicker();}catch(Exception){error="Resume Maestro before choosing an image.";}
#endif
        }
        internal void SetPickerForTests(IModelPicker value)=>picker=value;
        internal string Request=>request;
        internal bool Ready(out string issue){
            issue="Resume Maestro before importing an image";
            if(disposed||!editor||!isActiveAndEnabled||paused||!focused)return false;
            issue="Wait for the current room operation to finish";
            if(editor.RuntimeGate.Held||editor.WriteGate.Frozen||!editor.CanSaveRoom)return false;
            issue=null;return true;
        }
        internal bool CanStart(bool choose,out string issue){
            if(!Ready(out issue))return false;
            issue="Finish or cancel the current image selection first";
            if(working||lease!=null)return false;
            issue="Release held objects and finish drawing before importing";
            if(editor.AnyHeld||editor.DrawingInProgress)return false;
            var animation=editor.GetComponent<AnimationWorkshop>();
            issue="Save or discard the current pose or recording before importing";
            if(animation&&(animation.IsPosing||animation.IsRecording||animation.HasUnsavedPose||animation.HasUnsavedRecording))return false;
            issue="The file chooser is unavailable or its previous stream is still closing";
            if(choose){try{if(picker==null||!picker.ReadyToStart)return false;}catch(Exception){return false;}}
            issue=null;return true;
        }
        void Begin(){lease=editor.WriteGate.Write();cancel=new();request=Guid.NewGuid().ToString("N");error="";source="";revision=0;asset=null;began=Time.realtimeSinceStartup;}
        internal string Select(){
            if(!CanStart(true,out var issue))throw new InvalidOperationException(issue);Begin();phase="selecting";
            try{picker.Start(request);}catch(Exception){Fail("The image chooser could not open. Resume Maestro and try again.");ReleasePicker(request);End();}
            return request;
        }
        internal bool CanAccept(string id,string hash,out string issue){
            if(!Ready(out issue))return false;issue="Inspect the exact current image preview before accepting it";
            if(id!=request||working||phase!="preview"||asset==null||asset.Hash!=hash)return false;
            issue="The room image-definition limit is full";if(editor.Appearances().Length>=RoomAppearance.MaximumDefinitions)return false;
            string candidate=Guid.NewGuid().ToString("N");return editor.PrepareAppearance(new RoomAppearance{id=candidate,name=asset.Name,style=new AppearanceStyle{patternMode="image",imageHash=asset.Hash}},candidate,0,Array.Empty<string>(),out _,out issue);
        }
        internal bool CanCancel(string id,out string issue){
            issue="The image request changed or has already finished";
            if(id!=request||lease==null||phase=="completed")return false;
            issue="Accepted image writes must finish; inspect their result before another selection";
            if(phase=="accepting")return false;issue=null;return true;
        }
        internal void Cancel(string id){
            if(!CanCancel(id,out var issue))throw new InvalidOperationException(issue);cancel.Cancel();phase=working?"cancelling":"cancelled";
            if(!working){ReleasePicker(request);asset=null;End();}
        }
        void Update(){
            if(phase!="selecting"&&phase!="copying")return;
            if(Time.realtimeSinceStartup-began>310){Cancel(request);return;}
            if(Time.realtimeSinceStartup<nextPoll)return;nextPoll=Time.realtimeSinceStartup+.1f;
            try {
                var value=picker.Read(request);if(value==null)return;
                if((string)value["id"]!=request)throw new InvalidOperationException();
                string status=(string)value["phase"];
                if(status is "opening" or "selecting" or "copying"){phase=status=="copying"?status:"selecting";return;}
                if(status=="cancelled"){Cancel(request);return;}
                if(status=="failed"){Fail("The selected image could not be copied. Choose a local PNG/JPEG file.");ReleasePicker(request);End();return;}
                if(status!="selected")throw new InvalidOperationException();
                if(paused||!focused)return;
                string path=ImportWorkshop.SelectedPath((string)value["path"],picker.CacheRoot),name=(string)value["name"];
                _=Prepare(path,name,request,cancel.Token);
            }catch(Exception){Fail("The image selection could not be read. Choose the file again.");ReleasePicker(request);End();}
        }
        async Task Prepare(string path,string name,string id,CancellationToken token){
            working=true;phase="checking";
            try{var selected=await Task.Run(()=>ImageLibrary.ReadSelected(name,path,token),token);token.ThrowIfCancellationRequested();if(disposed)return;var verified=Art.AppearanceImages.Decode(selected);Art.ArtResources.Release(verified);token.ThrowIfCancellationRequested();asset=selected;phase="preview";}
            catch(OperationCanceledException){phase="cancelled";asset=null;}
            catch(Exception ex){Fail(ex is System.IO.InvalidDataException?ex.Message:"The selected PNG/JPEG could not be read. Choose it again.");}
            finally{ReleasePicker(id);working=false;if(disposed||phase!="preview")End();}
        }
        internal async Task<JObject> Accept(string id,string hash,CancellationToken stop){
            if(!CanAccept(id,hash,out var issue))throw new InvalidOperationException(issue);
            working=true;phase="accepting";using var linked=CancellationTokenSource.CreateLinkedTokenSource(stop,cancel.Token);
            var chosen=asset;
            try {
                await editor.Images.SaveAsync(chosen,linked.Token);linked.Token.ThrowIfCancellationRequested();
                if(!Ready(out issue))throw new InvalidOperationException(issue);
                var definition=new RoomAppearance {id=Guid.NewGuid().ToString("N"),name=chosen.Name,style=new AppearanceStyle{patternMode="image",imageHash=chosen.Hash}};
                if(!editor.EditAppearance(definition,definition.id,0,Array.Empty<string>(),out issue))throw new InvalidOperationException(issue);
                source=definition.id;revision=editor.AppearanceRevision(source);phase="completed";listed=false;editor.ImageTextures.Retry(chosen.Hash);return Receipt();
            }catch(OperationCanceledException){phase="cancelled";error="Image acceptance stopped. Its private file may remain; inspect the library. No object was bound.";throw;}
            catch(Exception){Fail("Image acceptance did not finish. Its private file may remain; refresh the library before retrying. No object was bound.");throw;}
            finally{working=false;End();}
        }
        internal async Task<JObject> Refresh(CancellationToken stop){
            if(!CanStart(false,out var issue))throw new InvalidOperationException(issue);Begin();phase="checking";working=true;listed=false;
            using var linked=CancellationTokenSource.CreateLinkedTokenSource(stop,cancel.Token);
            try{var fresh=await editor.Images.ListAsync(linked.Token);linked.Token.ThrowIfCancellationRequested();if(disposed)throw new OperationCanceledException();entries=fresh;foreach(var entry in fresh)editor.ImageTextures.Retry(entry.Hash);listed=true;listError="";phase="completed";return Receipt();}
            catch(OperationCanceledException){phase="cancelled";listError="Image-library refresh was cancelled.";throw;}
            catch(Exception){listError="Image-library inspection failed. Existing files are preserved; repair or reimport the damaged image.";Fail(listError);throw;}
            finally{working=false;End();}
        }
        internal JObject Receipt()=>new(){["requestId"]=request,["imageHash"]=asset?.Hash??"",["appearanceId"]=source,["revision"]=revision,["temporary"]=editor&&editor.TemporaryRoom,["library"]=Library(0,1)};
        internal JObject Observe()=>new(){["requestId"]=request,["phase"]=phase,["error"]=ImportObservation.Text(error),["preview"]=new JObject {["imageHash"]=asset?.Hash??"",["name"]=ImportObservation.Text(asset?.Name??""),["width"]=asset?.Inspection.DisplayWidth??0,["height"]=asset?.Inspection.DisplayHeight??0,["format"]=asset?.Inspection.Format??"",["kibibytes"]=(asset?.Content.Length??0)/1024d},["appearanceId"]=source,["revision"]=revision};
        internal JObject Library(int offset,int pageSize=2){
            var page=listed?entries.Skip(offset).Take(pageSize).ToArray():Array.Empty<ImageLibrary.Entry>();
            return new JObject {["ready"]=listed,["error"]=ImportObservation.Text(listError),["total"]=listed?entries.Length:0,["next"]=listed&&offset+page.Length<entries.Length?offset+page.Length:-1,["entries"]=new JArray(page.Select(e=>new JObject {["id"]=e.Hash,["name"]=ImportObservation.Text(e.Name),["width"]=e.Width,["height"]=e.Height,["format"]=e.Format}))};
        }
        void Fail(string value){phase="failed";error=ImportObservation.Text(value);}
        void ReleasePicker(string id){try{picker?.Release(id);}catch(Exception){error="The selected stream is still closing. Resume Maestro before choosing another.";}}
        void End(){lease?.Dispose();lease=null;cancel?.Dispose();cancel=null;}
        void Close(){disposed=true;cancel?.Cancel();if(working)return;ReleasePicker(request);if(lease!=null)phase="cancelled";End();asset=null;}
        void OnEnable()=>disposed=false;
        void OnApplicationPause(bool value)=>paused=value;
        void OnApplicationFocus(bool value)=>focused=value;
        void OnDisable()=>Close();
        void OnDestroy()=>Close();
    }
#if UNITY_ANDROID && !UNITY_EDITOR
    internal sealed class AndroidImagePicker:IModelPicker {
        public string CacheRoot {get;}
        public bool ReadyToStart {get{using var type=new AndroidJavaClass("com.maestro.quest.browser.ImagePicker");return type.CallStatic<bool>("ReadyToStart");}}
        public AndroidImagePicker(){using var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer");using var activity=player.GetStatic<AndroidJavaObject>("currentActivity");using var type=new AndroidJavaClass("com.maestro.quest.browser.ImagePicker");CacheRoot=type.CallStatic<string>("CacheRoot",activity);}
        public void Start(string id){using var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer");using var activity=player.GetStatic<AndroidJavaObject>("currentActivity");using var type=new AndroidJavaClass("com.maestro.quest.browser.ImagePicker");type.CallStatic<string>("Start",activity,id);}
        public JObject Read(string id){using var type=new AndroidJavaClass("com.maestro.quest.browser.ImagePicker");string value=type.CallStatic<string>("Read",id);return string.IsNullOrEmpty(value)?null:JObject.Parse(value);}
        public void Release(string id){using var type=new AndroidJavaClass("com.maestro.quest.browser.ImagePicker");type.CallStatic("Release",id);}
    }
#endif
}
