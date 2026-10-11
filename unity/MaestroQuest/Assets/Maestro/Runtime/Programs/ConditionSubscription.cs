// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Programs
{
    // The program supplies a typed, read-only expression. Sampling cannot execute
    // statements or acquire objects, and never runs catch-up evaluations.
    public sealed class ConditionSubscription : IProgramEventWatch
    {
        public const float SampleInterval=.1f,MaximumSampleGap=.3f;
        readonly Func<bool> read;
        readonly string transition,initial;
        readonly float stableSeconds;
        bool candidate,disposed;
        bool? confirmed;
        float since,lastSample,nextSample;
        public ConditionSubscription(Func<bool> read,string transition,string initial,float stableSeconds,float now) {
            this.read=read;this.transition=transition;this.initial=initial;this.stableSeconds=stableSeconds;
            Reset(read(),now);lastSample=now;nextSample=now+SampleInterval;
        }
        void Reset(bool value,float now) {candidate=value;confirmed=initial=="baseline"?value:null;since=now;}
        public bool Poll(float now,out ProgramValue value,out JObject fields,out string error) {
            value=default;fields=null;error=null;if(disposed||now<nextSample)return false;
            nextSample=now+SampleInterval;
            try {
                bool sample=read();
                if(now-lastSample>MaximumSampleGap)Reset(sample,now);
                lastSample=now;
                if(sample!=candidate){candidate=sample;since=now;}
                if(now-since+1e-6f<stableSeconds||confirmed==candidate)return false;
                confirmed=candidate;
                if(transition!="either"&&transition!=(candidate?"true":"false"))return false;
                value=new ProgramValue(candidate);return true;
            }catch(ProgramFault fault){error=fault.Message;return false;}
        }
        public void Dispose(){disposed=true;}
    }
}
