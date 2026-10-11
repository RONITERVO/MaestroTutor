// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Art;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    [Serializable] public sealed class WeatherSettings
    {
        public float rainMmPerHour,windX,windZ,cloudCover,fogDensity;
        public string fogColor="#CCD6E0";
        public WeatherSettings Copy()=>(WeatherSettings)MemberwiseClone();
        public bool Valid=>RoomLighting.Range(rainMmPerHour,0,120)&&RoomLighting.Range(windX,-15,15)&&RoomLighting.Range(windZ,-15,15)&&RoomLighting.Range(cloudCover,0,1)&&RoomLighting.Range(fogDensity,0,.25f)&&RoomLighting.ColorValid(fogColor);
    }
    [Serializable] public sealed class RoomWeather
    {
        public int version=1,seed=1,startDay;
        public double startSecond=43200,transitionSeconds;
        public WeatherSettings from=new(),settings=new();
        public RoomWeather Copy()=>new(){version=version,seed=seed,startDay=startDay,startSecond=startSecond,transitionSeconds=transitionSeconds,from=from?.Copy(),settings=settings?.Copy()};
        public bool Valid=>version==1&&seed>=1&&seed<=1000000&&RoomWorldTime.PositionValid(startDay,startSecond)&&double.IsFinite(transitionSeconds)&&transitionSeconds>=0&&transitionSeconds<=86400&&from?.Valid==true&&settings?.Valid==true;
        internal float Progress(double time)=>transitionSeconds==0?1:(float)Math.Clamp((time-(startDay*86400d+startSecond))/transitionSeconds,0,1);
        internal bool Same(RoomWeather other)=>other!=null&&JsonUtility.ToJson(this)==JsonUtility.ToJson(other);
        internal static bool ValidWire(JObject room){
            if(room["version"]?.Type!=JTokenType.Integer)return false;
            if((int)room["version"]<29&&room["weather"]==null)return true;
            if(room["weather"] is not JObject w||!RoomWorldTime.Exact(w,"version","seed","startDay","startSecond","transitionSeconds","from","settings")||new[]{"version","seed","startDay"}.Any(k=>w[k]?.Type!=JTokenType.Integer)||!RoomWorldTime.Number(w["startSecond"])||!RoomWorldTime.Number(w["transitionSeconds"]))return false;
            foreach(string key in new[]{"from","settings"})if(w[key] is not JObject s||!RoomWorldTime.Exact(s,"rainMmPerHour","windX","windZ","cloudCover","fogDensity","fogColor")||s["fogColor"]?.Type!=JTokenType.String||new[]{"rainMmPerHour","windX","windZ","cloudCover","fogDensity"}.Any(k=>!RoomWorldTime.Number(s[k])))return false;
            return true;
        }
    }
    public sealed partial class RoomJournal
    {
        RoomWeather weather=new();int weatherRevision;
        internal RoomWeather Weather=>weather.Copy();internal int WeatherRevision=>weatherRevision;
        internal double WorldSeconds=>worldTime.day*86400d+worldTime.second;
        void SetWeather(RoomWeather value){if(value==null)return;weather=value.Copy();weatherRevision=clock.Next++;}
    }
    public sealed partial class RoomEditor
    {
        internal RoomWeather Weather=>journal?.Weather;internal int WeatherRevision=>journal?.WeatherRevision??0;
        internal double WorldSeconds=>journal?.WorldSeconds??43200;
        WeatherProjection weatherProjection;int weatherProjectionRevision=-1;
        internal WeatherSample CurrentWeather {get{if(weatherProjection==null||weatherProjectionRevision!=WeatherRevision){weatherProjection=new WeatherProjection(Weather);weatherProjectionRevision=WeatherRevision;}return weatherProjection.Sample(WorldSeconds);}}
        internal bool CanSetWeather(int revision,double seconds,out string error){
            if(!CanEditStructures(out error))return false;
            if(!WorldIdentityReady||revision!=WeatherRevision){error="Read current world weather before editing it";return false;}
            var time=WorldTime;
            if(seconds>0&&(!time.settings.running||time.settings.rate==0)){error="Start the world clock for a timed weather transition, or choose zero seconds for an immediate change";return false;}
            return true;
        }
        internal bool SetWeather(int revision,int seed,WeatherSettings settings,double seconds,out string error){
            if(!CanSetWeather(revision,seconds,out error))return false;
            var time=WorldTime;var value=new RoomWeather{seed=seed,from=CurrentWeather.Settings(),settings=settings?.Copy(),startDay=time.day,startSecond=time.second,transitionSeconds=seconds};
            if(!value.Valid){error="Choose valid weather settings and transition duration";return false;}
            if(!FinishLiquidPour(out error))return false;
            return CommitPersisted(Array.Empty<RoomObjectData>(),Array.Empty<string>(),"World weather saved",false,out error,visualOnly:true,weather:value);
        }
        internal JObject ObserveWeather(){
            if(journal==null)return null;var w=Weather;
            return new JObject{["revision"]=WeatherRevision,["worldId"]=WorldIdentity.worldId,["regionId"]=WorldIdentity.regionId,["seed"]=w.seed,
                ["settings"]=JObject.Parse(JsonUtility.ToJson(w.settings)),["current"]=JObject.Parse(JsonUtility.ToJson(CurrentWeather.Settings())),
                ["transition"]=new JObject{["startDay"]=w.startDay,["startSecond"]=w.startSecond,["seconds"]=w.transitionSeconds,["progress"]=w.Progress(WorldSeconds)},["temporary"]=TemporaryRoom};
        }
    }
}
