// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
package com.maestro.quest.audio;

import java.util.concurrent.atomic.AtomicReference;

/** One prepared capture session. Does not acquire a microphone or change audio mode.
 * Calls are serialized by its worker; callbacks only latch invalidation.
 * Close capture first, then close this lease. Never reuse a faulted lease.
 */
public final class SessionEffects implements AutoCloseable {
    public enum Kind { ECHO, NOISE, GAIN }
    public enum Fault { NONE, INVALID_SESSION, UNAVAILABLE, CREATE, CONTROL, ENABLE, QUERY, CHANGED, CLOSED }
    public interface Change { void changed(boolean value); }
    public interface Effect {
        boolean hasControl();
        boolean isEnabled();
        boolean setEnabled(boolean enabled);
        void watch(Change control, Change enabled);
        void release();
    }
    public interface Backend {
        boolean available(Kind kind);
        Effect create(Kind kind, int session);
    }
    private final int session;
    private final Effect[] effects = new Effect[Kind.values().length];
    private final AtomicReference<Fault> invalid = new AtomicReference<>(Fault.NONE);
    private boolean ready, closed;
    private int queriedMask, availableMask, attachedMask, initiallyEnabledMask;

    private SessionEffects(int session) { this.session = session; }
    public static SessionEffects open(Backend backend, int session) {
        SessionEffects owner = new SessionEffects(session);
        owner.prepare(backend);
        return owner;
    }
    private void fail(Fault fault) { invalid.compareAndSet(Fault.NONE, fault); }
    private boolean wanted(int index) { return index == Kind.ECHO.ordinal(); }
    private void prepare(Backend backend) {
        if (session <= 0 || backend == null) { fail(Fault.INVALID_SESSION); release(); return; }
        try {
            for (Kind kind : Kind.values()) {
                int index = kind.ordinal();
                boolean available = backend.available(kind);
                queriedMask |= 1 << index;
                if (!available) {
                    if (kind == Kind.ECHO) { fail(Fault.UNAVAILABLE); release(); return; }
                    continue;
                }
                availableMask |= 1 << index;
                Effect effect = backend.create(kind, session);
                if (effect == null) { fail(Fault.CREATE); release(); return; }
                effects[index] = effect;
                attachedMask |= 1 << index;
                if (!effect.hasControl()) { fail(Fault.CONTROL); release(); return; }
                if (effect.isEnabled()) initiallyEnabledMask |= 1 << index;
                if (!effect.setEnabled(wanted(index))) { fail(Fault.ENABLE); release(); return; }
                effect.watch(value -> { if (!value) fail(Fault.CONTROL); },
                    value -> { if (value != wanted(index)) fail(Fault.CHANGED); });
                if (!effect.hasControl() || effect.isEnabled() != wanted(index)) {
                    fail(Fault.CHANGED); release(); return;
                }
            }
            ready = true;
            if (!isHealthy()) release();
        } catch (RuntimeException error) { fail(Fault.QUERY); release(); }
    }
    public synchronized boolean isHealthy() {
        if (!ready || closed || invalid.get() != Fault.NONE) return false;
        try {
            for (int i = 0; i < effects.length; ++i) {
                Effect effect = effects[i];
                if (effect == null) continue;
                if (!effect.hasControl()) fail(Fault.CONTROL);
                if (effect.isEnabled() != wanted(i)) fail(Fault.CHANGED);
            }
        } catch (RuntimeException error) { fail(Fault.QUERY); }
        return invalid.get() == Fault.NONE;
    }
    public int sessionId() { return session; }
    public int queriedMask() { return queriedMask; }
    public int availableMask() { return availableMask; }
    public int attachedMask() { return attachedMask; }
    public int initiallyEnabledMask() { return initiallyEnabledMask; }
    public Fault fault() { return invalid.get(); }
    private void release() {
        closed = true; ready = false;
        // Continue releasing every handle even if one platform release fails.
        for (int i = effects.length - 1; i >= 0; --i) {
            Effect effect = effects[i]; effects[i] = null;
            if (effect != null) {
                try { effect.release(); } catch (RuntimeException error) { fail(Fault.QUERY); }
            }
        }
    }
    @Override public synchronized void close() {
        if (closed) return;
        fail(Fault.CLOSED); release();
    }
}
