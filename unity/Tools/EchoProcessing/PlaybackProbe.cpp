// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#include "PlaybackOutput.h"
#include <algorithm>
#include <array>
#include <chrono>
#include <cstdio>
#include <limits>
#include <stdexcept>
#include <thread>
#include <vector>
using namespace maestro;
namespace {
int cases=0,checks=0;
void Check(bool value,const char* text){++checks;if(!value)throw std::runtime_error(text);}
void Done(const char* name){++cases;std::printf("{\"case\":\"%s\",\"status\":\"passed\"}\n",name);}
struct State{
    PlaybackCallback callback=nullptr;void* user=nullptr;
    int starts=0,closes=0,destroys=0,ready=1,timestamp=1,xruns=0;
    int64_t now=1000000000;DeviceAudioStamp stamp{0,990000000};
    bool start_ok=true,closed=false;
    std::atomic<int64_t> position{0},reference_read{0};
    std::atomic<bool> stop{false},failed{false},entered{false},release{false};
    std::thread thread;
    ~State(){stop=true;release=true;if(thread.joinable())thread.join();}
};
class Device final:public PlaybackDevice{
    std::shared_ptr<State>s;
public:
    explicit Device(std::shared_ptr<State> value):s(value){}
    ~Device()override{Close();++s->destroys;}
    bool Start()override{++s->starts;return s->start_ok;}
    int Ready()override{return s->ready;}
    int Timestamp(DeviceAudioStamp& stamp)override{if(s->timestamp==1)stamp=s->stamp;return s->timestamp;}
    int Xruns()override{return s->xruns;}
    int64_t Now()override{return s->now;}
    void Close()override{if(s->closed)return;s->closed=true;s->stop=true;s->release=true;if(s->thread.joinable())s->thread.join();++s->closes;}
};
thread_local std::shared_ptr<State> next;
std::unique_ptr<PlaybackDevice> Factory(PlaybackCallback cb,void* user){next->callback=cb;next->user=user;return std::make_unique<Device>(next);}
struct Fixture{
    std::shared_ptr<State>s=std::make_shared<State>();std::unique_ptr<PlaybackOutput> output;
    std::array<float,960> source{},reference{};PlaybackFrame frame{};PlaybackClock clock{};
    Fixture(){next=s;output=std::make_unique<PlaybackOutput>(Factory,17);next.reset();for(int i=0;i<480;++i){source[i*2]=.25f;source[i*2+1]=-.5f;}}
    ~Fixture(){output->Close();}
    void Start(){Check(output->Start(),"start");}
    bool Emit(int frames=480,int64_t position=-1,std::vector<float>* observed=nullptr){
        std::vector<float> data(static_cast<size_t>(frames)*2,.9f);const auto first=position<0?s->position.load():position;
        const bool ok=s->callback(s->user,data.data(),frames,first);if(ok)s->position+=frames;if(observed)*observed=std::move(data);return ok;
    }
    PlaybackStatus Read(){reference.fill(.9f);frame.epoch=99;return output->ReadReference(reference.data(),960,frame);}
    void Empty(){Check(std::all_of(reference.begin(),reference.end(),[](float x){return x==0;}),"cleared reference");Check(frame.epoch==0,"cleared reference metadata");}
    void Fault(PlaybackFault reason){Check(output->ObserveClock(clock)==PlaybackStatus::Faulted,"clock faults");Check(output->Fault()==reason,"reason");Check(s->closes==1&&s->destroys==1,"fault retires device");Check(!output->Start(),"fault cannot restart");Check(Read()==PlaybackStatus::Faulted,"fault latched");Empty();}
};
void Pattern(float* samples,int64_t start,int frames){for(int i=0;i<frames;++i){const auto n=start+i;samples[i*2]=static_cast<float>(n%1024+1)/2048; samples[i*2+1]=-static_cast<float>(n/1024+1)/2048;}}
}
auto (*volatile linked_android_output)(PlaybackCallback,void*) -> std::unique_ptr<PlaybackDevice> = OpenAndroidPlayback;
int main(){try{
    {Fixture f;Check(f.s->starts==0,"prepare does not play");f.Start();Check(!f.output->Start(),"single start");f.output->Close();f.output->Close();Check(f.s->closes==1&&f.s->destroys==1,"close once");Check(f.Read()==PlaybackStatus::Closed,"closed terminal");f.Empty();
#ifndef __ANDROID__
    Check(!OpenAndroidPlayback(nullptr,nullptr),"desktop has no real output");
#endif
    }Done("prepared-start-close");
    {Fixture f;const auto before=f.source;Check(f.output->Submit(f.source.data(),480)==PlaybackStatus::Ok,"prime");f.Start();std::vector<float> actual;Check(f.Emit(480,-1,&actual),"callback");Check(std::equal(actual.begin(),actual.end(),before.begin()),"stereo output exact");Check(f.source==before,"producer PCM immutable");Check(f.Read()==PlaybackStatus::Ok&&f.reference==before,"reference is actual output");Check(f.frame.epoch==17&&f.frame.sequence==0&&f.frame.first_position==0,"reference identity");}Done("exact-stereo-output-reference");
    {Fixture f;f.Start();Check(f.output->Submit(f.source.data(),480)==PlaybackStatus::Ok,"submit");Check(f.Emit(128),"small callback");Check(f.Read()==PlaybackStatus::Waiting,"incomplete reference");f.Empty();Check(f.Emit(352),"completing callback");Check(f.Read()==PlaybackStatus::Ok&&f.reference==f.source,"variable callback assembly");}Done("variable-callback-size");
    {Fixture f;std::vector<float> full(PlaybackOutput::Capacity*2,.125f);Check(f.output->Submit(full.data(),PlaybackOutput::Capacity)==PlaybackStatus::Ok,"fill queue");Check(f.output->Submit(f.source.data(),480)==PlaybackStatus::Full,"bounded backpressure");f.Start();Check(f.Emit(),"consume");Check(f.output->Submit(f.source.data(),480)==PlaybackStatus::Ok,"slot reused");Check(f.Read()==PlaybackStatus::Ok&&f.reference[0]==.125f,"full rejection never overwrites");}Done("submission-backpressure");
    {Fixture f;f.Start();for(int i=0;i<50;++i)Check(f.Emit(),"fill reference");Check(!f.Emit(),"overflow stops callback");f.Fault(PlaybackFault::ReferenceOverflow);}Done("reference-overflow");
    {Fixture f;f.Start();Check(f.output->Submit(f.source.data(),100)==PlaybackStatus::Ok,"short source");std::vector<float> out;Check(f.Emit(480,-1,&out),"underfilled callback");Check(std::equal(out.begin(),out.begin()+200,f.source.begin()),"queued prefix");Check(std::all_of(out.begin()+200,out.end(),[](float x){return x==0;}),"known silence suffix");Check(f.Read()==PlaybackStatus::Ok&&std::equal(out.begin(),out.end(),f.reference.begin()),"silence retained in reference");Check(f.output->UnderfilledFrames()==380,"underfill count");Check(f.output->Submit(f.source.data(),480)==PlaybackStatus::Ok&&f.Emit(),"later source");Check(f.Read()==PlaybackStatus::Ok&&f.reference==f.source,"no stale replay");}Done("known-silence-on-underfill");
    {Fixture f;f.Start();Check(!f.Emit(480,1),"device position gap");f.Fault(PlaybackFault::Gap);}Done("callback-position-gap");
    {for(float bad:{std::numeric_limits<float>::quiet_NaN(),std::numeric_limits<float>::infinity(),1.01f}){Fixture f;f.source[19]=bad;Check(f.output->Submit(f.source.data(),480)==PlaybackStatus::Invalid,"reject invalid sample atomically");f.Start();Check(f.Emit()&&f.Read()==PlaybackStatus::Ok,"empty output still measured");Check(std::all_of(f.reference.begin(),f.reference.end(),[](float x){return x==0;}),"invalid source never audible");}}Done("invalid-producer-samples");
    {Fixture f;Check(f.output->Submit(nullptr,1)==PlaybackStatus::Invalid,"null submit");Check(f.output->Submit(f.source.data(),0)==PlaybackStatus::Invalid,"empty submit");std::array<float,959> small;small.fill(.4f);Check(f.output->ReadReference(small.data(),959,f.frame)==PlaybackStatus::Invalid,"short reference buffer");Check(small.front()==.4f&&small.back()==.4f,"wrong buffer untouched");f.Start();Check(!f.s->callback(f.s->user,nullptr,480,0),"null callback");f.Fault(PlaybackFault::Gap);}Done("buffer-validation");
    {Fixture f;f.Start();f.s->ready=0;Check(f.output->ObserveClock(f.clock)==PlaybackStatus::Waiting,"start asynchronous");f.s->ready=1;f.s->timestamp=0;Check(f.output->ObserveClock(f.clock)==PlaybackStatus::Waiting,"timestamp not ready");f.s->timestamp=1;Check(f.output->ObserveClock(f.clock)==PlaybackStatus::Ok,"timestamp available");Check(f.clock.hardware.time_ns!=f.clock.query_finished_ns,"hardware distinct from arrival");}Done("asynchronous-playback-timestamp");
    {Fixture f;f.Start();f.s->now+=1000000001;f.Fault(PlaybackFault::Timeout);}Done("timestamp-startup-deadline");
    {Fixture f;f.s->start_ok=false;Check(!f.output->Start(),"failed start");Check(f.s->closes==1&&f.s->destroys==1,"failed start release");}Done("failed-start-release");
    {Fixture f;f.Start();f.s->timestamp=-1;f.Fault(PlaybackFault::Device);}Done("missing-timestamp-support");
    {Fixture f;f.Start();f.s->ready=-1;f.Fault(PlaybackFault::Device);}Done("device-disconnect");
    {Fixture f;f.Start();++f.s->xruns;f.Fault(PlaybackFault::Gap);}Done("device-xrun");
    {for(int kind=0;kind<3;++kind){Fixture f;f.Start();Check(f.output->ObserveClock(f.clock)==PlaybackStatus::Ok,"initial timestamp");if(kind==0)--f.s->now;if(kind==1)--f.s->stamp.time_ns;if(kind==2)++f.s->stamp.time_ns;f.Fault(PlaybackFault::Clock);}}Done("clock-rollback-and-inconsistency");
    {Fixture f;f.Start();f.s->stamp.position=480;f.Fault(PlaybackFault::Clock);}Done("unrendered-hardware-position");
    {Fixture f;f.Start();Check(f.output->Submit(f.source.data(),480)==PlaybackStatus::Ok,"submit");Check(f.output->ObserveClock(f.clock)==PlaybackStatus::Ok&&!f.output->Drained(f.clock),"queued is not played");Check(f.Emit(),"consume source");Check(f.output->ObserveClock(f.clock)==PlaybackStatus::Ok&&!f.output->Drained(f.clock),"callback is not physical completion");f.s->stamp={480,1000000000};Check(f.output->ObserveClock(f.clock)==PlaybackStatus::Ok&&f.output->Drained(f.clock),"hardware presentation completes source");}Done("hardware-gated-drain");
    {Fixture f,g;f.Start();g.Start();Check(f.output->ObserveClock(f.clock)==PlaybackStatus::Ok,"own clock");Check(g.output->ObserveClock(g.clock)==PlaybackStatus::Ok,"other clock");Check(!f.output->Drained(g.clock),"reject foreign owner");auto prior=f.clock;Check(f.output->ObserveClock(f.clock)==PlaybackStatus::Ok&&!f.output->Drained(prior),"reject old observation");f.s->timestamp=0;Check(f.output->ObserveClock(f.clock)==PlaybackStatus::Waiting&&!f.output->Drained(prior),"waiting invalidates receipts");g.s->now+=500000001;Check(!g.output->Drained(g.clock),"stale completion refused");}Done("completion-receipt-ownership");
    {Fixture f;f.Start();for(int i=0;i<200;++i){Pattern(f.source.data(),i*480,480);Check(f.output->Submit(f.source.data(),480)==PlaybackStatus::Ok,"wrap submit");Check(f.Emit(100)&&f.Emit(380),"wrap callbacks");Check(f.Read()==PlaybackStatus::Ok&&f.reference==f.source,"wrapped PCM exact");Check(f.frame.first_position==i*480&&f.frame.sequence==static_cast<uint64_t>(i),"wrapped positions");}}Done("ring-wrap-continuity");
    {Fixture f;f.Start();auto s=f.s;s->thread=std::thread([s]{std::array<float,960> data;data.fill(.9f);s->entered=true;while(!s->release.load())std::this_thread::yield();const bool ok=s->callback(s->user,data.data(),480,0);s->failed=ok||!std::all_of(data.begin(),data.end(),[](float x){return x==0;});});while(!s->entered.load())std::this_thread::yield();f.output->Close();Check(!s->failed&&s->closes==1,"close fences in-flight callback");Check(f.Read()==PlaybackStatus::Closed,"closed reference");f.Empty();}Done("close-during-callback");
    {Fixture f;f.Start();auto s=f.s;s->thread=std::thread([s]{std::array<float,1024> data{};int cycle=0;const int chunks[]={128,256,96};while(!s->stop.load()){if(s->position.load()-s->reference_read.load()>PlaybackOutput::Capacity-2048){std::this_thread::yield();continue;}const int frames=chunks[cycle++%3];const auto at=s->position.load();if(!s->callback(s->user,data.data(),frames,at)){if(!s->stop.load())s->failed=true;break;}s->position+=frames;}});
        constexpr int total=120000;int produced=0,observed=0;const auto deadline=std::chrono::steady_clock::now()+std::chrono::seconds(10);
        while(observed<total&&std::chrono::steady_clock::now()<deadline&&!s->failed.load()){
            if(produced<total){Pattern(f.source.data(),produced,480);const auto status=f.output->Submit(f.source.data(),480);Check(status==PlaybackStatus::Ok||status==PlaybackStatus::Full,"concurrent admission");if(status==PlaybackStatus::Ok)produced+=480;}
            const auto read=f.Read();Check(read==PlaybackStatus::Ok||read==PlaybackStatus::Waiting,"concurrent reference");
            if(read==PlaybackStatus::Ok){for(int i=0;i<480;++i){if(f.reference[i*2]==0){Check(f.reference[i*2+1]==0,"known stereo silence");continue;}std::array<float,2> expected;Pattern(expected.data(),observed,1);Check(f.reference[i*2]==expected[0]&&f.reference[i*2+1]==expected[1],"concurrent sample identity");++observed;}s->reference_read+=480;}else std::this_thread::yield();
        }
        s->stop=true;f.output->Close();
        std::printf("{\"concurrentProduced\":%d,\"concurrentObserved\":%d,\"driverFailed\":%s,\"outputFault\":%d}\n",produced,observed,s->failed?"true":"false",static_cast<int>(f.output->Fault()));
        Check(!s->failed&&produced==total&&observed==total,"concurrent stream completed without loss or replay");Check(s->closes==1&&s->destroys==1,"concurrent close released");
    }Done("single-producer-consumer-stress");
    Check(cases==22,"complete playback case set");std::printf("{\"status\":\"passed\",\"playbackCases\":%d,\"assertions\":%d,\"concurrentSourceFrames\":120000,\"deviceOpened\":false,\"physicalAudio\":false}\n",cases,checks);return 0;
}catch(const std::exception& error){std::fprintf(stderr,"FAILED: %s\n",error.what());return 1;}}
