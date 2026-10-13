// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Avatar;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
namespace Maestro.Quest.Creation
{
    [Serializable] public sealed class AvatarWalkObservation
    {
        public string source,motionId,modelHash,name,status,playbackStatus;
        public int clipIndex=-1;
        public bool available;
    }
    // Shared by the physical walk chooser, library book and room command journal.
    public static class AvatarWalkSelection
    {
        public static bool ValidId(string id) => id == "" || id != null && Guid.TryParseExact(id,"N",out _) && id == id.ToLowerInvariant();
        internal static ImportedModel Model(RoomEditor editor,RoomObjectData data)
        {
            var item=data==null?null:editor.Find(data.id);
            if(!item)return null;
            var avatar=item.GetComponent<MaestroAvatar>();
            return avatar ? (avatar.ModelBusy?null:avatar.CustomModel) : item.GetComponent<CreatedRoomObject>()?.Model;
        }
        internal static bool Loaded(RoomEditor editor,RoomObjectData data,out ImportedModel model,out string status)
        {
            model=Model(editor,data);status="Wait for the exact loaded model with a supported humanoid rig";
            if(!model||!model.Ready||model.AssetHash!=data.modelHash||!model.IsHumanoid)return false;
            status=model.PlaybackIssue;if(status!=null)return false;
            return true;
        }
        static bool Compatible(RoomEditor editor,RoomObjectData data,string id,out MotionEntry entry,out string status)
        {
            entry=editor.Motions.Inspect(id);
            status="Choose a downloaded motion compatible with the loaded Maestro, at least 0.1 seconds long";
            if (!Loaded(editor,data,out var model,out status)) return false;
            if (entry == null) {status="The saved walking motion is missing. Import its original export again.";return false;}
            if (!editor.Motions.Downloaded(id)) {status="The walking motion download is missing. Import its original export again.";return false;}
            if (entry.Short || entry.rigHash != model.MotionRigHash) {status="Choose a compatible walking motion at least 0.1 seconds long";return false;}
            status="Saved walking motion assigned";return true;
        }
        public static bool Apply(RoomEditor editor,RoomObjectData data,string id,out string status)
        {
            status="Choose Maestro and a saved walking motion, or the included walk";
            if (data==null || data.kind is not (RoomObjectKind.Maestro or RoomObjectKind.ImportedModel) || !ValidId(id)) return false;
            if (id != "" && !Compatible(editor,data,id,out _,out status)) return false;
            data.walkClip=0;data.walkMotionId=id == "" ? null : id;
            status=id == "" ? "Included walking animation selected" : "Saved walking motion assigned";return true;
        }
        public static bool ApplyEmbedded(RoomEditor editor,RoomObjectData data,string modelHash,int index,out string status)
        {
            status="Choose an embedded walking clip at least 0.1 seconds long from the exact loaded Maestro model";
            if(data==null||data.kind is not (RoomObjectKind.Maestro or RoomObjectKind.ImportedModel)||!ModelLibrary.ValidHash(modelHash)||data.modelHash!=modelHash||!Loaded(editor,data,out var model,out status)||index<0||index>=model.ClipCount||model.ClipDuration(index)<.1f){status??="Choose an available embedded walking clip at least 0.1 seconds long";return false;}
            data.walkClip=index+1;data.walkMotionId=null;status="Embedded walking animation selected";return true;
        }
        public static AvatarWalkObservation Observe(RoomEditor editor,string target="maestro")
        {
            var data=editor.Read(target);var item=editor.Find(target);var avatar=item?item.GetComponent<MaestroAvatar>():null;
            if(data==null||!item||data.kind is not (RoomObjectKind.Maestro or RoomObjectKind.ImportedModel))return null;
            bool isMaestro=data.kind==RoomObjectKind.Maestro,busy=isMaestro&&(!avatar||avatar.ModelBusy);
            var view=new AvatarWalkObservation {source=isMaestro?"included":"none",motionId="",modelHash=data.modelHash ?? "",name=isMaestro?"Included walk":"No walking animation",available=!busy,
                status=busy ? "Wait for Maestro's model to finish loading" : "Included walking animation selected",playbackStatus=isMaestro ? (avatar?avatar.WalkMotionStatus??"":"") : ImportedObjectWalk.StatusFor(item)};
            if (!string.IsNullOrEmpty(data.walkMotionId)) {
                view.source="library";view.motionId=data.walkMotionId;
                view.available=Compatible(editor,data,data.walkMotionId,out var entry,out var reason);
                view.name=entry?.name ?? "Missing saved walk";view.status=view.available ? "Saved walking motion is available" : reason;
            } else if(data.walkClip>0) {
                var model=Model(editor,data);view.source="embedded";view.clipIndex=data.walkClip-1;
                view.available=Loaded(editor,data,out model,out _) && view.clipIndex<model.ClipCount && model.ClipDuration(view.clipIndex)>=.1f;
                view.name=view.available ? model.ClipName(view.clipIndex) : "Unavailable embedded walk";
                view.status=view.available ? "Embedded walking clip is available" : "The selected embedded walk is unavailable for the current model";
            }
            if(!isMaestro&&view.source=="none")view.status="Movement has no assigned pose animation";
            return view;
        }
    }
}
