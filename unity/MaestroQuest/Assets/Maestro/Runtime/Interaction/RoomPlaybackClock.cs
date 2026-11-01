// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Maestro.Quest.Interaction {
    // One playback lifetime can have several geometry consumers. Waiting is
    // bounded cumulatively; releasing one consumer cannot resume another's wait.
    internal sealed class RoomPlaybackClock {
        readonly Func<float> now;readonly HashSet<object> waiting=new();
        float last,elapsed,waited;bool started;
        internal event Action<bool> PauseChanged;
        internal RoomPlaybackClock(Func<float> now=null){this.now=now??(()=>Time.unscaledTime);}
        internal bool Paused=>waiting.Count>0;
        internal bool Started=>started;
        internal float Elapsed {get{Sample();return elapsed;}}
        internal void Start(){if(started)return;started=true;last=now();}
        void Sample(){if(!started)return;float current=now(),delta=Mathf.Max(0,current-last);last=current;if(Paused)waited+=delta;else elapsed+=delta;}
        internal void Pause(object owner,bool value){Sample();bool before=Paused;if(value)waiting.Add(owner);else waiting.Remove(owner);if(before!=Paused)PauseChanged?.Invoke(Paused);}
        internal bool Valid(out string error){Sample();error=waited>=30?"Motion input preparation exceeded its 30 second budget; start a new action":null;return error==null;}
        internal float Remaining(float duration)=>Mathf.Max(0,duration-Elapsed);
    }
}
