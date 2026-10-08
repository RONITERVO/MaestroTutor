// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Quest.Art;
using Meta.XR;
using Meta.XR.MRUtilityKit;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace Maestro.Quest.Interaction
{
    /// <summary>Device scene data owns fixed environment geometry, outside recoverable user content.</summary>
    public sealed partial class ScannedRoom : MonoBehaviour
    {
        MRUK mruk;
        EffectMesh surfaces;
        EnvironmentRaycastManager liveSurfaces;
        MRUKRoom current;
        RoomPhysicsWorld world;
        Material outline;
        InputAction tracked;
        bool showing, virtualView;
        public void SetVirtualView(bool value)
        {
            if (virtualView == value) return; virtualView=value;
            NotifySetup();
            // Only virtual content moves. Physical tracking and MRUK world lock
            // keep updating in virtual view as well as mixed reality.
            if (surfaces) surfaces.HideMesh=!(value || showing);
        }
        float nextCheck;
        public void Initialize(RoomPhysicsWorld physics)
        {
            world = physics; world.Changed+=WorldChanged;
#if UNITY_ANDROID && !UNITY_EDITOR
            var sceneRoot = new GameObject("Scanned environment"); sceneRoot.SetActive(false); sceneRoot.transform.SetParent(transform,false);
            mruk = sceneRoot.AddComponent<MRUK>();
            mruk.SceneSettings = new MRUK.MRUKSettings { LoadSceneOnStartup = false, DataSource = MRUK.SceneDataSource.Device };
            mruk.EnableWorldLock = true;
            surfaces = sceneRoot.AddComponent<EffectMesh>();
            surfaces.SpawnOnStart = MRUK.RoomFilter.AllRooms;
            surfaces.Colliders = true; surfaces.HideMesh = true; surfaces.CastShadow = false;
            surfaces.Labels = MRUKAnchor.SceneLabels.FLOOR | MRUKAnchor.SceneLabels.CEILING | MRUKAnchor.SceneLabels.WALL_FACE |
                MRUKAnchor.SceneLabels.INVISIBLE_WALL_FACE | MRUKAnchor.SceneLabels.INNER_WALL_FACE |
                MRUKAnchor.SceneLabels.TABLE | MRUKAnchor.SceneLabels.COUCH | MRUKAnchor.SceneLabels.STORAGE |
                MRUKAnchor.SceneLabels.BED | MRUKAnchor.SceneLabels.OTHER;
            outline = IllustratedMaterials.CreateControl(IllustratedMaterials.Hex("2B8D88")); surfaces.MeshMaterial = outline;
            surfaces.Layer = RoomPhysicsLayers.Scanned;
            mruk.RoomUpdatedEvent.AddListener(RoomChanged); mruk.RoomRemovedEvent.AddListener(RoomChanged);
            mruk.SceneLoadedEvent.AddListener(SceneLoaded);
            sceneRoot.SetActive(true);
            tracked = new InputAction("Physics head tracking",InputActionType.Button,"<XRHMD>/isTracked"); tracked.Enable();
#endif
            source=new DeviceRoomSource(this);surfaceSource=new DeviceSurfaceSource(this);NotifySetup();
        }
        public void Load() => ManualRequest(false);
        public void Scan() => ManualRequest(true);
#if UNITY_ANDROID && !UNITY_EDITOR
        static Task<bool> pendingPermission;
        static Task<bool> ScenePermission(bool retainPending=false)
        {
            if (Permission.HasUserAuthorizedPermission(OVRPermissionsRequester.ScenePermission)) return Task.FromResult(true);
            if(pendingPermission!=null&&!pendingPermission.IsCompleted)return retainPending?pendingPermission:WaitForPermission(pendingPermission);
            var completion = new TaskCompletionSource<bool>();pendingPermission=completion.Task; var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => completion.TrySetResult(true);
            callbacks.PermissionDenied += _ => completion.TrySetResult(false);
            Permission.RequestUserPermission(OVRPermissionsRequester.ScenePermission,callbacks);
            return retainPending?completion.Task:WaitForPermission(completion.Task);
        }
        static async Task<bool> WaitForPermission(Task<bool> result) => await Task.WhenAny(result,Task.Delay(120000)) == result && await result;
#endif
        void SceneLoaded() { SetCurrentRoom(null); world.SetSurfaces(false,"Checking the scanned floor and walls…"); nextCheck = 0; }
        void RoomChanged(MRUKRoom _) { ClearAcousticScan(); world.SetSurfaces(false,"Room changed — check alignment and start physics again"); nextCheck = 0; }
        void AnchorChanged(MRUKAnchor _) { ClearAcousticScan(); world.SetSurfaces(false,"Room surfaces changed — check alignment and start physics again"); nextCheck = 0; }
        void SetCurrentRoom(MRUKRoom room)
        {
            ClearAcousticScan();
            if (current) { current.AnchorCreatedEvent.RemoveListener(AnchorChanged); current.AnchorUpdatedEvent.RemoveListener(AnchorChanged); current.AnchorRemovedEvent.RemoveListener(AnchorChanged); }
            current = room;
            if (current) { current.AnchorCreatedEvent.AddListener(AnchorChanged); current.AnchorUpdatedEvent.AddListener(AnchorChanged); current.AnchorRemovedEvent.AddListener(AnchorChanged); }
        }
        void ValidateRoom()
        {
            if (!SetupActive || !geometryAccepted || Busy || !mruk || !surfaces) return;
            var room = mruk.GetCurrentRoom();
            bool ready = room && room.FloorAnchors.Count > 0 && room.WallAnchors.Count > 0 && mruk.IsWorldLockActive &&
                room.FloorAnchors.All(anchor => surfaces.EffectMeshObjects.TryGetValue(anchor,out var floor) && floor.collider && floor.collider.enabled) &&
                room.WallAnchors.All(anchor => surfaces.EffectMeshObjects.TryGetValue(anchor,out var wall) && wall.collider && wall.collider.enabled);
            if (tracked != null && !tracked.IsPressed()) ready = false;
            if (current != room) { SetCurrentRoom(room); world.SetSurfaces(false,"Room changed — check alignment and start physics again"); }
            if (ready != world.SurfacesReady)
                world.SetSurfaces(ready,ready ? "Show room to check alignment, then Start physics" : "Waiting for a tracked room with floor and wall colliders");
            world.Contains = room ? point => room && room.IsPositionInRoom(point,true) : null;
        }
        public void ToggleSurfaces()
        {
            SetShowing(!showing);
        }
        void Update()
        {
            RefreshAcousticAvailability();
            if (Time.unscaledTime < nextCheck) return; nextCheck = Time.unscaledTime + .25f;
            ValidateRoom(); SynchronizeAcousticScan();
        }
        void OnApplicationPause(bool value) { setupPaused=value;LifecycleChanged(value); }
        void OnApplicationFocus(bool value) { setupFocused=value;LifecycleChanged(!value); }
        void OnDisable(){ClearAcousticScan();CancelRequest("Room setup cancelled because the room closed");if(world)world.SetSurfaces(false,"Room is inactive");NotifySetup();}
        void OnEnable()=>NotifySetup();
        void OnDestroy()
        {
            CancelRequest("Room setup cancelled because the room closed");
            if(world)world.Changed-=WorldChanged;
            SetCurrentRoom(null);
            tracked?.Dispose();
            if (mruk) { mruk.RoomUpdatedEvent.RemoveListener(RoomChanged); mruk.RoomRemovedEvent.RemoveListener(RoomChanged); mruk.SceneLoadedEvent.RemoveListener(SceneLoaded); }
            ArtResources.Release(outline);
        }
    }
}
