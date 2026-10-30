// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#pragma once
#include "DuplexProcessor.h"
namespace maestro {
enum class CaptureSessionState { Created, Running, Finishing, Finished, Faulted, Closed };
enum class CaptureSessionFault { None, Input, Processing, Sink, Clock, Timeout, Release };
enum class CapturePump { Waiting, Progress, Finished, Faulted, Closed, Invalid };
// Synchronous admission to the ORIGINAL app sink; copy before returning. False
// or an exception faults the session. The sink may Close(), but cannot reenter
// any other session operation or destroy the session during its callback.
using CaptureSink=bool(*)(void*,const int16_t*,int,const DuplexFrame&);
// Candidate owner-worker composition, not an app/provider integration. Caller
// supplies the sole app capture lease, permission, actual playback references
// and the same monotonic time domain as the devices. No device is opened here.
// Serialize every method and destruction. Playback remains independently owned.
class CaptureSession final {
public:
    CaptureSession(std::unique_ptr<CaptureDevice> input,uint64_t epoch,
                   MonotonicClock clock,void* clock_context,CaptureSink sink,void* sink_context);
    ~CaptureSession();
    CaptureSession(const CaptureSession&)=delete;
    CaptureSession& operator=(const CaptureSession&)=delete;
    bool Start();
    DuplexStatus Render(const float* input,int count,const PlaybackFrame& frame,const PlaybackClock& clock);
    CapturePump Pump(); // One nonblocking input read, at most eight output frames.
    FlushStatus Flush(DuplexFlush& ticket);
    FlushStatus PollFlush(const DuplexFlush& ticket);
    // Graceful terminal intent: keep the SAME microphone alive only to supply
    // real processing context, drain the accepted prefix, then close. An older
    // pending flush is completed before fencing any newer accepted prefix.
    // One-second total deadline; caller must keep pumping actual references.
    FlushStatus Finish();
    FlushStatus PollFinish();
    void Close(); // Immediate cancellation: no drain, no later sink admission.
    CaptureSessionState State()const{return state_;}
    CaptureSessionFault Fault()const{return fault_;}
    CaptureFault InputFault()const{return input_.Fault();}
    DuplexFault ProcessingFault()const{return processor_.Fault();}
private:
    bool Admit(CaptureRead status,const CaptureFrame& frame);
    bool HandoffPending();
    bool Time(int64_t& value);
    bool FinishDeadline();
    void Advance();
    bool Release();
    void Fail(CaptureSessionFault reason);
    FlushStatus TerminalStatus()const;
    CaptureInput input_;
    DuplexProcessor processor_;
    MonotonicClock clock_;void* clock_context_;
    CaptureSink sink_;void* sink_context_;
    CaptureSessionState state_=CaptureSessionState::Created;
    CaptureSessionFault fault_=CaptureSessionFault::None;
    std::array<float,480> input_buffer_{};
    std::array<int16_t,160> output_buffer_{};
    DuplexFlush flush_{},finish_{};
    uint64_t accepted_=0;
    int64_t last_now_=0,finish_started_=0;
    bool callback_=false,flush_done_=false,finish_again_=false;
};
}
