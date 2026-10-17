// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal sealed class HeightFieldTransferCapability:NativeResourceInputsCapability {
        internal const string Feature="heightFieldTransfer.v1";
        public override string Id=>"object.field.transfer";
        public override string Label=>"Transfer surface volume";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Channels=>new[]{"wholeTarget"};
        public override IReadOnlyList<string> Requirements=>new[]{"targets.created","targets.unheld","source.revision.current","storage.writable","targets.physics.fixed"};
        public override string Description=>"Move up to amountLitres between two distinct fixed height surfaces with identical material labels and colours. Each endpoint selects a local X/Z disk; smooth radial falloff bounds removal by available height and addition by headroom. Triangulated footprint support extends to adjacent grid vertices. Result reports actual removedLitres, addedLitres and signed roundingLitres. These are unscaled local geometric litres, like object.field.volumeLitres; resizing objects changes displayed geometry, not this measure. Both current revisions and exclusive ownership are required. One atomic saved edit and one Undo updates both meshes and collisions, or neither on failure. Maximum rounding difference is max(0.000001 litre, removedLitres * 0.000001); transfers below representable resolution refuse. Empty/full or missed footprints refuse. This instantaneous authoring operation does not check distance, move a shovel, start physics, simulate grains or convert to containers/snowballs. Programs can combine it with existing movement and event conditions. Stop does not retract completed edits; Undo does. No automatic replay.";
        static JObject Member()=>CurrentInputs(Object(new JObject{["target"]=DrawingData.Target(),["revision"]=Revision(),["centre"]=Object(new JObject{["x"]=Number(-2,2),["z"]=Number(-2,2)}),["radius"]=Number(.005,2)}),"object.field","revision",new JObject{["target"]="target"});
        public override JObject InputSchema {get{var s=Object(new JObject{["source"]=Member(),["destination"]=Member(),["amountLitres"]=Number(.001,8000)});s["x-features"]=new JArray(HeightFieldCapability.Feature,Feature);s["format"]="heightFieldTransfer";return s;}}
        public override JObject OutputSchema=>Object(new JObject{["removedLitres"]=Number(0,8000),["addedLitres"]=Number(0,8000),["roundingLitres"]=Number(-.008,.008)});
        public override JObject Example=>new(){["source"]=Endpoint(new string('0',32)),["destination"]=Endpoint(new string('1',32)),["amountLitres"]=1};
        static JObject Endpoint(string id)=>new(){["target"]=id,["revision"]=1,["centre"]=new JObject{["x"]=0,["z"]=0},["radius"]=.2};
        internal static Vector2 Centre(JToken endpoint)=>new((float)endpoint["centre"]["x"],(float)endpoint["centre"]["z"]);
        public override BehaviourCatalog.Claim[] Claims(JObject a)=>new[]{new BehaviourCatalog.Claim((string)a["source"]["target"],"wholeTarget"),new BehaviourCatalog.Claim((string)a["destination"]["target"],"wholeTarget")};
        public override bool Validate(JObject a,out string error){error="Choose different source and destination objects";if((string)a["source"]["target"]==(string)a["destination"]["target"])return false;error=null;return true;}
        public override bool CanRun(CapabilityContext c,JObject a,out string error)=>c.Editor.PrepareFieldTransfer(a,out _,out _,out error);
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation operation,out string error){
            operation=null;if(!c.Editor.TransferField(a,out var moved,out error))return false;
            operation=new CompletedCapability(new JObject{["removedLitres"]=moved.RemovedLitres,["addedLitres"]=moved.AddedLitres,["roundingLitres"]=moved.RoundingLitres});return true;
        }
    }
}
