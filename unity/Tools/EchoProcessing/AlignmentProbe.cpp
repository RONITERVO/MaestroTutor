// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#include "DuplexProcessor.h"
#include "AudioFixture.h"
#include <algorithm>
#include <array>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <limits>
#include <stdexcept>
#include <vector>
using namespace maestro;
namespace {
constexpr double pi=3.141592653589793;
int cases=0,checks=0;
void Check(bool v,const char* text){++checks;if(!v)throw std::runtime_error(text);}
void Done(const char* name){++cases;std::printf("{\"case\":\"%s\",\"status\":\"passed\"}\n",name);}
struct Time {
    int64_t now=1100000000,step=0;bool throws=false;
    static int64_t Get(void* p){auto& t=*static_cast<Time*>(p);if(t.throws)throw std::runtime_error("Clock unavailable");const auto result=t.now;t.now+=t.step;return result;}
};
struct Fixture {
    Time time;DuplexProcessor processor{7,Time::Get,&time};
    std::array<float,480> cap{};std::array<float,960> render{};std::array<int16_t,160> out{};DuplexFrame result;
    CaptureFrame C(uint64_t seq=0){return {7,seq,static_cast<int64_t>(seq)*480,{4800,1100000000},time.now,time.now};}
    PlaybackFrame R(uint64_t seq=0){return {7,seq,static_cast<int64_t>(seq)*480};}
    PlaybackClock K(uint64_t seq=1){return {this,7,seq,{960,1100000000},time.now,time.now};}
    void Cap(uint64_t seq=0){Check(processor.Capture(cap.data(),480,C(seq))==DuplexStatus::Accepted,"capture admitted");}
    void Ref(uint64_t seq=0){Check(processor.Render(render.data(),960,R(seq),K(seq+1))==DuplexStatus::Accepted,"render admitted");}
    DuplexStatus Process(){out.fill(999);result.epoch=99;return processor.Process(out.data(),160,result);}
    void Empty(){Check(std::all_of(out.begin(),out.end(),[](int16_t x){return x==0;}),"no raw or stale output");Check(result.epoch==0,"metadata empty");}
};
void ClockCases(){
    {HardwareClock c;Check(c.Observe({4800,1100000000},1100000000,1100000000),"observe");SampleTime t;Check(c.Project({0,.5},1100000000,t),"fractional projection");Check(std::abs(t.nanoseconds-(1000000000+.5*HardwareClock::NominalPeriod))<1,"project sample acquisition time");}Done("nominal-frame-time");
    {for(double ppm:{-1500.0,1500.0}){HardwareClock c;const double period=1e9/(48000*(1+ppm/1e6));for(int i=0;i<90;++i){const int64_t ns=1000000000+static_cast<int64_t>(std::llround(i*480*period))+((i%3)-1)*10000;Check(c.Observe({i*480,ns},ns+100000,ns+100000),"bounded jitter/skew");}Check(c.Fitted()&&std::abs(c.Period()/period-1)<.00005,"rate fitted");}}Done("clock-fit-skew-jitter");
    {HardwareClock c;Check(c.Observe({0,1000000000},1000000000,1000000000)&&!c.Fitted(),"nominal startup");SampleTime t;Check(c.Project({4800,0},1000000000,t)&&t.error_ns>=400000,"startup projection uncertainty");}Done("startup-rate-uncertainty");
    {HardwareClock c;for(int i=0;i<80;++i)Check(c.Observe({0,1000000000},1000000000,1000000000),"repeat stamp");Check(!c.Fitted(),"duplicates never fit drift");}Done("duplicate-clock-observation");
    {HardwareClock c;for(int i=0;i<900;++i){const int64_t ns=1000000000+static_cast<int64_t>(std::llround(i*48*1e9/48038.4));Check(c.Observe({i*48,ns},ns,ns),"rapid observations admitted");}Check(c.Fitted()&&std::abs(c.Period()/(1e9/48038.4)-1)<.000001,"rapid observations retain time coverage");}Done("clock-cadence-independent-fit");
    {HardwareClock c;Check(c.Observe({480,1010000000},1010000000,1010000000),"clock seed");Check(!c.Observe({479,1009999999},1010000001,1010000001)&&c.Faulted(),"rollback faults");Check(!c.Observe({960,1020000000},1020000000,1020000000),"fault terminal");}Done("clock-rollback-terminal");
    {HardwareClock c;bool accepted=true;for(int i=0;i<90&&accepted;++i){const int64_t ns=1000000000+i*10030000;accepted=c.Observe({i*480,ns},ns,ns);}Check(!accepted&&c.Faulted(),"outside admitted drift");}Done("clock-rate-limit");
    {HardwareClock c;for(int i=0;i<64;++i)Check(c.Observe({i*480,1000000000+i*10000000LL},1000000000+i*10000000LL,1000000000+i*10000000LL),"stable fit");Check(!c.Observe({64*480,1641000000},1641000000,1641000000),"timestamp spike fails");}Done("clock-discontinuity");
    {HardwareClock c;Check(c.Observe({0,1000000000},1000000000,1000000000),"seed");SampleTime t;Check(!c.Project({0,0},1250000001,t)&&t.nanoseconds==0,"stale denied");Check(!c.Project({24001,0},1000000000,t),"projection bounded");Check(!c.Project({0,.5},999999999,t),"time cannot regress");}Done("bounded-clock-projection");
    {HardwareClock c;constexpr int64_t origin=4000000000000000LL,pos=90000000000LL;for(int i=0;i<64;++i)Check(c.Observe({pos+i*480,origin+i*10000000LL},origin+i*10000000LL,origin+i*10000000LL),"large uptime");SampleTime t;Check(c.Project({pos+63*480,.5},origin+630000000,t)&&std::abs(t.nanoseconds-(origin+630000000+.5*HardwareClock::NominalPeriod))<1,"large-position precision");}Done("long-uptime-precision");
    {HardwareClock r,c;Check(r.Observe({0,1000000000},1020000000,1020000000)&&c.Observe({0,970000000},1020000000,1020000000),"seed clocks");MeasuredDelay d;Check(MeasureDelay(r,c,{0,0},{0,0},1020000000,1021000000,d)&&d.milliseconds==31,"worker time belongs in buffer delay");Check(std::abs(d.nanoseconds-31000000)<1&&d.error_ns>=900000,"delay uncertainty includes rounding");}Done("upstream-delay-formula");
    {HardwareClock r,c;Check(r.Observe({0,1000000000},1000000000,1000000000)&&c.Observe({0,1000000000},1000000000,1000000000),"zero-delay clocks");MeasuredDelay d;Check(!MeasureDelay(r,c,{0,0},{0,0},1000000000,1000000000,d)&&d.milliseconds==0,"below-headroom not clamped");Check(!MeasureDelay(r,c,{4800,0},{0,0},1000000000,1006000000,d),"stalled reverse processing rejected");Check(!MeasureDelay(r,c,{4800,0},{480,0},1000000000,1000000000,d),"future acquired sample rejected");}Done("invalid-delay-is-not-repaired");
    {HardwareClock r,c;Check(r.Observe({4800,1100000000},1100000000,1100000000)&&c.Observe({4800,1020000000},1100000000,1100000000),"same counters different origins");MeasuredDelay d;Check(MeasureDelay(r,c,{0,0},{0,0},1100000000,1100000000,d)&&d.milliseconds==80,"timestamps not counters align streams");}Done("independent-device-frame-origins");
}
void OwnershipCases(){
    {Fixture f;Check(f.Process()==DuplexStatus::Waiting,"empty waits");f.Empty();f.processor.Close();f.processor.Close();Check(f.Process()==DuplexStatus::Closed,"close terminal");f.Empty();}Done("duplex-empty-close");
    {Fixture f;std::array<int16_t,159> small;small.fill(99);Check(f.processor.Process(small.data(),159,f.result)==DuplexStatus::Invalid,"output bounds");Check(small.front()==99&&small.back()==99,"invalid output untouched");}Done("duplex-output-bounds");
    {for(float bad:{std::numeric_limits<float>::infinity(),std::numeric_limits<float>::quiet_NaN(),1.1f}){Fixture f;f.cap[17]=bad;Check(f.processor.Capture(f.cap.data(),480,f.C())==DuplexStatus::Faulted,"invalid PCM faults");Check(f.Process()==DuplexStatus::Faulted,"input terminal");f.Empty();}}Done("duplex-invalid-input");
    {for(int mode=0;mode<3;++mode){Fixture f;f.Cap();auto frame=f.C(mode==0?0:1);if(mode==1)++frame.first_position;if(mode==2)++frame.epoch;Check(f.processor.Capture(f.cap.data(),480,frame)==DuplexStatus::Faulted&&f.processor.Fault()==DuplexFault::Sequence,"capture identity/order");}}Done("duplex-capture-sequence");
    {for(int mode=0;mode<3;++mode){Fixture f;f.Ref();auto k=f.K(mode==0?2:1);if(mode==0)k.owner=&k;if(mode==1)++k.hardware.time_ns;if(mode==2)k.observation=0;Check(f.processor.Render(f.render.data(),960,f.R(1),k)==DuplexStatus::Faulted,"render clock ownership");}}Done("duplex-render-clock-identity");
    {Fixture f;for(int i=0;i<50;++i)f.Cap(i);Check(f.processor.Capture(f.cap.data(),480,f.C(50))==DuplexStatus::Faulted&&f.processor.Fault()==DuplexFault::Overflow,"bounded capture queue");}Done("duplex-capture-overflow");
    {Fixture f;for(int i=0;i<50;++i)f.Ref(i);Check(f.processor.Render(f.render.data(),960,f.R(50),f.K(51))==DuplexStatus::Faulted&&f.processor.Fault()==DuplexFault::Overflow,"bounded render queue");}Done("duplex-render-overflow");
    {Fixture f;f.Cap();f.Ref();Check(f.Process()==DuplexStatus::Waiting,"sinc waits for real lookahead");f.Empty();f.Cap(1);Check(f.Process()==DuplexStatus::Frame,"real lookahead releases frame");Check(f.result.epoch==7&&f.result.sequence==0&&f.result.capture_first.position==0&&f.result.delay.milliseconds==80,"sample identity and delay");}Done("duplex-lookahead-admission");
    {Fixture f;f.Cap();f.Cap(1);f.Ref();f.time.now+=250000001;Check(f.Process()==DuplexStatus::Faulted&&f.processor.Fault()==DuplexFault::Clock,"stalled frame rejected");f.Empty();}Done("duplex-stale-queued-frame");
    {Fixture f;Check(f.Process()==DuplexStatus::Waiting,"initial waits");f.time.now+=1000000001;Check(f.Process()==DuplexStatus::Faulted,"missing stream deadline");f.Empty();}Done("duplex-startup-watchdog");
    {Fixture f;f.cap.fill(.25f);f.render.fill(.1f);const auto cap=f.cap;const auto render=f.render;f.Cap();f.Cap(1);f.Ref();Check(f.Process()==DuplexStatus::Frame,"process");Check(f.cap==cap&&f.render==render,"inputs immutable");Check(f.result.capture_per_render==1&&f.result.clipped_samples==0,"nominal resampling trace");Check(f.Process()==DuplexStatus::Waiting,"no frame replay");f.Empty();}Done("duplex-immutable-no-replay");
    {Fixture f;f.Cap();f.Cap(1);f.Ref();f.time.step=6000000;Check(f.Process()==DuplexStatus::Faulted,"reverse processing deadline");f.Empty();}Done("duplex-processing-stall");
    {Fixture f;f.time.throws=true;Check(f.Process()==DuplexStatus::Faulted&&f.processor.Fault()==DuplexFault::Clock,"clock callback failure is terminal");f.Empty();}Done("duplex-clock-provider-failure");
    {Fixture f;auto c=f.C();++c.epoch;Check(f.processor.Capture(f.cap.data(),480,c)==DuplexStatus::Faulted,"old epoch retires");Fixture g;g.Cap();g.Cap(1);g.Ref();Check(g.Process()==DuplexStatus::Frame,"explicit new owner recovers");}Done("duplex-epoch-replacement");
}
float Wave(double sample,bool right=false){
    if(sample<0)return 0;
    const double t=sample/48000,shift=right?.37:0;
    const double frequencies[]={173,379,631,1061,1777,2599,3701,5113};double sum=0;
    for(int j=0;j<8;++j)sum+=std::sin(2*pi*(frequencies[j]*t+.11*std::sin((j+1.0)*t+shift))+j*.61+shift);
    return static_cast<float>(.055*(.65+.35*std::sin(2*pi*2.7*t+shift))*sum);
}
float Near(double seconds){return static_cast<float>(.16*(.75+.25*std::sin(17*seconds))*(std::sin(2*pi*211*seconds)+.3*std::sin(2*pi*422*seconds)+.15*std::sin(2*pi*844*seconds)));}
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
struct Quality { double reduction=0,correlation=0,gain=0,absolute_gain=0,max_delay_error_ms=0;uint64_t clipped=0; };
Quality AudioCase(double capture_ppm,double render_ppm,int jitter_us,bool tonal){
    Broadband broadband;auto sound=[&](double position,bool right=false){return tonal?Wave(position,right):broadband.At(position,right);};
    constexpr int frames=1800;const double cr=48000*(1+capture_ppm/1e6),rr=48000*(1+render_ppm/1e6);
    Time time;DuplexProcessor echo(31,Time::Get,&time),both(31,Time::Get,&time),near(31,Time::Get,&time);
    std::array<float,480> mic_echo{},mic_both{},mic_near{};std::array<float,960> ref{},silence{};
    std::array<int16_t,160> out{};DuplexFrame result;
    std::vector<double> cancelled,combined,baseline,expected_echo,expected_near;
    uint64_t input=0;Quality q;
    for(int frame=0;frame<frames;++frame){
        const double wall=.1+frame*480/rr;time.now=1000000000+static_cast<int64_t>(std::llround(wall*1e9));
        const auto cp=static_cast<int64_t>(std::floor((wall-.001)*cr));
        const auto rp=static_cast<int64_t>(std::floor((wall-.001-.08)*rr));
        const int64_t jitter=((frame%3)-1)*jitter_us*1000LL;
        const DeviceAudioStamp cs{cp,1000000000+static_cast<int64_t>(std::llround(cp*1e9/cr))+jitter};
        const DeviceAudioStamp rs{rp,1080000000+static_cast<int64_t>(std::llround(rp*1e9/rr))-jitter};
        // Keep actual input ahead of the sinc kernel while preserving every sample.
        const auto wanted=static_cast<uint64_t>(std::ceil(frame*480*cr/rr))+960;
        while(input*480<wanted){
            for(int i=0;i<480;++i){const double t=(input*480+i)/cr;
                const float e=.55f*sound((t-.08)*rr)+.22f*sound((t-.11)*rr,true);
                mic_echo[i]=e;mic_near[i]=Near(t);mic_both[i]=e+mic_near[i];}
            CaptureFrame cf{31,input,static_cast<int64_t>(input*480),cs,time.now,time.now};
            Check(echo.Capture(mic_echo.data(),480,cf)==DuplexStatus::Accepted,"quality echo capture");
            Check(both.Capture(mic_both.data(),480,cf)==DuplexStatus::Accepted,"quality mixed capture");
            Check(near.Capture(mic_near.data(),480,cf)==DuplexStatus::Accepted,"quality baseline capture");++input;
        }
        for(int i=0;i<480;++i){ref[i*2]=sound(frame*480+i);ref[i*2+1]=sound(frame*480+i,true);}
        PlaybackFrame rf{31,static_cast<uint64_t>(frame),frame*480};PlaybackClock rc{&time,31,static_cast<uint64_t>(frame+1),rs,time.now,time.now};
        Check(echo.Render(ref.data(),960,rf,rc)==DuplexStatus::Accepted,"quality echo reference");
        Check(both.Render(ref.data(),960,rf,rc)==DuplexStatus::Accepted,"quality mixed reference");
        Check(near.Render(silence.data(),960,rf,rc)==DuplexStatus::Accepted,"quality baseline reference");
        Check(echo.Process(out.data(),160,result)==DuplexStatus::Frame,"quality aligned echo frame");
        const auto metadata=result;
        for(int i=0;i<160;++i){cancelled.push_back(out[i]/32768.0);const double t=(metadata.capture_first.position+metadata.capture_first.fraction+i*3*metadata.capture_per_render)/cr;
            expected_echo.push_back(.55*sound((t-.08)*rr)+.22*sound((t-.11)*rr,true));expected_near.push_back(Near(t));}
        const double true_delay=(.08+frame*480/rr-(metadata.capture_first.position+metadata.capture_first.fraction)/cr)*1000;
        q.max_delay_error_ms=std::max(q.max_delay_error_ms,std::abs(metadata.delay.milliseconds-true_delay));
        Check(both.Process(out.data(),160,result)==DuplexStatus::Frame,"quality aligned double talk");for(auto x:out)combined.push_back(x/32768.0);q.clipped+=result.clipped_samples;
        Check(near.Process(out.data(),160,result)==DuplexStatus::Frame,"quality aligned near baseline");for(auto x:out)baseline.push_back(x/32768.0);
    }
    const int first=8*16000,last=17*16000;double err=0,original=0,bb=0,raw=0,cross=0,cc=0;
    for(int i=first;i<last;++i){err+=cancelled[i]*cancelled[i];original+=expected_echo[i]*expected_echo[i];bb+=baseline[i]*baseline[i];raw+=expected_near[i]*expected_near[i];cross+=combined[i]*baseline[i];cc+=combined[i]*combined[i];}
    q.reduction=10*std::log10(original/std::max(err,1e-30));q.correlation=cross/std::sqrt(std::max(bb*cc,1e-30));q.gain=cross/std::max(bb,1e-30);q.absolute_gain=std::sqrt(bb/std::max(raw,1e-30));return q;
}
int QualityCases(bool tonal){
    const double settings[][3]={{0,0,0},{900,-900,20},{-900,900,20}};
    int failures=0;
    for(const auto& s:settings){const auto q=AudioCase(s[0],s[1],static_cast<int>(s[2]),tonal);
        const bool pass=q.reduction>=15&&q.correlation>=.65&&q.gain>=.45&&q.gain<=1.5&&q.absolute_gain>=.45&&q.absolute_gain<=1.5&&q.max_delay_error_ms<=2&&q.clipped==0;
        if(!pass)++failures;
        std::printf("{\"capturePpm\":%.0f,\"renderPpm\":%.0f,\"timestampJitterUs\":%.0f,\"echoReductionDb\":%.6f,\"nearCorrelation\":%.6f,\"nearGain\":%.6f,\"baselineGain\":%.6f,\"maxDelayErrorMs\":%.6f,\"clipped\":%llu,\"status\":\"%s\"}\n",s[0],s[1],s[2],q.reduction,q.correlation,q.gain,q.absolute_gain,q.max_delay_error_ms,static_cast<unsigned long long>(q.clipped),pass?"passed":"failed");
    }
    std::printf("{\"status\":\"%s\",\"scope\":\"%s\",\"qualityCases\":3,\"failures\":%d,\"physicalAudio\":false,\"deviceOpened\":false}\n",failures?"failed":"passed",tonal?"clock-aligned-tonal":"clock-aligned-quality",failures);
    return failures?1:0;
}
}
int main(int argc,char** argv){try{
    if(argc==2&&std::strcmp(argv[1],"--quality")==0){return QualityCases(false);}
    if(argc==2&&std::strcmp(argv[1],"--tonal")==0){return QualityCases(true);}
    Check(argc==1,"known command");ClockCases();OwnershipCases();Check(cases==27,"complete clock/duplex contracts");
    std::printf("{\"status\":\"passed\",\"alignmentCases\":%d,\"assertions\":%d,\"physicalAudio\":false,\"deviceOpened\":false}\n",cases,checks);return 0;
}catch(const std::exception& e){std::fprintf(stderr,"FAILED: %s\n",e.what());return 1;}}
