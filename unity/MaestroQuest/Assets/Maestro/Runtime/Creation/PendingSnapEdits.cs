// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        internal bool BeginSnapPoint(string target,int revision,string point,RoomSnapPoint definition,
            out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null)
        {
            var captured=definition?.Copy();
            return BeginAttachedEdit(()=>CanEditSnapPoint(target,revision,out var issue)?null:issue,
                (out RoomObjectData data,out string issue)=>PrepareSnapPoint(target,revision,point,captured,out data,out issue),
                RoomEditScope.Content,captured==null?"Snap point removed":"Snap point saved",out pending,out error,checkpoint);
        }
        internal bool BeginSnap(RoomSnapPlacement request,out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null)
        {
            pending=null;
            // Capture only the small request. No candidate or body lease while the
            // capability is still waiting for its turn in the shared edit queue.
            var captured=request==null?null:JsonUtility.FromJson<RoomSnapPlacement>(JsonUtility.ToJson(request));
            if(PendingEditSave){error="Wait for the pending room edit to finish or cancel it first";return false;}
            if(!CanSnapSources(captured,null,out _,out _,out error))return false;
            var write=WriteGate.TryWrite(out error);if(write==null)return false;
            RoomJournal.PreparedEdit edit=null;LayoutPoseHolds poses=new();var owner=new object();
            try{
                if(!PrepareSnap(captured,out var replacements,out var before,out error)||
                    !journal.Prepare(replacements,Array.Empty<string>(),out edit,out error,observedBefore:before))return false;
                foreach(var p in before.placements)poses.Add(Find(p.target).GetComponent<RigidRoomItem>()?.HoldForEdit(owner));
                string CurrentIssue(){
                    if(!CanSnapSources(captured,owner,out _,out _,out var issue))return issue;
                    // A direct transform edit is not necessarily an authored revision.
                    // Never accept a joint computed against a different live frame.
                    foreach(var p in before.placements){
                        var current=Frame.Placement(p.target,Find(p.target).transform);
                        if((current.position-p.position).sqrMagnitude>1e-10f||
                            Quaternion.Angle(current.rotation,p.rotation)>.001f||Mathf.Abs(current.scale-p.scale)>.00001f)
                            return "A snap member moved during preparation; inspect it before retrying";
                    }
                    return null;
                }
                pendingEdit=pending=new PendingPersistedEdit(this,edit,write,poses,CurrentIssue,
                    captured.mode=="join"?"Construction snapped and joined — one Undo restores it":"Construction snapped — one Undo restores it",true,checkpoint);
                edit=null;write=null;poses=null;return true;
            }finally{poses?.Dispose();edit?.Dispose();write?.Dispose();}
        }
    }
}
