// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Reflection;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class AvatarSpatialTests
    {
        T NavigationField<T>(string name)=>(T)typeof(RoomNavigation).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(navigation);
        NavMeshQueryFilter NavigationFilter()=>new(){agentTypeID=NavigationField<int>("agentType"),areaMask=NavMesh.AllAreas};
        void EmptyNavigationCapture()
        {
            foreach(string name in new[]{"accepted","candidate"}){
                var capture=NavigationField<RoomNavigationGeometry>(name);Assert.AreEqual(0,capture.BuildSources.Count,name+" must not retain sources");
                foreach(string list in new[]{"colliders","entries","dependencies"})Assert.AreEqual(0,((ICollection)typeof(RoomNavigationGeometry).GetField(list,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(capture)).Count,name+" must not retain "+list);
                Assert.AreEqual(default(Bounds),capture.Bounds);
            }
            Assert.IsNull(NavigationField<NavMeshData>("data"));Assert.AreEqual(-1,NavigationField<int>("agentType"));
        }
        [UnityTest] public IEnumerator NavigationLifetimeDisableRemovesInstalledMapWithoutAnotherQuery()
        {
            Terrain(Vector3.zero,.2f,out _);Ready();var point=Vector3.up*.2f;var filter=NavigationFilter();uint build=navigation.BuildRevision;
            Assert.IsTrue(NavMesh.SamplePosition(point,out _,.2f,filter));navigation.enabled=false;
            Assert.IsFalse(NavMesh.SamplePosition(point,out _,.2f,filter),"Disabling navigation must immediately remove its global route map");EmptyNavigationCapture();
            navigation.enabled=true;Assert.IsFalse(navigation.Ready,"Re-enabling must not resume an old actor configuration");
            Assert.IsTrue(navigation.Prepare(.25f,1.7f,out var error),error);Assert.Greater(navigation.BuildRevision,build);Assert.IsTrue(navigation.Sample(point,.08f,out _));yield return null;
        }
        [UnityTest] public IEnumerator NavigationLifetimeDisabledComponentRefusesEveryMovementEntry()
        {
            Terrain(Vector3.zero,.2f,out _);Ready();navigation.enabled=false;var from=Vector3.up*.2f;var to=from+Vector3.forward*.1f;
            Assert.IsFalse(navigation.Ready);Assert.IsFalse(navigation.Prepare(.25f,1.7f,out _));Assert.IsFalse(navigation.Sample(from,.2f,out _));
            Assert.IsFalse(navigation.DirectStep(from,to,out _));Assert.IsFalse(navigation.Traverse(from,to,_=>false,out _,out _));
            Assert.IsFalse(navigation.ClearAuthoredStep(from,to,0,_=>false));Assert.IsFalse(navigation.Path(from,to,new NavMeshPath()));Assert.IsFalse(navigation.FollowRoute(from,to,_=>false,out _));
            EmptyNavigationCapture();yield return null;
        }
        [UnityTest] public IEnumerator NavigationLifetimePhysicsPauseRetiresMapAndGeometryImmediately()
        {
            var field=Terrain(Vector3.zero,.2f,out _);Ready();var filter=NavigationFilter();Assert.IsTrue(NavigationField<RoomNavigationGeometry>("accepted").Ground.Contains(field.Collision));
            world.PausePhysics();Assert.IsFalse(NavMesh.SamplePosition(Vector3.up*.2f,out _,.2f,filter),"Physics suspension must not leave a global route map until someone queries it");
            EmptyNavigationCapture();Assert.IsFalse(NavigationField<RoomNavigationGeometry>("accepted").Ground.Contains(field.Collision));
            world.StartPhysics();Assert.IsTrue(navigation.Prepare(.25f,1.7f,out var error),error);Assert.IsTrue(navigation.Sample(Vector3.up*.2f,.08f,out _));yield return null;
        }
        [UnityTest] public IEnumerator NavigationLifetimeMissingGroundReleasesCapturedMeshes()
        {
            var field=Terrain(Vector3.zero,.2f,out _);Ready();var collision=field.Collision;field.Apply(null);Assert.IsFalse(navigation.Ready);
            EmptyNavigationCapture();Assert.IsFalse(NavigationField<RoomNavigationGeometry>("accepted").Ground.Contains(collision));yield return null;
        }
        [UnityTest] public IEnumerator NavigationLifetimeDisabledActorCannotFallBackToUnownedRouting()
        {
            Terrain(Vector3.zero,.2f,out _);Tutor();Ready();var item=avatar.GetComponent<RoomItem>();Assert.IsTrue(navigation.Prepare(.25f,1.7f,out var error,item),error);
            item.enabled=false;Assert.IsFalse(navigation.Ready,"Disabled actors must not retain or rebuild a walking map");EmptyNavigationCapture();Assert.IsFalse(navigation.Prepare(.25f,1.7f,out _,item));
            item.enabled=true;Assert.IsTrue(navigation.Prepare(.25f,1.7f,out error,item),error);UnityEngine.Object.Destroy(item);yield return null;
            Assert.IsFalse(navigation.Ready,"A destroyed actor must not be treated as an intentionally actorless query");EmptyNavigationCapture();
        }
        [UnityTest] public IEnumerator NavigationLifetimeRebindingDropsOldWorldSubscriptionAndMap()
        {
            Terrain(Vector3.zero,.2f,out _);Ready();var filter=NavigationFilter();var next=new GameObject("Replacement physics world");
            try{
                var replacement=next.AddComponent<RoomPhysicsWorld>();navigation.Initialize(replacement);
                Assert.IsFalse(NavMesh.SamplePosition(Vector3.up*.2f,out _,.2f,filter));EmptyNavigationCapture();Assert.IsFalse(navigation.Ready);
                var listeners=(Action)typeof(RoomPhysicsWorld).GetField("Changed",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(world);
                Assert.IsFalse(Array.Exists(listeners?.GetInvocationList()??Array.Empty<Delegate>(),listener=>ReferenceEquals(listener.Target,navigation)),"The previous world must not retain navigation through its event");
            }finally{navigation.Initialize(world);UnityEngine.Object.Destroy(next);}
            yield return null;
        }
    }
}
