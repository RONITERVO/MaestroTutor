// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#include "CaptureInput.h"
#if defined(__ANDROID__)
#include <aaudio/AAudio.h>
#include <time.h>
namespace maestro {
namespace {
class AndroidCaptureDevice final : public CaptureDevice {
    AAudioStream* stream_;
public:
    explicit AndroidCaptureDevice(AAudioStream* stream) : stream_(stream) {}
    ~AndroidCaptureDevice() override { Close(); }
    bool Start() override { return stream_ && AAudioStream_requestStart(stream_) == AAUDIO_OK; }
    int Ready() override {
        if (!stream_) return -1;
        const auto state = AAudioStream_getState(stream_);
        if (state == AAUDIO_STREAM_STATE_STARTED) return 1;
        if (state == AAUDIO_STREAM_STATE_STARTING || state == AAUDIO_STREAM_STATE_OPEN) return 0;
        return -1;
    }
    int Read(float* samples, int capacity) override {
        return stream_ ? AAudioStream_read(stream_, samples, capacity, 0) : -1;
    }
    int Timestamp(DeviceAudioStamp& stamp) override {
        if (!stream_) return -1;
        const auto result = AAudioStream_getTimestamp(stream_, CLOCK_MONOTONIC, &stamp.position, &stamp.time_ns);
        if (result == AAUDIO_OK) return 1;
        return result == AAUDIO_ERROR_INVALID_STATE ? 0 : -1;
    }
    int64_t Position() override { return stream_ ? AAudioStream_getFramesRead(stream_) : -1; }
    int Xruns() override { return stream_ ? AAudioStream_getXRunCount(stream_) : -1; }
    int64_t Now() override {
        timespec value{};
        return clock_gettime(CLOCK_MONOTONIC, &value) == 0 ? value.tv_sec * int64_t{1000000000} + value.tv_nsec : -1;
    }
    void Close() override {
        if (!stream_) return;
        auto* retiring = stream_; stream_ = nullptr;
        // Single worker owns all stream calls; no callback or read can race close.
        AAudioStream_requestStop(retiring);
        AAudioStream_close(retiring);
    }
};
struct OpenStream {
    AAudioStream* value = nullptr;
    ~OpenStream() { if (value) AAudioStream_close(value); }
};
struct Builder {
    AAudioStreamBuilder* value = nullptr;
    ~Builder() { if (value) AAudioStreamBuilder_delete(value); }
};
}
std::unique_ptr<CaptureDevice> OpenAndroidCapture() {
    Builder builder;
    if (AAudio_createStreamBuilder(&builder.value) != AAUDIO_OK || !builder.value) return {};
    AAudioStreamBuilder_setDirection(builder.value, AAUDIO_DIRECTION_INPUT);
    AAudioStreamBuilder_setSampleRate(builder.value, 48000);
    AAudioStreamBuilder_setChannelCount(builder.value, 1);
    AAudioStreamBuilder_setFormat(builder.value, AAUDIO_FORMAT_PCM_FLOAT);
    AAudioStreamBuilder_setSharingMode(builder.value, AAUDIO_SHARING_MODE_SHARED);
    AAudioStreamBuilder_setPerformanceMode(builder.value, AAUDIO_PERFORMANCE_MODE_LOW_LATENCY);
    AAudioStreamBuilder_setInputPreset(builder.value, AAUDIO_INPUT_PRESET_UNPROCESSED);
    AAudioStreamBuilder_setPrivacySensitive(builder.value, true);
    OpenStream acquired;
    if (AAudioStreamBuilder_openStream(builder.value, &acquired.value) != AAUDIO_OK || !acquired.value) return {};
    // The stack guard also covers allocation failure while creating the owner.
    auto* stream = acquired.value;
    auto owner = std::make_unique<AndroidCaptureDevice>(stream);
    acquired.value = nullptr;
    if (AAudioStream_getDirection(stream) != AAUDIO_DIRECTION_INPUT ||
        AAudioStream_getSampleRate(stream) != 48000 || AAudioStream_getChannelCount(stream) != 1 ||
        AAudioStream_getFormat(stream) != AAUDIO_FORMAT_PCM_FLOAT ||
        !AAudioStream_isPrivacySensitive(stream) ||
        AAudioStream_getBufferCapacityInFrames(stream) <= 0 ||
        AAudioStream_getBufferCapacityInFrames(stream) > 24000) return {};
    return owner;
}
}
#else
namespace maestro { std::unique_ptr<CaptureDevice> OpenAndroidCapture() { return {}; } }
#endif
