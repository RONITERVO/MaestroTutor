// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Threading.Tasks;
using Maestro.Quest.Persistence;
namespace Maestro.Quest.Rules
{
    public sealed partial class RuleWorkshop
    {
        LatestCheckpoint<RuleDocument> lifecycleSave;
        internal Newtonsoft.Json.Linq.JObject ObserveCheckpoint()=>Programs.RoomPersistenceFact.Observe(
            storage!=null&&!storage.ReadOnly,saveTask!=null,dirty,saveDispatch.Frozen,
            storage?.ReadOnly==true?"Behaviour storage is unavailable; original files are preserved.":lastSaveError);
        internal Task LifecycleCompletion=>(Task)saveTask??Task.CompletedTask;
        internal Task QueueLifecycleSave()
        {
            Modules?.Poll();CompleteSave();
            if(saveDispatch.Frozen||storage==null||storage.ReadOnly||!dirty)return (Task)saveTask??Task.CompletedTask;
            var snapshot=document.Copy();var target=storage;
            if(lifecycleSave==null||!ReferenceEquals(saveTask,lifecycleSave.Completion)||!lifecycleSave.TryQueue(snapshot))
                lifecycleSave=new LatestCheckpoint<RuleDocument>(saveTask,snapshot,value=>SaveSnapshot(target,value));
            saveTask=lifecycleSave.Completion;dirty=false;
            return saveTask;
        }
    }
}
