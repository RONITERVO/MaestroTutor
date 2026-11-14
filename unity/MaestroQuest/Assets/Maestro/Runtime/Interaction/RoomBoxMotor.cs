// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using UnityEngine;
namespace Maestro.Quest.Interaction {
    internal sealed class RoomBoxMotor {
        readonly Collider[] overlaps=new Collider[64];
        readonly RaycastHit[] hits=new RaycastHit[64];
        bool Empty(Vector3 point,Vector3 extents,int mask,Func<Collider,bool> blocks){
            int count=Physics.OverlapBoxNonAlloc(point,extents,overlaps,Quaternion.identity,mask,QueryTriggerInteraction.Ignore);
            if(count==overlaps.Length)return false;
            for(int i=0;i<count;i++)if(overlaps[i]&&!RoomItem.IsPointerOnly(overlaps[i])&&blocks(overlaps[i]))return false;
            return true;
        }
        internal bool Clear(RoomSweptBody body,int mask,Func<Collider,bool> blocks){
            if(!Empty(body.From,body.Extents,mask,blocks)||!Empty(body.To,body.Extents,mask,blocks))return false;
            var delta=body.To-body.From;if(delta.sqrMagnitude<1e-10f)return true;
            int count=Physics.BoxCastNonAlloc(body.From,body.Extents,delta.normalized,hits,Quaternion.identity,delta.magnitude,mask,QueryTriggerInteraction.Ignore);
            if(count==hits.Length)return false;
            for(int i=0;i<count;i++)if(hits[i].collider&&!RoomItem.IsPointerOnly(hits[i].collider)&&blocks(hits[i].collider))return false;
            return true;
        }
        internal void Release(){Array.Clear(overlaps,0,overlaps.Length);Array.Clear(hits,0,hits.Length);}
    }
}
