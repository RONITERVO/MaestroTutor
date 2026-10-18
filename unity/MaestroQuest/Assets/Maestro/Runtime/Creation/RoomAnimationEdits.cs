// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Avatar;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        internal bool CanEditAnimation(string target,int revision,out string error)
        {
            if(!CanEditObject(target,false,out error)||!TryGetLiveObject(target,out var item,out error))return false;
            if(ObjectRevision(target)!=revision){error="The object changed; inspect its animation before editing";return false;}
            if(RuntimeGate.Held||Ownership.Suspended){error="Resume room interaction before editing animations";return false;}
            var workshop=GetComponent<AnimationWorkshop>();
            if(workshop&&workshop.HasUnfinishedAuthoring(target)){error="Finish or discard this object's recording or pose before editing its saved animation";return false;}
            var avatar=item.GetComponent<MaestroAvatar>();
            if(avatar&&(avatar.ModelBusy||avatar.UpperBodyActive||avatar.GetComponent<AvatarSpatialMotion>()?.Active==true)){
                error="Wait for Maestro's model and current movement or gesture before editing its animation";return false;
            }
            error=null;return true;
        }
        // Physical authoring owns its target, including retained takes. Share the
        // saved-data preparation, while its session keeps the synchronous contract.
        internal bool PrepareAnimation(string target,int revision,RoomMotion motion,JointPose[] joints,bool savePose,out RoomObjectData data,out string error)
        {
            data=null;error="The object changed; inspect its animation before editing";
            if(journal==null||ObjectRevision(target)!=revision)return false;
            data=Read(target);if(data==null)return false;
            data.motion=motion?.Copy();if(savePose)data.joints=MotionFrame.CopyJoints(joints);
            var replacement=data;var candidate=Snapshot();
            candidate.objects=candidate.objects.Select(x=>x.id==target?replacement:x).ToArray();
            return candidate.Validate(out error);
        }
        internal bool BeginAnimation(string target,int revision,RoomMotion motion,JointPose[] joints,bool savePose,
            out RoomObjectData prepared,out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null)
        {
            RoomObjectData captured=null;
            bool result=BeginAttachedEdit(()=>CanEditAnimation(target,revision,out var issue)?null:issue,
                (out RoomObjectData data,out string issue)=>{
                    bool valid=PrepareAnimation(target,revision,motion,joints,savePose,out data,out issue);captured=data;return valid;
                },RoomEditScope.Content,"Animation saved",out pending,out error,checkpoint);
            prepared=result?captured:null;return result;
        }
    }
}
