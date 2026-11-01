// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    // Immutable observations never retain a PCM buffer, renderer or stream credential.
    public readonly struct AudioInstanceSample
    {
        public readonly string Instance,Target,Emitter,Source,Phase,Lifetime,Error;
        public readonly int SourceRevision,Revision;
        public readonly double Seconds;
        public readonly bool Loop;
        public readonly float Gain;
        public AudioInstanceSample(string instance,string target,string emitter,string source,int sourceRevision,int revision,string phase,double seconds,bool loop,float gain,string lifetime,string error)
        {Instance=instance;Target=target;Emitter=emitter;Source=source;SourceRevision=sourceRevision;Revision=revision;Phase=phase;Seconds=seconds;Loop=loop;Gain=gain;Lifetime=lifetime;Error=error;}
        public JObject Json()=>new(){["revision"]=Revision,["identity"]=new JObject {["instance"]=Instance,["target"]=Target,["emitter"]=Emitter,["source"]=Source,["sourceRevision"]=SourceRevision},["playback"]=new JObject {["phase"]=Phase,["seconds"]=Seconds,["loop"]=Loop,["gain"]=Gain,["lifetime"]=Lifetime},["error"]=Error};
    }
    public interface IProgramAudioWorld
    {
        bool TryAudioInstance(string target,string instance,out AudioInstanceSample sample);
        bool TryAudioNotice(string target,string instance,int after,out AudioInstanceSample sample,out string error);
    }
    internal sealed class AudioInstanceSubscription:IProgramEventWatch
    {
        readonly IProgramAudioWorld world;readonly string target,instance;int after;bool disposed;
        internal AudioInstanceSubscription(IProgramEventWorld world,JObject args)
        {
            this.world=world as IProgramAudioWorld;target=(string)args["target"];instance=(string)args["instance"];after=(int)args["after"];
            if(this.world==null||!this.world.TryAudioInstance(target,instance,out var sample)||after>sample.Revision)throw new ProgramFault("Inspect a current audio.instance before watching its changes");
        }
        public bool Poll(float now,out ProgramValue value,out JObject fields,out string error)
        {
            value=default;fields=null;error=null;if(disposed)return false;
            if(!world.TryAudioNotice(target,instance,after,out var next,out error))return false;
            after=next.Revision;value=new ProgramValue(target);fields=new JObject {["instance"]=instance,["revision"]=next.Revision,["phase"]=next.Phase,["seconds"]=next.Seconds,["loop"]=next.Loop,["gain"]=next.Gain,["lifetime"]=next.Lifetime,["error"]=next.Error};return true;
        }
        public void Dispose()=>disposed=true;
        internal static BehaviourCatalog.EventDefinition Definition()=>new("audio.instance.changed","Sound playback changed",
            "Observe retained lifecycle/control changes for one exact audio instance. Source filter must be empty. Read audio.instance, then use its revision as after; after=0 requests the retained history from creation. Events carry instance, revision, phase, consumed seconds, loop, gain, lifetime and bounded error. Keep the delivered revision to wait for the next change. Each instance retains 16 changes; 32 terminal instances are retained per room owner. Overflow, an unknown identity or a changed room fails visibly rather than silently skipping changes. Reading or waiting never starts, resumes or owns audio. The primary text value is the target object ID. Native consumption is not proof of headset audibility.",
            Object(new JObject {["instance"]=AudioSchema.Id(),["revision"]=Revision(),["phase"]=AudioPlaybackCapabilities.Phase(),["seconds"]=Number(0,ProgramValue.MaximumNumber),["loop"]=new JObject {["type"]="boolean"},["gain"]=Number(0,1),["lifetime"]=Choice("action","room"),["error"]=Text("^.{0,120}$",120)}),
            input:AudioSchema.Featured(Object(new JObject {["target"]=AudioSchema.Target(),["instance"]=AudioSchema.Id(),["after"]=Revision(true)})),
            example:new JObject {["target"]="maestro",["instance"]=new string('0',32),["after"]=0},
            watch:(world,args,now)=>new AudioInstanceSubscription(world,args),features:new[]{AudioSchema.Feature});
    }
}
