// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
namespace Maestro.Quest.Interaction {
    // Exact source/copy payload counts and a voxel-volume work proxy, not a claim
    // about Recast's internal allocation or the installed map's byte size.
    internal readonly struct NavigationSourceCost {
        internal readonly long Sources,Boxes,MeshInstances,UniqueMeshes,ExpandedTriangles,UniqueTriangles,VertexBytes,IndexBytes,VoxelCells,AuxiliaryBytes;
        internal long CopyBytes=>checked(VertexBytes+IndexBytes+AuxiliaryBytes);
        internal bool Valid=>Sources>0&&Boxes>=0&&MeshInstances>=0&&Boxes<=Sources&&MeshInstances==Sources-Boxes&&UniqueMeshes>=0&&UniqueMeshes<=MeshInstances&&ExpandedTriangles>=0&&UniqueTriangles>=0&&UniqueTriangles<=ExpandedTriangles&&VertexBytes>=0&&IndexBytes>=0&&VertexBytes<=long.MaxValue-IndexBytes&&AuxiliaryBytes>=0&&AuxiliaryBytes<=long.MaxValue-VertexBytes-IndexBytes&&VoxelCells>0;
        internal NavigationSourceCost(long sources,long boxes,long instances,long unique,long triangles,long uniqueTriangles,long vertexBytes,long indexBytes,long cells,long auxiliaryBytes=0){
            Sources=sources;Boxes=boxes;MeshInstances=instances;UniqueMeshes=unique;ExpandedTriangles=triangles;UniqueTriangles=uniqueTriangles;VertexBytes=vertexBytes;IndexBytes=indexBytes;VoxelCells=cells;AuxiliaryBytes=auxiliaryBytes;
        }
        internal static bool TryProject(IReadOnlyList<NavigationSourceLayout> sources,long cells,out NavigationSourceCost cost,out string reason){
            cost=default;reason="The walking sources have an invalid or unsupported layout";
            if(sources==null||sources.Count==0||cells<1)return false;
            long boxes=0,instances=0,triangles=0,uniqueTriangles=0,vertices=0,indices=0,auxiliary=0;
            var unique=new Dictionary<int,NavigationMeshLayout>();
            try{checked{
                foreach(var source in sources){
                    if(source.Box){boxes++;continue;}
                    var mesh=source.Mesh;
                    if(mesh.Key==0||mesh.VertexBytes<1||mesh.IndexBytes<1||mesh.Triangles<1||mesh.AuxiliaryBytes<0||mesh.IndexWidth is not (2 or 4)||mesh.IndexBytes%mesh.IndexWidth!=0)return false;
                    instances++;triangles+=mesh.Triangles;
                    if(unique.TryGetValue(mesh.Key,out var old)){
                        if(!old.Equals(mesh))return false;
                    }else{unique.Add(mesh.Key,mesh);uniqueTriangles+=mesh.Triangles;vertices+=mesh.VertexBytes;indices+=mesh.IndexBytes;auxiliary+=mesh.AuxiliaryBytes;}
                }
                _=vertices+indices+auxiliary;
                cost=new(sources.Count,boxes,instances,unique.Count,triangles,uniqueTriangles,vertices,indices,cells,auxiliary);
            }}catch(OverflowException){reason="The walking source cost exceeds the supported numeric range";return false;}
            reason=null;return true;
        }
    }
    internal readonly struct NavigationMeshLayout : IEquatable<NavigationMeshLayout> {
        internal readonly int Key,IndexWidth;
        internal readonly long VertexBytes,IndexBytes,Triangles,AuxiliaryBytes;
        internal NavigationMeshLayout(int key,long vertices,long indices,int width,long triangles,long auxiliaryBytes=0){Key=key;VertexBytes=vertices;IndexBytes=indices;IndexWidth=width;Triangles=triangles;AuxiliaryBytes=auxiliaryBytes;}
        public bool Equals(NavigationMeshLayout other)=>Key==other.Key&&IndexWidth==other.IndexWidth&&VertexBytes==other.VertexBytes&&IndexBytes==other.IndexBytes&&Triangles==other.Triangles&&AuxiliaryBytes==other.AuxiliaryBytes;
    }
    internal readonly struct NavigationSourceLayout {
        internal readonly bool Box;
        internal readonly NavigationMeshLayout Mesh;
        internal NavigationSourceLayout(NavigationMeshLayout mesh){Box=false;Mesh=mesh;}
        internal static NavigationSourceLayout BoxSource=>new(true);
        NavigationSourceLayout(bool box){Box=box;Mesh=default;}
    }
    internal readonly struct NavigationLimits {
        internal readonly long Sources,Triangles,CopyBytes,VoxelCells;
        internal NavigationLimits(long sources,long triangles,long copyBytes,long cells){
            if(sources<1||triangles<0||copyBytes<0||cells<1)throw new ArgumentException("Invalid navigation limits");
            Sources=sources;Triangles=triangles;CopyBytes=copyBytes;VoxelCells=cells;
        }
        internal bool Contains(NavigationSourceCost cost)=>cost.Sources<=Sources&&cost.ExpandedTriangles<=Triangles&&cost.CopyBytes<=CopyBytes&&cost.VoxelCells<=VoxelCells;
        internal static NavigationLimits Unmeasured=>new(long.MaxValue,long.MaxValue,long.MaxValue,long.MaxValue);
    }
    internal enum NavigationAdmission { Started,WaitingForOperation,Rejected }
    internal sealed class NavigationBudgetLedger {
        internal const string BuildLimitReason="This walking map exceeds its source or build-work limit. Reduce the connected walking geometry and try again.";
        internal const string CapacityReason="Walking-map capacity is held by active or retiring maps. Stop unused movement, wait for resources to release, and retry.";
        internal const string SlotReason="Walking paths are waiting for another map build to finish.";
        internal sealed class Reservation {
            internal readonly string Owner;
            internal readonly NavigationSourceCost Cost;
            internal bool Running=true,Copies=true,Map=true;
            internal Reservation(string owner,NavigationSourceCost cost){Owner=owner;Cost=cost;}
        }
        readonly List<Reservation> entries=new();
        readonly NavigationLimits perBuild,total;
        readonly int maximumConcurrent,maximumMaps;
        internal NavigationBudgetLedger(NavigationLimits perBuild,NavigationLimits total,int maximumConcurrent,int maximumMaps=int.MaxValue){
            if(maximumConcurrent<1||maximumMaps<1)throw new ArgumentException("Invalid navigation operation limit");
            this.perBuild=perBuild;this.total=total;this.maximumConcurrent=maximumConcurrent;this.maximumMaps=maximumMaps;
        }
        internal int Count=>entries.Count;
        internal int Running {get{int value=0;foreach(var e in entries)if(e.Running)value++;return value;}}
        internal (long sources,long triangles,long copyBytes,long cells,int maps) Totals {
            get{long sources=0,triangles=0,bytes=0,cells=0;int maps=0;checked{foreach(var e in entries){if(e.Copies)bytes+=e.Cost.CopyBytes;if(e.Map){maps++;sources+=e.Cost.Sources;triangles+=e.Cost.ExpandedTriangles;cells+=e.Cost.VoxelCells;}}}return(sources,triangles,bytes,cells,maps);}
        }
        internal NavigationAdmission TryReserve(string owner,NavigationSourceCost cost,out Reservation lease,out string reason){
            lease=null;reason=BuildLimitReason;
            if(string.IsNullOrEmpty(owner)||!cost.Valid||!perBuild.Contains(cost))return NavigationAdmission.Rejected;
            var used=Totals;
            // Subtraction avoids overflow even while an old native map retires.
            if(used.maps>=maximumMaps||cost.Sources>total.Sources-used.sources||cost.ExpandedTriangles>total.Triangles-used.triangles||cost.CopyBytes>total.CopyBytes-used.copyBytes||cost.VoxelCells>total.VoxelCells-used.cells){reason=CapacityReason;return NavigationAdmission.Rejected;}
            if(Running>=maximumConcurrent){reason=SlotReason;return NavigationAdmission.WaitingForOperation;}
            lease=new(owner,cost);entries.Add(lease);reason=null;return NavigationAdmission.Started;
        }
        internal void Complete(Reservation lease){Require(lease);lease.Running=false;Prune(lease);}
        internal void ReleaseCopies(Reservation lease){Require(lease);lease.Copies=false;Prune(lease);}
        internal void ReleaseMap(Reservation lease){Require(lease);lease.Map=false;Prune(lease);}
        void Require(Reservation lease){if(lease==null||!entries.Contains(lease))throw new InvalidOperationException("Navigation reservation is no longer owned");}
        void Prune(Reservation lease){if(!lease.Running&&!lease.Copies&&!lease.Map)entries.Remove(lease);}
    }
}
