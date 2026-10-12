// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#include "CaptureEffects.h"
#include <algorithm>
namespace maestro {
namespace {
class GuardedCapture final : public CaptureDevice {
    std::unique_ptr<CaptureDevice> device_;
    std::unique_ptr<CaptureEffect> effect_;
    bool started_ = false;
    bool Check() {
        if (device_ && effect_ && effect_->Healthy()) return true;
        Close(); return false;
    }
public:
    GuardedCapture(std::unique_ptr<CaptureDevice> device, std::unique_ptr<CaptureEffect> effect)
        : device_(std::move(device)), effect_(std::move(effect)) {}
    ~GuardedCapture() override { Close(); }
    bool Start() override {
        if (started_ || !Check()) return false;
        started_ = true;
        if (device_->Start() && Check()) return true;
        Close(); return false;
    }
    int Ready() override {
        if (!Check()) return -1;
        const int result = device_->Ready();
        if (result < 0 || !Check()) { Close(); return -1; }
        return result;
    }
    int Read(float* output, int capacity) override {
        if (!output || capacity < 1 || capacity > CaptureInput::Samples) return -1;
        std::fill_n(output, capacity, 0.0f);
        if (!started_ || !Check()) return -1;
        const int count = device_->Read(output, capacity);
        if (count < 0 || count > capacity || !Check()) {
            std::fill_n(output, capacity, 0.0f); Close(); return -1;
        }
        return count;
    }
    int Timestamp(DeviceAudioStamp& stamp) override {
        stamp = {};
        if (!Check()) return -1;
        const int result = device_->Timestamp(stamp);
        if (result < 0 || !Check()) { stamp = {}; Close(); return -1; }
        return result;
    }
    int64_t Position() override { return device_ ? device_->Position() : -1; }
    int Xruns() override { return device_ ? device_->Xruns() : -1; }
    int64_t Now() override { return device_ ? device_->Now() : -1; }
    void Close() override {
        if (device_) { device_->Close(); device_.reset(); }
        if (effect_) { effect_->Close(); effect_.reset(); }
    }
};
}
std::unique_ptr<CaptureDevice> GuardCaptureEffects(std::unique_ptr<CaptureDevice> device,
        std::unique_ptr<CaptureEffect> effect) {
    // Keep cleanup order even when either acquisition failed.
    auto owner = std::make_unique<GuardedCapture>(std::move(device), std::move(effect));
    if (owner->Ready() < 0) return {};
    return owner;
}
}
