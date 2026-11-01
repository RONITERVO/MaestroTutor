// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#pragma once
#include "DeviceAudioClock.h"
#include <array>
#include <cstdint>
namespace maestro {
struct SamplePoint { int64_t position=0; double fraction=0; };
struct SampleTime { double nanoseconds=0, error_ns=0; };
// Worker-owned, bounded model of one 48 kHz device clock. These are evaluation
// admission limits, not claims about the accuracy of Android timestamps.
class HardwareClock final {
public:
    static constexpr double NominalPeriod=1000000000.0/48000;
    static constexpr double MaxDrift=.002, TimestampErrorNs=200000;
    static constexpr int64_t FreshNs=250000000, ProjectionNs=500000000;
    bool Observe(DeviceAudioStamp stamp,int64_t before,int64_t after);
    bool Project(SamplePoint point,int64_t now,SampleTime& time) const;
    double Period() const { return period_; }
    bool Fresh(int64_t now) const;
    bool Fitted() const { return fitted_; }
    bool Faulted() const { return faulted_; }
private:
    bool Fail();
    double ErrorAt(double position_delta) const;
    std::array<DeviceAudioStamp,64> history_{};
    size_t count_=0,next_=0;
    DeviceAudioStamp latest_{};
    int64_t last_query_=0;
    double period_=NominalPeriod, latest_time_=0, fit_mean_=0, fit_denominator_=0;
    bool fitted_=false,faulted_=false;
};
struct MeasuredDelay { int milliseconds=0; double nanoseconds=0,error_ns=0; };
// Upstream APM contract: (speaker presentation - reverse analysis) +
// (capture processing - microphone acquisition). No arbitrary latency clamp.
bool MeasureDelay(const HardwareClock& render,const HardwareClock& capture,
    SamplePoint render_first,SamplePoint capture_first,int64_t analyze_ns,
    int64_t process_ns,MeasuredDelay& delay);
}
