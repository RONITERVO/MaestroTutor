// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        [UnityTest]public IEnumerator ObjectWalkKeepsMaestroProtocolWhileAvatarIsUnavailable(){
            Assert.That(editor.Find("maestro").GetComponent<Maestro.Quest.Avatar.MaestroAvatar>(),Is.Null);
            var view=AvatarWalkSelection.Observe(editor);Assert.That(view.source,Is.EqualTo("included"));Assert.That(view.available,Is.False);Assert.That(view.playbackStatus,Is.Empty);
            Assert.That(view.status,Does.Contain("loading"));yield return null;
            UnityEngine.Object.Destroy(editor.Find("maestro").gameObject);yield return null;
            view=AvatarWalkSelection.Observe(editor);Assert.That(view,Is.Not.Null);Assert.That(view.source,Is.EqualTo("included"));Assert.That(view.available,Is.False);Assert.That(view.playbackStatus,Is.Empty);
        }
        [UnityTest]public IEnumerator ObjectWalkAdvertisesItsSharedFeature(){Assert.That(RoomControls.Capabilities(editor),Does.Contain(ObjectWalkSettingsCapability.Feature));yield return null;}
    }
    public sealed partial class AvatarSpatialTests {
        RoomItem gaitItem;ImportedModel gaitModel;
        IEnumerator WalkingCharacter(byte[] bytes=null){
            WalkingScene();avatar.transform.position=new(0,0,3);
            var asset=ModelLibrary.Inspect("Walking character.glb",bytes??ModelFixture.WalkingCharacter());var save=editor.Models.SaveAsync(asset);yield return new WaitUntil(()=>save.IsCompleted);Assert.That(save.Exception,Is.Null);
            Assert.That(editor.CreateImportedModel(asset.Hash,out var id,out var error),Is.True,error);gaitItem=editor.Find(id);
            var created=gaitItem.GetComponent<CreatedRoomObject>();yield return new WaitUntil(()=>!created.ModelLoading);gaitModel=created.Model;Assert.That(gaitModel&&gaitModel.Ready,Is.True,created.ModelStatus);
            Physics.SyncTransforms();Assert.That(GroundedMovement.LeaderFoot(gaitItem,out var foot),Is.True);
            Assert.That(editor.MoveObject(id,gaitItem.transform.position+new Vector3(-2,0,-2)-foot,out error),Is.True,error);Physics.SyncTransforms();Ready();
        }
        bool SelectGait(string source,string motionId,out string error)=>editor.ConfigureObjectWalk(editor.Identity(gaitItem),editor.ObjectRevision(editor.Identity(gaitItem)),source,gaitModel.AssetHash,motionId,0,out error);
        [UnityTest]public IEnumerator ObjectWalkSharedChoiceSavesWithoutPlayingAndUndoRetainsIt(){
            yield return WalkingCharacter();string id=editor.Identity(gaitItem);
            var action=new ObjectWalkSettingsCapability();var args=new JObject{["target"]=id,["revision"]=editor.ObjectRevision(id),["modelHash"]=gaitModel.AssetHash,["source"]="embedded",["clipIndex"]=0};
            Assert.That(BehaviourCatalog.TryCall(action.Id,1,args,out _,out var error),Is.True,error);
            Assert.That(SelectGait("embedded",null,out error),Is.True,error);Assert.That(gaitModel.IsPlaying,Is.False);
            var saved=new RoomStorage(directory).Load(out error);Assert.That(error,Is.Null);Assert.That(saved.objects.Single(o=>o.id==id).walkClip,Is.EqualTo(1));
            Assert.That((string)SettingsFact("object.walk.settings",id)["selection"]["source"],Is.EqualTo("embedded"));Assert.That(editor.ConfigureObjectWalk(id,(int)args["revision"],"none",gaitModel.AssetHash,null,-1,out error),Is.False,"Stale revisions cannot replace a selection");
            Assert.That(SelectGait("embedded",null,out error),Is.True,error);editor.Undo();Assert.That(editor.Read(id).walkClip,Is.Zero,"Identical save does not add Undo");editor.Redo();Assert.That(editor.Read(id).walkClip,Is.EqualTo(1));
        }
        [UnityTest]public IEnumerator ObjectWalkSavedChoiceSurvivesNativeLossAndPreparation(){
            yield return WalkingCharacter();Assert.That(SelectGait("embedded",null,out var error),Is.True,error);world.PausePhysics();
            string id=editor.Identity(gaitItem),hash=gaitModel.AssetHash,before=JsonUtility.ToJson(editor.Snapshot());int revision=editor.ObjectRevision(id);
            var retirement=editor.RetireNativeArea("");
            for(int i=0;i<600&&!retirement.IsCompleted;i++)yield return null;
            Assert.That(retirement.IsCompleted,Is.True);Assert.That(retirement.Exception,Is.Null);Assert.That(retirement.Result,Is.Null);yield return null;
            Assert.That(editor.NativeEntityDormant(id),Is.True);
            var lost=SettingsFact("object.walk.settings",id);var selection=(JObject)lost["selection"];
            Assert.That((string)selection["source"],Is.EqualTo("embedded"));Assert.That((string)selection["modelHash"],Is.EqualTo(hash));Assert.That((int)selection["clipIndex"],Is.Zero);
            Assert.That((bool)selection["available"],Is.False);Assert.That((string)selection["playbackStatus"],Is.EqualTo("inactive"));
            Assert.That((bool)editor.Find(id),Is.False,"Reading a saved gait must not instantiate it");Assert.That(editor.ObjectRevision(id),Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));
            var loading=editor.AcquireNativeEntities(new[]{id},()=>true,default);
            for(int i=0;i<600&&!loading.IsCompleted;i++){
                var pending=SettingsFact("object.walk.settings",id);Assert.That((string)pending["selection"]["source"],Is.EqualTo("embedded"));Assert.That((int)pending["selection"]["clipIndex"],Is.Zero);yield return null;
            }
            Assert.That(loading.IsCompleted,Is.True);Assert.That(loading.Exception,Is.Null);Assert.That(loading.Result,Is.Null);
            var loaded=SettingsFact("object.walk.settings",id);Assert.That((bool)loaded["selection"]["available"],Is.True);Assert.That((string)loaded["selection"]["playbackStatus"],Is.EqualTo("inactive"));
            Assert.That(editor.Find(id).GetComponent<CreatedRoomObject>().Model.IsPlaying,Is.False);Assert.That(editor.ObjectRevision(id),Is.EqualTo(revision));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));
        }
        [UnityTest]public IEnumerator ObjectWalkEmbeddedMovesBonesAndSkinWithoutEscapingItsNavigationBody(){
            yield return WalkingCharacter();Assert.That(SelectGait("embedded",null,out var error),Is.True,error);
            var hips=gaitModel.Humanoid.GetBoneTransform(HumanBodyBones.Hips);var head=gaitModel.Humanoid.GetBoneTransform(HumanBodyBones.Head);var leg=gaitModel.Humanoid.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            var restHip=gaitItem.transform.InverseTransformPoint(hips.position);var restHead=head.localRotation;var restLeg=leg.localRotation;var restContainer=gaitModel.transform.localPosition;float headSwing=0,legSwing=0,skinChange=0;
            var skin=gaitModel.Instance.SkinnedMeshRenderers.First();var baked=new Mesh();
            Vector3[] Bake(){skin.BakeMesh(baked,true);return baked.vertices.Select(v=>gaitItem.transform.InverseTransformPoint(skin.transform.TransformPoint(v))).ToArray();}
            var restSkin=Bake();
            var travel=new GroundedTravel(editor,gaitItem,new(0,0,-2),.5f);
            try{Assert.That(travel.Begin(out error),Is.True,error);for(int i=0;i<180;i++){
                Assert.That(travel.State(out error),Is.Not.EqualTo(RuleActionState.Failed),error);yield return null;
                var hip=gaitItem.transform.InverseTransformPoint(hips.position);Assert.That(new Vector2(hip.x-restHip.x,hip.z-restHip.z).magnitude,Is.LessThan(.0001f));
                headSwing=Mathf.Max(headSwing,Quaternion.Angle(restHead,head.localRotation));legSwing=Mathf.Max(legSwing,Quaternion.Angle(restLeg,leg.localRotation));
                var vertices=Bake();for(int v=0;v<vertices.Length;v++)skinChange=Mathf.Max(skinChange,Vector3.Distance(vertices[v],restSkin[v]));
            }Assert.That(gaitItem.transform.position.x,Is.GreaterThan(-1.5f));Assert.That(skinChange,Is.GreaterThan(.001f));Assert.That(headSwing,Is.GreaterThan(5));Assert.That(legSwing,Is.GreaterThan(5));Assert.That(ImportedObjectWalk.StatusFor(gaitItem),Is.EqualTo("walking"));
            }finally{travel.Stop(false);UnityEngine.Object.Destroy(baked);}Assert.That(Quaternion.Angle(restHead,head.localRotation),Is.LessThan(.001));Assert.That(gaitModel.transform.localPosition,Is.EqualTo(restContainer));Assert.That(ImportedObjectWalk.StatusFor(gaitItem),Is.EqualTo("inactive"));
        }
        [UnityTest]public IEnumerator ObjectWalkLibraryLoadsExactRigWaitsThenRestoresOnStop(){
            yield return WalkingCharacter();var import=editor.Motions.ImportAsync("Gait.glb",ModelFixture.WalkingCharacter());yield return new WaitUntil(()=>import.IsCompleted);Assert.That(import.Exception,Is.Null);var choice=import.Result.Single();Assert.That(SelectGait("library",choice.id,out var error),Is.True,error);
            var travel=new GroundedTravel(editor,gaitItem,new(0,0,-2),.5f);var start=gaitItem.transform.position;bool played=false;
            try{Assert.That(travel.Begin(out error),Is.True,error);for(int i=0;i<240;i++){Assert.That(travel.State(out error),Is.Not.EqualTo(RuleActionState.Failed),error);played|=ImportedObjectWalk.StatusFor(gaitItem)=="walking";yield return null;}Assert.That(played,Is.True);Assert.That(Vector3.Distance(start,gaitItem.transform.position),Is.GreaterThan(.5));}finally{travel.Stop(false);}
            Assert.That(editor.UsesMotion(choice.id),Is.True);Assert.That(new RoomStorage(directory).RetainsMotion(choice.id,out _),Is.True);
        }
        [UnityTest]public IEnumerator ObjectWalkMissingAssignedMotionRejectsMovementWithoutFallback(){
            yield return WalkingCharacter();var import=editor.Motions.ImportAsync("Gait.glb",ModelFixture.WalkingCharacter());yield return new WaitUntil(()=>import.IsCompleted);var choice=import.Result.Single();Assert.That(SelectGait("library",choice.id,out var error),Is.True,error);
            File.Delete(Path.Combine(directory,"motions",choice.hash+".motion.glb"));var before=gaitItem.transform.position;
            Assert.That(GroundedMovement.CanBegin(editor,gaitItem,out error),Is.False);Assert.That(error,Does.Contain("missing"));Assert.That(gaitItem.transform.position,Is.EqualTo(before));Assert.That(editor.Read(editor.Identity(gaitItem)).walkMotionId,Is.EqualTo(choice.id));
            Assert.That(SelectGait("none",null,out error),Is.True,error);Assert.That(GroundedMovement.CanBegin(editor,gaitItem,out error),Is.True,error);
        }
        [UnityTest]public IEnumerator ObjectWalkTrackingLossRestoresPoseAndFollowUsesTheSameGait(){
            yield return WalkingCharacter();Assert.That(SelectGait("embedded",null,out var error),Is.True,error);var leader=WalkingBlock(new(1,0,-2));var head=gaitModel.Humanoid.GetBoneTransform(HumanBodyBones.Head);var rest=head.localRotation;
            var follow=new GroundedFollowing(editor,gaitItem,editor.Identity(leader),1,.5f,30);
            try{Assert.That(follow.Begin(out error),Is.True,error);for(int i=0;i<60;i++){follow.Tick();yield return null;}Assert.That(Quaternion.Angle(rest,head.localRotation),Is.GreaterThan(1));
                viewer.SetActive(false);follow.Tick();Assert.That(follow.State(out error),Is.EqualTo(RuleActionState.Failed));Assert.That(Quaternion.Angle(rest,head.localRotation),Is.LessThan(.001));
            }finally{follow.Stop(false);}
        }
        [UnityTest]public IEnumerator ObjectWalkBlockedRouteHoldsTheInitialPoseAndRespectsOwnership(){
            yield return WalkingCharacter();Assert.That(SelectGait("embedded",null,out var error),Is.True,error);
            var head=gaitModel.Humanoid.GetBoneTransform(HumanBodyBones.Head);var rest=head.localRotation;var leader=WalkingBlock(new(2,0,-2));
            Surface(new(-1.2f,.8f,0),new(.1f,1.6f,14));Physics.SyncTransforms();
            var follow=new GroundedFollowing(editor,gaitItem,editor.Identity(leader),1,.5f,30);
            try{Assert.That(follow.Begin(out error),Is.True,error);for(int i=0;i<60;i++){follow.Tick();yield return null;}
                Assert.That(SelectGait("none",null,out error),Is.False,"A settings edit cannot steal walking pose ownership");
                for(int i=0;i<120;i++){follow.Tick();yield return null;}
                Assert.That((string)follow.Result["state"],Is.EqualTo("blocked"));Assert.That(ImportedObjectWalk.StatusFor(gaitItem),Is.EqualTo("idle"));
                Assert.That(Quaternion.Angle(rest,head.localRotation),Is.LessThan(.001));var stopped=gaitItem.transform.position;
                for(int i=0;i<30;i++){follow.Tick();yield return null;}Assert.That(gaitItem.transform.position,Is.EqualTo(stopped));
            }finally{follow.Stop(false);}
        }
        [UnityTest]public IEnumerator ObjectWalkDestroyedTargetReleasesItsOwnedPoseAndLateLibraryLease(){
            yield return WalkingCharacter();var import=editor.Motions.ImportAsync("Gait.glb",ModelFixture.WalkingCharacter());yield return new WaitUntil(()=>import.IsCompleted);Assert.That(SelectGait("library",import.Result.Single().id,out var error),Is.True,error);
            int before=ImportedObjectWalk.ActiveOwners;var gait=new ImportedObjectWalk(editor,gaitItem);Assert.That(ImportedObjectWalk.ActiveOwners,Is.EqualTo(before+1));
            UnityEngine.Object.Destroy(gaitItem.gameObject);yield return null;gait.Dispose();Assert.That(ImportedObjectWalk.ActiveOwners,Is.EqualTo(before));yield return null;
        }
        [UnityTest]public IEnumerator ObjectWalkRejectsNonHumanoidAssignmentButAllowsUnanimatedNavigation(){
            yield return WalkingCharacter(ModelFixture.Create());Assert.That(SelectGait("embedded",null,out var error),Is.False);Assert.That(error,Does.Contain("humanoid"));Assert.That(SelectGait("none",null,out error),Is.True,error);Assert.That(GroundedMovement.CanBegin(editor,gaitItem,out error),Is.True,error);
        }
    }
}
