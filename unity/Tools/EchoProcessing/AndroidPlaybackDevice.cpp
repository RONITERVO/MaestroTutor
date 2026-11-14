// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#include "PlaybackOutput.h"
#if defined(__ANDROID__)
#include <aaudio/AAudio.h>
#include <time.h>
namespace maestro {
namespace {
class AndroidPlaybackDevice final:public PlaybackDevice{
    AAudioStream* stream_=nullptr;
    PlaybackCallback callback_;void* user_;
    std::atomic<bool> disconnected_{false};
    static aaudio_data_callback_result_t Data(AAudioStream* stream,void* user,void* buffer,int32_t frames){
        auto* self=static_cast<AndroidPlaybackDevice*>(user);
        return self->callback_(self->user_,static_cast<float*>(buffer),frames,AAudioStream_getFramesWritten(stream))
            ?AAUDIO_CALLBACK_RESULT_CONTINUE:AAUDIO_CALLBACK_RESULT_STOP;
    }
    static void Error(AAudioStream*,void* user,aaudio_result_t){
        // Only signal here. The owning worker stops/closes the stream later.
        static_cast<AndroidPlaybackDevice*>(user)->disconnected_.store(true,std::memory_order_release);
    }
public:
    AndroidPlaybackDevice(PlaybackCallback callback,void* user):callback_(callback),user_(user){}
    ~AndroidPlaybackDevice()override{Close();}
    bool Open(){
        AAudioStreamBuilder* builder=nullptr;
        if(AAudio_createStreamBuilder(&builder)!=AAUDIO_OK||!builder){if(builder)AAudioStreamBuilder_delete(builder);return false;}
        AAudioStreamBuilder_setDirection(builder,AAUDIO_DIRECTION_OUTPUT);
        AAudioStreamBuilder_setSampleRate(builder,PlaybackOutput::Rate);
        AAudioStreamBuilder_setChannelCount(builder,PlaybackOutput::Channels);
        AAudioStreamBuilder_setFormat(builder,AAUDIO_FORMAT_PCM_FLOAT);
        AAudioStreamBuilder_setSharingMode(builder,AAUDIO_SHARING_MODE_SHARED);
        AAudioStreamBuilder_setPerformanceMode(builder,AAUDIO_PERFORMANCE_MODE_LOW_LATENCY);
        AAudioStreamBuilder_setUsage(builder,AAUDIO_USAGE_GAME);
        AAudioStreamBuilder_setContentType(builder,AAUDIO_CONTENT_TYPE_MUSIC);
        // The caller supplies the final binaural mix. API 32 is our minimum;
        // prevent a second platform spatialization pass from moving it again.
        AAudioStreamBuilder_setIsContentSpatialized(builder,true);
        AAudioStreamBuilder_setSpatializationBehavior(builder,AAUDIO_SPATIALIZATION_BEHAVIOR_NEVER);
        AAudioStreamBuilder_setDataCallback(builder,Data,this);
        AAudioStreamBuilder_setErrorCallback(builder,Error,this);
        const auto status=AAudioStreamBuilder_openStream(builder,&stream_);
        AAudioStreamBuilder_delete(builder);
        if(status!=AAUDIO_OK||!stream_)return false;
        return AAudioStream_getDirection(stream_)==AAUDIO_DIRECTION_OUTPUT&&
            AAudioStream_getSampleRate(stream_)==PlaybackOutput::Rate&&AAudioStream_getChannelCount(stream_)==PlaybackOutput::Channels&&
            AAudioStream_getFormat(stream_)==AAUDIO_FORMAT_PCM_FLOAT&&
            AAudioStream_getBufferCapacityInFrames(stream_)>0&&AAudioStream_getBufferCapacityInFrames(stream_)<=PlaybackOutput::Capacity;
    }
    bool Start()override{return stream_&&AAudioStream_requestStart(stream_)==AAUDIO_OK;}
    int Ready()override{
        if(!stream_||disconnected_.load(std::memory_order_acquire))return -1;
        const auto state=AAudioStream_getState(stream_);
        if(state==AAUDIO_STREAM_STATE_STARTED)return 1;
        return state==AAUDIO_STREAM_STATE_STARTING||state==AAUDIO_STREAM_STATE_OPEN?0:-1;
    }
    int Timestamp(DeviceAudioStamp& stamp)override{
        if(!stream_)return -1;
        const auto result=AAudioStream_getTimestamp(stream_,CLOCK_MONOTONIC,&stamp.position,&stamp.time_ns);
        if(result==AAUDIO_OK)return 1;
        return result==AAUDIO_ERROR_INVALID_STATE?0:-1;
    }
    int Xruns()override{return stream_?AAudioStream_getXRunCount(stream_):-1;}
    int64_t Now()override{timespec t{};return clock_gettime(CLOCK_MONOTONIC,&t)==0?t.tv_sec*int64_t{1000000000}+t.tv_nsec:-1;}
    void Close()override{
        if(!stream_)return;auto* retiring=stream_;stream_=nullptr;
        // AAudio permits owner-thread close while its data callback runs. Return
        // only after close has released the stream and its callbacks.
        AAudioStream_requestStop(retiring);AAudioStream_close(retiring);
    }
};
}
std::unique_ptr<PlaybackDevice> OpenAndroidPlayback(PlaybackCallback callback,void* user){
    if(!callback||!user)return {};
    auto device=std::make_unique<AndroidPlaybackDevice>(callback,user);
    if(!device->Open())return {};return device;
}
}
#else
namespace maestro{std::unique_ptr<PlaybackDevice> OpenAndroidPlayback(PlaybackCallback,void*){return {};}}
#endif
