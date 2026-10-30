// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#pragma once
#include "AudioFixture.h"
#include <algorithm>
#include <cmath>
#include <cstdint>
#include <stdexcept>
#include <vector>

// Exact generated sources shared by quality acceptance and history attribution.
// Keep their arithmetic/timeline unchanged when adding diagnostic controls.
namespace maestro_aligned_fixture {
constexpr double FixturePi=3.141592653589793;
inline float Wave(double sample,bool right=false){
    if(sample<0)return 0;
    const double t=sample/48000,shift=right?.37:0;
    const double frequencies[]={173,379,631,1061,1777,2599,3701,5113};double sum=0;
    for(int j=0;j<8;++j)sum+=std::sin(2*FixturePi*(frequencies[j]*t+.11*std::sin((j+1.0)*t+shift))+j*.61+shift);
    return static_cast<float>(.055*(.65+.35*std::sin(2*FixturePi*2.7*t+shift))*sum);
}
enum class NearSignal { Original, Overlapping, QuietOverlapping, BurstyOverlapping };
inline const char* Name(NearSignal signal){
    switch(signal){case NearSignal::Original:return "original";case NearSignal::Overlapping:return "overlapping-voiced";
        case NearSignal::QuietOverlapping:return "quiet-overlapping-voiced";case NearSignal::BurstyOverlapping:return "bursty-overlapping-voiced";}
    throw std::runtime_error("Unknown near signal");
}
inline float Near(double seconds,NearSignal signal){
    if(signal==NearSignal::Original)return static_cast<float>(.16*(.75+.25*std::sin(17*seconds))*(std::sin(2*FixturePi*211*seconds)+.3*std::sin(2*FixturePi*422*seconds)+.15*std::sin(2*FixturePi*844*seconds)));
    // This fundamental overlaps one render tone, with independent pitch modulation
    // and non-overlapping harmonics. It is not a copy of the reference or speech.
    const double phase=2*FixturePi*(173*seconds+.8*std::sin(2.3*seconds));
    const double wave=.6*std::sin(phase)+.28*std::sin(2*phase)+.15*std::sin(4*phase)+.08*std::sin(7*phase);
    const double amplitude=signal==NearSignal::QuietOverlapping?.016:signal==NearSignal::BurstyOverlapping?.04:.16;
    double envelope=.75+.25*std::sin(17*seconds);
    if(signal==NearSignal::BurstyOverlapping){
        const double offset=seconds-std::floor(seconds);
        envelope=seconds>=6&&offset<.28?std::min(offset/.01,1.0):0;
    }
    return static_cast<float>(amplitude*envelope*wave);
}
struct Broadband {
    std::vector<float> left,right;
    Broadband():left(18*48000+4096),right(left.size()){
        uint32_t state=0x5184ac21;
        for(size_t i=0;i<left.size();++i){
            auto sample=[&]{state^=state<<13;state^=state>>17;state^=state<<5;return static_cast<float>((state/4294967295.0-.5)*.65);};
            left[i]=sample();right[i]=sample();
        }
        maestro_fixture::BandLimit(left);maestro_fixture::BandLimit(right);
    }
    float At(double position,bool r=false)const{
        if(position<0)return 0;
        const auto& v=r?right:left;const auto i=static_cast<size_t>(position);
        if(i+1>=v.size())throw std::runtime_error("Fixture timeline exhausted");
        const double fraction=position-static_cast<double>(i);
        return static_cast<float>(v[i]*(1-fraction)+v[i+1]*fraction);
    }
};
enum class Pattern { Broadband, Tonal, WarmTonal, WarmTonalPathChange };
inline const char* Name(Pattern pattern){
    switch(pattern){case Pattern::Broadband:return "broadband";case Pattern::Tonal:return "cold-tone";
        case Pattern::WarmTonal:return "tone-after-broadband";case Pattern::WarmTonalPathChange:return "tone-after-broadband-path-change";}
    throw std::runtime_error("Unknown signal pattern");
}
}
