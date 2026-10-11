// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using UnityEngine;
namespace Maestro.Quest.Creation {
    public sealed partial class RoomEditor {
        internal bool PrepareMaterialScoop(string field,string carrier,RoomHeightField original,RoomMaterialStore stored,SculptTip tip,RoomHeightField preview,RoomMaterialStore contents,out RoomObjectData[] data,out string error){
            data=null;if(!ComponentSource(field,ObjectRevision(field),out var surface,out error))return false;
            error="The material tool or surface changed; discard the draft";var current=Read(carrier);var item=Find(carrier);
            if(field==carrier||current==null||current.IsBuiltIn||!item||GetComponent<AnimationWorkshop>()?.ControlsTarget(carrier)==true||JsonUtility.ToJson(surface.heightFields?.FirstOrDefault())!=JsonUtility.ToJson(original)||JsonUtility.ToJson(current.materialStores?.FirstOrDefault())!=JsonUtility.ToJson(stored)||JsonUtility.ToJson(current.sculptTips?.FirstOrDefault())!=JsonUtility.ToJson(tip))return false;
            var held=Pose(current,item.transform);surface.heightFields=new[]{preview.Copy()};held.materialStores=new[]{contents.Copy()};data=new[]{surface,held};return ComponentCandidate(data,out error);
        }
        internal bool CommitMaterialScoop(RoomObjectData[] data,out string error)=>CommitPersisted(data,Array.Empty<string>(),"Material gesture saved — Undo restores both balances",false,out error,ComponentBefore(data));
    }
}
