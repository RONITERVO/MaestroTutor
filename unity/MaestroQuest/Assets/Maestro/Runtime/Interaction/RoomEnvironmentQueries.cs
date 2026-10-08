// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using UnityEngine;
namespace Maestro.Quest.Interaction {
    // Reusable buffers, but no cross-frame environment cache. A batch belongs to
    // one synchronous, read-only sampling segment. Start a new batch after any
    // geometry/frame mutation. The ref struct cannot escape into an async task,
    // coroutine field or retained program state. Live pause and participant policy
    // checks remain outside the captured ground list.
    internal sealed class RoomEnvironmentQueries {
        readonly List<RoomWalkableSurface> surfaces=new(64);
        readonly List<RoomGroundColumn> columns=new(64);
        RoomPhysicsWorld world;uint generation;bool active,collected,valid;int frame;double step;
        internal Batch Begin(RoomPhysicsWorld value){
            world=value;generation++;active=true;collected=valid=false;frame=Time.frameCount;step=Time.fixedTimeAsDouble;surfaces.Clear();columns.Clear();return new(this,generation);
        }
        bool Live(uint token)=>active&&token==generation&&world&&frame==Time.frameCount&&step==Time.fixedTimeAsDouble;
        void End(uint token){if(token!=generation)return;active=false;world=null;surfaces.Clear();columns.Clear();}
        bool Contains(Vector3 point,bool real){
            if(real)return world.SurfacesReady&&(world.Contains==null||world.Contains(point));
            if(!collected){
                collected=true;valid=world.GatherGround(surfaces);
                if(valid)foreach(var surface in surfaces)if(surface&&surface.Available)columns.Add(new(surface.Collision));
            }
            if(!valid)return false;
            foreach(var column in columns)if(column.Contains(point))return true;
            return false;
        }
        bool CanSimulate(uint token,Vector3 point,RoomItem first,RoomItem second){
            if(!Live(token)||!world.SimulationActive||!float.IsFinite(point.sqrMagnitude))return false;
            bool a=world.IncludesRealRoom(first),b=world.IncludesRealRoom(second);
            // Two participants using the same environment need one spatial proof.
            // Mixed policies require both; a virtual actor cannot borrow scan readiness.
            return Contains(point,a)&&(a==b||Contains(point,b));
        }
        internal readonly ref struct Batch {
            readonly RoomEnvironmentQueries owner;readonly uint token;
            internal Batch(RoomEnvironmentQueries owner,uint token){this.owner=owner;this.token=token;}
            internal bool CanSimulate(Vector3 point,RoomItem first,RoomItem second)=>owner!=null&&owner.CanSimulate(token,point,first,second);
            public void Dispose()=>owner?.End(token);
        }
    }
    // Live and batched queries use exactly the same support-column proof.
    internal readonly struct RoomGroundColumn {
        readonly Collider collision;readonly Bounds bounds;
        internal RoomGroundColumn(Collider value){collision=value;bounds=value.bounds;}
        internal bool Contains(Vector3 point){
            if(point.x<bounds.min.x||point.x>bounds.max.x||point.z<bounds.min.z||point.z>bounds.max.z)return false;
            if(!collision||!collision.enabled||!collision.gameObject.activeInHierarchy)return false;
            var ray=new Ray(new Vector3(point.x,bounds.max.y+.1f,point.z),Vector3.down);
            return collision.Raycast(ray,out var hit,bounds.size.y+.2f)&&hit.normal.y>.1f&&point.y>=hit.point.y-.25f&&point.y<=hit.point.y+16;
        }
    }
}
