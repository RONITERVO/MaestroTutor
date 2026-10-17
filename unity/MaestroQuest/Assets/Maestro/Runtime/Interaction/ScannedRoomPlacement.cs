// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Quest.Creation;
using Meta.XR;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    internal interface IRoomSurfaceSource
    {
        bool Supported {get;}
        Task<bool> Prepare();
        bool Raycast(Ray ray,out Vector3 point,out Vector3 normal);
    }
    public sealed partial class ScannedRoom
    {
        IRoomSurfaceSource surfaceSource;
        internal string SetupIdentity=>setupId;
        internal void SetSurfaceSourceForTests(IRoomSurfaceSource value){surfaceSource=value;NotifySetup();}
        internal bool CanPlace(string expected,out string error)
        {
            error=null;
            if(expected!=setupId)error="Room setup changed; inspect room.environment before placing";
            else if(!world||!world.isActiveAndEnabled||!SetupActive||virtualView||world.RuntimeHeld)error="Return to an active, available mixed-reality room before placing";
            else if(Busy)error="Finish room loading or scanning before placing";
            else if(tracked!=null&&!tracked.IsPressed())error="Head tracking is unavailable";
            else if(surfaceSource?.Supported!=true)error="Live surface placement is unavailable on this device";
            return error==null;
        }
        public async Task<bool> PreparePlacement()
        {
            var expected=setupId;if(!CanPlace(expected,out _))return false;
            bool ready=await surfaceSource.Prepare();return this&&ready&&CanPlace(expected,out _);
        }
        public bool TrySurface(Ray ray,out Vector3 point,out Vector3 normal)
        {
            point=normal=default;
            if(!CanPlace(setupId,out _)||!Finite(ray.origin)||!Finite(ray.direction)||ray.direction.sqrMagnitude<.001f)return false;
            if(!surfaceSource.Raycast(new Ray(ray.origin,ray.direction.normalized),out point,out normal)||!Finite(point)||!Finite(normal)||normal.sqrMagnitude<.9f)return false;
            normal.Normalize();return Vector3.Distance(ray.origin,point)<=4.01f;
        }
        static bool Finite(Vector3 value)=>float.IsFinite(value.x)&&float.IsFinite(value.y)&&float.IsFinite(value.z);
        internal static bool Bounds(RoomItem item,out Bounds bounds)
        {
            bounds=default;if(!item||!item.Grab)return false;
            var colliders=item.Grab.colliders.Where(c=>c&&c.enabled&&!c.isTrigger&&c.gameObject.activeInHierarchy).ToArray();
            if(colliders.Length==0)return false;
            bounds=colliders[0].bounds;foreach(var collider in colliders.Skip(1))bounds.Encapsulate(collider.bounds);
            return Finite(bounds.center)&&Finite(bounds.extents)&&bounds.extents.sqrMagnitude>0;
        }
        internal bool PlaceObject(RoomEditor editor,string target,int revision,string expected,Ray ray,out JObject result,out string error)
        {
            result=null;if(!CanPlace(expected,out error)||!editor||!editor.CanEditObject(target,false,out error))return false;
            if(editor.ObjectRevision(target)!=revision){error="This object changed while placement was preparing";return false;}
            var item=editor.Find(target);Physics.SyncTransforms();
            if(!Bounds(item,out var bounds)){error="This object has no usable collision bounds";return false;}
            if(!TrySurface(ray,out var point,out var normal)||normal.y<.7f){error="No clear level surface detected within four metres; inspect and explicitly try again";return false;}
            float support=Vector3.Dot(new Vector3(Mathf.Abs(normal.x),Mathf.Abs(normal.y),Mathf.Abs(normal.z)),bounds.extents);
            var position=editor.transform.InverseTransformPoint(point+normal*(support+.01f)-(bounds.center-item.transform.position));
            if(!editor.MoveObject(target,position,out error))return false;
            result=new JObject {["target"]=target,["revision"]=editor.ObjectRevision(target),["position"]=Triple(position),["point"]=Triple(editor.transform.InverseTransformPoint(point)),["normal"]=Triple(editor.transform.InverseTransformDirection(normal).normalized),["temporary"]=editor.TemporaryRoom};return true;
        }
        internal static JObject Triple(Vector3 value)=>new() {["x"]=value.x,["y"]=value.y,["z"]=value.z};
        sealed class DeviceSurfaceSource:IRoomSurfaceSource
        {
            readonly ScannedRoom owner;
            public DeviceSurfaceSource(ScannedRoom value){owner=value;}
#if UNITY_ANDROID && !UNITY_EDITOR
            public bool Supported=>owner&&EnvironmentRaycastManager.IsSupported;
            public async Task<bool> Prepare()
            {
                if(!await ScenePermission()||!owner||!owner.CanPlace(owner.setupId,out _))return false;
                if(!owner.liveSurfaces)owner.liveSurfaces=owner.gameObject.AddComponent<EnvironmentRaycastManager>();
                // Give the native surface provider one update to enable. No raycast retry.
                await Task.Yield();return owner&&owner.liveSurfaces;
            }
            public bool Raycast(Ray ray,out Vector3 point,out Vector3 normal)
            {
                point=normal=default;if(!owner||!owner.liveSurfaces||!owner.liveSurfaces.Raycast(ray,out var hit,4)||!float.IsFinite(hit.normalConfidence)||hit.normalConfidence<.5f)return false;
                point=hit.point;normal=hit.normal;return true;
            }
#else
            public bool Supported=>false;
            public Task<bool> Prepare()=>Task.FromResult(false);
            public bool Raycast(Ray ray,out Vector3 point,out Vector3 normal){point=normal=default;return false;}
#endif
        }
    }
}
