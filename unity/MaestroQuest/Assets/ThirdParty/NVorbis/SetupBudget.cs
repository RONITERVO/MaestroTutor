// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: MIT
namespace NVorbis {
    // Per decoder, never shared across imports. Conservative reservation units
    // bound setup tables, not an assertion of exact managed heap consumption.
    sealed class SetupBudget {
        long remaining = 16 * 1024 * 1024;
        internal void Reserve(long bytes) {
            if (bytes < 0 || bytes > remaining)
                throw new System.IO.InvalidDataException("Vorbis setup exceeds the supported import complexity.");
            remaining -= bytes;
        }
    }
}
