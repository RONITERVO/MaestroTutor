// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#include "DuplexProcessor.h"
#include "AlignedAudioFixture.h"
#include <algorithm>
#include <array>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <limits>
#include <stdexcept>
#include <vector>
using namespace maestro;
using namespace maestro_aligned_fixture;
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
    CaptureFrame C(uint64_t seq=0){return {7,seq,static_cast<int64_t>(seq)*480,{4800,1100000000},time.now,time.now,480};}
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
    {Fixture f;auto c=f.C();c.samples=37;Check(f.processor.Capture(f.cap.data(),37,c)==DuplexStatus::Accepted,"short capture fragment");
     c.sequence=1;c.first_position=37;c.samples=443;Check(f.processor.Capture(f.cap.data(),443,c)==DuplexStatus::Accepted,"fragment completes block");
     c.sequence=2;c.first_position=480;c.samples=480;Check(f.processor.Capture(f.cap.data(),480,c)==DuplexStatus::Accepted,"real lookahead after fragment");f.Ref();
     Check(f.Process()==DuplexStatus::Frame&&f.result.capture_first.position==0&&f.result.sequence==0,"fragment boundary does not shift output");}Done("duplex-capture-fragments");
    {for(int count:{0,481,37}){Fixture f;auto c=f.C();Check(f.processor.Capture(f.cap.data(),count,c)==DuplexStatus::Faulted&&f.processor.Fault()==DuplexFault::Input,"fragment count contract");Check(f.Process()==DuplexStatus::Faulted,"bad fragment cannot produce output");f.Empty();}}Done("duplex-fragment-count-validation");
    {Fixture f;auto c=f.C();c.samples=37;Check(f.processor.Capture(f.cap.data(),37,c)==DuplexStatus::Accepted,"fragment before gap");c.sequence=1;c.first_position=38;
     Check(f.processor.Capture(f.cap.data(),37,c)==DuplexStatus::Faulted&&f.processor.Fault()==DuplexFault::Sequence,"partial sample gap rejected");}Done("duplex-fragment-position-gap");
    {Fixture f;auto c=f.C();++c.epoch;Check(f.processor.Capture(f.cap.data(),480,c)==DuplexStatus::Faulted,"old epoch retires");Fixture g;g.Cap();g.Cap(1);g.Ref();Check(g.Process()==DuplexStatus::Frame,"explicit new owner recovers");}Done("duplex-epoch-replacement");
}
void FragmentParity(){
    Time time;DuplexProcessor whole(31,Time::Get,&time),fragmented(31,Time::Get,&time);
    std::array<float,480> mic{};std::array<float,960> silence{};std::array<int16_t,160> a{},b{};
    uint64_t input=0,fragment_sequence=0;DuplexFrame af,bf;
    for(int frame=0;frame<60;++frame){
        time.now=1100000000+frame*10000000LL;
        const DeviceAudioStamp cs{frame*480+4752,time.now-1000000},rs{frame*480+912,time.now-1000000};
        while(input<static_cast<uint64_t>(frame+2)){
            for(int i=0;i<480;++i)mic[i]=static_cast<float>(.2*std::sin((input*480+i)*.07));
            CaptureFrame cf{31,input,static_cast<int64_t>(input*480),cs,time.now,time.now,480};
            Check(whole.Capture(mic.data(),480,cf)==DuplexStatus::Accepted,"whole input");
            int offset=0;for(int n:{37,181,262}){cf.sequence=fragment_sequence++;cf.first_position=input*480+offset;cf.samples=n;
                Check(fragmented.Capture(mic.data()+offset,n,cf)==DuplexStatus::Accepted,"fragment input");offset+=n;}
            ++input;
        }
        PlaybackFrame rf{31,static_cast<uint64_t>(frame),frame*480};PlaybackClock rc{&time,31,static_cast<uint64_t>(frame+1),rs,time.now,time.now};
        Check(whole.Render(silence.data(),960,rf,rc)==DuplexStatus::Accepted&&fragmented.Render(silence.data(),960,rf,rc)==DuplexStatus::Accepted,"shared render timeline");
        Check(whole.Process(a.data(),160,af)==DuplexStatus::Frame&&fragmented.Process(b.data(),160,bf)==DuplexStatus::Frame,"paired output");
        Check(whole.AcknowledgeDelivery(af)==DuplexStatus::Accepted&&fragmented.AcknowledgeDelivery(bf)==DuplexStatus::Accepted,"paired sink admission");
        Check(a==b&&af.capture_first.position==bf.capture_first.position&&af.capture_first.fraction==bf.capture_first.fraction&&af.sequence==bf.sequence,"fragment boundaries preserve PCM and sample identity");
    }
    Done("duplex-fragment-output-parity");
}
struct Quality { double reduction=0,correlation=0,gain=0,absolute_gain=0,max_delay_error_ms=0;
    double onset_gain=1,onset_correlation=1,onset_baseline_gain=1;
    double onset_maximum_gain=0,onset_maximum_baseline_gain=0;int onset_windows=0;uint64_t clipped=0; };
struct OnsetScore { double gain,correlation,baseline_gain; };
OnsetScore ScoreOnset(const std::vector<double>& combined,const std::vector<double>& baseline,
                     const std::vector<double>& expected,size_t first,size_t last){
    if(combined.size()!=baseline.size()||combined.size()!=expected.size()||first>=last||last>combined.size())
        throw std::invalid_argument("Onset metric requires matching arrays and a complete nonempty window");
    double cross=0,clean=0,mixed=0,raw=0;
    for(size_t i=first;i<last;++i){
        const double x=combined[i],y=baseline[i],source=expected[i];
        if(!std::isfinite(x)||!std::isfinite(y)||!std::isfinite(source)||std::abs(x)>1||std::abs(y)>1||std::abs(source)>1)
            throw std::invalid_argument("Onset metric requires finite normalized samples");
        cross+=x*y;clean+=y*y;mixed+=x*x;raw+=source*source;
    }
    if(raw<=0)throw std::invalid_argument("Onset metric requires an audible expected source");
    // A muted baseline must fail quality even when the mixed output is also
    // muted. It is not an invalid source fixture and must retain a result row.
    return {clean>0?cross/clean:0,cross/std::sqrt(std::max(clean*mixed,1e-30)),std::sqrt(clean/raw)};
}
void RecordOnset(Quality& q,const OnsetScore& score){
    // Retain the original reported minima; independent maxima must use actual
    // windows, including values below one or a negative projected near gain.
    q.onset_gain=std::min(q.onset_gain,score.gain);
    q.onset_correlation=std::min(q.onset_correlation,score.correlation);
    q.onset_baseline_gain=std::min(q.onset_baseline_gain,score.baseline_gain);
    q.onset_maximum_gain=q.onset_windows?std::max(q.onset_maximum_gain,score.gain):score.gain;
    q.onset_maximum_baseline_gain=q.onset_windows?std::max(q.onset_maximum_baseline_gain,score.baseline_gain):score.baseline_gain;
    ++q.onset_windows;
}
bool PassOnsets(const Quality& q){
    return q.onset_gain>=.45&&q.onset_correlation>=.65&&q.onset_baseline_gain>=.45&&
        q.onset_maximum_gain<=1.5&&q.onset_maximum_baseline_gain<=1.5;
}
int MetricContracts(){
    constexpr size_t count=4480,first=160,last=1440; // 280 ms burst, unchanged 10–90 ms onset window.
    std::vector<double> expected(count);
    for(size_t i=0;i<count;++i)expected[i]=.2*std::sin(2*pi*250*i/16000);
    auto WholeWithinLimits=[](const OnsetScore& score){return score.gain>=.45&&score.gain<=1.5&&score.correlation>=.65&&score.baseline_gain>=.45&&score.baseline_gain<=1.5;};
    auto Rejected=[](auto action){bool rejected=false;try{action();}catch(const std::invalid_argument&){rejected=true;}Check(rejected,"malformed onset metric input rejected");};
    {Quality q;RecordOnset(q,ScoreOnset(expected,expected,expected,first,last));
     Check(PassOnsets(q)&&q.onset_windows==1,"unchanged source accepted");
     Check(std::abs(q.onset_maximum_gain-1)<1e-12&&std::abs(q.onset_maximum_baseline_gain-1)<1e-12,"unit maxima measured");}
    Done("metric-unchanged-source");
    {auto mixed=expected;for(size_t i=first;i<last;++i)mixed[i]*=2;
     const auto whole=ScoreOnset(mixed,expected,expected,0,count);Quality q;RecordOnset(q,ScoreOnset(mixed,expected,expected,first,last));
     Check(WholeWithinLimits(whole)&&std::abs(whole.gain-9.0/7)<1e-12,"whole burst can hide doubled attack");
     Check(q.onset_gain==1&&q.onset_maximum_gain>1.5&&!PassOnsets(q),"actual near maximum rejects attack amplification");}
    Done("metric-amplified-near-attack");
    {auto clean=expected;for(size_t i=first;i<last;++i)clean[i]*=2;
     Quality q;RecordOnset(q,ScoreOnset(clean,clean,expected,first,last));
     Check(WholeWithinLimits(ScoreOnset(clean,clean,expected,0,count)),"whole baseline energy can hide amplified attack");
     Check(q.onset_gain==1&&q.onset_baseline_gain==1&&q.onset_maximum_baseline_gain>1.5&&!PassOnsets(q),"baseline maximum rejects shared amplification");}
    Done("metric-amplified-baseline-attack");
    {auto mixed=expected;std::fill(mixed.begin()+first,mixed.begin()+last,0);Quality q;
     RecordOnset(q,ScoreOnset(mixed,expected,expected,first,last));
     Check(q.onset_gain==0&&q.onset_maximum_gain==0&&!PassOnsets(q),"dropped near attack rejected");}
    Done("metric-dropped-near-attack");
    {auto muted=expected;std::fill(muted.begin()+first,muted.begin()+last,0);Quality q;
     RecordOnset(q,ScoreOnset(muted,muted,expected,first,last));
     Check(q.onset_baseline_gain==0&&q.onset_maximum_baseline_gain==0&&!PassOnsets(q),"mutually muted outputs cannot hide source loss");}
    Done("metric-mutually-muted-attack");
    {auto mixed=expected;for(size_t i=first;i<last;++i)mixed[i]+=.4*std::sin(2*pi*500*i/16000);
     Quality q;RecordOnset(q,ScoreOnset(mixed,expected,expected,first,last));
     Check(std::abs(q.onset_gain-1)<1e-12&&q.onset_maximum_gain<=1.5&&q.onset_correlation<.65&&!PassOnsets(q),"correlation rejects distortion with preserved projected gain");}
    Done("metric-correlated-gain-is-insufficient");
    {auto clean=expected,mixed=expected;for(size_t i=0;i<count;++i){clean[i]*=.8;mixed[i]*=.6;}
     Quality q;RecordOnset(q,ScoreOnset(mixed,clean,expected,first,last));
     Check(PassOnsets(q)&&std::abs(q.onset_maximum_gain-.75)<1e-12&&std::abs(q.onset_maximum_baseline_gain-.8)<1e-12,"maxima below one are not clipped to one");}
    Done("metric-actual-subunit-maxima");
    {auto mixed=expected;for(size_t i=first;i<last;++i)mixed[i]*=2;Quality q;
     RecordOnset(q,ScoreOnset(mixed,expected,expected,first,last));RecordOnset(q,ScoreOnset(mixed,expected,expected,1600,2880));
     Check(q.onset_windows==2&&q.onset_maximum_gain>1.5&&!PassOnsets(q),"later passing window cannot erase amplified attack");}
    Done("metric-maximum-survives-later-window");
    {Rejected([&]{ScoreOnset(expected,expected,expected,first,first);});
     Rejected([&]{ScoreOnset(expected,expected,expected,last,first);});
     Rejected([&]{ScoreOnset(expected,expected,expected,first,count+1);});}
    Done("metric-window-bounds");
    {auto short_input=expected;short_input.pop_back();
     Rejected([&]{ScoreOnset(short_input,expected,expected,first,last);});
     Rejected([&]{ScoreOnset(expected,short_input,expected,first,last);});
     Rejected([&]{ScoreOnset(expected,expected,short_input,first,last);});}
    Done("metric-shape-mismatch");
    {for(double value:{std::numeric_limits<double>::quiet_NaN(),std::numeric_limits<double>::infinity(),-std::numeric_limits<double>::infinity(),1.1,-1.1}){
      auto bad=expected;bad[first]=value;
      Rejected([&]{ScoreOnset(bad,expected,expected,first,last);});
      Rejected([&]{ScoreOnset(expected,bad,expected,first,last);});
      Rejected([&]{ScoreOnset(expected,expected,bad,first,last);});}}
    Done("metric-nonfinite-or-unnormalized-samples");
    {auto silent=expected;std::fill(silent.begin()+first,silent.begin()+last,0);
     Rejected([&]{ScoreOnset(expected,expected,silent,first,last);});}
    Done("metric-silent-source");
    {auto inverted=expected;for(auto& sample:inverted)sample=-sample;Quality q;
     RecordOnset(q,ScoreOnset(inverted,expected,expected,first,last));
     Check(std::abs(q.onset_maximum_gain+1)<1e-12&&!PassOnsets(q),"negative projected maximum remains an actual observation");}
    Done("metric-negative-gain-maximum");
    Check(cases==13,"complete onset metric contracts");
    std::printf("{\"status\":\"passed\",\"scope\":\"clock-aligned-metric-contracts\",\"metricCases\":13,\"assertions\":%d,\"physicalAudio\":false,\"deviceOpened\":false}\n",checks);return 0;
}
Quality AudioCase(double capture_ppm,double render_ppm,int jitter_us,Pattern pattern,NearSignal signal=NearSignal::Original){
    Broadband broadband;auto sound=[&](double position,bool right=false){
        // Four seconds of broadband history can train the filter before music.
        // Cold-start tones remain an independent requirement, never replaced.
        const bool tonal=pattern!=Pattern::Broadband&&(pattern==Pattern::Tonal||position>=4*48000);
        return tonal?Wave(position,right):broadband.At(position,right);
    };
    constexpr int frames=1800;const double cr=48000*(1+capture_ppm/1e6),rr=48000*(1+render_ppm/1e6);
    auto acoustic_echo=[&](double t){
        // A changed reflection at second eight models an acoustic path change,
        // not a device-clock change. Allow the existing three-second recovery.
        const bool changed=pattern==Pattern::WarmTonalPathChange&&t>=8;
        return (changed?.13f:.55f)*sound((t-.08)*rr)+(changed?.64f:.22f)*sound((t-(changed?.125:.11))*rr,true);
    };
    Time time;DuplexProcessor echo(31,Time::Get,&time),both(31,Time::Get,&time),near(31,Time::Get,&time);
    std::array<float,480> mic_echo{},mic_both{},mic_near{};std::array<float,960> ref{},silence{};
    std::array<int16_t,160> out{};DuplexFrame result;
    std::vector<double> cancelled,combined,baseline,expected_echo,expected_near,sample_times;
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
                const float e=acoustic_echo(t);
                mic_echo[i]=e;mic_near[i]=Near(t,signal);mic_both[i]=e+mic_near[i];}
            CaptureFrame cf{31,input,static_cast<int64_t>(input*480),cs,time.now,time.now,480};
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
        const auto metadata=result;q.clipped=metadata.clipped_samples;
        Check(echo.AcknowledgeDelivery(result)==DuplexStatus::Accepted,"echo delivered");
        for(int i=0;i<160;++i){cancelled.push_back(out[i]/32768.0);const double t=(metadata.capture_first.position+metadata.capture_first.fraction+i*3*metadata.capture_per_render)/cr;
            expected_echo.push_back(acoustic_echo(t));expected_near.push_back(Near(t,signal));sample_times.push_back(t);}
        const double true_delay=(.08+frame*480/rr-(metadata.capture_first.position+metadata.capture_first.fraction)/cr)*1000;
        q.max_delay_error_ms=std::max(q.max_delay_error_ms,std::abs(metadata.delay.milliseconds-true_delay));
        Check(both.Process(out.data(),160,result)==DuplexStatus::Frame,"quality aligned double talk");for(auto x:out)combined.push_back(x/32768.0);q.clipped+=result.clipped_samples;
        Check(both.AcknowledgeDelivery(result)==DuplexStatus::Accepted,"quality delivered");
        Check(near.Process(out.data(),160,result)==DuplexStatus::Frame,"quality aligned near baseline");for(auto x:out)baseline.push_back(x/32768.0);q.clipped+=result.clipped_samples;
        Check(near.AcknowledgeDelivery(result)==DuplexStatus::Accepted,"baseline delivered");
    }
    const int first=(pattern==Pattern::WarmTonalPathChange?11:8)*16000,last=17*16000;double err=0,original=0,bb=0,raw=0,cross=0,cc=0;
    for(int i=first;i<last;++i){err+=cancelled[i]*cancelled[i];original+=expected_echo[i]*expected_echo[i];bb+=baseline[i]*baseline[i];raw+=expected_near[i]*expected_near[i];cross+=combined[i]*baseline[i];cc+=combined[i]*combined[i];}
    q.reduction=10*std::log10(original/std::max(err,1e-30));q.correlation=cross/std::sqrt(std::max(bb*cc,1e-30));q.gain=cross/std::max(bb,1e-30);q.absolute_gain=std::sqrt(bb/std::max(raw,1e-30));
    if(signal==NearSignal::BurstyOverlapping){
        for(int second=pattern==Pattern::WarmTonalPathChange?11:8;second<17;++second){
            // Use measured source time under skew, not nominal output indices.
            // Evaluate 80 ms after a conservative 10 ms fixed-DSP allowance.
            const auto begin=std::lower_bound(sample_times.begin(),sample_times.end(),second+.01)-sample_times.begin();
            const auto end=std::lower_bound(sample_times.begin(),sample_times.end(),second+.09)-sample_times.begin();
            Check(end>begin&&end<static_cast<decltype(end)>(sample_times.size()),"complete onset window");
            RecordOnset(q,ScoreOnset(combined,baseline,expected_near,static_cast<size_t>(begin),static_cast<size_t>(end)));
        }
    }
    return q;
}
bool Pass(const Quality& q){
    return q.reduction>=15&&q.correlation>=.65&&q.gain>=.45&&q.gain<=1.5&&q.absolute_gain>=.45&&q.absolute_gain<=1.5&&q.max_delay_error_ms<=2&&q.clipped==0;
}
int OverlappingSpeechCases(){
    const double settings[][3]={{0,0,0},{900,-900,20},{-900,900,20}};
    int failures=0,total=0,onsets=0;
    for(auto signal:{NearSignal::Overlapping,NearSignal::QuietOverlapping,NearSignal::BurstyOverlapping})
        for(auto pattern:{Pattern::Tonal,Pattern::WarmTonal,Pattern::WarmTonalPathChange})for(const auto& c:settings){
            const auto q=AudioCase(c[0],c[1],static_cast<int>(c[2]),pattern,signal);
            const int expected_onsets=signal==NearSignal::BurstyOverlapping?(pattern==Pattern::WarmTonalPathChange?6:9):0;
            Check(q.onset_windows==expected_onsets,"complete overlap onset coverage");onsets+=q.onset_windows;
            const bool pass=Pass(q)&&PassOnsets(q);
            ++total;if(!pass)++failures;
            std::printf("{\"nearSignal\":\"%s\",\"pattern\":\"%s\",\"capturePpm\":%.0f,\"renderPpm\":%.0f,\"timestampJitterUs\":%.0f,\"echoReductionDb\":%.6f,\"nearCorrelation\":%.6f,\"nearGain\":%.6f,\"baselineGain\":%.6f,\"maxDelayErrorMs\":%.6f,\"onsetGain\":%.6f,\"onsetCorrelation\":%.6f,\"onsetBaselineGain\":%.6f,\"onsetMaximumGain\":%.6f,\"onsetMaximumBaselineGain\":%.6f,\"onsetWindows\":%d,\"clipped\":%llu,\"status\":\"%s\"}\n",Name(signal),Name(pattern),c[0],c[1],c[2],q.reduction,q.correlation,q.gain,q.absolute_gain,q.max_delay_error_ms,q.onset_gain,q.onset_correlation,q.onset_baseline_gain,q.onset_maximum_gain,q.onset_maximum_baseline_gain,q.onset_windows,static_cast<unsigned long long>(q.clipped),pass?"passed":"failed");
        }
    Check(total==27&&onsets==72,"complete overlapping voiced-signal matrix");
    std::printf("{\"status\":\"%s\",\"scope\":\"clock-aligned-overlapping-speech\",\"qualityCases\":27,\"onsetWindows\":72,\"failures\":%d,\"physicalAudio\":false,\"deviceOpened\":false}\n",failures?"failed":"passed",failures);
    return failures?1:0;
}
int QualityCases(Pattern pattern){
    const double settings[][3]={{0,0,0},{900,-900,20},{-900,900,20}};
    int failures=0;
    std::printf("{\"pattern\":\"%s\",\"measurementStartSeconds\":%d,\"measurementEndSeconds\":17}\n",Name(pattern),pattern==Pattern::WarmTonalPathChange?11:8);
    for(const auto& s:settings){const auto q=AudioCase(s[0],s[1],static_cast<int>(s[2]),pattern);
        const bool pass=Pass(q);
        if(!pass)++failures;
        std::printf("{\"capturePpm\":%.0f,\"renderPpm\":%.0f,\"timestampJitterUs\":%.0f,\"echoReductionDb\":%.6f,\"nearCorrelation\":%.6f,\"nearGain\":%.6f,\"baselineGain\":%.6f,\"maxDelayErrorMs\":%.6f,\"clipped\":%llu,\"status\":\"%s\"}\n",s[0],s[1],s[2],q.reduction,q.correlation,q.gain,q.absolute_gain,q.max_delay_error_ms,static_cast<unsigned long long>(q.clipped),pass?"passed":"failed");
    }
    std::printf("{\"status\":\"%s\",\"scope\":\"%s\",\"qualityCases\":3,\"failures\":%d,\"physicalAudio\":false,\"deviceOpened\":false}\n",failures?"failed":"passed",pattern==Pattern::Broadband?"clock-aligned-quality":pattern==Pattern::Tonal?"clock-aligned-tonal":pattern==Pattern::WarmTonal?"clock-aligned-warm-tonal":"clock-aligned-warm-tonal-path-change",failures);
    return failures?1:0;
}
}
int main(int argc,char** argv){try{
    if(argc==2&&std::strcmp(argv[1],"--metric-contracts")==0){return MetricContracts();}
    if(argc==2&&std::strcmp(argv[1],"--quality")==0){return QualityCases(Pattern::Broadband);}
    if(argc==2&&std::strcmp(argv[1],"--tonal")==0){return QualityCases(Pattern::Tonal);}
    if(argc==2&&std::strcmp(argv[1],"--warm-tonal")==0){return QualityCases(Pattern::WarmTonal);}
    if(argc==2&&std::strcmp(argv[1],"--warm-tonal-path-change")==0){return QualityCases(Pattern::WarmTonalPathChange);}
    if(argc==2&&std::strcmp(argv[1],"--overlapping-speech")==0){return OverlappingSpeechCases();}
    Check(argc==1,"known command");ClockCases();OwnershipCases();FragmentParity();Check(cases==31,"complete clock/duplex contracts");
    std::printf("{\"status\":\"passed\",\"alignmentCases\":%d,\"assertions\":%d,\"physicalAudio\":false,\"deviceOpened\":false}\n",cases,checks);return 0;
}catch(const std::exception& e){std::fprintf(stderr,"FAILED: %s\n",e.what());return 1;}}
