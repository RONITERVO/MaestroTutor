// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class WorldWeatherCapability:CapabilityModule
    {
        internal const string Feature="worldWeather.v1";
        public override string Id=>"world.weather.set";
        public override string Label=>"Set world weather";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[]{"world.weather.current","workspace.available","storage.writable"};
        public override string Description=>"Save region rain (0–120 mm/hour), authored X/Z wind (-15–15 metres/second), cloud cover (0–1), exponential fog density (0–0.25 per metre), fog sRGB colour and rain visual seed (1–1000000). Read world.weather for its exact revision. transitionSeconds=0 changes immediately; positive duration up to 86400 uses the running authored world clock, interpolating from current sampled weather. Seeking the clock resamples weather without replaying water input. Cloud cover attenuates authored sun/ambient lighting, fog affects virtual surfaces, and wind inclines bounded rain streaks. Browser pages and controls stay readable; passthrough is unchanged. Exposed compatible open containers collect Water through the existing physical liquid system while physics runs, using active real seconds irrespective of clock speed. Cover respects the receiving object's real-room collision profile; unavailable scan or saturated query is unknown, never clear. Existing different liquids do not mix. world.weather.exposure and object.container.rain report geometry/collection. No automatic ground puddles, wind forces, snow, weather audio or volumetric cloud geometry yet. One Undo restores weather; already saved collected water has its own liquid-flow Undo.";
        static JObject Bool()=>new(){["type"]="boolean"};
        internal static JObject Settings()=>Object(new JObject{["rainMmPerHour"]=Number(0,120),["windX"]=Number(-15,15),["windZ"]=Number(-15,15),["cloudCover"]=Number(0,1),["fogDensity"]=Number(0,.25),["fogColor"]=Text("^#[0-9A-Fa-f]{6}$",7)});
        public override JObject InputSchema{get{var s=CurrentInputs(Object(new JObject{["revision"]=Revision(),["seed"]=Number(1,1000000,true),["settings"]=Settings(),["transitionSeconds"]=Number(0,86400)}),"world.weather","revision",null,"settings","seed");s["x-features"]=new JArray(Feature);return s;}}
        internal static JObject Observation()=>Object(new JObject{["revision"]=Revision(),["worldId"]=Text("^[a-f0-9]{32}$",32),["regionId"]=Text("^[a-f0-9]{32}$",32),["seed"]=Number(1,1000000,true),["settings"]=Settings(),["current"]=Settings(),["transition"]=Object(new JObject{["startDay"]=Number(0,999999,true),["startSecond"]=Number(0,86400),["seconds"]=Number(0,86400),["progress"]=Number(0,1)}),["temporary"]=Bool()});
        public override JObject OutputSchema=>Observation();
        public override JObject Example=>new(){["revision"]=1,["seed"]=1,["settings"]=JObject.Parse(JsonUtility.ToJson(new WeatherSettings{rainMmPerHour=8,cloudCover=.7f})),["transitionSeconds"]=0};
        static WeatherSettings Value(JObject a)=>JsonUtility.FromJson<WeatherSettings>(a["settings"].ToString(Newtonsoft.Json.Formatting.None));
        public override bool Validate(JObject a,out string error){error="Choose valid weather settings";if(!Value(a).Valid)return false;error=null;return true;}
        public override bool CanRun(CapabilityContext c,JObject a,out string error){error="World weather is unavailable";return c.Editor&&c.Editor.CanSetWeather((int)a["revision"],(double)a["transitionSeconds"],out error);}
        public override bool Start(CapabilityContext c,string runId,JObject a,out CapabilityOperation operation,out string error){operation=null;error="World weather is unavailable";if(!c.Editor||!c.Editor.SetWeather((int)a["revision"],(int)a["seed"],Value(a),(double)a["transitionSeconds"],out error))return false;operation=new CompletedCapability(c.Editor.ObserveWeather());return true;}
        internal static BehaviourCatalog.FactDefinition Fact()=>new("world.weather",OutputType(Observation()),"World weather","Saved target, sampled current weather and transition on the authored world clock. Does not infer real weather or location. Read world.illumination for cloud-attenuated light energy. Rain collection uses active real time and has independent liquid-flow receipts.",null,null,(c,a)=>c.Editor&&c.Editor.ObserveWeather() is JObject v?ProgramValue.Literal(v,OutputType(Observation())):null,features:new[]{Feature});
        internal static BehaviourCatalog.FactDefinition Exposure(){
            var schema=Object(new JObject{["state"]=Choice("unknown","covered","open"),["realRoom"]=Bool(),["queryMetres"]=Number(100,100)});
            var cover=new WeatherCover();
            return new("world.weather.exposure",OutputType(schema),"Rain exposure at a point","Query upstream along the sampled wind/rain direction from an authored point, at most 100 physical metres. Accepted solid virtual objects and policy-enabled aligned scanned surfaces provide cover. target selects whose real-room participation applies; an empty target uses the global policy. Unknown means missing scan/alignment or saturated collision query, never an exposed surface. Rain intensity and physics pause do not change geometric exposure. Transparent solid roofs still block rain; visual opacity is independent. No unscanned physical geometry is inferred.",
                Object(new JObject{["position"]=Object(new JObject{["x"]=Number(-25,25),["y"]=Number(-25,25),["z"]=Number(-25,25)}),["target"]=Text("^(|maestro|book|[a-f0-9]{32})$",32)}),
                new JObject{["position"]=new JObject{["x"]=0,["y"]=1,["z"]=0},["target"]=""},
                (c,a)=>{var e=c.Editor;if(!e)return null;string id=(string)a["target"];var item=id==""?null:e.Find(id);if(id!=""&&!item)return null;Physics.SyncTransforms();var p=a["position"];var state=cover.Exposure(e,e.Frame.PointToWorld(new Vector3((float)p["x"],(float)p["y"],(float)p["z"])),WeatherCover.Velocity(e),item);
                    return ProgramValue.Literal(new JObject{["state"]=state.ToString().ToLowerInvariant(),["realRoom"]=e.PhysicsWorld&&e.PhysicsWorld.IncludesRealRoom(item),["queryMetres"]=100},OutputType(schema));},features:new[]{Feature});
        }
        internal static BehaviourCatalog.EventDefinition Collected()=>new("object.container.rainCollected","Collected rain was saved","A shared physical liquid-flow episode containing precipitation was accepted. Source/value is the receiving container ID; collectedMl is external Water intake during that episode. Only successful saves emit; failed saving, Undo and reload do not. Lifecycle-paused listeners can miss the event; inspect accepted contents.",Object(new JObject{["collectedMl"]=Number(0,8000000),["temporary"]=Bool()}),objectEvent:true,features:new[]{Feature});
        internal static BehaviourCatalog.FactDefinition Rain()=>new("object.container.rain",OutputType(Object(new JObject{["sessionId"]=Text("^[a-f0-9]{32}$",32),["phase"]=Choice("idle","flowing","failed"),["collectedMl"]=Number(0,8000000),["exposure"]=Number(0,1),["ready"]=Bool(),["reason"]=Text(null,256)})),
            "Rain collected by a container","Current shared liquid-flow episode's Water intake from precipitation. Collected quantity resets after publication or rollback. exposure is the open fraction of five aperture samples; unknown samples are excluded. Collection needs running physics, an available upward/wind-facing cavity and empty or matching Water contents. Rate uses projected opening area and active real time; camera visibility and authored clock speed do not multiply it. Read object.container.live for total and accepted quantities. Pause/save failure uses the same rollback and Undo as pouring.",Object(new JObject{["target"]=DrawingData.Target()}),new JObject{["target"]=new string('0',32)},(c,a)=>c.Editor&&c.Editor.Liquids?.ObserveRain((string)a["target"]) is JObject v?ProgramValue.Literal(v):null,features:new[]{Feature});
    }
}
