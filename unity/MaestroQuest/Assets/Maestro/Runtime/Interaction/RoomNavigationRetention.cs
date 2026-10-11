// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Creation;
namespace Maestro.Quest.Interaction
{
    public sealed partial class RoomPhysicsWorld
    {
        // The bound world owns its consumers. No global scene search or saved
        // retention flags: disabling/rebinding one consumer releases only its use.
        readonly HashSet<RoomNavigation> navigationConsumers=new();
        internal void RegisterNavigation(RoomNavigation value)=>navigationConsumers.Add(value);
        internal void UnregisterNavigation(RoomNavigation value)=>navigationConsumers.Remove(value);
        internal void RetainNavigation(RoomEditor editor,RoomRetentionGraph graph)
        {
            if(!editor||editor.PhysicsWorld!=this)return;
            foreach(var consumer in navigationConsumers)if(consumer)consumer.RetainDependencies(this,editor,graph);
        }
    }
    public sealed partial class RoomNavigation
    {
        internal void RetainDependencies(RoomPhysicsWorld authority,RoomEditor editor,RoomRetentionGraph graph)
        {
            if(world!=authority||!isActiveAndEnabled)return;
            // Observe held snapshots without Ready/Capture: a diagnostic must not
            // bake a map or replace native work. Pending mixed-world traversal uses
            // candidate ground after the previous map has already been retired.
            accepted.Retain(editor,graph);candidate.Retain(editor,graph);
            if(routeSearch?.Pending==true&&actor&&actor.GetComponentInParent<RoomEditor>()==editor)
                foreach(var id in routeSearch.WaterDependencies)graph.Retain(id,RoomRetentionReason.WaterRoute);
        }
    }
}
