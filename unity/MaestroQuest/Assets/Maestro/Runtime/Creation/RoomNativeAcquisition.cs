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
            internal Func<bool> Admission,InputCurrent;
            internal CancellationToken Cancellation;
            internal bool Active=>!Cancellation.IsCancellationRequested&&Admission()&&InputCurrent();
        }
        sealed class NativeAcquisition
        {
            internal readonly List<NativeInterest> Interests=new();
            internal readonly CancellationTokenSource Cancellation=new();
            internal string[] Closure;internal Func<bool> InputCurrent;
            internal Task<string> Work;internal bool Closed;
            internal bool Interested=>Interests.Any(value=>value.Active);
            internal void Cancel(){if(!Closed)Cancellation.Cancel();}
        }
        readonly List<NativeAcquisition> nativeAcquisitions=new();
        readonly RoomPreparationBudget.Window nativePreparationWindow=new();
        bool NativeAcquisitionAvailable(out string error)
        {
            if(!CanEditStructures(out error))return false;
            error=!isActiveAndEnabled||applying?"Wait until the room is available for loading objects":null;
            return error==null;
        }
        internal async Task<string> AcquireNativeEntities(string[] targets,Func<bool> actionAdmission,CancellationToken cancellation)
        {
            var graph=journal?.RetentionGraph();
            if(graph==null||targets.Any(id=>!graph.Contains(id)))return "A required saved object is missing";
            var closure=graph.Closure(targets);
            if(!closure.Any(NativeEntityDormant))return null;
            if(actionAdmission==null)return "Native activation has no action owner";
            if(!NativeAcquisitionAvailable(out var error))return error;
            var interest=new NativeInterest{Admission=actionAdmission,InputCurrent=CaptureNativeActionInput(targets),Cancellation=cancellation};
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
            void RuntimeChanged(){if(RuntimeGate.Held)job.Cancel();}
            RuntimeGate.Changed+=RuntimeChanged;
            bool Current()=>this&&isActiveAndEnabled&&CanEditStructures(out _)&&!job.Cancellation.IsCancellationRequested&&job.InputCurrent()&&job.Interested;
            try{
                // All jobs drain the same preparation lane, so more requesting
                // actions cannot multiply the cooperative work budget in a frame.
                while(nativeAcquisitions[0]!=job||nativeActivation!=null){
                    if(!Current())return "Area activation was cancelled or its saved input changed";
                    await Task.Yield();
                }
                if(!Current())return "Area activation was cancelled or its saved input changed";
                return await ActivateNativeTargets(job.Closure,job.Cancellation.Token,Current);
            }finally{
                RuntimeGate.Changed-=RuntimeChanged;nativeAcquisitions.Remove(job);job.Closed=true;job.Cancellation.Dispose();
            }
        }
    }
}
