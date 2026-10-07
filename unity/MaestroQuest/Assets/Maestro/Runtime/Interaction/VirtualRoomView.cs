// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Maestro.Quest.Interaction
{
    /// <summary>Virtual-only presentation over a movable content frame; physical tracking is never moved.</summary>
    public sealed class VirtualRoomView : MonoBehaviour
    {
        Transform content,trackingOrigin;
        RoomWorldMotion motion;
        public string MovementError {get;private set;}
        Camera viewer;
        ScannedRoom scan;
        RoomPhysicsWorld physics;
        ARCameraManager passthrough;
        Color homeBackground;
        CameraClearFlags homeFlags;
        bool passthroughWasEnabled;
        readonly List<RoomWalkableSurface> ground = new();
        readonly RaycastHit[] hits = new RaycastHit[32];
        readonly Collider[] overlaps = new Collider[32];
        public bool Active { get; private set; }
        public bool CanEnter=>isActiveAndEnabled&&motion!=null&&motion.Ready&&trackingOrigin&&viewer&&!viewer.transform.IsChildOf(content)&&(!scan||!scan.Busy);
        public void Initialize(Transform virtualContent,Transform physicalOrigin,Camera camera,ScannedRoom scanned,RoomPhysicsWorld world)
        { content=virtualContent;trackingOrigin=physicalOrigin;motion=new RoomWorldMotion(content,world);viewer=camera;scan=scanned;physics=world;
            passthrough=viewer.GetComponent<ARCameraManager>();world?.GetComponent<RoomNavigation>()?.SetVirtualFrame(content); }
        public bool Enter()
        {
            if (Active) return true;
            if (!CanEnter) return false;
            homeBackground=viewer.backgroundColor; homeFlags=viewer.clearFlags;
            scan?.SetVirtualView(true);
            passthroughWasEnabled=passthrough && passthrough.enabled;
            if (passthrough) passthrough.enabled=false;
            viewer.clearFlags=CameraClearFlags.SolidColor; viewer.backgroundColor=new Color(.91f,.90f,.86f,1);
            Active=true; return true;
        }
        public void Exit()
        {
            if (!Active) return;
            if(viewer) { viewer.backgroundColor=homeBackground; viewer.clearFlags=homeFlags; }
            if (passthrough) passthrough.enabled=passthroughWasEnabled;
            scan?.SetVirtualView(false);
            Active=false;
        }
        public bool Move(Vector3 delta)
        {
            MovementError=null;
            if (!Active || !float.IsFinite(delta.sqrMagnitude) || delta.sqrMagnitude > .01f) return false;
            delta.y=0;
            // Only accepted, scene-owned ground supports travel. Presentation never
            // inserts a second flat collider beneath editable terrain or water.
            Physics.SyncTransforms();
            float foot=trackingOrigin.position.y;
            content.GetComponentsInChildren(false,ground);
            if(!Supported(viewer.transform.position,foot)||!Supported(viewer.transform.position+delta,foot)) {
                MovementError="Your movement needs accepted level ground beneath you and the next step";return false;
            }
            float height = Mathf.Clamp(viewer.transform.position.y-foot,.65f,2.2f), radius=.2f;
            Vector3 bottom=new(viewer.transform.position.x,foot+radius+.04f,viewer.transform.position.z), top=new(viewer.transform.position.x,foot+height-radius,viewer.transform.position.z);
            // The viewer stays physical: this query follows virtual obstacles only.
            int mask=(1<<RoomPhysicsLayers.Item)|(1<<RoomPhysicsLayers.Environment);
            if(physics)mask=physics.CollisionMask(mask);
            int count=Physics.CapsuleCastNonAlloc(bottom,top,radius,delta.normalized,hits,delta.magnitude+.005f,mask,QueryTriggerInteraction.Ignore);
            if (count>0) return false;
            count=Physics.OverlapCapsuleNonAlloc(bottom+delta,top+delta,radius,overlaps,mask,QueryTriggerInteraction.Ignore);
            if (count>0) return false;
            bool moved=motion.SetPose(content.position-delta,content.rotation,out var error);MovementError=error;return moved;
        }
        bool Supported(Vector3 eye,float foot)
        {
            // Terrain-following elevation is a separate locomotion policy. Until
            // implemented, reject hills, holes and ledges instead of floating over
            // them. Sample the complete planar footprint within this owned world.
            for(int sample=0;sample<9;sample++) {
                float angle=(sample-1)*Mathf.PI/4;
                var offset=sample==0?Vector3.zero:new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*.2f;
                var ray=new Ray(new Vector3(eye.x,foot+.05f,eye.z)+offset,Vector3.down);
                bool found=false;
                foreach(var surface in ground) {
                    if(!surface.Available)continue;
                    if(surface.Collision.Raycast(ray,out var hit,.075f)&&hit.normal.y>.999f&&Mathf.Abs(hit.point.y-foot)<.025f) {found=true;break;}
                }
                if(!found)return false;
            }
            return true;
        }
        public bool Turn(float degrees)
        {
            MovementError=null;if(!Active||(degrees!=-30&&degrees!=30))return false;
            var turn=Quaternion.AngleAxis(-degrees,Vector3.up);var pivot=viewer.transform.position;
            bool moved=motion.SetPose(pivot+turn*(content.position-pivot),turn*content.rotation,out var error);MovementError=error;return moved;
        }
        void OnDisable() => Exit();
        void OnDestroy() => Exit();
    }
}
