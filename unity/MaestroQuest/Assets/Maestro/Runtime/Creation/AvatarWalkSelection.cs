// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Avatar;
using Maestro.Quest.Imports;
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
        static bool Compatible(RoomEditor editor,RoomObjectData data,string id,out MotionEntry entry,out string status)
        {
            entry=editor.Motions.Inspect(id);var avatar=editor.Find("maestro")?.GetComponent<MaestroAvatar>();
            status="Choose a downloaded motion compatible with the loaded Maestro, at least 0.1 seconds long";
            if (!avatar || avatar.ModelBusy || !avatar.CustomModel || !avatar.CustomModel.Ready || avatar.ModelHash != data.modelHash) return false;
            if (entry == null) {status="The saved walking motion is missing. Import its original export again.";return false;}
            if (!editor.Motions.Downloaded(id)) {status="The walking motion download is missing. Import its original export again.";return false;}
            if (entry.Short || entry.rigHash != avatar.CustomModel.MotionRigHash) return false;
            status="Saved walking motion assigned";return true;
        }
        public static bool Apply(RoomEditor editor,RoomObjectData data,string id,out string status)
        {
            status="Choose Maestro and a saved walking motion, or the included walk";
            if (data?.kind != RoomObjectKind.Maestro || !ValidId(id)) return false;
            if (id != "" && !Compatible(editor,data,id,out _,out status)) return false;
            data.walkClip=0;data.walkMotionId=id == "" ? null : id;
            status=id == "" ? "Included walking animation selected" : "Saved walking motion assigned";return true;
        }
        public static AvatarWalkObservation Observe(RoomEditor editor)
        {
            var data=editor.Read("maestro");var avatar=editor.Find("maestro")?.GetComponent<MaestroAvatar>();
            if (data == null || !avatar) return null;
            var view=new AvatarWalkObservation {source="included",motionId="",modelHash=data.modelHash ?? "",name="Included walk",available=!avatar.ModelBusy,
                status=avatar.ModelBusy ? "Wait for Maestro's model to finish loading" : "Included walking animation selected",playbackStatus=avatar.WalkMotionStatus ?? ""};
            if (!string.IsNullOrEmpty(data.walkMotionId)) {
                view.source="library";view.motionId=data.walkMotionId;
                view.available=Compatible(editor,data,data.walkMotionId,out var entry,out var reason);
                view.name=entry?.name ?? "Missing saved walk";view.status=view.available ? "Saved walking motion is available" : reason;
            } else if(data.walkClip>0) {
                var model=avatar.CustomModel;view.source="embedded";view.clipIndex=data.walkClip-1;
                view.available=!avatar.ModelBusy && model && model.Ready && avatar.ModelHash==data.modelHash && view.clipIndex<model.ClipCount && model.ClipDuration(view.clipIndex)>=.1f;
                view.name=view.available ? model.ClipName(view.clipIndex) : "Unavailable embedded walk";
                view.status=view.available ? "Embedded walking clip is available" : "The selected embedded walk is unavailable for the current model";
            }
            return view;
        }
    }
}
