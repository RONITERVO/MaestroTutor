// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        // A later edit to another object is not a reason to cancel this action.
        // Keep identities/revisions, never a second copy of recipes or saved state.
        // Shared resources used by the targets are part of the same input guard.
        internal Func<bool> CaptureNativeActionInput(string[] targets)
        {
            var source=journal;int generation=nativeGeneration;
            if(source==null)return ()=>false;
            var ids=targets.Distinct(StringComparer.Ordinal).ToArray();
            var values=ids.Select(Read).ToArray();if(values.Any(v=>v==null))return ()=>false;
            var objects=ids.Select(id=>(id,revision:ObjectRevision(id))).ToArray();
            var appearances=values.SelectMany(v=>v.appearanceBindings).Select(v=>v.appearanceId).Distinct(StringComparer.Ordinal).Select(id=>(id,revision:AppearanceRevision(id))).ToArray();
            var environments=values.Select(v=>v.environmentProfile).Where(id=>!string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).Select(id=>(id,revision:EnvironmentRevision(id))).ToArray();
            var visibility=values.Select(v=>v.visibilityLayer).Where(id=>!string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).Select(id=>(id,revision:VisibilityRevision(id))).ToArray();
            return ()=>this&&isActiveAndEnabled&&ReferenceEquals(journal,source)&&nativeGeneration==generation&&
                objects.All(v=>v.revision!=0&&ObjectRevision(v.id)==v.revision)&&
                appearances.All(v=>AppearanceRevision(v.id)==v.revision)&&
                environments.All(v=>EnvironmentRevision(v.id)==v.revision)&&
                visibility.All(v=>VisibilityRevision(v.id)==v.revision);
        }
        Func<bool> CaptureNativeClosureInput(string[] closure)
        {
            var current=CaptureNativeActionInput(closure);
            var areas=closure.Select(id=>(id,area:RegionFor(id))).ToArray();
            int checkedRevision=Revision;bool sameTopology=true;
            return ()=>{
                if(!current())return false;
                if(checkedRevision!=Revision){
                    checkedRevision=Revision;
                    sameTopology=areas.All(v=>RegionFor(v.id)==v.area)&&journal.RetentionGraph().Closure(closure).SequenceEqual(closure);
                }
                return sameTopology;
            };
        }
    }
}
