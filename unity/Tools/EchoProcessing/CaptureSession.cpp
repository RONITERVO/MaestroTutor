// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
#include "CaptureSession.h"
#include <stdexcept>
namespace maestro {
namespace {
std::unique_ptr<CaptureDevice> SoftwareInput(std::unique_ptr<CaptureDevice> input){
    if(input&&input->HasPlatformEcho()){
        input->Close();
        throw std::invalid_argument("Platform echo input cannot enter the software AEC session");
    }
    return input;
}
bool Same(const DuplexFlush& a,const DuplexFlush& b){
    return a.request&&a.owner==b.owner&&a.epoch==b.epoch&&a.request==b.request&&a.input_samples==b.input_samples;
}
}
CaptureSession::CaptureSession(std::unique_ptr<CaptureDevice> input,uint64_t epoch,
    MonotonicClock clock,void* clock_context,CaptureSink sink,void* sink_context):
    input_(SoftwareInput(std::move(input)),epoch),processor_(epoch,clock,clock_context),
    clock_(clock),clock_context_(clock_context),sink_(sink),sink_context_(sink_context){
    if(!sink_)throw std::invalid_argument("Capture sink required");
}
CaptureSession::~CaptureSession(){Close();}
bool CaptureSession::Release(){
    input_buffer_.fill(0);output_buffer_.fill(0);sink_=nullptr;
    bool released=true;
    try{input_.Close();}catch(...){released=false;}
    processor_.Close();return released;
}
void CaptureSession::Fail(CaptureSessionFault reason){
    if(state_==CaptureSessionState::Closed||state_==CaptureSessionState::Finished)return;
    if(fault_==CaptureSessionFault::None)fault_=reason;
    state_=CaptureSessionState::Faulted;Release();
}
void CaptureSession::Close(){
    if(state_==CaptureSessionState::Closed)return;
    state_=CaptureSessionState::Closed;
    if(!Release()&&fault_==CaptureSessionFault::None)fault_=CaptureSessionFault::Release;
}
FlushStatus CaptureSession::TerminalStatus()const{
    return state_==CaptureSessionState::Closed?FlushStatus::Closed:
        state_==CaptureSessionState::Faulted?FlushStatus::Faulted:
        state_==CaptureSessionState::Finished?FlushStatus::Ready:FlushStatus::Invalid;
}
bool CaptureSession::Time(int64_t& value){
    try{value=clock_(clock_context_);}catch(...){Fail(CaptureSessionFault::Clock);return false;}
    if(value<0||value<last_now_){Fail(CaptureSessionFault::Clock);return false;}
    last_now_=value;return true;
}
bool CaptureSession::FinishDeadline(){
    if(state_!=CaptureSessionState::Finishing)return true;
    int64_t now;if(!Time(now))return false;
    if(now-finish_started_>=1000000000){Fail(CaptureSessionFault::Timeout);return false;}
    return true;
}
bool CaptureSession::Start(){
    if(callback_||state_!=CaptureSessionState::Created)return false;
    try{if(!input_.Start()){Fail(CaptureSessionFault::Input);return false;}}
    catch(...){Fail(CaptureSessionFault::Input);return false;}
    state_=CaptureSessionState::Running;return true;
}
DuplexStatus CaptureSession::Render(const float* input,int count,const PlaybackFrame& frame,const PlaybackClock& clock){
    if(callback_||state_==CaptureSessionState::Created)return DuplexStatus::Invalid;
    if(state_==CaptureSessionState::Faulted)return DuplexStatus::Faulted;
    if(state_==CaptureSessionState::Closed||state_==CaptureSessionState::Finished)return DuplexStatus::Closed;
    if(!FinishDeadline())return DuplexStatus::Faulted;
    const auto result=processor_.Render(input,count,frame,clock);
    if(result!=DuplexStatus::Accepted)Fail(CaptureSessionFault::Processing);
    return result;
}
bool CaptureSession::Admit(CaptureRead status,const CaptureFrame& frame){
    if(status==CaptureRead::Empty||status==CaptureRead::Waiting)return true;
    if(status!=CaptureRead::Frame){Fail(CaptureSessionFault::Input);return false;}
    const auto result=processor_.Capture(input_buffer_.data(),frame.samples,frame);
    input_buffer_.fill(0);
    if(result!=DuplexStatus::Accepted){Fail(CaptureSessionFault::Processing);return false;}
    accepted_+=frame.samples;return true;
}
bool CaptureSession::HandoffPending(){
    CaptureFrame frame;
    try{return Admit(input_.FlushPending(input_buffer_.data(),480,frame),frame);}
    catch(...){Fail(CaptureSessionFault::Input);return false;}
}
FlushStatus CaptureSession::Flush(DuplexFlush& ticket){
    ticket={};if(callback_||state_==CaptureSessionState::Created||state_==CaptureSessionState::Finishing||state_==CaptureSessionState::Finished)return FlushStatus::Invalid;
    if(state_!=CaptureSessionState::Running)return TerminalStatus();
    if(!HandoffPending())return FlushStatus::Faulted;
    const auto result=processor_.BeginFlush(ticket);
    if(result!=FlushStatus::Pending&&result!=FlushStatus::Ready){Fail(CaptureSessionFault::Processing);return FlushStatus::Faulted;}
    flush_=ticket;flush_done_=result==FlushStatus::Ready;return result;
}
FlushStatus CaptureSession::PollFlush(const DuplexFlush& ticket){
    if(callback_)return FlushStatus::Invalid;
    if(state_==CaptureSessionState::Closed||state_==CaptureSessionState::Faulted)return TerminalStatus();
    if(!Same(ticket,flush_))return FlushStatus::Invalid;
    if(flush_done_)return FlushStatus::Ready;
    Advance();return state_==CaptureSessionState::Faulted?FlushStatus::Faulted:flush_done_?FlushStatus::Ready:FlushStatus::Pending;
}
FlushStatus CaptureSession::Finish(){
    if(callback_)return FlushStatus::Invalid;
    if(state_==CaptureSessionState::Finishing)return PollFinish();
    if(state_!=CaptureSessionState::Running)return TerminalStatus();
    if(!Time(finish_started_)||!HandoffPending())return FlushStatus::Faulted;
    const auto result=processor_.BeginFlush(finish_);
    if(result!=FlushStatus::Pending&&result!=FlushStatus::Ready){Fail(CaptureSessionFault::Processing);return FlushStatus::Faulted;}
    // A prior nonterminal flush may cover an older prefix. Complete that caller
    // first, then request one new fence; never loop by extending a live snapshot.
    finish_again_=finish_.input_samples<accepted_;
    state_=CaptureSessionState::Finishing;Advance();return PollFinish();
}
FlushStatus CaptureSession::PollFinish(){
    if(callback_)return FlushStatus::Invalid;
    if(state_!=CaptureSessionState::Finishing)return TerminalStatus();
    Advance();return state_==CaptureSessionState::Finishing?FlushStatus::Pending:TerminalStatus();
}
void CaptureSession::Advance(){
    if(state_!=CaptureSessionState::Running&&state_!=CaptureSessionState::Finishing)return;
    if(!FinishDeadline())return;
    if(flush_.request&&!flush_done_){
        const auto status=processor_.PollFlush(flush_);
        if(status==FlushStatus::Ready)flush_done_=true;
        else if(status!=FlushStatus::Pending){Fail(CaptureSessionFault::Processing);return;}
    }
    if(state_!=CaptureSessionState::Finishing)return;
    auto status=processor_.PollFlush(finish_);
    if(status==FlushStatus::Ready&&finish_again_){
        finish_again_=false;status=processor_.BeginFlush(finish_);
    }
    if(status==FlushStatus::Pending)return;
    if(status!=FlushStatus::Ready){Fail(CaptureSessionFault::Processing);return;}
    if(!Release()){Fail(CaptureSessionFault::Release);return;}
    state_=CaptureSessionState::Finished;
}
CapturePump CaptureSession::Pump(){
    if(callback_||state_==CaptureSessionState::Created)return CapturePump::Invalid;
    if(state_==CaptureSessionState::Closed)return CapturePump::Closed;
    if(state_==CaptureSessionState::Faulted)return CapturePump::Faulted;
    if(state_==CaptureSessionState::Finished)return CapturePump::Finished;
    if(!FinishDeadline())return CapturePump::Faulted;
    CaptureFrame input_frame;CaptureRead read;
    try{read=input_.Read(input_buffer_.data(),480,input_frame);}
    catch(...){Fail(CaptureSessionFault::Input);return CapturePump::Faulted;}
    if(!Admit(read,input_frame))return CapturePump::Faulted;
    bool progress=read==CaptureRead::Frame;
    for(int i=0;i<8;++i){
        DuplexFrame frame;const auto result=processor_.Process(output_buffer_.data(),160,frame);
        if(result==DuplexStatus::Waiting)break;
        if(result!=DuplexStatus::Frame){Fail(CaptureSessionFault::Processing);return CapturePump::Faulted;}
        bool admitted=false;callback_=true;
        try{admitted=sink_(sink_context_,output_buffer_.data(),160,frame);}catch(...){admitted=false;}
        callback_=false;output_buffer_.fill(0);
        if(state_==CaptureSessionState::Closed)return CapturePump::Closed;
        if(!admitted){Fail(CaptureSessionFault::Sink);return CapturePump::Faulted;}
        if(processor_.AcknowledgeDelivery(frame)!=DuplexStatus::Accepted){Fail(CaptureSessionFault::Processing);return CapturePump::Faulted;}
        progress=true;Advance();
        if(state_==CaptureSessionState::Finished)return CapturePump::Finished;
        if(state_==CaptureSessionState::Faulted)return CapturePump::Faulted;
    }
    Advance();
    return state_==CaptureSessionState::Finished?CapturePump::Finished:
        state_==CaptureSessionState::Faulted?CapturePump::Faulted:progress?CapturePump::Progress:CapturePump::Waiting;
}
}
