// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        sealed class NativeInterest
        {
            internal Func<bool> Admission,InputCurrent,RuntimeAvailable;
            internal bool DisplayDuringHold;
            internal CancellationToken Cancellation;
            internal bool Active=>!Cancellation.IsCancellationRequested&&RuntimeAvailable()&&Admission()&&InputCurrent();
        }
        sealed class NativeAcquisition
        {
            internal readonly List<NativeInterest> Interests=new();
            internal readonly CancellationTokenSource Cancellation=new();
            internal string[] Closure;internal Func<bool> InputCurrent;
            internal Task<string> Work;internal bool Closed;
            internal bool Interested=>Interests.Any(value=>value.Active);
            internal bool DisplayDuringHold=>Interests.Any(value=>value.DisplayDuringHold&&value.Active);
            internal void Cancel(){if(!Closed)Cancellation.Cancel();}
        }
        readonly List<NativeAcquisition> nativeAcquisitions=new();
        readonly RoomPreparationBudget.Window nativePreparationWindow=new();
        bool NativePreparationPermitted(out string error,bool displayDuringHold)
        {
            if(!displayDuringHold)return CanEditStructures(out error);
            else{
                // Display preparation may run during workspace review, but it
                // cannot cross a write freeze, retired workspace or missing store.
                error="Wait until the room is available for displaying saved objects";
                if(journal==null||storage.ReadOnly||WriteGate.Frozen)return false;
            }
            error=null;return true;
        }
        bool NativeAcquisitionAvailable(out string error,bool displayDuringHold=false)
        {
            if(!NativePreparationPermitted(out error,displayDuringHold))return false;
            error=!isActiveAndEnabled||applying?"Wait until the room is available for loading objects":null;
            return error==null;
        }
        internal async Task<string> AcquireNativeEntities(string[] targets,Func<bool> actionAdmission,CancellationToken cancellation,bool displayDuringHold=false)
        {
            var graph=journal?.RetentionTopology;
            if(graph==null||targets.Any(id=>!graph.Contains(id)))return "A required saved object is missing";
            var closure=graph.Closure(targets);
            if(!closure.Any(NativeEntityDormant))return null;
            if(actionAdmission==null)return "Native activation has no action owner";
            if(!NativeAcquisitionAvailable(out var error,displayDuringHold))return error;
            var interest=new NativeInterest{Admission=actionAdmission,InputCurrent=CaptureNativeActionInput(targets),Cancellation=cancellation,DisplayDuringHold=displayDuringHold,RuntimeAvailable=()=>displayDuringHold||!RuntimeGate.Held};
            if(!interest.Active)return "Area activation was cancelled or its saved input changed";
            // Equal connected closures share one load. Overlapping unequal requests
            // queue safely and re-evaluate which entities still need preparation.
            var job=nativeAcquisitions.FirstOrDefault(value=>!value.Closed&&!value.Cancellation.IsCancellationRequested&&value.InputCurrent()&&value.Closure.SequenceEqual(closure));
            bool created=job==null;
            if(created){job=new NativeAcquisition{Closure=closure,InputCurrent=CaptureNativeClosureInput(closure)};nativeAcquisitions.Add(job);}
            job.Interests.Add(interest);
            if(created){job.Work=RunNativeAcquisition(job);_ = job.Work.ContinueWith(task=>{_ = task.Exception;},TaskScheduler.Default);}
            try{
                while(!job.Work.IsCompleted){
                    if(!interest.Active)return "Area activation was cancelled or its saved input changed";
                    await Task.Yield();
                }
                var result=await job.Work;
                return interest.Active?result:"Area activation was cancelled or its saved input changed";
            }finally{
                job.Interests.Remove(interest);
                if(!job.Interested)job.Cancel();
            }
        }
        async Task<string> RunNativeAcquisition(NativeAcquisition job)
        {
            void RuntimeChanged(){if(RuntimeGate.Held&&!job.DisplayDuringHold)job.Cancel();}
            RuntimeGate.Changed+=RuntimeChanged;
            bool Current()=>this&&isActiveAndEnabled&&NativePreparationPermitted(out _,job.DisplayDuringHold)&&!job.Cancellation.IsCancellationRequested&&job.InputCurrent()&&job.Interested;
            try{
                // All jobs drain the same preparation lane, so more requesting
                // actions cannot multiply the cooperative work budget in a frame.
                while(nativeAcquisitions[0]!=job||nativeActivation!=null){
                    if(!Current())return "Area activation was cancelled or its saved input changed";
                    await Task.Yield();
                }
                if(!Current())return "Area activation was cancelled or its saved input changed";
                return await ActivateNativeTargets(job.Closure,job.Cancellation.Token,Current,()=>job.DisplayDuringHold);
            }finally{
                RuntimeGate.Changed-=RuntimeChanged;nativeAcquisitions.Remove(job);job.Closed=true;job.Cancellation.Dispose();
            }
        }
    }
}
