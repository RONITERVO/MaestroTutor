// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation
{
    /// <summary>Bounded physical contact query. Logical surface edits do not use this query.</summary>
    internal sealed class DrawingSurfaceOcclusion
    {
        const float Epsilon=.0001f;
        const int Mask=~(1<<RoomPhysicsLayers.Controller);
        readonly Collider[] overlaps=new Collider[64];
        readonly RaycastHit[] hits=new RaycastHit[64];
        public bool Clear(Ray ray,float distance,Transform owner,Transform tool)
        {
            // Grabs, recipe edits and program motion can move colliders after the last physics step.
            Physics.SyncTransforms();
            int count=Physics.OverlapSphereNonAlloc(ray.origin,Epsilon,overlaps,Mask,QueryTriggerInteraction.Ignore);
            if(count==overlaps.Length)return false;
            for(int i=0;i<count;i++)if(Blocks(overlaps[i],owner,tool))return false;
            float length=distance-Epsilon;
            if(length<=0)return true;
            if(!ClearCast(ray,length,owner,tool))return false;
            // Scanned room meshes may be one-sided. Check both sides without changing global physics settings.
            return ClearCast(new Ray(ray.GetPoint(length),-ray.direction),length,owner,tool);
        }
        bool ClearCast(Ray ray,float distance,Transform owner,Transform tool)
        {
            int count=Physics.RaycastNonAlloc(ray,hits,distance,Mask,QueryTriggerInteraction.Ignore);
            // A saturated query may omit the blocking hit, so it cannot establish a clear path.
            if(count==hits.Length)return false;
            for(int i=0;i<count;i++)if(Blocks(hits[i].collider,owner,tool))return false;
            return true;
        }
        static bool Blocks(Collider collider,Transform owner,Transform tool)
        {
            if(!collider||!collider.enabled||!collider.gameObject.activeInHierarchy||collider.isTrigger)return false;
            var at=collider.transform;
            // Explicit patches need not coincide with their owner's approximate collision proxy.
            return !at.IsChildOf(owner)&&(!tool||!at.IsChildOf(tool));
        }
    }
}
