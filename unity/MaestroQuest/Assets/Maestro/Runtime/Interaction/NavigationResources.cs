// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace Maestro.Quest.Interaction {
    internal static class NavigationResources {
        // Runtime admission ceilings, not authored/storage limits or measured RAM.
        // Offline Unity 6000.3.24f1 corpus: 652 mixed sources, 190728 triangles,
        // 15153840 copied bytes and 32625728 voxel cells produced exact-agent routes.
        // Four retained heavy maps plus two small builds exercised 130549152 cells;
        // Two-copy overlap used 30307680 bytes. Eight small maps were independently
        // queryable. This does not establish Quest frame time, Recast peak memory,
        // or capacity for eight largest maps. See NavigationMeasurementTests and
        // .quest-evidence/static-support-20261101/navigation-measurement-mixed-*.
        internal static bool PolicyMeasured=>true;
        internal const int MaximumMaps=8;
        internal static readonly NavigationLimits PerBuild=new(768,262144,16L*1024*1024,32L*1024*1024);
        internal static readonly NavigationLimits Global=new(3072,1048576,32L*1024*1024,128L*1024*1024);
        internal static readonly NavigationResourcePool Shared=new(PerBuild,Global,RoomNavigationBuild.MaximumConcurrent,MaximumMaps);
        // A provider owns original geometry through CollisionResources. This
        // passive check does not duplicate that reservation or promise that a
        // combined map will fit; map copies/work are admitted separately.
        internal static bool TryCheckSourceSet(NavigationSourceCost cost,out string reason)=>Shared.CheckSourceSet(cost,out reason);
        internal static int ActiveOperations=>Shared.ActiveOperations;
        internal static (long sources,long triangles,long copyBytes,long cells,int maps) Totals=>Shared.Totals;
        internal static int Count=>Shared.Count;
        internal static void Collect()=>Shared.Collect();
    }
    // Independent pools allow checked admission/retirement tests without changing
    // the process policy or stealing another world's resources.
    internal sealed class NavigationResourcePool {
        readonly NavigationLimits perBuild;
        readonly NavigationBudgetLedger ledger;
        readonly List<Lease> leases=new();
        internal NavigationResourcePool(NavigationLimits perBuild,NavigationLimits total,int maximumConcurrent,int maximumMaps=int.MaxValue){this.perBuild=perBuild;ledger=new(perBuild,total,maximumConcurrent,maximumMaps);}
        internal bool CheckSourceSet(NavigationSourceCost cost,out string reason){
            bool valid=cost.Valid&&cost.Sources<=perBuild.Sources&&cost.ExpandedTriangles<=perBuild.Triangles&&cost.CopyBytes<=perBuild.CopyBytes;
            reason=valid?null:NavigationBudgetLedger.BuildLimitReason;return valid;
        }
        internal sealed class Lease {
            internal readonly NavigationBudgetLedger.Reservation Reservation;
            readonly NavigationBudgetLedger ledger;
            readonly List<Mesh> meshes=new();
            NavMeshData data;
            bool complete,copiesReleased,mapRetiring,mapReleased;
            internal Lease(NavigationBudgetLedger ledger,NavigationBudgetLedger.Reservation value){this.ledger=ledger;Reservation=value;}
            internal void Own(Mesh mesh){if(complete)throw new InvalidOperationException("Navigation copy ownership ended");if(mesh)meshes.Add(mesh);}
            internal void Own(NavMeshData value){if(data||mapRetiring)throw new InvalidOperationException("Navigation map ownership already exists");data=value;}
            internal void Complete(){if(complete)return;complete=true;ledger.Complete(Reservation);Collect();}
            internal void RetireMap(){mapRetiring=true;Collect();}
            internal bool Collect(){
                if(complete&&!copiesReleased){bool alive=false;foreach(var mesh in meshes)if(mesh){alive=true;break;}if(!alive){copiesReleased=true;meshes.Clear();ledger.ReleaseCopies(Reservation);}}
                if(mapRetiring&&!mapReleased&&!data){mapReleased=true;ledger.ReleaseMap(Reservation);}
                return complete&&copiesReleased&&mapReleased;
            }
        }
        internal int ActiveOperations=>ledger.Running;
        internal (long sources,long triangles,long copyBytes,long cells,int maps) Totals{get{Collect();return ledger.Totals;}}
        internal int Count{get{Collect();return ledger.Count;}}
        internal NavigationAdmission TryBegin(string owner,NavigationSourceCost cost,out Lease lease,out string reason){
            Collect();lease=null;var status=ledger.TryReserve(owner,cost,out var reservation,out reason);
            if(status==NavigationAdmission.Started){lease=new(ledger,reservation);leases.Add(lease);}return status;
        }
        internal void Collect(){for(int i=leases.Count-1;i>=0;i--)if(leases[i].Collect())leases.RemoveAt(i);}
    }
}
