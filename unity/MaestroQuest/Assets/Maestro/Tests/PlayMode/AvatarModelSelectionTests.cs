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
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class AnimationWorkshopTests
    {
        JObject ModelRequest(string hash,int? revision=null)=>new() {["operation"]="start",["runId"]=authorActions.Observe()["nextRunId"].DeepClone(),["call"]=new JObject {["id"]="avatar.model.select",["version"]=1,["arguments"]=new JObject {["target"]="maestro",["modelHash"]=hash,["revision"]=revision??editor.ObjectRevision("maestro")}}};
        IEnumerator ModelFinished(string phase="completed"){
            float until=Time.realtimeSinceStartup+15;
            while(Time.realtimeSinceStartup<until){string state=(string)authorActions.Observe()["selected"]["phase"];if(state is "completed" or "failed" or "cancelled")break;yield return null;}
            Assert.That((string)authorActions.Observe()["selected"]["phase"],Is.EqualTo(phase),authorActions.Observe().ToString());
        }
        IEnumerator SaveModelAsset(ModelAsset asset){var save=editor.Models.SaveAsync(asset);yield return new WaitUntil(()=>save.IsCompleted);Assert.That(save.Exception,Is.Null);}
        JObject ModelFact(){Assert.That(BehaviourCatalog.TryRead("avatar.model",new BehaviourCatalog.FactContext(editor:editor),out var value),Is.True);return JObject.FromObject(value.Value);}
        [UnityTest] public IEnumerator LibraryDiscoveryAndAgentSelectionLoadTheSameHumanoidAndCommitOneUndo()
        {
            AuthorRuntime();var asset=ModelLibrary.Inspect("My blue Maestro.glb",ModelFixture.Mixamo());yield return SaveModelAsset(asset);
            var before=ModelFact();var query=new JObject {["operation"]="start",["runId"]=authorActions.Observe()["nextRunId"].DeepClone(),["call"]=new JObject {["id"]="model.library.inspect",["version"]=1,["arguments"]=new JObject {["offset"]=0}}};
            Assert.That(authorActions.Execute(query,out var error),Is.True,error);yield return ModelFinished();var library=authorActions.Observe();Assert.That((string)library["selected"]["output"]["entries"][0]["modelHash"],Is.EqualTo(asset.Hash));Assert.That((string)library["selected"]["output"]["entries"][0]["name"],Is.EqualTo("My blue Maestro.glb"));
            var request=ModelRequest(asset.Hash);Assert.That(authorActions.Execute(request,out error),Is.True,error);Assert.That(avatar.ModelBusy,Is.True);Assert.That(editor.Read("maestro").modelHash,Is.Null);var loading=ModelFact();yield return ModelFinished();var selected=authorActions.Observe();var after=ModelFact();
            Assert.That(avatar.ModelHash,Is.EqualTo(asset.Hash));Assert.That(avatar.PoseRig.Capture().Length,Is.EqualTo(17));Assert.That(avatar.IsImportedClipPlaying,Is.False);Assert.That(avatar.CustomModel,Is.Not.Null);Assert.That(editor.Snapshot().objects.Count(x=>x.kind==RoomObjectKind.ImportedModel),Is.Zero);
            Assert.That(new RoomStorage(directory).Load(out error).objects.Single(x=>x.id=="maestro").modelHash,Is.EqualTo(asset.Hash),error);
            string path=Environment.GetEnvironmentVariable("MAESTRO_AVATAR_SELECTION");if(!string.IsNullOrEmpty(path)){Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"selection.json"),new JObject {["before"]=before,["library"]=library,["loading"]=loading,["selection"]=selected,["after"]=after}.ToString());}
            int revision=editor.ObjectRevision("maestro");Assert.That(authorActions.Execute(request,out error),Is.True,error);Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(revision));
            editor.Undo();Assert.That(editor.Read("maestro").modelHash,Is.Null);Assert.That(avatar.CustomModel,Is.Null);editor.Redo();yield return new WaitUntil(()=>!avatar.ModelBusy);Assert.That(avatar.ModelHash,Is.EqualTo(asset.Hash));
        }
        [UnityTest] public IEnumerator MissingDamagedAndNonHumanoidSelectionsKeepTheSavedAndDisplayedAvatar()
        {
            AuthorRuntime();var asset=ModelLibrary.Inspect("Good.vrm",ModelFixture.Create(avatar:true));yield return SaveModelAsset(asset);Assert.That(editor.SetMaestroModel(asset.Hash),Is.True);yield return new WaitUntil(()=>!avatar.ModelBusy);var original=avatar.CustomModel;int revision=editor.ObjectRevision("maestro");
            var other=ModelLibrary.Inspect("Other.glb",ModelFixture.Create());yield return SaveModelAsset(other);
            foreach(string hash in new[]{new string('a',64),other.Hash}){
                Assert.That(authorActions.Execute(ModelRequest(hash),out var error),Is.True,error);yield return ModelFinished("failed");Assert.That(avatar.CustomModel,Is.SameAs(original));Assert.That(avatar.ModelHash,Is.EqualTo(asset.Hash));Assert.That(editor.Read("maestro").modelHash,Is.EqualTo(asset.Hash));Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(revision));
            }
            File.WriteAllBytes(Path.Combine(directory,"models",other.Hash+".glb"),asset.Bytes);
            Assert.That(authorActions.Execute(ModelRequest(other.Hash),out var reason),Is.True,reason);yield return ModelFinished("failed");Assert.That(avatar.CustomModel,Is.SameAs(original));Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(revision));Assert.That(new RoomStorage(directory).Load(out _).objects.Single(x=>x.id=="maestro").modelHash,Is.EqualTo(asset.Hash));
        }
        [UnityTest] public IEnumerator ModelSaveFailureKeepsCurrentVisualAndCanRetryAfterStorageRecovers()
        {
            AuthorRuntime();var asset=ModelLibrary.Inspect("Good.vrm",ModelFixture.Create(avatar:true));yield return SaveModelAsset(asset);editor.SaveNow();editor.SendMessage("OnApplicationPause",true);editor.SendMessage("OnApplicationPause",false);yield return null;
            string file=Path.Combine(directory,"room.v12.json");if(File.Exists(file))File.Delete(file);Directory.CreateDirectory(file);int revision=editor.ObjectRevision("maestro");
            Assert.That(authorActions.Execute(ModelRequest(asset.Hash),out var error),Is.True,error);yield return ModelFinished("failed");Assert.That(avatar.ModelHash,Is.Empty);Assert.That(avatar.CustomModel,Is.Null);Assert.That(editor.Read("maestro").modelHash,Is.Null);Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(revision));Directory.Delete(file);
            Assert.That(authorActions.Execute(ModelRequest(asset.Hash),out error),Is.True,error);yield return ModelFinished();Assert.That(avatar.ModelHash,Is.EqualTo(asset.Hash));
            File.Delete(file);Directory.CreateDirectory(file);var imports=root.AddComponent<ImportWorkshop>();imports.Initialize(editor,workshop);imports.DefaultMaestro();yield return null;
            Assert.That(avatar.ModelHash,Is.EqualTo(asset.Hash));Assert.That(imports.Status,Does.Contain("not saved"));Assert.That(imports.Status,Does.Not.Contain("restored"));Directory.Delete(file);
        }
        [UnityTest] public IEnumerator CancelledOrPausedSelectionNeverCommitsAfterItsWorkerDrains()
        {
            AuthorRuntime();var asset=ModelLibrary.Inspect("Cancel.vrm",ModelFixture.Create(avatar:true));yield return SaveModelAsset(asset);int revision=editor.ObjectRevision("maestro");
            var request=ModelRequest(asset.Hash);Assert.That(authorActions.Execute(request,out var error),Is.True,error);Assert.That(avatar.ModelBusy,Is.True);
            Assert.That(authorActions.Execute(new JObject {["operation"]="cancel",["runId"]=request["runId"].DeepClone()},out error),Is.True,error);yield return new WaitUntil(()=>!avatar.ModelBusy);yield return null;
            Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(revision));Assert.That(avatar.CustomModel,Is.Null);Assert.That(editor.Read("maestro").modelHash,Is.Null);
            Assert.That(editor.SetMaestroModel(asset.Hash),Is.True);editor.SendMessage("OnApplicationPause",true);yield return new WaitUntil(()=>!avatar.ModelBusy);editor.SendMessage("OnApplicationPause",false);Assert.That(editor.ObjectRevision("maestro"),Is.EqualTo(revision));Assert.That(avatar.CustomModel,Is.Null);
            Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);yield return FinishAuthoringSave();Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);
        }
        [UnityTest] public IEnumerator StaleRevisionAndExistingActorRefuseSelectionWithoutStoppingOtherTargets()
        {
            AuthorRuntime();var asset=ModelLibrary.Inspect("Good.vrm",ModelFixture.Create(avatar:true));yield return SaveModelAsset(asset);int revision=editor.ObjectRevision("maestro");Assert.That(editor.MoveObject("maestro",Vector3.right,out var error),Is.True,error);Assert.That(authorActions.Execute(ModelRequest(asset.Hash,revision),out _),Is.False);
            Assert.That(editor.Ownership.TryAcquire("existing","Existing gesture",RoomActorRole.Program,new[]{new BehaviourCatalog.Claim("maestro","upperBody")},_=>Assert.Fail("Must not steal"),out var lease,out error),Is.True,error);Assert.That(authorActions.Execute(ModelRequest(asset.Hash),out _),Is.False);Assert.That(lease.Held,Is.True);lease.Dispose();
            Assert.That(editor.Ownership.TryAcquire("book-actor","Book animation",RoomActorRole.Program,new[]{new BehaviourCatalog.Claim("book","wholeTarget")},_=>Assert.Fail("Unrelated actor must survive"),out var book,out error),Is.True,error);Assert.That(authorActions.Execute(ModelRequest(asset.Hash),out error),Is.True,error);yield return ModelFinished();Assert.That(book.Held,Is.True);book.Dispose();
        }
        [UnityTest] public IEnumerator TemporaryModelSelectionKeepsOriginalStorageUntilKeepAndUndoRetainsPose()
        {
            AuthorRuntime();var asset=ModelLibrary.Inspect("Good.vrm",ModelFixture.Create(avatar:true));yield return SaveModelAsset(asset);var pose=avatar.PoseRig.RestPose();pose.Single(x=>x.joint==PoseJoint.Head).rotation*=Quaternion.Euler(20,0,0);Assert.That(editor.SaveAnimation("maestro",null,pose,true),Is.True);Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);
            Assert.That(authorActions.Execute(ModelRequest(asset.Hash),out error),Is.True,error);Assert.That(editor.KeepTemporaryRoom(out _),Is.False);yield return ModelFinished();Assert.That((bool)authorActions.Observe()["selected"]["output"]["temporary"],Is.True);Assert.That(new RoomStorage(directory).Load(out error).objects.Single(x=>x.id=="maestro").modelHash,Is.Null.Or.Empty,error);Assert.That(editor.Read("maestro").joints.Length,Is.EqualTo(17));
            Assert.That(editor.KeepTemporaryRoom(out error),Is.True,error);yield return FinishAuthoringSave();Assert.That(new RoomStorage(directory).Load(out error).objects.Single(x=>x.id=="maestro").modelHash,Is.EqualTo(asset.Hash),error);editor.Undo();yield return new WaitUntil(()=>!avatar.ModelBusy);Assert.That(editor.Read("maestro").joints.Length,Is.EqualTo(17));
        }
        [UnityTest] public IEnumerator PhysicalSwitchCannotDiscardAFailedRecording()
        {
            AuthorRuntime();editor.Select(avatarItem);workshop.ToggleRecord();yield return new WaitForSeconds(.12f);editor.SaveNow();yield return null;
            string file=Path.Combine(directory,"room.v12.json");if(File.Exists(file))File.Delete(file);Directory.CreateDirectory(file);
            Assert.That(editor.SetMaestroModel(null),Is.False);Assert.That(workshop.HasUnsavedRecording,Is.True);Assert.That(avatar.ModelBusy,Is.False);workshop.DiscardTake();Directory.Delete(file);
        }
    }
}
