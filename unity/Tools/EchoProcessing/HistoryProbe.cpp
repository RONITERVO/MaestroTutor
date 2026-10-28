// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#include "DuplexProcessor.h"
#include "AlignedAudioFixture.h"
#include <array>
#include <cstdio>
#include <cstring>
#include <memory>
#include <limits>
using namespace maestro;
using namespace maestro_aligned_fixture;
namespace {
void Require(bool ok,const char* why){if(!ok)throw std::runtime_error(why);}
struct Time {
    int64_t now=1100000000;
    static int64_t Get(void* context){return static_cast<Time*>(context)->now;}
};
struct Selection {Pattern pattern;NearSignal near;double capture,render;int jitter;};
constexpr Selection selections[]={
    {Pattern::Broadband,NearSignal::Original,0,0,0},
    {Pattern::Tonal,NearSignal::QuietOverlapping,0,0,0},
    {Pattern::WarmTonal,NearSignal::QuietOverlapping,0,0,0},
    {Pattern::WarmTonalPathChange,NearSignal::QuietOverlapping,-900,900,20},
    {Pattern::WarmTonal,NearSignal::BurstyOverlapping,900,-900,20},
    {Pattern::WarmTonalPathChange,NearSignal::BurstyOverlapping,0,0,0}
};
constexpr double windows[][2]={{6.02,8},{8,11},{11,17}};
float Source(double t,NearSignal signal,bool quiet_prefix){
    // Causal diagnostic ONLY: never apply this intervention to microphone input.
    // It changes all pre-six-second microphone history, not just broadband history.
    return quiet_prefix&&t<6?0:Near(t,signal);
}
bool SameMetadata(const DuplexFrame& a,const DuplexFrame& b){
    return a.epoch==b.epoch&&a.sequence==b.sequence&&
        a.capture_first.position==b.capture_first.position&&a.capture_first.fraction==b.capture_first.fraction&&
        a.render_first==b.render_first&&a.capture_per_render==b.capture_per_render&&
        a.delay.milliseconds==b.delay.milliseconds&&a.delay.nanoseconds==b.delay.nanoseconds&&
        a.delay.error_ns==b.delay.error_ns;
}
struct Score {double gain,correlation,rms_ratio,error_ratio;};
struct Comparison {
    double reference=0,actual=0,cross=0,error=0;uint64_t count=0;
    void Add(double x,double baseline){
        if(!std::isfinite(x)||!std::isfinite(baseline)||std::abs(x)>1||std::abs(baseline)>1)
            throw std::invalid_argument("Comparison requires normalized finite PCM");
        reference+=baseline*baseline;actual+=x*x;cross+=x*baseline;error+=(x-baseline)*(x-baseline);++count;
    }
    Score Measure()const{
        if(!count||reference<=0)throw std::invalid_argument("Comparison needs a nonempty audible baseline");
        return {cross/reference,actual>0?cross/std::sqrt(actual*reference):0,
            std::sqrt(actual/reference),std::sqrt(error/reference)};
    }
};
void Print(const Score& s){
    std::printf("{\"projectedGain\":%.9f,\"correlation\":%.9f,\"rmsRatio\":%.9f,\"differenceRmsRatio\":%.9f}",
        s.gain,s.correlation,s.rms_ratio,s.error_ratio);
}
struct Window {
    Comparison control,mixed;
    double raw=0,expected_echo=0,cancelled=0;
    void Add(double source,double echo,double baseline,double echo_output,double mixed_output,double control_output){
        control.Add(control_output,baseline);mixed.Add(mixed_output,baseline);
        raw+=source*source;expected_echo+=echo*echo;cancelled+=echo_output*echo_output;
    }
};
void Run(int index,bool quiet_prefix){
    Require(index>=0&&index<6,"Known history case");
    Require(!quiet_prefix||index==2||index==3,"Only two defined history interventions");
    const auto c=selections[index];
    Broadband broadband;
    auto sound=[&](double position,bool right=false){
        const bool tonal=c.pattern!=Pattern::Broadband&&(c.pattern==Pattern::Tonal||position>=4*48000);
        return tonal?Wave(position,right):broadband.At(position,right);
    };
    const double cr=48000*(1+c.capture/1e6),rr=48000*(1+c.render/1e6);
    auto echo=[&](double t){
        const bool changed=c.pattern==Pattern::WarmTonalPathChange&&t>=8;
        return (changed?.13f:.55f)*sound((t-.08)*rr)+(changed?.64f:.22f)*sound((t-(changed?.125:.11))*rr,true);
    };
    Time time;std::array<std::unique_ptr<DuplexProcessor>,4> processors;
    // 0: near/silent reference; 1: echo/reference; 2: near+echo/reference;
    // 3: SAME near/reference, no acoustic echo. No stream is derived by subtraction.
    for(int s:{1,2,0,3})processors[s]=std::make_unique<DuplexProcessor>(31,Time::Get,&time);
    std::array<std::array<float,480>,4> mic{};
    std::array<float,960> ref{},silence{};
    std::array<std::array<int16_t,160>,4> out{};
    std::array<DuplexFrame,4> metadata{};
    std::array<Window,3> scores{};
    uint64_t input=0;double maximum_delay_error=0;
    constexpr int frames=1800;
    for(int frame=0;frame<frames;++frame){
        const double wall=.1+frame*480/rr;time.now=1000000000+static_cast<int64_t>(std::llround(wall*1e9));
        const auto cp=static_cast<int64_t>(std::floor((wall-.001)*cr));
        const auto rp=static_cast<int64_t>(std::floor((wall-.001-.08)*rr));
        const int64_t jitter=((frame%3)-1)*c.jitter*1000LL;
        const DeviceAudioStamp cs{cp,1000000000+static_cast<int64_t>(std::llround(cp*1e9/cr))+jitter};
        const DeviceAudioStamp rs{rp,1080000000+static_cast<int64_t>(std::llround(rp*1e9/rr))-jitter};
        const auto wanted=static_cast<uint64_t>(std::ceil(frame*480*cr/rr))+960;
        while(input*480<wanted){
            for(int i=0;i<480;++i){
                const double t=(input*480+i)/cr;const float n=Source(t,c.near,quiet_prefix),e=echo(t);
                mic[0][i]=n;mic[1][i]=e;mic[2][i]=e+n;mic[3][i]=n;
            }
            CaptureFrame cf{31,input,static_cast<int64_t>(input*480),cs,time.now,time.now,480};
            for(int s:{1,2,0,3})Require(processors[s]->Capture(mic[s].data(),480,cf)==DuplexStatus::Accepted,"Four-stream capture");
            ++input;
        }
        for(int i=0;i<480;++i){ref[i*2]=sound(frame*480+i);ref[i*2+1]=sound(frame*480+i,true);}
        PlaybackFrame rf{31,static_cast<uint64_t>(frame),frame*480};
        PlaybackClock rc{&time,31,static_cast<uint64_t>(frame+1),rs,time.now,time.now};
        for(int s:{1,2,0,3}){
            Require(processors[s]->Render(s==0?silence.data():ref.data(),960,rf,rc)==DuplexStatus::Accepted,"Four-stream render");
        }
        for(int s:{1,2,0,3}){
            Require(processors[s]->Process(out[s].data(),160,metadata[s])==DuplexStatus::Frame,"Four-stream output");
            Require(processors[s]->AcknowledgeDelivery(metadata[s])==DuplexStatus::Accepted,"Four-stream delivery");
        }
        for(int s=0;s<4;++s){
            Require(SameMetadata(metadata[0],metadata[s]),"Same sample/clock identity across all controls");
            for(int previous=0;previous<s;++previous)Require(metadata[s].owner!=metadata[previous].owner,"Independent DSP owners");
        }
        const auto& m=metadata[0];
        const double true_delay=(.08+frame*480/rr-(m.capture_first.position+m.capture_first.fraction)/cr)*1000;
        maximum_delay_error=std::max(maximum_delay_error,std::abs(m.delay.milliseconds-true_delay));
        for(int i=0;i<160;++i){
            // These are DSP input coordinates, not acoustic output time. No lag search.
            const double t=(m.capture_first.position+m.capture_first.fraction+i*3*m.capture_per_render)/cr;
            for(size_t w=0;w<scores.size();++w)if(t>=windows[w][0]&&t<windows[w][1])
                scores[w].Add(Source(t,c.near,quiet_prefix),echo(t),out[0][i]/32768.0,
                    out[1][i]/32768.0,out[2][i]/32768.0,out[3][i]/32768.0);
        }
    }
    for(size_t w=0;w<scores.size();++w){
        const auto& s=scores[w];const auto control=s.control.Measure(),mixed=s.mixed.Measure();
        Require(s.raw>0&&s.expected_echo>0,"Audible diagnostic sources");
        std::printf("{\"caseIndex\":%d,\"schedule\":\"%s\",\"pattern\":\"%s\",\"nearSignal\":\"%s\","
            "\"capturePpm\":%.0f,\"renderPpm\":%.0f,\"timestampJitterUs\":%d,"
            "\"sourceStartSeconds\":%.2f,\"sourceEndSeconds\":%.2f,\"samples\":%llu,"
            "\"baselineRms\":%.9f,\"rawNearRms\":%.9f,\"echoReductionDb\":%.9f,\"control\":",
            index,quiet_prefix?"quiet-before-six":"original",Name(c.pattern),Name(c.near),c.capture,c.render,c.jitter,
            windows[w][0],windows[w][1],static_cast<unsigned long long>(s.control.count),
            std::sqrt(s.control.reference/s.control.count),std::sqrt(s.raw/s.control.count),
            10*std::log10(s.expected_echo/std::max(s.cancelled,1e-30)));
        Print(control);std::printf(",\"mixed\":");Print(mixed);
        std::printf(",\"metadataParityFrames\":1800,\"independentStreams\":4,\"maxDelayErrorMs\":%.9f,"
            "\"clipped\":[%llu,%llu,%llu,%llu],\"status\":\"recorded\",\"speechQualityAccepted\":false}\n",
            maximum_delay_error,static_cast<unsigned long long>(metadata[0].clipped_samples),
            static_cast<unsigned long long>(metadata[1].clipped_samples),static_cast<unsigned long long>(metadata[2].clipped_samples),
            static_cast<unsigned long long>(metadata[3].clipped_samples));
    }
    // Full simulated continuation, not proof that Close drains pending acoustic input.
}
int Metrics(){
    int cases=0;
    auto Close=[](double a,double b){Require(std::abs(a-b)<1e-12,"Known metric value");};
    auto Reject=[](auto action){bool rejected=false;try{action();}catch(const std::invalid_argument&){rejected=true;}Require(rejected,"Invalid metric rejected");};
    {Comparison c;c.Add(.25,.25);c.Add(-.25,-.25);auto s=c.Measure();Close(s.gain,1);Close(s.correlation,1);Close(s.rms_ratio,1);Close(s.error_ratio,0);}++cases;
    {Comparison c;c.Add(.5,.25);c.Add(-.5,-.25);auto s=c.Measure();Close(s.gain,2);Close(s.rms_ratio,2);Close(s.error_ratio,1);}++cases;
    {Comparison c;c.Add(.5,.25);c.Add(0,-.25);auto s=c.Measure();Close(s.gain,1);Close(s.correlation,1/std::sqrt(2.0));Close(s.rms_ratio,std::sqrt(2.0));Close(s.error_ratio,1);}++cases;
    {Comparison c;c.Add(0,.25);auto s=c.Measure();Close(s.gain,0);Close(s.correlation,0);Close(s.rms_ratio,0);Close(s.error_ratio,1);}++cases;
    {Comparison c;c.Add(-.25,.25);auto s=c.Measure();Close(s.gain,-1);Close(s.correlation,-1);Close(s.error_ratio,2);}++cases;
    {Comparison c;Reject([&]{c.Measure();});c.Add(.25,0);Reject([&]{c.Measure();});}++cases;
    {for(double bad:{std::numeric_limits<double>::quiet_NaN(),std::numeric_limits<double>::infinity(),-1.1,1.1}){
        Comparison c;Reject([&]{c.Add(bad,.25);});Reject([&]{c.Add(.25,bad);});}}++cases;
    {for(double t:{0.0,3.5,4.0,5.999,6.0,6.001,8.0,17.0}){
        Require(Source(t,NearSignal::QuietOverlapping,false)==Near(t,NearSignal::QuietOverlapping),"Original source unchanged");
        Require(Source(t,NearSignal::QuietOverlapping,true)==(t<6?0:Near(t,NearSignal::QuietOverlapping)),"Only pre-six prefix changed");
    }}++cases;
    Require(cases==8,"Complete history metric contracts");
    std::printf("{\"status\":\"passed\",\"scope\":\"history-metric-contracts\",\"metricCases\":8,\"physicalAudio\":false,\"deviceOpened\":false}\n");
    return 0;
}
}
int main(int argc,char** argv){try{
    if(argc==2&&std::strcmp(argv[1],"--metric-contracts")==0)return Metrics();
    Require(argc==1,"Known history command");
    for(int i=0;i<6;++i)Run(i,false);
    for(int i:{2,3})Run(i,true);
    std::printf("{\"status\":\"recorded\",\"scope\":\"clock-aligned-history-controls\",\"caseCount\":8,"
        "\"windowCount\":24,\"framesPerCase\":1800,\"independentStreams\":4,"
        "\"speechQualityAccepted\":false,\"physicalAudio\":false,\"deviceOpened\":false}\n");return 0;
}catch(const std::exception& e){std::fprintf(stderr,"FAILED: %s\n",e.what());return 1;}}
