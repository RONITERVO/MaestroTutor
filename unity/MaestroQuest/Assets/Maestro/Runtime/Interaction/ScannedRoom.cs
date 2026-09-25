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
    public sealed class ScannedRoom : MonoBehaviour
    {
        MRUK mruk;
        EffectMesh surfaces;
        EnvironmentRaycastManager liveSurfaces;
        MRUKRoom current;
        RoomPhysicsWorld world;
        Material outline;
        InputAction tracked;
        bool busy, showing;
        float nextCheck;
        public void Initialize(RoomPhysicsWorld physics)
        {
            world = physics;
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
            outline = IllustratedMaterials.Create(IllustratedMaterials.Hex("2B8D88")); surfaces.MeshMaterial = outline;
            surfaces.Layer = RoomPhysicsLayers.Scanned;
            mruk.RoomUpdatedEvent.AddListener(RoomChanged); mruk.RoomRemovedEvent.AddListener(RoomChanged);
            mruk.SceneLoadedEvent.AddListener(SceneLoaded);
            sceneRoot.SetActive(true);
            tracked = new InputAction("Physics head tracking",InputActionType.Button,"<XRHMD>/isTracked"); tracked.Enable();
#endif
        }
        public void Load() => BeginLoad(false);
        public void Scan() => BeginLoad(true);
        public async Task<bool> PreparePlacement()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!await ScenePermission() || !this || !EnvironmentRaycastManager.IsSupported) return false;
            if (!liveSurfaces) liveSurfaces = gameObject.AddComponent<EnvironmentRaycastManager>();
            return true;
#else
            await Task.CompletedTask; return false;
#endif
        }
        async void BeginLoad(bool rescan)
        {
            if (busy) return;
            world.PausePhysics(); world.SetSurfaces(false,"Loading room surfaces…"); busy = true;
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (!await ScenePermission()) { if (this) world.SetSurfaces(false,"Room access was not granted — tap Load to try again"); return; }
                if (!this) return;
                if (rescan && !await OVRScene.RequestSpaceSetup()) { if (this) world.SetSurfaces(false,"Room scan canceled — Load uses the saved scan"); return; }
                if (!this) return;
                var result = await mruk.LoadSceneFromDevice(requestSceneCaptureIfNoDataFound:true);
                if (!this) return;
                if (result != MRUK.LoadDeviceResult.Success) { world.SetSurfaces(false,"Room unavailable — use Scan room, then Load"); return; }
                ValidateRoom();
#else
                await Task.CompletedTask;
                world.SetSurfaces(false,"Room scanning is available on Quest; editor tests use a synthetic room");
#endif
            }
            catch (Exception error) { if (this) { Debug.LogWarning("Room scene could not load: " + error.GetType().Name); world.SetSurfaces(false,"Could not load the room — try Load again"); } }
            finally { if (this) busy = false; }
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        static Task<bool> ScenePermission()
        {
            if (Permission.HasUserAuthorizedPermission(OVRPermissionsRequester.ScenePermission)) return Task.FromResult(true);
            var completion = new TaskCompletionSource<bool>(); var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => completion.TrySetResult(true);
            callbacks.PermissionDenied += _ => completion.TrySetResult(false);
            Permission.RequestUserPermission(OVRPermissionsRequester.ScenePermission,callbacks);
            return WaitForPermission(completion.Task);
        }
        static async Task<bool> WaitForPermission(Task<bool> result) => await Task.WhenAny(result,Task.Delay(120000)) == result && await result;
#endif
        void SceneLoaded() { SetCurrentRoom(null); world.SetSurfaces(false,"Checking the scanned floor and walls…"); nextCheck = 0; }
        void RoomChanged(MRUKRoom _) { world.SetSurfaces(false,"Room changed — check alignment and start physics again"); nextCheck = 0; }
        void AnchorChanged(MRUKAnchor _) { world.SetSurfaces(false,"Room surfaces changed — check alignment and start physics again"); nextCheck = 0; }
        void SetCurrentRoom(MRUKRoom room)
        {
            if (current) { current.AnchorCreatedEvent.RemoveListener(AnchorChanged); current.AnchorUpdatedEvent.RemoveListener(AnchorChanged); current.AnchorRemovedEvent.RemoveListener(AnchorChanged); }
            current = room;
            if (current) { current.AnchorCreatedEvent.AddListener(AnchorChanged); current.AnchorUpdatedEvent.AddListener(AnchorChanged); current.AnchorRemovedEvent.AddListener(AnchorChanged); }
        }
        void ValidateRoom()
        {
            if (!mruk || !surfaces) return;
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
            showing = !showing; if (surfaces) surfaces.HideMesh = !showing;
        }
        public bool TrySurface(Ray ray, out Vector3 point, out Vector3 normal)
        {
            point = normal = default;
            if (!liveSurfaces || !EnvironmentRaycastManager.IsSupported || !liveSurfaces.Raycast(ray,out var hit,4)) return false;
            if (!float.IsFinite(hit.point.sqrMagnitude) || hit.normal.sqrMagnitude < .9f || hit.normalConfidence < .5f) return false;
            point = hit.point; normal = hit.normal.normalized; return true;
        }
        void Update()
        {
            if (Time.unscaledTime < nextCheck) return; nextCheck = Time.unscaledTime + .25f;
            ValidateRoom();
        }
        void OnApplicationPause(bool paused) { if (paused && world) world.SetSurfaces(false,"Check room alignment after returning"); }
        void OnApplicationFocus(bool focused) { if (!focused && world) world.SetSurfaces(false,"Check room alignment after returning"); }
        void OnDestroy()
        {
            SetCurrentRoom(null);
            tracked?.Dispose();
            if (mruk) { mruk.RoomUpdatedEvent.RemoveListener(RoomChanged); mruk.RoomRemovedEvent.RemoveListener(RoomChanged); mruk.SceneLoadedEvent.RemoveListener(SceneLoaded); }
            ArtResources.Release(outline);
        }
    }
}
