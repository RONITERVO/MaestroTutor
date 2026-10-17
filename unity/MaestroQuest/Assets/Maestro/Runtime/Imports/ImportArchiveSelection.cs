// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Imports
{
    internal interface IModelArchivePicker { bool SelectMember(string id,int index,int request); }
    public sealed partial class ImportWorkshop
    {
        JArray archiveMembers;
        int archiveVersion,archiveCursor;
        bool archivePickerOwned;
        internal int ArchiveIndex=>archiveCursor;
        public bool HasArchive=>archiveMembers!=null&&selectionWrite!=null;
        public bool BrowsingArchive=>HasArchive&&selectionPhase=="archive";
        void AdoptArchive(JObject result)
        {
            var members=result["members"] as JArray;
            if(members==null||members.Count<1||members.Count>MotionBatch.MaximumFiles||modelPicker is not IModelArchivePicker)
                throw new ModelImportException("The ZIP file list is unavailable");
            foreach(var item in members){
                if(item is not JObject member||member["name"]?.Type!=JTokenType.String||((string)member["name"]).Length>120||member["bytes"]?.Type!=JTokenType.Integer||
                    (long)member["bytes"]<28||(long)member["bytes"]>ModelInspection.MaximumBytes)throw new ModelImportException("The ZIP returned invalid model information");
            }
            archiveMembers=(JArray)members.DeepClone();archivePickerOwned=true;archiveVersion=1;archiveCursor=0;selectionPhase="archive";picking=false;libraryMode=false;selectionError="";
            ShowArchive();Say("ZIP ready — choose a file, then Preview");
        }
        internal bool CanChooseArchive(string id,int version,int index,out string error)
        {
            error="Inspect the current ZIP and its version before choosing a file";
            if(disposed||!isActiveAndEnabled||!HasArchive||id!=selectionId||version!=archiveVersion||archiveVersion>=1000000||index<0||index>=archiveMembers.Count||Busy||selectionPhase is not ("archive" or "preview"))return false;
            if(selectionPaused||!selectionFocused||editor.RuntimeGate.Held){error="Resume Maestro before choosing a ZIP model";return false;}
            if(editor.WriteGate.Frozen||!editor.CanSaveRoom){error="Room saving is unavailable";return false;}
            if(editor.AnyHeld||editor.DrawingInProgress||animationWorkshop&&(animationWorkshop.IsPosing||animationWorkshop.IsRecording||animationWorkshop.HasUnsavedPose||animationWorkshop.HasUnsavedRecording)){error="Finish the active interaction before changing the model preview";return false;}
            error=null;return true;
        }
        internal void ChooseArchiveMember(string id,int version,int index)
        {
            if(!CanChooseArchive(id,version,index,out var error))throw new InvalidOperationException(error);
            archiveVersion++;archiveCursor=index;selectionInfo=null;selectionResult=null;selectionError="";selectionPhase="copying";ClearPreview(false);libraryMode=false;
            picking=true;selectionBegan=UnityEngine.Time.realtimeSinceStartup;
            try{if(!((IModelArchivePicker)modelPicker).SelectMember(id,index,archiveVersion))throw new InvalidOperationException();Say("Reading the selected ZIP model…");}
            catch(Exception){ArchiveMemberFailed("The selected ZIP is unavailable. Cancel and choose it again.");}
        }
        void ArchiveMemberFailed(string error)
        {
            selectionPhase="archive";selectionInfo=null;selectionError=Bounded(error);picking=false;ShowArchive();Say(selectionError);
        }
        public void PreviewArchiveMember()
        {
            if(!CanChooseArchive(selectionId,archiveVersion,archiveCursor,out var error)){Say(error);return;}ChooseArchiveMember(selectionId,archiveVersion,archiveCursor);
        }
        public void BrowseArchive()=>BrowseArchive(selectionId,archiveVersion);
        internal void BrowseArchive(string id,int version)
        {
            if(!CanChooseArchive(id,version,archiveCursor,out var error)){Say(error);return;}
            archiveVersion++;selectionPhase="archive";selectionInfo=null;selectionError="";ClearPreview(false);libraryMode=false;ShowArchive();Say("Choose a ZIP file to preview");
        }
        public void NextArchiveMember()=>MoveArchive(1);
        public void PreviousArchiveMember()=>MoveArchive(-1);
        void MoveArchive(int direction)
        {
            if(!BrowsingArchive){Say("Open ZIP files before browsing");return;}
            if(!CanChooseArchive(selectionId,archiveVersion,archiveCursor,out var error)){Say(error);return;}
            archiveVersion++;archiveCursor=(archiveCursor+direction+archiveMembers.Count)%archiveMembers.Count;ShowArchive();
        }
        void ShowArchive()
        {
            if(!HasArchive)return;var item=archiveMembers[archiveCursor];
            Details="ZIP model "+(archiveCursor+1)+" / "+archiveMembers.Count+"\n"+string.Join("\n",ModelText.Wrap(ImportObservation.Text((string)item["name"]),58))+"\n"+Math.Ceiling((long)item["bytes"]/1024d)+" KiB\nPreview checks the model without saving it.\nThen choose Add model or Use Maestro.";
            Changed?.Invoke();
        }
        internal JObject ObserveArchive(string id,string query,int offset)
        {
            if(!HasArchive||id!=selectionId||query==null||query.Length>80||offset<0||offset>=MotionBatch.MaximumFiles)return null;
            var found=archiveMembers.Select((member,index)=>new {Member=member,Index=index}).Where(x=>((string)x.Member["name"]).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0).ToArray();
            var page=found.Skip(offset).Take(3).Select(x=>new JObject {["index"]=x.Index,["name"]=ImportObservation.Text((string)x.Member["name"]),["kibibytes"]=Math.Ceiling((long)x.Member["bytes"]/1024d)}).ToArray();
            return new JObject {["requestId"]=selectionId,["version"]=archiveVersion,["phase"]=selectionPhase,["count"]=archiveMembers.Count,["total"]=found.Length,["offset"]=offset,["focusedIndex"]=archiveCursor,["entries"]=new JArray(page)};
        }
        void ReleaseArchive()
        {
            if(archivePickerOwned){archivePickerOwned=false;ReleaseSelectionPicker(selectionId);}
            archiveMembers=null;
        }
    }
}
