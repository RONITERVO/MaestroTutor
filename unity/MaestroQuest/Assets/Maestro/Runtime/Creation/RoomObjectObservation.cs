// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Interaction;
using UnityEngine;

namespace Maestro.Quest.Creation
{
    public sealed partial class RoomJournal
    {
        // Project only the object-list fields. Copying recipes, strokes and animation
        // data here made the four-times-per-second book update grow with room detail.
        // Observations own their data; never lend mutable journal objects to callers.
        Color ObservedAppearanceColor(RoomObjectData item) {
            var root=item.appearanceBindings.FirstOrDefault(b=>b.kind=="root");
            if(root!=null&&appearances.TryGetValue(root.appearanceId,out var style)&&UnityEngine.ColorUtility.TryParseHtmlString(root.tint==""?style.style.tint:root.tint,out var color))return color;
            return item.color;
        }
        internal RoomAgentObject[] ObserveObjects() => items.Values
            .OrderBy(item=>item.id,StringComparer.Ordinal)
            .Select(item=>new RoomAgentObject {
                id=item.id,objectRevision=ObjectRevision(item.id),name=item.name??item.kind.ToString(),kind=item.kind.ToString(),
                position=item.position,scale=item.scale,color=ObservedAppearanceColor(item),physics=RoomControls.Physics(item),
                movement=item.kind==RoomObjectKind.Maestro ? new AvatarMovementSettings {
                    distance=item.followDistance==0 ? 1.3f : item.followDistance,speed=item.walkSpeed==0 ? .65f : item.walkSpeed
                } : null
            }).ToArray();
    }
    public sealed partial class RoomEditor
    {
        internal RoomAgentObject[] ObserveObjects()
        {
            var result=journal.ObserveObjects();var frame=Frame;
            foreach(var value in result) {
                var item=Find(value.id);if(!item)continue;
                if(frame.Valid)value.position=frame.PointToRoom(item.transform.position);
                value.held=item.Grab&&item.Grab.isSelected;
                value.simulating=item.GetComponent<RigidRoomItem>()?.Simulating??false;
                value.animated=item.GetComponent<RecipeObject>()?.IsPlaying??false;
            }
            return result;
        }
    }
}
