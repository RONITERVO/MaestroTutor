// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Maestro.Quest.Avatar;
using Maestro.Quest.Imports;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Interaction {
    // Movement owns the root. This optional exact source pose owns the imported
    // skeleton; an explicit source-node anchor removes translation on all axes.
    // No humanoid subset or implicit walking clip participates.
    internal sealed class ImportedLocomotionPose:IDisposable {
        readonly RoomItem item;readonly ImportedModel model;readonly JObject selection;
        readonly ImportedMotionPose initial;readonly Transform anchor;readonly Vector3 rest;
        readonly HumanoidRetargeter retargeter;readonly bool retargeterEnabled;
        MotionLibrary.Lease lease;Task loading;bool disposed;string failure;float time;
        internal static bool Selected(JObject value)=>value!=null&&(string)value["source"]!="none";
        internal static bool Valid(RoomEditor editor,RoomItem item,JObject value,out string error){
            error=null;if(!Selected(value))return true;
            var model=AnimationTargets.ClipModel(item);
            if(!model||!model.Ready||model.AssetHash!=(string)value["modelHash"]||model.PlaybackIssue!=null){
                error="The exact gait model must be loaded and playable";return false;
            }
            int index=(int)value["anchorNode"];
            if(index<0||index>=model.Instance.Nodes.Count||!model.Instance.Nodes[index]){
                error="The gait root anchor is not present in this exact model";return false;
            }
            if((string)value["source"]=="embedded"){
                index=(int)value["clip"];if(index>=0&&index<model.ClipCount&&model.ClipDuration(index)>0)return true;
                error="The exact embedded gait clip is unavailable";return false;
            }
            var motion=editor.Motions?.Find((string)value["motionId"]);
            if(motion==null||string.IsNullOrEmpty(model.MotionRigHash)||motion.rigHash!=model.MotionRigHash){
                error="The exact gait motion needs a matching model rig and rest pose";return false;
            }
            return true;
        }
        internal ImportedLocomotionPose(RoomEditor editor,RoomItem item,JObject value){
            this.item=item;selection=value==null?null:(JObject)value.DeepClone();
            if(!Selected(value))return;
            model=AnimationTargets.ClipModel(item);initial=new(model,model.transform.localPosition);
            retargeter=model.GetComponent<HumanoidRetargeter>();if(retargeter){retargeterEnabled=retargeter.enabled;retargeter.enabled=false;}
            anchor=model.Instance.Nodes[(int)value["anchorNode"]];rest=item.transform.InverseTransformPoint(anchor.position);
            if((string)value["source"]=="library")loading=Load(editor,(string)value["motionId"]);
        }
        async Task Load(RoomEditor editor,string id){
            MotionLibrary.Lease acquired=null;
            try{acquired=await editor.Motions.AcquireAsync(id,model.MotionRigHash);if(!disposed){lease=acquired;acquired=null;}}
            catch(Exception){if(!disposed)failure="The exact gait motion could not be loaded; inspect or reimport it";}
            finally{acquired?.Dispose();}
        }
        internal RuleActionState State(out string error){
            error=failure;if(disposed){error="The gait owner ended";return RuleActionState.Failed;}
            if(initial==null)return RuleActionState.Ready;
            if(!item||!model||!model.Ready||AnimationTargets.ClipModel(item)!=model||model.AssetHash!=(string)selection["modelHash"]||!anchor||model.PlaybackIssue!=null){
                error="The gait model or root anchor became unavailable";return RuleActionState.Failed;
            }
            if(error!=null)return RuleActionState.Failed;
            if(loading!=null&&!loading.IsCompleted)return RuleActionState.Preparing;
            if((string)selection["source"]=="library"&&lease==null){error="The exact gait motion is unavailable";return RuleActionState.Failed;}
            return RuleActionState.Ready;
        }
        internal bool Apply(float delta,out string error){
            error=null;if(initial==null)return true;
            if(State(out error)!=RuleActionState.Ready)return false;
            initial.Restore();time+=Mathf.Clamp(delta,0,.05f)*(float)selection["rate"];
            bool sampled=lease!=null?model.SampleMotion(lease,time,true):model.SampleClip((int)selection["clip"],time,true);
            if(!sampled){initial.Restore();error="The exact gait pose could not be sampled";return false;}
            model.transform.position+=item.transform.TransformPoint(rest)-anchor.position;
            return true;
        }
        public void Dispose(){if(disposed)return;disposed=true;initial?.Restore();if(retargeter)retargeter.enabled=retargeterEnabled;lease?.Dispose();lease=null;}
    }
}
