// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#pragma once
#include "CaptureInput.h"
#include "PlaybackOutput.h"
#include "ClockAlignment.h"
#include <memory>
namespace maestro {
using MonotonicClock=int64_t(*)(void*);
enum class DuplexStatus { Waiting, Frame, Accepted, Faulted, Closed, Invalid };
enum class DuplexFault { None, Input, Sequence, Clock, Overflow, Processing };
struct DuplexFrame {
    uint64_t epoch=0,sequence=0;
    SamplePoint capture_first;
    int64_t render_first=0;
    MeasuredDelay delay;
    double capture_per_render=0;
    uint64_t clipped_samples=0;
};
// Worker-only candidate. Connects already-owned input/output frames to the same
// APM ABI; never opens devices or calls providers. All methods and destruction
// must be serialized. Fault/Close are terminal; a new epoch needs a new owner.
class DuplexProcessor final {
public:
    DuplexProcessor(uint64_t epoch,MonotonicClock clock,void* clock_context);
    ~DuplexProcessor();
    DuplexProcessor(const DuplexProcessor&)=delete;
    DuplexProcessor& operator=(const DuplexProcessor&)=delete;
    // Accept 1..480 real input samples, including a flushed capture fragment.
    // frame.samples must match count; sequence and sample position stay exact.
    DuplexStatus Capture(const float* input,int count,const CaptureFrame& frame);
    DuplexStatus Render(const float* input,int count,const PlaybackFrame& frame,const PlaybackClock& clock);
    DuplexStatus Process(int16_t* output,int count,DuplexFrame& frame);
    DuplexFault Fault()const;
    void Close();
private:
    struct State;
    std::unique_ptr<State> state_;
};
}
