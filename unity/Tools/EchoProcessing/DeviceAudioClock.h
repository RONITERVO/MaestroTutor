// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#pragma once
#include <cstdint>
namespace maestro {
// Raw platform presentation/recording observation, in this stream's frame units.
// Android sources use CLOCK_MONOTONIC nanoseconds. Retaining a platform timestamp
// is not proof of physical calibration, clock alignment or drift correction.
struct DeviceAudioStamp { int64_t position=0, time_ns=0; };
}
