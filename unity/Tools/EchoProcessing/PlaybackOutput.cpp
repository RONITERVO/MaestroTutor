// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#include "PlaybackOutput.h"
#include <algorithm>
#include <cmath>
#include <limits>
#include <stdexcept>
namespace maestro {
static_assert(std::atomic<uint64_t>::is_always_lock_free);
static_assert(std::atomic<bool>::is_always_lock_free);
static_assert(std::atomic<PlaybackFault>::is_always_lock_free);
namespace {
constexpr int64_t second=1000000000,fresh=second/2;
void CopyIn(std::array<float,PlaybackOutput::Capacity*2>& ring,uint64_t at,const float* data,int frames){
    const int offset=static_cast<int>(at%PlaybackOutput::Capacity)*2;
    const int first=std::min(frames*2,PlaybackOutput::Capacity*2-offset);
    std::copy_n(data,first,ring.data()+offset);std::copy_n(data+first,frames*2-first,ring.data());
}
void CopyOut(const std::array<float,PlaybackOutput::Capacity*2>& ring,uint64_t at,float* data,int frames){
    const int offset=static_cast<int>(at%PlaybackOutput::Capacity)*2;
    const int first=std::min(frames*2,PlaybackOutput::Capacity*2-offset);
    std::copy_n(ring.data()+offset,first,data);std::copy_n(ring.data(),frames*2-first,data+first);
}
}
PlaybackOutput::PlaybackOutput(PlaybackFactory factory,uint64_t epoch):epoch_(epoch){
    if(!factory||!epoch)throw std::invalid_argument("Playback requires an epoch and device factory");
    device_=factory(Callback,this);
    if(!device_)throw std::runtime_error("Playback device unavailable");
}
PlaybackOutput::~PlaybackOutput(){Close();}
void PlaybackOutput::Retire(){
    if(retired_)return;retired_=true;clock_valid_=false;
    active_.store(false,std::memory_order_release);
    if(device_){device_->Close();device_.reset();}
    // Close has fenced the callback; clearing its rings cannot race Render.
    queued_.fill(0);reference_.fill(0);
}
PlaybackStatus PlaybackOutput::Fail(PlaybackFault reason){
    auto expected=PlaybackFault::None;fault_.compare_exchange_strong(expected,reason,std::memory_order_acq_rel);
    Retire();return PlaybackStatus::Faulted;
}
bool PlaybackOutput::Start(){
    if(closed_||started_||Fault()!=PlaybackFault::None)return false;
    started_ns_=last_now_=device_->Now();xruns_=device_->Xruns();
    if(started_ns_<0||xruns_<0){Fail(PlaybackFault::Device);return false;}
    started_=true;active_.store(true,std::memory_order_release);
    if(!device_->Start()){Fail(PlaybackFault::Device);return false;}
    return true;
}
PlaybackStatus PlaybackOutput::Submit(const float* samples,int frames){
    if(closed_)return PlaybackStatus::Closed;
    if(Fault()!=PlaybackFault::None)return Fail(Fault());
    if(!samples||frames<1||frames>Capacity)return PlaybackStatus::Invalid;
    for(int i=0;i<frames*Channels;++i)if(!std::isfinite(samples[i])||std::abs(samples[i])>1)return PlaybackStatus::Invalid;
    const uint64_t written=submitted_.load(std::memory_order_relaxed),read=consumed_.load(std::memory_order_acquire);
    if(written>static_cast<uint64_t>(std::numeric_limits<int64_t>::max())-frames)return Fail(PlaybackFault::Gap);
    if(written-read>Capacity-static_cast<uint64_t>(frames))return PlaybackStatus::Full;
    CopyIn(queued_,written,samples,frames);submitted_.store(written+frames,std::memory_order_release);
    return PlaybackStatus::Ok;
}
bool PlaybackOutput::Callback(void* user,float* output,int frames,int64_t position){
    return static_cast<PlaybackOutput*>(user)->Render(output,frames,position);
}
bool PlaybackOutput::Render(float* output,int frames,int64_t position){
    // Bad buffer dimensions are a caller error; never write outside a known size.
    if(!output||frames<1||frames>Capacity){fault_.store(PlaybackFault::Gap,std::memory_order_release);return false;}
    std::fill_n(output,frames*Channels,0.0f);
    if(!active_.load(std::memory_order_acquire)||Fault()!=PlaybackFault::None)return false;
    if(position<0||static_cast<uint64_t>(position)!=callback_position_||
       callback_position_>static_cast<uint64_t>(std::numeric_limits<int64_t>::max())-frames){
        fault_.store(PlaybackFault::Gap,std::memory_order_release);return false;
    }
    const auto ref_read=ref_read_.load(std::memory_order_acquire);
    if(callback_position_-ref_read>Capacity-static_cast<uint64_t>(frames)){
        fault_.store(PlaybackFault::ReferenceOverflow,std::memory_order_release);return false;
    }
    const auto read=consumed_.load(std::memory_order_relaxed),written=submitted_.load(std::memory_order_acquire);
    const int copied=static_cast<int>(std::min<uint64_t>(written-read,frames));
    CopyOut(queued_,read,output,copied);
    // Missing queued audio becomes KNOWN zero output, not an invented reference
    // or a replay of old PCM. Device xruns remain a separate fatal timing gap.
    CopyIn(reference_,callback_position_,output,frames);
    if(copied)last_source_end_.store(callback_position_+copied,std::memory_order_release);
    consumed_.store(read+copied,std::memory_order_release);
    underfilled_.fetch_add(frames-copied,std::memory_order_relaxed);
    callback_position_+=frames;ref_written_.store(callback_position_,std::memory_order_release);
    return true;
}
PlaybackStatus PlaybackOutput::ReadReference(float* output,int count,PlaybackFrame& frame){
    frame={};if(output&&count==Frame*Channels)std::fill_n(output,count,0.0f);
    if(closed_)return PlaybackStatus::Closed;
    if(Fault()!=PlaybackFault::None)return Fail(Fault());
    if(!output||count!=Frame*Channels)return PlaybackStatus::Invalid;
    const auto read=ref_read_.load(std::memory_order_relaxed),written=ref_written_.load(std::memory_order_acquire);
    if(written-read<Frame)return PlaybackStatus::Waiting;
    CopyOut(reference_,read,output,Frame);
    frame={epoch_,sequence_++,static_cast<int64_t>(read)};ref_read_.store(read+Frame,std::memory_order_release);
    return PlaybackStatus::Ok;
}
PlaybackStatus PlaybackOutput::ObserveClock(PlaybackClock& clock){
    clock={};clock_valid_=false;
    if(clock_sequence_==std::numeric_limits<uint64_t>::max())return Fail(PlaybackFault::Clock);
    ++clock_sequence_;
    if(closed_)return PlaybackStatus::Closed;
    if(Fault()!=PlaybackFault::None)return Fail(Fault());
    if(!started_)return PlaybackStatus::Waiting;
    const auto before=device_->Now();
    if(before<last_now_||before<0)return Fail(PlaybackFault::Clock);
    last_now_=before;
    if((!stamped_&&before-started_ns_>second)||(stamped_&&before-last_stamp_.time_ns>fresh))return Fail(PlaybackFault::Timeout);
    const int ready=device_->Ready();
    if(ready<0)return Fail(PlaybackFault::Device);
    if(!ready)return stamped_?Fail(PlaybackFault::Device):PlaybackStatus::Waiting;
    DeviceAudioStamp stamp;const int result=device_->Timestamp(stamp);const auto after=device_->Now();
    if(after<before||after-before>fresh)return Fail(PlaybackFault::Clock);
    last_now_=after;
    if(result<0)return Fail(PlaybackFault::Device);
    if(!result)return PlaybackStatus::Waiting;
    if(stamp.position<0||static_cast<uint64_t>(stamp.position)>ref_written_.load(std::memory_order_acquire)||stamp.time_ns<0||stamp.time_ns>after||after-stamp.time_ns>fresh||
       (stamped_&&(stamp.position<last_stamp_.position||stamp.time_ns<last_stamp_.time_ns||
        ((stamp.position==last_stamp_.position)!=(stamp.time_ns==last_stamp_.time_ns)))))return Fail(PlaybackFault::Clock);
    if(device_->Xruns()!=xruns_)return Fail(PlaybackFault::Gap);
    if(Fault()!=PlaybackFault::None)return Fail(Fault());
    last_stamp_=stamp;stamped_=true;clock_valid_=true;clock={this,epoch_,clock_sequence_,stamp,before,after};return PlaybackStatus::Ok;
}
bool PlaybackOutput::Drained(const PlaybackClock& clock){
    // Only the last observation from THIS owner is admissible, while it is live.
    if(closed_||!stamped_||!clock_valid_||clock.owner!=this||clock.epoch!=epoch_||clock.observation!=clock_sequence_||
       Fault()!=PlaybackFault::None||clock.hardware.position!=last_stamp_.position||
       clock.hardware.time_ns!=last_stamp_.time_ns||clock.query_finished_ns!=last_now_)return false;
    const auto now=device_->Now();
    if(now<last_now_||now-clock.hardware.time_ns>fresh)return false;
    const auto read=consumed_.load(std::memory_order_acquire);
    return read==submitted_.load(std::memory_order_relaxed)&&
        static_cast<uint64_t>(clock.hardware.position)>=last_source_end_.load(std::memory_order_acquire);
}
void PlaybackOutput::Close(){if(closed_)return;closed_=true;Retire();}
}
