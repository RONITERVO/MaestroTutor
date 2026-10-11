// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Diagnostics;
using System.Collections.Generic;
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
        internal sealed class Window
        {
            internal int Frame=-1,Steps;internal long Started;
            internal void Begin(){Frame=Time.frameCount;Steps=0;Started=Stopwatch.GetTimestamp();}
        }
        readonly Window window;
        internal RoomPreparationBudget(Action validate,Window window=null){this.validate=validate??throw new ArgumentNullException(nameof(validate));this.window=window??new Window();}
        // MoveNext performs one owned preparation unit. Validation runs before
        // each unit, including after a yielded frame; disposal closes the iterator.
        internal async Task Run(IEnumerable<object> work)
        {
            using var steps=work.GetEnumerator();
            while(true){await Step();if(!steps.MoveNext())return;}
        }
        internal async Task Step()
        {
            validate();
            if(window.Frame!=Time.frameCount)window.Begin();
            if(window.Steps>0&&(window.Steps>=MaximumStepsPerFrame||(Stopwatch.GetTimestamp()-window.Started)/(double)Stopwatch.Frequency>=SecondsPerFrame)){
                do{await Task.Yield();validate();}while(Time.frameCount==window.Frame);
                window.Begin();
            }
            window.Steps++;
        }
    }
}
