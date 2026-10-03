// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Imports;
using Maestro.Quest.Interaction;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class AnimationWorkshopTests
    {
        sealed class ModelChoice:IModelPicker,IModelArchivePicker
        {
            public string CacheRoot {get;}
            public bool ReadyToStart=>Result==null;
            public JObject Result;public int Starts,Releases;public string SelectedPath;
            public ModelChoice(string root){CacheRoot=Path.Combine(root,"selected");}
            public void Start(string id){Assert.That(ReadyToStart,Is.True);Starts++;Result=new JObject {["id"]=id,["phase"]="selecting",["path"]="",["name"]="",["error"]=""};}
            public JObject Read(string id)=>(JObject)Result?.DeepClone();
            public void Choose(byte[] bytes,string name="My model.glb"){
                string folder=Path.Combine(CacheRoot,Guid.NewGuid().ToString("D"));Directory.CreateDirectory(folder);SelectedPath=Path.Combine(folder,Guid.NewGuid().ToString("D"));File.WriteAllBytes(SelectedPath,bytes);
                Result["phase"]="selected";Result["path"]=SelectedPath;Result["name"]=name;
            }
            public byte[][] ArchiveBytes;public int MemberChoices;public bool HoldMember;
            public void ChooseArchive(params byte[][] files){ArchiveBytes=files;Result["phase"]="archive";Result["members"]=new JArray(files.Select((bytes,index)=>new JObject {["name"]="model-"+index+".glb",["bytes"]=bytes.Length}));}
            public bool SelectMember(string id,int index,int request){if(Result==null||(string)Result["id"]!=id)return false;MemberChoices++;Result["memberRequest"]=request;Result["phase"]="copying";if(!HoldMember)Choose(ArchiveBytes[index],"model-"+index+".glb");return true;}
            public void Release(string id){if(Result==null||(string)Result["id"]!=id)return;Releases++;Result=null;if(SelectedPath!=null&&File.Exists(SelectedPath))File.Delete(SelectedPath);}
        }
        ImportWorkshop imports;ModelChoice modelChoice;
        void ImportRuntime(){AuthorRuntime();imports=root.AddComponent<ImportWorkshop>();imports.Initialize(editor,workshop);modelChoice=new ModelChoice(directory);imports.SetPickerForTests(modelChoice);}
        JObject ImportFact()=>Fact("model.import.selection",null);
        JObject ImportRequest(JObject args)=>new() {["operation"]="start",["runId"]=authorActions.Observe()["nextRunId"].DeepClone(),["call"]=new JObject {["id"]="model.import",["version"]=1,["arguments"]=args}};
        IEnumerator ImportAction(JObject args){Assert.That(authorActions.Execute(ImportRequest(args),out var error),Is.True,error);yield return ModelFinished();}
        JObject ImportAccept(string destination){var f=ImportFact();var value=new JObject {["operation"]=destination,["requestId"]=f["requestId"].DeepClone(),["modelHash"]=f["preview"]["modelHash"].DeepClone()};if(destination=="maestro"){value["target"]="maestro";value["revision"]=editor.ObjectRevision("maestro");}return value;}
        IEnumerator ImportPhase(string phase){float deadline=Time.realtimeSinceStartup+15;while((string)ImportFact()["phase"]!=phase&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That((string)ImportFact()["phase"],Is.EqualTo(phase),ImportFact().ToString());}
        IEnumerator ChooseModel(byte[] bytes){yield return ImportAction(new JObject {["operation"]="select"});modelChoice.Choose(bytes);yield return ImportPhase("preview");}
        [UnityTest] public IEnumerator SharedPickerPreviewAndObjectAcceptUseExactReceiptsAndDurableUndo()
        {
            ImportRuntime();var before=ImportFact();yield return ImportAction(new JObject {["operation"]="select"});var selected=authorActions.Observe().DeepClone();var choosing=ImportFact();
            Assert.That(modelChoice.Starts,Is.EqualTo(1));Assert.That(editor.WriteGate.CanFreeze(out _),Is.False);modelChoice.Choose(ModelFixture.Create());yield return ImportPhase("preview");var preview=ImportFact();
            Assert.That(imports.HasPreview,Is.True);Assert.That(File.Exists(modelChoice.SelectedPath),Is.False);Assert.That(modelChoice.Releases,Is.EqualTo(1));Assert.That(editor.Snapshot().objects.Count(x=>x.kind==RoomObjectKind.ImportedModel),Is.Zero);
            var request=ImportRequest(ImportAccept("object"));Assert.That(authorActions.Execute(request,out var error),Is.True,error);yield return ModelFinished();var accepted=authorActions.Observe().DeepClone();var after=ImportFact();string id=(string)after["accepted"]["objectId"];
            Assert.That((string)after["phase"],Is.EqualTo("completed"));Assert.That(imports.HasPreview,Is.False);Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);
            Assert.That(new RoomStorage(directory).Load(out _).objects.Single(x=>x.id==id).modelHash,Is.EqualTo((string)preview["preview"]["modelHash"]));
            var created=editor.Find(id).GetComponent<CreatedRoomObject>();yield return new WaitUntil(()=>created.Model&&created.Model.Ready);Assert.That(created.Model.IsPlaying,Is.False);
            Assert.That(authorActions.Execute(request,out error),Is.True,error);Assert.That(editor.Snapshot().objects.Count(x=>x.kind==RoomObjectKind.ImportedModel),Is.EqualTo(1));editor.Undo();Assert.That(editor.Read(id),Is.Null);editor.Redo();Assert.That(editor.Read(id),Is.Not.Null);
            string capture=Environment.GetEnvironmentVariable("MAESTRO_MODEL_SELECTION");if(!string.IsNullOrEmpty(capture)){Directory.CreateDirectory(capture);File.WriteAllText(Path.Combine(capture,"selection.json"),new JObject {["before"]=before,["select"]=selected,["choosing"]=choosing,["preview"]=preview,["accept"]=accepted,["after"]=after}.ToString());}
        }
        [UnityTest] public IEnumerator SharedImportChoosesHumanoidWithoutObjectCopyAndKeepsStalePreviewForRetry()
        {
            ImportRuntime();yield return ChooseModel(ModelFixture.Mixamo());var stale=ImportAccept("maestro");Assert.That(editor.MoveObject("maestro",editor.Read("maestro").position+Vector3.right*.01f,out var error),Is.True,error);
            Assert.That(authorActions.Execute(ImportRequest(stale),out _),Is.False);Assert.That(imports.HasPreview,Is.True);yield return ImportAction(ImportAccept("maestro"));
            Assert.That(avatar.ModelHash,Is.EqualTo((string)ImportFact()["preview"]["modelHash"]));Assert.That(avatar.CustomModel.IsHumanoid,Is.True);Assert.That(avatar.IsImportedClipPlaying,Is.False);
            Assert.That(editor.Snapshot().objects.Count(x=>x.kind==RoomObjectKind.ImportedModel),Is.Zero);editor.Undo();Assert.That(editor.Read("maestro").modelHash,Is.Null);
        }
        [UnityTest] public IEnumerator SharedMotionImportReturnsExactIdsWithoutSavingAnotherModelOrAutoplay()
        {
            ImportRuntime();yield return ChooseModel(ModelFixture.Mixamo());yield return ImportAction(ImportAccept("motions"));var value=ImportFact();var motionPage=Fact("model.import.motions",new JObject {["requestId"]=imports.SelectionRequestId,["motionOffset"]=0});var ids=((JArray)motionPage["motionIds"]).Select(x=>(string)x).ToArray();Assert.That((int)value["accepted"]["motionCount"],Is.EqualTo(ids.Length));
            Assert.That(ids,Is.Not.Empty);Assert.That(ids.All(id=>editor.Motions.Inspect(id)!=null),Is.True);Assert.That(editor.Snapshot().objects.Count(x=>x.kind==RoomObjectKind.ImportedModel),Is.Zero);Assert.That(avatar.IsImportedClipPlaying,Is.False);
            Assert.That(Directory.Exists(Path.Combine(directory,"models")),Is.False);Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);
        }
        [UnityTest] public IEnumerator StaleCancelAndDuplicateSelectCannotAffectANewerPickerAndPausePreservesItsChoice()
        {
            ImportRuntime();var start=ImportRequest(new JObject {["operation"]="select"});Assert.That(authorActions.Execute(start,out var error),Is.True,error);yield return ModelFinished();string old=imports.SelectionRequestId;
            Assert.That(authorActions.Execute(start,out error),Is.True,error);Assert.That(modelChoice.Starts,Is.EqualTo(1));yield return ImportAction(new JObject {["operation"]="cancel",["requestId"]=old});
            yield return ImportAction(new JObject {["operation"]="select"});Assert.That(imports.SelectionRequestId,Is.Not.EqualTo(old));Assert.That(authorActions.Execute(ImportRequest(new JObject {["operation"]="cancel",["requestId"]=old}),out _),Is.False);
            imports.SendMessage("OnApplicationPause",true);modelChoice.Choose(ModelFixture.Create());yield return new WaitForSecondsRealtime(.15f);Assert.That(imports.HasPreview,Is.False);Assert.That(File.Exists(modelChoice.SelectedPath),Is.True);
            imports.SendMessage("OnApplicationPause",false);yield return ImportPhase("preview");Assert.That(imports.HasPreview,Is.True);imports.Cancel();Assert.That((string)ImportFact()["phase"],Is.EqualTo("cancelled"));Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);
        }
        [UnityTest] public IEnumerator ForgedPickerIdentityAndExternalPathsNeverLoadOrRevealPrivateBytes()
        {
            ImportRuntime();yield return ImportAction(new JObject {["operation"]="select"});modelChoice.Result["id"]=new string('f',32);yield return ImportPhase("failed");Assert.That(imports.HasPreview,Is.False);Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);
            modelChoice.Result=null;yield return ImportAction(new JObject {["operation"]="select"});string outside=Path.Combine(directory,"private.glb");Directory.CreateDirectory(directory);File.WriteAllBytes(outside,ModelFixture.Create());modelChoice.Result["phase"]="selected";modelChoice.Result["path"]=outside;
            yield return ImportPhase("failed");Assert.That(imports.HasPreview,Is.False);Assert.That(File.Exists(outside),Is.True);Assert.That(ImportFact().ToString(),Does.Not.Contain(outside));
        }
        [UnityTest] public IEnumerator FailedObjectSaveRetainsPreviewAndAllowsAnExplicitRetry()
        {
            ImportRuntime();yield return ChooseModel(ModelFixture.Create());Assert.That(editor.TryFlush(out var flush),Is.True,flush);string file=Path.Combine(directory,"room.v11.json");if(File.Exists(file))File.Delete(file);Directory.CreateDirectory(file);
            Assert.That(authorActions.Execute(ImportRequest(ImportAccept("object")),out var error),Is.True,error);yield return ModelFinished("failed");Assert.That(imports.HasPreview,Is.True);Assert.That(editor.Snapshot().objects.Count(x=>x.kind==RoomObjectKind.ImportedModel),Is.Zero);Assert.That(editor.WriteGate.CanFreeze(out _),Is.False);
            Directory.Delete(file);var accept=imports.AcceptAsync();yield return new WaitUntil(()=>accept.IsCompleted);Assert.That(accept.Result,Is.True,imports.Status);Assert.That((string)ImportFact()["phase"],Is.EqualTo("completed"));Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);
        }
        [UnityTest] public IEnumerator SharedImportRespectsAuthoringAndOtherOwnersAndLibrarySaveDoesNotPlaceAnything()
        {
            ImportRuntime();workshop.TogglePose();Assert.That(authorActions.Execute(ImportRequest(new JObject {["operation"]="select"}),out _),Is.False);Assert.That(workshop.IsPosing,Is.True);workshop.Stop();
            yield return ChooseModel(ModelFixture.Mixamo());Assert.That(editor.Ownership.TryAcquire("other","Other actor",RoomActorRole.Program,new[]{new Maestro.Quest.Programs.BehaviourCatalog.Claim("maestro","wholeTarget")},_=>Assert.Fail("Import cannot steal another actor"),out var lease,out var error),Is.True,error);
            Assert.That(authorActions.Execute(ImportRequest(ImportAccept("maestro")),out _),Is.False);Assert.That(lease.Held,Is.True);yield return ImportAction(ImportAccept("library"));Assert.That(lease.Held,Is.True);lease.Dispose();
            Assert.That(File.Exists(Path.Combine(directory,"models",(string)ImportFact()["preview"]["modelHash"]+".glb")),Is.True);Assert.That(editor.Read("maestro").modelHash,Is.Null);Assert.That(editor.Snapshot().objects.Count(x=>x.kind==RoomObjectKind.ImportedModel),Is.Zero);
        }
        [UnityTest] public IEnumerator SharedAcceptanceCannotStopNewPhysicalPosingAndTemporaryPlacementWaitsForKeep()
        {
            ImportRuntime();yield return ChooseModel(ModelFixture.Create());Assert.That(authorActions.Execute(ImportRequest(ImportAccept("object")),out var error),Is.True,error);workshop.TogglePose();Assert.That(workshop.IsPosing,Is.True);
            yield return ModelFinished("failed");Assert.That(workshop.IsPosing,Is.True);Assert.That(imports.HasPreview,Is.True);Assert.That(editor.Snapshot().objects.Count(x=>x.kind==RoomObjectKind.ImportedModel),Is.Zero);workshop.Stop();imports.Cancel();
            Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);yield return FinishAuthoringSave();yield return ChooseModel(ModelFixture.Create());yield return ImportAction(ImportAccept("object"));string id=(string)ImportFact()["accepted"]["objectId"];
            Assert.That((bool)ImportFact()["accepted"]["temporary"],Is.True);Assert.That(new RoomStorage(directory).Load(out _).objects.Any(x=>x.id==id),Is.False);
            Assert.That(editor.KeepTemporaryRoom(out error),Is.True,error);yield return FinishAuthoringSave();Assert.That(new RoomStorage(directory).Load(out _).objects.Any(x=>x.id==id),Is.True);
        }
        [UnityTest] public IEnumerator CancellingInFlightModelPreparationDrainsBeforeReleasingTheWorkspace()
        {
            ImportRuntime();var prepare=imports.PrepareAsync("cancelled.glb",ModelFixture.Mixamo());string id=imports.SelectionRequestId;Assert.That(imports.CanCancelSelection(id,out var error),Is.True,error);imports.CancelSelection(id);
            yield return new WaitUntil(()=>prepare.IsCompleted);Assert.That(prepare.Exception,Is.Null);Assert.That(imports.HasPreview,Is.False);Assert.That((string)ImportFact()["phase"],Is.EqualTo("cancelled"));Assert.That(editor.WriteGate.CanFreeze(out _),Is.True);
            Assert.That(editor.Snapshot().objects.Count(x=>x.kind==RoomObjectKind.ImportedModel),Is.Zero);
        }
    }
}
