// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.Linq;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    internal static class NavigationTestWait
    {
        internal static IEnumerator Built(RoomNavigation navigation){
            float deadline=Time.realtimeSinceStartup+10;
            while(navigation.Ready&&navigation.PathsPending&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(navigation.Ready,Is.True);Assert.That(navigation.PathsPending,Is.False,"Native walking map did not complete");
        }
    }
    public sealed partial class AvatarSpatialTests
    {
        IEnumerator NavigationBuilt()=>NavigationTestWait.Built(navigation);
        [UnityTest]public IEnumerator AsyncNavigationDefersRoutingButUsesCurrentGroundForManualSteps(){
            Terrain(Vector3.zero,.2f,out _);Ready();
            Assert.That(navigation.PathsPending,Is.True);Assert.That(navigation.BuildRevision,Is.Zero);
            Assert.That(navigation.Path(new Vector3(-1,.2f,0),new Vector3(1,.2f,0),new NavMeshPath()),Is.False);
            Assert.That(navigation.Traverse(new Vector3(0,.2f,0),new Vector3(0,.2f,.1f),_=>false,out var foot,out var error),Is.True,error);
            Assert.That(foot.y,Is.EqualTo(.2f).Within(.001));yield return NavigationBuilt();
            Assert.That(navigation.BuildRevision,Is.EqualTo(1));Assert.That(navigation.Path(new Vector3(-1,.2f,0),new Vector3(1,.2f,0),new NavMeshPath()),Is.True);
        }
        [UnityTest]public IEnumerator AsyncNavigationReplacesPendingTerrainWithoutPublishingOldGeometry(){
            var field=Terrain(Vector3.zero,.2f,out var data);Ready();
            var original=((MeshCollider)field.Collision).sharedMesh;var copies=Resources.FindObjectsOfTypeAll<Mesh>().Where(m=>m.name=="Walking build snapshot").ToArray();
            Assert.That(copies.Length,Is.GreaterThan(0));Assert.That(copies,Has.None.SameAs(original));
            data.heights=Enumerable.Repeat(.4f,81).ToArray();field.Apply(new[]{data});Physics.SyncTransforms();
            Assert.That(navigation.Sample(new Vector3(0,.2f,0),.05f,out _),Is.False);
            Assert.That(navigation.Sample(new Vector3(0,.4f,0),.05f,out _),Is.True);
            Assert.That(navigation.BuildRevision,Is.Zero);yield return NavigationBuilt();
            Assert.That(navigation.BuildRevision,Is.EqualTo(1));Assert.That(navigation.Sample(new Vector3(0,.2f,0),.05f,out _),Is.False);
            Assert.That(navigation.Sample(new Vector3(0,.4f,0),.05f,out _),Is.True);
        }
        [UnityTest]public IEnumerator AsyncNavigationPauseCancelsPendingPublicationAndDoesNotResumeIt(){
            Terrain(Vector3.zero,.2f,out _);Ready();Assert.That(navigation.PathsPending,Is.True);world.PausePhysics();EmptyNavigationCapture();
            for(int i=0;i<8;i++)yield return null;
            Assert.That(navigation.BuildRevision,Is.Zero);Assert.That(RoomNavigationBuild.ActiveCount,Is.Zero);
            world.StartPhysics();yield return null;Assert.That(navigation.BuildRevision,Is.Zero);
            Assert.That(navigation.Prepare(.25f,1.7f,out var error),Is.True,error);yield return NavigationBuilt();Assert.That(navigation.BuildRevision,Is.EqualTo(1));
        }
        [UnityTest]public IEnumerator AsyncNavigationInstallsCompletedMapAtTheCurrentPhysicalFrame(){
            var field=Terrain(Vector3.zero,.2f,out _);Ready();physicalRoot.transform.SetPositionAndRotation(new Vector3(8,2,6),Quaternion.Euler(0,45,0));Physics.SyncTransforms();
            yield return NavigationBuilt();var point=field.transform.TransformPoint(Vector3.up*.2f);
            Assert.That(navigation.Sample(point,.05f,out var foot),Is.True);Assert.That(Vector3.Distance(point,foot),Is.LessThan(.01));Assert.That(navigation.BuildRevision,Is.EqualTo(1));
            Assert.That(navigation.Sample(Vector3.up*.2f,.05f,out _),Is.False);
        }
        [UnityTest]public IEnumerator AsyncNavigationBoundsConcurrentWorkAndEveryConsumerCanFinish(){
            Terrain(Vector3.zero,.2f,out _);Ready();var others=new RoomNavigation[3];
            for(int i=0;i<others.Length;i++){
                var child=new GameObject("Independent walking map");child.transform.SetParent(physicalRoot.transform,false);others[i]=child.AddComponent<RoomNavigation>();others[i].Initialize(world);
                Assert.That(others[i].Prepare(.25f,1.7f,out var error),Is.True,error);Assert.That(RoomNavigationBuild.ActiveCount,Is.LessThanOrEqualTo(RoomNavigationBuild.MaximumConcurrent));
            }
            yield return NavigationBuilt();foreach(var other in others)yield return NavigationTestWait.Built(other);
            Assert.That(RoomNavigationBuild.ActiveCount,Is.Zero);foreach(var other in others)Assert.That(other.BuildRevision,Is.EqualTo(1));
        }
        [UnityTest]public IEnumerator AsyncNavigationChangedGroundRetiresInstalledRouteBeforeWaitingForReplacement(){
            var field=Terrain(Vector3.zero,.2f,out var data);Ready();yield return NavigationBuilt();var filter=NavigationFilter();uint build=navigation.BuildRevision;
            Assert.That(NavMesh.SamplePosition(Vector3.up*.2f,out _,.05f,filter),Is.True);
            data.heights=Enumerable.Repeat(.4f,81).ToArray();field.Apply(new[]{data});Assert.That(navigation.Ready,Is.True);Assert.That(navigation.PathsPending,Is.True);
            Assert.That(NavMesh.SamplePosition(Vector3.up*.2f,out _,.05f,filter),Is.False,"Retired paths cannot remain globally available");
            Assert.That(navigation.BuildRevision,Is.EqualTo(build));yield return NavigationBuilt();Assert.That(navigation.BuildRevision,Is.EqualTo(build+1));
        }
        [UnityTest]public IEnumerator AsyncNavigationAuthoredSeekPreservesItsFirstRequestedTimeWhileWaiting(){
            TravelStage();yield return LoadTravelAvatar(ModelFixture.HipPath(new[]{0f,.001f,.002f,.004f,.5f,1f},new[]{0f,2f,0f,0f,1f,0f}));avatar.PauseOwnedPlayback(this,true);
            try{
                Assert.That(avatar.PlayImportedClip(0,false,true),Is.True);var before=avatar.transform.position;
                Assert.That(avatar.SampleImportedAt(.004f),Is.False);Assert.That(avatar.ImportedPlaybackError,Is.Null);
                Assert.That((string)AuthoredInputFact()["state"],Is.EqualTo("pending"));Assert.That(avatar.transform.position,Is.EqualTo(before));
                yield return NavigationBuilt();Assert.That((string)AuthoredInputFact()["state"],Is.EqualTo("ready"));
                Assert.That(avatar.SampleImportedAt(.5f),Is.True,avatar.ImportedPlaybackError);Assert.That(avatar.transform.position,Is.EqualTo(before));
            }finally{avatar.StopImportedClip();avatar.PauseOwnedPlayback(this,false);}
        }
        [UnityTest]public IEnumerator AsyncNavigationDestroyingOwnerDrainsNativeWorkWithoutInstallingAnything(){
            Terrain(Vector3.zero,.2f,out _);Ready();var old=navigation;Object.Destroy(navigation);yield return null;
            float until=Time.realtimeSinceStartup+10;while(RoomNavigationBuild.ActiveCount>0&&Time.realtimeSinceStartup<until)yield return null;
            Assert.That(old==null,Is.True);Assert.That(RoomNavigationBuild.ActiveCount,Is.Zero);yield return null;
            Assert.That(Resources.FindObjectsOfTypeAll<Mesh>().Count(m=>m.name=="Walking build snapshot"),Is.Zero);
        }
    }
}
