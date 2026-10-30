// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Creation;
namespace Maestro.Quest.Book {
    internal readonly struct WaterSoundMix {
        internal const double ReferenceRate=48000,MinimumCutoff=120;
        internal static readonly WaterSoundMix Dry=new(1,0);
        internal readonly float Gain,Pole;
        internal bool Applied=>Gain<1||Pole>0;
        internal WaterSoundMix(float gain,float pole){Gain=gain;Pole=pole;}
        internal static WaterSoundMix From(LiquidPath path){
            if(path?.Complete!=true)return Dry;
            double loss=0,muffling=0;
            for(int i=0;i<path.Count;i++){
                var settings=WaterAudioSettings.Effective(path.Medium(i).Contents.waterAudio);
                if(!settings.enabled)continue;
                double metres=path.MetresIn(i);loss+=metres*settings.attenuationDbPerMetre;muffling+=metres*settings.mufflingPerMetre;
            }
            // A reference-rate pole makes depth strength independent of device
            // sample rate. Clamp the time constant to bound both delay and tail.
            double pole=Math.Min(1-Math.Exp(-muffling),Math.Exp(-2*Math.PI*MinimumCutoff/ReferenceRate));
            return new((float)Math.Pow(10,-Math.Min(48,loss)/20),(float)pole);
        }
    }
}
