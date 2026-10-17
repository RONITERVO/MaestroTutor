// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        internal bool PrepareCollisionEdit(string target,int revision,CollisionRecipe collision,out RoomObjectData result,out string error) {
            result=null;if(!CurrentSettings(target,revision,true,out error))return false;
            if(GetComponent<AnimationWorkshop>()?.ControlsTarget(target)==true){error="Finish authoring this object before changing collision shapes";return false;}
            if(collision==null){error="Provide a collision recipe";return false;}if(!collision.Validate(out error))return false;
            result=Pose(Read(target),Find(target).transform);result.collision=collision.shapes.Length==0?null:collision.Copy();result.collisionShape=ItemCollider.Automatic;
            var replacement=result;var candidate=Snapshot();candidate.objects=candidate.objects.Select(x=>x.id==target?replacement:x).ToArray();return candidate.Validate(out error);
        }
        internal bool EditCollision(string target,int revision,CollisionRecipe collision,out string error) {
            if(!PrepareCollisionEdit(target,revision,collision,out var data,out error))return false;
            return CommitPersisted(new[]{data},Array.Empty<string>(),"Collision shapes saved",false,out error);
        }
    }
}
