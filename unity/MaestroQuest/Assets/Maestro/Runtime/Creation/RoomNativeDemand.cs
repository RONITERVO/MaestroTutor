// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace Maestro.Quest.Creation
{
    // Owns residency, not an editing/animation channel. It spans preparation AND
    // the effect, so another consumer cannot retire successfully loaded inputs.
    internal sealed class RoomNativeDemand : IDisposable
    {
        readonly CancellationTokenSource cancellation=new();
        readonly Func<bool> current;
        readonly Action release;
        internal Task<string> Preparation {get;}
        bool disposed;
        internal RoomNativeDemand(RoomEditor editor,string[] targets,Func<bool> current,Func<bool> admission,Action release)
        {
            this.current=current;this.release=release;
            Preparation=editor.AcquireNativeEntities(targets,()=>!disposed&&current()&&(admission?.Invoke()??false),cancellation.Token);
        }
        internal string Error {
            get {
                if(disposed)return "The action was cancelled";
                if(!Preparation.IsCompleted)return "Required objects are still loading";
                if(Preparation.IsFaulted||Preparation.IsCanceled){_ = Preparation.Exception;return "Required objects could not be loaded";}
                return Preparation.Result??(current()?null:"Required objects or spatial dependencies changed before the action could start; inspect their latest state and retry");
            }
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;release();
            if(Preparation.IsCompleted)cancellation.Dispose();
            else {cancellation.Cancel();_ = Preparation.ContinueWith(task=>{_ = task.Exception;cancellation.Dispose();},TaskScheduler.Default);}
        }
    }
    public sealed partial class RoomEditor
    {
        sealed class NativeResidencyOwner
        {
            internal RoomJournal Journal;internal int Generation;internal string[] Targets;
        }
        readonly HashSet<NativeResidencyOwner> nativeResidencyOwners=new();
        internal RoomNativeDemand DemandNativeEntities(string[] targets,Func<IEnumerable<string>> latestTargets,Func<bool> admission)
        {
            var captured=CaptureNativeActionInput(targets);
            var held=targets.ToHashSet(StringComparer.Ordinal);
            // A conservative unknown envelope may narrow after loading. It may
            // shed dependencies, but newly relevant entities require a new action.
            bool Current()=>captured()&&(latestTargets==null||latestTargets().All(held.Contains));
            var owner=new NativeResidencyOwner{Journal=journal,Generation=nativeGeneration,Targets=targets};
            nativeResidencyOwners.Add(owner);
            try{return new RoomNativeDemand(this,targets,Current,admission,()=>nativeResidencyOwners.Remove(owner));}
            catch{nativeResidencyOwners.Remove(owner);throw;}
        }
        void RetainNativeActionInputs(RoomRetentionGraph graph)
        {
            foreach(var owner in nativeResidencyOwners)
                if(ReferenceEquals(journal,owner.Journal)&&nativeGeneration==owner.Generation)
                    foreach(var target in owner.Targets)graph.Retain(target,RoomRetentionReason.Ownership);
        }
    }
}
