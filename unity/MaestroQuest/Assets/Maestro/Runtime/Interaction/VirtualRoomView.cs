// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Art;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Maestro.Quest.Interaction
{
    /// <summary>Explicit virtual view: no translated passthrough, and no saved artificial camera offset.</summary>
    public sealed class VirtualRoomView : MonoBehaviour
    {
        Transform origin;
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
        public void Initialize(Transform userOrigin,Camera camera,ScannedRoom scanned,RoomPhysicsWorld world)
        { origin=userOrigin; viewer=camera; scan=scanned; physics=world; passthrough=viewer.GetComponent<ARCameraManager>(); }
        public bool Enter()
        {
            if (Active) return true;
            if (!origin || !viewer || scan && scan.Busy) return false;
            homePosition=origin.localPosition; homeRotation=origin.localRotation; homeBackground=viewer.backgroundColor; homeFlags=viewer.clearFlags;
            scan?.SetVirtualView(true);
            passthroughWasEnabled=passthrough && passthrough.enabled;
            if (passthrough) passthrough.enabled=false;
            viewer.clearFlags=CameraClearFlags.SolidColor; viewer.backgroundColor=new Color(.91f,.90f,.86f,1);
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name="Virtual paper floor"; floor.transform.SetParent(transform,false);
            floor.transform.position=new Vector3(viewer.transform.position.x,origin.position.y-.065f,viewer.transform.position.z);
            floor.transform.localScale=new Vector3(20,.1f,20); floor.layer=RoomPhysicsLayers.Environment;
            paper=IllustratedMaterials.Create(IllustratedMaterials.Paper); floor.GetComponent<Renderer>().sharedMaterial=paper;
            Active=true; return true;
        }
        public void Exit()
        {
            if (!Active) return;
            physics?.PausePhysics();
            origin.SetLocalPositionAndRotation(homePosition,homeRotation);
            viewer.backgroundColor=homeBackground; viewer.clearFlags=homeFlags;
            if (passthrough) passthrough.enabled=passthroughWasEnabled;
            scan?.SetVirtualView(false);
            if (floor) { floor.SetActive(false); ArtResources.Release(floor); } ArtResources.Release(paper); floor=null; paper=null;
            Active=false;
        }
        public bool Move(Vector3 delta)
        {
            if (!Active || !float.IsFinite(delta.sqrMagnitude) || delta.sqrMagnitude > .01f) return false;
            delta.y=0;
            if ((Vector3.ProjectOnPlane(viewer.transform.position+delta-floor.transform.position,Vector3.up)).sqrMagnitude > 64) return false;
            float foot = floor.transform.position.y+.065f;
            float height = Mathf.Clamp(viewer.transform.position.y-foot,.65f,2.2f), radius=.2f;
            Vector3 bottom=new(viewer.transform.position.x,foot+radius+.04f,viewer.transform.position.z), top=new(viewer.transform.position.x,foot+height-radius,viewer.transform.position.z);
            int mask=(1<<RoomPhysicsLayers.Scanned)|(1<<RoomPhysicsLayers.Item)|(1<<RoomPhysicsLayers.Environment);
            int count=Physics.CapsuleCastNonAlloc(bottom,top,radius,delta.normalized,hits,delta.magnitude+.005f,mask,QueryTriggerInteraction.Ignore);
            if (count>0) return false;
            count=Physics.OverlapCapsuleNonAlloc(bottom+delta,top+delta,radius,overlaps,mask,QueryTriggerInteraction.Ignore);
            if (count>0) return false;
            origin.position+=delta; return true;
        }
        public void Turn(float degrees)
        {
            if (Active && (degrees == -30 || degrees == 30)) origin.RotateAround(viewer.transform.position,Vector3.up,degrees);
        }
        void OnDisable() => Exit();
        void OnDestroy() => Exit();
    }
}
