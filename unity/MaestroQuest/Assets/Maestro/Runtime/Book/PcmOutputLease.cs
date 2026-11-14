// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;
namespace Maestro.Quest.Book
{
    // Shared ownership boundary for speech and authored sounds. A selected
    // renderer never changes during playback and cannot steal an existing input.
    internal sealed partial class PcmOutputLease : IDisposable
    {
        internal static readonly Func<PcmOutputRequest,SpeechPcmStream,IPcmOutput> CreateDefault=null;
        internal readonly SpeechPcmStream Stream;
        readonly Transform parent;
        readonly PcmOutputBackend backend;
        readonly bool selected;
        IPcmOutput renderer;
        bool closed,started;
        internal AudioSource Source=>renderer?.Source;
        internal bool Available=>!closed&&parent&&parent.gameObject.activeInHierarchy
            &&(!selected||backend&&backend.isActiveAndEnabled)&&renderer?.Available==true;
        internal static double DeviceTail {
            get { AudioSettings.GetDSPBufferSize(out var length,out var buffers);
                return Math.Max(.02,(double)length*Math.Max(1,buffers)/Math.Max(8000,AudioSettings.outputSampleRate)+.02); }
        }
        PcmOutputLease(Transform parent,PcmOutputBackend backend,SpeechPcmStream stream)
        {this.parent=parent;this.backend=backend;selected=!ReferenceEquals(backend,null);Stream=stream;}
        internal static PcmOutputLease Open(Transform parent,string name,SpeechPcmStream stream,
            bool spatial,float gain,int priority,float minDistance,float maxDistance)
        {
            if(stream==null)throw new ArgumentNullException(nameof(stream));
            var ownership=new object();
            if(!stream.TryClaimOutput(ownership))throw new InvalidOperationException("PCM input already has a playback owner");
            PcmOutputLease result=null;
            try {
                var state=stream.Read(double.MinValue);
                if(state.Closed||state.Faulted)throw new InvalidOperationException("PCM input is unavailable");
                var request=new PcmOutputRequest(parent,name,spatial,gain,priority,minDistance,maxDistance,ownership);
                var backend=parent.GetComponentInParent<PcmOutputBackend>(true);
                result=new PcmOutputLease(parent,backend,stream);
                result.renderer=backend?backend.Open(request,stream):CreateDefault?.Invoke(request,stream);
                if(result.renderer==null||!result.Available)throw new InvalidOperationException("The native PCM renderer is unavailable.");
                if(backend)backend.Track(result);
                return result;
            }catch{if(result!=null)result.Dispose();else stream.Close();throw;}
        }
        internal void Start(){RequireAvailable();if(started)return;renderer.Start();started=true;}
        internal void SetPose(Vector3 position,Quaternion rotation){RequireAvailable();renderer.SetPose(position,rotation);}
        internal void SetPaused(bool value){RequireAvailable();renderer.SetPaused(value);}
        internal void SetGain(float value)
        {
            if(!float.IsFinite(value)||value<0||value>1)throw new ArgumentOutOfRangeException(nameof(value));
            RequireAvailable();renderer.SetGain(value);
        }
        internal bool IsDrained(double now)=>Available&&Stream.Read(now).SourceDrained&&renderer.IsDrained(now);
        void RequireAvailable(){if(!Available)throw new InvalidOperationException("The native PCM renderer is unavailable.");}
        public void Dispose()
        {
            if(closed)return;closed=true;Stream.Close();
            try{renderer?.Dispose();}finally{renderer=null;if(!ReferenceEquals(backend,null))backend.Release(this);}
        }
    }
}
