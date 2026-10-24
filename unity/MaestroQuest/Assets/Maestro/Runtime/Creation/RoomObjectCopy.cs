// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    public sealed partial class RoomEditor
    {
        internal bool CanCopyObject(string source,int revision,out string error)
        {
            if(!CanCreatePrimitive(out error)||!CanEditObject(source,true,out error))return false;
            if(!Frame.Read(Find(source).transform,out _,out _,out _)){error="The source needs a valid uniform room frame before copying";return false;}
            if(ObjectRevision(source)!=revision){error="The source changed; inspect its current definition before copying";return false;}
            if(GetComponent<AnimationWorkshop>()?.ControlsTarget(source)==true){error="Finish authoring this object before copying";return false;}
            return true;
        }
        internal bool CopyObject(string source,int revision,string name,Vector3 position,out string id,out string error)
        {
            id=null;if(!PrepareCopyObject(source,revision,name,position,out var data,out error))return false;
            return CommitCreatedObject(data,out id,out error);
        }
        bool PrepareCopyObject(string source,int revision,string name,Vector3 position,out RoomObjectData data,out string error)
        {
            data=null;if(!CanCopyObject(source,revision,out error))return false;
            data=Pose(Read(source),Find(source).transform);var translation=position-data.position;
            data.id=Guid.NewGuid().ToString("N");data.position=position;if(!string.IsNullOrEmpty(name))data.name=name;
            // Recorded paths use room coordinates. Copy the same translation, retaining
            // key times, rotations, scales and loop; never alter the source's frames.
            if(data.motion!=null)foreach(var frame in data.motion.frames)frame.position+=translation;
            // A copy retains editable tracks but cannot silently start another actor.
            if(data.recipe!=null)data.recipe.playing=false;
            return true;
        }
    }
}
