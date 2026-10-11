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
using UnityEngine.XR.Interaction.Toolkit;
namespace Maestro.Quest.Tests
{
    public sealed partial class BundledAvatarRuntimeTests
    {
        string directory;GameObject root;RoomEditor editor;MaestroAvatar avatar;RoomExecutions actions;AnimationWorkshop workshop;
        [SetUp]public void SetUp(){directory=Path.Combine(Path.GetTempPath(),"MaestroBundledRuntime-"+Guid.NewGuid().ToString("N"));}
        void Open(BundledAvatar included,BundledMotions motions=null)
        {
            root=new GameObject("Included avatar room");root.AddComponent<XRInteractionManager>();var room=root.AddComponent<RoomInteraction>();
            RoomItem Item(string name){var go=new GameObject(name);go.transform.SetParent(root.transform,false);var collider=go.AddComponent<BoxCollider>();collider.size=Vector3.one*.1f;var item=go.AddComponent<RoomItem>();item.Configure(new Collider[]{collider});room.Register(item);return item;}
            var book=Item("book");var tutor=Item("maestro");avatar=tutor.gameObject.AddComponent<MaestroAvatar>();
            editor=root.AddComponent<RoomEditor>();editor.Initialize(room,book,tutor,Path.Combine(directory,"saved"),includedAvatar:included,includedMotions:motions);
            workshop=root.AddComponent<AnimationWorkshop>();workshop.Initialize(editor);var rules=root.AddComponent<RuleWorkshop>();rules.Initialize(editor);root.AddComponent<RoomRules>().Initialize(rules,editor,workshop,null,room,null);actions=new RoomExecutions(editor);
        }
        IEnumerator Loaded(){float until=Time.realtimeSinceStartup+20;while(avatar.ModelBusy&&Time.realtimeSinceStartup<until)yield return null;Assert.That(avatar.ModelBusy,Is.False);Assert.That(avatar.ModelLoad.IsCompleted,Is.True);}
        JObject Fact(string name){Assert.That(BehaviourCatalog.TryRead(name,new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True);return JObject.FromObject(value.Value);}
        JObject DefaultCall()=>new() {["operation"]="start",["runId"]=actions.Observe()["nextRunId"].DeepClone(),["call"]=new JObject {["id"]="avatar.model.select",["version"]=1,["arguments"]=new JObject {["target"]="maestro",["modelHash"]="",["revision"]=editor.ObjectRevision("maestro")}}};
        IEnumerator Completed(string phase="completed") {float until=Time.realtimeSinceStartup+20;while(Time.realtimeSinceStartup<until){string state=(string)actions.Observe()["selected"]["phase"];if(state is "completed" or "failed" or "cancelled")break;yield return null;}Assert.That((string)actions.Observe()["selected"]["phase"],Is.EqualTo(phase),actions.Observe().ToString());}
        [UnityTest]public IEnumerator IncludedAvatarLoadsOfflineAndSavedSelectionSurvivesAnAppArtworkUpdate()
        {
            var included=BundledAvatarFixture.Write(Path.Combine(directory,"package1"),ModelFixture.Mixamo());Open(included);yield return Loaded();
            Assert.That(avatar.ModelHash,Is.EqualTo(included.Hash),avatar.ModelStatus);Assert.That(avatar.CustomModel.IsHumanoid,Is.True);Assert.That(avatar.IsImportedClipPlaying,Is.False);Assert.That(avatar.CustomModel.IsPlaying,Is.False);Assert.That(avatar.PoseRig.Capture(),Has.Length.EqualTo(17));Assert.That((string)Fact("avatar.included")["modelHash"],Is.EqualTo(included.Hash));Assert.That((string)Fact("avatar.model")["phase"],Is.EqualTo("ready"));
            editor.SaveNow();UnityEngine.Object.Destroy(root);yield return null;
            var changed=BundledAvatarFixture.Write(Path.Combine(directory,"package2"),ModelFixture.Mixamo(j=>j["animations"][0]["name"]="A later default"));File.Delete(Path.Combine(directory,"package1",BundledAvatar.RelativePath));Open(changed);yield return Loaded();
            Assert.That(avatar.ModelHash,Is.EqualTo(included.Hash));Assert.That(editor.Read("maestro").modelHash,Is.EqualTo(included.Hash));Assert.That((string)Fact("avatar.included")["modelHash"],Is.EqualTo(changed.Hash));
        }
        [UnityTest]public IEnumerator HumanAndAgentDefaultUseOneExactSelectionAndUndoRestoresThePreviousAvatarAndPose()
        {
            var included=BundledAvatarFixture.Write(Path.Combine(directory,"package"),ModelFixture.Mixamo());Open(included);yield return Loaded();
            var custom=ModelLibrary.Inspect("My avatar",ModelFixture.Create(avatar:true));var saved=editor.Models.SaveAsync(custom);yield return new WaitUntil(()=>saved.IsCompleted);Assert.That(saved.Exception,Is.Null);Assert.That(editor.SetMaestroModel(custom.Hash),Is.True);yield return Loaded();
            editor.Select(editor.Find("maestro"));workshop.TogglePose();var bone=avatar.PoseRig.CanonicalBone(PoseJoint.Head);avatar.PoseRig.Rotate(PoseJoint.Head,bone.parent.rotation*Quaternion.Euler(0,12,0));avatar.PoseRig.FinishedHandle();workshop.TogglePose();var pose=editor.Read("maestro").joints;Assert.That(pose,Is.Not.Null);
            var call=DefaultCall();Assert.That(actions.Execute(call,out var error),Is.True,error);yield return Completed();Assert.That((string)actions.Observe()["selected"]["output"]["modelHash"],Is.EqualTo(included.Hash));Assert.That(avatar.ModelHash,Is.EqualTo(included.Hash));Assert.That(editor.Read("maestro").joints,Has.Length.EqualTo(pose.Length));Assert.That(avatar.IsImportedClipPlaying,Is.False);
            int revision=editor.ObjectRevision("maestro");Assert.That(actions.Execute(call,out error),Is.True,error);Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(revision));
            editor.Undo();yield return Loaded();Assert.That(avatar.ModelHash,Is.EqualTo(custom.Hash));editor.Redo();yield return Loaded();Assert.That(avatar.ModelHash,Is.EqualTo(included.Hash));
            Assert.That(editor.SetMaestroModel(custom.Hash),Is.True);yield return Loaded();var imports=root.AddComponent<ImportWorkshop>();imports.Initialize(editor,workshop);imports.DefaultMaestro();yield return Loaded();Assert.That(avatar.ModelHash,Is.EqualTo(included.Hash));Assert.That(imports.Status,Does.Contain("restored"));
            string evidence=Environment.GetEnvironmentVariable("MAESTRO_INCLUDED_AVATAR_EVIDENCE");if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"selection.json"),new JObject {["included"]=Fact("avatar.included"),["model"]=Fact("avatar.model"),["execution"]=actions.Observe()}.ToString());}
        }
        [UnityTest]public IEnumerator FailedBundledSelectionKeepsTheCurrentModelAndCancelledSelectionNeverCommits()
        {
            var included=BundledAvatarFixture.Write(Path.Combine(directory,"package"),ModelFixture.Mixamo());
            // An existing room's sketch remains selected until the user explicitly requests the new default.
            new RoomStorage(Path.Combine(directory,"saved")).Save(new RoomDocument {version=2,objects=new[]{new RoomObjectData {id="book",kind=RoomObjectKind.Book},new RoomObjectData {id="maestro",kind=RoomObjectKind.Maestro}}},out _);
            Open(included);yield return Loaded();Assert.That(avatar.ModelHash,Is.Empty);int revision=editor.ObjectRevision("maestro");string file=Path.Combine(directory,"package",BundledAvatar.RelativePath);var bytes=File.ReadAllBytes(file);File.WriteAllBytes(file,new byte[bytes.Length]);
            Assert.That(actions.Execute(DefaultCall(),out var error),Is.True,error);yield return Completed("failed");Assert.That(avatar.ModelHash,Is.Empty);Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(revision));Assert.That(editor.Read("maestro").modelHash,Is.Null.Or.Empty);
            File.WriteAllBytes(file,bytes);Assert.That(editor.Models.TryCaptureArchive(out var capture),Is.True);var call=DefaultCall();Assert.That(actions.Execute(call,out error),Is.True,error);
            Assert.That(actions.Execute(new JObject {["operation"]="cancel",["runId"]=call["runId"].DeepClone()},out error),Is.True,error);capture.Dispose();yield return Loaded();Assert.That(avatar.ModelHash,Is.Empty);Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(revision));
        }
        [UnityTest]public IEnumerator ShippedModelUsesTheRealDefaultPoseAndWalkPathWithoutStartingItsClipOnLoad()
        {
            var included=BundledAvatar.FromApplication();Assert.That(included,Is.Not.Null,"Release artwork must be explicitly packaged");Open(included);yield return Loaded();
            Assert.That(avatar.ModelLoad.Result,Is.True,avatar.ModelStatus);Assert.That(avatar.PoseRig.Capture(),Has.Length.EqualTo(17));Assert.That(avatar.CustomModel.IsHumanoid,Is.True);Assert.That(avatar.IsImportedClipPlaying,Is.False);Assert.That(avatar.CustomModel.ClipCount,Is.GreaterThan(0));
            var wrist=avatar.PoseRig.CanonicalBone(PoseJoint.LeftHand);var before=wrist.localRotation;avatar.SetEditing(true);avatar.PoseRig.SetPosing(true);avatar.PoseRig.Rotate(PoseJoint.LeftHand,wrist.parent.rotation*Quaternion.Euler(25,0,0));yield return null;Assert.That(Quaternion.Angle(before,wrist.localRotation),Is.GreaterThan(5));avatar.SetEditing(false);
            avatar.SpatialWalk(.4f);Assert.That(avatar.IsImportedClipPlaying,Is.True);yield return new WaitForSeconds(.1f);avatar.SpatialWalk(0);Assert.That(avatar.IsImportedClipPlaying,Is.False);
            Assert.That(File.ReadAllBytes(Path.Combine(directory,"saved","models",included.Hash+".glb")),Is.EqualTo(included.Read().Bytes));
        }
        [UnityTearDown]public IEnumerator TearDown(){
            var modelLoad=avatar?avatar.ModelLoad:null;var includedCopy=editor?editor.Motions?.IncludedInitialization:null;
            UnityEngine.Object.Destroy(root);yield return null;
            // Destroy requests disposal; an in-flight atomic copy still owns its
            // file until the background task exits. Never delete its workspace early.
            float until=Time.realtimeSinceStartup+20;
            while((modelLoad!=null&&!modelLoad.IsCompleted||includedCopy!=null&&!includedCopy.IsCompleted)&&Time.realtimeSinceStartup<until)yield return null;
            Assert.That(modelLoad?.IsCompleted??true,Is.True,"Avatar load did not finish after disposal");Assert.That(includedCopy?.IsCompleted??true,Is.True,"Included motion copy did not finish after disposal");
            if(Directory.Exists(directory))Directory.Delete(directory,true);
        }
    }
}
