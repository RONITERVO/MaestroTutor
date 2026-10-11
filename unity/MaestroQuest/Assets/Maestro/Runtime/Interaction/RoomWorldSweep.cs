// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    /// <summary>Read-only admission for a complete frame transfer. Continuous box
    /// envelopes conservatively cover translation and yaw arcs; queries never move
    /// a live collider, wake bodies or dispatch contacts while preparing a move.</summary>
    internal sealed class RoomWorldSweep
    {
        const float Skin=.001f;
        const int MaximumQueries=8192;
        static readonly int[] PhysicalLayers={RoomPhysicsLayers.Item,RoomPhysicsLayers.Environment,RoomPhysicsLayers.Controller,RoomPhysicsLayers.Scanned};
        readonly List<Collider> shapes=new();
        readonly Collider[] overlaps=new Collider[128];
        readonly RaycastHit[] hits=new RaycastHit[128];
        int queries;
        Transform content;
        IReadOnlyDictionary<Rigidbody,bool> physical;
        bool Obstacle(Collider other,Collider own) {
            if(!other||other==own||other.isTrigger||Physics.GetIgnoreLayerCollision(own.gameObject.layer,other.gameObject.layer)||Physics.GetIgnoreCollision(own,other))return false;
            if(!other.transform.IsChildOf(content))return true;
            return other.attachedRigidbody&&physical.TryGetValue(other.attachedRigidbody,out var fixedPose)&&fixedPose;
        }
        bool Clear(Collider own,Vector3 start,Vector3 end,Vector3 extents,int mask,out string error) {
            error="World movement needs more clearance from a physical surface or anchored item";
            // A millimetre of contact tolerance avoids trapping a resting object
            // on its supporting floor. This is not a point sample or a teleport.
            extents=new Vector3(Mathf.Max(.00001f,extents.x-Skin),Mathf.Max(.00001f,extents.y-Skin),Mathf.Max(.00001f,extents.z-Skin));
            if((queries+=3)>MaximumQueries){error="This world movement exceeds the physical-clearance query budget";return false;}
            for(int endpoint=0;endpoint<2;endpoint++) {
                var point=endpoint==0?start:end;
                int count=Physics.OverlapBoxNonAlloc(point,extents,overlaps,Quaternion.identity,mask,QueryTriggerInteraction.Ignore);
                if(count==overlaps.Length){error="The physical path is too crowded to verify world movement";return false;}
                for(int i=0;i<count;i++)if(Obstacle(overlaps[i],own))return false;
            }
            var delta=end-start;
            if(delta.sqrMagnitude>1e-12f) {
                int count=Physics.BoxCastNonAlloc(start,extents,delta.normalized,hits,Quaternion.identity,delta.magnitude,mask,QueryTriggerInteraction.Ignore);
                if(count==hits.Length){error="The physical path is too crowded to verify world movement";return false;}
                for(int i=0;i<count;i++)if(Obstacle(hits[i].collider,own))return false;
            }
            error=null;return true;
        }
        internal bool CanMove(Transform frame,RoomPhysicsWorld world,IReadOnlyDictionary<Rigidbody,bool> fixedBodies,Vector3 position,Quaternion rotation,out string error) {
            error=null;if(!world||!world.Running)return true;
            content=frame;physical=fixedBodies;queries=0;shapes.Clear();
            var path=new RoomWorldPath(frame.position,frame.rotation,position,rotation);
            if(!path.Valid){error="World movement needs a finite upright path";return false;}
            if(path.Unchanged)return true;
            // A real participant waiting for alignment must not be silently moved
            // while virtual-only participants continue their own simulation.
            foreach(var pair in fixedBodies) {
                if(pair.Value||!pair.Key||!pair.Key.gameObject.activeInHierarchy)continue;
                var item=pair.Key.GetComponent<RoomItem>();if(!item||!world.IncludesRealRoom(item))continue;
                if(!world.SurfacesReady){error="Load and align the real room before moving its participating objects";return false;}
                for(int step=0;step<=path.Steps;step++)if(!world.ContainsEnvironment(path.Point(pair.Key.position,(float)step/path.Steps),true)){
                    error="World movement would take a participating object outside its real room";return false;
                }
            }
            frame.GetComponentsInChildren(false,shapes);
            foreach(var own in shapes) {
                if(!own||!own.enabled||own.isTrigger)continue;
                var body=own.attachedRigidbody;
                if(body&&fixedBodies.TryGetValue(body,out var anchored)&&anchored)continue;
                var item=body?body.GetComponent<RoomItem>():null;
                int mask=0;
                foreach(int layer in PhysicalLayers)
                    if(!Physics.GetIgnoreLayerCollision(own.gameObject.layer,layer)&&(layer!=RoomPhysicsLayers.Scanned||world.IncludesRealRoom(item)))mask|=1<<layer;
                if(mask==0)continue;
                var bounds=own.bounds;
                if(!Creation.RoomRecipe.Finite(bounds.center)||!Creation.RoomRecipe.Finite(bounds.extents)||bounds.extents.sqrMagnitude<=0){error="An object has no finite physical-clearance bounds";return false;}
                for(int step=0;step<path.Steps;step++) {
                    path.Envelope(bounds,step,out var start,out var end,out var extents);
                    if(!Clear(own,start,end,extents,mask,out error))return false;
                }
            }
            error=null;return true;
        }
    }
}
