// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        delegate bool PrepareGeometryEdit(out RoomObjectData data,out string error);
        bool BeginGeometryEdit(Func<string> validate,PrepareGeometryEdit prepare,string message,
            out RoomObjectData prepared,out PendingPersistedEdit pending,out string error,
            Action<string> checkpoint,Func<string> validateApplied=null)
        {
            RoomObjectData candidate=null;
            bool started=BeginPreparedEdit(validate,(out RoomJournal.PreparedEdit edit,out string issue)=>{
                edit=null;return prepare(out candidate,out issue)&&
                    journal.Prepare(new[]{candidate},Array.Empty<string>(),out edit,out issue,scope:RoomEditScope.Content);
            },message,out pending,out error,checkpoint,validateApplied:validateApplied);
            prepared=started?candidate:null;return started;
        }
        internal bool BeginRecipeEdit(string target,int revision,JObject patch,out RoomObjectData prepared,
            out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null)
        {
            var captured=(JObject)patch.DeepClone();
            return BeginGeometryEdit(()=>CanEditRecipe(target,revision,out var issue)?null:issue,
                (out RoomObjectData data,out string issue)=>PrepareRecipeEdit(target,revision,captured,out data,out issue),
                "Recipe edited; animation stopped",out prepared,out pending,out error,checkpoint);
        }
        internal bool BeginCollisionEdit(string target,int revision,CollisionRecipe collision,out RoomObjectData prepared,
            out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null)
        {
            var captured=collision?.Copy();
            return BeginGeometryEdit(()=>CanEditCollision(target,revision,out var issue)?null:issue,
                (out RoomObjectData data,out string issue)=>PrepareCollisionEdit(target,revision,captured,out data,out issue),
                "Collision shapes saved",out prepared,out pending,out error,checkpoint);
        }
        internal bool BeginModelGeometry(string target,int revision,RoomModelGeometry settings,
            out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null)
        {
            var captured=settings?.Copy();
            return BeginGeometryEdit(()=>CanEditModelGeometry(target,revision,captured,out var issue)?null:issue,
                (out RoomObjectData data,out string issue)=>PrepareModelGeometry(target,revision,captured,out data,out issue),
                "Imported geometry saved",out _,out pending,out error,checkpoint,()=>AppliedModelGeometryIssue(target));
        }
    }
}
