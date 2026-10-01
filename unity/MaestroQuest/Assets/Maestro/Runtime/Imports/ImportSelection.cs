// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Persistence;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Imports
{
    internal interface IModelPicker
    {
        string CacheRoot {get;}
        bool ReadyToStart {get;}
        void Start(string id);
        JObject Read(string id);
        void Release(string id);
    }
    public sealed partial class ImportWorkshop
    {
        IModelPicker modelPicker;
        string selectionId="",selectionPhase="idle",selectionError="";
        JObject selectionInfo;
        IDisposable selectionWrite;
        CancellationTokenSource selectionCancel;
        bool preparingSelection,selectionPaused,selectionFocused=true;
        float selectionBegan,nextSelectionPoll;
        public string SelectionRequestId=>selectionId;
        void InitializeSelection()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try{modelPicker=new AndroidModelPicker();}catch(Exception){Say("Resume Maestro before choosing a model.");}
#endif
        }
        internal void SetPickerForTests(IModelPicker value)=>modelPicker=value;
        internal bool CanBeginSelection(bool requirePicker,out string error,bool ignoreBatch=false)
        {
            error=null;
            if(!editor||disposed||!isActiveAndEnabled||selectionPaused||!selectionFocused)error="Resume Maestro before importing a model";
            else if(editor.RuntimeGate.Held)error=editor.RuntimeGate.Reason;
            else if(editor.WriteGate.Frozen||!editor.CanSaveRoom)error="Room saving is unavailable";
            else if(busy||picking||HasPreview||selectionWrite!=null||!ignoreBatch&&Batches&&Batches.HasSession)error="Finish or cancel the current import first";
            else if(editor.AnyHeld||editor.DrawingInProgress)error="Release objects and finish drawing before opening the picker";
            else if(animationWorkshop&&(animationWorkshop.IsPosing||animationWorkshop.IsRecording||animationWorkshop.HasUnsavedPose||animationWorkshop.HasUnsavedRecording))error="Finish authoring and save or discard the retained pose or take first";
            else if(requirePicker)try{if(modelPicker==null||!modelPicker.ReadyToStart)error="The file chooser is unavailable or its previous stream is still closing";}catch(Exception){error="Resume Maestro before choosing a model";}
            return error==null;
        }
        void BeginSelectionOwner()
        {
            selectionWrite=editor.WriteGate.Write();selectionId=Guid.NewGuid().ToString("N");selectionPhase="selecting";selectionInfo=null;selectionResult=null;selectionError="";
            selectionCancel=new CancellationTokenSource();selectionBegan=Time.realtimeSinceStartup;
        }
        internal string BeginSelection()
        {
            if(!CanBeginSelection(true,out var error))throw new InvalidOperationException(error);
            BeginSelectionOwner();picking=true;
            try{modelPicker.Start(selectionId);Say("Choose one GLB or VRM file in the document picker");}
            catch(Exception){SelectionFailed("The document picker could not open. Resume Maestro and try again.");ReleaseSelectionPicker(selectionId);EndSelectionOwner();}
            return selectionId;
        }
        void PollSelection()
        {
            if(!picking||Time.realtimeSinceStartup<nextSelectionPoll)return;nextSelectionPoll=Time.realtimeSinceStartup+.1f;
            if(Time.realtimeSinceStartup-selectionBegan>310){CancelSelection(selectionId);return;}
            try{
                var result=modelPicker.Read(selectionId);if(result==null)return;
                if((string)result["id"]!=selectionId)throw new ModelImportException("The model chooser returned a different request identity");
                string phase=(string)result["phase"];
                if(phase is "opening" or "selecting" or "copying"){selectionPhase=phase=="opening"?"selecting":phase;return;}
                if(phase=="cancelled"){CancelSelection(selectionId);return;}
                if(phase=="failed"){SelectionFailed("The selected model could not be copied. Choose a local GLB or VRM of 64 MB or less.");ReleaseSelectionPicker(selectionId);EndSelectionOwner();return;}
                if(phase!="selected")throw new ModelImportException("The model chooser returned an invalid state");
                if(selectionPaused||!selectionFocused)return;
                string path=SelectedPath((string)result["path"],modelPicker.CacheRoot),name=ModelLibrary.SafeName((string)result["name"]);
                _=PrepareSelectionAsync(()=>Task.Run(()=>ModelLibrary.Inspect(name,ModelLibrary.ReadBounded(path))),selectionId,true);
            }catch(Exception){SelectionFailed("The selected model could not be read. Choose the file again.");ReleaseSelectionPicker(selectionId);EndSelectionOwner();}
        }
        internal static string SelectedPath(string value,string root)
        {
            if(string.IsNullOrEmpty(value)||string.IsNullOrEmpty(root)||!Path.IsPathRooted(value))throw new IOException("Missing selected copy");
            string path=Path.GetFullPath(value),parent=Path.GetDirectoryName(path),cache=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            if(Path.GetDirectoryName(parent)!=cache||!Guid.TryParseExact(Path.GetFileName(parent),"D",out _)||!Guid.TryParseExact(Path.GetFileName(path),"D",out _))throw new IOException("Not a private selected copy");
            WorkspaceArchive.NoLink(cache);WorkspaceArchive.NoLink(parent);WorkspaceArchive.NoLink(path);return path;
        }
        async Task PrepareSelectionAsync(Func<Task<ModelAsset>> read,string id,bool releasePicker)
        {
            picking=false;busy=true;preparingSelection=true;selectionPhase="checking";var token=selectionCancel.Token;Say("Checking selected model…");
            try{
                var asset=await read();token.ThrowIfCancellationRequested();if(!this||disposed||id!=selectionId)return;
                selectionInfo=new JObject {["modelHash"]=asset.Hash,["name"]=asset.Name,["kibibytes"]=Math.Ceiling(asset.Bytes.Length/1024d),["vertices"]=asset.Inspection.Vertices,["triangles"]=asset.Inspection.Triangles,["clips"]=asset.Inspection.Clips,["humanoid"]=false};
                await PreviewAsync(asset);token.ThrowIfCancellationRequested();if(!this||disposed||id!=selectionId)return;
                if(!HasPreview)throw new ModelImportException("The model preview did not finish loading");
                selectionInfo["humanoid"]=preview.IsHumanoid;selectionPhase="preview";
            }catch(OperationCanceledException){selectionPhase="cancelled";ClearPreview();Say("Import cancelled — no model was added");}
            catch(Exception ex){SelectionFailed(ex is ModelImportException?ex.Message:"The model could not be imported. Try a self-contained GLB or VRM.");ClearPreview();}
            finally{
                // The selected bytes belong to the worker until reading/loading drains.
                if(releasePicker)ReleaseSelectionPicker(id);
                preparingSelection=false;busy=false;
                if(disposed||selectionPhase!="preview")EndSelectionOwner();
            }
        }
        internal bool CanCancelSelection(string id,out string error)
        {
            error="The import request changed; inspect model.import.selection first";
            if(string.IsNullOrEmpty(id)||id!=selectionId||selectionPhase is "completed" or "consumed")return false;
            if(busy&&!preparingSelection||Batches&&Batches.Busy){error="Wait for the accepted import to finish saving";return false;}
            error=null;return true;
        }
        internal void CancelSelection(string id)
        {
            if(!CanCancelSelection(id,out var error))throw new InvalidOperationException(error);
            selectionCancel?.Cancel();selectionPhase=preparingSelection?"cancelling":"cancelled";picking=false;
            if(!preparingSelection){ReleaseSelectionPicker(id);ClearPreview();EndSelectionOwner();Say("Import cancelled — saved models remain");}
        }
        void SelectionFailed(string message){selectionPhase="failed";selectionError=Bounded(message);picking=false;Say(message);}
        void FinishSelectionPreview()
        {
            if(selectionPhase=="preview")selectionPhase="consumed";
            if(!preparingSelection)EndSelectionOwner();
        }
        void EndSelectionOwner(){picking=false;selectionWrite?.Dispose();selectionWrite=null;selectionCancel?.Dispose();selectionCancel=null;}
        void ReleaseSelectionPicker(string id){try{modelPicker?.Release(id);}catch(Exception){selectionError="The selected file is still closing. Resume Maestro before choosing another.";}}
        void CloseSelection()
        {
            selectionCancel?.Cancel();if(preparingSelection||selectionPhase=="accepting"){selectionPhase="cancelling";return;}
            if(picking||selectionWrite!=null){selectionPhase="cancelled";ReleaseSelectionPicker(selectionId);ClearPreview();EndSelectionOwner();}
        }
        static string Bounded(string text)=>ImportObservation.Text(text);
        internal JObject ObserveSelection()
        {
            var accepted=selectionResult??EmptyImportResult();
            var preview=(JObject)selectionInfo?.DeepClone()??new JObject {["modelHash"]="",["name"]="",["kibibytes"]=0,["vertices"]=0,["triangles"]=0,["clips"]=0,["humanoid"]=false};
            preview["name"]=ImportObservation.Text((string)preview["name"]);
            return new JObject {["requestId"]=selectionId,["phase"]=selectionPhase,["error"]=Bounded(selectionError),["preview"]=preview,
                ["accepted"]=new JObject {["destination"]=accepted["destination"].DeepClone(),["objectId"]=accepted["objectId"].DeepClone(),["revision"]=accepted["revision"].DeepClone(),["temporary"]=accepted["temporary"].DeepClone(),["motionCount"]=((JArray)accepted["motionIds"]).Count}};
        }
        internal JObject ObserveAcceptedMotions(string requestId,int offset)
        {
            if(requestId!=selectionId||selectionResult==null||selectionInfo==null||offset<0||offset>31)return null;
            var ids=(JArray)selectionResult["motionIds"];
            return new JObject {["requestId"]=selectionId,["modelHash"]=selectionInfo["modelHash"].DeepClone(),["motionCount"]=ids.Count,["motionOffset"]=offset,["motionIds"]=new JArray(ids.Skip(offset).Take(ImportObservation.MotionPageSize).Select(id=>id.DeepClone()))};
        }
    }
#if UNITY_ANDROID && !UNITY_EDITOR
    internal sealed class AndroidModelPicker:IModelPicker
    {
        public string CacheRoot {get;}
        public bool ReadyToStart {get{using var picker=new AndroidJavaClass("com.maestro.quest.browser.ModelPicker");return picker.CallStatic<bool>("ReadyToStart");}}
        public AndroidModelPicker(){using var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer");using var activity=player.GetStatic<AndroidJavaObject>("currentActivity");using var picker=new AndroidJavaClass("com.maestro.quest.browser.ModelPicker");CacheRoot=picker.CallStatic<string>("CacheRoot",activity);}
        public void Start(string id){using var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer");using var activity=player.GetStatic<AndroidJavaObject>("currentActivity");using var picker=new AndroidJavaClass("com.maestro.quest.browser.ModelPicker");picker.CallStatic<string>("Start",activity,id);}
        public JObject Read(string id){using var picker=new AndroidJavaClass("com.maestro.quest.browser.ModelPicker");string value=picker.CallStatic<string>("Read",id);return string.IsNullOrEmpty(value)?null:JObject.Parse(value);}
        public void Release(string id){using var picker=new AndroidJavaClass("com.maestro.quest.browser.ModelPicker");picker.CallStatic("Release",id);}
    }
#endif
}
