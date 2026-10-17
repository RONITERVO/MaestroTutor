// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        // These builders use the same validators and setting application as physical
        // controls. Capture mutable preferences before a worker or waiter can yield.
        delegate bool ApplySettings(RoomObjectData data,out string error);
        bool BeginSettingsEdit(string target,Func<string> validate,ApplySettings apply,string message,
            out PendingPersistedEdit pending,out string error,Action<string> checkpoint)
        {
            return BeginContentEdit(validate,(out RoomObjectData[] values,out string issue)=>{
                var data=Pose(Read(target),Find(target).transform);values=new[]{data};return apply(data,out issue);
            },Array.Empty<string>(),message,out pending,out error,checkpoint);
        }
        internal bool BeginConfigurePhysics(string target,int revision,ObjectPhysicsSettings settings,
            out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null)
        {
            var captured=settings==null?null:new ObjectPhysicsSettings{mode=settings.mode,shape=settings.shape,mass=settings.mass};
            return BeginSettingsEdit(target,()=>CanConfigurePhysics(target,revision,captured,out var issue)?null:issue,
                (RoomObjectData data,out string issue)=>RoomControls.SetPhysics(data,captured,out issue),
                "Physics settings saved",out pending,out error,checkpoint);
        }
        internal bool BeginConfigureMovement(int revision,AvatarMovementSettings settings,
            out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null)
        {
            var captured=settings==null?null:new AvatarMovementSettings{distance=settings.distance,speed=settings.speed};
            return BeginSettingsEdit("maestro",()=>CanConfigureMovement(revision,captured,out var issue)?null:issue,
                (RoomObjectData data,out string issue)=>RoomControls.SetMovement(data,captured,out issue),
                "Maestro movement preferences saved",out pending,out error,checkpoint);
        }
        internal bool BeginConfigureWalk(int revision,string source,string motionId,string modelHash,int clipIndex,
            out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null)=>
            BeginSettingsEdit("maestro",()=>CanConfigureWalk(revision,source,motionId,modelHash,clipIndex,out var issue)?null:issue,
                (RoomObjectData data,out string issue)=>ApplyWalkChoice(data,source,motionId,modelHash,clipIndex,out issue),
                "Walking animation saved",out pending,out error,checkpoint);
        internal bool BeginConfigureObjectWalk(string target,int revision,string source,string hash,string motionId,int index,
            out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null)=>
            BeginSettingsEdit(target,()=>CanConfigureObjectWalk(target,revision,source,hash,motionId,index,out var issue)?null:issue,
                (RoomObjectData data,out string issue)=>ApplyWalkChoice(data,source,motionId,hash,index,out issue),
                "Character walking animation saved",out pending,out error,checkpoint);
        internal bool BeginConfigureWaterTraversal(string target,int revision,RoomWaterTraversal settings,
            out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null)
        {
            var captured=settings?.Copy();
            return BeginSettingsEdit(target,()=>CanConfigureWaterTraversal(target,revision,captured,out var issue)?null:issue,
                (RoomObjectData data,out string issue)=>{data.waterTraversal=captured.Copy();issue=null;return true;},
                "Water traversal settings saved",out pending,out error,checkpoint);
        }
    }
}
