// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#include "ClockAlignment.h"
#include <algorithm>
#include <cmath>
namespace maestro {
bool HardwareClock::Fail(){faulted_=true;return false;}
bool HardwareClock::Observe(DeviceAudioStamp stamp,int64_t before,int64_t after){
    if(faulted_)return false;
    if(stamp.position<0||stamp.time_ns<0||before<last_query_||after<before||
       after-before>FreshNs||stamp.time_ns>after||after-stamp.time_ns>FreshNs)return Fail();
    last_query_=after;
    if(count_){
        if(stamp.position<latest_.position||stamp.time_ns<latest_.time_ns||
           ((stamp.position==latest_.position)!=(stamp.time_ns==latest_.time_ns)))return Fail();
        if(stamp.position==latest_.position)return true; // Repeated HAL observation is not a new fit point.
        const double elapsed=static_cast<double>(stamp.position-latest_.position)*period_;
        const double predicted=latest_time_+elapsed;
        if(std::abs(static_cast<double>(stamp.time_ns)-predicted)>
           TimestampErrorNs+ErrorAt(static_cast<double>(stamp.position-latest_.position)))return Fail();
    }
    latest_=stamp;
    // Retain time coverage even if a worker drains many short bursts. Every new
    // observation is validated, but sub-10 ms observations do not evict history.
    if(!count_||stamp.time_ns-history_[(next_+history_.size()-1)%history_.size()].time_ns>=10000000){
        history_[next_]=stamp;next_=(next_+1)%history_.size();
        count_=std::min(count_+1,history_.size());
    }
    latest_time_=static_cast<double>(stamp.time_ns);
    // Fit relative coordinates; absolute device uptime must not erase sample precision.
    const auto oldest=history_[count_==history_.size()?next_:0];
    const double span=static_cast<double>(stamp.time_ns-oldest.time_ns);
    if(count_<8||span<250000000)return true;
    double x=0,y=0,xx=0,xy=0;
    for(size_t i=0;i<count_;++i){
        const double dx=static_cast<double>(history_[i].position-stamp.position);
        const double dy=static_cast<double>(history_[i].time_ns-stamp.time_ns);
        x+=dx;y+=dy;xx+=dx*dx;xy+=dx*dy;
    }
    const double n=static_cast<double>(count_), denominator=xx-x*x/n;
    if(denominator<=0)return Fail();
    const double period=(xy-x*y/n)/denominator,intercept=(y-period*x)/n;
    if(!std::isfinite(period)||std::abs(period/NominalPeriod-1)>MaxDrift)return Fail();
    for(size_t i=0;i<count_;++i){
        const double predicted=intercept+period*static_cast<double>(history_[i].position-stamp.position);
        if(std::abs(predicted-static_cast<double>(history_[i].time_ns-stamp.time_ns))>TimestampErrorNs)return Fail();
    }
    period_=period;latest_time_+=intercept;fitted_=true;
    fit_mean_=x/n;fit_denominator_=denominator;
    return true;
}
double HardwareClock::ErrorAt(double delta)const{
    if(!fitted_)return TimestampErrorNs+std::abs(delta*period_)*MaxDrift;
    // Bound the fitted prediction using the absolute regression weights. A simple
    // 2*error/span slope formula would understate an adversarial jitter pattern.
    double weights=0,min_position=0,max_position=static_cast<double>(history_[0].position-latest_.position);
    const double n=static_cast<double>(count_);
    for(size_t i=0;i<count_;++i){
        const double x=static_cast<double>(history_[i].position-latest_.position);
        min_position=std::min(min_position,x);max_position=std::max(max_position,x);
        weights+=std::abs(1/n+(delta-fit_mean_)*(x-fit_mean_)/fit_denominator_);
    }
    const double outside=delta>max_position?delta-max_position:delta<min_position?min_position-delta:0;
    return TimestampErrorNs*weights+outside*period_*MaxDrift;
}
bool HardwareClock::Fresh(int64_t now)const{
    return !faulted_&&count_&&now>=last_query_&&now>=latest_.time_ns&&now-latest_.time_ns<=FreshNs;
}
bool HardwareClock::Project(SamplePoint point,int64_t now,SampleTime& time)const{
    time={};
    if(!Fresh(now)||
       point.position<0||!std::isfinite(point.fraction)||point.fraction<0||point.fraction>=1)return false;
    const double offset=(static_cast<double>(point.position-latest_.position)+point.fraction)*period_;
    if(std::abs(offset)>ProjectionNs)return false;
    const double result=latest_time_+offset;
    if(!std::isfinite(result)||result<0)return false;
    time={result,ErrorAt(static_cast<double>(point.position-latest_.position)+point.fraction)};return true;
}
bool MeasureDelay(const HardwareClock& render,const HardwareClock& capture,
    SamplePoint render_first,SamplePoint capture_first,int64_t analyze_ns,
    int64_t process_ns,MeasuredDelay& delay){
    delay={};SampleTime r,c;
    if(analyze_ns<0||process_ns<analyze_ns||process_ns-analyze_ns>5000000||
       !render.Project(render_first,process_ns,r)||!capture.Project(capture_first,process_ns,c)||
       c.nanoseconds>static_cast<double>(process_ns))return false;
    const double value=(r.nanoseconds-static_cast<double>(analyze_ns))+
        (static_cast<double>(process_ns)-c.nanoseconds);
    const double error=r.error_ns+c.error_ns+500000; // Integer millisecond quantization.
    if(!std::isfinite(value)||error>2000000||value-error<4000000||value+error>500000000)return false;
    delay={static_cast<int>(std::lround(value/1000000)),value,error};return true;
}
}
