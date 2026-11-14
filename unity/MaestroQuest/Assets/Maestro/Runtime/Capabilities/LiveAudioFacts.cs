// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal static class LiveAudioFacts {
        static ProgramDataType SourceType()=>ProgramDataType.Read(JObject.Parse("{\"record\":{\"id\":\"text\",\"revision\":\"number\",\"endpoint\":\"text\",\"total\":\"number\",\"next\":\"number\"}}"));
        internal static ProgramValue? SourcePage(Creation.RoomAudioDefinition source,int revision,int offset){
            if(source?.kind!="stream"||offset<0||offset>source.endpoint.Length||offset<source.endpoint.Length&&char.IsLowSurrogate(source.endpoint[offset]))return null;
            int count=System.Math.Min(128,source.endpoint.Length-offset);if(count>0&&char.IsHighSurrogate(source.endpoint[offset+count-1]))count--;
            int next=offset+count;return ProgramValue.Literal(new JObject{["id"]=source.id,["revision"]=revision,["endpoint"]=source.endpoint.Substring(offset,count),["total"]=source.endpoint.Length,["next"]=next<source.endpoint.Length?next:-1},SourceType());
        }
        internal static BehaviourCatalog.FactDefinition Source()=>new("audio.source.stream",SourceType(),"Live sound endpoint",
            "Read at most 128 characters of a saved public HTTPS endpoint at offset. Start at zero; concatenate endpoint segments in order until next=-1. total is the full character count. Keep id/revision constant between pages or start reading again. Other source kinds are unavailable. This passive read never connects, authenticates or resumes playback. Save through audio.source.edit; only audio.start opens a connection. No credentials, query or fragment may be stored.",
            Object(new JObject{["id"]=AudioSchema.Id(),["offset"]=Number(0,2048,true)}),new JObject{["id"]=new string('0',32),["offset"]=0},(c,a)=>{
                var source=c.Editor?c.Editor.ReadAudio((string)a["id"]):null;return source==null?null:SourcePage(source,c.Editor.AudioRevision(source.id),(int)a["offset"]);
            },features:new[]{AudioSchema.StreamFeature});
        static ProgramDataType LiveType()=>ProgramDataType.Read(JObject.Parse("{\"record\":{\"revision\":\"number\",\"sourcePhase\":\"text\",\"receivedSeconds\":\"number\",\"skippedSeconds\":\"number\",\"discardedQueuedSeconds\":\"number\",\"bufferedSeconds\":\"number\"}}"));
        internal static BehaviourCatalog.FactDefinition Live()=>new("audio.live",LiveType(),"Live sound buffering",
            "Read the latest native observation for an exact live playback instance. sourcePhase distinguishes connecting, buffering, available, ended, failed or closed. Received time counts decoded source PCM; skipped time counts expired or deliberately skipped source PCM. Discarded queued time is output not yet confirmed consumed when resynchronizing; it is not proof of what a listener heard. Buffered time is the remaining bounded source PCM, excluding the native output queue. audio.instance retains the control revision, lifecycle and consumed cursor. Reads never advance, connect or restart the source. Ordinary clip instances are unavailable here.",
            AudioPlaybackCapabilities.Query(),AudioPlaybackCapabilities.Example(),(c,a)=>{
                var audio=c.Editor?c.Editor.GetComponent<Creation.WorldAudio>():null;var result=audio?audio.ObserveLiveInstance((string)a["target"],(string)a["instance"]):null;
                return result==null?null:ProgramValue.Literal(result,LiveType());
            },features:new[]{AudioSchema.StreamFeature});
    }
}
