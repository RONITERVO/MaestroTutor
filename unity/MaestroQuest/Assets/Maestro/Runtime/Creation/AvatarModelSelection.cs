// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Avatar;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        internal bool CanSelectMaestroModel(string hash,int revision,out string error)
        {
            error="Choose an included or imported model identity";
            if(hash!=null&&hash!=""&&!ModelLibrary.ValidHash(hash))return false;
            if(RuntimeGate.Held){error=RuntimeGate.Reason;return false;}
            if(Ownership.Suspended){error="Room actions are paused";return false;}
            if(!CanEditObject("maestro",false,out error))return false;
            if(ObjectRevision("maestro")!=revision){error="Maestro changed; inspect avatar.model before choosing";return false;}
            var avatar=Find("maestro").GetComponent<MaestroAvatar>();
            if(!avatar||avatar.ModelBusy){error="Wait for Maestro to finish loading";return false;}
            var authoring=GetComponent<AnimationWorkshop>();
            if(authoring&&authoring.HasUnfinishedAuthoring("maestro")){error="Finish authoring Maestro and save or discard its retained animation first";return false;}
            error=null;return true;
        }
        internal Action<string> AvatarSelectionCheckpoint {get;set;}
        PendingModelSelection avatarSelection;
        internal bool AvatarSelectionPending=>avatarSelection!=null&&!avatarSelection.Drained||manualCreation?.Label=="Avatar selection";
        internal Task<bool> AvatarSelectionCompletion=>ManualCreationCompletion;
        internal bool StartMaestroModel(string hash,int revision,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanSelectMaestroModel(hash,revision,out error))return false;
            string captured=string.IsNullOrEmpty(hash)?IncludedAvatar?.Hash??"":hash;
            var checkpoint=AvatarSelectionCheckpoint;PendingModelSelection selected=null;
            bool Begin(out IPersistedRoomEdit pending,out string issue){
                if(!BeginPersistedStateEdit(write=>avatarSelection=selected=new PendingModelSelection(this,captured,revision,write,checkpoint),out pending,out issue))return false;
                selected.Start();return true;
            }
            return PersistedEditOperation.StartStateChange(this,Begin,
                (out string issue)=>CanSelectMaestroModel(captured,revision,out issue),
                ()=>selected?.Result??new JObject(),out operation,out error);
        }
        internal async Task<JObject> SelectMaestroModelAsync(string hash,int revision,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if(!StartMaestroModel(hash,revision,out var operation,out var error))throw new ModelImportException(error);
            try{
                while(true){
                    if(token.IsCancellationRequested){
                        var accepted=operation.InterruptionStatus;operation.Stop(false);
                        if(accepted!=null)throw new ModelImportException(accepted);
                        token.ThrowIfCancellationRequested();
                    }
                    var state=operation.State(out error);
                    if(state==Rules.RuleActionState.Failed)throw new ModelImportException(error);
                    if(state==Rules.RuleActionState.Ready){
                        if(!operation.Complete(out error))throw new ModelImportException(error);
                        return operation.Result;
                    }
                    await Task.Yield();
                }
            }finally{operation.Stop(false);}
        }
        public bool SetMaestroModel(string hash)
        {
            if(hash!=null&&hash!=""&&!ModelLibrary.ValidHash(hash)){SetStatus("Choose an included or imported model identity");return false;}
            string captured=string.IsNullOrEmpty(hash)?IncludedAvatar?.Hash??"":hash;
            return StartManualCreation("maestro",true,
                (out CapabilityOperation operation,out string error)=>StartMaestroModel(captured,ObjectRevision("maestro"),out operation,out error),
                null,"Avatar selection");
        }
        internal JObject ObserveMaestroModel()
        {
            var avatar=Find("maestro")?.GetComponent<MaestroAvatar>();if(!avatar)return null;
            bool busy=avatar.ModelBusy||AvatarSelectionPending;
            string saved=Read("maestro").modelHash??"",displayed=avatar.ModelHash??"",status=(storage.SelectionPublicationUnconfirmed?storage.UnavailableReason:avatar.ModelStatus).Replace("\r"," ").Replace("\n"," ");
            return new JObject {["revision"]=ObjectRevision("maestro"),["selectedHash"]=saved,["displayedHash"]=displayed,["busy"]=busy,["phase"]=busy?"loading":!storage.SelectionPublicationUnconfirmed&&saved==displayed?"ready":"unavailable",["status"]=status.Length>128?status[..128]:status,["temporary"]=TemporaryRoom,["canPose"]=!busy&&avatar.PoseRig};
        }
    }
}
