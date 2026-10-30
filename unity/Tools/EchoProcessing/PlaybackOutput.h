// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#pragma once
#include "DeviceAudioClock.h"
#include <array>
#include <atomic>
#include <cstdint>
#include <memory>
namespace maestro {
// Returns false to stop callbacks. Position is the first device frame of this
// buffer, not the worker's submission cursor or callback arrival time.
using PlaybackCallback = bool(*)(void*, float*, int, int64_t);
class PlaybackDevice {
public:
    virtual ~PlaybackDevice() = default;
    virtual bool Start() = 0;
    virtual int Ready() = 0;
    virtual int Timestamp(DeviceAudioStamp&) = 0;
    virtual int Xruns() = 0;
    virtual int64_t Now() = 0;
    // Must quiesce callbacks before returning; never called from a callback.
    virtual void Close() = 0;
};
using PlaybackFactory = std::unique_ptr<PlaybackDevice>(*)(PlaybackCallback, void*);
std::unique_ptr<PlaybackDevice> OpenAndroidPlayback(PlaybackCallback, void*);
enum class PlaybackStatus { Ok, Waiting, Invalid, Full, Faulted, Closed };
enum class PlaybackFault { None, Device, Clock, Gap, ReferenceOverflow, Timeout };
struct PlaybackFrame { uint64_t epoch=0, sequence=0; int64_t first_position=0; };
struct PlaybackClock {
    const void* owner=nullptr; uint64_t epoch=0, observation=0;
    DeviceAudioStamp hardware; int64_t query_started_ns=0, query_finished_ns=0;
};
// Optional device endpoint, outside the app. One control worker owns every
// method except Render, which has exactly one platform callback producer.
// The control worker submits the final mix and drains the exact callback output
// reference. No decoder, spatializer, provider, clock resampler or Unity selector
// is duplicated here. Format is immutable: stereo float, 48 kHz.
class PlaybackOutput final {
public:
    static constexpr int Rate=48000, Channels=2, Frame=480, Capacity=24000;
    PlaybackOutput(PlaybackFactory factory, uint64_t epoch);
    ~PlaybackOutput();
    PlaybackOutput(const PlaybackOutput&)=delete;
    PlaybackOutput& operator=(const PlaybackOutput&)=delete;
    bool Start();
    PlaybackStatus Submit(const float* samples, int frames);
    PlaybackStatus ReadReference(float* output, int count, PlaybackFrame& frame);
    PlaybackStatus ObserveClock(PlaybackClock& clock);
    bool Drained(const PlaybackClock& clock);
    void Close();
    PlaybackFault Fault() const { return fault_.load(std::memory_order_acquire); }
    uint64_t UnderfilledFrames() const { return underfilled_.load(std::memory_order_acquire); }
private:
    static bool Callback(void*,float*,int,int64_t);
    bool Render(float*,int,int64_t);
    PlaybackStatus Fail(PlaybackFault);
    void Retire();
    std::unique_ptr<PlaybackDevice> device_;
    std::array<float,Capacity*Channels> queued_{}, reference_{};
    std::atomic<uint64_t> submitted_{0}, consumed_{0}, ref_written_{0}, ref_read_{0};
    std::atomic<uint64_t> last_source_end_{0}, underfilled_{0};
    std::atomic<bool> active_{false};
    std::atomic<PlaybackFault> fault_{PlaybackFault::None};
    uint64_t callback_position_=0, epoch_, sequence_=0, clock_sequence_=0;
    DeviceAudioStamp last_stamp_{};
    int xruns_=0;
    int64_t started_ns_=0,last_now_=0;
    bool started_=false,closed_=false,stamped_=false,retired_=false,clock_valid_=false;
};
}
