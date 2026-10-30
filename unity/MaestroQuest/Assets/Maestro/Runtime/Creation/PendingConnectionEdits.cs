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
        internal bool BeginConnection(string target,int revision,string operation,string connected,int connectedRevision,
            RoomConnection definition,float amount,out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null)
        {
            pending=null;var captured=definition?.Copy();bool placement=ConnectionPlaces(operation);
            if(operation=="rearm"){error="Rearm changes only runtime state; use the immediate connection action";return false;}
            if(PendingEditSave){error="Wait for the pending room edit to finish or cancel it first";return false;}
            if(!CanEditConnectionSources(target,revision,connected,connectedRevision,out error))return false;
            var write=WriteGate.TryWrite(out error);if(write==null)return false;
            RoomJournal.PreparedEdit edit=null;LayoutPoseHolds poses=new();var owner=new object();
            try{
                if(!PrepareConnection(target,revision,operation,connected,captured,amount,out var data,out error))return false;
                var replacements=placement?new[]{data,Pose(Read(connected),Find(connected).transform)}:new[]{data};
                var before=placement?new RoomLayout{placements=replacements.Select(x=>Frame.Placement(x.id,Find(x.id).transform)).ToArray()}:null;
                if(before!=null&&!before.Validate(out error))return false;
                if(!journal.Prepare(replacements,Array.Empty<string>(),out edit,out error,observedBefore:before,
                    scope:placement?RoomEditScope.Full:RoomEditScope.Content))return false;
                if(placement)foreach(var p in before.placements)poses.Add(Find(p.target).GetComponent<RigidRoomItem>()?.HoldForEdit(owner));
                string CurrentIssue(){
                    if(!CanEditConnectionSources(target,revision,connected,connectedRevision,out var issue,owner))return issue;
                    if(operation=="attach"&&PhysicsWorld&&PhysicsWorld.Running)return "Pause room physics before joining the current poses";
                    if(before!=null)foreach(var p in before.placements){
                        var current=Frame.Placement(p.target,Find(p.target).transform);
                        if((current.position-p.position).sqrMagnitude>1e-10f||
                            Quaternion.Angle(current.rotation,p.rotation)>.001f||Mathf.Abs(current.scale-p.scale)>.00001f)
                            return "A connection member moved during preparation; inspect both members before retrying";
                    }
                    return null;
                }
                void Repair(){
                    if(operation=="remove")return;
                    var item=Find(target);var view=item?item.GetComponent<RoomConnectionView>():null;
                    if(!view)throw new InvalidOperationException("The accepted connection has no native view");
                    view.ResetBreak();
                }
                pendingEdit=pending=new PendingPersistedEdit(this,edit,write,poses,CurrentIssue,
                    "Connection "+operation,placement,checkpoint,afterNativeApply:Repair);
                edit=null;write=null;poses=null;return true;
            }finally{poses?.Dispose();edit?.Dispose();write?.Dispose();}
        }
    }
}
