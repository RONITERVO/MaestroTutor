// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        internal const int MaximumLiquidCheckpoints=2;
        sealed class LiquidCheckpoint {
            internal RoomJournal Journal;internal Task<string> Completion;internal Action Confirmed;
            internal HashSet<string> Targets;internal bool Current=true;
        }
        readonly Queue<LiquidCheckpoint> liquidCheckpoints=new();
        bool acceptingLiquidFlow,notifyingLiquid;
        string liquidPersistenceFailure;
        RoomJournal liquidFailureJournal;
        internal Action<string> LiquidPublicationCheckpoint;
        internal bool PendingLiquidSave=>liquidCheckpoints.Count>0;
        internal bool LiquidCheckpointCapacity=>LiquidCheckpointWait==null;
        internal string LiquidCheckpointWait=>liquidCheckpoints.Count>=MaximumLiquidCheckpoints?
            "Liquid checkpoints are still saving":PendingEditSave&&!pendingPersistedEdit.Accepted?
            "A room edit is preparing its save":null;
        void InvalidateLiquidReports(RoomJournal.PreparedEdit edit) {
            if(acceptingLiquidFlow||edit.VisualOnly)return;
            foreach(var pending in liquidCheckpoints)
                foreach(var id in edit.ChangedObjects)if(pending.Targets.Contains(id)){pending.Current=false;break;}
        }
        internal JObject ObserveLiquidPersistence()=>new(){
            ["phase"]=ReferenceEquals(liquidFailureJournal,journal)&&liquidPersistenceFailure!=null?"unconfirmed":PendingLiquidSave?"saving":"idle",
            ["pending"]=liquidCheckpoints.Count,["capacity"]=MaximumLiquidCheckpoints,["temporary"]=TemporaryRoom,
            ["error"]=Imports.ImportObservation.Text(ReferenceEquals(liquidFailureJournal,journal)?liquidPersistenceFailure:null)};
        // Reconciliation never waits for this task. Writers run in acceptance
        // order with ordinary room saves; a failed ancestor stops later publication.
        static async Task<string> PersistLiquid(RoomStorage storage,RoomDocument snapshot,Task<string> previous,IDisposable write,Action<string> checkpoint) {
            try {
                if(previous!=null){var issue=await previous.ConfigureAwait(false);if(issue!=null)return storage.PreserveUnconfirmedEdit(issue);}
                return await RoomCheckpointSave.Start(storage,snapshot,checkpoint).ConfigureAwait(false);
            }catch(Exception){return storage.PreserveUnconfirmedEdit("Liquid checkpoint was not confirmed.");}
            finally{write.Dispose();}
        }
        bool AcceptLiquidContents(RoomObjectData[] changes,Action confirmed,out string error) {
            error=LiquidCheckpointWait;
            if(!LiquidCheckpointCapacity)return false;
            if(!CanSaveRoom){error="Room storage is unavailable; liquid changes were not accepted";return false;}
            var write=WriteGate.TryWrite(out error);if(write==null)return false;
            try {
                if(!journal.Prepare(changes,Array.Empty<string>(),out var edit,out error,scope:RoomEditScope.LiquidContents))return false;
                using(edit) {
                    // Capture the complete immutable candidate before accepting. No
                    // await or callback can separate this snapshot from acceptance.
                    var snapshot=TemporaryRoom?null:edit.Snapshot();
                    if(!edit.Accept(journal,out error))return false;
                    Task<string> completion;
                    if(TemporaryRoom){write.Dispose();write=null;completion=Task.FromResult<string>(null);}
                    else {
                        var targetStorage=storage;var previous=saveTask;var ownedWrite=write;var checkpoint=LiquidPublicationCheckpoint;
                        completion=Task.Run(()=>PersistLiquid(targetStorage,snapshot,previous,ownedWrite,checkpoint));
                        saveTask=completion;write=null;
                    }
                    liquidCheckpoints.Enqueue(new LiquidCheckpoint{Journal=journal,Completion=completion,Confirmed=confirmed,Targets=new HashSet<string>(edit.ChangedObjects,StringComparer.Ordinal)});
                    MarkDirty();if(!TemporaryRoom)dirty=false;
                    acceptingLiquidFlow=true;
                    try{ReconcileEdit(edit,null,false);}finally{acceptingLiquidFlow=false;}
                    SetStatus(TemporaryRoom?"Liquid flow accepted in the temporary room":"Liquid flow accepted — saving");
                    error=null;return true;
                }
            }finally{write?.Dispose();}
        }
        internal void PollLiquidCheckpoints() {
            if(notifyingLiquid||!isActiveAndEnabled||Ownership.Suspended||Liquids&&Liquids.Active)return;
            notifyingLiquid=true;
            try {
                while(liquidCheckpoints.Count>0&&liquidCheckpoints.Peek().Completion.IsCompleted) {
                    var current=liquidCheckpoints.Dequeue();string issue;
                    try{issue=current.Completion.GetAwaiter().GetResult();}catch(Exception){issue="Liquid saving did not confirm its outcome";}
                    if(!ReferenceEquals(current.Journal,journal))continue;
                    if(issue!=null){liquidPersistenceFailure=issue;liquidFailureJournal=journal;if(Liquids)Liquids.PublicationFailed(issue);SaveFailed(issue);continue;}
                    if(current.Current)current.Confirmed?.Invoke();
                    if(!this||!isActiveAndEnabled||Ownership.Suspended||Liquids&&Liquids.Active)break;
                }
                if(this&&!PendingLiquidSave&&Status=="Liquid flow accepted — saving")SetStatus("Liquid flow saved — Undo restores its quantities");
            }finally{notifyingLiquid=false;}
        }
    }
}
