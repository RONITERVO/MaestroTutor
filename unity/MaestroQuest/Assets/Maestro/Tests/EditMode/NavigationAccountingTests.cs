// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
namespace Maestro.Quest.Tests {
    public sealed class NavigationAccountingTests {
        static NavigationMeshLayout Mesh(int key=1,long vertices=96,long indices=12,long triangles=2)=>new(key,vertices,indices,2,triangles);
        static NavigationSourceCost Cost(int count=1){
            var sources=new NavigationSourceLayout[count];for(int i=0;i<count;i++)sources[i]=new(Mesh());
            Assert.IsTrue(NavigationSourceCost.TryProject(sources,100,out var value,out var error),error);return value;
        }
        static NavigationLimits Limits(long sources=100,long triangles=1000,long bytes=10000,long cells=10000)=>new(sources,triangles,bytes,cells);
        [Test] public void SharedCopiesDeduplicatePayloadButChargeEveryTransformedInstance(){
            var m=new NavigationSourceLayout(Mesh());
            Assert.IsTrue(NavigationSourceCost.TryProject(new[]{m,NavigationSourceLayout.BoxSource,m,new NavigationSourceLayout(Mesh(2))},240,out var value,out var error),error);
            Assert.AreEqual(4,value.Sources);Assert.AreEqual(1,value.Boxes);Assert.AreEqual(3,value.MeshInstances);Assert.AreEqual(2,value.UniqueMeshes);
            Assert.AreEqual(6,value.ExpandedTriangles);Assert.AreEqual(4,value.UniqueTriangles);Assert.AreEqual(216,value.CopyBytes);Assert.AreEqual(240,value.VoxelCells);
        }
        [Test] public void SharedIndexRangesStillChargeEverySubmeshTriangle(){
            Assert.IsTrue(NavigationSourceCost.TryProject(new[]{new NavigationSourceLayout(Mesh(triangles:4))},1,out var value,out var error),error);
            Assert.AreEqual(4,value.ExpandedTriangles);Assert.AreEqual(108,value.CopyBytes);
        }
        [Test] public void ConflictingSameIdentityLayoutCannotBorrowTheEarlierCost(){
            Assert.IsFalse(NavigationSourceCost.TryProject(new[]{new NavigationSourceLayout(Mesh()),new NavigationSourceLayout(Mesh(vertices:100))},1,out _,out _));
        }
        [Test] public void OverflowIsRefusedWithoutAValidLookingPartialProjection(){
            Assert.IsFalse(NavigationSourceCost.TryProject(new[]{new NavigationSourceLayout(Mesh(vertices:long.MaxValue))},1,out var cost,out var error));
            Assert.IsFalse(cost.Valid);StringAssert.Contains("numeric range",error);
        }
        [TestCase(0,2,2)][TestCase(20,0,2)][TestCase(20,6,0)][TestCase(-1,6,1)]
        public void InvalidMeshLayoutCannotReduceAReservation(long vertices,long indices,long triangles){
            Assert.IsFalse(NavigationSourceCost.TryProject(new[]{new NavigationSourceLayout(Mesh(vertices:vertices,indices:indices,triangles:triangles))},1,out _,out _));
        }
        [Test] public void BusyOperationWaitsButCompletedOrRetiringMapCapacityRefuses(){
            var ledger=new NavigationBudgetLedger(Limits(),Limits(sources:2),1);var cost=Cost();
            Assert.AreEqual(NavigationAdmission.Started,ledger.TryReserve("world-a",cost,out var first,out _));
            Assert.AreEqual(NavigationAdmission.WaitingForOperation,ledger.TryReserve("world-b",cost,out var waiting,out var reason));Assert.IsNull(waiting);Assert.AreEqual(NavigationBudgetLedger.SlotReason,reason);
            ledger.Complete(first);ledger.ReleaseCopies(first);
            Assert.AreEqual(NavigationAdmission.Started,ledger.TryReserve("world-b",cost,out var second,out _));ledger.Complete(second);ledger.ReleaseCopies(second);
            Assert.AreEqual(NavigationAdmission.Rejected,ledger.TryReserve("world-c",cost,out _,out reason));Assert.AreEqual(NavigationBudgetLedger.CapacityReason,reason);
            ledger.ReleaseMap(first);
            Assert.AreEqual(NavigationAdmission.Started,ledger.TryReserve("world-c",cost,out var third,out _));
            ledger.ReleaseMap(second);ledger.Complete(third);ledger.ReleaseCopies(third);ledger.ReleaseMap(third);Assert.AreEqual(0,ledger.Count);
        }
        [Test] public void TinyBoxMapsConsumeIndependentMapCapacityUntilPhysicalRetirement(){
            Assert.IsTrue(NavigationSourceCost.TryProject(new[]{NavigationSourceLayout.BoxSource},1,out var tiny,out _));
            var ledger=new NavigationBudgetLedger(Limits(),Limits(),2,maximumMaps:2);
            ledger.TryReserve("first",tiny,out var first,out _);ledger.Complete(first);ledger.ReleaseCopies(first);
            ledger.TryReserve("second",tiny,out var second,out _);ledger.Complete(second);ledger.ReleaseCopies(second);
            Assert.AreEqual(0,ledger.Totals.copyBytes);Assert.AreEqual(2,ledger.Totals.sources);var before=ledger.Totals;
            Assert.AreEqual(NavigationAdmission.Rejected,ledger.TryReserve("third",tiny,out _,out var reason));Assert.AreEqual(NavigationBudgetLedger.CapacityReason,reason);Assert.AreEqual(before,ledger.Totals);
            ledger.ReleaseMap(first);Assert.AreEqual(NavigationAdmission.Started,ledger.TryReserve("third",tiny,out var third,out _));
            ledger.ReleaseMap(second);ledger.Complete(third);ledger.ReleaseCopies(third);ledger.ReleaseMap(third);Assert.AreEqual(0,ledger.Count);
        }
        [Test] public void BuildLimitRefusesBeforeSlotWaitAndLeavesLedgerUnchanged(){
            var ledger=new NavigationBudgetLedger(Limits(sources:1),Limits(),1);ledger.TryReserve("a",Cost(),out _,out _);var before=ledger.Totals;
            Assert.AreEqual(NavigationAdmission.Rejected,ledger.TryReserve("b",Cost(2),out var refused,out var reason));Assert.IsNull(refused);Assert.AreEqual(NavigationBudgetLedger.BuildLimitReason,reason);Assert.AreEqual(before,ledger.Totals);
        }
        [Test] public void CopiesAndMapRetireIndependentlyAndOperationEndsOnlyAtCompletion(){
            var ledger=new NavigationBudgetLedger(Limits(),Limits(),2);ledger.TryReserve("owner",Cost(),out var lease,out _);
            ledger.ReleaseMap(lease);Assert.AreEqual(0,ledger.Totals.maps);Assert.AreEqual(108,ledger.Totals.copyBytes);Assert.AreEqual(1,ledger.Running);
            ledger.Complete(lease);Assert.AreEqual(0,ledger.Running);Assert.AreEqual(1,ledger.Count);
            ledger.ReleaseCopies(lease);Assert.AreEqual(0,ledger.Count);Assert.Throws<InvalidOperationException>(()=>ledger.ReleaseMap(lease));
        }
        [Test] public void CopyCapacityCanRemainHeldAfterItsMapHasGone(){
            var ledger=new NavigationBudgetLedger(Limits(),Limits(bytes:108),2);ledger.TryReserve("owner",Cost(),out var lease,out _);ledger.Complete(lease);ledger.ReleaseMap(lease);
            Assert.AreEqual(NavigationAdmission.Rejected,ledger.TryReserve("other",Cost(),out _,out var reason));Assert.AreEqual(NavigationBudgetLedger.CapacityReason,reason);
            ledger.ReleaseCopies(lease);Assert.AreEqual(NavigationAdmission.Started,ledger.TryReserve("other",Cost(),out _,out _));
        }
        [Test] public void MetadataProjectionCountsAllVertexStreamsAndIndexPaddingWithoutCloning(){
            var mesh=new UnityEngine.Mesh();
            try{
                mesh.SetVertexBufferParams(3,new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3,0),new VertexAttributeDescriptor(VertexAttribute.Color,VertexAttributeFormat.Float32,4,1));
                mesh.SetIndexBufferParams(12,IndexFormat.UInt16);mesh.SetIndexBufferData(new ushort[]{0,1,2,0,0,0,0,0,0,0,0,0},0,0,12);
                mesh.subMeshCount=1;mesh.SetSubMesh(0,new SubMeshDescriptor(0,3),MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontValidateIndices);
                var source=new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Mesh,sourceObject=mesh,transform=Matrix4x4.identity};
                Assert.IsTrue(NavigationBuildInput.TryCapture(new[]{source,source},new Bounds(Vector3.zero,Vector3.one),.25f,1.7f,out var input,out var reason),reason);
                Assert.AreEqual(84,input.Cost.VertexBytes);Assert.AreEqual(24,input.Cost.IndexBytes);Assert.AreEqual(1,input.Cost.UniqueTriangles);Assert.AreSame(mesh,input.Sources[0].sourceObject);
            }finally{UnityEngine.Object.DestroyImmediate(mesh);}
        }
        [Test] public void UnusedBindPosesOnRigidMeshesRemainSupportedAndCharged(){
            var mesh=new UnityEngine.Mesh{vertices=new[]{Vector3.zero,Vector3.right,Vector3.forward},triangles=new[]{0,1,2},bindposes=new[]{Matrix4x4.identity}};
            try{
                var source=new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Mesh,sourceObject=mesh,transform=Matrix4x4.identity};
                Assert.IsTrue(NavigationBuildInput.TryCapture(new[]{source},new Bounds(Vector3.zero,Vector3.one),.25f,1.7f,out var input,out var reason),reason);
                Assert.AreEqual(64,input.Cost.AuxiliaryBytes);Assert.AreEqual(input.Cost.VertexBytes+input.Cost.IndexBytes+64,input.Cost.CopyBytes);
            }finally{UnityEngine.Object.DestroyImmediate(mesh);}
        }
        [Test] public void BoxWorkUsesExpandedBoundsAndActualVoxelClamp(){
            var source=new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,size=Vector3.one,transform=Matrix4x4.identity};
            Assert.IsTrue(NavigationBuildInput.TryCapture(new[]{source},new Bounds(Vector3.zero,Vector3.one),.1f,1.7f,out var input,out _));
            Assert.AreEqual(.025f,input.VoxelSize);Assert.AreEqual(Vector3.one*1.2f,input.Bounds.size);Assert.AreEqual(0,input.Cost.CopyBytes);
            Assert.IsTrue(NavigationBuildInput.TryCells(input.Bounds.size,input.VoxelSize,out var cells));Assert.AreEqual(cells,input.Cost.VoxelCells);
            Assert.IsFalse(NavigationBuildInput.TryCells(new Vector3(float.MaxValue,1,1),.025f,out _));
        }
        static NavigationSourceCost ScalarCost(long sources=1,long triangles=1,long bytes=1,long cells=1)=>new(sources,sources-1,1,1,triangles,triangles,bytes,0,cells);
        [TestCase("sources")][TestCase("triangles")][TestCase("bytes")][TestCase("cells")]
        public void ProductionPerBuildBoundaryIsInclusiveAndRefusalChangesNoReservation(string dimension){
            var limit=NavigationResources.PerBuild;
            NavigationSourceCost At(long extra)=>dimension switch{
                "sources"=>ScalarCost(sources:limit.Sources+extra),"triangles"=>ScalarCost(triangles:limit.Triangles+extra),
                "bytes"=>ScalarCost(bytes:limit.CopyBytes+extra),_=>ScalarCost(cells:limit.VoxelCells+extra)};
            var ledger=new NavigationBudgetLedger(limit,NavigationResources.Global,RoomNavigationBuild.MaximumConcurrent,NavigationResources.MaximumMaps);
            Assert.AreEqual(NavigationAdmission.Started,ledger.TryReserve("exact",At(0),out var accepted,out _));ledger.Complete(accepted);ledger.ReleaseCopies(accepted);ledger.ReleaseMap(accepted);var before=ledger.Totals;
            Assert.AreEqual(NavigationAdmission.Rejected,ledger.TryReserve("over",At(1),out var refused,out var reason));Assert.IsNull(refused);Assert.AreEqual(NavigationBudgetLedger.BuildLimitReason,reason);Assert.AreEqual(before,ledger.Totals);
        }
        [TestCase("sources")][TestCase("triangles")][TestCase("bytes")][TestCase("cells")]
        public void ProductionGlobalBoundaryRejectsExcessUntilItsSpecificChargeReleases(string dimension){
            var per=NavigationResources.PerBuild;var all=NavigationResources.Global;
            long unit=dimension switch{"sources"=>per.Sources,"triangles"=>per.Triangles,"bytes"=>per.CopyBytes,_=>per.VoxelCells};
            long limit=dimension switch{"sources"=>all.Sources,"triangles"=>all.Triangles,"bytes"=>all.CopyBytes,_=>all.VoxelCells};
            var cost=dimension switch{"sources"=>ScalarCost(sources:unit),"triangles"=>ScalarCost(triangles:unit),"bytes"=>ScalarCost(bytes:unit),_=>ScalarCost(cells:unit)};
            var ledger=new NavigationBudgetLedger(per,all,RoomNavigationBuild.MaximumConcurrent,NavigationResources.MaximumMaps);var held=new System.Collections.Generic.List<NavigationBudgetLedger.Reservation>();
            Assert.AreEqual(0,limit%unit);
            for(int i=0;i<limit/unit;i++){Assert.AreEqual(NavigationAdmission.Started,ledger.TryReserve("owner-"+i,cost,out var value,out _));held.Add(value);ledger.Complete(value);if(dimension=="bytes")ledger.ReleaseMap(value);else ledger.ReleaseCopies(value);}
            var before=ledger.Totals;Assert.AreEqual(NavigationAdmission.Rejected,ledger.TryReserve("excess",ScalarCost(),out _,out var reason));Assert.AreEqual(NavigationBudgetLedger.CapacityReason,reason);Assert.AreEqual(before,ledger.Totals);
            if(dimension=="bytes")ledger.ReleaseCopies(held[0]);else ledger.ReleaseMap(held[0]);
            Assert.AreEqual(NavigationAdmission.Started,ledger.TryReserve("retry",ScalarCost(),out var retry,out _));ledger.Complete(retry);ledger.ReleaseCopies(retry);ledger.ReleaseMap(retry);
            for(int i=1;i<held.Count;i++){if(dimension=="bytes")ledger.ReleaseCopies(held[i]);else ledger.ReleaseMap(held[i]);}Assert.AreEqual(0,ledger.Count);
        }
        [Test] public void ProductionPolicyMatchesTheReviewedMixedAndOverlapEnvelope(){
            var per=NavigationResources.PerBuild;var all=NavigationResources.Global;
            Assert.AreEqual((768L,262144L,16777216L,33554432L),(per.Sources,per.Triangles,per.CopyBytes,per.VoxelCells));
            Assert.AreEqual((3072L,1048576L,33554432L,134217728L),(all.Sources,all.Triangles,all.CopyBytes,all.VoxelCells));
            Assert.AreEqual(8,NavigationResources.MaximumMaps);Assert.AreEqual(2,RoomNavigationBuild.MaximumConcurrent);
        }
        [Test] public void ProductionMapLimitIsIndependentOfTinySourceAndWorkCosts(){
            var ledger=new NavigationBudgetLedger(NavigationResources.PerBuild,NavigationResources.Global,RoomNavigationBuild.MaximumConcurrent,NavigationResources.MaximumMaps);
            Assert.IsTrue(NavigationSourceCost.TryProject(new[]{NavigationSourceLayout.BoxSource},1,out var tiny,out _));var owned=new System.Collections.Generic.List<NavigationBudgetLedger.Reservation>();
            for(int i=0;i<NavigationResources.MaximumMaps;i++){Assert.AreEqual(NavigationAdmission.Started,ledger.TryReserve("owner-"+i,tiny,out var value,out _));ledger.Complete(value);ledger.ReleaseCopies(value);owned.Add(value);}
            Assert.AreEqual(8,ledger.Totals.maps);Assert.AreEqual(NavigationAdmission.Rejected,ledger.TryReserve("ninth",tiny,out _,out var reason));Assert.AreEqual(NavigationBudgetLedger.CapacityReason,reason);
            ledger.ReleaseMap(owned[0]);Assert.AreEqual(NavigationAdmission.Started,ledger.TryReserve("replacement",tiny,out var replacement,out _));ledger.Complete(replacement);ledger.ReleaseCopies(replacement);ledger.ReleaseMap(replacement);for(int i=1;i<owned.Count;i++)ledger.ReleaseMap(owned[i]);Assert.AreEqual(0,ledger.Count);
        }
        [Test] public void ProviderAdmissionDoesNotReserveOriginalGeometryOrConsumeAnOperation(){
            var before=NavigationResources.Totals;int operations=NavigationResources.ActiveOperations;
            Assert.IsTrue(NavigationResources.PolicyMeasured);Assert.IsTrue(NavigationResources.TryCheckSourceSet(Cost(),out _));
            Assert.IsFalse(NavigationResources.TryCheckSourceSet(ScalarCost(sources:NavigationResources.PerBuild.Sources+1),out var reason));Assert.AreEqual(NavigationBudgetLedger.BuildLimitReason,reason);
            Assert.AreEqual(before,NavigationResources.Totals);Assert.AreEqual(operations,NavigationResources.ActiveOperations);
        }
        [Test] public void NonfiniteAndSingularSourcesRefuseBeforeNativeAllocation(){
            var source=new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,size=Vector3.one,transform=Matrix4x4.Scale(Vector3.zero)};
            Assert.IsFalse(NavigationBuildInput.TryCapture(new[]{source},new Bounds(Vector3.zero,Vector3.one),.25f,1.7f,out _,out _));
            source.transform=Matrix4x4.identity;source.size=new Vector3(float.NaN,1,1);
            Assert.IsFalse(NavigationBuildInput.TryCapture(new[]{source},new Bounds(Vector3.zero,Vector3.one),.25f,1.7f,out _,out _));
        }
    }
}
