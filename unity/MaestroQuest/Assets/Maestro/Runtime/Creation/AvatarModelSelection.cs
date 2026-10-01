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
            if(authoring&&(authoring.ControlsTarget("maestro")||authoring.HasUnsavedRecording)){error="Finish authoring and save or discard the retained take first";return false;}
            error=null;return true;
        }
        internal bool BeginMaestroModel(string hash,int revision,CancellationToken cancellation,out Task<bool> completion,out string error)
        {
            completion=null;if(!CanSelectMaestroModel(hash,revision,out error))return false;
            var write=WriteGate.TryWrite(out error);if(write==null)return false;
            var avatar=Find("maestro").GetComponent<MaestroAvatar>();string roomSession=TemporarySessionId;
            var data=Read("maestro");hash=string.IsNullOrEmpty(hash)?null:hash;
            if(data.modelHash!=hash)data.walkClip=0;data.modelHash=hash;
            bool CommitPrepared(){
                if(!this||cancellation.IsCancellationRequested||RuntimeGate.Held||Ownership.Suspended||TemporarySessionId!=roomSession||ObjectRevision("maestro")!=revision||Find("maestro").Grab.isSelected){if(this)SetStatus("Maestro or the room changed while loading; selection was not saved");return false;}
                bool saved=CommitPersisted(new[]{data},Array.Empty<string>(),hash==null?"Included Maestro selected":"Custom Maestro selected",false,out var reason);
                if(!saved)SetStatus(reason);return saved;
            }
            try {completion=ReleaseModelWrite(avatar.SelectModel(hash,Models,CommitPrepared,cancellation),write);return true;}
            catch {write.Dispose();throw;}
        }
        static async Task<bool> ReleaseModelWrite(Task<bool> pending,IDisposable write){try{return await pending;}finally{write.Dispose();}}
        public bool SetMaestroModel(string hash)
        {
            if(RuntimeGate.Held||WriteGate.Frozen){SetStatus(RuntimeGate.Held?RuntimeGate.Reason:Persistence.WorkspaceWriteGate.FrozenReason);return false;}
            // Trusted physical selection ends authoring before capturing its new revision.
            Editing?.Invoke();if(Busy())return false;
            if(!CanSelectMaestroModel(hash,ObjectRevision("maestro"),out var error)){SetStatus(error);return false;}
            var cancel=new CancellationTokenSource();
            if(!Ownership.TryAcquire("avatar-selection:"+Guid.NewGuid().ToString("N"),"Avatar selection",RoomActorRole.Control,new[]{new BehaviourCatalog.Claim("maestro","wholeTarget")},_=>cancel.Cancel(),out var lease,out error,replaceControl:true)){cancel.Dispose();SetStatus(error);return false;}
            if(!BeginMaestroModel(hash,ObjectRevision("maestro"),cancel.Token,out var completion,out error)){lease.Dispose();cancel.Dispose();SetStatus(error);return false;}
            _=ReleaseModelOwner(completion,lease,cancel);return true;
        }
        static async Task ReleaseModelOwner(Task<bool> pending,IDisposable lease,CancellationTokenSource cancel){try{await pending;}finally{lease.Dispose();cancel.Dispose();}}
        internal JObject ObserveMaestroModel()
        {
            var avatar=Find("maestro")?.GetComponent<MaestroAvatar>();if(!avatar)return null;
            string saved=Read("maestro").modelHash??"",displayed=avatar.ModelHash??"",status=avatar.ModelStatus.Replace("\r"," ").Replace("\n"," ");
            return new JObject {["revision"]=ObjectRevision("maestro"),["selectedHash"]=saved,["displayedHash"]=displayed,["busy"]=avatar.ModelBusy,["phase"]=avatar.ModelBusy?"loading":saved==displayed?"ready":"unavailable",["status"]=status.Length>128?status[..128]:status,["temporary"]=TemporaryRoom,["canPose"]=!avatar.ModelBusy&&avatar.PoseRig};
        }
    }
}
