// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Art
{
    internal struct WeatherSample
    {
        internal float Rain,WindX,WindZ,Cloud,Fog;internal Color FogColor;
        internal Vector3 RainVelocity=>new(WindX*.35f,-6,WindZ*.35f);
        internal static WeatherSample From(WeatherSettings s){ColorUtility.TryParseHtmlString(s.fogColor,out var c);return new(){Rain=s.rainMmPerHour,WindX=s.windX,WindZ=s.windZ,Cloud=s.cloudCover,Fog=s.fogDensity,FogColor=c};}
        internal static WeatherSample Lerp(WeatherSample a,WeatherSample b,float t)=>new(){Rain=Mathf.Lerp(a.Rain,b.Rain,t),WindX=Mathf.Lerp(a.WindX,b.WindX,t),WindZ=Mathf.Lerp(a.WindZ,b.WindZ,t),Cloud=Mathf.Lerp(a.Cloud,b.Cloud,t),Fog=Mathf.Lerp(a.Fog,b.Fog,t),FogColor=Color.Lerp(a.FogColor,b.FogColor,t)};
        internal WeatherSettings Settings()=>new(){rainMmPerHour=Rain,windX=WindX,windZ=WindZ,cloudCover=Cloud,fogDensity=Fog,fogColor="#"+ColorUtility.ToHtmlStringRGB(FogColor)};
        internal WorldLightSample Apply(WorldLightSample light){light.Sun*=1-.9f*Cloud;light.Ambient*=1-.35f*Cloud;return light;}
    }
    internal sealed class WeatherProjection
    {
        readonly RoomWeather saved;readonly WeatherSample from,to;
        internal WeatherProjection(RoomWeather value){saved=value.Copy();from=WeatherSample.From(saved.from);to=WeatherSample.From(saved.settings);}
        internal WeatherSample Sample(double seconds)=>WeatherSample.Lerp(from,to,saved.Progress(seconds));
    }
}
