// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation {
    public sealed partial class RoomEditor {
        internal bool PrepareMaterialStore(string target,int revision,RoomMaterialStore store,out RoomObjectData data,out string error){
            data=null;if(!ComponentSource(target,revision,out data,out error))return false;
            data.materialStores=store==null?Array.Empty<RoomMaterialStore>():new[]{store.Copy()};return ComponentCandidate(new[]{data},out error);
        }
        internal bool CanEditMaterialStore(string target,int revision,out string error)=>ComponentSource(target,revision,out _,out error);
        internal bool BeginMaterialStore(string target,int revision,RoomMaterialStore store,out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null){
            var captured=store?.Copy();
            return BeginPreparedEdit(()=>CanEditMaterialStore(target,revision,out var issue)?null:issue,
                (out RoomJournal.PreparedEdit edit,out string issue)=>{
                    edit=null;return PrepareMaterialStore(target,revision,captured,out var data,out issue)&&
                        journal.Prepare(new[]{data},Array.Empty<string>(),out edit,out issue,scope:RoomEditScope.Content);
                },captured==null?"Material store removed":"Material store saved",out pending,out error,checkpoint);
        }
        internal bool EditMaterialStore(string target,int revision,RoomMaterialStore store,out string error){
            if(!PrepareMaterialStore(target,revision,store,out var data,out error))return false;
            return CommitPersisted(new[]{data},Array.Empty<string>(),store==null?"Material store removed":"Material store saved",false,out error,ComponentBefore(new[]{data}));
        }
        internal bool PrepareMaterialPack(JObject args,out RoomObjectData source,out RoomObjectData packed,out double amount,out string error){
            source=packed=null;amount=0;var endpoint=args["source"];
            if(!CanCreatePrimitive(out error)||!ComponentSource((string)endpoint["target"],(int)endpoint["revision"],out source,out error))return false;
            var field=source.heightFields?.FirstOrDefault();var centre=new Vector2((float)endpoint["centre"]["x"],(float)endpoint["centre"]["z"]);
            if(!HeightFieldTransfer.Extract(field,centre,(float)endpoint["radius"],(double)args["amountLitres"],out amount,out error))return false;
            if(!RoomTerrainReservoir.Normalize(source,out error))return false;
            float diameter=(float)(2*Math.Pow(amount/1000*3/(4*Math.PI),1.0/3));
            var recipe=new RoomRecipe{parts=new[]{new RecipePart{id="packed",shape="sphere",size=Vector3.one*diameter,color=field.color}}};
            var collision=new CollisionRecipe{shapes=new[]{new CollisionShape{id="packed",shape="sphere",size=Vector3.one*diameter}}};
            var position=JsonUtility.FromJson<Vector3>(args["position"].ToString());
            if(!PrepareRecipeObject((string)args["name"],position,1,recipe,collision,new ObjectPhysicsSettings{mode="solid",shape="automatic",mass=(float)args["mass"]},out packed,out error))return false;
            packed.materialStores=new[]{new RoomMaterialStore{capacityLitres=amount,amountLitres=amount,material=field.material,color=field.color}};
            string sourceId=source.id;var candidate=Snapshot();candidate.objects=candidate.objects.Where(o=>o.id!=sourceId).Concat(new[]{source,packed}).ToArray();return candidate.Validate(out error);
        }
        internal bool CanPackMaterial(JObject args,out string error)=>
            CanCreatePrimitive(out error)&&ComponentSource((string)args["source"]["target"],(int)args["source"]["revision"],out _,out error);
        internal bool BeginPackMaterial(JObject args,out string id,out double amount,out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null){
            var captured=(JObject)args.DeepClone();string created=null;double quantity=0;
            bool started=BeginPreparedEdit(()=>CanPackMaterial(captured,out var issue)?null:issue,
                (out RoomJournal.PreparedEdit edit,out string issue)=>{
                    edit=null;if(!PrepareMaterialPack(captured,out var source,out var packed,out quantity,out issue))return false;
                    created=packed.id;
                    return journal.Prepare(new[]{source,packed},Array.Empty<string>(),out edit,out issue,observedBefore:ComponentBefore(new[]{source}));
                },"Material packed — one Undo restores the surface and removes the ball",out pending,out error,checkpoint);
            id=started?created:null;amount=started?quantity:0;return started;
        }
        internal bool PackMaterial(JObject args,out string id,out double amount,out string error){
            id=null;if(!PrepareMaterialPack(args,out var source,out var packed,out amount,out error))return false;
            if(!CommitPersisted(new[]{source,packed},Array.Empty<string>(),"Material packed — one Undo restores the surface and removes the ball",false,out error,ComponentBefore(new[]{source}))){amount=0;return false;}
            id=packed.id;return true;
        }
    }
}
