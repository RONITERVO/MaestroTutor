// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using UnityEngine;
using Maestro.Quest.Interaction;
namespace Maestro.Quest.Creation {
    public sealed partial class RoomEditor {
        HeightFieldView ApplyHeightFields(string target,RoomItem owner,RoomHeightField[] fields,RoomEditPreparation preparation=null){
            var view=owner.GetComponent<HeightFieldView>();
            if(!view&&(fields?.Length??0)>0){view=owner.gameObject.AddComponent<HeightFieldView>();view.ConfigureResourceOwner(new RoomResourceOwner(WorldIdentity,target,"collision"));}
            if(!view)return null;
            bool hadSurface=view.Collision;var area=view.WorldBounds;
            if(!view.Apply(fields,preparation))return view;
            if(view.Collision){if(hadSurface)area.Encapsulate(view.WorldBounds);else area=view.WorldBounds;}
            // Replacing static mesh geometry does not wake sleeping contacts in
            // PhysX. Wake nearby free bodies once, only after the edit is accepted.
            // The room registry bounds this scan; no per-frame geometry queries.
            area.Expand(.1f);
            foreach(var item in objects.Values){
                if(!item||item==owner||!item.isActiveAndEnabled||!item.Grab||item.Grab.isSelected)continue;
                var rigid=item.GetComponent<RigidRoomItem>();var body=item.GetComponent<Rigidbody>();
                if(!rigid||!rigid.Simulating||rigid.AnimationOwned||!body||!body.IsSleeping())continue;
                foreach(var collider in item.Grab.colliders)
                    if(collider&&collider.enabled&&collider.gameObject.activeInHierarchy&&area.Intersects(collider.bounds)){body.WakeUp();break;}
            }
            return view;
        }
        internal bool PrepareHeightField(string target,int revision,RoomHeightField field,out RoomObjectData data,out string error){
            data=null;if(!ComponentSource(target,revision,out data,out error))return false;
            data.heightFields=field==null?Array.Empty<RoomHeightField>():new[]{field.Copy()};return ComponentCandidate(new[]{data},out error);
        }
        internal bool EditHeightField(string target,int revision,RoomHeightField field,out string error){
            if(!PrepareHeightField(target,revision,field,out var data,out error))return false;
            return CommitPersisted(new[]{data},Array.Empty<string>(),field==null?"Height surface removed":"Height surface saved",false,out error,ComponentBefore(new[]{data}));
        }
        internal bool PrepareSculpt(string target,int revision,string mode,Vector2[] path,float radius,float height,out RoomObjectData data,out int changed,out string error){
            data=null;changed=0;if(!ComponentSource(target,revision,out data,out error))return false;
            var field=data.heightFields?.FirstOrDefault();if(field==null){error="Configure a height surface on this object first";return false;}
            if(!field.Sculpt(mode,path,radius,height,out changed,out error))return false;return ComponentCandidate(new[]{data},out error);
        }
        internal bool SculptHeightField(string target,int revision,string mode,Vector2[] path,float radius,float height,out int changed,out string error){
            if(!PrepareSculpt(target,revision,mode,path,radius,height,out var data,out changed,out error))return false;
            if(CommitPersisted(new[]{data},Array.Empty<string>(),"Surface sculpted — one Undo restores its shape",false,out error,ComponentBefore(new[]{data})))return true;changed=0;return false;
        }
    }
}
