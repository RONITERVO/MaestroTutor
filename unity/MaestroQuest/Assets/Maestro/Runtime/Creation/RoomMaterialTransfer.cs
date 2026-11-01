// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation {
    public sealed partial class RoomEditor {
        static MaterialTransfer.Endpoint MaterialEndpoint(RoomObjectData data,JToken endpoint)=>(string)endpoint["kind"]=="field"
            ?MaterialTransfer.Endpoint.Surface(data.heightFields?.FirstOrDefault(),HeightFieldTransferCapability.Centre(endpoint),(float)endpoint["radius"])
            :MaterialTransfer.Endpoint.Stored(data.materialStores?.FirstOrDefault());
        internal bool PrepareMaterialTransfer(JObject args,out RoomObjectData[] data,out HeightFieldTransfer.Result result,out string error){
            data=null;result=default;var source=args["source"];var destination=args["destination"];
            string from=(string)source["target"],to=(string)destination["target"];
            error="Choose different source and destination objects";if(from==to)return false;
            if(!ComponentSource(from,(int)source["revision"],out var first,out error)||!ComponentSource(to,(int)destination["revision"],out var second,out error))return false;
            if(!MaterialTransfer.Apply(MaterialEndpoint(first,source),MaterialEndpoint(second,destination),(double)args["amountLitres"],out result,out error))return false;
            data=new[]{first,second};return ComponentCandidate(data,out error);
        }
        internal bool CanTransferMaterial(JObject args,out string error)=>
            ComponentSource((string)args["source"]["target"],(int)args["source"]["revision"],out _,out error)&&
            ComponentSource((string)args["destination"]["target"],(int)args["destination"]["revision"],out _,out error);
        internal bool BeginMaterialTransfer(JObject args,out HeightFieldTransfer.Result result,out PendingPersistedEdit pending,out string error,Action<string> checkpoint=null){
            var captured=(JObject)args.DeepClone();HeightFieldTransfer.Result moved=default;
            bool started=BeginPreparedEdit(()=>CanTransferMaterial(captured,out var issue)?null:issue,
                (out RoomJournal.PreparedEdit edit,out string issue)=>{
                    edit=null;return PrepareMaterialTransfer(captured,out var data,out moved,out issue)&&
                        journal.Prepare(data,Array.Empty<string>(),out edit,out issue,scope:RoomEditScope.Content);
                },"Material transferred — one Undo restores both balances",out pending,out error,checkpoint);
            result=started?moved:default;return started;
        }
        internal bool TransferMaterial(JObject args,out HeightFieldTransfer.Result result,out string error){
            if(!PrepareMaterialTransfer(args,out var data,out result,out error))return false;
            if(CommitPersisted(data,Array.Empty<string>(),"Material transferred — one Undo restores both balances",false,out error,ComponentBefore(data)))return true;
            result=default;return false;
        }
    }
}
