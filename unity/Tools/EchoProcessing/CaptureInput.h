// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#pragma once
#include "DeviceAudioClock.h"
#include <array>
#include <cstdint>
#include <memory>
namespace maestro {
struct CaptureFrame {
    uint64_t epoch = 0, sequence = 0;
    int64_t first_position = 0;
    DeviceAudioStamp hardware;
    int64_t query_started_ns = 0, query_finished_ns = 0;
};
enum class CaptureRead { Waiting, Frame, Invalid, Faulted, Closed };
enum class CaptureFault { None, Device, Clock, Gap, Samples, Overflow, Timeout };
// Every call, including destruction, belongs to ONE non-real-time worker.
// No close/read race, callbacks, implicit reopening or permission UI lives here.
class CaptureDevice {
public:
    virtual ~CaptureDevice() = default;
    virtual bool Start() = 0;
    virtual int Ready() = 0; // 1 started, 0 starting, negative disconnected/error.
    virtual int Read(float* samples, int capacity) = 0; // Nonblocking, actual count.
    virtual int Timestamp(DeviceAudioStamp& stamp) = 0; // 1 available, 0 not yet, negative error.
    virtual int64_t Position() = 0;
    virtual int Xruns() = 0;
    virtual int64_t Now() = 0; // CLOCK_MONOTONIC nanoseconds, same as Timestamp.
    virtual void Close() = 0; // Idempotent, off callback/render threads.
};
// Explicit acquisition; caller must first hold microphone permission and the
// application's sole capture lease. Failure never falls back to another device.
// Requested input: mono, float, 48 kHz, privacy-sensitive, unprocessed preset.
// UNPROCESSED is a request, not proof that hardware performs no preprocessing.
std::unique_ptr<CaptureDevice> OpenAndroidCapture();

class CaptureInput final {
public:
    static constexpr int Samples = 480; // Exactly 10 ms, normalized mono float.
    CaptureInput(std::unique_ptr<CaptureDevice> device, uint64_t epoch);
    ~CaptureInput();
    CaptureInput(const CaptureInput&) = delete;
    CaptureInput& operator=(const CaptureInput&) = delete;
    bool Start();
    CaptureRead Read(float* output, int count, CaptureFrame& frame);
    void Close();
    CaptureFault Fault() const { return fault_; }
private:
    CaptureRead Fail(CaptureFault reason);
    std::unique_ptr<CaptureDevice> device_;
    std::array<float, Samples> pending_{};
    DeviceAudioStamp last_stamp_{};
    uint64_t epoch_, sequence_ = 0;
    int pending_count_ = 0, xruns_ = 0;
    int64_t started_ns_ = 0, last_now_ = 0, last_frame_ns_ = 0;
    int64_t next_position_ = 0, first_position_ = 0;
    bool started_ = false, closed_ = false, stamped_ = false, positioned_ = false;
    CaptureFault fault_ = CaptureFault::None;
};
}
