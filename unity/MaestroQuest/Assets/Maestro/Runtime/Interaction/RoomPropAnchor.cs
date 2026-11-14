// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Rules;
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    /// <summary>Immutable anchor identity. Revision is an admission guard; live placement is allowed to change.</summary>
    public sealed class RoomPropAnchor
    {
        public readonly string Kind,HolderId,Part,AvatarHash;public readonly int Revision;
        public RoomPropAnchor(string kind,string holder,string part,int revision,string avatarHash=""){Kind=kind;HolderId=holder;Part=part;Revision=revision;AvatarHash=avatarHash;}
        public static RoomPropAnchor Avatar(PropAttachment attachment)=>new("avatarHand","maestro",attachment.Hand==PropHand.Left?"left":"right",0,attachment.AvatarHash??"");
        public bool Resolve(RoomEditor editor,out RoomItem holder,out Transform socket,out string error,bool checkRevision=true)
        {
            holder=editor?editor.Find(HolderId):null;socket=null;error="The prop holder is unavailable or held";
            if(!holder||!holder.isActiveAndEnabled||holder.Grab.isSelected||editor.RuntimeGate.Held)return false;
            if(Kind=="avatarHand"){
                var avatar=holder.GetComponent<MaestroAvatar>();
                if(HolderId!="maestro"||!avatar||avatar.ModelBusy||!avatar.PoseRig||(avatar.ModelHash??"")!=(AvatarHash??"")||Part is not ("left" or "right")){error="Choose Maestro's current avatar and fit the prop again";return false;}
                socket=avatar.PoseRig.Bone(Part=="left"?PoseJoint.LeftHand:PoseJoint.RightHand);
            }else{
                if(checkRevision&&editor.ObjectRevision(HolderId)!=Revision){error="The prop holder changed; read its current revision";return false;}
                if(Kind=="recipePart"){var recipe=holder.GetComponent<RecipeObject>();socket=recipe&&recipe.isActiveAndEnabled?recipe.Part(Part):null;}
                else if(Kind=="object")socket=holder.transform;
                else {error="Unknown prop anchor kind";return false;}
            }
            if(!socket){error="The holder has no matching attachment point";return false;}error=null;return true;
        }
        public bool Matches(RoomEditor editor,RoomItem expected,Transform socket,out string error)
        {
            if(!Resolve(editor,out var holder,out var live,out error,false))return false;
            if(holder==expected&&live==socket)return true;error="The prop holder or its attachment point changed";return false;
        }
    }
}
