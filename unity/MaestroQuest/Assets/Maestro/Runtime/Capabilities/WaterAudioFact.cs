// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Book;
using Newtonsoft.Json.Linq;
using static Maestro.Quest.Programs.CapabilitySchema;
namespace Maestro.Quest.Programs {
    internal static class WaterAudioFact {
        internal static BehaviourCatalog.FactDefinition Definition()=>new("world.audio.water",
            OutputType(Object(new JObject{["state"]=Choice("idle","disabled","active","pending","unavailable"),["sources"]=Number(0,RoomWaterAudio.MaximumSources,true),["treatedSources"]=Number(0,RoomWaterAudio.MaximumSources,true),["unavailableSources"]=Number(0,RoomWaterAudio.MaximumSources,true),["reason"]=Text("^.{0,256}$",256)})),
            "Water sound treatment",
            "Last bounded native water-audio control pass for this workspace. sources counts registered spatial PCM renderers, including paused/pending playback; treatedSources counts paths receiving nonzero treatment, not audible samples. idle has no sources; disabled has no enabled cavity settings; active has complete listener/path geometry; pending/unavailable marks missing paths or listener/runtime readiness. The whole snapshot becomes unavailable after 0.2 seconds or loss of focus. Individual missing paths bypass treatment, never mute speech. Nonspatial sounds and optional evaluation renderers are excluded. Reading does not start audio, capture a microphone, load geometry or change physics. Use object.container.audio for settings and world.medium.path for finite geometry.",
            null,null,(c,a)=>c.Editor?.GetComponent<RoomWaterAudio>()?.Observe() is JObject value?ProgramValue.Literal(value):null,features:new[]{WaterAudioCapability.Feature});
    }
}
