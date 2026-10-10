// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    // Cooperative main-thread budget. A constructor/upload remains indivisible;
    // this bounds batches, not the worst-case duration of an individual unit.
    internal sealed class RoomPreparationBudget
    {
        internal const int MaximumStepsPerFrame=4;
        const double SecondsPerFrame=.002;
        readonly Action validate;
        int frame=-1,steps;
        long started;
        internal RoomPreparationBudget(Action validate){this.validate=validate??throw new ArgumentNullException(nameof(validate));}
        void BeginFrame(){frame=Time.frameCount;steps=0;started=Stopwatch.GetTimestamp();}
        internal async Task Step()
        {
            validate();
            if(frame!=Time.frameCount)BeginFrame();
            if(steps>0&&(steps>=MaximumStepsPerFrame||(Stopwatch.GetTimestamp()-started)/(double)Stopwatch.Frequency>=SecondsPerFrame)){
                do{await Task.Yield();validate();}while(Time.frameCount==frame);
                BeginFrame();
            }
            steps++;
        }
    }
}
