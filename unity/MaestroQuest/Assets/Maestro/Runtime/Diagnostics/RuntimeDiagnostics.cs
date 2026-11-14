// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Diagnostics
{
    /// <summary>One process-local sampler. No persistence, network, XR setting changes or per-frame allocations.</summary>
    public sealed class RuntimeDiagnostics : MonoBehaviour
    {
        readonly FrameIntervalWindow frames=new();
        bool focused,paused;double nextReading;JObject cached;
        public bool Active=>isActiveAndEnabled&&focused&&!paused;
        void OnEnable(){focused=Application.isFocused;ResetWindow();}
        void OnDisable()=>ResetWindow();
        void OnApplicationFocus(bool value){focused=value;ResetWindow();}
        void OnApplicationPause(bool value){paused=value;ResetWindow();}
        void ResetWindow(){frames.Clear();cached=null;nextReading=0;}
        void Update(){if(Active)frames.Sample(Time.realtimeSinceStartupAsDouble);}
        public JObject ObserveFrames()
        {
            double now=Time.realtimeSinceStartupAsDouble;
            if(cached==null||now>=nextReading){
                var value=frames.Read(now);nextReading=now+1;
                cached=new JObject {["active"]=Active,["hasSamples"]=value.Samples>0,["samples"]=value.Samples,["seconds"]=value.Seconds,
                    ["milliseconds"]=new JObject {["mean"]=value.MeanMs,["p95"]=value.P95Ms,["max"]=value.MaxMs},
                    ["capacityLimited"]=value.CapacityLimited,["ageSeconds"]=value.AgeSeconds,["editor"]=Application.isEditor};
            }
            return (JObject)cached.DeepClone();
        }
    }
}
