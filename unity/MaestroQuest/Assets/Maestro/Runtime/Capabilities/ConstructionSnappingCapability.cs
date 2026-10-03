// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal sealed class ConstructionSnappingCapability:CapabilityModule {
        internal const string Feature="constructionSnapping.v1";
        public override string Id=>"room.selection.snapSettings";
        public override string Label=>"Configure construction grip snapping";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[]{"construction.released","room.interaction.active"};
        public override string Description=>"Choose off, place or join for the solid construction handle. Session-only settings apply to later grips. Read this action's current settings first; held handles refuse changes. Place aligns loose pieces; join adds a fixed physical connection with explicit break limits (zero unbreakable). With 1–15 selected pieces, all compatible point pairs are considered, closest first with stable ID tie-breaking; join only uses a solid/bouncy source without an outgoing connection. distance is room-local metres (0.01–0.25). Matching permits at most 30 degrees of tilt; turnStep rounds twist about the destination +Y, with zero preserving free twist. Two-hand resizing works with snapping. A solid marker and handle text show the preview; pulling away removes it. Release calls ordinary object.layout.snap with exact point IDs/revisions and one Undo. Invalidated targets or failed saves restore the whole original arrangement. No occupancy or overlap guarantee, no scan snapping and no direct-item grip interception. Physics must be paused to use the handle. Off preserves normal group movement. These settings do not save a room edit; they reset when the workspace is reopened.";
        internal static JObject SettingsSchema()=>Object(new JObject{["stateId"]=Text("^[a-f0-9]{32}$",32),["mode"]=Choice("off","place","join"),["distance"]=Number(.01,.25),["turnStep"]=Number(0,180),["breakForce"]=Number(0,10000),["breakTorque"]=Number(0,10000)});
        public override JObject InputSchema {get{var schema=CurrentInputs(SettingsSchema(),"room.selection.snapping","stateId",null,"mode","distance","turnStep","breakForce","breakTorque");schema["x-features"]=new JArray(Feature);return schema;}}
        public override JObject OutputSchema=>SettingsSchema();
        public override JObject Example=>new(){["stateId"]=new string('0',32),["mode"]="place",["distance"]=.08,["turnStep"]=90,["breakForce"]=45,["breakTorque"]=3};
        static ConstructionSnapSettings Read(JObject args)=>JsonUtility.FromJson<ConstructionSnapSettings>(args.ToString());
        public override bool CanRun(CapabilityContext context,JObject args,out string error)=>context.Editor.CanConfigureConstructionSnapping(Read(args),out error);
        public override bool Start(CapabilityContext context,string runId,JObject args,out CapabilityOperation operation,out string error){operation=null;if(!context.Editor.ConfigureConstructionSnapping(Read(args),out error))return false;operation=new CompletedCapability(JObject.FromObject(context.Editor.ObserveConstructionSnapping()));return true;}
        internal static BehaviourCatalog.FactDefinition PreviewFact(){
            var schema=Object(new JObject{["active"]=new JObject{["type"]="boolean"},["mode"]=Choice("off","place","join"),["source"]=Text("^[a-f0-9]{0,32}$",32),["point"]=Text("^[a-zA-Z0-9_]{0,32}$",32),["destination"]=Text("^[a-f0-9]{0,32}$",32),["destinationPoint"]=Text("^[a-zA-Z0-9_]{0,32}$",32),["turn"]=Number(-180,180),["scale"]=Number(.1,4)});
            return new("room.selection.snapPreview",OutputType(schema),"Current grip snap preview","Exact point IDs and transform of the visible transient preview. Empty IDs/active=false mean no compatible preview. No saved edit until release succeeds.",null,null,(context,args)=>{
                if(!context.Editor)return null;var request=context.Editor.GetComponent<ConstructionManipulator>()?.SnapPreview;
                return ProgramValue.Literal(new JObject{["active"]=request!=null,["mode"]=request?.mode??"off",["source"]=request?.members[0].target??"",["point"]=request?.point??"",["destination"]=request?.destination.target??"",["destinationPoint"]=request?.destination.point??"",["turn"]=request?.turn??0,["scale"]=request?.scale??1});
            },features:new[]{Feature});
        }
        internal static BehaviourCatalog.FactDefinition Fact()=>new("room.selection.snapping",OutputType(SettingsSchema()),"Construction grip snapping settings","Session-local configuration for later construction-handle grips. Reading does not enable snapping. Use stateId to guard a configuration change.",null,null,(context,args)=>context.Editor?ProgramValue.Literal(JObject.FromObject(context.Editor.ObserveConstructionSnapping())):null,features:new[]{Feature});
    }
}
