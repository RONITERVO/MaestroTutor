// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation {
    public sealed partial class SpatialSculpting {
        RoomMaterialStore materialSource,materialDraft;
        SculptTip materialTip;
        bool depositing;
        double requestedLitres;
        HeightFieldTransfer.Result materialResult;
        bool Material=>materialSource!=null;
        internal bool OwnsMaterial(string id)=>Busy&&Material&&tool==id;
        internal void BeginMaterialTool(string id,string field,Vector2 point,bool deposit,SculptTip tip,RoomActorRole actor){
            if(!Editor||Busy||Editor.DrawingInProgress||Editor.Ownership.Suspended||Editor.WriteGate.Frozen||id==field)return;
            if(!Editor.CanEditObject(field,true,out var error)){Editor.ReportStatus(error);return;}
            var carrier=Editor.Read(id);var store=carrier?.materialStores?.FirstOrDefault();var surface=Editor.Read(field)?.heightFields?.FirstOrDefault();
            if(store==null||surface==null||tip?.IsMaterial!=true||!tip.enabled){Editor.ReportStatus("Configure a material store on the held scoop first");return;}
            write=Editor.WriteGate.TryWrite(out error);if(write==null){Editor.ReportStatus(error);return;}
            SessionId=Guid.NewGuid().ToString("N");target=field;tool=id;mode=deposit?"deposit":"take";radius=tip.radius;height=0;role=actor;roomSession=Editor.TemporarySessionId;
            source=surface.Copy();before=JsonUtility.ToJson(source);draft=source.Copy();materialSource=store.Copy();materialDraft=store.Copy();materialTip=tip.Copy();depositing=deposit;requestedLitres=tip.amountLitres;
            var patch=MaterialTransfer.Endpoint.Surface(draft,point,radius);var held=MaterialTransfer.Endpoint.Stored(materialDraft);
            if(!MaterialTransfer.Apply(deposit?held:patch,deposit?patch:held,requestedLitres,out materialResult,out error)){Editor.ReportStatus(error);Clear();return;}
            if(!Editor.Ownership.TryAcquire("sculpt:"+SessionId,"Held material tool",role,new[]{new BehaviourCatalog.Claim(target,"wholeTarget")},_=>Finish(false),out lease,out error,preservePlacement:true)||!Unchanged(out error)){Editor.ReportStatus(error);Clear();return;}
            owner=-2;points.Clear();points.Add(point);errorText="";
            Editor.Find(target)?.GetComponent<HeightFieldView>()?.Preview(draft);MaterialPreview(materialDraft);
            Editor.ReportStatus(deposit?"Deposit preview — lift to save both material balances":"Scoop preview — lift to save the surface and carried material");
        }
        internal void MoveMaterialTool(string id,string field,RoomActorRole actor){
            if(!OwnsTool(id)||!Material)return;
            if(actor!=role||!Unchanged(out var error)||Editor.Ownership.Suspended||lease?.Held!=true){Finish(false);return;}
            if(field!=target)Finish(true);
            // One measured dose per contact. Dragging or waiting cannot create more material.
        }
        bool MaterialUnchanged(out string error){
            error=null;if(!Material)return true;var current=Editor.Read(tool);
            if(current==null||!Editor.Find(tool)||JsonUtility.ToJson(current.materialStores?.FirstOrDefault())!=JsonUtility.ToJson(materialSource)||JsonUtility.ToJson(current.sculptTips?.FirstOrDefault())!=JsonUtility.ToJson(materialTip)){error="The carrier or its material changed; discard this draft";return false;}return true;
        }
        bool PrepareMaterial(out RoomObjectData[] data,out string error)=>Editor.PrepareMaterialScoop(target,tool,source,materialSource,materialTip,draft,materialDraft,out data,out error);
        bool CommitMaterial(out int changed,out string error){
            changed=0;if(!PrepareMaterial(out var data,out error)||!Editor.CommitMaterialScoop(data,out error))return false;
            changed=source.heights.Zip(draft.heights,(a,b)=>a!=b?1:0).Sum();return true;
        }
        void MaterialPreview(RoomMaterialStore preview){if(Editor)Editor.Find(tool)?.GetComponent<MaterialToolContentsView>()?.Preview(preview);}
        void ClearMaterial(){MaterialPreview(null);materialSource=materialDraft=null;materialTip=null;requestedLitres=0;depositing=false;materialResult=default;}
        internal JObject ObserveMaterial()=>new(){
            ["sessionId"]=SessionId,["phase"]=Material?(Active?"contact":"unsaved"):"idle",["tool"]=Material?tool:"",["field"]=Material?target:"",["direction"]=Material?(depositing?"deposit":"take"):"none",
            ["balance"]=new JObject{["requestedLitres"]=requestedLitres,["savedLitres"]=materialSource?.amountLitres??0,["previewLitres"]=materialDraft?.amountLitres??0,
            ["removedLitres"]=materialResult.RemovedLitres,["addedLitres"]=materialResult.AddedLitres,["roundingLitres"]=materialResult.RoundingLitres},
            ["temporary"]=Editor&&Editor.TemporaryRoom,["error"]=Maestro.Quest.Imports.ImportObservation.Text(Material?errorText:"")};
    }
}
