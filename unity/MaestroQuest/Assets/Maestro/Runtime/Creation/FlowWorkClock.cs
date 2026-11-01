// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
namespace Maestro.Quest.Creation {
    // Select before body preparation/PhysX; retain the opportunity through that
    // frame's LateUpdate liquid sample. Multiple catch-up fixed steps must not
    // rotate away a grant before the rendered frame can sample it.
    [DefaultExecutionOrder(-1000)]
    internal sealed class FlowWorkClock:MonoBehaviour {
        static FlowWorkClock instance;
        double last;int frame=-1;
        internal static void Ensure(){
            if(instance||!Application.isPlaying)return;
            var go=new GameObject("Liquid work clock"){hideFlags=HideFlags.HideAndDontSave};
            DontDestroyOnLoad(go);instance=go.AddComponent<FlowWorkClock>();
            instance.last=Time.fixedUnscaledTimeAsDouble;
        }
        void FixedUpdate(){
            double now=Time.fixedUnscaledTimeAsDouble;
            if(Time.frameCount==frame||now-last<.05)return;
            frame=Time.frameCount;float seconds=(float)(now-last);last=now;
            FlowWorkResources.Advance(seconds);
        }
    }
}
