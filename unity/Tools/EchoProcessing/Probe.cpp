// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#include "EchoProcessor.h"
#include <algorithm>
#include <array>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <cstdio>
#include <limits>
#include <fstream>
#include <string>
#include <stdexcept>
#include <vector>
namespace {
int checks = 0;
void Check(bool value, const char* description) {
    ++checks;
    if (!value) throw std::runtime_error(description);
}
struct Owner {
    MaestroEcho* handle = nullptr;
    Owner(int render, int channels, int capture) {
        Check(maestro_echo_create(render, channels, capture, 1, &handle) == 0 && handle, "create");
    }
    ~Owner() { maestro_echo_destroy(&handle); }
    Owner(const Owner&) = delete;
    Owner& operator=(const Owner&) = delete;
};
struct Noise {
    uint32_t state;
    float previous = 0;
    float Next() {
        state ^= state << 13; state ^= state >> 17; state ^= state << 5;
        const float sample = (static_cast<double>(state) / 4294967295.0 - .5) * 2;
        previous = .68f * previous + .32f * sample;
        return previous;
    }
};
constexpr int seconds = 18, base = 48000;
struct Signals {
    std::vector<float> left, right, near;
    Signals(bool noise_near = false) : left(seconds * base), right(seconds * base), near(seconds * base) {
        Noise a{0x32455243}, b{0x94376521}, c{0x27182818};
        for (int i = 0; i < seconds * base; ++i) {
            const double t = static_cast<double>(i) / base;
            // Independent, reproducible broadband sources with speech-like
            // amplitude envelopes. No recordings, speakers, or microphones.
            const double envelope = .65 + .35 * std::sin(2 * 3.141592653589793 * 2.7 * t);
            left[i] = static_cast<float>(.6 * envelope * a.Next());
            right[i] = static_cast<float>(.55 * envelope * b.Next());
            const double phase = 2 * 3.141592653589793 * (173 * t + .8 * std::sin(2.3 * t));
            const double voiced = .6 * std::sin(phase) + .28 * std::sin(2 * phase)
                + .15 * std::sin(4 * phase) + .08 * std::sin(7 * phase);
            near[i] = noise_near ? static_cast<float>(.33 * (.75 + .25 * std::sin(17 * t)) * c.Next()) : static_cast<float>(.28 * (.75 + .25 * std::sin(17 * t)) * (voiced + .15 * c.Next()));
        }
    }
    float At(const std::vector<float>& v, int index) const { return index < 0 ? 0 : v[index]; }
    float Echo(int index, int channels) const {
        // 80 ms speaker-buffer delay and two later reflected paths.
        const float first = .55f * At(left, index - 3840) + .18f * At(left, index - 5280);
        return first + (channels == 2 ? .35f * At(right, index - 4320) : .1f * At(left, index - 6240));
    }
};
double Energy(const std::vector<float>& values, int first, int last) {
    double sum = 0; for (int i = first; i < last; ++i) sum += values[i] * values[i];
    return sum / (last - first);
}
struct Similarity { double correlation = 0, gain = 0; int lag = 0; };
Similarity Compare(const std::vector<float>& out, const std::vector<float>& expected, int first, int last) {
    Similarity best;
    // APM includes filtering/resampling delay; do not confuse that with muting.
    for (int lag = -320; lag <= 320; ++lag) {
        double cross = 0, a = 0, b = 0;
        for (int i = first; i < last; ++i) {
            const double x = out[i], y = expected[i - lag];
            cross += x * y; a += x * x; b += y * y;
        }
        const double corr = cross / std::sqrt(std::max(a * b, 1e-30));
        if (corr > best.correlation) best = {corr, cross / std::max(b, 1e-30), lag};
    }
    return best;
}
void LoadSpeech(Signals& signals, const char* path) {
    std::ifstream file(path, std::ios::binary | std::ios::ate);
    if (!file || file.tellg() < 44 || file.tellg() > 16000 * 2 * 60)
        throw std::runtime_error("expected a bounded PCM16 mono 16 kHz RIFF WAV");
    const auto length = static_cast<size_t>(file.tellg());
    std::vector<uint8_t> bytes(length); file.seekg(0);
    file.read(reinterpret_cast<char*>(bytes.data()), static_cast<std::streamsize>(length));
    if (!file || std::memcmp(bytes.data(),"RIFF",4) || std::memcmp(bytes.data()+8,"WAVE",4))
        throw std::runtime_error("invalid RIFF WAV");
    auto u16 = [&](size_t at) {return uint32_t(bytes.at(at)) | uint32_t(bytes.at(at+1)) << 8;};
    auto u32 = [&](size_t at) {return u16(at) | u16(at+2) << 16;};
    bool format = false; size_t start = 0, count = 0;
    for (size_t at = 12; at + 8 <= length;) {
        const auto size = u32(at+4);
        if (size > length - at - 8) throw std::runtime_error("truncated WAV chunk");
        if (!std::memcmp(bytes.data()+at,"fmt ",4)) {
            if (size < 16 || u16(at+8) != 1 || u16(at+10) != 1 || u32(at+12) != 16000 ||
                u32(at+16) != 32000 || u16(at+20) != 2 || u16(at+22) != 16)
                throw std::runtime_error("WAV must be PCM16 mono 16 kHz");
            format = true;
        }
        if (!std::memcmp(bytes.data()+at,"data",4)) {start=at+8;count=size/2;}
        at += 8 + size + (size & 1);
    }
    if (!format || count < 16000) throw std::runtime_error("missing speech samples");
    double energy = 0; float maximum = 0;
    std::vector<float> speech(count);
    for (size_t i = 0; i < count; ++i) {
        const auto value = u16(start+2*i);
        speech[i] = (value >= 32768 ? static_cast<int>(value)-65536 : static_cast<int>(value)) / 32768.f;
        maximum = std::max(maximum,std::abs(speech[i])); energy += speech[i]*speech[i];
    }
    if (energy/count < 1e-5 || maximum <= 0) throw std::runtime_error("speech WAV is silent");
    // Keep speech at its recorded level, capped only to avoid synthetic mixing
    // clipping; do not normalize quiet phonemes into an easy test signal.
    const float gain = std::min(1.f,.65f/maximum);
    for (size_t i = 0; i < signals.near.size(); ++i) {
        const auto sample = (i / 3) % count, next = (sample+1) % count;
        const float fraction = static_cast<float>(i%3)/3.f;
        signals.near[i] = gain*(speech[sample]*(1-fraction)+speech[next]*fraction);
    }
}
void AudioCase(const Signals& signals, const char* name, int render_rate,
               int channels, int capture_rate, bool echo, bool double_talk, bool reset) {
    Owner owner(render_rate, channels, capture_rate);
    std::vector<float> output(seconds * 16000), expected(seconds * 16000), raw(seconds * 16000);
    std::vector<float> render(render_rate / 100 * channels), capture(capture_rate / 100);
    std::array<int16_t, 160> pcm{};
    uint64_t epoch = 1, sequence = 0;
    for (int frame = 0; frame < seconds * 100; ++frame) {
        if (reset && frame == 800) {
            // Model an explicit gap and a new paired-clock epoch.
            Check(maestro_echo_render(owner.handle, epoch, sequence + 1, render.data(),
                static_cast<int>(render.size())) == MAESTRO_ECHO_RESET_REQUIRED, "gap rejected");
            Check(maestro_echo_reset(owner.handle, ++epoch) == 0, "reset DSP"); sequence = 0;
        }
        for (int j = 0; j < render_rate / 100; ++j) {
            const int source = frame * 480 + j * base / render_rate;
            render[j * channels] = echo ? signals.left[source] : 0;
            if (channels == 2) render[j * channels + 1] = echo ? signals.right[source] : 0;
        }
        const bool near_active = !echo || (double_talk && frame >= 1000);
        for (int j = 0; j < capture_rate / 100; ++j) {
            const int source = frame * 480 + j * base / capture_rate;
            capture[j] = (echo ? signals.Echo(source, channels) : 0) + (near_active ? signals.near[source] : 0);
        }
        const auto unchanged_render = render;
        Check(maestro_echo_render(owner.handle, epoch, sequence, render.data(), static_cast<int>(render.size())) == 0, "render");
        Check(render == unchanged_render, "reference must not alter playback samples");
        Check(maestro_echo_capture(owner.handle, epoch, sequence, capture.data(), static_cast<int>(capture.size()),
            80, pcm.data(), 160) == 0, "capture");
        for (int j = 0; j < 160; ++j) {
            const int source = frame * 480 + j * 3, dest = frame * 160 + j;
            output[dest] = pcm[j] / 32768.0f;
            expected[dest] = near_active ? signals.near[source] : 0;
            raw[dest] = (echo ? signals.Echo(source, channels) : 0) + expected[dest];
        }
        ++sequence;
    }
    const int first = (double_talk ? 6 : 14) * 16000, last = (double_talk ? 9 : 17) * 16000;
    const double reduction = 10 * std::log10(std::max(Energy(raw, first, last), 1e-20) /
        std::max(Energy(output, first, last), 1e-20));
    const auto near = Compare(output, expected, 14 * 16000, 17 * 16000);
    std::printf("{\"case\":\"%s\",\"echoReductionDb\":%.4f,\"nearCorrelation\":%.4f,\"nearGain\":%.4f,\"lagSamples\":%d}\n",
        name, reduction, near.correlation, near.gain, near.lag);
    if (echo) Check(reduction >= 15, "echo reduction below fixed 15 dB acceptance");
    if (!echo || double_talk) {
        Check(near.correlation >= .65, "near-end correlation below fixed .65 acceptance");
        Check(near.gain >= .45 && near.gain <= 1.5, "near-end missing or amplified");
    }
}
void Contracts() {
    MaestroEcho* empty = nullptr;
    Check(maestro_echo_create(44100, 1, 16000, 1, &empty) == MAESTRO_ECHO_INVALID && !empty, "unsupported rate");
    Check(maestro_echo_create(16000, 3, 16000, 1, &empty) == MAESTRO_ECHO_INVALID && !empty, "unsupported channels");
    Check(maestro_echo_create(16000, 1, 16000, 0, &empty) == MAESTRO_ECHO_INVALID && !empty, "zero epoch");
    Check(maestro_echo_create(16000, 1, 16000, 1, nullptr) == MAESTRO_ECHO_INVALID, "null handle target");
    std::array<float,160> input{}; std::array<int16_t,160> output{};
    Owner owner(16000, 1, 16000); auto* original = owner.handle;
    Check(maestro_echo_create(16000, 1, 16000, 1, &owner.handle) == MAESTRO_ECHO_INVALID && owner.handle == original, "do not replace live handle");
    auto cap = [&](uint64_t epoch, uint64_t seq, int delay = 0) {
        output.fill(1234);
        const int status = maestro_echo_capture(owner.handle, epoch, seq, input.data(), 160, delay, output.data(), 160);
        if (status) Check(std::all_of(output.begin(),output.end(),[](int16_t x){return x == 0;}), "failure output cleared");
        return status;
    };
    auto ren = [&](uint64_t epoch, uint64_t seq) {return maestro_echo_render(owner.handle, epoch, seq, input.data(), 160);};
    Check(cap(1,0) == MAESTRO_ECHO_RESET_REQUIRED, "capture without reference");
    Check(ren(1,0) == MAESTRO_ECHO_RESET_REQUIRED, "fault latched");
    Check(maestro_echo_reset(owner.handle, 1) == MAESTRO_ECHO_INVALID, "epoch cannot repeat");
    Check(maestro_echo_reset(owner.handle, 2) == 0 && ren(2,0) == 0 && cap(2,0) == 0, "reset resumes");
    Check(cap(2,1) == MAESTRO_ECHO_RESET_REQUIRED, "stale reference cannot be reused");
    Check(maestro_echo_reset(owner.handle, 3) == 0 && ren(3,0) == 0, "new epoch");
    Check(ren(3,1) == MAESTRO_ECHO_RESET_REQUIRED, "unpaired extra render rejected");
    Check(maestro_echo_reset(owner.handle, 4) == 0, "reset for invalid sample");
    input[0] = std::numeric_limits<float>::quiet_NaN();
    Check(ren(4,0) == MAESTRO_ECHO_INVALID && cap(4,0) == MAESTRO_ECHO_RESET_REQUIRED, "nonfinite faults");
    input[0] = 0;
    Check(maestro_echo_reset(owner.handle, 5) == 0 && ren(5,0) == 0 && cap(4,0) == MAESTRO_ECHO_RESET_REQUIRED, "old capture epoch");
    Check(maestro_echo_reset(owner.handle, 6) == 0 && ren(6,0) == 0 && cap(6,1) == MAESTRO_ECHO_RESET_REQUIRED, "capture gap");
    Check(maestro_echo_reset(owner.handle, 7) == 0 && ren(7,0) == 0 && cap(7,0,501) == MAESTRO_ECHO_INVALID, "invalid delay");
    Check(maestro_echo_reset(owner.handle, 8) == 0, "reset for malformed frame");
    Check(maestro_echo_render(owner.handle,8,0,input.data(),159) == MAESTRO_ECHO_INVALID, "short render");
    Check(maestro_echo_reset(owner.handle, 9) == 0 && ren(9,0) == 0, "reset for invalid capture");
    input[0] = std::numeric_limits<float>::infinity();
    Check(cap(9,0) == MAESTRO_ECHO_INVALID, "infinite microphone input"); input[0] = 0;
    Check(maestro_echo_reset(owner.handle, 10) == 0 && ren(10,0) == 0, "reset for short capture");
    output.fill(1234);
    Check(maestro_echo_capture(owner.handle,10,0,input.data(),159,0,output.data(),160) == MAESTRO_ECHO_INVALID,
        "short capture rejected");
    Check(std::all_of(output.begin(),output.end(),[](int16_t x){return x==0;}), "malformed capture cannot return stale output");
    Check(maestro_echo_reset(owner.handle, 11) == 0 && ren(11,0) == 0, "reset for output boundary");
    output.fill(1234);
    Check(maestro_echo_capture(owner.handle,11,0,input.data(),160,0,output.data(),159) == MAESTRO_ECHO_INVALID,
        "short output rejected");
    Check(std::all_of(output.begin(),output.end(),[](int16_t x){return x==1234;}), "do not write wrong-sized output");
    Check(maestro_echo_reset(owner.handle, 12) == 0, "reset for out-of-range render");
    input[0] = 1.01f; Check(ren(12,0) == MAESTRO_ECHO_INVALID, "do not silently clip reference"); input[0] = 0;
    Check(maestro_echo_reset(owner.handle, 13) == 0, "reset for null input");
    Check(maestro_echo_render(owner.handle,13,0,nullptr,160) == MAESTRO_ECHO_INVALID, "null render");
    Check(maestro_echo_reset(owner.handle, 14) == 0, "reset for old render epoch");
    Check(ren(13,0) == MAESTRO_ECHO_RESET_REQUIRED, "old render epoch");
    Check(maestro_echo_reset(owner.handle, 15) == 0, "reset for invalid counter");
    Check(ren(15,std::numeric_limits<uint64_t>::max()) == MAESTRO_ECHO_RESET_REQUIRED, "sequence cannot wrap");
    maestro_echo_destroy(&owner.handle); maestro_echo_destroy(&owner.handle); maestro_echo_destroy(nullptr);
    Check(!owner.handle, "destroy nulls handle");
    Check(cap(9,0) == MAESTRO_ECHO_INVALID, "closed handle");
    for (int i = 0; i < 32; ++i) { Owner next(48000,2,48000); }
    std::printf("{\"case\":\"ownership-validation-and-gap-recovery\",\"status\":\"passed\",\"repeatedOwners\":32}\n");
}
}
int main(int argc, char** argv) {
    try {
        if (argc == 2 && std::strcmp(argv[1], "--noise-stress") == 0) {
            const Signals noisy(true);
            AudioCase(noisy,"noise-double-talk16",16000,1,16000,true,true,false);
            std::printf("{\"status\":\"passed\",\"scope\":\"noise-stress-only\"}\n");
            return 0;
        }
        if (argc == 3 && std::strcmp(argv[1], "--speech-wav") == 0) {
            Signals speech; LoadSpeech(speech,argv[2]);
            AudioCase(speech,"speech-only16",16000,1,16000,false,false,false);
            AudioCase(speech,"speech-double-talk16",16000,1,16000,true,true,false);
            AudioCase(speech,"speech-double-talk48",48000,2,48000,true,true,false);
            std::printf("{\"status\":\"passed\",\"scope\":\"supplied-speech-only\"}\n");
            return 0;
        }
        if (argc != 1) throw std::runtime_error("unknown probe arguments");
        Contracts();
        const Signals signals;
        AudioCase(signals,"mono16-echo",16000,1,16000,true,false,false);
        AudioCase(signals,"mono32-echo",32000,1,32000,true,false,false);
        AudioCase(signals,"stereo48-echo",48000,2,48000,true,false,false);
        AudioCase(signals,"mixed48-16-echo",48000,2,16000,true,false,false);
        AudioCase(signals,"near-only16",16000,1,16000,false,false,false);
        AudioCase(signals,"double-talk16",16000,1,16000,true,true,false);
        AudioCase(signals,"double-talk48",48000,2,48000,true,true,false);
        AudioCase(signals,"gap-reset48",48000,2,48000,true,false,true);
        std::printf("{\"status\":\"passed\",\"audioCases\":8,\"checks\":%d,\"providerUsed\":false,\"physicalAudio\":false}\n", checks);
        return 0;
    } catch (const std::exception& error) {
        std::fprintf(stderr,"FAILED: %s\n", error.what()); return 1;
    }
}
