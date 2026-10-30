// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Maestro.Quest.Creation;
using UnityEngine;
namespace Maestro.Quest.Interaction {
    public sealed partial class RoomNavigation {
        // Transient query keys never become saved object identities. Keep canonical
        // owners alongside collider snapshots for retention and changed-input checks.
        sealed class RouteSolid {
            internal Collider Collision;internal RoomEditor Editor;internal string Target;
            internal Bounds Bounds;internal Matrix4x4 Matrix;internal int Layer;
            internal bool Current=>Collision&&Collision.enabled&&!Collision.isTrigger&&Collision.gameObject.activeInHierarchy&&
                Collision.gameObject.layer==Layer&&Collision.bounds.Equals(Bounds)&&Collision.transform.localToWorldMatrix.Equals(Matrix);
        }
        const int MaximumSolidRouteBodies=64;
        readonly Dictionary<string,RouteSolid> solidRouteBodies=new();
        internal bool CanDetourTraversalBlocker=>TraversalBlocker&&!CurrentGround.Contains(TraversalBlocker);
        bool WalkingActor(Collider collision){
            if(!collision||!collision.enabled||collision.isTrigger||!collision.gameObject.activeInHierarchy)return false;
            var other=collision.GetComponentInParent<RoomItem>();
            return other&&other!=actor&&other.GetComponentInParent<RoomEditor>()==InputEditor&&GroundedMovement.HasWalkingOwner(other);
        }
        internal bool HasWalkingActorBlocker {get{
            if(WalkingActor(TraversalBlocker))return true;
            foreach(var body in solidRouteBodies.Values)if(WalkingActor(body.Collision))return true;
            return false;
        }}
        string RememberRouteSolid(Collider collision){
            string key="solid:"+collision.GetInstanceID();
            if(solidRouteBodies.ContainsKey(key))return key;
            if(solidRouteBodies.Count>=MaximumSolidRouteBodies){WaterBlocker="The walking route contains too many solid obstacles";return null;}
            var item=collision.GetComponentInParent<RoomItem>();var authority=item?item.GetComponentInParent<RoomEditor>():null;
            solidRouteBodies.Add(key,new(){Collision=collision,Editor=authority,Target=authority?authority.Identity(item):null,
                Bounds=collision.bounds,Matrix=collision.transform.localToWorldMatrix,Layer=collision.gameObject.layer});return key;
        }
        bool SolidRouteBodiesCurrent(){foreach(var body in solidRouteBodies.Values)if(!body.Current)return false;return true;}
        bool SolidRouteFootprint(string id,Vector3 reference,List<Vector3> points){
            if(!solidRouteBodies.TryGetValue(id,out var body))return false;
            if(!body.Current)return true;
            var bounds=body.Bounds;bounds.Expand(new Vector3(2*(radius+.08f),0,2*(radius+.08f)));
            var min=bounds.min;var max=bounds.max;var centre=bounds.center;float y=reference.y;
            points.Add(new(min.x,y,min.z));points.Add(new(centre.x,y,min.z));points.Add(new(max.x,y,min.z));points.Add(new(max.x,y,centre.z));
            points.Add(new(max.x,y,max.z));points.Add(new(centre.x,y,max.z));points.Add(new(min.x,y,max.z));points.Add(new(min.x,y,centre.z));return true;
        }
    }
}
