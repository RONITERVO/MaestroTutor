// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using Maestro.Quest.Art;
using UnityEngine;
using UnityEngine.AI;
namespace Maestro.Quest.Interaction
{
    // One native operation owns immutable mesh copies until completion/cancellation.
    // Retiring a room never lets a late completion install a map in another room.
    internal sealed class RoomNavigationBuild : IDisposable
    {
        internal const int MaximumConcurrent=2;
        internal static int ActiveCount {get;private set;}
        readonly List<Mesh> meshes=new();
        readonly List<NavMeshBuildSource> sources=new();
        AsyncOperation operation;
        NavMeshData data;
        int agent=-1;
        bool completed,disposed;
        internal bool Done=>completed;
        internal static bool TryStart(RoomNavigationGeometry geometry,float radius,float height,out RoomNavigationBuild build)
        {
            build=null;if(ActiveCount>=MaximumConcurrent)return false;
            var value=new RoomNavigationBuild();ActiveCount++;
            try{
                var settings=NavMesh.CreateSettings();value.agent=settings.agentTypeID;
                settings.agentRadius=radius+.025f;settings.agentHeight=height;
                settings.agentClimb=RoomGroundQuery.MaximumStep;settings.agentSlope=RoomGroundQuery.MaximumSlope;
                settings.overrideVoxelSize=true;settings.voxelSize=Mathf.Clamp(radius/4,.025f,.08f);settings.minRegionArea=.1f;
                var copied=new Dictionary<Mesh,Mesh>();
                foreach(var input in geometry.BuildSources){
                    var source=input;source.component=null;
                    if(source.sourceObject is Mesh mesh){
                        if(!copied.TryGetValue(mesh,out var copy)){copy=UnityEngine.Object.Instantiate(mesh);copy.name="Walking build snapshot";copied.Add(mesh,copy);value.meshes.Add(copy);}
                        source.sourceObject=copy;
                    }
                    value.sources.Add(source);
                }
                value.data=new NavMeshData(value.agent);var bounds=geometry.Bounds;bounds.Expand(.2f);
                value.operation=NavMeshBuilder.UpdateNavMeshDataAsync(value.data,settings,value.sources,bounds);
                if(value.operation==null)throw new InvalidOperationException("The walking map build did not start");
                value.operation.completed+=value.Finished;
                if(value.operation.isDone)value.Finished(value.operation);
                build=value;return true;
            }catch{value.Dispose();if(value.operation==null)value.Finished(null);throw;}
        }
        void Finished(AsyncOperation _)
        {
            if(completed)return;completed=true;ActiveCount--;
            if(operation!=null)operation.completed-=Finished;
            foreach(var mesh in meshes)ArtResources.Release(mesh);meshes.Clear();sources.Clear();
            if(disposed)ReleaseData();
        }
        internal void Take(out NavMeshData result,out int type)
        {
            if(!completed||disposed||!data)throw new InvalidOperationException("The walking map is not available");
            result=data;type=agent;data=null;agent=-1;disposed=true;
        }
        void ReleaseData(){ArtResources.Release(data);data=null;if(agent!=-1)NavMesh.RemoveSettings(agent);agent=-1;}
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            if(!completed&&operation!=null){NavMeshBuilder.Cancel(data);if(operation.isDone)Finished(operation);}
            if(completed)ReleaseData();
        }
    }
}
