// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Art;
using UnityEngine;
using UnityEngine.AI;

namespace Maestro.Quest.Interaction
{
    /// <summary>Owned navigation built from accepted scanned and authored collision surfaces.</summary>
    public sealed class RoomNavigation : MonoBehaviour
    {
        RoomPhysicsWorld world;
        NavMeshData data;
        NavMeshDataInstance installed;
        int agentType = -1;
        float radius, height;
        NavMeshQueryFilter Filter => new() { agentTypeID = agentType, areaMask = NavMesh.AllAreas };
        RoomNavigationGeometry accepted=new(), candidate=new();
        bool configured;
        internal uint SurfaceRevision { get; private set; }
        internal uint BuildRevision { get; private set; }
        Vector3 installedPosition;
        Quaternion installedRotation;
        public bool Ready => configured && RefreshGeometry(out _);
        public void Initialize(RoomPhysicsWorld value) { world = value; world.Changed += RoomChanged; }
        void RoomChanged() { if (!world.SimulationReady) Clear(); }
        public bool Prepare(float bodyRadius, float bodyHeight, out string error)
        {
            error = null;
            if (!world || !world.Running || !world.SimulationReady) { error = "Prepare the selected ground and Start physics before walking"; return false; }
            if(!float.IsFinite(bodyRadius)||!float.IsFinite(bodyHeight)||bodyRadius<=0||bodyHeight<bodyRadius*2) { error="Choose a valid walking body";return false; }
            if(!configured||Mathf.Abs(radius-bodyRadius)>=.005f||Mathf.Abs(height-bodyHeight)>=.01f)Clear();
            radius=bodyRadius;height=bodyHeight;configured=true;
            return RefreshGeometry(out error);
        }
        bool RefreshGeometry(out string error)
        {
            error="Room navigation needs active physics and accepted surfaces";
            if(!world||!world.Running||!world.SimulationReady)return false;
            if(!candidate.Capture(world.transform,world.RealCollisions)) { Clear();error="No accepted scanned or authored floor is available for walking";return false; }
            if(data&&installed.valid&&candidate.Same(accepted)){
                Install(candidate.Position,candidate.Rotation);error=null;return installed.valid;
            }
            // A mesh edit or root move must never reuse a stale route. Build from
            // exactly the collider geometry captured for this accepted revision.
            Clear();
            var bounds=candidate.Bounds;bounds.Expand(.2f);
            var settings = NavMesh.CreateSettings(); agentType = settings.agentTypeID;
            settings.agentRadius = radius + .025f; settings.agentHeight = height;
            settings.agentClimb = .10f; settings.agentSlope = 25;
            settings.overrideVoxelSize = true; settings.voxelSize = Mathf.Clamp(radius/4,.025f,.08f);
            settings.minRegionArea = .1f;
            data = NavMeshBuilder.BuildNavMeshData(settings,candidate.BuildSources,bounds,Vector3.zero,Quaternion.identity);
            if (!data) { Clear(); error = "No walking area could be built from the accepted surfaces"; return false; }
            Install(candidate.Position,candidate.Rotation); BuildRevision++;
            (accepted,candidate)=(candidate,accepted);
            error=installed.valid?null:"The accepted walking area could not be installed";
            return installed.valid;
        }
        void Install(Vector3 position,Quaternion rotation)
        {
            if(installed.valid&&installedPosition.Equals(position)&&installedRotation.Equals(rotation))return;
            if(installed.valid)installed.Remove();
            installed=NavMesh.AddNavMeshData(data,position,rotation);
            installedPosition=position;installedRotation=rotation;SurfaceRevision++;
        }
        public bool Sample(Vector3 point, float maximumDistance, out Vector3 floor)
        {
            floor = default;
            if (!Ready || !NavMesh.SamplePosition(point,out var hit,maximumDistance,Filter) || !world.ContainsSimulation(hit.position + Vector3.up*.1f)) return false;
            floor = hit.position; return true;
        }
        public bool DirectStep(Vector3 from,Vector3 to,out Vector3 floor)
        {
            floor=default;
            return Sample(from,.1f,out var start) && Sample(to,.08f,out floor) &&
                Vector3.Distance(new Vector3(to.x,floor.y,to.z),floor) < .025f && !NavMesh.Raycast(start,floor,out _,Filter);
        }
        public bool Path(Vector3 from, Vector3 to, NavMeshPath path) => Ready &&
            Sample(from,.25f,out var start) && Sample(to,.5f,out var end) &&
            NavMesh.CalculatePath(start,end,Filter,path) && path.status == NavMeshPathStatus.PathComplete;
        void Clear()
        {
            if (installed.valid) { installed.Remove(); SurfaceRevision++; }
            ArtResources.Release(data); data = null;
            if (agentType != -1) NavMesh.RemoveSettings(agentType);
            agentType = -1;
        }
        void OnDestroy() { if (world) world.Changed -= RoomChanged; Clear(); }
    }
}
