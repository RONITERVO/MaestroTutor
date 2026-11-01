// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#pragma once
#include "CaptureInput.h"
namespace maestro {
// Owned by the same non-real-time worker as the capture device. Healthy must
// latch observed control/enabled-state failures; it may never silently reacquire.
class CaptureEffect {
public:
    virtual ~CaptureEffect() = default;
    virtual bool Healthy() = 0;
    virtual void Close() = 0;
};
class CaptureEffectFactory {
public:
    virtual ~CaptureEffectFactory() = default;
    virtual std::unique_ptr<CaptureEffect> Open(int session_id) = 0;
};
// Both resources transfer here. Retire the microphone BEFORE releasing effects.
// Invalid or unhealthy effects never turn this into an unguarded raw capture.
std::unique_ptr<CaptureDevice> GuardCaptureEffects(std::unique_ptr<CaptureDevice> device,
    std::unique_ptr<CaptureEffect> effect);
// Explicit alternative to OpenAndroidCapture, never an automatic fallback.
// Requests VOICE_COMMUNICATION with a newly allocated, positive AAudio session.
// The supplied factory must own platform AEC; do NOT feed this into software AEC.
std::unique_ptr<CaptureDevice> OpenAndroidPlatformCapture(CaptureEffectFactory& effects);
}
#if defined(__ANDROID__)
#include <jni.h>
namespace maestro {
// Resolve PlatformCaptureEffects.class using the app's class loader first; the
// native worker must not use FindClass with an unrelated native-thread loader.
// No device is opened. The factory retains the supplied class until destruction.
std::unique_ptr<CaptureEffectFactory> MakeAndroidCaptureEffects(JNIEnv* env, jclass effect_class);
}
#endif
