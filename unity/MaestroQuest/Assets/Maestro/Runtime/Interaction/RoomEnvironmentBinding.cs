// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using UnityEngine;
namespace Maestro.Quest.Interaction
{
    /// <summary>One owning item controls every child collider without consuming a layer per entity.</summary>
    public sealed class RoomEnvironmentBinding:MonoBehaviour, Maestro.Quest.Art.INativeResourceOwner
    {
        RoomPhysicsWorld world;
        RoomItem item;
        internal bool RealCollisions {get;private set;}=true;
        internal void Apply(RoomPhysicsWorld source,RoomItem target,bool real) {
            bool changed=world!=source||RealCollisions!=real;
            if(world!=source){if(world){world.Changed-=Refresh;world.UnregisterEnvironment(this);}world=source;if(world){world.Changed+=Refresh;world.RegisterEnvironment(this);}}
            item=target;RealCollisions=real;Refresh();
            if(changed)item.GetComponent<RigidRoomItem>()?.Teleported();
        }
        void Refresh() {
            if(!item)return;
            var body=item.GetComponent<Rigidbody>();if(!body)return;
            int bit=1<<RoomPhysicsLayers.Scanned;
            body.excludeLayers=world&&!world.IncludesRealRoom(item)?body.excludeLayers.value|bit:body.excludeLayers.value&~bit;
        }
        void OnEnable(){if(world)world.RegisterEnvironment(this);Refresh();}
        void OnDisable(){if(world)world.UnregisterEnvironment(this);}
        void Maestro.Quest.Art.INativeResourceOwner.ReleaseNativeResources()=>ReleaseNativeResources();
        void OnDestroy()=>ReleaseNativeResources();
        void ReleaseNativeResources(){if(world){world.Changed-=Refresh;world.UnregisterEnvironment(this);}world=null;item=null;}
    }
}
