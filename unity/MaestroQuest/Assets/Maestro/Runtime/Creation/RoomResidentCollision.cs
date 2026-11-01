// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation {
    public sealed partial class RoomEditor {
        // Retain accepted collision extents, never a second authored definition or
        // a visual approximation. Identity, geometry source and live placement
        // must all remain provable before a failed model can be isolated locally.
        sealed class ResidentCollision {
            internal RoomItem Item;internal CreatedRoomObject Model;
            internal string Source,CurrentSource;internal int Revision;
            internal RoomSpatialExtent Local;internal bool Available;
            internal RoomCollisionGuard Guard;
        }
        readonly Dictionary<string,ResidentCollision> residentCollisions=new(StringComparer.Ordinal);
        static bool ResidentGeometryReady(RoomItem item,CreatedRoomObject model)=>item&&item.isActiveAndEnabled&&model&&model.isActiveAndEnabled&&model.ModelGeometryReady;
        bool CurrentResidentCollision(string target,ResidentCollision entry) {
            if(!entry.Item||Find(target)!=entry.Item||!regionModels.TryGetValue(target,out var model)||!ReferenceEquals(model,entry.Model))return false;
            int revision=ObjectRevision(target);
            if(entry.CurrentSource==null||entry.Revision!=revision){
                var data=Read(target);if(data==null)return false;
                entry.CurrentSource=SpatialSource(data);entry.Revision=revision;
            }
            return entry.Source==entry.CurrentSource&&entry.Local.Known;
        }
        bool TryRetainedResidentCollision(string target,out RoomSpatialExtent local) {
            local=default;
            if(!residentCollisions.TryGetValue(target,out var entry)||!CurrentResidentCollision(target,entry))return false;
            local=entry.Local;return true;
        }
        bool ResidentGeometryLost(string target)=>residentCollisions.TryGetValue(target,out var entry)&&CurrentResidentCollision(target,entry)&&!ResidentGeometryReady(entry.Item,entry.Model);
        internal RoomCollisionGuard ResidentCollisionGuard(string target)=>target!=null&&residentCollisions.TryGetValue(target,out var entry)?entry.Guard:null;
        internal RoomCollisionGuard CollisionGuard(string target)=>DormantCollisionGuard(target)??ResidentCollisionGuard(target);
        bool RefreshResidentCollisions() {
            bool changed=false;
            foreach(var target in residentCollisions.Keys.Where(id=>!regionModels.ContainsKey(id)||!Find(id)).ToArray()){
                residentCollisions[target].Guard?.Dispose();residentCollisions.Remove(target);changed=true;
            }
            foreach(var pair in regionModels.ToArray()){
                var item=Find(pair.Key);var model=pair.Value;bool ready=ResidentGeometryReady(item,model);
                if(model)model.RefreshModelCollisionAvailability(ready);
                else if(item){foreach(var shape in item.Grab.colliders)if(shape)shape.enabled=false;item.GetComponent<RigidRoomItem>()?.Refresh();}
                residentCollisions.TryGetValue(pair.Key,out var entry);
                if(entry!=null&&(!ReferenceEquals(entry.Item,item)||!ReferenceEquals(entry.Model,model))){
                    entry.Guard?.Dispose();residentCollisions.Remove(pair.Key);entry=null;changed=true;
                }
                if(ready){
                    var local=RoomSpatialBounds.CaptureCollision(item,RoomObjectKind.ImportedModel);
                    if(local.Known){
                        if(entry==null){entry=new(){Item=item,Model=model};residentCollisions.Add(pair.Key,entry);changed=true;}
                        int revision=ObjectRevision(pair.Key);
                        if(entry.CurrentSource==null||entry.Revision!=revision){entry.CurrentSource=SpatialSource(Read(pair.Key));entry.Revision=revision;}
                        entry.Source=entry.CurrentSource;entry.Local=local;
                    }else if(entry!=null)entry.Local=default;
                }
                if(entry==null)continue;
                changed|=entry.Available!=ready;entry.Available=ready;
                if(ready){if(entry.Guard!=null){entry.Guard.Dispose();entry.Guard=null;changed=true;}continue;}
                var extent=ReadCollisionBounds(pair.Key);
                if(!extent.Known){if(entry.Guard!=null){entry.Guard.Dispose();entry.Guard=null;changed=true;}continue;}
                entry.Guard??=new RoomCollisionGuard(this,pair.Key);
                uint generation=entry.Guard.Generation;entry.Guard.Synchronize(extent);
                changed|=generation!=entry.Guard.Generation;
            }
            return changed;
        }
        void ReleaseResidentCollisions(bool forget=true){
            foreach(var entry in residentCollisions.Values){entry.Guard?.Dispose();entry.Guard=null;}
            if(forget)residentCollisions.Clear();
        }
    }
}
