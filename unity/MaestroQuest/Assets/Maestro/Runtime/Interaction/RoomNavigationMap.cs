// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Art;
using UnityEngine;
using UnityEngine.AI;
namespace Maestro.Quest.Interaction {
    // A completed map transfers once from its build to its consumer. Installation
    // never transfers ownership to the global NavMesh registry.
    internal sealed class RoomNavigationMap : IDisposable {
        internal NavMeshData Data{get;private set;}
        internal int AgentType{get;private set;}
        internal bool Installed=>installed.valid;
        readonly NavigationResourcePool.Lease lease;
        readonly Func<NavMeshData,Vector3,Quaternion,NavMeshDataInstance> add;
        static readonly Func<NavMeshData,Vector3,Quaternion,NavMeshDataInstance> NativeAdd=NavMesh.AddNavMeshData;
        NavMeshDataInstance installed;Vector3 position;Quaternion rotation;
        bool disposed;
        internal RoomNavigationMap(NavMeshData data,int agent,NavigationResourcePool.Lease lease,Func<NavMeshData,Vector3,Quaternion,NavMeshDataInstance> add=null){Data=data;AgentType=agent;this.lease=lease;this.add=add??NativeAdd;}
        internal bool TryInstall(Vector3 nextPosition,Quaternion nextRotation,out bool changed){
            changed=false;
            if(disposed||!Data)throw new InvalidOperationException("Walking map ownership has ended");
            if(installed.valid&&position.Equals(nextPosition)&&rotation.Equals(nextRotation))return true;
            if(installed.valid)installed.Remove();installed=default;changed=true;
            installed=add(Data,nextPosition,nextRotation);position=nextPosition;rotation=nextRotation;return installed.valid;
        }
        public void Dispose(){
            if(disposed)return;disposed=true;
            if(installed.valid)installed.Remove();installed=default;
            ArtResources.Release(Data);Data=null;
            if(AgentType!=-1)NavMesh.RemoveSettings(AgentType);AgentType=-1;lease.RetireMap();
        }
    }
}
