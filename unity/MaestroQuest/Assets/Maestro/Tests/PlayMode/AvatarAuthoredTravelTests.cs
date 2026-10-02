// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class AvatarSpatialTests
    {
        IEnumerator LoadTravelAvatar(byte[] bytes)
        {
            var asset=ModelLibrary.Inspect("Authored test avatar",bytes);var save=editor.Models.SaveAsync(asset);yield return new WaitUntil(()=>save.IsCompleted);Assert.That(save.Exception,Is.Null);
            Assert.That(editor.SetMaestroModel(asset.Hash),Is.True);yield return new WaitUntil(()=>avatar.ModelLoad.IsCompleted);Assert.That(avatar.ModelLoad.Result,Is.True,avatar.ModelStatus);avatar.SetEditing(true);
        }
        void TravelStage(){Surface(new Vector3(0,-.1f,0),new Vector3(20,.2f,20));Tutor();viewer.transform.position=new Vector3(8,1.6f,8);editor.Find("book").transform.position=new Vector3(7,1,7);foreach(var item in root.GetComponentsInChildren<CreatedRoomObject>())item.transform.position=new Vector3(-7,1,-7);Ready();}
        [UnityTest]public IEnumerator AuthoredTravelKeepsOriginalWorldPoseAcrossLoopSeamsWhileStationaryPlaybackStaysPut()
        {
            TravelStage();yield return LoadTravelAvatar(ModelFixture.HipTravel());var origin=avatar.transform.position;
            Assert.That(avatar.PlayImportedClip(0,true),Is.True);Assert.That(avatar.SampleImportedAt(.5f),Is.True);Assert.That(avatar.transform.position,Is.EqualTo(origin));avatar.StopImportedClip();
            Assert.That(avatar.PlayImportedClip(0,true,true),Is.True,avatar.ImportedPlaybackError);Assert.That(avatar.SampleImportedAt(.99f),Is.True,avatar.ImportedPlaybackError);var before=avatar.transform.position;
            Assert.That(avatar.SampleImportedAt(1.01f),Is.True,avatar.ImportedPlaybackError);var after=avatar.transform.position;Assert.That(Vector3.Distance(before,after),Is.LessThan(.08f));Assert.That(Vector3.Distance(origin,after),Is.GreaterThan(1));
            var final=avatar.transform.position;avatar.StopImportedClip();Assert.That(avatar.transform.position,Is.EqualTo(final));Assert.That(avatar.CustomModel.transform.localPosition,Is.EqualTo(Vector3.zero));Assert.That(editor.Read("maestro").position,Is.EqualTo(final));
        }
        [UnityTest]public IEnumerator AuthoredTravelRefusesWallCrossingAndLostReadinessWithoutMovingPastTheLastAcceptedPose()
        {
            TravelStage();yield return LoadTravelAvatar(ModelFixture.HipTravel());var origin=avatar.transform.position;Assert.That(avatar.PlayImportedClip(0,true,true),Is.True);Assert.That(avatar.SampleImportedAt(.2f),Is.True,avatar.ImportedPlaybackError);var accepted=avatar.transform.position;var direction=(accepted-origin).normalized;
            var wall=Surface(accepted+direction*.5f+Vector3.up,new Vector3(.08f,2,3));Physics.SyncTransforms();Assert.That(avatar.SampleImportedAt(.9f),Is.False);Assert.That(avatar.ImportedPlaybackError,Does.Contain("blocked"));Assert.That(avatar.transform.position,Is.EqualTo(accepted));
            avatar.StopImportedClip();UnityEngine.Object.Destroy(wall);yield return null;world.PausePhysics();Assert.That(avatar.PlayImportedClip(0,true,true),Is.False);Assert.That(avatar.transform.position,Is.EqualTo(accepted));
            world.StartPhysics();Assert.That(avatar.PlayImportedClip(0,true,true),Is.True);tracked=false;Assert.That(avatar.SampleImportedAt(.3f),Is.False);Assert.That(avatar.transform.position,Is.EqualTo(accepted));avatar.StopImportedClip();tracked=true;
        }
        [UnityTest]public IEnumerator ShippedAuthoredMotionMatchesAllSourceJointsAndDeformedVerticesAtTheRoomPlacement()
        {
            TravelStage();var asset=BundledAvatar.FromApplication().Read();yield return LoadTravelAvatar(asset.Bytes);var pack=BundledMotions.FromApplication();
            using var library=new MotionLibrary(Path.Combine(directory,"authored-motions"),includedMotions:pack);yield return new WaitUntil(()=>library.IncludedInitialization.IsCompleted);Assert.That(library.Notice,Is.Null);
            var entry=library.List().First(x=>x.name=="360_Power_Spin_Jump");var request=library.AcquireAsync(entry.id,avatar.CustomModel.MotionRigHash);yield return new WaitUntil(()=>request.IsCompleted);Assert.That(request.Exception,Is.Null);
            var referenceRoot=new GameObject("Original sampled reference");referenceRoot.transform.SetParent(root.transform,false);var reference=referenceRoot.AddComponent<ImportedModel>();var load=reference.LoadAsync(asset);yield return new WaitUntil(()=>load.IsCompleted);Assert.That(load.Exception,Is.Null);reference.FitAsMaestro();
            using var referenceLease=library.AcquireAsync(entry.id,avatar.CustomModel.MotionRigHash).GetAwaiter().GetResult();var mesh=new Mesh();float maximum=0;
            Vector3[] Vertices(ImportedModel model)=>model.Instance.SkinnedMeshRenderers.SelectMany(skin=>{skin.BakeMesh(mesh,true);return mesh.vertices.Select(skin.transform.TransformPoint).ToArray();}).ToArray();
            try{
                Assert.That(avatar.PlayLibraryMotion(request.Result,false,true),Is.True,avatar.ImportedPlaybackError);
                foreach(float fraction in new[]{0f,.17f,.5f,.83f,1f}){
                    float time=entry.duration*fraction;Assert.That(reference.SampleMotion(referenceLease,time,false),Is.True);Assert.That(avatar.SampleImportedAt(time),Is.True,avatar.ImportedPlaybackError);avatar.CustomModel.GetComponent<HumanoidRetargeter>().ApplyPose();
                    var expected=reference.Instance.Nodes;var actual=avatar.CustomModel.Instance.Nodes;Assert.That(actual.Count,Is.EqualTo(expected.Count));
                    for(int i=0;i<actual.Count;i++){Assert.That(Vector3.Distance(expected[i].position,actual[i].position),Is.LessThan(.0002f),"Node "+i+" at "+fraction);Assert.That(Quaternion.Angle(expected[i].rotation,actual[i].rotation),Is.LessThan(.08f),"Rotation "+i);}
                    maximum=Mathf.Max(maximum,Vertices(reference).Zip(Vertices(avatar.CustomModel),Vector3.Distance).Max());
                }
                Assert.That(maximum,Is.LessThan(.0002f));Debug.Log("MAESTRO_AUTHORED_EQUIVALENCE motion="+entry.id+" maximumVertexError="+maximum);
            }finally{avatar.StopImportedClip();UnityEngine.Object.Destroy(mesh);UnityEngine.Object.Destroy(referenceRoot);}
        }
        [UnityTest]public IEnumerator SharedAuthoredActionReportsRoomFailureAndKeepsItsExplicitPolicyOutOfLossySimpleControls()
        {
            TravelStage();yield return LoadTravelAvatar(ModelFixture.HipTravel());avatar.SetEditing(false);
            var call=new JObject {["id"]="animation.play",["version"]=1,["arguments"]=new JObject {["source"]=new JObject {["kind"]="embedded",["modelHash"]=avatar.ModelHash,["clipIndex"]=0},["target"]="maestro",["channel"]="wholeTarget",["seconds"]=1,["loop"]=false,["movement"]="authored"}};
            Assert.That(BehaviourCatalog.TryCall("animation.play",1,(JObject)call["arguments"],out var parsed,out var error),Is.True,error);Assert.That(parsed.TryStep(out _,out _),Is.False);
            var scheduler=new RuleScheduler(new RoomRuleActions(editor,authoring));Assert.That(scheduler.Invoke(call,Time.unscaledTime,out var id,out error),Is.True,error);yield return null;world.PausePhysics();yield return null;scheduler.Tick(Time.unscaledTime);
            Assert.That(scheduler.RunningCount,Is.Zero);Assert.That(avatar.IsImportedClipPlaying,Is.False);Assert.That(avatar.CustomModel.transform.localPosition,Is.EqualTo(Vector3.zero));
        }
    }
}
