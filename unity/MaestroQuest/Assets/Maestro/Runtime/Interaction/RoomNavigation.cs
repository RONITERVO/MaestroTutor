// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Art;
using UnityEngine;
using UnityEngine.AI;

namespace Maestro.Quest.Interaction
{
    /// <summary>Owned navigation data built from the same MRUK colliders used by room physics.</summary>
    public sealed class RoomNavigation : MonoBehaviour
    {
        RoomPhysicsWorld world;
        NavMeshData data;
        NavMeshDataInstance installed;
        int agentType = -1;
        float radius, height;
        NavMeshQueryFilter Filter => new() { agentTypeID = agentType, areaMask = NavMesh.AllAreas };
        public bool Ready => data && installed.valid && world && world.Running && world.SurfacesReady;
        public void Initialize(RoomPhysicsWorld value) { world = value; world.Changed += RoomChanged; }
        void RoomChanged() { if (!world.SurfacesReady) Clear(); }
        public bool Prepare(float bodyRadius, float bodyHeight, out string error)
        {
            error = null;
            if (!world || !world.Running || !world.SurfacesReady) { error = "Load the room, check its alignment, then Start physics before following"; return false; }
            if (Ready && Mathf.Abs(radius-bodyRadius) < .005f && Mathf.Abs(height-bodyHeight) < .01f) return true;
            Clear();
            var geometry = FindObjectsByType<Collider>(FindObjectsSortMode.None).Where(c => c.enabled && !c.isTrigger && c.gameObject.layer == RoomPhysicsLayers.Scanned).ToArray();
            if (geometry.Length == 0) { error = "No scanned floor or walls are available for walking"; return false; }
            var bounds = geometry[0].bounds; foreach (var collider in geometry.Skip(1)) bounds.Encapsulate(collider.bounds);
            bounds.Expand(.2f);
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds,1 << RoomPhysicsLayers.Scanned,NavMeshCollectGeometry.PhysicsColliders,0,false,new List<NavMeshBuildMarkup>(),false,sources);
            if (sources.Count == 0) { error = "The scanned surfaces cannot form a walking area"; return false; }
            var settings = NavMesh.CreateSettings(); agentType = settings.agentTypeID;
            settings.agentRadius = bodyRadius + .025f; settings.agentHeight = bodyHeight;
            settings.agentClimb = .10f; settings.agentSlope = 25;
            settings.overrideVoxelSize = true; settings.voxelSize = Mathf.Clamp(bodyRadius/4,.025f,.08f);
            settings.minRegionArea = .1f;
            data = NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity);
            if (!data) { Clear(); error = "No walking area could be built from this scan"; return false; }
            installed = NavMesh.AddNavMeshData(data); radius = bodyRadius; height = bodyHeight;
            return Ready;
        }
        public bool Sample(Vector3 point, float maximumDistance, out Vector3 floor)
        {
            floor = default;
            if (!Ready || !NavMesh.SamplePosition(point,out var hit,maximumDistance,Filter) || world.Contains != null && !world.Contains(hit.position + Vector3.up*.1f)) return false;
            floor = hit.position; return true;
        }
        public bool Path(Vector3 from, Vector3 to, NavMeshPath path) => Ready &&
            Sample(from,.25f,out var start) && Sample(to,.5f,out var end) &&
            NavMesh.CalculatePath(start,end,Filter,path) && path.status == NavMeshPathStatus.PathComplete;
        void Clear()
        {
            if (installed.valid) installed.Remove();
            ArtResources.Release(data); data = null;
            if (agentType != -1) NavMesh.RemoveSettings(agentType);
            agentType = -1;
        }
        void OnDestroy() { if (world) world.Changed -= RoomChanged; Clear(); }
    }
}
