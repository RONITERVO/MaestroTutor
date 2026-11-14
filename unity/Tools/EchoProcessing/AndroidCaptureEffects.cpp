// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#include "CaptureEffects.h"
#if defined(__ANDROID__)
namespace maestro {
namespace {
class Environment {
    JavaVM* vm_;
    bool attached_ = false;
public:
    JNIEnv* env = nullptr;
    explicit Environment(JavaVM* vm) : vm_(vm) {
        if (!vm_) return;
        const auto status = vm_->GetEnv(reinterpret_cast<void**>(&env), JNI_VERSION_1_6);
        if (status == JNI_EDETACHED) {
            attached_ = vm_->AttachCurrentThread(&env, nullptr) == JNI_OK;
            if (!attached_) env = nullptr;
        } else if (status != JNI_OK) env = nullptr;
    }
    ~Environment() { if (attached_) vm_->DetachCurrentThread(); }
};
bool Ok(JNIEnv* env) {
    if (!env || !env->ExceptionCheck()) return env != nullptr;
    // This evaluation returns failure, never Java exception text or private data.
    env->ExceptionClear(); return false;
}
class JavaEffect final : public CaptureEffect {
    JavaVM* vm_;
    jobject instance_;
    jmethodID healthy_, close_;
    bool failed_ = false;
public:
    JavaEffect(JavaVM* vm, jobject instance, jmethodID healthy, jmethodID close)
        : vm_(vm), instance_(instance), healthy_(healthy), close_(close) {}
    ~JavaEffect() override { Close(); }
    bool Healthy() override {
        if (failed_ || !instance_) return false;
        Environment scope(vm_);
        if (!scope.env) { failed_ = true; return false; }
        const bool healthy = scope.env->CallBooleanMethod(instance_, healthy_) == JNI_TRUE;
        failed_ = !Ok(scope.env) || !healthy;
        return !failed_;
    }
    void Close() override {
        if (!instance_) return;
        Environment scope(vm_);
        // A live JVM and worker attachment are required through resource release.
        if (!scope.env) { failed_ = true; return; }
        scope.env->CallVoidMethod(instance_, close_); Ok(scope.env);
        scope.env->DeleteGlobalRef(instance_); instance_ = nullptr; failed_ = true;
    }
};
class JavaFactory final : public CaptureEffectFactory {
    JavaVM* vm_;
    jclass class_;
    jmethodID open_, healthy_, close_;
public:
    JavaFactory(JavaVM* vm, jclass cls, jmethodID open, jmethodID healthy, jmethodID close)
        : vm_(vm), class_(cls), open_(open), healthy_(healthy), close_(close) {}
    ~JavaFactory() override {
        Environment scope(vm_); if (scope.env) scope.env->DeleteGlobalRef(class_);
    }
    std::unique_ptr<CaptureEffect> Open(int session_id) override {
        if (session_id <= 0) return {};
        Environment scope(vm_); auto* env = scope.env;
        if (!env || env->PushLocalFrame(8) != JNI_OK) { Ok(env); return {}; }
        struct Frame { JNIEnv* env; ~Frame() { env->PopLocalFrame(nullptr); } } frame{env};
        jobject local = env->CallStaticObjectMethod(class_, open_, session_id);
        if (!Ok(env) || !local) return {};
        struct Pending {
            JNIEnv* env; jobject object; jmethodID close;
            ~Pending() { if (object) { env->CallVoidMethod(object, close); Ok(env); } }
        } pending{env, local, close_};
        jobject global = env->NewGlobalRef(local);
        if (!Ok(env) || !global) return {};
        struct Reference { JNIEnv* env; jobject value; ~Reference() { if (value) env->DeleteGlobalRef(value); } } ref{env, global};
        auto owner = std::make_unique<JavaEffect>(vm_, global, healthy_, close_);
        ref.value = nullptr; pending.object = nullptr;
        if (!owner->Healthy()) return {};
        return owner;
    }
};
}
std::unique_ptr<CaptureEffectFactory> MakeAndroidCaptureEffects(JNIEnv* env, jclass effect_class) {
    if (!env || !effect_class || env->ExceptionCheck()) return {};
    JavaVM* vm = nullptr;
    if (env->GetJavaVM(&vm) != JNI_OK || !vm) return {};
    const auto open = env->GetStaticMethodID(effect_class, "open",
        "(I)Lcom/maestro/quest/audio/PlatformCaptureEffects;");
    if (!Ok(env) || !open) return {};
    const auto healthy = env->GetMethodID(effect_class, "isHealthy", "()Z");
    if (!Ok(env) || !healthy) return {};
    const auto close = env->GetMethodID(effect_class, "close", "()V");
    if (!Ok(env) || !close) return {};
    auto cls = static_cast<jclass>(env->NewGlobalRef(effect_class));
    if (!Ok(env) || !cls) return {};
    struct Reference { JNIEnv* env; jclass value; ~Reference() { if (value) env->DeleteGlobalRef(value); } } ref{env, cls};
    auto owner = std::make_unique<JavaFactory>(vm, cls, open, healthy, close);
    ref.value = nullptr;
    return owner;
}
}
#endif
