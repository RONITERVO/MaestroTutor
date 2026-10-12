// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Deliberately truncated generated microphone input. Records why zero context
// cannot be treated as real speech after EOF; this is NOT a quality pass gate.
#include "EchoProcessor.h"
#include "AudioFixture.h"
#include <array>
#include <algorithm>
#include <cmath>
#include <cstdio>
#include <vector>
constexpr double pi=3.141592653589793;
int main(){
 int cases=0;
 const int length=12*48000;std::vector<float> left(length),right(length);unsigned state=0x391472a5;
 for(int i=0;i<length;++i){auto n=[&](){state^=state<<13;state^=state>>17;state^=state<<5;return float((state/4294967295.0-.5)*.8);};left[i]=n();right[i]=n();}
 maestro_fixture::BandLimit(left);maestro_fixture::BandLimit(right);
 auto ref=[&](int i,bool r=false){return i<0?0:(r?right:left)[i];};
 for(int rate:{16000,32000,48000})for(int phase:{1,79,159,160})for(int mode:{0,1,2})for(bool cut_render:{false,true}){
  MaestroEcho* h=nullptr;if(maestro_echo_create(48000,2,rate,1,&h))return 1;
  const int step=48000/rate,end=11*rate+phase*(rate/16000);const double end16=end*16000.0/rate;
  std::array<float,960> render{};std::array<float,480> mic{};std::array<int16_t,160> out{};std::vector<float> pcm;
  for(int f=0;f<1160;++f){
   for(int i=0;i<480;++i){const int p=f*480+i;const bool active=mode!=1&&(!cut_render||p<end*step);render[i*2]=active?ref(p):0;render[i*2+1]=active?ref(p,true):0;}
   for(int i=0;i<rate/100;++i){const int p=f*(rate/100)+i;const int sample=p*step;
    const float echo=.55f*ref(sample-3840)+.22f*ref(sample-5280,true)+.13f*ref(sample-7440);
    const float near=p>=end-rate/5?float(.18*std::sin(2*pi*211*p/rate)+.1*std::sin(2*pi*881*p/rate)):0;
    mic[i]=p<end?((mode!=1?echo:0)+(mode!=0?near:0)):0;
   }
   if(maestro_echo_render(h,1,f,render.data(),960)||maestro_echo_capture(h,1,f,mic.data(),rate/100,80,out.data(),160))return 2;
   for(auto sample:out)pcm.push_back(sample/32768.f);
  }
  maestro_echo_destroy(&h);++cases;double reference=0;for(int i=int(end16)-3200;i<int(end16);++i)reference+=pcm[i]*pcm[i];
  for(int tail:{0,10,20,40,100}){const int begin=int(std::ceil(end16))+160+tail*16,finish=int(std::ceil(end16))+160+500*16;double energy=0,peak=0;
   for(int i=begin;i<finish;++i){energy+=pcm[i]*pcm[i];peak=std::max(peak,std::abs(double(pcm[i])));}
   std::printf("{\"rate\":%d,\"phase\":%d,\"signal\":\"%s\",\"renderAfterEnd\":\"%s\",\"afterDelayPlusMs\":%d,\"rmsDbfs\":%.6f,\"peakDbfs\":%.6f,\"remainingEnergyRelativeLast200ms\":%.9f}\n",rate,phase,mode==0?"echo":mode==1?"near":"both",cut_render?"zero":"continued",tail,10*std::log10(std::max(energy/(finish-begin),1e-30)),20*std::log10(std::max(peak,1e-15)),energy/std::max(reference,1e-30));
  }
 }
 std::printf("{\"status\":\"recorded\",\"scope\":\"finite-input-zero-context-diagnostic\",\"terminationCases\":%d,\"physicalAudio\":false,\"deviceOpened\":false}\n",cases);
 return cases==72?0:3;
}
