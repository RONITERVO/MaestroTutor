// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading.Tasks;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        delegate bool ResolveSurface(out Vector3 position,out JObject receipt,out string error);
        string SurfaceFrameIssue(ScannedRoom scan,Matrix4x4 frame)
        {
            if(!this||!isActiveAndEnabled||!scan||!scan.isActiveAndEnabled||!Frame.Valid)
                return "The placement workspace is unavailable";
            return transform.localToWorldMatrix.Equals(frame)?null:"Room coordinates changed while placing; inspect the surface and request placement again";
        }
        bool StartResolvedSurfacePlacement(string target,int revision,Func<string> dependencies,ResolveSurface resolve,
            out CapabilityOperation operation,out string error,Action<string> checkpoint)
        {
            PendingPersistedEdit saved=null;JObject receipt=null;
            bool Validate(out string issue){
                issue=dependencies();if(issue!=null)return false;
                if(ObjectRevision(target)!=revision){issue="This object changed while placement was preparing";return false;}
                return CanEditObject(target,false,out issue);
            }
            bool Begin(out PendingPersistedEdit pending,out string issue){
                pending=null;
                if(!resolve(out var position,out var captured,out issue))return false;
                if(!BeginMove(target,position,out pending,out issue,checkpoint,dependencies))return false;
                saved=pending;receipt=(JObject)captured.DeepClone();return true;
            }
            JObject Result(){
                if(receipt==null)return new JObject();
                var value=(JObject)receipt.DeepClone();
                // A no-op has no replacement entry; retain its already observed revision.
                int accepted=saved?.AcceptedObjectRevision(target)??0;
                value["revision"]=accepted==0?revision:accepted;return value;
            }
            return PersistedEditOperation.Start(this,target,Begin,out operation,out error,Validate,Result);
        }
        internal bool StartScanPlacement(ScannedRoom scan,string target,int revision,string identity,string anchor,float x,float y,
            out CapabilityOperation operation,out string error,Action<string> checkpoint=null)
        {
            var frame=transform.localToWorldMatrix;
            string Dependencies()=>SurfaceFrameIssue(scan,frame)??(scan.CanUsePlacementLayout(transform,identity,out var issue)?null:issue);
            bool Resolve(out Vector3 position,out JObject receipt,out string issue)=>
                scan.PrepareScanPlacement(this,target,identity,anchor,x,y,out position,out receipt,out issue);
            return StartResolvedSurfacePlacement(target,revision,Dependencies,Resolve,out operation,out error,checkpoint);
        }
        internal bool StartSurfacePlacement(ScannedRoom scan,string target,int revision,string identity,Ray ray,
            out CapabilityOperation operation,out string error,bool prepared=false,Action<string> checkpoint=null)
        {
            operation=null;var value=new SurfacePlacementOperation(this,scan,target,revision,identity,ray,checkpoint);
            if(!value.Validate(out error))return false;
            value.BeginPreparation(prepared);operation=value;return true;
        }
        internal bool PlaceOnSurfaceManually(ScannedRoom scan,string target,int revision,string identity,Ray ray)
        {
            if(!scan||ObjectRevision(target)!=revision){SetStatus("Room or object changed; inspect it and request placement again");return false;}
            if(!scan.CanPlace(identity,out var error)){SetStatus(error);return false;}
            var data=Read(target);var checkpoint=ManualCreationCheckpoint;
            return StartManualCreation(target,true,
                (out CapabilityOperation operation,out string issue)=>StartSurfacePlacement(scan,target,revision,identity,ray,out operation,out issue,true,checkpoint),
                null,"Surface placement",string.IsNullOrWhiteSpace(data?.name)?data?.kind.ToString()??"item":data.name);
        }
        sealed class SurfacePlacementOperation:CapabilityOperation
        {
            readonly RoomEditor editor;readonly ScannedRoom scan;
            readonly string target,identity,session;readonly int revision;readonly Ray ray;readonly Matrix4x4 frame;
            readonly Action<string> checkpoint;
            Task<bool> preparation;CapabilityOperation placement;bool stopped;
            internal SurfacePlacementOperation(RoomEditor editor,ScannedRoom scan,string target,int revision,string identity,Ray ray,Action<string> checkpoint)
            {
                this.editor=editor;this.scan=scan;this.target=target;this.revision=revision;this.identity=identity;this.ray=ray;this.checkpoint=checkpoint;
                frame=editor.transform.localToWorldMatrix;session=editor.TemporarySessionId;
            }
            string Dependencies()=>editor.SurfaceFrameIssue(scan,frame)??(scan.CanPlace(identity,out var issue)?null:issue);
            internal bool Validate(out string error)
            {
                error="Surface placement was cancelled";
                if(stopped||!editor||editor.TemporarySessionId!=session)return false;
                error=Dependencies();if(error!=null)return false;
                if(editor.ObjectRevision(target)!=revision){error="This object changed while placement was preparing";return false;}
                return editor.CanEditObject(target,false,out error);
            }
            internal void BeginPreparation(bool prepared)=>preparation=prepared?Task.FromResult(true):Prepare(scan);
            static async Task<bool> Prepare(ScannedRoom scan){try{return await scan.PreparePlacement();}catch(Exception){return false;}}
            bool Resolve(out Vector3 position,out JObject receipt,out string error)=>
                scan.PrepareObjectSurface(editor,target,revision,identity,ray,out position,out receipt,out error);
            public override float Seconds=>0;
            public override RuleActionState State(out string error)
            {
                error=null;
                if(stopped){error="Surface placement was cancelled";return RuleActionState.Failed;}
                if(placement!=null)return placement.State(out error);
                if(!Validate(out error))return RuleActionState.Failed;
                if(!preparation.IsCompleted)return RuleActionState.Preparing;
                if(!preparation.Result){error="Live surface detection could not prepare; finish room access and explicitly try again";return RuleActionState.Failed;}
                if(!editor.StartResolvedSurfacePlacement(target,revision,Dependencies,Resolve,out placement,out error,checkpoint))return RuleActionState.Failed;
                return placement.State(out error);
            }
            public override bool Complete(out string error){if(State(out error)!=RuleActionState.Ready)return false;return placement.Complete(out error);}
            public override JObject Result=>placement?.Result??new JObject();
            public override void Stop(bool preservePlacement){stopped=true;placement?.Stop(preservePlacement);}
            public override string InterruptionStatus=>placement?.InterruptionStatus??"Placement cancelled; late surface preparation cannot move the object.";
        }
    }
}
