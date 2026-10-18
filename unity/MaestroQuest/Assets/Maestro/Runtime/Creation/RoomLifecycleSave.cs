// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Threading.Tasks;
using Maestro.Quest.Persistence;
using Maestro.Quest.Interaction;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        LatestCheckpoint<RoomDocument> lifecycleSave;
        internal Newtonsoft.Json.Linq.JObject ObserveCheckpoint()=>Programs.RoomPersistenceFact.Observe(
            storage!=null&&!storage.ReadOnly,TemporaryRoom?TemporarySavePending:saveTask!=null,dirty,saveDispatch.Frozen,
            storage?.ReadOnly==true?storage.UnavailableReason:TemporaryRoom?TemporarySaveError:lastSaveError,TemporaryRoom);
        internal Task LifecycleCompletion=>(TemporaryRoom?(Task)temporarySave:saveTask)??Task.CompletedTask;
        // Lifecycle callbacks never wait for disk. The same saveTask is observed
        // by ordinary updates, prepared edits, recovery and workspace retirement.
        internal Task QueueLifecycleSave()
        {
            if(TemporaryRoom){
                // Only an explicitly dispatched baseline/Keep may reach disk.
                // Do not snapshot or promote the later temporary fork.
                CompleteTemporarySave();
                return (Task)temporarySave??Task.CompletedTask;
            }
            if(!FinishLiquidPour(out var issue)){if(issue!=null)SetStatus(issue);return (Task)saveTask??Task.CompletedTask;}
            if(saveDispatch.Frozen||journal==null||storage==null)return (Task)saveTask??Task.CompletedTask;
            GetComponent<WorkspaceViewpoint>()?.Capture();CapturePhysicsPlacements();CompleteSave();
            if(storage.ReadOnly||!dirty)return (Task)saveTask??Task.CompletedTask;
            var snapshot=journal.Snapshot();var target=storage;
            if(lifecycleSave==null||!ReferenceEquals(saveTask,lifecycleSave.Completion)||!lifecycleSave.TryQueue(snapshot))
                lifecycleSave=new LatestCheckpoint<RoomDocument>(saveTask,snapshot,value=>SaveSnapshot(target,value));
            saveTask=lifecycleSave.Completion;dirty=false;
            return saveTask;
        }
    }
}
