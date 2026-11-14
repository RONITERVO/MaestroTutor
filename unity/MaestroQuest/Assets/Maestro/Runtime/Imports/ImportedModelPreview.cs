// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
namespace Maestro.Quest.Imports
{
    public sealed partial class ImportedModel
    {
        RoomOwnership.Lease previewOwner;
        // Only native physical preview controls enter this path. Program clips
        // retain their scheduler reservation and cannot choose Control priority.
        internal bool PlayPreview(RoomEditor editor,string target,int index,bool loop,out string error)
        {
            error="Choose a ready saved model with an embedded animation";
            if(!editor||!editor.isActiveAndEnabled||!isActiveAndEnabled||!Ready||index<0||index>=ClipCount)return false;
            if(editor.RuntimeGate.Held){error=editor.RuntimeGate.Reason;return false;}
            if(PlaybackIssue!=null){error=PlaybackIssue;return false;}
            var item=editor.Find(target);
            if(!item||item.GetComponent<CreatedRoomObject>()?.Model!=this)return false;
            if(item.Grab.isSelected){error="Release this object before previewing its clip";return false;}
            if(!editor.Ownership.TryAcquire("model-preview:"+Guid.NewGuid().ToString("N"),"Model clip preview",RoomActorRole.Control,
                new[]{new BehaviourCatalog.Claim(target,"wholeTarget")},_=>Stop(),out var owner,out error,
                preservePlacement:true,replaceControl:true))return false;
            try{
                if(editor.GetComponent<AnimationWorkshop>()?.HasUnfinishedAuthoring(target)==true){
                    error="Finish saving or discard this object's unfinished animation before previewing it";return false;
                }
                Play(index,loop);
                if(!owner.Held||!IsPlaying){Stop();error="The model clip preview could not start";return false;}
                previewOwner=owner;owner=null;error=null;return true;
            }finally{owner?.Dispose();}
        }
        internal void StopPreview(){if(previewOwner!=null)Stop();}
        void ReleasePreviewOwner(){var owner=previewOwner;previewOwner=null;owner?.Dispose();}
        void LateUpdate(){if(previewOwner!=null&&(!previewOwner.Held||!IsPlaying))Stop();}
    }
}
