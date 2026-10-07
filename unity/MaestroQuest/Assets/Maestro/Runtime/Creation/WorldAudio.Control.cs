// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Programs;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class WorldAudio
    {
        internal const int MaximumHistory=32,MaximumNotices=16;
        readonly Dictionary<string,Instance> history=new(StringComparer.Ordinal);
        readonly Queue<string> historyOrder=new();
        static AudioInstanceSample Sample(Instance v)=>new(v.Id,v.Target,v.Emitter,v.Source,v.SourceRevision,v.Revision,v.Phase,v.Cursor,v.Loop,v.Gain,v.RoomOwned?"room":"action",Bounded(v.Error));
        static string Bounded(string value)=>value==null?"":new string(value.Where(c=>!char.IsControl(c)).Take(120).ToArray());
        static void Note(Instance v)
        {
            v.Revision=checked(v.Revision+1);v.Notices.Enqueue(Sample(v));
            while(v.Notices.Count>MaximumNotices)v.Notices.Dequeue();
        }
        void Remember(Instance v)
        {
            if(history.ContainsKey(v.Id))return;history.Add(v.Id,v);historyOrder.Enqueue(v.Id);
            while(historyOrder.Count>MaximumHistory)history.Remove(historyOrder.Dequeue());
        }
        Instance FindInstance(string target,string id)
        {
            if(id==null||!editor)return null;
            if(!instances.TryGetValue(id,out var v)&&!history.TryGetValue(id,out v))return null;
            return v.Target==target&&v.Session==editor.TemporarySessionId?v:null;
        }
        internal bool ReadInstance(string target,string id,out AudioInstanceSample sample)
        {
            var v=FindInstance(target,id);sample=v==null?default:Sample(v);return v!=null;
        }
        internal AudioInstanceSample[] ActiveInstances()=>instances.Values.Where(v=>editor&&v.Session==editor.TemporarySessionId).OrderBy(v=>v.Id,StringComparer.Ordinal).Select(Sample).ToArray();
        internal bool NextNotice(string target,string id,int after,out AudioInstanceSample sample,out string error)
        {
            sample=default;error=null;var v=FindInstance(target,id);
            if(v==null){error="The audio instance is missing or its retained history expired";return false;}
            if(after<0||after>v.Revision){error="The audio observation revision is not valid for this instance";return false;}
            if(after<v.Notices.Peek().Revision-1){error="Audio changes exceeded the retained event window; inspect the instance again";return false;}
            foreach(var notice in v.Notices)if(notice.Revision>after){sample=notice;return true;}
            return false;
        }
        internal bool Handoff(Instance v,out string error)
        {
            error=v.Error;if(error!=null)return false;
            if(v.Cursor<=0){error="The audio renderer has not consumed sound yet";return false;}
            if(!v.RoomOwned){v.RoomOwned=true;Note(v);}return true;
        }
        internal bool CanControl(string target,string id,int revision,string operation,out Instance v,out string error)
        {
            v=FindInstance(target,id);error="Inspect the current audio.instance before controlling it";
            if(v==null||v.Revision!=revision)return false;
            error="This sound belongs to its waiting action; use that action's Stop, or audio.start for independent playback";
            if(!v.RoomOwned)return false;
            error="The sound already ended; start a new playback instance";
            if(v.Closed&&operation!="stop")return false;
            error="Room audio controls are paused";
            if(!isActiveAndEnabled||paused||!focused||editor.RuntimeGate.Held)return false;
            error=null;return true;
        }
        internal bool Control(string target,string id,int revision,string operation,float gain,out AudioInstanceSample result,out string error)
        {
            result=default;
            if(!CanControl(target,id,revision,operation,out var v,out error))return false;
            if(operation=="gain"&&!RoomAudioDefinition.Range(gain,0,1)){error="Sound gain must be between zero and one";return false;}
            if(operation is not ("pause" or "resume" or "stop" or "gain")){error="Unknown sound control";return false;}
            if(operation=="stop"){Close(v,null,"stopped");result=Sample(v);return true;}
            if(!v.Output||v.Stream==null){error="The native sound renderer is unavailable";return false;}
            switch(operation){
                case "pause" when !v.Paused:
                    v.Stream.SetPaused(true);v.Output.Pause();v.Paused=true;v.Phase="paused";Note(v);break;
                case "resume" when v.Paused:
                    v.Paused=false;v.ProgressAt=Time.realtimeSinceStartupAsDouble;v.Stream.SetPaused(false);v.Output.UnPause();v.Phase="playing";Note(v);break;
                case "gain" when v.Gain!=gain:
                    v.Gain=gain;v.Output.volume=gain;Note(v);break;
            }
            result=Sample(v);return true;
        }
    }
}
