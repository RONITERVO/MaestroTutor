// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Book {
    [DefaultExecutionOrder(500)]
    internal sealed class RoomWaterAudio:MonoBehaviour {
        internal const int MaximumSources=AudioReservations.MaximumOwners+1;
        readonly SpeechPcmFilter[] sources=new SpeechPcmFilter[MaximumSources];
        readonly LiquidPath path=new();
        RoomEditor editor;int count,treated,unavailable;
        float nextAt,sampledAt=float.NegativeInfinity;
        bool paused,focused=true;
        string state="idle",reason="";
        internal void Initialize(RoomEditor value)=>editor=value;
        internal void Register(SpeechPcmFilter source){
            for(int i=0;i<count;i++)if(sources[i]==source)return;
            if(count==MaximumSources)throw new InvalidOperationException("Water audio source budget is full");
            sources[count++]=source;nextAt=0;sampledAt=float.NegativeInfinity;
        }
        internal void Unregister(SpeechPcmFilter source){for(int i=0;i<count;i++)if(sources[i]==source){sources[i]=sources[--count];sources[count]=null;nextAt=0;sampledAt=float.NegativeInfinity;if(count==0){state="idle";reason="";treated=unavailable=0;}break;}}
        void DryAll(){for(int i=0;i<count;i++)if(sources[i])sources[i].SetWater(WaterSoundMix.Dry,double.PositiveInfinity);treated=0;}
        internal void Tick(float now,double dspTime){
            if(now<nextAt)return;nextAt=now+.05f;sampledAt=now;treated=unavailable=0;reason="";
            if(count==0){state="idle";return;}
            if(!editor||!editor.isActiveAndEnabled||!isActiveAndEnabled||paused||!focused||editor.RuntimeGate.Held){
                state="unavailable";reason="Water audio workspace is unavailable";unavailable=count;DryAll();return;
            }
            if(!editor.WaterAudioConfigured){state="disabled";DryAll();return;}
            var viewer=editor.Viewer;var listener=viewer?viewer.GetComponent<AudioListener>():null;var tracking=editor.GetComponent<WorkspaceViewpoint>();
            if(!listener||!listener.isActiveAndEnabled||tracking&&!tracking.AudioReady||!RoomRecipe.Finite(viewer.position)){
                state="unavailable";reason="Water audio needs an active tracked listener";unavailable=count;DryAll();return;
            }
            state="active";editor.Liquids.CaptureLiquidPaths();
            for(int i=0;i<count;i++){
                var source=sources[i];if(!source){state="unavailable";unavailable++;continue;}
                var availability=editor.Liquids.MeasureCapturedLiquidPath(source.transform.position,viewer.position,path);
                if(!availability.Ready){
                    unavailable++;if(availability.State=="unavailable"||state=="active")state=availability.State;
                    reason="Some sound paths lack complete liquid geometry";
                    source.SetWater(WaterSoundMix.Dry,dspTime+.2);continue;
                }
                var mix=WaterSoundMix.From(path);if(mix.Applied)treated++;
                source.SetWater(mix,dspTime+.2);
            }
        }
        internal JObject Observe(){
            bool stale=count>0&&(!isActiveAndEnabled||paused||!focused||Time.unscaledTime-sampledAt>.2f);
            return new JObject{["state"]=stale?"unavailable":state,["sources"]=count,["treatedSources"]=stale?0:treated,["unavailableSources"]=stale?count:unavailable,["reason"]=stale?"Water audio observation is unavailable or stale":reason};
        }
        void LateUpdate()=>Tick(Time.unscaledTime,AudioSettings.dspTime);
        void OnDisable(){DryAll();state="unavailable";sampledAt=float.NegativeInfinity;}
        void OnDestroy(){DryAll();Array.Clear(sources,0,sources.Length);count=0;}
        void OnApplicationPause(bool value){paused=value;if(value){DryAll();sampledAt=float.NegativeInfinity;}else nextAt=0;}
        void OnApplicationFocus(bool value){focused=value;if(!value){DryAll();sampledAt=float.NegativeInfinity;}else nextAt=0;}
    }
}
