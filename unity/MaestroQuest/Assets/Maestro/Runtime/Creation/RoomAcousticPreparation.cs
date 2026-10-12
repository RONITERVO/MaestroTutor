// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Quest.Book;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        Task acousticPreparation;CancellationTokenSource acousticCancellation;
        Func<bool> acousticCurrent;double acousticDeadline;bool acousticTimedOut;
        internal bool AcousticPreparationPending=>acousticPreparation!=null&&!acousticPreparation.IsCompleted;
        internal string AcousticPreparationProblem=>dormantNative.Where(pair=>!string.IsNullOrEmpty(pair.Value.AcousticIssue)).Select(pair=>"Saved object "+pair.Key+": "+pair.Value.AcousticIssue).FirstOrDefault();
        internal string AcousticPreparationIssue(string id)=>dormantNative.TryGetValue(id,out var entry)?entry.AcousticIssue:null;
        void StopAcousticPreparation(){acousticCancellation?.Cancel();}
        void ReleaseNativeAcoustic(DormantEntity entry){
            if(entry.AcousticPendingOwner)entry.AcousticPendingOwner.SetPreparing(entry,null);entry.AcousticPendingOwner=null;
            entry.Acoustics?.Dispose();entry.Acoustics=null;
        }
        internal void TickNativeAcoustics()
        {
            if(acousticPreparation!=null){
                if(acousticPreparation.IsCompleted){_=acousticPreparation.Exception;acousticPreparation=null;acousticCancellation?.Dispose();acousticCancellation=null;acousticCurrent=null;}
                else if(acousticCurrent?.Invoke()!=true)StopAcousticPreparation();
                else if(Time.realtimeSinceStartupAsDouble>acousticDeadline){acousticTimedOut=true;StopAcousticPreparation();}
            }
            if(journal==null||!DisplayReady)return;
            var owner=GetComponentInParent<RoomAcoustics>();if(!owner)return;
            foreach(var pair in dormantNative){
                var entry=pair.Value;var phase=NativePhase(pair.Key);
                if(entry.Acoustics?.Published==true){if(entry.AcousticPendingOwner)entry.AcousticPendingOwner.SetPreparing(entry,null);entry.AcousticPendingOwner=null;continue;}
                int revision=ObjectRevision(pair.Key);RoomObjectData data=null;
                if(entry.AcousticMetadataRevision!=revision){data=Read(pair.Key);if(data==null)continue;entry.AcousticBoundary=data.physics==ItemPhysics.Fixed;entry.AcousticMetadataRevision=revision;}
                if(entry.AcousticPendingOwner&&entry.AcousticPendingOwner!=owner)entry.AcousticPendingOwner.SetPreparing(entry,null);
                owner.SetPreparing(entry,entry.AcousticBoundary);entry.AcousticPendingOwner=owner;
                if(acousticPreparation!=null||entry.Acoustics?.Current==true||phase is NativeEntityPhase.Preparing or NativeEntityPhase.Retiring
                    ||startupTargets.Contains(pair.Key)&&phase!=NativeEntityPhase.Failed)continue;
                if(entry.AcousticAttempt==revision&&(entry.AcousticCapacity<0||entry.AcousticCapacity==owner.ResidencyEpoch))continue;
                entry.Acoustics?.Dispose();entry.Acoustics=null;entry.AcousticAttempt=revision;entry.AcousticCapacity=-1;entry.AcousticIssue=null;
                data??=Read(pair.Key);acousticDeadline=Time.realtimeSinceStartupAsDouble+120;acousticTimedOut=false;acousticCancellation=new CancellationTokenSource();acousticPreparation=PrepareNativeAcoustics(pair.Key,entry,data,owner,revision,acousticCancellation.Token);
            }
        }
        async Task PrepareNativeAcoustics(string id,DormantEntity entry,RoomObjectData data,RoomAcoustics owner,int revision,CancellationToken cancellation)
        {
            var original=journal;var source=SpatialSource(data.Copy());
            bool Current()=>this&&DisplayReady&&owner&&ReferenceEquals(journal,original)&&dormantNative.TryGetValue(id,out var current)&&ReferenceEquals(current,entry)
                &&(entry.Phase is NativeEntityPhase.Dormant or NativeEntityPhase.Failed)&&ObjectRevision(id)==revision
                &&(!startupTargets.Contains(id)||entry.Phase==NativeEntityPhase.Failed);
            acousticCurrent=Current;
            void Check(){cancellation.ThrowIfCancellationRequested();if(!Current())throw new OperationCanceledException();}
            try{
                Check();var budget=new RoomPreparationBudget(Check,nativePreparationWindow);
                var inputs=await RoomAcousticGeometry.Prepare(data,entry.Recipe,Models,budget,cancellation);Check();
                var pose=NativeActivationPose(data.Copy());
                if(!owner.TryPrepareInputs(inputs,transform,Matrix4x4.TRS(pose.position,pose.rotation,Vector3.one*pose.scale),data.physics==ItemPhysics.Fixed,out var prepared,out var error)){
                    entry.AcousticIssue=error;entry.AcousticCapacity=owner.ResidencyEpoch;return;
                }
                try{
                    Check();if(!prepared.Commit())throw new OperationCanceledException();entry.Acoustics=prepared;entry.AcousticSource=source;entry.AcousticRevision=revision;prepared=null;
                }finally{prepared?.Dispose();}
            }catch(OperationCanceledException){
                if(acousticTimedOut&&Current())entry.AcousticIssue="Acoustic preparation timed out; edit or reload the object to retry";
                else if(this&&dormantNative.TryGetValue(id,out var current)&&ReferenceEquals(current,entry))entry.AcousticAttempt=-1;
            }
            catch(Exception failure){if(Current())entry.AcousticIssue=failure.Message;}
        }
    }
}
