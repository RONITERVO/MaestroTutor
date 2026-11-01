// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#include "DuplexProcessor.h"
#include "EchoProcessor.h"
#include <algorithm>
#include <array>
#include <cmath>
#include <cstdio>
#include <limits>
#include <stdexcept>
#include <vector>
using namespace maestro;
namespace {
int cases=0,checks=0;
void Check(bool value,const char* message){++checks;if(!value)throw std::runtime_error(message);}
void Done(const char* name){++cases;std::printf("{\"case\":\"%s\",\"status\":\"passed\"}\n",name);}
struct Fixture {
    int64_t now=1200000000;bool clock_throws=false;
    double capture_rate=48000,render_rate=48000;
    static int64_t Clock(void* p){auto& f=*static_cast<Fixture*>(p);if(f.clock_throws)throw std::runtime_error("clock");return f.now;}
    DuplexProcessor processor{7,Clock,this};
    uint64_t input=0,chunks=0,references=0;
    std::array<float,480> mic{};std::array<float,960> silence{};
    std::array<int16_t,160> out{};DuplexFrame result;
    void Tick(int frame){now=1200000000+static_cast<int64_t>(std::llround(frame*480*1e9/render_rate));}
    DeviceAudioStamp Stamp(bool capture)const{
        const double rate=capture?capture_rate:render_rate;
        const int64_t origin=capture?1000000000:1080000000;
        const int64_t position=static_cast<int64_t>(std::floor((now-origin-1000000)*rate/1e9));
        return {position,origin+static_cast<int64_t>(std::llround(position*1e9/rate))};
    }
    void Feed(int count,int64_t impulse=-1,bool tone=false){
        while(count>0){const int n=std::min(count,480);
            for(int i=0;i<n;++i)mic[i]=static_cast<int64_t>(input+i)==impulse?.5f:
                tone?static_cast<float>(.2*std::sin((input+i)*.07)):0;
            CaptureFrame frame{7,chunks++,static_cast<int64_t>(input),Stamp(true),now,now,n};
            Check(processor.Capture(mic.data(),n,frame)==DuplexStatus::Accepted,"capture admitted");
            input+=n;count-=n;
        }
    }
    void Ensure(uint64_t wanted,int64_t impulse=-1,bool tone=false){
        if(input<wanted)Feed(static_cast<int>(wanted-input),impulse,tone);
    }
    void Reference(){
        PlaybackFrame frame{7,references,static_cast<int64_t>(references)*480};
        PlaybackClock clock{this,7,references+1,Stamp(false),now,now};
        Check(processor.Render(silence.data(),960,frame,clock)==DuplexStatus::Accepted,"reference admitted");++references;
    }
    DuplexStatus Process(){out.fill(999);result.epoch=999;return processor.Process(out.data(),160,result);}
    void Ack(){Check(processor.AcknowledgeDelivery(result)==DuplexStatus::Accepted,"sink acknowledgment");}
    void Step(int frame,bool acknowledge=true,bool tone=false){
        Tick(frame);Ensure(static_cast<uint64_t>(std::ceil(frame*480*capture_rate/render_rate))+960,-1,tone);
        Reference();Check(Process()==DuplexStatus::Frame,"processed real context");if(acknowledge)Ack();
    }
    DuplexFlush Begin(){DuplexFlush ticket;Check(processor.BeginFlush(ticket)==FlushStatus::Pending,"nonempty pending");return ticket;}
    void Empty(){Check(result.epoch==0&&std::all_of(out.begin(),out.end(),[](int16_t x){return x==0;}),"failure clears output");}
};
void DelayContract(){
    int samples=999;
    Check(maestro_echo_processing_delay(nullptr,&samples)==MAESTRO_ECHO_INVALID&&samples==0,"null delay owner");
    MaestroEcho* owner=nullptr;
    Check(maestro_echo_create(48000,2,48000,1,&owner)==0,"delay owner");
    Check(maestro_echo_processing_delay(owner,nullptr)==MAESTRO_ECHO_INVALID,"null delay result");
    Check(maestro_echo_processing_delay(owner,&samples)==0&&samples==160,"pinned processing bound");
    std::array<float,480> mic{};std::array<int16_t,160> out{};
    Check(maestro_echo_capture(owner,1,0,mic.data(),480,80,out.data(),160)==MAESTRO_ECHO_RESET_REQUIRED,"force fault");
    samples=999;Check(maestro_echo_processing_delay(owner,&samples)==MAESTRO_ECHO_RESET_REQUIRED&&samples==0,"faulted delay not usable");
    Check(maestro_echo_reset(owner,2)==0&&maestro_echo_processing_delay(owner,&samples)==0&&samples==160,"reset delay");
    maestro_echo_destroy(&owner);Done("backend-processing-delay-contract");
}
void DelayPhases(){
    int combinations=0;
    for(int rate:{16000,32000,48000})for(int phase:{0,1,31,63,79,127,159}){
        MaestroEcho* owner=nullptr;Check(maestro_echo_create(48000,2,rate,1,&owner)==0,"impulse owner");
        int delay=0;Check(maestro_echo_processing_delay(owner,&delay)==0,"impulse delay");
        const int n=rate/100,last_input=10*n+phase*(rate/16000);
        std::array<float,960> render{};std::array<float,480> capture{};std::array<int16_t,160> output{};
        int peak=0,peak_at=0;
        for(int frame=0;frame<16;++frame){capture.fill(0);if(last_input>=frame*n&&last_input<(frame+1)*n)capture[last_input-frame*n]=.5f;
            Check(maestro_echo_render(owner,1,frame,render.data(),960)==0,"impulse reference");
            Check(maestro_echo_capture(owner,1,frame,capture.data(),n,80,output.data(),160)==0,"impulse process");
            for(int i=0;i<160;++i)if(std::abs(static_cast<int>(output[i]))>peak){peak=std::abs(static_cast<int>(output[i]));peak_at=frame*160+i;}
        }
        Check(peak>100&&peak_at>=last_input*16000/rate&&peak_at<=last_input*16000/rate+delay,"impulse peak within reported bound");
        maestro_echo_destroy(&owner);++combinations;
    }
    Check(combinations==21,"rate/phase matrix");Done("backend-delay-rate-and-block-phases");
}
void FenceCases(){
    {Fixture f;DuplexFlush ticket;Check(f.processor.PollFlush(ticket)==FlushStatus::Invalid,"unissued invalid");
     Check(f.processor.BeginFlush(ticket)==FlushStatus::Ready&&ticket.input_samples==0,"empty prefix complete");
     f.now+=2000000000;Check(f.processor.PollFlush(ticket)==FlushStatus::Ready,"completed fence remains complete");}
    Done("empty-flush-and-stable-completion");
    {Fixture f;f.Feed(37);const auto first=f.Begin();f.Feed(181);const auto second=f.Begin();
     Check(first.owner==second.owner&&first.request==second.request&&second.input_samples==37,"pending request snapshot coalesces");}
    Done("coalesced-input-snapshot");
    {Fixture f;f.Feed(480);const auto ticket=f.Begin();f.Reference();Check(f.Process()==DuplexStatus::Waiting,"needs lookahead");f.Empty();
     f.Feed(1);Check(f.Process()==DuplexStatus::Waiting,"one real sample insufficient");f.Empty();
     Check(f.processor.PollFlush(ticket)==FlushStatus::Pending,"starvation cannot complete");
     f.Feed(31);Check(f.Process()==DuplexStatus::Frame,"real lookahead produces output");f.Ack();
     Check(f.processor.PollFlush(ticket)==FlushStatus::Pending,"cursor crossing alone cannot complete");}
    Done("no-invented-input-on-flush");
    {Fixture f;f.Feed(480);const auto ticket=f.Begin();f.Feed(1440);
     for(int frame=0;frame<3;++frame){f.Reference();Check(f.Process()==DuplexStatus::Frame,"drain block");
         Check(f.processor.PollFlush(ticket)==FlushStatus::Pending,"produced not delivered");
         f.Ack();Check(f.processor.PollFlush(ticket)==(frame==2?FlushStatus::Ready:FlushStatus::Pending),"alignment and backend delay accounted");}
     Check(ticket.input_samples==480,"later capture did not extend fence");}
    Done("backend-delay-and-delivery-fence");
    {Fixture a,b;a.Step(0,false);b.Step(0,false);const auto ticket=a.Begin();
     Check(b.processor.PollFlush(ticket)==FlushStatus::Invalid,"same epoch foreign ticket");
     Check(a.processor.AcknowledgeDelivery(b.result)==DuplexStatus::Invalid,"same epoch foreign receipt");a.Ack();b.Ack();}
    Done("owner-bound-flush-and-delivery");
    {Fixture f;f.Step(0,false);auto first=f.result;f.Step(1,false);auto second=f.result;
     Check(f.processor.AcknowledgeDelivery(second)==DuplexStatus::Invalid,"out of order receipt");
     Check(f.processor.AcknowledgeDelivery(first)==DuplexStatus::Accepted,"first receipt");
     Check(f.processor.AcknowledgeDelivery(first)==DuplexStatus::Invalid,"duplicate receipt");
     ++second.epoch;Check(f.processor.AcknowledgeDelivery(second)==DuplexStatus::Invalid,"wrong epoch receipt");--second.epoch;
     ++second.sequence;Check(f.processor.AcknowledgeDelivery(second)==DuplexStatus::Invalid,"unproduced receipt");--second.sequence;
     Check(f.processor.AcknowledgeDelivery(second)==DuplexStatus::Accepted,"second receipt preserved");}
    Done("ordered-nonreplayed-delivery");
    {Fixture f;f.Step(0,false);const auto receipt=f.result;const auto ticket=f.Begin();f.processor.Close();f.processor.Close();
     Check(f.processor.PollFlush(ticket)==FlushStatus::Closed&&f.processor.AcknowledgeDelivery(receipt)==DuplexStatus::Closed,"close fences receipts");
     DuplexFlush next;Check(f.processor.BeginFlush(next)==FlushStatus::Closed&&next.owner==0,"close clears ticket result");
     Check(f.Process()==DuplexStatus::Closed,"close fences PCM");f.Empty();}
    Done("close-cancels-pending-delivery");
    {Fixture f;f.Step(0,false);const auto ticket=f.Begin();f.mic[0]=std::numeric_limits<float>::quiet_NaN();
     CaptureFrame frame{7,f.chunks,static_cast<int64_t>(f.input),f.Stamp(true),f.now,f.now,1};
     Check(f.processor.Capture(f.mic.data(),1,frame)==DuplexStatus::Faulted,"capture fault");
     Check(f.processor.PollFlush(ticket)==FlushStatus::Faulted&&f.processor.AcknowledgeDelivery(f.result)==DuplexStatus::Faulted,"fault cannot drain");}
    Done("fault-cancels-pending-flush");
    {Fixture f;f.Feed(37);const auto ticket=f.Begin();f.now+=999999999;Check(f.processor.PollFlush(ticket)==FlushStatus::Pending,"before timeout");
     ++f.now;Check(f.processor.PollFlush(ticket)==FlushStatus::Faulted&&f.processor.Fault()==DuplexFault::FlushTimeout,"deadline exact");}
    Done("flush-deadline-on-poll");
    {Fixture f;f.Feed(37);f.Begin();f.now+=1000000000;DuplexFlush next;
     Check(f.processor.BeginFlush(next)==FlushStatus::Faulted&&next.owner==0,"coalescing cannot reset deadline");}
    Done("flush-deadline-on-coalesce");
    {Fixture f;f.Step(0,false);f.Begin();f.now+=1000000000;
     Check(f.processor.AcknowledgeDelivery(f.result)==DuplexStatus::Faulted&&f.processor.Fault()==DuplexFault::FlushTimeout,"late delivery cannot revive fence");}
    Done("flush-deadline-on-delivery");
    {Fixture f;f.Feed(37);f.Begin();f.now+=1000000000;
     Check(f.Process()==DuplexStatus::Faulted&&f.processor.Fault()==DuplexFault::FlushTimeout,"worker pump enforces deadline");f.Empty();}
    Done("flush-deadline-on-pump");
    {Fixture f;f.Feed(37);const auto ticket=f.Begin();--f.now;
     Check(f.processor.PollFlush(ticket)==FlushStatus::Faulted&&f.processor.Fault()==DuplexFault::Clock,"clock rollback faults pending fence");}
    Done("flush-clock-rollback");
    {Fixture f;f.clock_throws=true;DuplexFlush ticket;
     Check(f.processor.BeginFlush(ticket)==FlushStatus::Faulted&&ticket.owner==0,"clock exception contained");}
    Done("flush-clock-exception");
    {Fixture f;DuplexFlush old,next;Check(f.processor.BeginFlush(old)==FlushStatus::Ready,"empty complete");f.Feed(37);next=f.Begin();
     Check(f.processor.PollFlush(old)==FlushStatus::Invalid,"superseded complete ticket");
     auto altered=next;++altered.input_samples;Check(f.processor.PollFlush(altered)==FlushStatus::Invalid,"altered prefix");
     altered=next;++altered.request;Check(f.processor.PollFlush(altered)==FlushStatus::Invalid,"unissued future request");
     Check(f.processor.PollFlush(next)==FlushStatus::Pending,"valid fence unchanged");}
    Done("superseded-and-altered-tickets");
    {Fixture f;for(int i=0;i<50;++i)f.Step(i,false);f.Tick(50);f.Ensure(52*480);f.Reference();
     Check(f.Process()==DuplexStatus::Faulted&&f.processor.Fault()==DuplexFault::Overflow,"bounded outstanding delivery");f.Empty();}
    Done("bounded-undelivered-output");
}
void ResumeParity(){
    Fixture a,b;DuplexFlush ticket{};int completed=0;
    for(int frame=0;frame<120;++frame){
        a.Tick(frame);b.Tick(frame);a.Ensure((frame+2)*480,-1,true);b.Ensure((frame+2)*480,-1,true);
        if(frame%17==0)ticket=a.Begin();
        a.Reference();b.Reference();Check(a.Process()==DuplexStatus::Frame&&b.Process()==DuplexStatus::Frame,"parity frame");
        Check(a.out==b.out&&a.result.sequence==b.result.sequence&&a.result.capture_first.position==b.result.capture_first.position,"flush retains exact PCM and sequence");
        a.Ack();b.Ack();if(ticket.request&&a.processor.PollFlush(ticket)==FlushStatus::Ready){++completed;ticket={};}
    }
    Check(completed==7,"repeated flush resumed over ring wrap");Done("flush-resume-without-dsp-reset");
}
void ImpulseFences(){
    int combinations=0;
    for(double ppm:{0.0,900.0,-900.0})for(int phase:{1,2,31,32,33,127,128,129,159,160,161,319,320,321,479,480}){
        Fixture f;f.capture_rate=48000*(1+ppm/1e6);f.render_rate=48000*(1-ppm/1e6);
        for(int frame=0;frame<70;++frame)f.Step(frame);
        f.Tick(70);const int64_t last=static_cast<int64_t>(f.input)+phase-1;
        f.Feed(phase,last);const auto ticket=f.Begin();int peak=0,peak_frame=-1,ready_frame=-1;
        for(int frame=70;frame<82;++frame){
            f.Tick(frame);f.Ensure(static_cast<uint64_t>(std::ceil(frame*480*f.capture_rate/f.render_rate))+1440);
            f.Reference();Check(f.Process()==DuplexStatus::Frame,"impulse context processed");
            for(auto sample:f.out)if(std::abs(static_cast<int>(sample))>peak){peak=std::abs(static_cast<int>(sample));peak_frame=frame;}
            const auto before=f.processor.PollFlush(ticket);f.Ack();const auto after=f.processor.PollFlush(ticket);
            if(ready_frame<0&&after==FlushStatus::Ready){Check(before==FlushStatus::Pending,"completion requires last sink receipt");ready_frame=frame;}
        }
        Check(peak>100&&ready_frame>=70&&peak_frame<=ready_frame,"last accepted impulse reached sink before ready");++combinations;
    }
    Check(combinations==48,"fragment and clock-skew matrix");Done("last-fragment-impulse-fence");
}
}
int main(){try{
    DelayContract();DelayPhases();FenceCases();ResumeParity();ImpulseFences();
    Check(cases==20,"complete drain contracts");
    std::printf("{\"status\":\"passed\",\"drainCases\":%d,\"assertions\":%d,\"physicalAudio\":false,\"deviceOpened\":false}\n",cases,checks);return 0;
}catch(const std::exception& e){std::fprintf(stderr,"FAILED: %s\n",e.what());return 1;}}
