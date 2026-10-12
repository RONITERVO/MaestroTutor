// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#include "CaptureEffects.h"
#include <algorithm>
#include <array>
#include <cstdio>
#include <functional>
#include <stdexcept>
#include <vector>
using namespace maestro;
namespace {
int cases = 0, assertions = 0;
void Check(bool ok, const char* message) { ++assertions; if (!ok) throw std::runtime_error(message); }
void Done(const char* name) { ++cases; std::printf("{\"case\":\"%s\",\"status\":\"passed\"}\n", name); }
struct State {
    bool healthy = true, device_closed = false, effect_closed = false, start_ok = true;
    int starts = 0, reads = 0, count = 480, ready = 1, timestamp = 1, effect_queries = 0;
    int64_t position = 0;
    std::vector<int> order;
    std::function<void()> on_read, on_start, on_ready, on_stamp;
};
class Device final : public CaptureDevice {
    std::shared_ptr<State> s;
public:
    explicit Device(std::shared_ptr<State> state) : s(state) {}
    ~Device() override { Close(); }
    bool Start() override { ++s->starts; if (s->on_start) s->on_start(); return s->start_ok; }
    int Ready() override { if (s->on_ready) s->on_ready(); return s->ready; }
    int Read(float* pcm, int capacity) override {
        ++s->reads; const int count = std::min(capacity, s->count);
        if (count >= 0) { std::fill_n(pcm, count, .25f); s->position += count; }
        if (s->on_read) s->on_read();
        return count;
    }
    int Timestamp(DeviceAudioStamp& stamp) override {
        stamp = {480,990000000}; if (s->on_stamp) s->on_stamp(); return s->timestamp;
    }
    int64_t Position() override { return s->position; }
    int Xruns() override { return 0; }
    int64_t Now() override { return 1000000000; }
    void Close() override { if (!s->device_closed) { s->device_closed = true; s->order.push_back(1); } }
};
class Effect final : public CaptureEffect {
    std::shared_ptr<State> s;
public:
    explicit Effect(std::shared_ptr<State> state) : s(state) {}
    ~Effect() override { Close(); }
    bool Healthy() override { ++s->effect_queries; return s->healthy && !s->effect_closed; }
    void Close() override { if (!s->effect_closed) { s->effect_closed = true; s->order.push_back(2); } }
};
struct Fixture {
    std::shared_ptr<State> s = std::make_shared<State>();
    std::unique_ptr<CaptureDevice> Open() {
        return GuardCaptureEffects(std::make_unique<Device>(s), std::make_unique<Effect>(s));
    }
    void Closed() { Check(s->order == std::vector<int>({1,2}), "device must retire before effects, exactly once"); }
};
}
int main() { try {
    {Fixture f; auto device = f.Open(); Check(device != nullptr && f.s->starts == 0, "prepare never records");
    Check(device->Start() && !device->Start(), "start once"); device->Close(); device->Close(); device.reset(); f.Closed();} Done("prepare-start-close");
    {Fixture f; f.s->healthy = false; Check(!f.Open(), "reject disabled or uncontrolled AEC"); f.Closed();} Done("unhealthy-acquisition");
    {Fixture f; Check(!GuardCaptureEffects(std::make_unique<Device>(f.s), {}), "null effect is not raw fallback"); Check(f.s->device_closed, "close input");} Done("missing-effect");
    {Fixture f; Check(!GuardCaptureEffects({}, std::make_unique<Effect>(f.s)), "missing device"); Check(f.s->effect_closed, "close effect");} Done("missing-device");
    {Fixture f; auto device = f.Open(); f.s->healthy = false; Check(!device->Start() && f.s->starts == 0, "loss before start"); f.Closed();} Done("loss-before-start");
    {Fixture f; auto device = f.Open(); f.s->on_start = [&]{f.s->healthy = false;}; Check(!device->Start(), "loss during start"); f.Closed();} Done("loss-during-start");
    {Fixture f; auto device = f.Open(); f.s->start_ok = false; Check(!device->Start(), "failed start"); f.Closed();} Done("start-failure");
    {Fixture f; auto device = f.Open(); f.s->ready = 0; Check(device->Ready() == 0, "starting may wait"); f.s->ready = -1; Check(device->Ready() < 0, "disconnected"); f.Closed();} Done("readiness-failure");
    {Fixture f; auto device = f.Open(); f.s->on_ready = [&]{f.s->healthy = false;}; Check(device->Ready() < 0, "loss during ready"); f.Closed();} Done("loss-during-ready");
    {Fixture f; auto device = f.Open(); DeviceAudioStamp stamp; Check(device->Timestamp(stamp) == 1 && stamp.time_ns == 990000000, "preserve hardware time");
    f.s->on_stamp = [&]{f.s->healthy = false;}; Check(device->Timestamp(stamp) < 0 && stamp.time_ns == 0, "no stale stamp on loss"); f.Closed();} Done("loss-during-timestamp");
    {Fixture f; auto device = f.Open(); std::array<float,480> pcm; Check(device->Read(pcm.data(),480) < 0 && f.s->reads == 0, "no read before start");
    Check(device->Start(), "start"); f.s->count = 173; Check(device->Read(pcm.data(),480) == 173, "actual count");
    Check(std::all_of(pcm.begin(),pcm.begin()+173,[](float x){return x == .25f;}), "preserve PCM");
    Check(std::all_of(pcm.begin()+173,pcm.end(),[](float x){return x == 0;}), "clear suffix");
    Check(device->Position() == 173 && device->Xruns() == 0, "preserve device position");} Done("exact-partial-pcm");
    {Fixture f; auto device = f.Open(); Check(device->Start(), "start"); std::array<float,480> pcm; pcm.fill(.9f); f.s->healthy = false;
    Check(device->Read(pcm.data(),480) < 0 && f.s->reads == 0, "no hardware read after loss");
    Check(std::all_of(pcm.begin(),pcm.end(),[](float x){return x == 0;}), "no stale PCM"); f.s->healthy = true;
    Check(device->Read(pcm.data(),480) < 0 && !device->Start(), "cannot recover by toggling availability"); f.Closed();} Done("loss-before-read-latched");
    {Fixture f; auto device = f.Open(); Check(device->Start(), "start"); std::array<float,480> pcm;
    f.s->on_read = [&]{f.s->healthy = false;}; Check(device->Read(pcm.data(),480) < 0, "loss inside read");
    Check(std::all_of(pcm.begin(),pcm.end(),[](float x){return x == 0;}), "discard affected read"); f.Closed();} Done("loss-during-read");
    {Fixture f; auto device = f.Open(); Check(device->Start(), "start"); f.s->count = -1; std::array<float,480> pcm;
    Check(device->Read(pcm.data(),480) < 0, "read failure"); f.Closed();} Done("device-read-failure");
    {Fixture f; CaptureInput input(f.Open(),19); Check(input.Start(), "start"); std::array<float,480> pcm; CaptureFrame frame;
    Check(input.Read(pcm.data(),480,frame) == CaptureRead::Frame && frame.epoch == 19 && frame.samples == 480, "same input contract");
    f.s->healthy = false; Check(input.Read(pcm.data(),480,frame) == CaptureRead::Faulted, "input latches platform failure");
    Check(frame.epoch == 0 && std::all_of(pcm.begin(),pcm.end(),[](float x){return x == 0;}), "no raw input fallback"); f.Closed();} Done("capture-input-integration");
    {Fixture f; CaptureInput input(f.Open(),19); Check(input.Start(), "start"); f.s->count = 47; std::array<float,480> pcm; CaptureFrame frame;
    Check(input.Read(pcm.data(),480,frame) == CaptureRead::Waiting, "retain fragment"); f.s->healthy = false;
    Check(input.FlushPending(pcm.data(),480,frame) == CaptureRead::Faulted, "discard pending fragment after effect loss");
    Check(frame.samples == 0 && f.s->reads == 1, "flush cannot read more or return stale prefix"); f.Closed();} Done("flush-after-effect-loss");
    {Fixture old, fresh; auto a = old.Open(), b = fresh.Open(); Check(a->Start() && b->Start(), "independent modeled owners");
    old.s->healthy = false; Check(a->Ready() < 0 && b->Ready() == 1, "old effect cannot retire new session"); old.Closed(); b->Close(); fresh.Closed();} Done("session-isolation");
    std::printf("{\"status\":\"passed\",\"effectsCases\":%d,\"assertions\":%d,\"deviceOpened\":false,\"physicalAudio\":false}\n",cases,assertions);
    return 0;
} catch (const std::exception& e) { std::fprintf(stderr,"effects probe failed: %s\n",e.what()); return 1; } }
