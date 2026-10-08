// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class AvatarSpatialTests {
        IEnumerator WaterRoute(Vector3 from,Vector3 to,Action<Vector3[]> done,bool expected=true){
            bool allowed=false;Vector3[] points=Array.Empty<Vector3>();int ticks=0;
            do {allowed=navigation.FollowRoute(from,to,c=>!c.transform.IsChildOf(avatar.transform),out points);ticks++;if(navigation.FollowRoutePending)yield return null;}
            while(navigation.FollowRoutePending&&ticks<200);
            Assert.That(navigation.FollowRoutePending,Is.False,"Route search must remain bounded");
            Assert.That(allowed,Is.EqualTo(expected),navigation.FollowRouteStatus);done(points);
        }
        [UnityTest]public IEnumerator WaterRouteFollowingWalksAroundAPoolWithoutCrossingIt(){
            WaterBasin();viewer.transform.position=new Vector3(3,1.6f,0);motion.SetPreferences(.8f,1.2f);
            Assert.That(motion.Begin("around water",AvatarSpatialMode.Follow,out var e),Is.True,e);
            var previous=avatar.transform.position;float lateral=0;uint built=navigation.BuildRevision;
            for(int i=0;i<360&&avatar.transform.position.x<2.1f;i++){
                yield return null;var current=avatar.transform.position;lateral=Mathf.Max(lateral,Mathf.Abs(current.z));
                Assert.That(editor.Liquids.CheckTraversal(editor.Find("maestro"),previous,current,.25f,1.7f,out var hit),Is.True,hit.Reason);previous=current;
            }
            Assert.That(lateral,Is.GreaterThan(.6f),motion.Status);Assert.That(avatar.transform.position.x,Is.GreaterThan(2.1f),motion.Status);
            Assert.That(navigation.BuildRevision,Is.EqualTo(built),"Liquid routing must not rebake accepted ground");motion.Stop();
        }
        [UnityTest]public IEnumerator WaterRouteFollowingStopsAtTheChosenDistanceWithABookNearTheUser(){
            WaterBasin();viewer.transform.position=new Vector3(3,1.6f,0);var book=editor.Find("book");book.transform.position=new Vector3(2.9f,1,0);
            motion.SetPreferences(.8f,1.2f);Assert.That(motion.Begin("stand off",AvatarSpatialMode.Follow,out var e),Is.True,e);
            for(int i=0;i<360&&avatar.transform.position.x<2.1f;i++)yield return null;
            Assert.That(avatar.transform.position.x,Is.GreaterThan(2.1f),motion.Status);
            Assert.That(Vector3.ProjectOnPlane(viewer.transform.position-avatar.transform.position,Vector3.up).magnitude,Is.GreaterThanOrEqualTo(.78f));motion.Stop();
        }
        [UnityTest]public IEnumerator WaterRouteReplansWhenWaterMovesAcrossAnAlreadyAcceptedPath(){
            string pool=WaterBasin();var liquid=editor.Find(pool);liquid.transform.position=new Vector3(1.4f,-.2f,3);liquid.GetComponent<RigidRoomItem>().Teleported();
            viewer.transform.position=new Vector3(3.5f,1.6f,0);motion.SetPreferences(.8f,1.2f);
            Assert.That(motion.Begin("moving water",AvatarSpatialMode.Follow,out var e),Is.True,e);
            for(int i=0;i<30&&avatar.transform.position.x<.3f;i++)yield return null;
            Assert.That(avatar.transform.position.x,Is.GreaterThan(.1f));uint built=navigation.BuildRevision;
            liquid.transform.position=new Vector3(1.4f,-.2f,0);liquid.GetComponent<RigidRoomItem>().Teleported();Physics.SyncTransforms();
            var previous=avatar.transform.position;float lateral=0;
            for(int i=0;i<420&&avatar.transform.position.x<2.6f;i++){
                yield return null;var current=avatar.transform.position;lateral=Mathf.Max(lateral,Mathf.Abs(current.z));
                Assert.That(editor.Liquids.CheckTraversal(editor.Find("maestro"),previous,current,.25f,1.7f,out var hit),Is.True,hit.Reason);previous=current;
            }
            Assert.That(lateral,Is.GreaterThan(.6f),motion.Status);Assert.That(avatar.transform.position.x,Is.GreaterThan(2.6f),motion.Status);
            Assert.That(navigation.BuildRevision,Is.EqualTo(built));motion.Stop();
        }
        [UnityTest]public IEnumerator WaterRoutePolicyChangeRetiresPendingDetour(){
            WaterBasin();var end=new Vector3(3,0,0);
            Assert.That(navigation.FollowRoute(Vector3.zero,end,c=>!c.transform.IsChildOf(avatar.transform),out _),Is.False);
            Assert.That(navigation.FollowRoutePending,Is.True);
            Assert.That(editor.ConfigureWaterTraversal("maestro",editor.ObjectRevision("maestro"),new(){mode="wade",maxDepthMetres=.25f},out var e),Is.True,e);
            Assert.That(navigation.FollowRoute(Vector3.zero,end,c=>!c.transform.IsChildOf(avatar.transform),out var points),Is.True,navigation.FollowRouteStatus);
            Assert.That(navigation.FollowRoutePending,Is.False);Assert.That(points.All(v=>Mathf.Abs(v.z)<.05f),Is.True);yield return null;
        }
        [UnityTest]public IEnumerator WaterRouteCurrentPolicyAndEmptyWaterRestoreTheDirectPathWithoutRebaking(){
            string pool=WaterBasin();var end=new Vector3(3,0,0);uint built=navigation.BuildRevision;
            yield return WaterRoute(Vector3.zero,end,p=>Assert.That(p.Any(v=>Mathf.Abs(v.z)>.6f),Is.True));
            Assert.That(editor.ConfigureWaterTraversal("maestro",editor.ObjectRevision("maestro"),new(){mode="wade",maxDepthMetres=.25f},out var e),Is.True,e);
            yield return WaterRoute(Vector3.zero,end,p=>Assert.That(p.All(v=>Mathf.Abs(v.z)<.05f),Is.True));
            Assert.That(editor.ConfigureWaterTraversal("maestro",editor.ObjectRevision("maestro"),new(){mode="avoid"},out e),Is.True,e);
            var container=editor.Read(pool).containers[0].Copy();container.amountMl=0;Assert.That(editor.EditContainer(pool,editor.ObjectRevision(pool),container,out e),Is.True,e);
            yield return WaterRoute(Vector3.zero,end,p=>Assert.That(p.All(v=>Mathf.Abs(v.z)<.05f),Is.True));
            Assert.That(navigation.BuildRevision,Is.EqualTo(built));
        }
        [UnityTest]public IEnumerator WaterRouteDiscoversAnotherPoolAndChecksEveryReturnedSegment(){
            WaterBasin();Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Second pool",new Vector3(1.8f,-.2f,-.8f),1,Color.white,out var id,out var e),Is.True,e);
            Assert.That(editor.EditContainer(id,editor.ObjectRevision(id),new(){version=2,rectangle=new(){width=1,depth=1},frame=new(){position=new Vector3(0,.2f,0)},height=.3f,capacityMl=300,amountMl=200},out e),Is.True,e);
            yield return WaterRoute(Vector3.zero,new Vector3(4,0,0),points=>{
                Assert.That(points.Length,Is.GreaterThan(2));
                for(int i=1;i<points.Length;i++)Assert.That(editor.Liquids.CheckTraversal(editor.Find("maestro"),points[i-1],points[i],.25f,1.7f,out var hit),Is.True,hit.Reason);
            });
        }
        [UnityTest]public IEnumerator WaterRouteRefusesAnUnderwaterDestinationAndStopCancelsSearch(){
            WaterBasin();yield return WaterRoute(Vector3.zero,new Vector3(.8f,0,0),p=>Assert.That(p,Is.Empty),false);
            StringAssert.Contains("water",navigation.FollowRouteStatus);
            viewer.transform.position=new Vector3(3,1.6f,0);Assert.That(motion.Begin("cancel route",AvatarSpatialMode.Follow,out var e),Is.True,e);
            for(int i=0;i<10&&!navigation.FollowRoutePending;i++)yield return null;
            Assert.That(navigation.FollowRoutePending,Is.True);motion.Stop();Assert.That(navigation.FollowRoutePending,Is.False);
            var position=avatar.transform.position;yield return new WaitForSeconds(.1f);Assert.That(avatar.transform.position,Is.EqualTo(position));
        }
        [UnityTest]public IEnumerator WaterRouteUsesVirtualGroundBelowTheScannedFloorForBothParticipants(){
            string pool=WaterBasin();world.PausePhysics();
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.transform.SetParent(root.transform,false);ground.transform.position=new Vector3(0,-2.1f,0);ground.transform.localScale=new Vector3(12,.2f,12);ground.layer=RoomPhysicsLayers.Environment;ground.AddComponent<RoomWalkableSurface>().Publish(ground.GetComponent<Collider>());
            var profile=new RoomEnvironmentProfile{id=Guid.NewGuid().ToString("N"),name="Below-floor actors",realCollisions=false};
            Assert.That(editor.EditEnvironment(profile,profile.id,0,Array.Empty<string>(),out var e),Is.True,e);
            foreach(var id in new[]{"maestro",pool})Assert.That(editor.BindEnvironment(id,editor.ObjectRevision(id),profile.id,editor.EnvironmentRevision(profile.id),out e),Is.True,e);
            avatar.transform.position=Vector3.down*2;var liquid=editor.Find(pool);liquid.transform.position+=Vector3.down*2;liquid.GetComponent<RigidRoomItem>().Teleported();
            Physics.SyncTransforms();world.SetSurfaces(false,"No real scan");world.StartPhysics();
            Assert.That(navigation.Prepare(.25f,1.7f,out e,editor.Find("maestro")),Is.True,e);
            yield return WaterRoute(Vector3.down*2,new Vector3(3,-2,0),points=>{
                Assert.That(points.Any(p=>Mathf.Abs(p.z)>.6f),Is.True);Assert.That(points.All(p=>Mathf.Abs(p.y+2)<.01f),Is.True);
            });
        }
    }
}
