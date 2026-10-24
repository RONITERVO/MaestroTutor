// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation {
    public sealed partial class SpatialSculpting {
        SculptResolution resolution;
        internal bool Resolving=>resolution!=null;
        internal bool ResolvingTool(string id)=>Resolving&&tool==id;
        internal RoomEditor.PendingPersistedEdit ResolvingEdit=>resolution?.Pending;
        void Update()=>resolution?.State(out _);
        void Interrupt(){if(Active)Finish(false);resolution?.Stop(false);}
        void ClearPreview(){
            if(Editor)Editor.Find(target)?.GetComponent<HeightFieldView>()?.Preview(null);
            MaterialPreview(null);
            if(packingPreview)packingPreview.SetActive(false);
        }
        internal bool BeginResolve(string session,bool discard,out CapabilityOperation operation,out string error,Action<string> checkpoint=null){
            operation=null;if(!CanResolve(session,discard,out error))return false;
            var receipt=new JObject{["sessionId"]=session,["phase"]=discard?"discarded":"saved",["target"]=discard?"":target,
                ["points"]=points.Count,["changedVertices"]=discard?0:source.heights.Zip(draft.heights,(a,b)=>a!=b?1:0).Sum(),["temporary"]=Editor.TemporaryRoom};
            if(discard){Clear();Editor.ReportStatus("Surface draft discarded");operation=new CompletedCapability(receipt);return true;}
            if(lease?.Held!=true&&!Editor.Ownership.TryAcquire("sculpt:"+SessionId,tool==null?"Your sculpt tool":"Held sculpt tool",role,
                new[]{new BehaviourCatalog.Claim(target,"wholeTarget")},_=>Interrupt(),out lease,out error,preservePlacement:true))return false;
            bool packing=Packing,material=Material;string packedId=null;double packedAmount=0;
            string message=packing?"Ball packed — grab it normally; Undo restores the surface and removes it":material?
                "Material gesture saved — Undo restores both balances":"Sculpt gesture saved — Undo restores the surface";
            var value=new SculptResolution(this,receipt,message,()=>{
                if(packing){lastPackSession=session;lastPackedObject=packedId;lastPackedAmount=packedAmount;}
            },()=>packedId);
            string Validate(){
                if(!this||!isActiveAndEnabled||!Retained||SessionId!=session)return "The retained sculpt session ended";
                if(lease?.Held!=true)return "The sculpt gesture lost ownership; inspect and retry or discard its draft";
                if(!Unchanged(out var issue)||!Editor.CanEditObject(target,true,out issue))return issue;
                if(packing){
                    if(!Editor.CanCreatePrimitive(out issue))return issue;
                    float ballRadius=(float)Math.Pow(packingAmount/1000*3/(4*Math.PI),1.0/3);
                    var p=UnityEngine.JsonUtility.FromJson<UnityEngine.Vector3>(packingArgs["position"].ToString());
                    if(!PackingSpaceClear(Editor.transform.TransformPoint(p),ballRadius,out issue))return issue;
                }
                return null;
            }
            bool Prepare(out RoomObjectData[] data,out string issue){
                data=null;
                if(packing){
                    if(!PreparePacking(out issue))return false;
                    var args=(JObject)packingArgs.DeepClone();args["source"]["revision"]=Editor.ObjectRevision(target);
                    if(!Editor.PrepareMaterialPack(args,out var field,out var ball,out packedAmount,out issue))return false;
                    packedId=ball.id;data=new[]{field,ball};return true;
                }
                if(material)return PrepareMaterial(out data,out issue);
                if(!Editor.PrepareSculpt(target,Editor.ObjectRevision(target),mode,points.ToArray(),radius,height,out var sculpted,out _,out issue))return false;
                data=new[]{sculpted};return true;
            }
            if(!PersistedEditOperation.Start(Editor,target,(out RoomEditor.PendingPersistedEdit pending,out string issue)=>{
                    bool started=Editor.BeginSculptGesture(Validate,Prepare,message,material,out pending,out issue,checkpoint);value.Pending=pending;return started;
                },out var inner,out error,(out string issue)=>{issue=Validate();return issue==null;},()=>receipt))return false;
            value.Inner=inner;resolution=value;operation=value;errorText="";Editor.ReportStatus("Saving sculpt gesture — wait for confirmation");return true;
        }
        // The capture owns progression for physical input; the returned handle lets
        // book/agent/program callers observe and stop that same operation. Results
        // and Stop remain idempotent even after this capture starts another gesture.
        sealed class SculptResolution:CapabilityOperation {
            readonly SpatialSculpting owner;
            readonly JObject receipt;
            readonly string message;
            readonly Action saved;
            readonly Func<string> packedId;
            internal CapabilityOperation Inner;
            internal RoomEditor.PendingPersistedEdit Pending;
            bool finished,previewCleared;
            RuleActionState state=RuleActionState.Preparing;
            string failure;
            JObject result;
            internal SculptResolution(SpatialSculpting owner,JObject receipt,string message,Action saved,Func<string> packedId){
                this.owner=owner;this.receipt=receipt;this.message=message;this.saved=saved;this.packedId=packedId;
            }
            public override float Seconds=>0;
            public override RuleActionState State(out string error){
                if(!finished){
                    state=Inner.State(out failure);
                    if(Pending?.Accepted==true&&!previewCleared){previewCleared=true;if(owner)owner.ClearPreview();}
                    if(state==RuleActionState.Failed)Finish(false);
                    else if(state==RuleActionState.Ready){
                        if(Inner.Complete(out failure)){result=(JObject)receipt.DeepClone();Finish(true);}
                        else {state=RuleActionState.Failed;Finish(false);}
                    }
                }
                error=failure;return state;
            }
            void Finish(bool success){
                if(finished)return;finished=true;
                bool accepted=Pending?.Accepted==true;
                if(owner&&ReferenceEquals(owner.resolution,this)){
                    owner.resolution=null;
                    if(success){saved();owner.Clear();owner.Editor?.ReportStatus(message);}
                    else if(accepted){
                        string status=InterruptionStatus;owner.Clear();owner.errorText=status;owner.Editor?.ReportStatus(status);
                    }else owner.Failed(failure??"Sculpt saving interrupted before acceptance; retry or discard the retained draft");
                }
                Inner.Stop(false);
            }
            public override bool Complete(out string error){var current=State(out error);if(current==RuleActionState.Preparing)error="Sculpt gesture is still preparing or saving";return current==RuleActionState.Ready;}
            public override JObject Result=>result==null?new JObject():(JObject)result.DeepClone();
            public override void Stop(bool preservePlacement){
                if(finished)return;
                Inner.Stop(preservePlacement);failure=Pending?.Accepted==true?InterruptionStatus:"Sculpt saving stopped before acceptance; retry or discard the retained draft";
                state=RuleActionState.Failed;Finish(false);
            }
            public override string InterruptionStatus {
                get {var status=Inner?.InterruptionStatus;var id=packedId();return status==null?null:string.IsNullOrEmpty(id)?status:"Packed object "+id+". "+status;}
            }
        }
    }
}
