// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Collections.Generic;
using Maestro.Quest.Interaction;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        // Start prepares the existing free actors and their local dependency
        // closure, or tracked local ground in a cold virtual-only world.
        // New/observed actors acquire their own inputs before gravity.
        // This query is passive and never wakes unrelated saved content.
        internal string[] NativeSimulationTargets()=>NativeSimulationTargets(out _);
        internal string[] NativeSimulationTargets(out Func<bool> startCurrent)
        {
            startCurrent=()=>true;
            var targets=new HashSet<string>(StringComparer.Ordinal);
            foreach(var item in objects.Values){
                var rigid=item?item.GetComponent<RigidRoomItem>():null;
                if(!item||!item.isActiveAndEnabled||!rigid||!rigid.isActiveAndEnabled||
                    !rigid.Dynamic||!item.Grab||item.Grab.isSelected||!PhysicsIslandOwned(item))continue;
                var query=PhysicsInputs(item);targets.UnionWith(query.Members);targets.UnionWith(query.Targets);
            }
            // A cold virtual world may contain only fixed/animated objects. An
            // explicit Start can prepare ground at the current tracked viewpoint;
            // it must not make every saved object's existence a loading request.
            var viewpoint=GetComponent<WorkspaceViewpoint>();
            if(PhysicsWorld&&!PhysicsWorld.RealCollisions&&!PhysicsWorld.AnySimulationReady&&
                viewpoint&&viewpoint.ReadTrackedViewpoint(out var point)){
                var local=TrackedViewTargets(point,true);targets.UnionWith(local);
                string state=viewpoint.PlacementState();var held=targets.ToHashSet(StringComparer.Ordinal);
                startCurrent=()=>viewpoint&&viewpoint.PlacementState()==state&&viewpoint.ReadTrackedViewpoint(out var current)&&
                    TrackedViewTargets(current,true).All(held.Contains);
            }
            return targets.OrderBy(id=>id,StringComparer.Ordinal).ToArray();
        }
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
