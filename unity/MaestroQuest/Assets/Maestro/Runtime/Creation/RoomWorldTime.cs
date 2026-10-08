// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    [Serializable] public sealed class WorldLightKeyframe
    {
        public double second;
        public string ambientColor="#FFFFFF",sunColor="#FFF4D6";
        public float ambientIntensity=.35f,sunIntensity=.65f,azimuth=-150,elevation=55;
        public WorldLightKeyframe Copy()=>(WorldLightKeyframe)MemberwiseClone();
        internal RoomLighting Light()=>new(){enabled=true,ambientColor=ambientColor,sunColor=sunColor,ambientIntensity=ambientIntensity,sunIntensity=sunIntensity,azimuth=azimuth,elevation=elevation};
        internal bool Valid=>double.IsFinite(second)&&second>=0&&second<=86399.999&&Light().Valid;
    }
    [Serializable] public sealed class WorldTimeSettings
    {
        public bool running,cycleEnabled;
        public double rate=1;
        public WorldLightKeyframe[] frames=Array.Empty<WorldLightKeyframe>();
        public WorldTimeSettings Copy()=>new(){running=running,cycleEnabled=cycleEnabled,rate=rate,frames=frames?.Select(x=>x?.Copy()).ToArray()};
        public bool Valid=>double.IsFinite(rate)&&rate>=0&&rate<=3600&&frames!=null&&frames.Length<=8&&(!cycleEnabled||frames.Length>=2)&&frames.All(x=>x!=null&&x.Valid)&&frames.Zip(frames.Skip(1),(a,b)=>a.second<b.second).All(x=>x);
    }
    /// <summary>One authored region clock. No wall-clock/offline catch-up and no physics time scaling.</summary>
    [Serializable] public sealed class RoomWorldTime
    {
        public int version=1,day;
        public double second=43200;
        public WorldTimeSettings settings=new();
        public RoomWorldTime Copy()=>new(){version=version,day=day,second=second,settings=settings?.Copy()};
        internal static bool PositionValid(int day,double second)=>day>=0&&day<=999999&&double.IsFinite(second)&&second>=0&&second<86400;
        public bool Valid=>version==1&&PositionValid(day,second)&&settings!=null&&settings.Valid;
        internal bool Same(RoomWorldTime other)=>other!=null&&JsonUtility.ToJson(this)==JsonUtility.ToJson(other);
        internal static bool Exact(JObject o,params string[] keys)=>o!=null&&o.Count==keys.Length&&keys.All(o.ContainsKey);
        internal static bool Number(JToken v)=>v?.Type is JTokenType.Float or JTokenType.Integer;
        internal static bool ValidWire(JObject room){
            if(room["version"]?.Type!=JTokenType.Integer)return false;
            if((int)room["version"]<28&&room["worldTime"]==null)return true;
            if(room["worldTime"] is not JObject t||!Exact(t,"version","day","second","settings")||t["version"]?.Type!=JTokenType.Integer||t["day"]?.Type!=JTokenType.Integer||!Number(t["second"]))return false;
            if(t["settings"] is not JObject s||!Exact(s,"running","rate","cycleEnabled","frames")||s["running"]?.Type!=JTokenType.Boolean||s["cycleEnabled"]?.Type!=JTokenType.Boolean||!Number(s["rate"])||s["frames"] is not JArray frames||frames.Count>8)return false;
            return frames.All(f=>f is JObject k&&Exact(k,"second","ambientColor","sunColor","ambientIntensity","sunIntensity","azimuth","elevation")&&Number(k["second"])&&k["ambientColor"]?.Type==JTokenType.String&&k["sunColor"]?.Type==JTokenType.String&&new[]{"ambientIntensity","sunIntensity","azimuth","elevation"}.All(n=>Number(k[n])));
        }
    }
    public sealed partial class RoomJournal
    {
        RoomWorldTime worldTime=new();int worldTimeRevision;
        internal RoomWorldTime WorldTime=>worldTime.Copy();
        internal int WorldTimeRevision=>worldTimeRevision;
        internal double WorldSecond=>worldTime.second;
        internal bool DayCycleEnabled=>worldTime.settings.cycleEnabled;
        void SetWorldTime(RoomWorldTime value){if(value==null)return;worldTime=value.Copy();worldTimeRevision=clock.Next++;}
        // Runtime progression never creates Undo entries or invalidates settings guards.
        // Explicit seeks/configuration and Undo do invalidate them.
        internal bool AdvanceWorldTime(double delta){
            if(!worldTime.settings.running||worldTime.settings.rate==0||!double.IsFinite(delta)||delta<=0||delta>1)return false;
            double position=worldTime.day*86400d+worldTime.second+delta*worldTime.settings.rate;
            double limit=1000000d*86400-.001;
            if(position>=limit){position=limit;worldTime.settings.running=false;worldTimeRevision=clock.Next++;}
            worldTime.day=(int)(position/86400);worldTime.second=position-worldTime.day*86400d;return true;
        }
    }
    public sealed partial class RoomEditor
    {
        internal RoomWorldTime WorldTime=>journal?.WorldTime;
        internal int WorldTimeRevision=>journal?.WorldTimeRevision??0;
        internal double WorldSecond=>journal?.WorldSecond??0;
        internal bool DayCycleEnabled=>journal?.DayCycleEnabled==true;
        bool worldTimeTickReady;float worldTimeCheckpoint;int worldTimeTickRevision=-1;
        internal bool CanChangeWorldTime(int revision,out string error){
            if(!CanEditStructures(out error))return false;
            if(!WorldIdentityReady||revision!=WorldTimeRevision){error="Read current world time before editing it";return false;}
            return true;
        }
        internal bool ConfigureWorldTime(int revision,WorldTimeSettings settings,out string error){
            if(!CanChangeWorldTime(revision,out error))return false;
            if(settings==null||!settings.Valid){error="Choose valid clock settings and up to eight ordered lighting keyframes";return false;}
            var value=WorldTime;value.settings=settings.Copy();
            return CommitPersisted(Array.Empty<RoomObjectData>(),Array.Empty<string>(),"World time settings saved",false,out error,visualOnly:true,worldTime:value);
        }
        internal bool SeekWorldTime(int revision,int day,double second,out string error){
            if(!CanChangeWorldTime(revision,out error))return false;
            if(!RoomWorldTime.PositionValid(day,second)){error="Choose a day 0–999999 and a second within that day";return false;}
            var value=WorldTime;value.day=day;value.second=second;
            return CommitPersisted(Array.Empty<RoomObjectData>(),Array.Empty<string>(),"World time moved",false,out error,visualOnly:true,worldTime:value);
        }
        internal bool WorldTimeActive=>journal!=null&&isActiveAndEnabled&&!Ownership.Suspended&&!RuntimeGate.Held&&!WriteGate.Frozen&&!saveDispatch.Frozen&&CanSaveRoom&&Frame.Valid;
        internal void TickWorldTime(double delta){
            if(!WorldTimeActive){worldTimeTickReady=false;return;}
            if(!worldTimeTickReady||worldTimeTickRevision!=WorldTimeRevision){worldTimeTickRevision=WorldTimeRevision;worldTimeTickReady=true;return;}
            using var write=WriteGate.TryWrite(out _);if(write==null||!journal.AdvanceWorldTime(delta))return;
            if(TemporaryRoom)return;
            // Persist at most once per five active seconds, and on explicit saves/lifecycle flush.
            // Do not move an already scheduled save deadline forward every frame.
            bool wasDirty=dirty;dirty=true;
            worldTimeCheckpoint+=(float)delta;
            if(!wasDirty)saveAt=Time.unscaledTime+Mathf.Max(0,5-worldTimeCheckpoint);
            if(worldTimeCheckpoint>=5)worldTimeCheckpoint=0;
        }
        internal JObject ObserveWorldTime(){
            if(journal==null)return null;var t=WorldTime;
            return new JObject{["revision"]=WorldTimeRevision,["worldId"]=WorldIdentity.worldId,["regionId"]=WorldIdentity.regionId,
                ["day"]=t.day,["second"]=t.second,["settings"]=JObject.Parse(JsonUtility.ToJson(t.settings)),
                ["advancing"]=WorldTimeActive&&t.settings.running&&t.settings.rate>0,["temporary"]=TemporaryRoom};
        }
    }
}
