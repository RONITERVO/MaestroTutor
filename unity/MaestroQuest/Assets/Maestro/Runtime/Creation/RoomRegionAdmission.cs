// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        // Current authored scope is one bounded region. Saved references remain
        // valid while loading, but its collision consumers cannot borrow a scan
        // or another floor as proof that missing authored geometry is available.
        readonly Dictionary<string,CreatedRoomObject> regionModels=new();
        string blockedModel,blockedIssue,collisionIssue;
        string RegionCollisionIssue()
        {
            if(!this||!isActiveAndEnabled||journal==null)return "The authored region is unavailable";
            if(dormantNative.Count>0)return "Authored areas must finish native activation before whole-world collision simulation";
            foreach(var entry in regionModels){
                var model=entry.Value;
                if(!model||!model.isActiveAndEnabled||!model.ModelGeometryReady){
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
            PhysicsWorld?.RefreshCollisionAdmission();
            // Refresh may acquire dormant peers synchronously. Iterate this publication
            // snapshot; nested activation publishes and refreshes its own objects.
            foreach(var item in objects.Values.ToArray())if(item)item.GetComponent<Interaction.RigidRoomItem>()?.RefreshAfterPublication();
        }
    }
}
