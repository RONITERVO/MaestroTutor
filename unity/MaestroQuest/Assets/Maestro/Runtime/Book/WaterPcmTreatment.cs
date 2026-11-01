// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Threading;
namespace Maestro.Quest.Book {
    // Main thread publishes a tiny value snapshot under this lock; all filtering
    // and history belong exclusively to the audio thread. No world/Unity calls,
    // allocations or geometry queries occur inside Process or while locked.
    internal sealed class WaterPcmTreatment {
        readonly object gate=new();
        readonly float[] history=new float[8];
        WaterSoundMix target=WaterSoundMix.Dry;
        double expires=double.PositiveInfinity;
        float gain=1,pole,goalGain=1,goalPole,gainStep,poleStep;
        int remaining,lastRate,lastChannels,resetRequested;
        // Pause can suspend callbacks entirely. Request a reset without touching
        // audio-thread history; repeated requests coalesce at the next block.
        internal void RequestReset()=>Volatile.Write(ref resetRequested,1);
        internal void Publish(WaterSoundMix value,double until){
            if(!float.IsFinite(value.Gain)||value.Gain<0||value.Gain>1||!float.IsFinite(value.Pole)||value.Pole<0||value.Pole>=1||double.IsNaN(until))throw new ArgumentException("Invalid water audio parameters");
            lock(gate){target=value;expires=until;}
        }
        internal WaterSoundMix Read(double now){lock(gate)return double.IsFinite(now)&&now<=expires?target:WaterSoundMix.Dry;}
        internal void Reset(){Array.Clear(history,0,history.Length);gain=goalGain=1;pole=goalPole=0;remaining=lastRate=lastChannels=0;}
        internal void Process(float[] data,int channels,int rate,double now){
            if(data==null||channels<1||channels>history.Length||data.Length%channels!=0||rate<24000||rate>192000||!double.IsFinite(now)){if(data!=null)Array.Clear(data,0,data.Length);Reset();return;}
            if(Interlocked.Exchange(ref resetRequested,0)!=0||lastRate!=rate||lastChannels!=channels){Reset();lastRate=rate;lastChannels=channels;}
            var value=Read(now);float nextPole=value.Pole==0?0:(float)Math.Pow(value.Pole,WaterSoundMix.ReferenceRate/rate);
            if(value.Gain!=goalGain||nextPole!=goalPole){
                goalGain=value.Gain;goalPole=nextPole;remaining=Math.Max(1,rate/50);
                gainStep=(goalGain-gain)/remaining;poleStep=(goalPole-pole)/remaining;
            }
            if(remaining==0&&gain==1&&pole==0){
                if(data.Length>=channels)for(int c=0;c<channels;c++)history[c]=data[data.Length-channels+c];
                return; // Default/off/fully recovered output is bit-exact.
            }
            for(int frame=0;frame<data.Length;frame+=channels){
                if(remaining>0){gain+=gainStep;pole+=poleStep;if(--remaining==0){gain=goalGain;pole=goalPole;}}
                for(int c=0;c<channels;c++){
                    float sample=data[frame+c],filtered=sample+(history[c]-sample)*pole;
                    history[c]=filtered;data[frame+c]=filtered*gain;
                }
            }
        }
    }
}
