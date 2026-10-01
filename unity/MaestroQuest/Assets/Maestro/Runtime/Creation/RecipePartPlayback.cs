// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RecipeObject
    {
        // A named local rotation is independent of its parent's local rotation.
        // Ancestor motion still carries descendants, just as in the saved hierarchy.
        internal sealed class PartPlayback
        {
            internal RecipeTrack Track;internal bool Loop,Active=true;internal float Time;
        }
        readonly Dictionary<string,PartPlayback> partsPlaying=new();
        readonly HashSet<string> suppressedParts=new();
        internal bool HasTrack(string part)=>recipe!=null&&System.Array.Exists(recipe.tracks,t=>t.part==part)&&Part(part);
        internal bool PartPlaying(string part)=>partsPlaying.ContainsKey(part)||WholePlaying&&!suppressedParts.Contains(part)&&HasTrack(part);
        internal bool BeginPart(string part,bool loop,out PartPlayback playback,out string error)
        {
            playback=null;error="This recipe part has no saved animation track";
            if(!HasTrack(part))return false;
            if(!isActiveAndEnabled||runtimeGate?.Held==true){error="Recipe playback is paused";return false;}
            if(partsPlaying.ContainsKey(part)){error="This recipe part is already playing";return false;}
            playback=new PartPlayback{Track=System.Array.Find(recipe.tracks,t=>t.part==part),Loop=loop};
            partsPlaying.Add(part,playback);suppressedParts.Add(part);SamplePart(playback,0);error=null;return true;
        }
        internal bool OwnsPart(PartPlayback playback)=>playback!=null&&playback.Active&&isActiveAndEnabled&&runtimeGate?.Held!=true&&partsPlaying.TryGetValue(playback.Track.part,out var active)&&ReferenceEquals(playback,active);
        void SamplePart(PartPlayback playback,float at)=>nodes[playback.Track.part].localRotation=rest[playback.Track.part]*recipe.Sample(playback.Track,at,playback.Loop);
        internal bool FinishPart(PartPlayback playback,float seconds)
        {
            if(!OwnsPart(playback))return false;SamplePart(playback,seconds);return true;
        }
        internal void EndPart(PartPlayback playback)
        {
            if(playback==null)return;
            if(partsPlaying.TryGetValue(playback.Track.part,out var active)&&ReferenceEquals(playback,active))partsPlaying.Remove(playback.Track.part);
            playback.Active=false;
            // Hold the observed pose. No saved edit, root snap or autoplay resume.
        }
        void CancelParts(){foreach(var playback in partsPlaying.Values)playback.Active=false;partsPlaying.Clear();}
        void AdvanceParts(float delta){foreach(var playback in partsPlaying.Values){playback.Time+=delta;SamplePart(playback,playback.Time);}}
    }
}
