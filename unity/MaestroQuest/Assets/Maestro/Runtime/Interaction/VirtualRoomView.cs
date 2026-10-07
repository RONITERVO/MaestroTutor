// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Art;
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
        Vector3 homePosition;
        Quaternion homeRotation;
        Color homeBackground;
        CameraClearFlags homeFlags;
        bool passthroughWasEnabled;
        GameObject floor;
        Material paper;
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
            homePosition=content.position; homeRotation=content.rotation; homeBackground=viewer.backgroundColor; homeFlags=viewer.clearFlags;
            scan?.SetVirtualView(true);
            passthroughWasEnabled=passthrough && passthrough.enabled;
            if (passthrough) passthrough.enabled=false;
            viewer.clearFlags=CameraClearFlags.SolidColor; viewer.backgroundColor=new Color(.91f,.90f,.86f,1);
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name="Virtual paper floor"; floor.transform.SetParent(content,false);
            floor.transform.position=new Vector3(viewer.transform.position.x,trackingOrigin.position.y-.065f,viewer.transform.position.z);
            floor.transform.localScale=new Vector3(20,.1f,20); floor.layer=RoomPhysicsLayers.Environment;
            paper=IllustratedMaterials.Create(IllustratedMaterials.Paper); floor.GetComponent<Renderer>().sharedMaterial=paper;
            Book.AcousticSurface.Attach(floor,floor.GetComponent<MeshFilter>().sharedMesh,environment:true);
            floor.AddComponent<RoomWalkableSurface>().Publish(floor.GetComponent<Collider>());
            Active=true; return true;
        }
        public void Exit()
        {
            if (!Active) return;
            physics?.PausePhysics();
            motion.SetPose(homePosition,homeRotation,out var error);MovementError=error;
            viewer.backgroundColor=homeBackground; viewer.clearFlags=homeFlags;
            if (passthrough) passthrough.enabled=passthroughWasEnabled;
            scan?.SetVirtualView(false);
            if (floor) { floor.SetActive(false); ArtResources.Release(floor); } ArtResources.Release(paper); floor=null; paper=null;
            Active=false;
        }
        public bool Move(Vector3 delta)
        {
            MovementError=null;
            if (!Active || !float.IsFinite(delta.sqrMagnitude) || delta.sqrMagnitude > .01f) return false;
            delta.y=0;
            if ((Vector3.ProjectOnPlane(viewer.transform.position+delta-floor.transform.position,Vector3.up)).sqrMagnitude > 64) return false;
            float foot = floor.transform.position.y+.065f;
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
