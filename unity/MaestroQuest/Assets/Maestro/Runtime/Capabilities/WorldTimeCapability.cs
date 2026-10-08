// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Art;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class WorldTimeCapability:CapabilityModule
    {
        internal const string Feature="worldTime.v1";
        readonly bool seek;
        internal WorldTimeCapability(bool seek=false){this.seek=seek;}
        public override string Id=>seek?"world.time.seek":"world.time.configure";
        public override string Label=>seek?"Set world time":"Configure world time and day lighting";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[]{"world.time.current","workspace.available","storage.writable"};
        public override string Description=>seek?"Explicitly seek the authored day and second of day without changing clock settings. Day is 0–999999; second is 0 inclusive to 86400 exclusive. Read world.time for the exact edit revision. Lighting samples the resulting time immediately. This is not a physical calendar, a physics simulation seek, or replay of past events. One Undo restores the prior world time; runtime progress creates no Undo entries.":"Configure the region's authored environment clock and optional daily lighting keyframes while retaining its current day/second. Read world.time and its exact edit revision. rate is authored seconds per active real second (0–3600). running=false pauses time; rate=0 freezes it too. A lighting cycle needs 2–8 strictly time-ordered frames, at seconds 0–86400 exclusive. It loops at midnight, interpolating light energy and the shortest sun-azimuth arc. Disabling the cycle retains its frames and restores world.lighting's manual preset. Static lighting edits refuse while the cycle owns lighting. Focus loss, app suspension, workspace write/runtime holds and invalid world frames freeze advancement; the first resumed tick and gaps over one second are skipped. Saved running intent resumes from the saved position, without offline catch-up. Clock progress checkpoints through room persistence at most every five active seconds; explicit saves and lifecycle flush include current position. A crash may lose progress since the last successful checkpoint. Ordinary editing, looking away and viewer opacity do not stop it. Weather transitions sample this same clock; rain collection and physics use active real seconds. This does not scale physics/animation, change the device calendar or cast shadows. Exact save/Undo/temporary semantics apply.";
        static JObject Bool()=>new(){["type"]="boolean"};
        internal static JObject Settings()=>Object(new JObject{["running"]=Bool(),["rate"]=Number(0,3600),["cycleEnabled"]=Bool(),["frames"]=List(Frame(),0,8)});
        static JObject Frame(){var fields=(JObject)WorldLightingCapability.Settings()["properties"];fields.Remove("version");fields.Remove("enabled");fields["second"]=Number(0,86399.999);return Object(fields);}
        static JObject Position()=>Number(0,86399.999);
        public override JObject InputSchema{get{
            var fields=seek?new JObject{["revision"]=Revision(),["day"]=Number(0,999999,true),["second"]=Position()}:new JObject{["revision"]=Revision(),["settings"]=Settings()};
            // Seek loads only the guard. Its requested destination is never replaced by a moving observation.
            var s=CurrentInputs(Object(fields),"world.time","revision",null,seek?System.Array.Empty<string>():new[]{"settings"});s["x-features"]=new JArray(Feature);return s;
        }}
        static JObject Observation()=>Object(new JObject{["revision"]=Revision(),["worldId"]=Text("^[a-f0-9]{32}$",32),["regionId"]=Text("^[a-f0-9]{32}$",32),["day"]=Number(0,999999,true),["second"]=Number(0,86400),["settings"]=Settings(),["advancing"]=Bool(),["temporary"]=Bool()});
        public override JObject OutputSchema=>Observation();
        public override JObject Example=>seek?new(){["revision"]=1,["day"]=0,["second"]=64800}:new(){["revision"]=1,["settings"]=JObject.Parse(JsonUtility.ToJson(new WorldTimeSettings()))};
        static WorldTimeSettings Value(JObject a)=>JsonUtility.FromJson<WorldTimeSettings>(a["settings"].ToString(Newtonsoft.Json.Formatting.None));
        public override bool Validate(JObject a,out string error){error="Choose valid world time or ordered lighting keyframes";if(seek?!RoomWorldTime.PositionValid((int)a["day"],(double)a["second"]):!Value(a).Valid)return false;error=null;return true;}
        public override bool CanRun(CapabilityContext c,JObject a,out string error){error="World time is unavailable";return c.Editor&&c.Editor.CanChangeWorldTime((int)a["revision"],out error);}
        public override bool Start(CapabilityContext c,string runId,JObject a,out CapabilityOperation operation,out string error){
            operation=null;error="World time is unavailable";if(!c.Editor)return false;
            bool ok=seek?c.Editor.SeekWorldTime((int)a["revision"],(int)a["day"],(double)a["second"],out error):c.Editor.ConfigureWorldTime((int)a["revision"],Value(a),out error);
            if(!ok)return false;operation=new CompletedCapability(c.Editor.ObserveWorldTime());return true;
        }
        internal static BehaviourCatalog.FactDefinition Fact()=>new("world.time",OutputType(Observation()),"World time and lighting cycle","Current authored day/second and saved clock settings. Revision guards explicit edits; ordinary ticking does not invalidate settings. advancing reports current runtime eligibility. This is not calendar time or offline progression.",null,null,(c,a)=>c.Editor&&c.Editor.ObserveWorldTime() is JObject v?ProgramValue.Literal(v,OutputType(Observation())):null,features:new[]{Feature});
        internal static BehaviourCatalog.FactDefinition Illumination(){
            var rgb=Object(new JObject{["r"]=Number(0,2),["g"]=Number(0,2),["b"]=Number(0,2)});
            var schema=Object(new JObject{["enabled"]=Bool(),["ambient"]=rgb,["sun"]=rgb.DeepClone(),["azimuth"]=Number(-180,180),["elevation"]=Number(-90,90),["colorSpace"]=Choice("linear","gamma")});
            return new("world.illumination",OutputType(schema),"Effective world illumination","Sampled manual/day-cycle light energy attenuated by current cloud cover in the renderer's working colour space and authored-axis sun angles. Independent of physical passthrough, viewer state and collisions. This is mathematical readback, not measured device pixels.",null,null,(c,a)=>c.Editor?ProgramValue.Literal(c.Editor.CurrentWeather.Apply(new WorldLightingProjection(c.Editor.Lighting,c.Editor.WorldTime.settings).Sample(c.Editor.WorldSecond)).Observe(),OutputType(schema)):null,features:new[]{Feature});
        }
    }
}
