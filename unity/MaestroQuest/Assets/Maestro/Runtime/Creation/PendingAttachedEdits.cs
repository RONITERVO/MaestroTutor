// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        delegate bool PrepareAttachedEdit(out RoomObjectData data,out string error);
        bool BeginAttachedEdit(Func<string> validate,PrepareAttachedEdit prepare,RoomEditScope scope,string message,
            out PendingPersistedEdit pending,out string error,Action<string> checkpoint)
        {
            return BeginPreparedEdit(validate,(out RoomJournal.PreparedEdit edit,out string issue)=>{
                edit=null;return prepare(out var data,out issue)&&
                    journal.Prepare(new[]{data},Array.Empty<string>(),out edit,out issue,scope:scope);
            },message,out pending,out error,checkpoint);
        }
        internal bool BeginDrawingTip(string target,int revision,DrawingTip tip,out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null)
        {
            var captured=tip?.Copy();
            return BeginAttachedEdit(()=>CanEditDrawingTip(target,revision,out var issue)?null:issue,
                (out RoomObjectData data,out string issue)=>PrepareDrawingTip(target,revision,captured,out data,out issue),
                RoomEditScope.Content,captured==null?"Drawing tip removed":"Drawing tip saved",out pending,out error,checkpoint);
        }
        internal bool BeginSculptTip(string target,int revision,SculptTip tip,out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null)
        {
            var captured=tip?.Copy();
            return BeginAttachedEdit(()=>CanEditComponent(target,revision,out var issue)?null:issue,
                (out RoomObjectData data,out string issue)=>PrepareSculptTip(target,revision,captured,out data,out issue),
                RoomEditScope.Content,captured==null?"Sculpt tip removed":"Sculpt tip saved",out pending,out error,checkpoint);
        }
        internal bool BeginLightEdit(string target,int revision,JObject args,out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null)
        {
            var captured=(JObject)args.DeepClone();
            return BeginAttachedEdit(()=>CanEditLight(target,revision,out var issue)?null:issue,
                (out RoomObjectData data,out string issue)=>PrepareLightEdit(target,revision,captured,out data,out issue),
                RoomEditScope.Visual,"Object light saved",out pending,out error,checkpoint);
        }
        internal bool BeginWindowEdit(string target,int revision,JObject args,out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null)
        {
            var captured=(JObject)args.DeepClone();
            return BeginAttachedEdit(()=>CanEditWindow(target,revision,out var issue)?null:issue,
                (out RoomObjectData data,out string issue)=>PrepareWindowEdit(target,revision,captured,out data,out issue),
                RoomEditScope.Visual,"Passthrough window saved",out pending,out error,checkpoint);
        }
        internal bool BeginSurfaceEdit(string target,int revision,JObject args,out string strokeId,
            out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null)
        {
            var captured=(JObject)args.DeepClone();string stroke="";
            bool started=BeginAttachedEdit(()=>CanEditSurface(target,revision,out var issue)?null:issue,
                (out RoomObjectData data,out string issue)=>PrepareSurfaceEdit(target,revision,captured,out data,out stroke,out issue),
                RoomEditScope.Content,"Surface drawing saved",out pending,out error,checkpoint);
            strokeId=started?stroke:"";return started;
        }
    }
}
