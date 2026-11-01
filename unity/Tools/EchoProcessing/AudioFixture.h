// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#pragma once
#include <array>
#include <cmath>
#include <vector>
namespace maestro_fixture {
// The acoustic source must be band-limited before simulating a 16 kHz ADC.
// Point-decimating full-band 48 kHz noise invents aliased microphone content that
// is absent from APM's properly downsampled render reference and cannot cancel.
inline void BandLimit(std::vector<float>& values) {
    constexpr int taps = 161, half = taps / 2;
    constexpr double pi = 3.141592653589793, cutoff = 6000.0 / 48000;
    std::array<double,taps> kernel{}; double total = 0;
    for (int k = 0; k < taps; ++k) {
        const int offset = k-half;
        const double sinc = offset ? std::sin(2*pi*cutoff*offset)/(pi*offset) : 2*cutoff;
        kernel[k] = sinc*(.54-.46*std::cos(2*pi*k/(taps-1))); total += kernel[k];
    }
    for (auto& k : kernel) k /= total;
    auto input = values;
    for (size_t i = 0; i < values.size(); ++i) {
        double sum = 0;
        for (int k = 0; k < taps && static_cast<size_t>(k) <= i; ++k) sum += kernel[k]*input[i-k];
        values[i] = static_cast<float>(sum);
    }
}
}
