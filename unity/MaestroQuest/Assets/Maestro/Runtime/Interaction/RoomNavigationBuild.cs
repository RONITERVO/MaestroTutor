// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Art;
using UnityEngine;
using UnityEngine.AI;
namespace Maestro.Quest.Interaction {
    // One native operation owns immutable mesh copies until completion/cancellation.
    // Retiring a room never lets a late completion install a map in another room.
    internal sealed class RoomNavigationBuild : IDisposable {
        internal const int MaximumConcurrent=2;
        internal static int ActiveCount=>NavigationResources.ActiveOperations;
        readonly List<Mesh> meshes=new();
        readonly List<NavMeshBuildSource> sources=new();
        readonly NavigationResourcePool.Lease lease;
        AsyncOperation operation;NavMeshData data;int agent=-1;
        bool completed,disposed;
        internal bool Done=>completed;
        RoomNavigationBuild(NavigationResourcePool.Lease lease)=>this.lease=lease;
        internal static NavigationAdmission TryStart(RoomNavigationGeometry geometry,float radius,float height,string owner,out RoomNavigationBuild build,out string reason){
            build=null;
            if(!NavigationBuildInput.TryCapture(geometry.BuildSources,geometry.Bounds,radius,height,out var input,out reason))return NavigationAdmission.Rejected;
            return TryStart(input,owner,out build,out reason);
        }
        internal static NavigationAdmission TryStart(NavigationBuildInput input,string owner,out RoomNavigationBuild build,out string reason){
            return TryStart(input,owner,NavigationResources.Shared,out build,out reason);
        }
        internal static NavigationAdmission TryStart(NavigationBuildInput input,string owner,NavigationResourcePool pool,out RoomNavigationBuild build,out string reason){
            build=null;
            var status=pool.TryBegin(owner,input.Cost,out var lease,out reason);
            if(status!=NavigationAdmission.Started)return status;
            var value=new RoomNavigationBuild(lease);
            try{
                value.agent=NavMesh.CreateSettings().agentTypeID;var settings=input.Settings(value.agent);
                var copied=new Dictionary<Mesh,Mesh>();
                foreach(var original in input.Sources){
                    var source=original;
                    if(source.sourceObject is Mesh mesh){
                        if(!copied.TryGetValue(mesh,out var copy)){
                            copy=UnityEngine.Object.Instantiate(mesh);copy.name="Walking build snapshot";
                            copied.Add(mesh,copy);value.meshes.Add(copy);lease.Own(copy);
                        }
                        source.sourceObject=copy;
                    }
                    value.sources.Add(source);
                }
                value.data=new NavMeshData(value.agent);lease.Own(value.data);
                value.operation=NavMeshBuilder.UpdateNavMeshDataAsync(value.data,settings,value.sources,input.Bounds);
                if(value.operation==null)throw new InvalidOperationException("The walking map build did not start");
                value.operation.completed+=value.Finished;
                if(value.operation.isDone)value.Finished(value.operation);
                build=value;reason=null;return NavigationAdmission.Started;
            }catch{value.Dispose();throw;}
        }
        void Finished(AsyncOperation _){
            if(completed)return;completed=true;
            if(operation!=null)operation.completed-=Finished;
            foreach(var mesh in meshes)ArtResources.Release(mesh);meshes.Clear();sources.Clear();
            lease.Complete();if(disposed)ReleaseData();
        }
        internal RoomNavigationMap Take(){
            if(!completed||disposed||!data)throw new InvalidOperationException("The walking map is not available");
            var result=new RoomNavigationMap(data,agent,lease);data=null;agent=-1;disposed=true;return result;
        }
        void ReleaseData(){ArtResources.Release(data);data=null;if(agent!=-1)NavMesh.RemoveSettings(agent);agent=-1;lease.RetireMap();}
        public void Dispose(){
            if(disposed)return;disposed=true;
            if(!completed){
                if(operation==null)Finished(null);
                else{NavMeshBuilder.Cancel(data);if(operation.isDone)Finished(operation);}
            }else ReleaseData();
        }
    }
}
