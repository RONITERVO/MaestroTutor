// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation {
    public sealed partial class RoomEditor {
        internal bool PrepareContainer(string target,int revision,RoomContainer definition,out RoomObjectData data,out string error){
            data=null;if(!ComponentSource(target,revision,out data,out error))return false;
            data.containers=definition==null?Array.Empty<RoomContainer>():new[]{definition.Copy()};return ComponentCandidate(new[]{data},out error);
        }
        internal bool EditContainer(string target,int revision,RoomContainer definition,out string error){
            if(!PrepareContainer(target,revision,definition,out var data,out error))return false;
            return CommitPersisted(new[]{data},Array.Empty<string>(),definition==null?"Container removed":"Container saved",false,out error,ComponentBefore(new[]{data}));
        }

        internal bool CanEditContainer(string target,int revision,out string error)=>ComponentSource(target,revision,out _,out error);
        internal bool CanTransferContainer(string source,int sourceRevision,string destination,int destinationRevision,out string error)=>
            ComponentSource(source,sourceRevision,out _,out error)&&ComponentSource(destination,destinationRevision,out _,out error);
        internal bool BeginContainer(string target,int revision,RoomContainer definition,out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null)
        {
            var captured=definition?.Copy();
            return BeginPreparedEdit(()=>CanEditContainer(target,revision,out var issue)?null:issue,
                (out RoomJournal.PreparedEdit edit,out string issue)=>{
                    edit=null;return PrepareContainer(target,revision,captured,out var data,out issue)&&
                        journal.Prepare(new[]{data},Array.Empty<string>(),out edit,out issue,observedBefore:ComponentBefore(new[]{data}));
                },captured==null?"Container removed":"Container saved",out pending,out error,checkpoint);
        }
        internal bool BeginContainerTransfer(string source,int sourceRevision,string destination,int destinationRevision,double requested,
            out double moved,out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null)
        {
            double quantity=0;
            bool started=BeginPreparedEdit(()=>CanTransferContainer(source,sourceRevision,destination,destinationRevision,out var issue)?null:issue,
                (out RoomJournal.PreparedEdit edit,out string issue)=>{
                    edit=null;return PrepareContainerTransfer(source,sourceRevision,destination,destinationRevision,requested,out var data,out quantity,out issue)&&
                        journal.Prepare(data,Array.Empty<string>(),out edit,out issue,scope:RoomEditScope.LiquidContents);
                },"Liquid transferred — one Undo restores both containers",out pending,out error,checkpoint);
            moved=started?quantity:0;return started;
        }

        internal bool PrepareContainerTransfer(string source,int sourceRevision,string destination,int destinationRevision,double requested,out RoomObjectData[] data,out double moved,out string error){
            data=null;moved=0;error="Choose different source and destination objects";if(source==destination)return false;
            if(!ComponentSource(source,sourceRevision,out var from,out error)||!ComponentSource(destination,destinationRevision,out var to,out error))return false;
            if(!RoomContainer.Transfer(from.containers?.FirstOrDefault(),to.containers?.FirstOrDefault(),requested,out moved,out error))return false;
            data=new[]{from,to};return ComponentCandidate(data,out error);
        }
        internal bool TransferContainer(string source,int sourceRevision,string destination,int destinationRevision,double requested,out double moved,out string error){
            if(!PrepareContainerTransfer(source,sourceRevision,destination,destinationRevision,requested,out var data,out moved,out error))return false;
            if(CommitPersisted(data,Array.Empty<string>(),"Liquid transferred — one Undo restores both containers",false,out error,scope:RoomEditScope.LiquidContents))return true;
            moved=0;return false;
        }
    }
}
