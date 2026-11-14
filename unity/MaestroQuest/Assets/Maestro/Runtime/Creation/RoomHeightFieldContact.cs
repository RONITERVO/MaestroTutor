// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using UnityEngine;
namespace Maestro.Quest.Creation {
    public sealed partial class RoomEditor {
        internal bool FindHeightField(Ray ray,float maximum,string exclude,out string target,out Vector2 local,out float distance){
            target=null;local=default;distance=maximum;
            if(!float.IsFinite(ray.origin.sqrMagnitude)||!float.IsFinite(ray.direction.sqrMagnitude)||ray.direction.sqrMagnitude<.00001f||!float.IsFinite(maximum)||maximum<0)return false;
            ray.direction=ray.direction.normalized;Physics.SyncTransforms();
            foreach(var pair in objects){
                if(pair.Key==exclude||!pair.Value||!pair.Value.isActiveAndEnabled)continue;
                var view=pair.Value.GetComponent<HeightFieldView>();
                if(!view||!view.Collision||!view.Collision.enabled||!view.Collision.Raycast(ray,out var hit,distance)||Vector3.Dot(hit.normal,view.Surface.up)<=0)continue;
                var p=view.Surface.InverseTransformPoint(hit.point);target=pair.Key;local=new Vector2(p.x,p.z);distance=hit.distance;
            }
            var tool=Find(exclude);
            if(target!=null&&drawingOcclusion.Clear(ray,distance,Find(target).transform,tool?tool.transform:null))return true;
            target=null;local=default;distance=maximum;return false;
        }
        internal bool FindHeightFieldNear(Vector3 point,string exclude,out string target,out Vector2 local){
            target=null;local=default;if(!float.IsFinite(point.sqrMagnitude))return false;float closest=.015f;Physics.SyncTransforms();
            foreach(var pair in objects){
                if(pair.Key==exclude||!pair.Value||!pair.Value.isActiveAndEnabled)continue;
                var view=pair.Value.GetComponent<HeightFieldView>();if(!view||!view.Surface||!view.Collision||!view.Collision.enabled||!view.Surface.gameObject.activeInHierarchy||view.Accepted==null)continue;
                var source=view.Accepted;var p=view.Surface.InverseTransformPoint(point);
                if(Mathf.Abs(p.x)>source.width/2||Mathf.Abs(p.z)>source.depth/2)continue;
                var at=new Vector2(p.x,p.z);var world=view.Surface.TransformPoint(new Vector3(p.x,source.HeightAt(at),p.z));float gap=Vector3.Distance(point,world);
                if(gap>closest)continue;
                var origin=point+view.Surface.up*.02f;var delta=world-origin;var tool=Find(exclude);
                if(!drawingOcclusion.Clear(new Ray(origin,delta.normalized),delta.magnitude,pair.Value.transform,tool?tool.transform:null))continue;
                target=pair.Key;local=at;closest=gap;
            }
            return target!=null;
        }
        internal SpatialSculpting Sculpting {get{var value=GetComponent<SpatialSculpting>();if(!value){value=gameObject.AddComponent<SpatialSculpting>();value.Editor=this;}return value;}}
        internal bool SculptingInProgress=>GetComponent<SpatialSculpting>() is SpatialSculpting value&&value.Busy;
        internal bool SculptMode=>GetComponent<SpatialSculpting>()?.Enabled==true;
    }
}
