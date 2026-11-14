// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Rules;
using UnityEngine;
namespace Maestro.Quest.Interaction {
    // Navigation owns placement; this lease supplies only full imported pose data.
    // No autonomous Update clock: a pose advances only with an accepted body step.
    internal sealed class ImportedObjectWalk : IDisposable {
        static readonly Dictionary<RoomItem,ImportedObjectWalk> active=new();
        readonly RoomEditor editor;readonly RoomItem item;readonly ImportedModel model;
        readonly RoomObjectData data;readonly ImportedMotionPose initial;readonly Transform hips;
        readonly Vector3 restHip;readonly float nominalSpeed;
        MotionLibrary.Lease motion;Task loading;bool disposed;float time;string error;
        internal static int ActiveOwners=>active.Count;
        internal string Status {get;private set;}="inactive";
        internal static bool Owns(RoomItem item)=>item&&active.ContainsKey(item);
        internal static string StatusFor(RoomItem item)=>item&&active.TryGetValue(item,out var value)?value.Status:"inactive";
        internal ImportedObjectWalk(RoomEditor editor,RoomItem item){
            Maestro.Quest.Avatar.HumanoidFootPlacement.RestoreFor(item);
            this.editor=editor;this.item=item;data=editor.Read(editor.Identity(item));model=AvatarWalkSelection.Model(editor,data);
            if(data?.kind!=RoomObjectKind.ImportedModel||data.walkClip==0&&string.IsNullOrEmpty(data.walkMotionId))return;
            active[item]=this;
            if(!AvatarWalkSelection.Loaded(editor,data,out _,out error)){Status="unavailable";return;}
            initial=new ImportedMotionPose(model,model.transform.localPosition);hips=model.Humanoid.GetBoneTransform(HumanBodyBones.Hips);
            restHip=item.transform.InverseTransformPoint(hips.position);
            nominalSpeed=Mathf.Max(.01f,.65f*model.LocalBounds.size.y*item.transform.lossyScale.y/1.7f);
            if(!string.IsNullOrEmpty(data.walkMotionId)){Status="loading";loading=Load();}else Status="ready";
        }
        async Task Load(){
            MotionLibrary.Lease acquired=null;
            try{
                acquired=await editor.Motions.AcquireAsync(data.walkMotionId,model.MotionRigHash);
                if(disposed||!model||!model.Ready||AvatarWalkSelection.Model(editor,data)!=model)return;
                motion=acquired;acquired=null;Status="ready";
            }catch(Exception){if(!disposed){error="The exact assigned walking motion could not be loaded; inspect or reimport it";Status="unavailable";}}
            finally{acquired?.Dispose();}
        }
        internal RuleActionState State(out string reason){
            reason=error;if(disposed){reason="Walking pose ownership ended";return RuleActionState.Failed;}
            if(initial==null)return error==null?RuleActionState.Ready:RuleActionState.Failed;
            if(!item||!model||!model.Ready||editor.Find(data.id)!=item||AvatarWalkSelection.Model(editor,data)!=model||model.AssetHash!=data.modelHash||model.PlaybackIssue!=null){reason="The walking model changed or became unavailable";return RuleActionState.Failed;}
            if(error!=null)return RuleActionState.Failed;
            return loading!=null&&!loading.IsCompleted?RuleActionState.Preparing:RuleActionState.Ready;
        }
        internal bool Apply(float speed,float dt){
            if(disposed||initial==null)return error==null;
            Maestro.Quest.Avatar.HumanoidFootPlacement.RestoreFor(item);
            initial.Restore();
            if(error!=null){Status="unavailable";return false;}
            if(speed<=.025f||dt<=0){Status=loading!=null&&!loading.IsCompleted?"loading":"idle";return true;}
            if(State(out var reason)!=RuleActionState.Ready){error=reason;return false;}
            time+=Mathf.Min(dt,.05f)*Mathf.Clamp(speed/nominalSpeed,.25f,2);
            bool sampled=motion!=null?model.SampleMotion(motion,time,true):model.SampleClip(data.walkClip-1,time,true);
            if(!sampled){error="The assigned walking pose could not be sampled";Status="unavailable";initial.Restore();return false;}
            // Cancel source planar travel in the object's frame. Preserve all other
            // nodes, vertical bounce and blend shapes, including non-editable bones.
            var displacement=item.transform.TransformPoint(restHip)-hips.position;
            model.transform.position+=Vector3.ProjectOnPlane(displacement,item.transform.up);
            Status="walking";return true;
        }
        public void Dispose(){
            if(disposed)return;Maestro.Quest.Avatar.HumanoidFootPlacement.RestoreFor(item);disposed=true;initial?.Restore();motion?.Dispose();motion=null;
            if(!ReferenceEquals(item,null)&&active.TryGetValue(item,out var value)&&ReferenceEquals(value,this))active.Remove(item);
            Status="inactive";
        }
    }
}
