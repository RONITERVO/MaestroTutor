// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        // Known unavailable geometry uses per-consumer admission and native guards.
        // Unknown bounds/placement still require the conservative global gate.
        readonly Dictionary<string,CreatedRoomObject> regionModels=new();
        string blockedModel,blockedIssue,collisionIssue;
        string RegionCollisionIssue()
        {
            if(!this||!isActiveAndEnabled||journal==null)return "The authored region is unavailable";
            foreach(var entry in regionModels){
                var model=entry.Value;
                if(!ResidentGeometryReady(Find(entry.Key),model)&&!ReadCollisionBounds(entry.Key).Known){
                    string issue=!model||!model.isActiveAndEnabled||string.IsNullOrEmpty(model.ModelGeometryIssue)?"geometry unavailable":model.ModelGeometryIssue;
                    if(blockedModel!=entry.Key||blockedIssue!=issue){blockedModel=entry.Key;blockedIssue=issue;collisionIssue="Imported object "+entry.Key+" is not ready: "+issue;}
                    return collisionIssue;
                }
            }
            return null;
        }
        internal bool SpatialInputsChanging=>applying;
        internal void RefreshRegionCollision(){
            if(applying)return;
            bool changed=isActiveAndEnabled&&RefreshResidentCollisions();
            PhysicsWorld?.RefreshCollisionAdmission();
            // Refresh may acquire dormant peers synchronously. Iterate this publication
            // snapshot; nested activation publishes and refreshes its own objects.
            foreach(var item in objects.Values.ToArray())if(item&&item.GetComponent<Interaction.RigidRoomItem>() is Interaction.RigidRoomItem body){if(changed)body.Refresh();else body.RefreshAfterPublication();}
        }
    }
}
