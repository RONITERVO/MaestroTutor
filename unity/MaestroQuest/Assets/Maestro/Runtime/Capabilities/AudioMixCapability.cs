// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs
{
    internal sealed class AudioMixCapability:CapabilityModule
    {
        public override string Id=>"audio.mix.control";
        public override string Label=>"Adjust world sound levels";
        public override string Duration=>"instant";
        public override IReadOnlyList<string> Requirements=>new[]{"room.ready","audio.mix.current"};
        public override string Description=>"Set the current world's master, effects, media and ambience levels, or reset all four to one. Read audio.mix and use its exact stateId; concurrent changes and room boundaries invalidate stale requests. Native world-voice gain is its current instance gain multiplied by master and its captured emitter role level. Existing, preparing, paused and future sounds use the same controls. Zero mutes without stopping, pausing, releasing voices or restarting PCM; audio.instance retains its own independent gain and control revision. A completed setting lasts after its program stops. No room save or Undo entry; source/emitter edits and playback renderer recreation preserve these controls. Temporary-room Begin/Discard, room reload/workspace replacement and disabling RoomEditor reset them; copies/exports contain no live mix. Focus or audio-device loss still cancels sounds and retains levels. Unavailable renderers fail through their existing instance lifecycle. Only authored world audio is affected: separate conversation/Live speech and web artifact playback keep their own output ownership. This is volume control, not echo cancellation or a microphone policy, and cannot select a new audio backend, open capture, change acoustic occlusion or prove physical audibility.";
        internal static JObject ViewSchema()=>Object(new JObject {
            ["stateId"]=AudioSchema.Id(),["master"]=Number(0,1),["effects"]=Number(0,1),["media"]=Number(0,1),["ambience"]=Number(0,1)});
        static JObject Variant(string op)
        {
            var fields=new JObject {["operation"]=Choice(op),["stateId"]=AudioSchema.Id()};fields["operation"]["x-static"]=true;
            if(op=="set")foreach(var role in new[]{"master","effects","media","ambience"})fields[role]=Number(0,1);
            var schema=CurrentInputs(Object(fields),"audio.mix","stateId",null,op=="set"?new[]{"master","effects","media","ambience"}:System.Array.Empty<string>());
            schema["title"]=op=="set"?"Set world sound levels":"Reset world sound levels";return AudioSchema.Featured(schema);
        }
        public override JObject InputSchema=>new(){["type"]="object",["x-discriminators"]=new JArray("operation"),["oneOf"]=new JArray(Variant("set"),Variant("reset"))};
        public override JObject OutputSchema=>ViewSchema();
        public override JObject Example=>new(){["operation"]="set",["stateId"]=new string('0',32),["master"]=1,["effects"]=1,["media"]=.5,["ambience"]=.25};
        public override bool CanRun(CapabilityContext c,JObject a,out string error){error="Room unavailable";return c.Editor&&c.Editor.CanControlAudioMix((string)a["stateId"],out error);}
        public override bool Start(CapabilityContext c,string run,JObject a,out CapabilityOperation operation,out string error)
        {
            operation=null;if(!CanRun(c,a,out error))return false;
            string op=(string)a["operation"];if(op is not ("set" or "reset")){error="Unknown world audio control";return false;}
            bool reset=op=="reset";
            if(!c.Editor.ControlAudioMix((string)a["stateId"],reset?1:(float)a["master"],reset?1:(float)a["effects"],reset?1:(float)a["media"],reset?1:(float)a["ambience"],out error))return false;
            operation=new CompletedCapability(c.Editor.ObserveAudioMix());return true;
        }
        internal static BehaviourCatalog.FactDefinition Fact()
        {
            var type=OutputType(ViewSchema());return new("audio.mix",type,"World sound levels",
                "Read the current world's transient master and effects/media/ambience multipliers and exact stateId for audio.mix.control. Defaults are one. Passive: cannot allocate, start, stop, advance or adopt any sound. Instance gain stays separate: effective native gain = instance gain * master * emitter role level. A zero level does not pause consumption or free voice capacity. Does not include separate Maestro speech, web artifacts, device volume, distance attenuation or acoustic effects. Values are controls, not proof of audible output. Room boundaries and editor disable reset levels and invalidate the stateId.",
                null,null,(c,a)=>c.Editor&&c.Editor.ObserveAudioMix() is JObject value?ProgramValue.Literal(value,type):null,features:new[]{AudioSchema.Feature});
        }
    }
}
