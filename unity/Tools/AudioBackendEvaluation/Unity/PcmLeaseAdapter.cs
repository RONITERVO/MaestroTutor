// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Book;
using UnityEngine;
namespace Maestro.AudioEvaluation
{
    // Exercises the application's actual output lease with the evaluated native
    // owner. The selection owns voices; the separate room driver owns the graph.
    internal sealed class PcmLeaseAdapter : IDisposable
    {
        readonly OwnedLifecycle owner;
        readonly IDisposable selection;
        bool closed;
        internal PcmLeaseAdapter(PcmOutputBackend backend,OwnedLifecycle owner)
        {
            this.owner=owner??throw new ArgumentNullException(nameof(owner));
            if(!backend)throw new ArgumentNullException(nameof(backend));selection=backend.Configure(Open);
        }
        IPcmOutput Open(PcmOutputRequest request,SpeechPcmStream stream)
        {
            if(closed)throw new InvalidOperationException("The candidate PCM backend is closed");
            string id="lease/"+Guid.NewGuid().ToString("N");
            // Bind without consuming queued input before the caller starts it.
            stream.SetPaused(true);
            var voice=owner.BeginPcm(id,owner.Generation,stream,PcmOutputLease.DeviceTail,
                settings:new PcmSpatialSettings(request.Spatial,request.MinDistance,request.MaxDistance),ownership:request.Ownership);
            if(voice==null)throw new InvalidOperationException("The candidate PCM backend is preparing");
            Output output=null;
            try{output=new Output(owner,id,voice);output.Prepare(request);return output;}
            catch{if(output!=null)output.Dispose();else{owner.StopPcm(id,voice);voice.Close();}throw;}
        }
        public void Dispose(){if(closed)return;closed=true;selection.Dispose();}
        sealed class Output : IPcmOutput
        {
            readonly OwnedLifecycle owner;readonly string id;readonly PcmVoice voice;
            GameObject view;bool closed,started,paused;
            internal Output(OwnedLifecycle owner,string id,PcmVoice voice){this.owner=owner;this.id=id;this.voice=voice;}
            internal void Prepare(PcmOutputRequest request)
            {
                view=new GameObject(request.Name);view.transform.SetParent(request.Parent,false);
                SetGain(request.Gain);view.AddComponent<PcmAnchorFollower>().Bind(owner,id,voice);
                if(!Available)throw new InvalidOperationException("The candidate PCM anchor is unavailable");
            }
            public bool Available=>!closed&&view&&view.activeInHierarchy&&owner.Active?.MatchesPcm(id,voice)==true;
            public AudioSource Source=>null;
            public void Start(){if(started)return;started=true;voice.SetPaused(paused);}
            public void SetPaused(bool value){paused=value;voice.SetPaused(!started||paused);}
            public void SetGain(float value){if(!owner.SetPcmGain(id,voice,value))throw new InvalidOperationException("The candidate PCM owner changed");}
            public void SetPose(Vector3 position,Quaternion rotation)
            {
                view.transform.SetPositionAndRotation(position,rotation);
                if(!owner.SetPcmPose(id,voice,PcmTransformBinding.Pose(view.transform)))throw new InvalidOperationException("The candidate PCM owner changed");
            }
            public bool IsDrained(double now)=>Available&&voice.RendererDrained(now);
            public void Dispose()
            {
                if(closed)return;closed=true;owner.StopPcm(id,voice);voice.Close();
                if(view)UnityEngine.Object.Destroy(view);view=null;
            }
        }
    }
}
