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
        public bool SimulationReady => RealCollisions ? SurfacesReady : AuthoredReady();
        internal int CollisionMask(int mask) => RealCollisions ? mask : mask & ~(1<<RoomPhysicsLayers.Scanned);
        bool GatherGround() {
            ground.Clear();
            var frame=new Creation.RoomFrame(transform);
            if(!frame.Valid||Mathf.Abs(frame.MetresPerUnit-1)>.00001f||Vector3.Dot(transform.up,Vector3.up)<.99999f)return false;
            GetComponentsInChildren(false,ground);return true;
        }
        bool AuthoredReady() {
            if(!GatherGround())return false;
            foreach(var surface in ground)if(surface.Available)return true;
            return false;
        }
        internal bool ContainsSimulation(Vector3 point) {
            if(!float.IsFinite(point.sqrMagnitude))return false;
            if(RealCollisions)return SurfacesReady&&(Contains==null||Contains(point));
            if(!GatherGround())return false;
            // Until streamed region bounds exist, simulation is bounded to actual
            // accepted ground columns. A hidden scan/fallback plane is never used.
            foreach(var surface in ground) {
                if(!surface.Available)continue;
                var c=surface.Collision;var b=c.bounds;
                if(point.x<b.min.x||point.x>b.max.x||point.z<b.min.z||point.z>b.max.z)continue;
                var ray=new Ray(new Vector3(point.x,b.max.y+.1f,point.z),Vector3.down);
                if(c.Raycast(ray,out var hit,b.size.y+.2f)&&hit.normal.y>.1f&&point.y>=hit.point.y-.25f&&point.y<=hit.point.y+16)return true;
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
            bool ready=SimulationReady;
            if(ready==observedReady)return;
            if(!ready)Running=false;
            Status=Running?"Physics on — grip to pick up, release to throw":IdleStatus;Notify();
        }
    }
}
