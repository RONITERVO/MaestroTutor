// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#include "CaptureInput.h"
#include <algorithm>
#include <array>
#include <cstdio>
#include <limits>
#include <stdexcept>
#include <vector>
using namespace maestro;
namespace {
int cases = 0, assertions = 0;
void Check(bool value, const char* message) { ++assertions; if (!value) throw std::runtime_error(message); }
void Done(const char* name) { ++cases; std::printf("{\"case\":\"%s\",\"status\":\"passed\"}\n", name); }
struct State {
    int starts=0, reads=0, closes=0, destroys=0, ready=1, timestamp=1, xruns=0;
    bool start_ok=true, closed=false, read_error=false, xrun_during_read=false;
    int64_t now=1000000000, position=0, jump_after_read=0;
    DeviceAudioStamp stamp{480,990000000};
    std::vector<int> chunks;
    float sample=.125f;
};
class ScriptedDevice final : public CaptureDevice {
    std::shared_ptr<State> s;
public:
    explicit ScriptedDevice(std::shared_ptr<State> value):s(value){}
    ~ScriptedDevice() override { Close(); ++s->destroys; }
    bool Start() override { ++s->starts; return s->start_ok; }
    int Ready() override { return s->ready; }
    int Read(float* output,int capacity) override {
        const int index=s->reads++;
        if(s->read_error) return -1;
        const int count=index<static_cast<int>(s->chunks.size())?std::min(capacity,s->chunks[index]):capacity;
        std::fill_n(output,count,s->sample);s->position+=count;
        if(s->xrun_during_read) ++s->xruns;
        s->now+=s->jump_after_read;
        return count;
    }
    int Timestamp(DeviceAudioStamp& stamp) override { if(s->timestamp==1) stamp=s->stamp; return s->timestamp; }
    int64_t Position() override { return s->position; }
    int Xruns() override { return s->xruns; }
    int64_t Now() override { return s->now; }
    void Close() override { if(!s->closed){s->closed=true;++s->closes;} }
};
struct Fixture {
    std::shared_ptr<State> s=std::make_shared<State>();
    CaptureInput input{std::make_unique<ScriptedDevice>(s),17};
    std::array<float,CaptureInput::Samples> pcm{};
    CaptureFrame frame{};
    void Start(){Check(input.Start(),"start");}
    CaptureRead Read(){pcm.fill(.9f);frame.epoch=99;return input.Read(pcm.data(),static_cast<int>(pcm.size()),frame);}
    void Empty(){Check(std::all_of(pcm.begin(),pcm.end(),[](float x){return x==0;}),"no stale/raw output");Check(frame.epoch==0,"no stale metadata");}
    void Fault(CaptureFault reason){Check(Read()==CaptureRead::Faulted,"fault expected");Empty();Check(input.Fault()==reason,"fault reason");Check(s->closes==1&&s->destroys==1,"fault retires device");Check(Read()==CaptureRead::Faulted,"fault latched");Check(!input.Start(),"fault cannot restart");}
};
}
// Retain the real Android adapter in the cross-linked probe without ever opening
// a microphone. This executable runs scripted devices only, on every platform.
auto (*volatile linked_android_factory)() -> std::unique_ptr<CaptureDevice> = OpenAndroidCapture;
int main(){try{
    {Fixture f;Check(f.s->starts==0,"prepare does not start");f.Start();Check(!f.input.Start(),"single start");f.input.Close();f.input.Close();Check(f.s->closes==1&&f.s->destroys==1,"close once");Check(f.Read()==CaptureRead::Closed,"closed terminal");f.Empty();
#ifndef __ANDROID__
    Check(!OpenAndroidCapture(),"desktop never acquires microphone");
#endif
    auto invalid=std::make_shared<State>();bool rejected=false;
    try{CaptureInput bad(std::make_unique<ScriptedDevice>(invalid),0);}catch(const std::invalid_argument&){rejected=true;}
    Check(rejected&&invalid->closes==1&&invalid->destroys==1,"invalid epoch releases acquisition");
    }Done("owned-prepare-start-close");
    {Fixture f;f.Start();f.s->ready=0;Check(f.Read()==CaptureRead::Waiting,"asynchronous start");f.Empty();f.s->ready=1;f.s->timestamp=0;Check(f.Read()==CaptureRead::Waiting,"unavailable stamp");f.Empty();Check(f.s->reads==0,"no unmeasured reads");f.s->timestamp=1;Check(f.Read()==CaptureRead::Frame,"timestamp becomes available");Check(f.frame.hardware.time_ns==990000000&&f.frame.query_started_ns==1000000000,"hardware and arrival distinct");
    Fixture late;late.Start();late.s->now+=900000000;late.s->stamp.time_ns=late.s->now-10000000;late.s->chunks={240,240};
    Check(late.Read()==CaptureRead::Waiting,"late first timestamp may retain partial frame");
    late.s->now+=10000000;Check(late.Read()==CaptureRead::Frame,"late startup completes within fresh window");
    }Done("timestamp-startup");
    {Fixture f;f.Start();f.s->timestamp=0;f.s->now+=1000000001;f.Fault(CaptureFault::Timeout);}Done("startup-deadline");
    {Fixture f;f.Start();f.s->chunks={100,200,180};Check(f.Read()==CaptureRead::Waiting,"first partial");f.Empty();Check(f.Read()==CaptureRead::Waiting,"second partial");f.Empty();Check(f.Read()==CaptureRead::Frame,"assembled frame");Check(f.frame.first_position==0&&f.frame.sequence==0&&f.frame.epoch==17,"frame identity");Check(std::all_of(f.pcm.begin(),f.pcm.end(),[](float x){return x==.125f;}),"samples preserved");Check(f.Read()==CaptureRead::Frame&&f.frame.first_position==480&&f.frame.sequence==1,"contiguous next frame");}Done("partial-frame-assembly");
    {Fixture f;f.Start();f.s->chunks={0,480};Check(f.Read()==CaptureRead::Waiting,"zero read");f.Empty();Check(f.Read()==CaptureRead::Frame&&f.frame.first_position==0,"retry zero read");}Done("nonblocking-empty-read");
    {Fixture f;Check(f.Read()==CaptureRead::Invalid,"read before start");f.Empty();f.Start();std::array<float,479> short_pcm;short_pcm.fill(.9f);Check(f.input.Read(short_pcm.data(),479,f.frame)==CaptureRead::Invalid,"short buffer");Check(short_pcm[0]==.9f&&short_pcm.back()==.9f,"wrong buffer untouched");Check(f.input.Read(nullptr,480,f.frame)==CaptureRead::Invalid,"null buffer");Check(f.s->reads==0,"invalid calls never read device");}Done("buffer-boundaries");
    {Fixture f;f.s->start_ok=false;Check(!f.input.Start(),"failed start");Check(f.s->closes==1&&f.s->destroys==1,"failed start closes");Check(f.Read()==CaptureRead::Faulted,"start failure latched");f.Empty();}Done("start-failure-release");
    {Fixture f;f.Start();Check(f.Read()==CaptureRead::Frame,"first frame");f.s->stamp.position--;f.Fault(CaptureFault::Clock);}Done("hardware-position-rollback");
    {Fixture f;f.Start();Check(f.Read()==CaptureRead::Frame,"first frame");f.s->now+=490000000;f.s->chunks={480,0};Check(f.Read()==CaptureRead::Waiting,"same timestamp temporarily valid");f.s->now+=20000000;f.Fault(CaptureFault::Timeout);
    Fixture stale;stale.Start();Check(stale.Read()==CaptureRead::Frame,"first timed frame");stale.s->now+=490000000;
    Check(stale.Read()==CaptureRead::Frame,"freshness boundary");stale.s->now+=1000000;stale.Fault(CaptureFault::Clock);
    }Done("stale-progress-deadline");
    {Fixture f;f.Start();Check(f.Read()==CaptureRead::Frame,"first frame");++f.s->position;f.Fault(CaptureFault::Gap);}Done("sample-position-gap");
    {Fixture f;f.Start();++f.s->xruns;f.Fault(CaptureFault::Overflow);Check(f.s->reads==0,"overflow prevents read");}Done("input-overrun-before-read");
    {Fixture f;f.Start();f.s->xrun_during_read=true;f.Fault(CaptureFault::Overflow);}Done("input-overrun-during-read");
    {Fixture f;f.Start();f.s->read_error=true;f.Fault(CaptureFault::Device);}Done("read-error-release");
    {for(float bad:{std::numeric_limits<float>::quiet_NaN(),std::numeric_limits<float>::infinity(),1.01f}){Fixture f;f.Start();f.s->sample=bad;f.Fault(CaptureFault::Samples);}}Done("invalid-input-samples");
    {Fixture f;f.Start();Check(f.Read()==CaptureRead::Frame,"first frame");f.s->ready=-1;f.Fault(CaptureFault::Device);}Done("device-disconnect");
    {Fixture f;f.Start();f.s->timestamp=-1;f.Fault(CaptureFault::Device);}Done("timestamp-unsupported");
    {for(int kind=0;kind<3;++kind){Fixture f;f.Start();Check(f.Read()==CaptureRead::Frame,"first frame");if(kind==0)--f.s->stamp.time_ns;if(kind==1)++f.s->stamp.time_ns;if(kind==2)f.s->stamp.time_ns=f.s->now+1;f.Fault(CaptureFault::Clock);}}Done("incoherent-hardware-timestamps");
    {Fixture f;f.Start();--f.s->now;f.Fault(CaptureFault::Clock);}Done("observation-clock-rollback");
    {Fixture f;f.Start();f.s->jump_after_read=500000001;f.Fault(CaptureFault::Clock);}Done("stalled-worker-does-not-deliver");
    {for(int i=0;i<64;++i){auto s=std::make_shared<State>();s->chunks={30};{CaptureInput input(std::make_unique<ScriptedDevice>(s),i+1);Check(input.Start(),"repeat start");std::array<float,480> pcm;CaptureFrame frame;Check(input.Read(pcm.data(),480,frame)==CaptureRead::Waiting,"pending owned samples");}Check(s->closes==1&&s->destroys==1,"destruction releases pending capture");}}Done("pending-close-and-repeated-ownership");
    Check(cases==20,"complete scenario list");
    std::printf("{\"status\":\"passed\",\"captureCases\":%d,\"assertions\":%d,\"physicalAudio\":false,\"deviceOpened\":false}\n",cases,assertions);return 0;
}catch(const std::exception& error){std::fprintf(stderr,"FAILED: %s\n",error.what());return 1;}}
