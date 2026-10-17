// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Maestro.Quest.Book
{
    // Transient identity belongs to a registered geometry owner, not its view or
    // native SDK handle. Active/retained ownership transfers keep this exact key.
    internal sealed class AcousticGeometryKey { }
    internal readonly struct AcousticGeometryBinding
    {
        internal readonly AcousticGeometryKey Key;
        internal readonly IAcousticGeometry Input;
        internal readonly bool Active, HasGeometry, Scanned, Boundary;
        internal readonly int Revision;
        internal readonly Matrix4x4 Pose;
        internal AcousticGeometryBinding(AcousticGeometryKey key, IAcousticGeometry input)
        {
            Key=key??throw new ArgumentNullException(nameof(key));
            Input=input??throw new ArgumentNullException(nameof(input));
            Active=input.Active; HasGeometry=Active && input.HasGeometry;
            Scanned=input.Scanned; Boundary=input.ReflectionBoundary; Revision=input.Revision;
            Pose=HasGeometry?input.Pose:Matrix4x4.identity;
        }
        internal bool SameDefinition(AcousticGeometryBinding other) =>
            ReferenceEquals(Key,other.Key) && ReferenceEquals(Input,other.Input) &&
            Active==other.Active && HasGeometry==other.HasGeometry && Scanned==other.Scanned &&
            Boundary==other.Boundary && Revision==other.Revision;
        internal bool Matches(AcousticGeometryKey key,IAcousticGeometry input)
        {
            if(!ReferenceEquals(Key,key)||!ReferenceEquals(Input,input))return false;
            var current=new AcousticGeometryBinding(key,input);
            return SameDefinition(current)&&Pose.Equals(current.Pose);
        }
    }
    // Passive main-thread description. No mesh read, native upload, model load,
    // simulation, or second authored world. Borrowed inputs are only read while
    // their definition is current; render workers receive copied values instead.
    internal sealed class AcousticGeometryCatalog
    {
        readonly AcousticGeometryBinding[] bindings;
        readonly Dictionary<AcousticGeometryKey,AcousticGeometryBinding> byKey=new();
        internal readonly bool Available;
        internal readonly int PendingBoundaries;
        internal int Count=>bindings.Length;
        internal AcousticGeometryBinding At(int index)=>bindings[index];
        internal static readonly AcousticGeometryCatalog Unavailable=new(false,0,Array.Empty<AcousticGeometryBinding>());
        internal AcousticGeometryCatalog(bool available,int pendingBoundaries,params AcousticGeometryBinding[] inputs)
        {
            if(pendingBoundaries<0||inputs==null)throw new ArgumentException("Invalid acoustic publication");
            Available=available;PendingBoundaries=pendingBoundaries;
            bindings=(AcousticGeometryBinding[])inputs.Clone();
            foreach(var input in bindings)
                if(input.Key==null||input.Input==null||!byKey.TryAdd(input.Key,input))throw new ArgumentException("Duplicate or missing acoustic identity");
        }
        internal bool Matches(AcousticGeometryKey key,IAcousticGeometry input) =>
            byKey.TryGetValue(key,out var prior)&&prior.Matches(key,input);
        internal bool SameDefinitions(AcousticGeometryCatalog other)
        {
            if(ReferenceEquals(this,other))return true;
            if(other==null||Available!=other.Available||PendingBoundaries!=other.PendingBoundaries||Count!=other.Count)return false;
            foreach(var input in bindings)
                if(!other.byKey.TryGetValue(input.Key,out var current)||!input.SameDefinition(current))return false;
            return true;
        }
        internal bool SamePlacements(AcousticGeometryCatalog other)
        {
            if(ReferenceEquals(this,other))return true;
            if(!SameDefinitions(other))return false;
            foreach(var input in bindings)if(!input.Pose.Equals(other.byKey[input.Key].Pose))return false;
            return true;
        }
    }
}
