// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class AnimationWorkshopTests
    {
        RoomExecutions authorActions;RoomRules authorRuntime;JObject authorState,authorRequest;
        void AuthorRuntime(){var rules=root.AddComponent<RuleWorkshop>();rules.Initialize(editor,directory);Assert.That(rules.Memory.Initialization.Wait(TimeSpan.FromSeconds(5)),Is.True,"Memory fixture did not load");Assert.That(rules.Memory.Error,Is.Null);authorRuntime=root.AddComponent<RoomRules>();authorRuntime.Initialize(rules,editor,workshop,null,root.GetComponent<RoomInteraction>(),null);authorActions=new RoomExecutions(editor);}
        static JObject QuaternionJson(Quaternion q)=>new() {["x"]=q.x,["y"]=q.y,["z"]=q.z,["w"]=q.w};
        JObject FrameJson(string target,float time,JointPose[] joints=null)=>new() {["time"]=time,["position"]=JObject.FromObject(new {x=editor.Read(target).position.x,y=editor.Read(target).position.y,z=editor.Read(target).position.z}),["rotation"]=QuaternionJson(editor.Read(target).rotation),["scale"]=editor.Read(target).scale,["joints"]=joints==null?JValue.CreateNull():new JArray(joints.Select(j=>new JObject {["joint"]=j.joint.ToString(),["rotation"]=QuaternionJson(j.rotation)}))};
        JObject AuthorArgs(string operation,string target="maestro")=>new() {["operation"]=operation,["target"]=target,["revision"]=editor.ObjectRevision(target)};
        JObject AuthorCall(JObject args)=>new() {["operation"]="start",["runId"]=authorActions.Observe()["nextRunId"].DeepClone(),["call"]=new JObject {["id"]="animation.author",["version"]=1,["arguments"]=args}};
        IEnumerator Author(JObject args)
        {
            authorRequest=AuthorCall(args);Assert.That(authorActions.Execute(authorRequest,out var error),Is.True,error);yield return null;
            authorState=authorActions.Observe();Assert.That((string)authorState["selected"]["phase"],Is.EqualTo("completed"),authorState.ToString());
        }
        JObject Fact(string id,JObject args){Assert.That(BehaviourCatalog.TryRead(id,1,args,new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True,id);return JObject.FromObject(value.Value);}
        [UnityTest] public IEnumerator AgentAuthoredNodUsesPhysicalPlaybackAndDurableExactReceiptWithoutAutoplay()
        {
            AuthorRuntime();var rest=avatar.PoseRig.RestPose();var nod=MotionFrame.CopyJoints(rest);nod.Single(j=>j.joint==PoseJoint.Head).rotation*=Quaternion.Euler(30,0,0);
            var args=AuthorArgs("frames");args["replace"]=true;args["frames"]=new JArray(FrameJson("maestro",0,rest),FrameJson("maestro",.25f,nod),FrameJson("maestro",.5f,rest));
            yield return Author(args);var written=authorState.DeepClone();var request=authorRequest.DeepClone();Assert.That(workshop.IsPlaying,Is.False);Assert.That(workshop.IsPosing,Is.False);var motion=editor.Read("maestro").motion;Assert.That(motion.frames.Length,Is.EqualTo(3));
            var persisted=new RoomStorage(directory).Load(out var issue);Assert.That(issue,Is.Null);Assert.That(persisted.objects.Single(x=>x.id=="maestro").motion.frames.Length,Is.EqualTo(3));
            var query=new JObject {["target"]="maestro",["revision"]=editor.ObjectRevision("maestro"),["index"]=1};var frame=Fact("animation.frame",query);Assert.That(((JArray)frame["joints"]).Count,Is.EqualTo(17));Assert.That((bool)frame["jointChannels"],Is.True);
            var jointQuery=(JObject)query.DeepClone();jointQuery["joint"]="Head";var joint=Fact("animation.joint",jointQuery);Assert.That(Quaternion.Angle(AnimationAuthoringCapability.Rotation(joint),nod.Single(j=>j.joint==PoseJoint.Head).rotation),Is.LessThan(.01f));
            editor.Select(avatarItem);workshop.Play();Assert.That(workshop.IsPlaying,Is.True);yield return new WaitForSeconds(.24f);Assert.That(Quaternion.Angle(avatar.PoseRig.CanonicalBone(PoseJoint.Head).localRotation,rest.Single(j=>j.joint==PoseJoint.Head).rotation),Is.GreaterThan(15));workshop.Stop();
            int before=editor.ObjectRevision("maestro");Assert.That(authorActions.Execute((JObject)request,out var error),Is.True,error);Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(before));
            var meta=Fact("animation.authored",new JObject {["target"]="maestro"});string proof=Environment.GetEnvironmentVariable("MAESTRO_ANIMATION_AUTHORING");if(!string.IsNullOrEmpty(proof)){Directory.CreateDirectory(proof);File.WriteAllText(Path.Combine(proof,"authoring.json"),new JObject {["execution"]=written,["metadata"]=meta,["frame"]=frame,["frameQuery"]=query,["joint"]=joint,["jointQuery"]=jointQuery}.ToString());}
            editor.Undo();Assert.That(editor.Read("maestro").motion,Is.Null);editor.Redo();Assert.That(editor.Read("maestro").motion.frames.Length,Is.EqualTo(3));
        }
        [UnityTest] public IEnumerator AuthoredEditsRejectStaleAndInvalidBatchesAndActivePhysicalAuthoring()
        {
            AuthorRuntime();var args=AuthorArgs("frames");args["replace"]=true;args["frames"]=new JArray(FrameJson("maestro",0),FrameJson("maestro",1));yield return Author(args);
            var stale=AuthorArgs("clear");var settings=AuthorArgs("settings");settings["duration"]=2;settings["loop"]=true;yield return Author(settings);int revision=editor.ObjectRevision("maestro");Assert.That(authorActions.Execute(AuthorCall(stale),out _),Is.False);
            var bad=AuthorArgs("frames");bad["replace"]=false;var invalid=FrameJson("maestro",3);invalid["scale"]=4;bad["frames"]=new JArray(FrameJson("maestro",1),invalid);Assert.That(authorActions.Execute(AuthorCall(bad),out _),Is.False);Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(revision));Assert.That(editor.Read("maestro").motion.frames.Length,Is.EqualTo(2));
            workshop.TogglePose();Assert.That(workshop.IsPosing,Is.True);Assert.That(authorActions.Execute(AuthorCall(AuthorArgs("clear")),out _),Is.False);Assert.That(workshop.IsPosing,Is.True);workshop.Stop();
            var oldQuery=new JObject {["target"]="maestro",["revision"]=(int)stale["revision"],["index"]=0};Assert.That(BehaviourCatalog.TryRead("animation.frame",1,oldQuery,new BehaviourCatalog.FactContext(editor:editor),out _),Is.False);
        }
        [UnityTest] public IEnumerator PhysicalAndAgentFrameEditsShareTimingRemovalAndSavedUndo()
        {
            AuthorRuntime();string id=editor.Snapshot().objects.First(x=>x.kind==RoomObjectKind.Block).id;editor.Select(editor.Find(id));workshop.AddFrame();editor.Find(id).transform.localPosition+=Vector3.right*.2f;workshop.AddFrame();workshop.Stop();
            var settings=AuthorArgs("settings",id);settings["duration"]=2;settings["loop"]=true;yield return Author(settings);Assert.That(editor.Read(id).motion.Duration,Is.EqualTo(2));Assert.That(editor.Read(id).motion.loop,Is.True);
            var remove=AuthorArgs("removeFrames",id);remove["times"]=new JArray(0);yield return Author(remove);Assert.That(editor.Read(id).motion.frames.Length,Is.EqualTo(1));Assert.That(editor.Read(id).motion.frames[0].time,Is.Zero);editor.Undo();Assert.That(editor.Read(id).motion.frames.Length,Is.EqualTo(2));
            workshop.StepFrame(1);workshop.ReplaceFrame();workshop.ToggleLoop();workshop.ChangeSpeed(.8f);workshop.Stop();Assert.That(editor.Read(id).motion.Duration,Is.EqualTo(1.6f).Within(.001f));Assert.That(editor.Read(id).motion.loop,Is.False);
            yield return Author(AuthorArgs("clear",id));Assert.That(editor.Read(id).motion,Is.Null);editor.Undo();Assert.That(editor.Read(id).motion.frames.Length,Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator SavedPoseAndAutomaticResetUseCanonicalJointsAndPreserveMotion()
        {
            AuthorRuntime();var frames=AuthorArgs("frames");frames["replace"]=true;frames["frames"]=new JArray(FrameJson("maestro",0),FrameJson("maestro",1));yield return Author(frames);
            var pose=avatar.PoseRig.RestPose();pose.Single(j=>j.joint==PoseJoint.LeftUpperArm).rotation*=Quaternion.Euler(0,0,30);var args=AuthorArgs("pose");args["joints"]=FrameJson("maestro",0,pose)["joints"].DeepClone();yield return Author(args);Assert.That(editor.Read("maestro").joints.Length,Is.EqualTo(17));Assert.That(avatar.PoseRig.CanonicalBone(PoseJoint.LeftUpperArm).localRotation,Is.EqualTo(pose.Single(j=>j.joint==PoseJoint.LeftUpperArm).rotation));
            var reset=AuthorArgs("pose");reset["joints"]=null;yield return Author(reset);Assert.That(editor.Read("maestro").joints,Is.Null);editor.Undo();Assert.That(editor.Read("maestro").joints.Length,Is.EqualTo(17));Assert.That(editor.Read("maestro").motion.frames.Length,Is.EqualTo(2));
        }
        IEnumerator FinishAuthoringSave()
        {
            float deadline=Time.realtimeSinceStartup+10;
            while(editor.TemporarySavePending&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(editor.TemporarySavePending,Is.False);Assert.That(editor.TemporarySaveError,Is.Null);
        }
        [UnityTest] public IEnumerator AuthoredTemporaryMotionStaysInForkUntilKeptAndDiscardInvalidatesReads()
        {
            AuthorRuntime();Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);yield return FinishAuthoringSave();
            var args=AuthorArgs("frames");args["replace"]=true;args["frames"]=new JArray(FrameJson("maestro",0),FrameJson("maestro",1));yield return Author(args);
            int revision=editor.ObjectRevision("maestro");Assert.That(new RoomStorage(directory).Load(out error).objects.Single(x=>x.id=="maestro").motion,Is.Null,error);
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.Read("maestro").motion,Is.Null);
            Assert.That(BehaviourCatalog.TryRead("animation.frame",1,new JObject {["target"]="maestro",["revision"]=revision,["index"]=0},new BehaviourCatalog.FactContext(editor:editor),out _),Is.False);
            Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);yield return FinishAuthoringSave();args["revision"]=editor.ObjectRevision("maestro");yield return Author(args);
            Assert.That(editor.KeepTemporaryRoom(out error),Is.True,error);yield return FinishAuthoringSave();
            Assert.That(new RoomStorage(directory).Load(out error).objects.Single(x=>x.id=="maestro").motion.frames.Length,Is.EqualTo(2),error);
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.Read("maestro").motion.frames.Length,Is.EqualTo(2));editor.Undo();Assert.That(editor.Read("maestro").motion,Is.Null);
        }
        [UnityTest] public IEnumerator CanonicalAuthoredMotionAnimatesTheImportedDisplayedRig()
        {
            AuthorRuntime();var asset=Maestro.Quest.Imports.ModelLibrary.Inspect("authoring-avatar.vrm",ModelFixture.Create(avatar:true));
            var save=editor.Models.SaveAsync(asset);yield return new WaitUntil(()=>save.IsCompleted);Assert.That(save.Exception,Is.Null);
            Assert.That(editor.SetMaestroModel(asset.Hash),Is.True);yield return new WaitUntil(()=>!avatar.ModelBusy);Assert.That(avatar.ModelHash,Is.EqualTo(asset.Hash),avatar.ModelStatus);
            var rest=avatar.PoseRig.RestPose();var nod=MotionFrame.CopyJoints(rest);nod.Single(j=>j.joint==PoseJoint.Head).rotation*=Quaternion.Euler(30,0,0);
            var args=AuthorArgs("frames");args["replace"]=true;args["frames"]=new JArray(FrameJson("maestro",0,rest),FrameJson("maestro",.25f,nod),FrameJson("maestro",.5f,rest));yield return Author(args);
            var bone=avatar.PoseRig.Bone(PoseJoint.Head);Assert.That(bone,Is.Not.SameAs(avatar.PoseRig.CanonicalBone(PoseJoint.Head)));var before=bone.localRotation;
            editor.Select(avatarItem);workshop.Play();Assert.That(workshop.IsPlaying,Is.True);yield return new WaitForSeconds(.24f);Assert.That(Quaternion.Angle(before,bone.localRotation),Is.GreaterThan(15));workshop.Stop();
        }
        [UnityTest] public IEnumerator FailedDurableAnimationSaveLeavesPoseRevisionAndUndoUnchanged()
        {
            AuthorRuntime();int revision=editor.ObjectRevision("maestro");bool undo=editor.CanUndo;editor.SaveNow();yield return null;string file=Path.Combine(directory,"room.v5.json");if(File.Exists(file))File.Delete(file);Directory.CreateDirectory(file);
            var args=AuthorArgs("frames");args["replace"]=true;args["frames"]=new JArray(FrameJson("maestro",0),FrameJson("maestro",1));Assert.That(authorActions.Execute(AuthorCall(args),out _),Is.False);Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(revision));Assert.That(editor.Read("maestro").motion,Is.Null);Assert.That(editor.CanUndo,Is.EqualTo(undo));
            Assert.That(editor.SaveAnimation("maestro",new RoomMotion {frames=new[]{new MotionFrame()}},null,false),Is.False);Assert.That(editor.Read("maestro").motion,Is.Null);
        }
    }
}
