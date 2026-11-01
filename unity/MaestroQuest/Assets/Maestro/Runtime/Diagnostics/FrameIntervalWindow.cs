// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
namespace Maestro.Quest.Diagnostics
{
    /// <summary>Bounded monotonic Update intervals, not GPU or compositor measurements.</summary>
    public sealed class FrameIntervalWindow
    {
        public const int Capacity=4096;
        public const double WindowSeconds=30;
        readonly double[] ends=new double[Capacity], intervals=new double[Capacity], sorted=new double[Capacity];
        int first,count;double previous=double.NaN,lastCapacityDrop=double.NegativeInfinity;
        public readonly struct Reading
        {
            public readonly int Samples;
            public readonly double Seconds,MeanMs,P95Ms,MaxMs,AgeSeconds;
            public readonly bool CapacityLimited;
            public Reading(int samples,double seconds,double mean,double p95,double max,double age,bool limited)
            {Samples=samples;Seconds=seconds;MeanMs=mean;P95Ms=p95;MaxMs=max;AgeSeconds=age;CapacityLimited=limited;}
        }
        public void Clear(){first=count=0;previous=double.NaN;lastCapacityDrop=double.NegativeInfinity;}
        void Expire(double now){while(count>0&&ends[first]<=now-WindowSeconds){first=(first+1)%Capacity;count--;}}
        public void Sample(double now)
        {
            if(!double.IsFinite(now)||now<0){Clear();return;}
            if(double.IsNaN(previous)){previous=now;return;}
            double elapsed=now-previous;
            if(elapsed==0)return; // Some timers return the same value in adjacent Updates.
            if(elapsed<0||elapsed>1000){Clear();previous=now;return;}
            previous=now;Expire(now);
            if(count==Capacity){lastCapacityDrop=ends[first];first=(first+1)%Capacity;count--;}
            int index=(first+count)%Capacity;ends[index]=now;intervals[index]=elapsed;count++;
        }
        public Reading Read(double now)
        {
            if(!double.IsFinite(now)||now<0||(!double.IsNaN(previous)&&now<previous)){Clear();return default;}
            Expire(now);if(count==0)return default;
            double seconds=0;for(int i=0;i<count;i++){double value=intervals[(first+i)%Capacity];sorted[i]=value;seconds+=value;}
            Array.Sort(sorted,0,count);
            return new Reading(count,seconds,seconds/count*1000,sorted[(int)Math.Ceiling(count*.95)-1]*1000,sorted[count-1]*1000,
                now-ends[(first+count-1)%Capacity],lastCapacityDrop>now-WindowSeconds);
        }
    }
}
