// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#include "EchoProcessor.h"
#include "api/audio/audio_processing.h"
#include "common_audio/resampler/sinc_resampler.h"
#include "modules/audio_processing/aec3/aec3_common.h"
#include <algorithm>
#include <array>
#include <cmath>
#include <limits>
#include <memory>
#if defined(MAESTRO_ECHO_STATIONARY_EVALUATION) || defined(MAESTRO_ECHO_MEASURED_EVALUATION)
#include "modules/audio_processing/aec3/echo_canceller3.h"
#include <optional>
#include <stdexcept>

// Explicit evaluation profiles only. The stock profile remains the default.
// Measured-delay processing requires a caller-owned, calibrated timing path.
namespace {
class EchoFactory final : public webrtc::EchoControlFactory {
public:
    std::unique_ptr<webrtc::EchoControl> Create(int rate, int render, int capture) override {
        webrtc::EchoCanceller3Config config;
        // At 16 kHz the stock 13-block filter covers only 52 ms. Extend all
        // filter phases together so delayed room reflections fit after alignment.
        config.filter.refined.length_blocks = 32;
        config.filter.coarse.length_blocks = 32;
        config.filter.refined_initial.length_blocks = 32;
        config.filter.coarse_initial.length_blocks = 32;
        // Stationary world audio must not make residual suppression erase quiet
        // near-end speech. Keep AEC's adaptive echo subtraction and path-change
        // handling; do not switch to a raw/linear-output bypass.
        config.echo_audibility.use_stationarity_properties = true;
        config.echo_audibility.use_stationarity_properties_at_init = true;
#if defined(MAESTRO_ECHO_MEASURED_EVALUATION)
        // Keep the 128 ms paths, but adapt the coarse filter at the rate used by
        // upstream's multichannel preset. A measured buffer delay preserves weak
        // early paths and avoids a late correlation-driven reset after recovery.
        config.filter.coarse.rate = .95f;
        config.filter.coarse_initial.rate = .95f;
        config.delay.use_external_delay_estimator = true;
        config.delay.delay_headroom_samples = 64; // One 4 ms block at 16 kHz.
#endif
        if (!webrtc::EchoCanceller3Config::Validate(&config))
            throw std::runtime_error("Invalid pinned echo configuration");
        return std::make_unique<webrtc::EchoCanceller3>(config, std::nullopt, rate, render, capture);
    }
};
}
#endif
struct MaestroEcho {
    int render_rate, render_channels, capture_rate;
    uint64_t epoch, render_sequence = 0, capture_sequence = 0;
    bool faulted = true, has_render = false;
    rtc::scoped_refptr<webrtc::AudioProcessing> apm;
    std::array<std::array<float, 480>, 2> render{};
    std::array<float, 160> processed{};

    MaestroEcho(int rate, int channels, int capture, uint64_t generation)
        : render_rate(rate), render_channels(channels), capture_rate(capture), epoch(generation) {}
    void Clear() noexcept {
        for (auto& channel : render) channel.fill(0);
        processed.fill(0);
    }
    int Fail(int status) noexcept { faulted = true; Clear(); return status; }
    int Initialize() {
        // Retire the old DSP before allocating the new one. On failure there is
        // no usable old state to resume accidentally.
        faulted = true; has_render = false; render_sequence = capture_sequence = 0;
        apm = nullptr; Clear();
        webrtc::AudioProcessing::Config config;
        config.echo_canceller.enabled = true;
        config.echo_canceller.mobile_mode = false;
        config.pipeline.multi_channel_render = render_channels == 2;
        webrtc::AudioProcessingBuilder builder;
        builder.SetConfig(config);
#if defined(MAESTRO_ECHO_STATIONARY_EVALUATION) || defined(MAESTRO_ECHO_MEASURED_EVALUATION)
        builder.SetEchoControlFactory(std::make_unique<EchoFactory>());
#endif
        apm = builder.Create();
        if (!apm) return MAESTRO_ECHO_DSP_ERROR;
        webrtc::ProcessingConfig streams;
        streams.input_stream() = webrtc::StreamConfig(capture_rate, 1);
        streams.output_stream() = webrtc::StreamConfig(16000, 1);
        streams.reverse_input_stream() = webrtc::StreamConfig(render_rate, render_channels);
        streams.reverse_output_stream() = webrtc::StreamConfig(render_rate, render_channels);
        if (apm->Initialize(streams) != 0) return MAESTRO_ECHO_DSP_ERROR;
        faulted = false;
        return MAESTRO_ECHO_OK;
    }
    ~MaestroEcho() { Clear(); }
};
namespace {
constexpr int MinimumDelayMs() {
#if defined(MAESTRO_ECHO_MEASURED_EVALUATION)
    return 4; // External alignment must not subtract headroom below zero.
#else
    return 0;
#endif
}
bool Rate(int rate) { return rate == 16000 || rate == 32000 || rate == 48000; }
bool Audio(const float* input, int count) {
    if (!input) return false;
    for (int i = 0; i < count; ++i)
        if (!std::isfinite(input[i]) || std::abs(input[i]) > 1.0f) return false;
    return true;
}
bool Sequence(uint64_t value, uint64_t expected) {
    return value == expected && value != std::numeric_limits<uint64_t>::max();
}
}
extern "C" {
int maestro_echo_create(int render_rate, int render_channels, int capture_rate,
                        uint64_t epoch, MaestroEcho** result) {
    if (!result) return MAESTRO_ECHO_INVALID;
    // Caller supplies an empty handle; never overwrite a live owner.
    if (*result || !Rate(render_rate) || !Rate(capture_rate) ||
        (render_channels != 1 && render_channels != 2) || epoch == 0) return MAESTRO_ECHO_INVALID;
    try {
        auto owner = std::make_unique<MaestroEcho>(render_rate, render_channels, capture_rate, epoch);
        const int status = owner->Initialize();
        if (status != MAESTRO_ECHO_OK) return status;
        *result = owner.release();
        return MAESTRO_ECHO_OK;
    } catch (...) { return MAESTRO_ECHO_DSP_ERROR; }
}
int maestro_echo_reset(MaestroEcho* owner, uint64_t epoch) {
    if (!owner || epoch <= owner->epoch) return MAESTRO_ECHO_INVALID;
    owner->epoch = epoch;
    try { return owner->Initialize(); }
    catch (...) { return owner->Fail(MAESTRO_ECHO_DSP_ERROR); }
}
int maestro_echo_render(MaestroEcho* owner, uint64_t epoch, uint64_t sequence,
                        const float* input, int sample_count) {
    if (!owner) return MAESTRO_ECHO_INVALID;
    if (owner->faulted) return MAESTRO_ECHO_RESET_REQUIRED;
    if (epoch != owner->epoch || owner->has_render || !Sequence(sequence, owner->render_sequence))
        return owner->Fail(MAESTRO_ECHO_RESET_REQUIRED);
    const int frames = owner->render_rate / 100;
    if (sample_count != frames * owner->render_channels || !Audio(input, sample_count))
        return owner->Fail(MAESTRO_ECHO_INVALID);
    for (int f = 0; f < frames; ++f)
        for (int c = 0; c < owner->render_channels; ++c)
            owner->render[c][f] = input[f * owner->render_channels + c];
    const float* channels[] = {owner->render[0].data(), owner->render[1].data()};
    try {
        const int result = owner->apm->AnalyzeReverseStream(channels,
            webrtc::StreamConfig(owner->render_rate, owner->render_channels));
        owner->Clear();
        if (result != 0) return owner->Fail(MAESTRO_ECHO_DSP_ERROR);
        ++owner->render_sequence; owner->has_render = true;
        return MAESTRO_ECHO_OK;
    } catch (...) { return owner->Fail(MAESTRO_ECHO_DSP_ERROR); }
}
int maestro_echo_capture(MaestroEcho* owner, uint64_t epoch, uint64_t sequence,
                         const float* input, int sample_count, int delay_ms,
                         int16_t* output, int output_count) {
    if (output && output_count == 160) std::fill_n(output, 160, int16_t{0});
    if (!owner) return MAESTRO_ECHO_INVALID;
    if (owner->faulted) return MAESTRO_ECHO_RESET_REQUIRED;
    if (epoch != owner->epoch || !owner->has_render || !Sequence(sequence, owner->capture_sequence))
        return owner->Fail(MAESTRO_ECHO_RESET_REQUIRED);
    if (!output || output_count != 160 || sample_count != owner->capture_rate / 100 ||
        delay_ms < MinimumDelayMs() || delay_ms > 500 || !Audio(input, sample_count))
        return owner->Fail(MAESTRO_ECHO_INVALID);
    try {
        const float* in[] = {input};
        float* out[] = {owner->processed.data()};
        if (owner->apm->set_stream_delay_ms(delay_ms) != 0 ||
            owner->apm->ProcessStream(in, webrtc::StreamConfig(owner->capture_rate, 1),
                webrtc::StreamConfig(16000, 1), out) != 0)
            return owner->Fail(MAESTRO_ECHO_DSP_ERROR);
        for (float sample : owner->processed)
            if (!std::isfinite(sample)) return owner->Fail(MAESTRO_ECHO_DSP_ERROR);
        for (int i = 0; i < 160; ++i)
            output[i] = static_cast<int16_t>(std::lround(std::clamp(owner->processed[i],
                -1.0f, 32767.0f / 32768.0f) * 32768.0f));
        owner->Clear(); owner->has_render = false; ++owner->capture_sequence;
        return MAESTRO_ECHO_OK;
    } catch (...) { return owner->Fail(MAESTRO_ECHO_DSP_ERROR); }
}
int maestro_echo_processing_delay(MaestroEcho* owner, int* samples) {
    if (samples) *samples = 0;
    if (!owner || !samples) return MAESTRO_ECHO_INVALID;
    if (owner->faulted) return MAESTRO_ECHO_RESET_REQUIRED;
    // APM selects min(input, output) = 16 kHz: no split-band/output resampler.
    // AEC3's framer and suppression overlap each delay one 64-sample block.
    // Reserve a full 32-tap input sinc kernel in OUTPUT samples (conservative
    // for 32/48 -> 16 kHz; no input resampling at 16 kHz). See DRAIN_CONTRACT.md.
    static_assert(webrtc::kBlockSize == 64 && webrtc::SincResampler::kKernelSize == 32);
    *samples = 2 * webrtc::kBlockSize + webrtc::SincResampler::kKernelSize;
    return MAESTRO_ECHO_OK;
}
void maestro_echo_destroy(MaestroEcho** owner) {
    if (!owner) return;
    delete *owner; *owner = nullptr;
}
}
