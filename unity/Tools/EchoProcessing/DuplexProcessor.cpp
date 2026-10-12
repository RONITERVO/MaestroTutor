// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#include "DuplexProcessor.h"
#include "EchoProcessor.h"
#include "common_audio/resampler/sinc_resampler.h"
#include <algorithm>
#include <array>
#include <cmath>
#include <limits>
#include <stdexcept>
namespace maestro {
namespace {
constexpr int frame_size=480, capacity=24000, request_size=128;
bool Valid(const float* input,int count){
    if(!input)return false;
    for(int i=0;i<count;++i)if(!std::isfinite(input[i])||std::abs(input[i])>1)return false;
    return true;
}
}
struct DuplexProcessor::State final:webrtc::SincResamplerCallback {
    struct RenderBlock { std::array<float,960> samples{};int64_t first=0; };
    uint64_t epoch,cap_sequence=0,render_sequence=0,sequence=0,clipped=0;
    MonotonicClock now;void* context;
    HardwareClock capture_clock,render_clock;
    const void* render_owner=nullptr;
    uint64_t observation=0;
    PlaybackClock last_observation{};
    MaestroEcho* echo=nullptr;
    std::unique_ptr<webrtc::SincResampler> resampler;
    std::array<float,capacity> capture{};
    std::array<RenderBlock,50> render{};
    std::array<float,480> resampled{};
    int64_t cap_first=0,cap_next=0,render_next=0;
    uint64_t written=0,read=0,render_written=0,render_read=0;
    // Whole/fraction split keeps the resampling cursor precise across long calls.
    uint64_t source_whole=0;double source_fraction=0,ratio=1;
    DuplexFault fault=DuplexFault::None;
    bool closed=false,starved=false;
    int64_t last_now=0,first_process=-1;
    State(uint64_t generation,MonotonicClock clock,void* user):epoch(generation),now(clock),context(user){
        if(!epoch||!now)throw std::invalid_argument("Duplex requires an epoch and monotonic clock");
        if(maestro_echo_create(48000,2,48000,epoch,&echo)!=MAESTRO_ECHO_OK)throw std::runtime_error("Echo processor unavailable");
        try{resampler=std::make_unique<webrtc::SincResampler>(1,request_size,this);}
        catch(...){maestro_echo_destroy(&echo);throw;}
    }
    ~State(){Retire();}
    void Retire(){
        maestro_echo_destroy(&echo);resampler.reset();capture.fill(0);resampled.fill(0);
        for(auto& b:render)b.samples.fill(0);
    }
    DuplexStatus Fail(DuplexFault reason){if(fault==DuplexFault::None)fault=reason;Retire();return DuplexStatus::Faulted;}
    DuplexStatus Status()const{return closed?DuplexStatus::Closed:fault!=DuplexFault::None?DuplexStatus::Faulted:DuplexStatus::Accepted;}
    // SincResampler invokes this synchronously on the owner worker, not an audio
    // callback. Preflight guarantees full input. Any violation invalidates output.
    void Run(size_t frames,float* destination)override{
        if(frames!=request_size||written-read<frames){std::fill_n(destination,frames,0.0f);starved=true;return;}
        for(size_t i=0;i<frames;++i)destination[i]=capture[(read+i)%capacity];
        read+=frames;
    }
    bool ClockNow(int64_t& value){
        try{value=now(context);}catch(...){return false;}
        if(value<0||value<last_now)return false;last_now=value;return true;
    }
};
DuplexProcessor::DuplexProcessor(uint64_t epoch,MonotonicClock clock,void* context):state_(std::make_unique<State>(epoch,clock,context)){}
DuplexProcessor::~DuplexProcessor()=default;
DuplexStatus DuplexProcessor::Capture(const float* input,int count,const CaptureFrame& frame){
    auto& s=*state_;if(s.Status()!=DuplexStatus::Accepted)return s.Status();
    if(count<1||count>480||frame.samples!=count||!Valid(input,count))return s.Fail(DuplexFault::Input);
    if(frame.epoch!=s.epoch||frame.sequence!=s.cap_sequence||frame.sequence==UINT64_MAX||frame.first_position<0||
       frame.first_position>INT64_MAX-count||(s.cap_sequence&&frame.first_position!=s.cap_next))return s.Fail(DuplexFault::Sequence);
    if(s.written-s.read>static_cast<uint64_t>(capacity-count))return s.Fail(DuplexFault::Overflow);
    if(!s.capture_clock.Observe(frame.hardware,frame.query_started_ns,frame.query_finished_ns))return s.Fail(DuplexFault::Clock);
    if(!s.cap_sequence)s.cap_first=frame.first_position;
    for(int i=0;i<count;++i)s.capture[(s.written+i)%capacity]=input[i];
    s.written+=count;s.cap_next=frame.first_position+count;++s.cap_sequence;return DuplexStatus::Accepted;
}
DuplexStatus DuplexProcessor::Render(const float* input,int count,const PlaybackFrame& frame,const PlaybackClock& clock){
    auto& s=*state_;if(s.Status()!=DuplexStatus::Accepted)return s.Status();
    if(count!=960||!Valid(input,count))return s.Fail(DuplexFault::Input);
    if(frame.epoch!=s.epoch||frame.sequence!=s.render_sequence||frame.sequence==UINT64_MAX||frame.first_position<0||
       frame.first_position>INT64_MAX-480||(s.render_sequence&&frame.first_position!=s.render_next))return s.Fail(DuplexFault::Sequence);
    if(s.render_written-s.render_read==s.render.size())return s.Fail(DuplexFault::Overflow);
    if(clock.epoch!=s.epoch||!clock.owner||!clock.observation||(s.render_owner&&s.render_owner!=clock.owner)||clock.observation<s.observation)
        return s.Fail(DuplexFault::Clock);
    if(clock.observation==s.observation&&
       (clock.hardware.position!=s.last_observation.hardware.position||clock.hardware.time_ns!=s.last_observation.hardware.time_ns||
        clock.query_started_ns!=s.last_observation.query_started_ns||clock.query_finished_ns!=s.last_observation.query_finished_ns))return s.Fail(DuplexFault::Clock);
    if(!s.render_clock.Observe(clock.hardware,clock.query_started_ns,clock.query_finished_ns))return s.Fail(DuplexFault::Clock);
    s.render_owner=clock.owner;s.observation=clock.observation;s.last_observation=clock;
    auto& block=s.render[s.render_written%s.render.size()];std::copy_n(input,960,block.samples.data());block.first=frame.first_position;
    ++s.render_written;s.render_next=frame.first_position+480;++s.render_sequence;return DuplexStatus::Accepted;
}
DuplexStatus DuplexProcessor::Process(int16_t* output,int count,DuplexFrame& frame){
    frame={};if(output&&count==160)std::fill_n(output,count,int16_t{0});
    auto& s=*state_;if(s.Status()!=DuplexStatus::Accepted)return s.Status();
    if(!output||count!=160)return DuplexStatus::Invalid;
    int64_t analyze;if(!s.ClockNow(analyze))return s.Fail(DuplexFault::Clock);
    if(s.first_process<0)s.first_process=analyze;
    if((s.cap_sequence&&!s.capture_clock.Fresh(analyze))||(s.render_sequence&&!s.render_clock.Fresh(analyze))||
       ((!s.cap_sequence||!s.render_sequence)&&analyze-s.first_process>1000000000))return s.Fail(DuplexFault::Clock);
    if(!s.cap_sequence||s.render_written==s.render_read)return DuplexStatus::Waiting;
    const double target=s.render_clock.Period()/s.capture_clock.Period();
    if(!std::isfinite(target)||target<.995||target>1.005)return s.Fail(DuplexFault::Clock);
    // Slew at <=10 ppm per processed block, preserving phase instead of skipping
    // samples when a fresh fit changes the estimated clock rate.
    const double ratio=s.ratio+std::clamp(target-s.ratio,-.00001,.00001);
    const double last=s.source_fraction+479*ratio;
    const uint64_t last_whole=s.source_whole+static_cast<uint64_t>(std::floor(last));
    // Pinned upstream kernel is centered on the source cursor and needs future
    // samples. Its reader requests 128-sample blocks. Never zero-pad missing input.
    static_assert(webrtc::SincResampler::kKernelSize==32);
    const uint64_t needed=((last_whole+17)/request_size+1)*request_size;
    if(s.written<needed)return DuplexStatus::Waiting;
    if(s.source_whole>static_cast<uint64_t>(INT64_MAX-s.cap_first))return s.Fail(DuplexFault::Sequence);
    const SamplePoint first{s.cap_first+static_cast<int64_t>(s.source_whole),s.source_fraction};
    auto& block=s.render[s.render_read%s.render.size()];MeasuredDelay delay;
    // Check timing before consuming the resampler. The second measurement below
    // includes actual reverse-processing time and can still fail terminally.
    if(!MeasureDelay(s.render_clock,s.capture_clock,{block.first,0},first,analyze,analyze,delay))return s.Fail(DuplexFault::Clock);
    s.ratio=ratio;s.resampler->SetRatio(ratio);s.resampler->Resample(480,s.resampled.data());
    if(s.starved)return s.Fail(DuplexFault::Processing);
    for(float& sample:s.resampled){
        if(!std::isfinite(sample))return s.Fail(DuplexFault::Processing);
        if(std::abs(sample)>1){sample=std::clamp(sample,-1.0f,1.0f);++s.clipped;}
    }
    if(!s.ClockNow(analyze))return s.Fail(DuplexFault::Clock);
    if(maestro_echo_render(s.echo,s.epoch,s.sequence,block.samples.data(),960)!=MAESTRO_ECHO_OK)return s.Fail(DuplexFault::Processing);
    int64_t process;if(!s.ClockNow(process)||!MeasureDelay(s.render_clock,s.capture_clock,{block.first,0},first,analyze,process,delay))return s.Fail(DuplexFault::Clock);
    if(maestro_echo_capture(s.echo,s.epoch,s.sequence,s.resampled.data(),480,delay.milliseconds,output,160)!=MAESTRO_ECHO_OK)return s.Fail(DuplexFault::Processing);
    frame={s.epoch,s.sequence++,first,block.first,delay,ratio,s.clipped};
    const double advance=s.source_fraction+480*ratio;const auto whole=static_cast<uint64_t>(std::floor(advance));
    s.source_whole+=whole;s.source_fraction=advance-static_cast<double>(whole);
    block.samples.fill(0);++s.render_read;return DuplexStatus::Frame;
}
DuplexFault DuplexProcessor::Fault()const{return state_->fault;}
void DuplexProcessor::Close(){auto& s=*state_;if(s.closed)return;s.closed=true;s.Retire();}
}
