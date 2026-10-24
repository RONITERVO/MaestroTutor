// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Book
{
    internal readonly struct PcmOutputRequest
    {
        internal readonly Transform Parent;
        internal readonly object Ownership;
        internal readonly string Name;
        internal readonly bool Spatial;
        internal readonly float Gain,MinDistance,MaxDistance;
        internal readonly int Priority;
        internal PcmOutputRequest(Transform parent,string name,bool spatial,float gain,int priority,float minDistance,float maxDistance,object ownership)
        {
            if(!parent||string.IsNullOrEmpty(name)||!float.IsFinite(gain)||gain<0||gain>1||priority<0||priority>256
                ||!float.IsFinite(minDistance)||!float.IsFinite(maxDistance)||minDistance<=0||maxDistance<minDistance)
                throw new ArgumentException("Invalid native PCM emitter configuration.");
            Ownership=ownership??throw new ArgumentNullException(nameof(ownership));
            Parent=parent;Name=name;Spatial=spatial;Gain=gain;Priority=priority;MinDistance=minDistance;MaxDistance=maxDistance;
        }
    }
    internal interface IPcmOutput : IDisposable
    {
        bool Available {get;}
        // Optional per-voice Unity source, used only by the current direct adapter.
        AudioSource Source {get;}
        void Start();
        void SetPose(Vector3 position,Quaternion rotation);
        void SetPaused(bool value);
        void SetGain(float value);
        bool IsDrained(double now);
    }
    // Explicit room-scoped selection. An unavailable selected backend fails
    // admission; it never silently switches a transferred stream to a fallback.
    public sealed class PcmOutputBackend : MonoBehaviour
    {
        readonly HashSet<PcmOutputLease> leases=new();
        sealed class Selection : IDisposable
        {
            internal readonly PcmOutputBackend Owner;
            internal readonly Func<PcmOutputRequest,SpeechPcmStream,IPcmOutput> Factory;
            internal Selection(PcmOutputBackend owner,Func<PcmOutputRequest,SpeechPcmStream,IPcmOutput> factory){Owner=owner;Factory=factory;}
            public void Dispose(){if(ReferenceEquals(Owner.selection,this)){Owner.selection=null;Owner.CloseLeases();}}
        }
        Selection selection;
        internal IDisposable Configure(Func<PcmOutputRequest,SpeechPcmStream,IPcmOutput> value)
        {
            if(value==null)throw new ArgumentNullException(nameof(value));
            if(leases.Count!=0)throw new InvalidOperationException("Stop active playback before replacing its renderer");
            return selection=new Selection(this,value);
        }
        internal IPcmOutput Open(PcmOutputRequest request,SpeechPcmStream stream)
        {
            if(!isActiveAndEnabled||selection==null)throw new InvalidOperationException("The selected PCM renderer is unavailable");
            var selected=selection;var result=selected.Factory(request,stream);
            if(!isActiveAndEnabled||!ReferenceEquals(selected,selection)){
                result?.Dispose();throw new InvalidOperationException("The selected PCM renderer changed during admission");
            }
            return result;
        }
        internal void Track(PcmOutputLease lease)=>leases.Add(lease);
        internal void Release(PcmOutputLease lease)=>leases.Remove(lease);
        void CloseLeases()
        {
            Exception error=null;
            foreach(var lease in leases.ToArray())try{lease.Dispose();}catch(Exception failure){error??=failure;}
            if(error!=null)Debug.LogException(error);
        }
        void OnDisable()=>CloseLeases();
        void OnDestroy(){selection=null;CloseLeases();}
    }
}
