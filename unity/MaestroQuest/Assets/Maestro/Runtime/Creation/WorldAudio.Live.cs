// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Imports;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation {
    public sealed partial class WorldAudio {
        internal JObject ObserveLiveInstance(string target,string id){
            var v=FindInstance(target,id);if(v==null||!v.IsLive)return null;
            return new JObject{["revision"]=v.Revision,["sourcePhase"]=v.LivePhase,["receivedSeconds"]=v.LiveReceivedFrames/24000d,
                ["skippedSeconds"]=v.LiveSkippedFrames/24000d,["discardedQueuedSeconds"]=v.LiveDiscardedFrames/24000d,["bufferedSeconds"]=v.LiveBufferedFrames/24000d};
        }
        bool ObserveLive(Instance v){
            if(v.Asset is not LivePcmAsset asset){Close(v,"The live source is unavailable");return false;}
            var source=asset.State;v.LivePhase=source.Phase;v.LiveReceivedFrames=source.Produced;v.LiveBufferedFrames=source.Buffered;v.LiveSkippedFrames=source.Dropped;
            if(source.Phase is "failed" or "closed"){Close(v,source.Error.Length>0?source.Error:"The live connection closed");return false;}
            return true;
        }
        bool RestartLiveOutput(Instance v){
            try{
                var state=v.Stream.Read(AudioSettings.dspTime);v.LivePlayedBase+=state.Played;v.LiveDiscardedFrames+=state.Submitted-state.Played;
                v.Cursor=v.LivePlayedBase/24000d;v.Renderer.Dispose();v.Renderer=null;v.Written=v.Played=v.Sequence=0;
                OpenOutput(v);if(!Follow(v))return false;v.Renderer.Start();v.ProgressAt=Time.realtimeSinceStartupAsDouble;return true;
            }catch(Exception){Close(v,"The live sound output could not resume");return false;}
        }
        void PumpLive(Instance v){
            if(!ObserveLive(v))return;var asset=(LivePcmAsset)v.Asset;var state=v.Stream.Read(AudioSettings.dspTime);
            if(state.Faulted||state.Closed){Close(v,"Audio output stopped consuming live samples");return;}
            bool sync=v.LiveSync||asset.State.Dropped>v.LiveReadDropped;
            if(!state.InputComplete&&state.Submitted-state.Played<2400){
                int wanted=(int)Math.Min(v.Producer.Length,2400-(state.Submitted-state.Played));
                var read=asset.ReadLive(v.Producer,wanted,sync);
                if(read.Count>0){
                    v.LiveSync=false;v.LiveReadDropped=read.State.Dropped;
                    if(read.Dropped>0){if(v.Written>0&&!RestartLiveOutput(v))return;Note(v);}
                    if(!v.Stream.TryWrite(v.Sequence+1,v.Producer,read.Count,AudioSettings.dspTime,out var error)){Close(v,error);return;}
                    v.Sequence++;v.Written+=read.Count;
                }
                if(!ObserveLive(v))return;
                if(read.State.Phase=="ended"&&!v.Stream.Read(AudioSettings.dspTime).InputComplete){
                    if(!v.Stream.TryComplete(v.Sequence,out var error)){Close(v,error);return;}
                }
            }
            state=v.Stream.Read(AudioSettings.dspTime);
            if(state.Played!=v.Played){
                v.Played=state.Played;v.ProgressAt=Time.realtimeSinceStartupAsDouble;
                if(v.Phase is "preparing" or "buffering"){v.Phase="playing";Note(v);}
            } else if(state.Submitted>state.Played&&Time.realtimeSinceStartupAsDouble-v.ProgressAt>3){Close(v,"Audio output did not consume its live samples");return;}
            else if(!state.InputComplete&&state.Submitted==state.Played&&v.Phase=="playing"&&Time.realtimeSinceStartupAsDouble-v.ProgressAt>.25){v.Phase="buffering";Note(v);}
            v.Cursor=(v.LivePlayedBase+state.Played)/24000d;
            if(v.Renderer.IsDrained(AudioSettings.dspTime)){v.Complete=true;Close(v,null,"ended");}
        }
    }
}
