// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed class NavigationResourceLifetimeTests {
        static NavigationBuildInput Input(Mesh mesh=null){
            var source=new NavMeshBuildSource{shape=mesh?NavMeshBuildSourceShape.Mesh:NavMeshBuildSourceShape.Box,sourceObject=mesh,size=new Vector3(4,.1f,4),transform=Matrix4x4.identity};
            Assert.IsTrue(NavigationBuildInput.TryCapture(new[]{source},new Bounds(Vector3.zero,new Vector3(4,.1f,4)),.25f,1.7f,out var input,out var reason),reason);return input;
        }
        static NavigationResourcePool Pool(long sources=100)=>new(new NavigationLimits(100,10000,1000000,10000000),new NavigationLimits(sources,10000,1000000,10000000),2);
        static IEnumerator Complete(RoomNavigationBuild build){float until=Time.realtimeSinceStartup+10;while(!build.Done&&Time.realtimeSinceStartup<until)yield return null;Assert.IsTrue(build.Done);}
        static IEnumerator Empty(NavigationResourcePool pool){float until=Time.realtimeSinceStartup+10;while(pool.Count!=0&&Time.realtimeSinceStartup<until){pool.Collect();yield return null;}Assert.AreEqual(0,pool.Count);Assert.AreEqual(0,pool.ActiveOperations);}
        [UnityTest] public IEnumerator CompletedMapTransfersOnceAndRetainsCapacityThroughDeferredDestruction(){
            var pool=Pool(1);RoomNavigationBuild build=null;RoomNavigationMap map=null;
            try{
                Assert.AreEqual(NavigationAdmission.Started,RoomNavigationBuild.TryStart(Input(),"owner",pool,out build,out var reason),reason);yield return Complete(build);
                Assert.AreEqual(0,pool.ActiveOperations);Assert.AreEqual(1,pool.Totals.maps);
                map=build.Take();Assert.Throws<InvalidOperationException>(()=>build.Take());build.Dispose();
                Assert.IsTrue(map.TryInstall(Vector3.zero,Quaternion.identity,out _));Assert.IsTrue(map.Installed);
                Assert.AreEqual(NavigationAdmission.Rejected,RoomNavigationBuild.TryStart(Input(),"other",pool,out _,out reason));Assert.AreEqual(NavigationBudgetLedger.CapacityReason,reason);
                map.Dispose();map.Dispose();Assert.IsFalse(map.Installed);Assert.AreEqual(1,pool.Totals.maps,"Destroy is deferred; a logical Dispose cannot free physical capacity");
                yield return Empty(pool);
            }finally{map?.Dispose();build?.Dispose();}
        }
        [UnityTest] public IEnumerator CancelledBuildKeepsCopiesUntilCompletionThenReleasesEverything(){
            var mesh=new Mesh{vertices=new[]{new Vector3(-2,0,-2),new Vector3(-2,0,2),new Vector3(2,0,2),new Vector3(2,0,-2)},triangles=new[]{0,1,2,0,2,3}};
            var pool=Pool();RoomNavigationBuild build=null;
            try{
                Assert.AreEqual(NavigationAdmission.Started,RoomNavigationBuild.TryStart(Input(mesh),"cancelled-owner",pool,out build,out var reason),reason);
                Assert.Greater(pool.Totals.copyBytes,0);build.Dispose();build.Dispose();Assert.Throws<InvalidOperationException>(()=>build.Take());
                yield return Empty(pool);Assert.IsTrue(mesh,"The original source is not a navigation-owned copy");
            }finally{build?.Dispose();UnityEngine.Object.Destroy(mesh);}
            yield return null;
        }
        [UnityTest] public IEnumerator InvalidNativeInstallationReportsFailureAndStillOwnsRetiringResources(){
            var input=Input();var pool=new NavigationResourcePool(NavigationLimits.Unmeasured,NavigationLimits.Unmeasured,2,maximumMaps:1);
            Assert.AreEqual(NavigationAdmission.Started,pool.TryBegin("failed-install",input.Cost,out var lease,out _));
            int agent=NavMesh.CreateSettings().agentTypeID;var data=new NavMeshData(agent);lease.Own(data);lease.Complete();
            int calls=0;var map=new RoomNavigationMap(data,agent,lease,(_,_,_)=>{calls++;return default;});
            try{
                Assert.IsFalse(map.TryInstall(Vector3.zero,Quaternion.identity,out var changed));Assert.IsTrue(changed);Assert.IsFalse(map.Installed);Assert.AreEqual(1,calls);
                Assert.AreEqual(1,pool.Totals.maps);Assert.AreEqual(NavigationAdmission.Rejected,pool.TryBegin("other",input.Cost,out _,out var reason));Assert.AreEqual(NavigationBudgetLedger.CapacityReason,reason);
                map.Dispose();Assert.AreEqual(-1,map.AgentType);Assert.AreEqual(1,pool.Totals.maps);yield return Empty(pool);
                Assert.That(data==null,Is.True);Assert.Throws<InvalidOperationException>(()=>map.TryInstall(Vector3.zero,Quaternion.identity,out _));
            }finally{map.Dispose();}
        }
        [UnityTest] public IEnumerator PerBuildRefusalAllocatesNoNativeMapOrSettings(){
            var tiny=new NavigationLimits(1,0,0,1);var pool=new NavigationResourcePool(tiny,tiny,1);int settings=NavMesh.GetSettingsCount();
            Assert.AreEqual(NavigationAdmission.Rejected,RoomNavigationBuild.TryStart(Input(),"too-large",pool,out var build,out var reason));Assert.IsNull(build);Assert.AreEqual(NavigationBudgetLedger.BuildLimitReason,reason);
            Assert.AreEqual(0,pool.Count);Assert.AreEqual(settings,NavMesh.GetSettingsCount());yield return null;
        }
    }
}
