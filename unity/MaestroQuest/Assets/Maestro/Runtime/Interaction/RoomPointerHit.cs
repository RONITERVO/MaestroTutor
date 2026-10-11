// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
using Maestro.Quest.Book;
namespace Maestro.Quest.Interaction
{
    /// <summary>Visual pointing skips invisible creations without disabling their physical colliders.</summary>
    internal static class RoomPointerHit
    {
        static readonly RaycastHit[] buffer=new RaycastHit[128];
        static bool Visible(Collider collider) {
            if(collider.GetComponent<BookPageTarget>())return true;
            var owner=collider.GetComponentInParent<RoomItem>();return !owner||owner.PointerVisible;
        }
        internal static bool Raycast(Ray ray,out RaycastHit hit,float distance,int layers,QueryTriggerInteraction triggers) {
            if(!Physics.Raycast(ray,out hit,distance,layers,triggers))return false;
            if(Visible(hit.collider))return true;
            int count=Physics.RaycastNonAlloc(ray,buffer,distance,layers,triggers);
            // Dense imported assemblies must not make a farther hidden hit win by truncation.
            var hits=count==buffer.Length?Physics.RaycastAll(ray,distance,layers,triggers):buffer;
            if(hits!=buffer)count=hits.Length;
            bool found=false;float closest=float.PositiveInfinity;
            for(int i=0;i<count;i++)if(hits[i].distance<closest&&Visible(hits[i].collider)){hit=hits[i];closest=hit.distance;found=true;}
            return found;
        }
    }
}
