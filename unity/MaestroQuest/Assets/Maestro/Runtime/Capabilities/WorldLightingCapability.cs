// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class WorldLightingCapability:CapabilityModule
    {
        internal const string Feature="worldLighting.v1";
        public override string Id=>"world.lighting.set";
        public override string Label=>"Set world lighting";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[]{"world.lighting.current","workspace.available","storage.writable"};
        public override string Description=>"While the world.time lighting cycle is disabled, save ambient and directional sun lighting for the active world region. Read world.lighting and pass its exact revision. Colours are sRGB hex; intensities are multipliers 0–2. Azimuth is degrees around authored +Y (0 faces +Z, +90 faces +X); elevation is -90–90 above the authored horizon. enabled=false restores the original illustrated lighting while retaining these settings. This affects illustrated virtual surfaces including imported models; it does not relight passthrough, cast shadows, create local lights or simulate time/weather. Browser pages and tool labels remain readable. Movement, animation, sound, physics and view opacity continue independently. One Undo and temporary Keep/Discard apply; failed saving changes nothing. Readback confirms accepted settings, not physical headset appearance.";
        static JObject Bool()=>new(){["type"]="boolean"};
        internal static JObject Settings()=>Object(new JObject{["version"]=Number(1,1,true),["enabled"]=Bool(),["ambientColor"]=Text("^#[0-9A-Fa-f]{6}$",7),["sunColor"]=Text("^#[0-9A-Fa-f]{6}$",7),["ambientIntensity"]=Number(0,2),["sunIntensity"]=Number(0,2),["azimuth"]=Number(-180,180),["elevation"]=Number(-90,90)});
        public override JObject InputSchema {get{var s=CurrentInputs(Object(new JObject{["revision"]=Revision(),["settings"]=Settings()}),"world.lighting","revision",null,"settings");s["x-features"]=new JArray(Feature);return s;}}
        public override JObject OutputSchema=>Observation();
        static JObject Observation()=>Object(new JObject{["revision"]=Revision(),["worldId"]=Text("^[a-f0-9]{32}$",32),["regionId"]=Text("^[a-f0-9]{32}$",32),["settings"]=Settings(),["temporary"]=Bool()});
        public override JObject Example=>new(){["revision"]=1,["settings"]=JObject.Parse(JsonUtility.ToJson(new RoomLighting{enabled=true}))};
        static RoomLighting Value(JObject a)=>JsonUtility.FromJson<RoomLighting>(a["settings"].ToString(Newtonsoft.Json.Formatting.None));
        public override bool Validate(JObject a,out string error){error="Choose valid world lighting";if(!Value(a).Valid)return false;error=null;return true;}
        public override bool CanRun(CapabilityContext c,JObject a,out string error){error="Room lighting is unavailable";return c.Editor&&c.Editor.CanSetLighting((int)a["revision"],Value(a),out error);}
        public override bool Start(CapabilityContext c,string runId,JObject a,out CapabilityOperation operation,out string error){operation=null;error="Room lighting is unavailable";if(!c.Editor||!c.Editor.SetLighting((int)a["revision"],Value(a),out error))return false;operation=new CompletedCapability(c.Editor.ObserveLighting());return true;}
        internal static BehaviourCatalog.FactDefinition Fact()=>new("world.lighting",OutputType(Observation()),"World lighting","Saved region illumination and its current edit revision. Directions use authored world axes, independent of viewer movement. Does not report shadows, physical light or device rendering.",null,null,(c,a)=>c.Editor&&c.Editor.ObserveLighting() is JObject v?ProgramValue.Literal(v,OutputType(Observation())):null,features:new[]{Feature});
    }
}
