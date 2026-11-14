// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RecipeObject
    {
        // Runtime-only, bounded by this recipe's parts. Stopping an animation leaves
        // its last pose visible; retirement must not reset it or restart saved autoplay.
        internal sealed class IdleState
        {
            internal string Source;internal float Time;internal bool Interrupted;internal bool? Loop;
            internal string[] Suppressed;
            internal Dictionary<string,(Vector3 position,Quaternion rotation,Vector3 scale)> Parts;
        }
        internal IdleState CaptureIdleState()
        {
            if(IsPlaying)throw new System.InvalidOperationException("A playing recipe must remain resident");
            return new IdleState{Source=encoded,Time=time,Interrupted=interrupted,Loop=runtimeLoop,Suppressed=suppressedParts.ToArray(),
                Parts=nodes.ToDictionary(p=>p.Key,p=>(p.Value.localPosition,p.Value.localRotation,p.Value.localScale))};
        }
        internal void RestoreIdleState(IdleState state)
        {
            if(state==null||state.Source!=encoded)return;
            time=state.Time;interrupted=state.Interrupted;runtimeLoop=state.Loop;
            suppressedParts.Clear();foreach(var part in state.Suppressed)suppressedParts.Add(part);
            foreach(var part in state.Parts)if(nodes.TryGetValue(part.Key,out var node)){
                node.localPosition=part.Value.position;node.localRotation=part.Value.rotation;node.localScale=part.Value.scale;
            }
        }
    }
}
