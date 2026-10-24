// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation {
    // A conservative native barrier is not an authored object or a real contact.
    // It has no renderer, grab component, ground provider or acoustic identity.
    internal sealed class RoomCollisionProxy:MonoBehaviour {
        internal RoomEditor Owner;internal string Target;internal uint Generation;
    }
    internal sealed class RoomCollisionGuard:IDisposable {
        readonly RoomEditor owner;readonly string target;BoxCollider shape;bool disposed;uint generation;
        internal bool Known {get;private set;}
        internal bool HasBounds {get;private set;}
        internal BoxCollider Shape=>shape;
        internal uint Generation=>generation;
        internal long Synchronizations {get;private set;}
        internal RoomCollisionGuard(RoomEditor owner,string target){this.owner=owner;this.target=target;}
        internal void Synchronize(RoomSpatialExtent value){
            if(disposed)return;Synchronizations++;
            Known=value.Known&&(!value.HasBounds||RoomRecipe.Finite(value.Bounds.center)&&RoomRecipe.Finite(value.Bounds.size)&&value.Bounds.size.x>=0&&value.Bounds.size.y>=0&&value.Bounds.size.z>=0);
            HasBounds=Known&&value.HasBounds;
            if(!owner||!HasBounds){if(shape&&shape.enabled){shape.enabled=false;shape.GetComponent<RoomCollisionProxy>().Generation=++generation;}return;}
            if(!shape){
                var root=new GameObject("Pending collision / "+target);root.SetActive(false);root.transform.SetParent(owner.transform,false);
                root.layer=RoomPhysicsLayers.Pending;var marker=root.AddComponent<RoomCollisionProxy>();marker.Owner=owner;marker.Target=target;
                shape=root.AddComponent<BoxCollider>();root.SetActive(true);
            }
            // A small conservative thickness also represents planar extents.
            var size=value.Bounds.size+Vector3.one*.004f;
            if(!shape.enabled||shape.center!=value.Bounds.center||shape.size!=size)shape.GetComponent<RoomCollisionProxy>().Generation=++generation;
            if(shape.center!=value.Bounds.center)shape.center=value.Bounds.center;
            if(shape.size!=size)shape.size=size;
            shape.enabled=true;
        }
        public void Dispose(){
            if(disposed)return;disposed=true;Known=HasBounds=false;
            if(shape){shape.enabled=false;shape.gameObject.SetActive(false);UnityEngine.Object.Destroy(shape.gameObject);}shape=null;
        }
    }
    public sealed partial class RoomEditor {
        // Only physical anchors need fresh projection every frame. Accepted edits
        // and retirement synchronously publish all other guard changes before
        // native geometry is detached or the edited state becomes usable.
        readonly HashSet<string> anchoredCollisionTargets=new(StringComparer.Ordinal);
        string[] anchoredCollisionSnapshot=Array.Empty<string>();bool anchoredCollisionChanged;
        internal RoomCollisionGuard DormantCollisionGuard(string target)=>target!=null&&dormantNative.TryGetValue(target,out var entry)?entry.CollisionGuard:null;
        void SynchronizeDormantCollision(string target,DormantEntity entry){
            int revision=ObjectRevision(target);
            if(entry.CollisionGuard!=null&&entry.CollisionRevision==revision&&entry.ScanPlacement==null)return;
            var collision=ReadDormantSpatial(target,entry,out _).Collision;
            entry.CollisionGuard??=new RoomCollisionGuard(this,target);
            entry.CollisionGuard.Synchronize(collision);entry.CollisionRevision=revision;
            bool changed=entry.ScanPlacement!=null?anchoredCollisionTargets.Add(target):anchoredCollisionTargets.Remove(target);
            anchoredCollisionChanged|=changed;
        }
        internal void RefreshAnchoredDormantCollisions(){
            if(anchoredCollisionChanged){anchoredCollisionSnapshot=anchoredCollisionTargets.ToArray();anchoredCollisionChanged=false;}
            foreach(var target in anchoredCollisionSnapshot)
                if(dormantNative.TryGetValue(target,out var entry))SynchronizeDormantCollision(target,entry);
            // Authored metadata is visual-only. Completing its preparation cannot
            // make unknown collision geometry known or create a physical barrier.
        }
        void ReleaseDormantCollision(string target,DormantEntity entry){
            anchoredCollisionChanged|=anchoredCollisionTargets.Remove(target);
            entry.CollisionGuard?.Dispose();entry.CollisionGuard=null;entry.CollisionRevision=-1;
        }
        void ReleaseDormantCollisions(){
            foreach(var entry in dormantNative.Values){entry.CollisionGuard?.Dispose();entry.CollisionGuard=null;entry.CollisionRevision=-1;}
            anchoredCollisionTargets.Clear();anchoredCollisionSnapshot=Array.Empty<string>();anchoredCollisionChanged=false;
        }
    }
}
