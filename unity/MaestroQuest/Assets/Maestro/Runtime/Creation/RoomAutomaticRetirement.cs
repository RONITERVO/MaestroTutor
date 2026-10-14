// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
namespace Maestro.Quest.Creation {
    public sealed partial class RoomEditor {
        sealed class AutomaticRetirement {
            internal string Area;internal string[] Targets;internal Func<bool> Current;
            internal CancellationTokenSource Cancel;internal Task<string> Task;internal double Deadline;internal bool TimedOut,CancelledByOwner;
            internal void Stop(){if(Task?.IsCompleted==true)return;CancelledByOwner=true;try{Cancel.Cancel();}catch(ObjectDisposedException){}}
        }
        sealed class RetirementFailure {internal Func<bool> Current;internal string Issue;}
        bool automaticRetirement;
        AutomaticRetirement areaRetirement;
        readonly Dictionary<string,RetirementFailure> retirementFailures=new(StringComparer.Ordinal);
        double nextRetirement,lastRetirement;
        internal void ConfigureNativeRetirement(bool enabled=true){automaticRetirement=enabled;StopNativeRetirement();}
        void StopNativeRetirement(){areaRetirement?.Stop();retirementFailures.Clear();nextRetirement=lastRetirement=0;}
        // Missing tracking is not evidence that the user no longer needs content.
        // Every asynchronous preparation boundary rechecks this admission guard.
        bool RetirementViewReady() {
            var camera=ActiveViewerCamera();
            return automaticRetirement&&DisplayReady&&!RuntimeGate.Held&&Frame.Valid&&ViewerTrackingReady&&camera&&sampledViewer==camera
                &&ReferenceEquals(observationJournal,journal)&&viewerPrediction.Read(Time.unscaledTimeAsDouble,out _,out _)
                &&new RoomObservationView(camera).Reliable;
        }
        internal void TickNativeRetirement(double now) {
            if(!double.IsFinite(now))return;
            if(now<lastRetirement){StopNativeRetirement();}lastRetirement=now;
            if(areaRetirement!=null){
                var job=areaRetirement;
                if(!job.Task.IsCompleted){
                    if(now>=job.Deadline){job.TimedOut=true;job.Stop();}
                    if(!RetirementViewReady()||!job.Current())job.Stop();
                    return;
                }
                string issue=job.Task.IsFaulted||job.Task.IsCanceled?"Area retirement did not finish":job.Task.Result;
                if(job.TimedOut)issue="Area retirement timed out; the loaded objects were retained";
                if(issue!=null&&(!job.CancelledByOwner||job.TimedOut)&&job.Current()&&RetirementViewReady()){
                    var graph=ReadRetention();
                    if(graph!=null&&job.Targets.All(id=>graph.Reasons(id)==RoomRetentionReason.None))
                        retirementFailures[job.Area]=new(){Current=job.Current,Issue=NativeRetirementIssueText(issue)};
                }
                areaRetirement=null;nextRetirement=now+.5;
            }
            if(!RetirementViewReady()||now<nextRetirement||NativeDisplayPending||!NativeTransitionAvailable(out _))return;
            nextRetirement=now+.5;
            var retention=ReadRetention();if(retention==null)return;
            foreach(var area in retirementFailures.Keys.ToArray()){
                var members=retention.Members(area);
                if(!retirementFailures[area].Current()||members==null||members.Any(id=>retention.Reasons(id)!=RoomRetentionReason.None))retirementFailures.Remove(area);
            }
            foreach(var area in Regions().Select(x=>x.id).Append("").OrderBy(x=>x,StringComparer.Ordinal)){
                var members=retention.Members(area);if(members==null||members.Length==0||retirementFailures.ContainsKey(area))continue;
                var targets=retention.Closure(members);
                if(targets.All(NativeEntityDormant)||targets.Any(id=>retention.Reasons(id)!=RoomRetentionReason.None))continue;
                var job=new AutomaticRetirement{Area=area,Targets=targets,Current=CaptureNativeClosureInput(targets),Cancel=new(),Deadline=now+NativeDisplayTimeout};
                areaRetirement=job;
                job.Task=RetireNativeArea(area,job.Cancel.Token,()=>RetirementViewReady()&&job.Current()&&Time.unscaledTimeAsDouble<job.Deadline);
                _=job.Task.ContinueWith(task=>{_=task.Exception;job.Cancel.Dispose();},CancellationToken.None,TaskContinuationOptions.None,TaskScheduler.Default);
                return; // One existing preparation lane; never a second object evaluator.
            }
        }
        internal static string NativeRetirementIssueText(string issue)=>new((issue??"").Take(128).Select(c=>char.IsControl(c)?' ':c).ToArray());
        string NativeRetirementState(string area,string[] members,RoomRetentionReason reasons) {
            if(!automaticRetirement)return "disabled";
            if(areaRetirement?.Task?.IsCompleted==false&&members.Any(id=>areaRetirement.Targets.Contains(id)))return "preparing";
            if(retirementFailures.TryGetValue(area,out var failure)&&failure.Current())return "failed";
            if(members.Length>0&&members.All(NativeEntityDormant))return "dormant";
            if(reasons!=RoomRetentionReason.None)return "retained";
            return RetirementViewReady()?"ready":"waiting";
        }
        string NativeRetirementIssue(string area)=>retirementFailures.TryGetValue(area,out var failure)&&failure.Current()?failure.Issue:"";
    }
}
