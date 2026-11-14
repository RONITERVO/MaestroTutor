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
        RoomPhysicsWorld world;uint generation;bool active,collected,valid;int frame;double step;ulong sourceRevision;
        internal Batch Begin(RoomPhysicsWorld value){
            world=value;generation++;active=true;collected=valid=false;frame=Time.frameCount;step=Time.fixedTimeAsDouble;sourceRevision=RoomWalkableSurface.ChangeRevision;surfaces.Clear();columns.Clear();return new(this,generation);
        }
        bool Live(uint token)=>active&&token==generation&&world&&frame==Time.frameCount&&step==Time.fixedTimeAsDouble&&sourceRevision==RoomWalkableSurface.ChangeRevision;
        void End(uint token){if(token!=generation)return;active=false;world=null;surfaces.Clear();columns.Clear();}
        bool Contains(Vector3 point,bool real,RoomItem participant){
            if(real)return world.SurfacesReady&&(world.Contains==null||world.Contains(point));
            if(!world.VirtualGroundComplete(point,participant))return false;
            if(!collected){
                collected=true;valid=world.GatherGround(surfaces);
                if(valid)foreach(var surface in surfaces)if(surface&&surface.Available)columns.Add(new(surface.Collision));
            }
            if(!valid)return false;
            foreach(var column in columns)if(column.Contains(point))return true;
            return false;
        }
        bool CanOccupy(uint token,Vector3 point,RoomItem first,RoomItem second){
            if(!Live(token)||!world.EnvironmentActive||!float.IsFinite(point.sqrMagnitude))return false;
            // Restored geometry may be visible while its collision clearance still
            // needs explicit recovery. Keep that live hold for both participants,
            // including paused traversal, instead of borrowing the other's readiness.
            if(!RoomEnvironmentBinding.RestorationReady(first)||!RoomEnvironmentBinding.RestorationReady(second))return false;
            bool a=world.IncludesRealRoom(first),b=world.IncludesRealRoom(second);
            // Real-room policy shares one scan proof. Each virtual participant
            // needs its own completeness check: only its own ordinary support
            // obligation can be suspended, never another participant's source.
            return Contains(point,a,first)&&(a&&b||ReferenceEquals(first,second)&&a==b||Contains(point,b,second));
        }
        internal readonly ref struct Batch {
            readonly RoomEnvironmentQueries owner;readonly uint token;
            internal Batch(RoomEnvironmentQueries owner,uint token){this.owner=owner;this.token=token;}
            internal bool CanSimulate(Vector3 point,RoomItem first,RoomItem second)=>owner!=null&&owner.world&&owner.world.Running&&owner.CanOccupy(token,point,first,second);
            internal bool CanOccupy(Vector3 point,RoomItem first,RoomItem second)=>owner!=null&&owner.CanOccupy(token,point,first,second);
            public void Dispose()=>owner?.End(token);
        }
    }
    // Live and batched queries use exactly the same support-column proof.
    internal readonly struct RoomGroundColumn {
        readonly Collider collision;readonly Bounds bounds;readonly RoomGroundHandle handle;readonly ulong sourceRevision;readonly RoomWalkableSurface provider;
        internal RoomGroundColumn(Collider value){collision=value;bounds=value.bounds;provider=RoomWalkableSurface.Find(value);handle=new(value,provider);sourceRevision=RoomWalkableSurface.ChangeRevision;}
        internal bool Contains(Vector3 point){
            if(point.x<bounds.min.x||point.x>bounds.max.x||point.z<bounds.min.z||point.z>bounds.max.z)return false;
            if(sourceRevision!=RoomWalkableSurface.ChangeRevision||!handle.Valid||!provider||!provider.Available)return false;
            // Search beneath this point, including the existing small below-ground
            // allowance. Starting above the entire mesh finds the roof instead of
            // an interior floor when a building has several storeys in one collider.
            // Limit the ray to the same supported air column as the result check;
            // the collider's bounds alone never fill holes or provide support.
            const float belowGround=.25f,aboveGround=16,skin=.002f;
            float top=Mathf.Min(point.y+belowGround+skin,bounds.max.y+.1f);
            float bottom=Mathf.Max(point.y-aboveGround-skin,bounds.min.y-.1f);
            if(top<=bottom)return false;
            var ray=new Ray(new Vector3(point.x,top,point.z),Vector3.down);
            return collision.Raycast(ray,out var hit,top-bottom)&&hit.normal.y>.1f&&point.y>=hit.point.y-belowGround&&point.y<=hit.point.y+aboveGround;
        }
    }
}
