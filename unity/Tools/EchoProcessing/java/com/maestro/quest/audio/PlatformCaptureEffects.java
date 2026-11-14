// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.audio;

import android.media.audiofx.AcousticEchoCanceler;
import android.media.audiofx.AudioEffect;
import android.media.audiofx.AutomaticGainControl;
import android.media.audiofx.NoiseSuppressor;

/** JNI entry point for the isolated AAudio factory; not packaged in the app. */
public final class PlatformCaptureEffects implements AutoCloseable {
    private final SessionEffects lease;
    private PlatformCaptureEffects(int session) { lease = SessionEffects.open(new Backend(), session); }
    public static PlatformCaptureEffects open(int session) { return new PlatformCaptureEffects(session); }
    public boolean isHealthy() { return lease.isHealthy(); }
    public SessionEffects diagnostics() { return lease; }
    @Override public void close() { lease.close(); }

    private static final class Backend implements SessionEffects.Backend {
        @Override public boolean available(SessionEffects.Kind kind) {
            switch (kind) {
                case ECHO: return AcousticEchoCanceler.isAvailable();
                case NOISE: return NoiseSuppressor.isAvailable();
                case GAIN: return AutomaticGainControl.isAvailable();
                default: throw new IllegalArgumentException("Unknown capture effect");
            }
        }
        @Override public SessionEffects.Effect create(SessionEffects.Kind kind, int session) {
            AudioEffect effect;
            switch (kind) {
                case ECHO: effect = AcousticEchoCanceler.create(session); break;
                case NOISE: effect = NoiseSuppressor.create(session); break;
                case GAIN: effect = AutomaticGainControl.create(session); break;
                default: throw new IllegalArgumentException("Unknown capture effect");
            }
            return effect == null ? null : new OwnedEffect(effect);
        }
    }
    private static final class OwnedEffect implements SessionEffects.Effect {
        private final AudioEffect effect;
        private OwnedEffect(AudioEffect effect) { this.effect = effect; }
        @Override public boolean hasControl() { return effect.hasControl(); }
        @Override public boolean isEnabled() { return effect.getEnabled(); }
        @Override public boolean setEnabled(boolean enabled) { return effect.setEnabled(enabled) == AudioEffect.SUCCESS; }
        @Override public void watch(SessionEffects.Change control, SessionEffects.Change enabled) {
            effect.setControlStatusListener((source, value) -> control.changed(value));
            effect.setEnableStatusListener((source, value) -> enabled.changed(value));
        }
        @Override public void release() {
            // AudioEffect.release removes native event ownership. Late callbacks
            // can only invalidate their old lease, never a new capture session.
            effect.release();
        }
    }
}
