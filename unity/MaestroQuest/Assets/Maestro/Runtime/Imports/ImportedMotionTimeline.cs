// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
namespace Maestro.Quest.Imports {
    // Derived from validated source bytes, never inferred from the posing UI's
    // humanoid subset. The fixed cadence supplements, rather than replaces, keys.
    internal sealed class ImportedMotionTimeline {
        internal const int MaximumSamples=256;
        const double SamplesPerSecond=120;
        readonly float[] keys;
        internal ImportedMotionTimeline(IEnumerable<float> values){keys=values.Distinct().OrderBy(x=>x).ToArray();}
        // glTF cubic tangents are derivatives per second. Include component
        // extrema even when an entire out-and-back fits between display ticks.
        internal static void IncludeCubicExtrema(HashSet<float> keys,float[] times,float[] values){
            int width=values.Length/(times.Length*3);
            for(int i=0;i+1<times.Length;i++)for(int component=0;component<width;component++){
                double span=(double)times[i+1]-times[i],p0=values[(i*3+1)*width+component],p1=values[((i+1)*3+1)*width+component];
                double m0=values[(i*3+2)*width+component]*span,m1=values[((i+1)*3)*width+component]*span;
                double a=3*(2*p0-2*p1+m0+m1),b=2*(-3*p0+3*p1-2*m0-m1),c=m0;
                void Add(double u){if(double.IsFinite(u)&&u>0&&u<1)keys.Add((float)(times[i]+span*u));}
                if(a==0){if(b!=0)Add(-c/b);continue;}
                double discriminant=b*b-4*a*c;if(discriminant<0)continue;
                double root=Math.Sqrt(discriminant),q=-.5*(b+(b>=0?root:-root));
                Add(q/a);if(q!=0)Add(c/q);
            }
        }
        internal readonly struct Point {
            internal readonly float Time,At;internal readonly int Cycle;
            internal Point(float time,float at,int cycle){Time=time;At=at;Cycle=cycle;}
        }
        internal bool Interval(float from,float to,float duration,bool loop,out Point[] points,out string error){
            points=null;error="The authored interval exceeds its bounded path-sampling budget";
            if(!float.IsFinite(from)||!float.IsFinite(to)||!float.IsFinite(duration)||from<0||to<0||duration<=0||duration>3600)return false;
            if(!loop){from=Math.Min(from,duration);to=Math.Min(to,duration);}
            double lower=Math.Min(from,to),upper=Math.Max(from,to);
            if((upper-lower)*SamplesPerSecond>MaximumSamples||upper/duration>=int.MaxValue)return false;
            int first=loop?(int)Math.Floor(lower/duration):0,last=loop?(int)Math.Floor(upper/duration):0;
            if((long)last-first>MaximumSamples/2)return false;
            var result=new List<Point>();var seen=new HashSet<(float,float,int)>();
            bool Add(float time,float at,int cycle){
                if(time<lower||time>upper||time==from&&from!=to)return true;
                if(seen.Add((time,at,cycle)))result.Add(new Point(time,at,cycle));return result.Count<=MaximumSamples;
            }
            bool At(float time){int cycle=loop?(int)Math.Floor(time/duration):0;return Add(time,loop?time%duration:Math.Min(time,duration),cycle);}
            for(int cycle=first;cycle<=last;cycle++){
                double origin=loop?(double)cycle*duration:0;
                float begin=(float)Math.Max(0,lower-origin),end=(float)Math.Min(duration,upper-origin);
                int index=Array.BinarySearch(keys,begin);if(index<0)index=~index;
                for(;index<keys.Length&&keys[index]<=end;index++)if(!Add((float)(origin+keys[index]),keys[index],cycle))return false;
                // A loop seam owns both poses. End precedes start in forward play;
                // reversing the ordered path reverses that order as well.
                if(!Add((float)origin,0,cycle)||!Add((float)(origin+duration),duration,cycle))return false;
            }
            for(long tick=(long)Math.Floor(lower*SamplesPerSecond)+1;tick<=Math.Floor(upper*SamplesPerSecond);tick++)if(!At((float)(tick/SamplesPerSecond)))return false;
            if(!At(to))return false;
            result.Sort((a,b)=>{int order=a.Time.CompareTo(b.Time);if(order!=0)return order;order=a.Cycle.CompareTo(b.Cycle);return order!=0?order:a.At.CompareTo(b.At);});
            if(to<from)result.Reverse();
            // Fractional cycle origins can round onto the requested float time.
            // Preserve those crossed seam poses, but always finish at the same
            // modulo pose that ordinary imported playback would sample.
            int endCycle=loop?(int)Math.Floor(to/duration):0;float endAt=loop?to%duration:Math.Min(to,duration);
            if(result.Count==0||result[^1].Time!=to||result[^1].At!=endAt||result[^1].Cycle!=endCycle){
                if(result.Count>=MaximumSamples)return false;result.Add(new Point(to,endAt,endCycle));
            }
            points=result.ToArray();error=null;return true;
        }
    }
}
