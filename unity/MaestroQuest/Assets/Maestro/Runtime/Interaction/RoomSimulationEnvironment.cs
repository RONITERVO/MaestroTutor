// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    public sealed partial class RoomPhysicsWorld
    {
        // This is an explicit runtime policy. A fresh room defaults to physical
        // collision; changing view/opacity does not change it or resume physics.
        public bool RealCollisions { get; private set; } = true;
        readonly List<RoomWalkableSurface> ground = new();
        bool observedReady;
        readonly HashSet<RoomEnvironmentBinding> environments=new();
        internal void RegisterEnvironment(RoomEnvironmentBinding value)=>environments.Add(value);
        internal void UnregisterEnvironment(RoomEnvironmentBinding value)=>environments.Remove(value);
        internal bool IncludesRealRoom(RoomItem item)=>RealCollisions&&(!item||!item.TryGetComponent<RoomEnvironmentBinding>(out var binding)||binding.RealCollisions);
        internal bool EnvironmentReady(RoomItem item)=>CollisionReady&&(IncludesRealRoom(item)?SurfacesReady:AuthoredReady());
        internal bool AnySimulationReady {
            get {if(!CollisionReady)return false;if(SimulationReady)return true;foreach(var binding in environments)if(binding&&binding.isActiveAndEnabled&&!binding.RealCollisions)return AuthoredReady();return false;}
        }
        public bool SimulationReady => CollisionReady&&(RealCollisions ? SurfacesReady : AuthoredReady());
        internal int CollisionMask(int mask,RoomItem item=null) => IncludesRealRoom(item) ? mask : mask & ~(1<<RoomPhysicsLayers.Scanned);
        bool GatherGround()=>GatherGround(ground);
        internal bool GatherGround(List<RoomWalkableSurface> destination) {
            destination.Clear();
            var frame=new Creation.RoomFrame(transform);
            if(!frame.Valid||Mathf.Abs(frame.MetresPerUnit-1)>.00001f||Vector3.Dot(transform.up,Vector3.up)<.99999f)return false;
            GetComponentsInChildren(false,destination);return true;
        }
        bool AuthoredReady() {
            if(!GatherGround())return false;
            foreach(var surface in ground)if(surface.Available)return true;
            return false;
        }
        internal bool ContainsSimulation(Vector3 point,RoomItem item=null)=>ContainsEnvironment(point,IncludesRealRoom(item));
        internal bool ContainsEnvironment(Vector3 point,bool real) {
            if(!CollisionReady||!float.IsFinite(point.sqrMagnitude))return false;
            if(real)return SurfacesReady&&(Contains==null||Contains(point));
            if(!GatherGround())return false;
            // Until streamed region bounds exist, simulation is bounded to actual
            // accepted ground columns. A hidden scan/fallback plane is never used.
            foreach(var surface in ground) {
                if(!surface.Available)continue;
                if(new RoomGroundColumn(surface.Collision).Contains(point))return true;
            }
            return false;
        }
        internal JObject ObserveEnvironment()=>new() {
            ["stateId"]=stateId,["realCollisions"]=RealCollisions,["scanReady"]=SurfacesReady,
            ["authoredReady"]=AuthoredReady(),["ready"]=SimulationReady,
            ["scope"]=RealCollisions?"alignedPhysicalRoom":"acceptedGroundColumns"
        };
        internal bool CanSetEnvironment(string expected,out string error) {
            error="Room physics changed; read physics.environment again before changing collisions";
            if(expected!=stateId)return false;
            error=RuntimeHeld?RuntimeHoldReason:!Active?"Return to the active room before changing collisions":null;
            return error==null;
        }
        internal bool SetEnvironment(string expected,bool real,out JObject result,out string error) {
            result=null;if(!CanSetEnvironment(expected,out error))return false;
            if(RealCollisions!=real) { Running=false;RealCollisions=real;Status=IdleStatus;Notify(); }
            result=ObserveEnvironment();return true;
        }
        void Update() {
            Creation.CollisionResources.Collect();
            RefreshCollisionAdmission();
            bool ready=AnySimulationReady;
            if(ready==observedReady)return;
            if(!ready)Running=false;
            Status=Running?"Physics on — grip to pick up, release to throw":IdleStatus;Notify();
        }
    }
}
