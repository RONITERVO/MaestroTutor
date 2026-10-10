// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    public sealed partial class RoomPhysicsWorld
    {
        UnityEngine.Object collisionAuthority;
        Func<string> collisionAdmission;
        string observedCollisionIssue;
        bool hasCollisionAuthority;
        internal string CollisionAdmissionIssue=>!hasCollisionAuthority?null:!collisionAuthority?"The authored region is unavailable":collisionAdmission();
        internal bool CollisionReady=>CollisionAdmissionIssue==null;
        internal void ConfigureCollisionAdmission(UnityEngine.Object owner,Func<string> admission)
        {
            if(!owner||admission==null)throw new ArgumentException("Collision admission needs a live region owner");
            // The persistent shell survives workspace replacement. A disabled owner still
            // owns it; only a destroyed predecessor permits a fresh region binding.
            if(hasCollisionAuthority&&collisionAuthority&&collisionAuthority!=owner)throw new InvalidOperationException("A physics world already belongs to an authored region");
            collisionAuthority=owner;collisionAdmission=admission;hasCollisionAuthority=true;RefreshCollisionAdmission();
        }
        internal void RefreshCollisionAdmission()
        {
            string issue=CollisionAdmissionIssue;
            if(issue==observedCollisionIssue)return;
            observedCollisionIssue=issue;
            if(issue!=null)Running=false;
            Status=Running?"Physics on — grip to pick up, release to throw":IdleStatus;
            Notify();
        }
    }
}
