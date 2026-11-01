// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Newtonsoft.Json.Linq;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        // This workspace state exists before the first voice. Observing it cannot
        // create an audio renderer, decoder or microphone owner.
        string audioMixStateId=Guid.NewGuid().ToString("N");
        float audioMaster=1,audioEffects=1,audioMedia=1,audioAmbience=1;
        internal JObject ObserveAudioMix()=>journal==null?null:new JObject {
            ["stateId"]=audioMixStateId,["master"]=audioMaster,["effects"]=audioEffects,
            ["media"]=audioMedia,["ambience"]=audioAmbience};
        internal float AudioMixGain(string role)=>audioMaster*(role switch {
            "effects"=>audioEffects,"media"=>audioMedia,"ambience"=>audioAmbience,_=>0});
        internal bool CanControlAudioMix(string expected,out string error)
        {
            error="World audio controls are unavailable";
            if(journal==null||!isActiveAndEnabled||RuntimeGate.Held||Ownership.Suspended)return false;
            error="World audio levels changed; read audio.mix again";
            if(expected!=audioMixStateId)return false;error=null;return true;
        }
        internal bool ControlAudioMix(string expected,float master,float effects,float media,float ambience,out string error)
        {
            if(!CanControlAudioMix(expected,out error))return false;
            if(!RoomAudioDefinition.Range(master,0,1)||!RoomAudioDefinition.Range(effects,0,1)||
                !RoomAudioDefinition.Range(media,0,1)||!RoomAudioDefinition.Range(ambience,0,1)) {
                error="World audio levels must be finite numbers between zero and one";return false;
            }
            if(audioMaster==master&&audioEffects==effects&&audioMedia==media&&audioAmbience==ambience)return true;
            audioMaster=master;audioEffects=effects;audioMedia=media;audioAmbience=ambience;
            audioMixStateId=Guid.NewGuid().ToString("N");GetComponent<WorldAudio>()?.ApplyMix();return true;
        }
        void ResetAudioMix()
        {
            audioMaster=audioEffects=audioMedia=audioAmbience=1;
            // Even a default-valued previous room must not authorize this room.
            audioMixStateId=Guid.NewGuid().ToString("N");GetComponent<WorldAudio>()?.ApplyMix();
        }
    }
}
