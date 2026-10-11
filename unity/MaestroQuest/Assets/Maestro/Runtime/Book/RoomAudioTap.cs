// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Threading;
using UnityEngine;

namespace Maestro.Quest.Book
{
    /// <summary>Observes the listener mix after spatialization and the reflection
    /// mixer. It neither changes nor records the outgoing samples.</summary>
    [RequireComponent(typeof(AudioListener))]
    internal sealed class RoomAudioTap : MonoBehaviour
    {
        sealed class Binding
        {
            internal readonly AcousticOutputMonitor Monitor;
            internal readonly int Rate;
            internal Binding(AcousticOutputMonitor monitor, int rate) { Monitor = monitor; Rate = rate; }
        }
        Binding binding;
        internal void Bind(AcousticOutputMonitor monitor, int rate) => Volatile.Write(ref binding, new Binding(monitor, rate));
        void OnAudioFilterRead(float[] data, int channels)
        {
            var current = Volatile.Read(ref binding);
            current?.Monitor.Observe(data, channels, current.Rate, AcousticOutputMonitor.Now);
        }
        void OnDestroy() => Volatile.Write(ref binding, null);
    }
}
