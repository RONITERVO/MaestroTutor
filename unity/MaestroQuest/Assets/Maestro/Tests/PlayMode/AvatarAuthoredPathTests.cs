// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class AvatarSpatialTests {
        IEnumerator AuthoredPathSample(float time,bool expected=true){
            bool result=avatar.SampleImportedAt(time);float deadline=Time.realtimeSinceStartup+10;
            while(!result&&avatar.ImportedPlaybackError==null&&Time.realtimeSinceStartup<deadline){yield return null;result=avatar.SampleImportedAt(time);}
            Assert.That(result,Is.EqualTo(expected),avatar.ImportedPlaybackError);
        }
        static byte[] AuthoredOutAndBack()=>ModelFixture.HipPath(new[]{0f,.001f,.002f,1f},new[]{0f,2f,0f,0f});
        Vector3 AuthoredExcursion(){
            Assert.That(avatar.PlayImportedClip(0,false),Is.True);Assert.That(avatar.SampleImportedAt(.001f),Is.True);
            var excursion=avatar.transform.TransformVector(Vector3.ProjectOnPlane(avatar.PoseRig.ImportedHipOffset,Vector3.up));
            Assert.That(excursion.magnitude,Is.GreaterThan(1));avatar.StopImportedClip();return excursion;
        }
        string RetireAuthoredPathWall(Vector3 position){
            world.PausePhysics();Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Intermediate authored wall",position,.3f,Color.white,out var id,out var error),Is.True,error);
            var data=editor.Read(id);data.physics=ItemPhysics.Fixed;Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);Physics.SyncTransforms();
            var region=new RegionSaveCapability();Assert.That(region.Start(new CapabilityContext(editor,authoring),"authored-path-region",region.Example,out var saved,out error),Is.True,error);string area=(string)saved.Result["id"];
            Assert.That(editor.BindRegion(id,editor.ObjectRevision(id),area,editor.RegionRevision(area),out error),Is.True,error);
            Assert.That(editor.RetireNativeArea(area,out error),Is.True,error);
            world.ConfigureCollisionAdmission(editor,()=>null);world.StartPhysics();Assert.That(world.Running,Is.True);return id;
        }
        [UnityTest]public IEnumerator AuthoredPathRejectsAnOutAndBackThroughSolidGeometryWithinOneFrame(){
            TravelStage();yield return LoadTravelAvatar(AuthoredOutAndBack());avatar.PauseOwnedPlayback(this,true);
            try{
                var origin=avatar.transform.position;var excursion=AuthoredExcursion();Surface(origin+excursion*.5f+Vector3.up,new Vector3(.2f,2,.2f));Physics.SyncTransforms();
                Assert.That(avatar.PlayImportedClip(0,false,true),Is.True,avatar.ImportedPlaybackError);
                var nodes=avatar.CustomModel.Instance.Nodes.ToArray();var positions=nodes.Select(n=>n.localPosition).ToArray();
                yield return AuthoredPathSample(.003f,false);Assert.That(avatar.ImportedPlaybackError,Does.Contain("blocked"));Assert.That(avatar.transform.position,Is.EqualTo(origin));
                for(int i=0;i<nodes.Length;i++)Assert.That(Vector3.Distance(nodes[i].localPosition,positions[i]),Is.LessThan(.00001f));
            }finally{avatar.StopImportedClip();avatar.PauseOwnedPlayback(this,false);}
        }
        [UnityTest]public IEnumerator AuthoredPathPreparesTheIntermediateObstacleBeforeRejectingTheWholeInterval(){
            TravelStage();yield return LoadTravelAvatar(AuthoredOutAndBack());avatar.PauseOwnedPlayback(this,true);
            try{
                var origin=avatar.transform.position;var excursion=AuthoredExcursion();string id=RetireAuthoredPathWall(origin+excursion*.5f+Vector3.up*.6f);
                Assert.That(avatar.PlayImportedClip(0,false,true),Is.True,avatar.ImportedPlaybackError);Assert.That(editor.Find(id),Is.Null);
                Assert.That(avatar.SampleImportedAt(.003f),Is.False);Assert.That(avatar.ImportedPlaybackError,Is.Null);Assert.That((string)AuthoredInputFact()["state"],Is.EqualTo("pending"));Assert.That(avatar.transform.position,Is.EqualTo(origin));
                yield return AuthoredInputsLoaded();Assert.That(editor.Find(id),Is.Not.Null);
                yield return AuthoredPathSample(.5f,false);Assert.That(avatar.ImportedPlaybackError,Does.Contain("blocked"));Assert.That(avatar.transform.position,Is.EqualTo(origin));
            }finally{avatar.StopImportedClip();avatar.PauseOwnedPlayback(this,false);}
        }
        [UnityTest]public IEnumerator AuthoredPathLibraryPlaybackKeepsTheSameIntermediateSourceKeys(){
            TravelStage();var bytes=AuthoredOutAndBack();yield return LoadTravelAvatar(bytes);avatar.PauseOwnedPlayback(this,true);
            try{
                var origin=avatar.transform.position;var excursion=AuthoredExcursion();Surface(origin+excursion*.5f+Vector3.up,new Vector3(.2f,2,.2f));Physics.SyncTransforms();
                var imported=editor.Motions.ImportAsync("subframe.glb",bytes);yield return new WaitUntil(()=>imported.IsCompleted);Assert.That(imported.Exception,Is.Null);var entry=imported.Result.Single();
                var loaded=editor.Motions.AcquireAsync(entry.id,avatar.CustomModel.MotionRigHash);yield return new WaitUntil(()=>loaded.IsCompleted);Assert.That(loaded.Exception,Is.Null);using var lease=loaded.Result;
                Assert.That(avatar.PlayLibraryMotion(lease,false,true),Is.True,avatar.ImportedPlaybackError);Assert.That(avatar.LibraryMotionId,Is.EqualTo(entry.id));
                yield return AuthoredPathSample(.003f,false);Assert.That(avatar.ImportedPlaybackError,Does.Contain("blocked"));Assert.That(avatar.transform.position,Is.EqualTo(origin));
            }finally{avatar.StopImportedClip();avatar.PauseOwnedPlayback(this,false);}
        }
        [UnityTest]public IEnumerator AuthoredPathReverseSamplingAlsoChecksTheIntermediateExcursion(){
            TravelStage();yield return LoadTravelAvatar(AuthoredOutAndBack());avatar.PauseOwnedPlayback(this,true);
            try{
                var excursion=AuthoredExcursion();Assert.That(avatar.PlayImportedClip(0,false,true),Is.True);yield return AuthoredPathSample(.004f);var accepted=avatar.transform.position;
                Surface(accepted+excursion*.5f+Vector3.up,new Vector3(.2f,2,.2f));Physics.SyncTransforms();
                yield return AuthoredPathSample(0,false);Assert.That(avatar.ImportedPlaybackError,Does.Contain("blocked"));Assert.That(avatar.transform.position,Is.EqualTo(accepted));
            }finally{avatar.StopImportedClip();avatar.PauseOwnedPlayback(this,false);}
        }
        [UnityTest]public IEnumerator AuthoredPathCubicTurningPointCannotPassThroughAnObstacleBetweenItsOnlyKeys(){
            TravelStage();yield return LoadTravelAvatar(ModelFixture.HipPath(new[]{0f,.001f},new[]{0f,0f,8000f,-8000f,0f,0f},true));avatar.PauseOwnedPlayback(this,true);
            try{
                Assert.That(avatar.PlayImportedClip(0,false),Is.True);Assert.That(avatar.SampleImportedAt(.0005f),Is.True);
                var excursion=avatar.transform.TransformVector(Vector3.ProjectOnPlane(avatar.PoseRig.ImportedHipOffset,Vector3.up));Assert.That(excursion.magnitude,Is.GreaterThan(1));avatar.StopImportedClip();var before=avatar.transform.position;
                Surface(before+excursion*.5f+Vector3.up,new Vector3(.2f,2,.2f));Physics.SyncTransforms();
                Assert.That(avatar.PlayImportedClip(0,false,true),Is.True,avatar.ImportedPlaybackError);yield return AuthoredPathSample(.001f,false);Assert.That(avatar.ImportedPlaybackError,Does.Contain("blocked"));Assert.That(avatar.transform.position,Is.EqualTo(before));
            }finally{avatar.StopImportedClip();avatar.PauseOwnedPlayback(this,false);}
        }
        [UnityTest]public IEnumerator AuthoredPathChecksIntermediateWaterAndHonoursTheActorsExplicitIgnorePolicy(){
            TravelStage();yield return LoadTravelAvatar(AuthoredOutAndBack());avatar.PauseOwnedPlayback(this,true);
            try{
                var origin=avatar.transform.position;var excursion=AuthoredExcursion();world.PausePhysics();editor.Liquids.enabled=false;
                Assert.That(editor.CreatePrimitive(RoomObjectKind.Block,"Intermediate water",origin+excursion*.5f+Vector3.down*.6f,1,Color.white,out var pool,out var error),Is.True,error);
                var data=editor.Read(pool);data.physics=ItemPhysics.Fixed;Assert.That(editor.ApplyAgentEdit(editor.Revision,new[]{data},Array.Empty<string>(),out error),Is.True,error);
                Assert.That(editor.EditContainer(pool,editor.ObjectRevision(pool),new RoomContainer{version=2,rectangle=new(){width=1,depth=1},frame=new(){position=Vector3.up*.6f},height=.5f,capacityMl=500000,amountMl=500000},out error),Is.True,error);
                Assert.That(editor.ConfigureWaterTraversal("maestro",editor.ObjectRevision("maestro"),new(){mode="avoid"},out error),Is.True,error);world.StartPhysics();Physics.SyncTransforms();
                Assert.That(avatar.PlayImportedClip(0,false,true),Is.True,avatar.ImportedPlaybackError);yield return AuthoredPathSample(.003f,false);Assert.That(avatar.ImportedPlaybackError,Does.Contain("water"));Assert.That(avatar.transform.position,Is.EqualTo(origin));avatar.StopImportedClip();
                Assert.That(editor.ConfigureWaterTraversal("maestro",editor.ObjectRevision("maestro"),new(){mode="ignore"},out error),Is.True,error);
                Assert.That(avatar.PlayImportedClip(0,false,true),Is.True,avatar.ImportedPlaybackError);yield return AuthoredPathSample(.003f);Assert.That(Vector3.Distance(avatar.transform.position,origin),Is.LessThan(.00001f));
            }finally{avatar.StopImportedClip();avatar.PauseOwnedPlayback(this,false);}
        }
        [UnityTest]public IEnumerator AuthoredPathExcessiveSeekStopsWithoutPublishingAnIntermediatePose(){
            TravelStage();yield return LoadTravelAvatar(ModelFixture.HipTravel());avatar.PauseOwnedPlayback(this,true);
            try{
                Assert.That(avatar.PlayImportedClip(0,true,true),Is.True);var before=avatar.transform.position;
                var nodes=avatar.CustomModel.Instance.Nodes.ToArray();var positions=nodes.Select(n=>n.localPosition).ToArray();
                yield return AuthoredPathSample(3,false);Assert.That(avatar.ImportedPlaybackError,Does.Contain("budget"));Assert.That(avatar.transform.position,Is.EqualTo(before));
                for(int i=0;i<nodes.Length;i++)Assert.That(Vector3.Distance(nodes[i].localPosition,positions[i]),Is.LessThan(.00001f));
            }finally{avatar.StopImportedClip();avatar.PauseOwnedPlayback(this,false);}
        }
    }
}
