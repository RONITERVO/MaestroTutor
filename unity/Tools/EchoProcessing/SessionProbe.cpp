// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#include "CaptureSession.h"
#include "CaptureEffects.h"
#include "AudioFixture.h"
#include <algorithm>
#include <cmath>
#include <cstdio>
#include <functional>
#include <stdexcept>
#include <vector>
using namespace maestro;
namespace {
int cases=0,checks=0;
void Check(bool v,const char* text){++checks;if(!v)throw std::runtime_error(text);}
void Done(const char* name){++cases;std::printf("{\"case\":\"%s\",\"status\":\"passed\"}\n",name);}
struct DeviceState {
    int64_t now=1200000000,position=0,impulse=-1;
    int starts=0,reads=0,closes=0,chunk=480,xruns=0,ready=1;
    bool start_ok=true,stall=false,throw_read=false,throw_clock=false,throw_close=false,invalid_pcm=false;
    std::function<float(int64_t)> signal=[](int64_t p){return static_cast<float>(.2*std::sin(p*.07));};
    static int64_t Clock(void* p){auto& s=*static_cast<DeviceState*>(p);if(s.throw_clock)throw std::runtime_error("clock");return s.now;}
};
struct Device:CaptureDevice {
    std::shared_ptr<DeviceState> s;
    explicit Device(std::shared_ptr<DeviceState> state):s(std::move(state)){}
    bool Start()override{++s->starts;return s->start_ok;}
    int Ready()override{return s->ready;}
    int Read(float* out,int capacity)override{
        ++s->reads;if(s->throw_read)throw std::runtime_error("read");if(s->stall)return 0;
        const int count=std::min(s->chunk,capacity);
        for(int i=0;i<count;++i)out[i]=s->invalid_pcm?2:s->position+i==s->impulse?.5f:s->signal(s->position+i);
        s->position+=count;return count;
    }
    int Timestamp(DeviceAudioStamp& stamp)override{
        const auto position=(s->now-1000000000-1000000)*48000/1000000000;
        stamp={position,1000000000+position*1000000000/48000};return 1;
    }
    int64_t Position()override{return s->position;}
    int Xruns()override{return s->xruns;}
    int64_t Now()override{return s->now;}
    void Close()override{++s->closes;if(s->throw_close)throw std::runtime_error("close");}
};
struct Fixture {
    std::shared_ptr<DeviceState> device=std::make_shared<DeviceState>();
    std::unique_ptr<CaptureSession> session;
    std::vector<int16_t> pcm;std::vector<DuplexFrame> frames;
    bool reject=false,throws=false,cancel=false,reenter=false;
    uint64_t references=0;
    std::function<float(int64_t,bool)> render_signal=[](int64_t,bool){return 0.f;};
    static bool Sink(void* p,const int16_t* values,int count,const DuplexFrame& frame){
        auto& f=*static_cast<Fixture*>(p);Check(count==160,"fixed sink PCM frame");
        if(f.throws)throw std::runtime_error("sink");if(f.reject)return false;
        f.pcm.insert(f.pcm.end(),values,values+count);f.frames.push_back(frame);
        if(f.reenter){DuplexFlush ticket;
            Check(f.session->Pump()==CapturePump::Invalid&&f.session->Flush(ticket)==FlushStatus::Invalid&&
                f.session->Finish()==FlushStatus::Invalid&&!f.session->Start(),"callback cannot recursively pump or change ownership");}
        if(f.cancel)f.session->Close();return true;
    }
    Fixture(){session=std::make_unique<CaptureSession>(std::make_unique<Device>(device),17,DeviceState::Clock,device.get(),Sink,this);}
    void Start(){Check(session->Start(),"start");}
    void Tick(int frame){device->now=1200000000+frame*10000000LL;}
    void Reference(){
        std::array<float,960> silence{};
        for(int i=0;i<480;++i){silence[2*i]=render_signal(references*480+i,false);silence[2*i+1]=render_signal(references*480+i,true);}
        const auto position=(device->now-1080000000-1000000)*48000/1000000000;
        PlaybackClock clock{this,17,references+1,{position,1080000000+position*1000000000/48000},device->now,device->now};
        PlaybackFrame frame{17,references,static_cast<int64_t>(references)*480};
        Check(session->Render(silence.data(),960,frame,clock)==DuplexStatus::Accepted,"reference admitted");++references;
    }
    CapturePump Step(int frame,bool reference=true){Tick(frame);if(reference)Reference();return session->Pump();}
    void ActiveStep(int frame){const auto result=Step(frame);Check(result==CapturePump::Progress||result==CapturePump::Waiting,"active pump");}
    void FinishBy(int start,int limit=12){
        for(int frame=start;frame<start+limit&&session->State()==CaptureSessionState::Finishing;++frame){
            const auto result=Step(frame);Check(result!=CapturePump::Faulted&&result!=CapturePump::Closed,"graceful pump");}
        Check(session->State()==CaptureSessionState::Finished&&session->PollFinish()==FlushStatus::Ready,"finished");
    }
};
void Contracts(){
    {Fixture f;DuplexFlush ticket;Check(f.session->Pump()==CapturePump::Invalid&&f.session->Flush(ticket)==FlushStatus::Invalid&&f.session->Finish()==FlushStatus::Invalid,"no implicit start");f.Start();Check(!f.session->Start()&&f.device->starts==1,"one start");}
    Done("explicit-single-owner-start");
    {Fixture f;f.Start();Check(f.session->Finish()==FlushStatus::Ready&&f.session->State()==CaptureSessionState::Finished,"empty finish");Check(f.device->reads==0&&f.device->closes==1&&f.pcm.empty(),"empty finish closes without invented data");DuplexFlush ticket;Check(f.session->Flush(ticket)==FlushStatus::Invalid,"finished cannot flush again");}
    Done("empty-graceful-finish");
    {Fixture f;f.Start();for(int i=0;i<4;++i)f.ActiveStep(i);Check(f.frames.size()==3,"only real lookahead releases frames");Check(f.device->starts==1&&f.device->closes==0,"same live device");}
    Done("composed-capture-dsp-sink");
    {Fixture f;f.device->chunk=37;f.Start();f.ActiveStep(0);DuplexFlush ticket;Check(f.session->Flush(ticket)==FlushStatus::Pending&&ticket.input_samples==37,"accepted short read enters flush");Check(f.device->reads==1,"flush did not read hardware");f.device->chunk=480;for(int i=1;i<6;++i)f.ActiveStep(i);Check(f.session->PollFlush(ticket)==FlushStatus::Ready&&f.device->closes==0,"flush leaves owner open");}
    Done("short-fragment-nonterminal-flush");
    {Fixture f;f.Start();f.ActiveStep(0);DuplexFlush first,again;Check(f.session->Flush(first)==FlushStatus::Pending,"first flush");f.ActiveStep(1);Check(f.session->Flush(again)==FlushStatus::Pending&&first.request==again.request&&first.input_samples==again.input_samples,"same pending prefix");}
    Done("nonterminal-flush-coalesces");
    {Fixture f;f.Start();for(int i=0;i<3;++i)f.ActiveStep(i);Check(f.session->Finish()==FlushStatus::Pending,"finish waits");const auto read=f.device->reads;f.FinishBy(3);Check(f.device->reads>read&&f.device->starts==1&&f.device->closes==1,"real context from same device then close");}
    Done("graceful-real-context-then-close");
    {Fixture f;f.Start();f.ActiveStep(0);DuplexFlush earlier;Check(f.session->Flush(earlier)==FlushStatus::Pending,"older flush");f.ActiveStep(1);f.ActiveStep(2);Check(f.session->Finish()==FlushStatus::Pending,"finish includes newer prefix");f.FinishBy(3);Check(f.session->PollFlush(earlier)==FlushStatus::Ready,"older caller completes before internal fence supersedes it");Check(f.frames.back().capture_first.position>=1440,"newer prefix not dropped by coalescing");}
    Done("finish-behind-older-flush");
    {Fixture f;f.Start();f.ActiveStep(0);Check(f.session->Finish()==FlushStatus::Pending&&f.session->Finish()==FlushStatus::Pending,"finish idempotent pending");f.FinishBy(1);const auto reads=f.device->reads;const auto calls=f.frames.size();Check(f.session->Finish()==FlushStatus::Ready&&f.session->Pump()==CapturePump::Finished,"finish idempotent complete");Check(f.device->reads==reads&&f.frames.size()==calls&&f.device->closes==1,"no post-finish activity");}
    Done("idempotent-graceful-finish");
    {Fixture f;f.Start();f.ActiveStep(0);DuplexFlush ticket;f.session->Flush(ticket);f.session->Finish();const auto reads=f.device->reads;const auto calls=f.frames.size();f.session->Close();f.session->Close();Check(f.session->Pump()==CapturePump::Closed&&f.session->PollFinish()==FlushStatus::Closed&&f.session->PollFlush(ticket)==FlushStatus::Closed,"cancel terminal");Check(f.device->reads==reads&&f.frames.size()==calls&&f.device->closes==1,"cancel does not drain");}
    Done("cancel-discards-without-context-read");
    {Fixture f;f.Start();f.ActiveStep(0);f.cancel=true;Check(f.Step(1)==CapturePump::Closed&&f.device->closes==1,"sink cancellation fences acknowledgment and next frame");Check(f.frames.size()==1&&f.session->Pump()==CapturePump::Closed,"no late sink delivery");}
    Done("sink-can-cancel-immediately");
    {Fixture f;f.Start();f.reenter=true;for(int i=0;i<5;++i)f.ActiveStep(i);Check(f.frames.size()==4&&f.device->starts==1,"reentry cannot duplicate processing");}
    Done("sink-reentry-guard");
    {Fixture f;f.Start();f.ActiveStep(0);f.reject=true;Check(f.Step(1)==CapturePump::Faulted&&f.session->Fault()==CaptureSessionFault::Sink&&f.device->closes==1,"rejected sink releases hardware");Check(f.pcm.empty(),"rejected sink not recorded delivered");}
    Done("sink-refusal-faults-and-releases");
    {Fixture f;f.Start();f.ActiveStep(0);f.throws=true;Check(f.Step(1)==CapturePump::Faulted&&f.session->Fault()==CaptureSessionFault::Sink&&f.device->closes==1,"sink exception contained");}
    Done("sink-exception-releases");
    {Fixture f;f.Start();f.ActiveStep(0);f.device->ready=-1;Check(f.Step(1)==CapturePump::Faulted&&f.device->closes==1,"disconnected input closes");}
    Done("device-loss-releases");
    {Fixture f;f.Start();f.ActiveStep(0);f.device->throw_read=true;Check(f.Step(1)==CapturePump::Faulted&&f.device->closes==1,"read exception contained");}
    Done("device-read-exception-releases");
    {Fixture f;f.device->start_ok=false;Check(!f.session->Start()&&f.session->State()==CaptureSessionState::Faulted&&f.device->closes==1,"failed start does not leak");}
    Done("failed-start-releases");
    {Fixture f;f.Start();f.device->invalid_pcm=true;Check(f.Step(0)==CapturePump::Faulted&&f.device->closes==1&&f.pcm.empty()&&f.session->InputFault()==CaptureFault::Samples,"invalid input never raw fallback");}
    Done("invalid-input-no-fallback");
    {Fixture f;f.Start();f.ActiveStep(0);f.session->Finish();f.device->now+=1000000000;Check(f.session->PollFinish()==FlushStatus::Faulted&&f.session->Fault()==CaptureSessionFault::Timeout&&f.device->closes==1,"one second total finish deadline");}
    Done("finish-deadline-releases");
    {Fixture f;f.Start();f.ActiveStep(0);f.session->Finish();--f.device->now;Check(f.session->PollFinish()==FlushStatus::Faulted&&f.session->Fault()==CaptureSessionFault::Clock&&f.device->closes==1,"clock rollback releases");}
    Done("finish-clock-rollback");
    {Fixture f;f.Start();f.ActiveStep(0);f.session->Finish();f.device->throw_clock=true;Check(f.session->PollFinish()==FlushStatus::Faulted&&f.device->closes==1,"clock exception releases");}
    Done("finish-clock-exception");
    {Fixture f;f.Start();f.device->throw_close=true;Check(f.session->Finish()==FlushStatus::Faulted&&f.session->Fault()==CaptureSessionFault::Release,"failed release cannot report successful finish");}
    Done("release-failure-not-success");
    {Fixture f;f.Start();f.ActiveStep(0);f.session->Finish();f.device->stall=true;for(int i=1;i<10;++i)f.ActiveStep(i);Check(f.pcm.empty()&&f.session->PollFinish()==FlushStatus::Pending,"starved input is not zero padding");f.device->now+=1000000000;Check(f.session->PollFinish()==FlushStatus::Faulted&&f.device->closes==1,"starvation eventually releases");}
    Done("no-synthetic-context-on-starvation");
}
void AdditionalContracts(){
    {auto state=std::make_shared<DeviceState>();bool threw=false;
     try{CaptureSession invalid(std::make_unique<Device>(state),17,DeviceState::Clock,state.get(),nullptr,nullptr);}
     catch(const std::invalid_argument&){threw=true;}
     Check(threw&&state->starts==0&&state->closes==1,"invalid construction releases transferred input");}
    Done("invalid-construction-releases");
    {Fixture f;f.Start();f.ActiveStep(0);std::array<float,960> reference{};
     PlaybackFrame frame{18,1,480};PlaybackClock clock{};
     Check(f.session->Render(reference.data(),960,frame,clock)==DuplexStatus::Faulted&&
        f.session->ProcessingFault()==DuplexFault::Sequence&&f.device->closes==1,"invalid reference faults owner and releases capture");
     Check(f.pcm.empty()&&f.session->Pump()==CapturePump::Faulted,"reference failure no raw fallback");}
    Done("invalid-reference-releases");
}
void EchoContextParity(){
    std::vector<float> left(12*48000),right(left.size());uint32_t random=0x43579128;
    for(size_t i=0;i<left.size();++i){auto noise=[&](){random^=random<<13;random^=random>>17;random^=random<<5;return float((random/4294967295.0-.5)*.65);};left[i]=noise();right[i]=noise();}
    maestro_fixture::BandLimit(left);maestro_fixture::BandLimit(right);
    auto source=[&](int64_t sample,bool r){return sample<0?0:(r?right:left).at(static_cast<size_t>(sample));};
    auto microphone=[&](int64_t sample){return .55f*source(sample-3840,false)+.22f*source(sample-5280,true)+
        (sample>=479040&&sample<480000?float(.2*std::sin(sample*.053)):0);};
    Fixture continuous,stopping;continuous.render_signal=source;stopping.render_signal=source;
    continuous.device->signal=microphone;stopping.device->signal=microphone;continuous.Start();stopping.Start();
    for(int frame=0;frame<1000;++frame){continuous.ActiveStep(frame);stopping.ActiveStep(frame);}
    Check(stopping.session->Finish()==FlushStatus::Pending,"echo finish pending");
    for(int frame=1000;frame<1015&&stopping.session->State()==CaptureSessionState::Finishing;++frame){continuous.ActiveStep(frame);stopping.Step(frame);}
    Check(stopping.session->State()==CaptureSessionState::Finished&&continuous.pcm==stopping.pcm,"real echo context preserves continuous PCM");
    Check(stopping.device->starts==1&&stopping.device->closes==1,"echo context never reopens input");
    Done("graceful-echo-reference-continuity");
}
void Parity(){
    Fixture continuous,stopping;continuous.Start();stopping.Start();
    for(int i=0;i<80;++i){continuous.ActiveStep(i);stopping.ActiveStep(i);}
    Check(stopping.session->Finish()==FlushStatus::Pending,"parity finish");
    for(int i=80;i<95&&stopping.session->State()==CaptureSessionState::Finishing;++i){continuous.ActiveStep(i);stopping.Step(i);}
    Check(stopping.session->State()==CaptureSessionState::Finished,"parity finished");
    Check(continuous.pcm==stopping.pcm&&continuous.device->closes==0&&stopping.device->closes==1,"graceful path identical to uninterrupted real input");
    Done("graceful-continuation-pcm-parity");
}
void FinalFragments(){
    for(int phase:{1,31,159,319,479}){Fixture f;f.device->signal=[](int64_t){return 0.f;};f.Start();for(int i=0;i<20;++i)f.ActiveStep(i);
        f.device->chunk=phase;f.device->impulse=f.device->position+phase-1;f.ActiveStep(20);
        const auto accepted_end=f.device->position;const auto cutoff=f.frames.size()*160;
        Check(f.session->Finish()==FlushStatus::Pending,"partial final input");f.device->chunk=480;f.FinishBy(21);
        int peak=0;size_t peak_at=0;for(size_t i=0;i<f.pcm.size();++i)if(std::abs(int(f.pcm[i]))>peak){peak=std::abs(int(f.pcm[i]));peak_at=i;}
        Check(peak>100&&peak_at>=cutoff&&peak_at>=size_t(accepted_end/3)&&f.device->closes==1,"last fragment survives owned shutdown before close");
    }
    Done("last-accepted-fragment-survives-finish");
}
void RejectPlatformEcho(){
    auto device=std::make_shared<DeviceState>();
    struct EffectState {int closes=0;bool input_first=false;};
    auto effect=std::make_shared<EffectState>();
    struct OwnedEffect final:CaptureEffect {
        std::shared_ptr<DeviceState> device;std::shared_ptr<EffectState> state;
        OwnedEffect(std::shared_ptr<DeviceState> d,std::shared_ptr<EffectState> s):device(d),state(s){}
        ~OwnedEffect()override{Close();}
        bool Healthy()override{return state->closes==0;}
        void Close()override{if(!state->closes){++state->closes;state->input_first=device->closes==1;}}
    };
    auto guarded=GuardCaptureEffects(std::make_unique<Device>(device),std::make_unique<OwnedEffect>(device,effect));
    Check(guarded&&guarded->HasPlatformEcho(),"platform processing declared");
    bool rejected=false;
    try{CaptureSession bad(std::move(guarded),17,DeviceState::Clock,device.get(),Fixture::Sink,nullptr);}
    catch(const std::invalid_argument&){rejected=true;}
    Check(rejected&&device->starts==0&&device->reads==0,"reject double AEC before recording");
    Check(device->closes==1&&effect->closes==1&&effect->input_first,"rejected input releases in order");
    Done("platform-echo-cannot-enter-software-aec");
}
}
int main(){try{RejectPlatformEcho();Contracts();AdditionalContracts();Parity();FinalFragments();EchoContextParity();Check(cases==28,"complete session scenarios");
    std::printf("{\"status\":\"passed\",\"sessionCases\":%d,\"assertions\":%d,\"deviceOpened\":false,\"physicalAudio\":false}\n",cases,checks);return 0;
}catch(const std::exception& e){std::fprintf(stderr,"FAILED: %s\n",e.what());return 1;}}
