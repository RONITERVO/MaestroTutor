// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#include "CaptureInput.h"
#include <algorithm>
#include <cmath>
#include <limits>
#include <stdexcept>
namespace maestro {
namespace { constexpr int64_t second = 1000000000, fresh = second / 2; }
CaptureInput::CaptureInput(std::unique_ptr<CaptureDevice> device, uint64_t epoch)
    : device_(std::move(device)), epoch_(epoch) {
    if (!device_ || !epoch_) {
        if (device_) device_->Close();
        throw std::invalid_argument("Capture requires one device and a nonzero epoch");
    }
}
CaptureInput::~CaptureInput() { Close(); }
bool CaptureInput::Start() {
    if (closed_ || started_ || fault_ != CaptureFault::None) return false;
    started_ns_ = last_now_ = last_frame_ns_ = device_->Now();
    xruns_ = device_->Xruns();
    if (started_ns_ < 0 || xruns_ < 0 || !device_->Start()) {
        Fail(CaptureFault::Device); return false;
    }
    started_ = true;
    return true;
}
CaptureRead CaptureInput::Fail(CaptureFault reason) {
    fault_ = reason;
    pending_.fill(0); pending_count_ = 0;
    if (device_) { device_->Close(); device_.reset(); }
    return CaptureRead::Faulted;
}
CaptureRead CaptureInput::Read(float* output, int count, CaptureFrame& frame) {
    frame = {};
    if (output && count == Samples) std::fill_n(output, Samples, 0.0f);
    if (closed_) return CaptureRead::Closed;
    if (fault_ != CaptureFault::None) return CaptureRead::Faulted;
    if (!output || count != Samples || !started_) return CaptureRead::Invalid;
    const int64_t before = device_->Now();
    if (before < last_now_ || before < 0) return Fail(CaptureFault::Clock);
    last_now_ = before;
    // Unavailable startup never becomes raw PCM or an invented timestamp.
    if ((!stamped_ && before - started_ns_ > second) ||
        (stamped_ && before - last_frame_ns_ > fresh)) return Fail(CaptureFault::Timeout);
    const int ready = device_->Ready();
    if (ready < 0) return Fail(CaptureFault::Device);
    if (!ready) return stamped_ ? Fail(CaptureFault::Device) : CaptureRead::Waiting;
    DeviceAudioStamp stamp;
    const int result = device_->Timestamp(stamp);
    const int64_t after = device_->Now();
    if (after < before) return Fail(CaptureFault::Clock);
    last_now_ = after;
    if (result < 0) return Fail(CaptureFault::Device);
    if (!result) return CaptureRead::Waiting;
    if (stamp.position < 0 || stamp.time_ns < 0 || stamp.time_ns > after ||
        after - stamp.time_ns > fresh || after - before > fresh ||
        (stamped_ && (stamp.position < last_stamp_.position || stamp.time_ns < last_stamp_.time_ns ||
         ((stamp.position == last_stamp_.position) != (stamp.time_ns == last_stamp_.time_ns)))))
        return Fail(CaptureFault::Clock);
    if (device_->Xruns() != xruns_) return Fail(CaptureFault::Overflow);
    const int64_t position = device_->Position();
    if (position < 0 || (positioned_ && position != next_position_)) return Fail(CaptureFault::Gap);
    if (!pending_count_) first_position_ = position;
    const int count_read = device_->Read(pending_.data() + pending_count_, Samples - pending_count_);
    if (count_read < 0) return Fail(CaptureFault::Device);
    if (count_read > Samples - pending_count_ || position > std::numeric_limits<int64_t>::max() - count_read)
        return Fail(CaptureFault::Gap);
    next_position_ = position + count_read;
    if (device_->Position() != next_position_) return Fail(CaptureFault::Gap);
    if (device_->Xruns() != xruns_) return Fail(CaptureFault::Overflow);
    const int64_t completed = device_->Now();
    if (completed < after || completed - stamp.time_ns > fresh || completed - before > fresh)
        return Fail(CaptureFault::Clock);
    last_now_ = completed;
    if (!stamped_) last_frame_ns_ = completed;
    last_stamp_ = stamp; stamped_ = true; positioned_ = true;
    query_started_ns_ = before; query_finished_ns_ = after;
    pending_count_ += count_read;
    for (int i = 0; i < pending_count_; ++i)
        if (!std::isfinite(pending_[i]) || std::abs(pending_[i]) > 1) return Fail(CaptureFault::Samples);
    if (pending_count_ < Samples) return CaptureRead::Waiting;
    return Deliver(output, frame, completed);
}
CaptureRead CaptureInput::Deliver(float* output, CaptureFrame& frame, int64_t completed) {
    if (sequence_ == std::numeric_limits<uint64_t>::max()) return Fail(CaptureFault::Gap);
    frame = {epoch_, sequence_++, first_position_, last_stamp_, query_started_ns_, query_finished_ns_, pending_count_};
    std::copy_n(pending_.begin(), pending_count_, output);
    pending_.fill(0); pending_count_ = 0; last_frame_ns_ = completed;
    return CaptureRead::Frame;
}
CaptureRead CaptureInput::FlushPending(float* output, int count, CaptureFrame& frame) {
    frame = {};
    if (output && count == Samples) std::fill_n(output, Samples, 0.0f);
    if (closed_) return CaptureRead::Closed;
    if (fault_ != CaptureFault::None) return CaptureRead::Faulted;
    if (!output || count != Samples || !started_) return CaptureRead::Invalid;
    if (!pending_count_) return CaptureRead::Empty;
    // Preserve the acquisition observation. Reading the clock to enforce
    // freshness must never relabel the old fragment with a newer timestamp.
    const auto now = device_->Now();
    if (now < last_now_ || now < 0 || now - last_stamp_.time_ns > fresh)
        return Fail(CaptureFault::Clock);
    last_now_ = now;
    if (device_->Ready() != 1) return Fail(CaptureFault::Device);
    if (device_->Xruns() != xruns_) return Fail(CaptureFault::Overflow);
    const auto completed = device_->Now();
    if (completed < now || completed - last_stamp_.time_ns > fresh)
        return Fail(CaptureFault::Clock);
    last_now_ = completed;
    return Deliver(output, frame, completed);
}

void CaptureInput::Close() {
    if (closed_) return;
    closed_ = true;
    pending_.fill(0); pending_count_ = 0; last_stamp_ = {};
    if (device_) { device_->Close(); device_.reset(); }
}
}
