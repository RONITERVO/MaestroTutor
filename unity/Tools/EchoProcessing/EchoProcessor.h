// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#pragma once
#include <stdint.h>
#if defined(_WIN32)
# if defined(MAESTRO_ECHO_BUILD)
#  define MAESTRO_ECHO_API __declspec(dllexport)
# else
#  define MAESTRO_ECHO_API __declspec(dllimport)
# endif
#else
# define MAESTRO_ECHO_API __attribute__((visibility("default")))
#endif
#ifdef __cplusplus
extern "C" {
#endif
// Experimental, off-device evaluation ABI. One owner must serialize ALL calls,
// including destruction. Never call from Unity's render callback. Reset/create
// allocate; this API makes no hard real-time or microphone ownership claim.
typedef struct MaestroEcho MaestroEcho;
enum MaestroEchoStatus {
    MAESTRO_ECHO_OK = 0,
    MAESTRO_ECHO_INVALID = 1,
    MAESTRO_ECHO_RESET_REQUIRED = 2,
    MAESTRO_ECHO_DSP_ERROR = 3
};
// Rates: 16000, 32000, 48000. Capture: mono; render: 1 or 2 channels.
// Epoch must be nonzero. Frame sequences begin at zero independently after reset.
MAESTRO_ECHO_API int maestro_echo_create(int render_rate, int render_channels,
    int capture_rate, uint64_t epoch, MaestroEcho** result);
// New epoch must increase. Replaces DSP history, including any faulted history.
MAESTRO_ECHO_API int maestro_echo_reset(MaestroEcho* owner, uint64_t epoch);
// Feed one render frame, then one capture frame; caller aligns their clocks.
// A second render or capture without its partner requires reset.
// Interleaved normalized float render, exactly 10 ms. Does not alter playback.
MAESTRO_ECHO_API int maestro_echo_render(MaestroEcho* owner, uint64_t epoch,
    uint64_t sequence, const float* input, int sample_count);
// Mono normalized float capture, exactly 10 ms; outputs exactly 160 PCM16 samples.
// delay_ms is APM's render/capture hardware-buffer delay estimate, NOT a clock
// alignment implementation. Caller must supply it from its actual capture path.
// The opt-in measured32 evaluation requires delay_ms in [4, 500] and a calibrated
// timing source; it does not estimate hardware delay from audio. Other profiles
// accept [0, 500]. Range validation does not establish calibration or clock sync.
// Input and output buffers must not overlap.
// On failure, a valid 160-sample output buffer is cleared, never stale/raw PCM.
// Missing frames, invalid audio, and DSP errors fault the owner until reset.
MAESTRO_ECHO_API int maestro_echo_capture(MaestroEcho* owner, uint64_t epoch,
    uint64_t sequence, const float* input, int sample_count, int delay_ms,
    int16_t* output, int output_count);
// Conservative fixed processing-delay bound in 16 kHz output samples for this
// pinned backend. Excludes device queues, alignment resampling and IIR decay;
// not an end-of-stream tail length. Does not consume audio or reset DSP history.
// Clears a valid result on failure. A faulted owner requires reset.
MAESTRO_ECHO_API int maestro_echo_processing_delay(MaestroEcho* owner, int* samples);
// Clears retained scratch, releases DSP, nulls caller's handle. Null is allowed.
// Copies of the old handle are invalid; caller must fence queued work first.
MAESTRO_ECHO_API void maestro_echo_destroy(MaestroEcho** owner);
#ifdef __cplusplus
}
#endif
