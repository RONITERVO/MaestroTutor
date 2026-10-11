// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        // Startup and observed reappearance share one display owner and exactly
        // the action preparation lane. Admission never authorizes retirement.
        readonly HashSet<string> startupTargets=new(StringComparer.Ordinal);
        readonly HashSet<string> suppressedDisplayTargets=new(StringComparer.Ordinal);
        RoomJournal displayJournal;Task<string> displayAdmission;int displayVersion;
        double displayDeadline=double.PositiveInfinity;string[] displayAttempt=Array.Empty<string>();
        internal const double NativeDisplayTimeout=120;
        internal bool NativeDisplayPending=>startupTargets.Count>0||displayAdmission!=null;
        void InitializeNativeStartup(RoomDocument document)
        {
            nativeJournal=journal;displayJournal=journal;
            foreach(var data in document.objects.Where(data=>!data.IsBuiltIn)){
                dormantNative.Add(data.id,new DormantEntity{
                    SavedPosition=data.position,SavedRotation=data.rotation,SavedScale=data.scale,
                    Position=data.position,Rotation=data.rotation,Scale=data.scale,Phase=NativeEntityPhase.Dormant,
                    Spatial=RoomSpatialBounds.Unknown,SpatialRevision=ObjectRevision(data.id),SuppressStartupAutoplay=RuntimeGate.Held,
                    SpatialPlacement=Matrix4x4.TRS(data.position,data.rotation.normalized,Vector3.one*data.scale)
                });
                startupTargets.Add(data.id);
            }
        }
        void InvalidateNativeDisplay(){displayVersion++;ResetNativeDisplayDemand();if(RuntimeGate.Held)foreach(var id in startupTargets)if(dormantNative.TryGetValue(id,out var entry))entry.SuppressStartupAutoplay=true;}
        void ForkNativeDisplay(RoomJournal previous){if(ReferenceEquals(displayJournal,previous)){InvalidateNativeDisplay();displayJournal=journal;}}
        bool DisplayReady=>this&&isActiveAndEnabled&&!ownershipPaused&&ownershipFocused&&!WriteGate.Frozen;
        internal void TickNativeDisplay()=>TickNativeDisplay(Time.unscaledTimeAsDouble);
        internal void TickNativeDisplay(double now)
        {
            if(!double.IsFinite(now)||journal==null)return;
            if(!ReferenceEquals(displayJournal,journal)){
                InvalidateNativeDisplay();startupTargets.Clear();suppressedDisplayTargets.Clear();displayJournal=journal;
            }
            RefreshNativeDisplayDemand(now);
            if(displayAdmission!=null){
                if(now>=displayDeadline){
                    InvalidateNativeDisplay();startupTargets.ExceptWith(displayAttempt);suppressedDisplayTargets.UnionWith(displayAttempt);displayDeadline=double.PositiveInfinity;
                    if(displayAttempt.Any(NativeEntityDormant))SetStatus("Opening saved objects timed out; inspect the affected area and retry its action");
                }
                if(!displayAdmission.IsCompleted)return;
                if(displayAdmission.IsFaulted){_=displayAdmission.Exception;suppressedDisplayTargets.UnionWith(displayAttempt);startupTargets.ExceptWith(displayAttempt);}
                displayAdmission=null;
            }
            if(dormantNative.Count==0){startupTargets.Clear();suppressedDisplayTargets.Clear();return;}
            // Failure and timeout remain inspectable and explicitly retryable;
            // keeping a broken object in view cannot create a per-frame retry loop.
            startupTargets.RemoveWhere(id=>!HasSavedObject(id)||!NativeEntityDormant(id)||NativePhase(id)==NativeEntityPhase.Failed);
            suppressedDisplayTargets.RemoveWhere(id=>!HasSavedObject(id)||!NativeEntityDormant(id));
            if(dormantNative.Count==0||!DisplayReady||!NativeAcquisitionAvailable(out _,displayDuringHold:true))return;
            BeginNativeDisplay(now);
        }
        void BeginNativeDisplay(double now)
        {
            string target=dormantNative.Keys.Where(id=>!suppressedDisplayTargets.Contains(id)&&NativePhase(id)!=NativeEntityPhase.Failed&&(startupTargets.Contains(id)||NativeDisplayPriority(id,now)<4))
                .OrderBy(id=>NativeDisplayPriority(id,now)).ThenBy(id=>id,StringComparer.Ordinal).FirstOrDefault();
            if(target==null)return;
            int version=displayVersion;var source=journal;double deadline=now+NativeDisplayTimeout;displayDeadline=deadline;
            var attempt=journal.RetentionGraph().Closure(new[]{target});displayAttempt=attempt;
            bool Current()=>DisplayReady&&Time.unscaledTimeAsDouble<deadline&&displayVersion==version&&ReferenceEquals(journal,source)&&ReferenceEquals(displayJournal,source)
                &&attempt.Any(id=>startupTargets.Contains(id)||NativeDisplayPriority(id,Time.unscaledTimeAsDouble)<4);
            displayAdmission=AcquireNativeEntities(new[]{target},Current,CancellationToken.None,displayDuringHold:true);
            _=displayAdmission.ContinueWith(task=>{_=task.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);
        }
    }
}
