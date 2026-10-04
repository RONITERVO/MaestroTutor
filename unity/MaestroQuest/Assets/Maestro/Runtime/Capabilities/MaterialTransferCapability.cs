// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal sealed class MaterialTransferCapability:CapabilityModule {
        internal const string Feature="materialTransfer.v1";
        public override string Id=>"object.material.transfer";
        public override string Label=>"Transfer carried or surface material";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"targets.created","targets.unheld","source.revision.current","storage.writable","material.compatible"};
        public override string Description=>"Transfer measured material between two distinct objects, each explicitly choosing its height field or material store. Field endpoints use a local X/Z disk with smooth radial falloff; store endpoints use their saved local-litre balance. Supports surface-to-store, store-to-surface, store-to-store and the existing surface-to-surface kernel. Both material labels and opaque colours must match, even for empty receivers; configure them explicitly before transferring. Requested amount is capped by available volume and receiver capacity/headroom. Fields retain their fixed-physics requirement; carried stores can be on rigid objects, but both owners must be unheld for this authoring action. Current revisions, ownership and writable storage are required. One save and Undo updates both endpoints; failed saves, missing components and numeric refusal change neither. Returns actual removedLitres, addedLitres and signed roundingLitres; maximum difference is max(0.000001 litre, removedLitres * 0.000001). Local litres do not rescale with world geometry or infer rigid mass. Store transfers do not resize geometry, spill or mix materials. No liquid-container balance, proximity, shovel gesture, hand pose, grain simulation or automatic physics is inferred. Stop cannot retract a completed edit and receipt replay never repeats it.";
        static JObject Variant(string kind){
            var fields=new JObject{["kind"]=Choice(kind),["target"]=DrawingData.Target(),["revision"]=Revision()};fields["kind"]["x-static"]=true;
            if(kind=="field"){fields["centre"]=Object(new JObject{["x"]=Number(-2,2),["z"]=Number(-2,2)});fields["radius"]=Number(.005,2);}
            var s=CurrentInputs(Object(fields),kind=="field"?"object.field":"object.material","revision",new JObject{["target"]="target"});s["title"]=kind=="field"?"Surface footprint":"Carried material store";return s;
        }
        static JObject Endpoint()=>new(){["type"]="object",["x-discriminators"]=new JArray("kind"),["oneOf"]=new JArray(Variant("field"),Variant("store"))};
        public override JObject InputSchema{get{var s=Object(new JObject{["source"]=Endpoint(),["destination"]=Endpoint(),["amountLitres"]=Number(.001,8000)});s["format"]="materialTransfer";s["x-features"]=new JArray(Feature,MaterialStoreCapability.Feature,HeightFieldCapability.Feature,"actionResults.v1");return s;}}
        public override JObject OutputSchema=>Object(new JObject{["removedLitres"]=Number(0,8000),["addedLitres"]=Number(0,8000),["roundingLitres"]=Number(-.008,.008)});
        public override JObject Example=>new(){["source"]=new JObject{["kind"]="field",["target"]=new string('0',32),["revision"]=1,["centre"]=new JObject{["x"]=0,["z"]=0},["radius"]=.2},["destination"]=new JObject{["kind"]="store",["target"]=new string('1',32),["revision"]=1},["amountLitres"]=.5};
        public override BehaviourCatalog.Claim[] Claims(JObject a)=>new[]{new BehaviourCatalog.Claim((string)a["source"]["target"],"wholeTarget"),new BehaviourCatalog.Claim((string)a["destination"]["target"],"wholeTarget")};
        public override bool Validate(JObject a,out string error){error="Choose different source and destination objects";if((string)a["source"]["target"]==(string)a["destination"]["target"])return false;error=null;return true;}
        public override bool CanRun(CapabilityContext c,JObject a,out string error)=>c.Editor.PrepareMaterialTransfer(a,out _,out _,out error);
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation operation,out string error){operation=null;if(!c.Editor.TransferMaterial(a,out var moved,out error))return false;operation=new CompletedCapability(new JObject{["removedLitres"]=moved.RemovedLitres,["addedLitres"]=moved.AddedLitres,["roundingLitres"]=moved.RoundingLitres});return true;}
    }
}
