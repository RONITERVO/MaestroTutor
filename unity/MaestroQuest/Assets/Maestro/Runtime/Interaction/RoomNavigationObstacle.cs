// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
namespace Maestro.Quest.Interaction {
    // Accepted rigid structure contributes its actual mesh to routing, without
    // turning its roof/faces into walkable ground. Preview geometry never publishes.
    internal sealed class RoomNavigationObstacle : MonoBehaviour {
        internal MeshCollider Collision {get;private set;}
        internal uint Revision {get;private set;}
        internal void Publish(MeshCollider collision){Collision=collision;Revision++;}
        internal bool Available=>isActiveAndEnabled&&Collision&&Collision.enabled&&!Collision.isTrigger&&!Collision.convex&&
            Collision.gameObject.activeInHierarchy&&Collision.sharedMesh&&Collision.sharedMesh.vertexCount>0&&
            (!Collision.attachedRigidbody||Collision.attachedRigidbody.isKinematic)&&
            GetComponentInParent<RoomItem>()?.Grab?.isSelected!=true;
    }
}
