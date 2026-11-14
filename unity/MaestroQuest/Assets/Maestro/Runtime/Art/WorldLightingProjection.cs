// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Art
{
    internal struct WorldLightSample
    {
        internal bool Enabled;
        internal Vector3 Ambient,Sun;
        internal float Azimuth,Elevation;
        internal Vector3 Direction=>new(Mathf.Sin(Azimuth*Mathf.Deg2Rad)*Mathf.Cos(Elevation*Mathf.Deg2Rad),Mathf.Sin(Elevation*Mathf.Deg2Rad),Mathf.Cos(Azimuth*Mathf.Deg2Rad)*Mathf.Cos(Elevation*Mathf.Deg2Rad));
        internal static WorldLightSample From(RoomLighting light)=>new(){Enabled=light.enabled,Ambient=Energy(light.ambientColor,light.ambientIntensity),Sun=Energy(light.sunColor,light.sunIntensity),Azimuth=light.azimuth,Elevation=light.elevation};
        static Vector3 Energy(string hex,float intensity){ColorUtility.TryParseHtmlString(hex,out var color);if(QualitySettings.activeColorSpace==ColorSpace.Linear)color=color.linear;return new Vector3(color.r,color.g,color.b)*intensity;}
        internal static WorldLightSample Blend(WorldLightSample a,WorldLightSample b,float t)=>new(){Enabled=true,Ambient=Vector3.Lerp(a.Ambient,b.Ambient,t),Sun=Vector3.Lerp(a.Sun,b.Sun,t),Azimuth=Mathf.Repeat(a.Azimuth+Mathf.DeltaAngle(a.Azimuth,b.Azimuth)*t+180,360)-180,Elevation=Mathf.Lerp(a.Elevation,b.Elevation,t)};
        internal JObject Observe(){
            JObject Rgb(Vector3 v)=>new(){["r"]=v.x,["g"]=v.y,["b"]=v.z};
            return new JObject{["enabled"]=Enabled,["ambient"]=Rgb(Ambient),["sun"]=Rgb(Sun),["azimuth"]=Azimuth,["elevation"]=Elevation,["colorSpace"]=QualitySettings.activeColorSpace==ColorSpace.Linear?"linear":"gamma"};
        }
    }
    /// <summary>Cached keyframes, sampled without allocation; interpolation wraps at midnight.</summary>
    internal sealed class WorldLightingProjection
    {
        readonly WorldLightSample fallback;readonly WorldLightSample[] frames;readonly double[] seconds;
        internal readonly bool Cycle;
        internal WorldLightingProjection(RoomLighting manual,WorldTimeSettings settings){
            fallback=WorldLightSample.From(manual);Cycle=settings.cycleEnabled;
            frames=Cycle?settings.frames.Select(x=>WorldLightSample.From(x.Light())).ToArray():Array.Empty<WorldLightSample>();
            seconds=Cycle?settings.frames.Select(x=>x.second).ToArray():Array.Empty<double>();
        }
        internal WorldLightSample Sample(double second){
            if(!Cycle)return fallback;
            int after=0;while(after<seconds.Length&&seconds[after]<=second)after++;
            int next=after%frames.Length,previous=(next+frames.Length-1)%frames.Length;
            double start=seconds[previous],end=seconds[next];if(end<=start)end+=86400;if(second<start)second+=86400;
            return WorldLightSample.Blend(frames[previous],frames[next],(float)((second-start)/(end-start)));
        }
    }
}
