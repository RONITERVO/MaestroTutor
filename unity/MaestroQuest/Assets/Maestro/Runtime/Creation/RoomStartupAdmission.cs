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
        // This owner admits the current bounded saved room, using exactly the
        // action preparation lane. Regional selection is a later policy; never
        // infer that an unseen area is dispensable before all dependencies exist.
        readonly HashSet<string> startupTargets=new(StringComparer.Ordinal);
        RoomJournal startupJournal;Task<string> startupAdmission;int startupVersion;
        double startupDeadline=double.PositiveInfinity;string[] startupAttempt=Array.Empty<string>();
        internal const double NativeStartupTimeout=120;
        internal bool NativeStartupPending=>startupTargets.Count>0||startupAdmission!=null;
        void InitializeNativeStartup(RoomDocument document)
        {
            nativeJournal=journal;startupJournal=journal;
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
        void InvalidateNativeStartup(){startupVersion++;if(RuntimeGate.Held)foreach(var id in startupTargets)if(dormantNative.TryGetValue(id,out var entry))entry.SuppressStartupAutoplay=true;}
        void ForkNativeStartup(RoomJournal previous){if(ReferenceEquals(startupJournal,previous)){InvalidateNativeStartup();startupJournal=journal;}}
        bool StartupReady=>this&&isActiveAndEnabled&&!ownershipPaused&&ownershipFocused&&!WriteGate.Frozen;
        internal void TickNativeStartup()=>TickNativeStartup(Time.unscaledTimeAsDouble);
        internal void TickNativeStartup(double now)
        {
            if(!double.IsFinite(now))return;
            if(startupAdmission!=null){
                if(now>=startupDeadline){
                    InvalidateNativeStartup();startupTargets.ExceptWith(startupAttempt);startupDeadline=double.PositiveInfinity;
                    if(startupAttempt.Any(NativeEntityDormant))SetStatus("Opening saved objects timed out; inspect the affected area and retry its action");
                }
                if(!startupAdmission.IsCompleted)return;
                if(startupAdmission.IsFaulted||startupAdmission.IsCanceled)_=startupAdmission.Exception;
                startupAdmission=null;
            }
            if(!ReferenceEquals(startupJournal,journal)){startupTargets.Clear();startupJournal=null;return;}
            // A failed load remains inspectable and explicitly retryable. Do not
            // keep hammering a missing/invalid asset on every frame.
            startupTargets.RemoveWhere(id=>!HasSavedObject(id)||!NativeEntityDormant(id)||NativePhase(id)==NativeEntityPhase.Failed);
            if(startupTargets.Count==0||!StartupReady||!NativeAcquisitionAvailable(out _,displayDuringHold:true))return;
            int version=startupVersion;var source=journal;double deadline=now+NativeStartupTimeout;startupDeadline=deadline;
            bool Current()=>StartupReady&&Time.unscaledTimeAsDouble<deadline&&startupVersion==version&&ReferenceEquals(journal,source)&&ReferenceEquals(startupJournal,source);
            // Connected area members publish together. Separate areas can finish
            // independently, so one missing import does not hide the entire room.
            string target=startupTargets.OrderBy(id=>id,StringComparer.Ordinal).First();
            startupAttempt=journal.RetentionGraph().Closure(new[]{target});
            startupAdmission=AcquireNativeEntities(new[]{target},Current,CancellationToken.None,displayDuringHold:true);
            _=startupAdmission.ContinueWith(task=>{_=task.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);
        }
    }
}
