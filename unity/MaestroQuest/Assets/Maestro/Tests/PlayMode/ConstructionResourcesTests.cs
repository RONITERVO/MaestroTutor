// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Art;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        static CreationBatch PortableBatch()=>CreationBatch.Read((JObject)JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/construction-resources-contract.json")))[0]["arguments"]);
        [UnityTest] public IEnumerator CapturedResourcesSurviveOriginalDeletionAndCreateAtomicallyWithReceiptUndo() {
            yield return WaitForModuleLibrary();
            string style=NewAppearance(),sound=WorldSound(),profile=NewEnvironment(),first=AppearanceBlock();
            Assert.That(editor.CreatePrimitive(RoomObjectKind.Ball,"Partner",new Vector3(.5f,1,.7f),1,Color.green,out var second,out var error),Is.True,error);
            foreach(var target in new[]{first,second}){
                Assert.That(AssignAppearance(target,style,out error,tint:"#BBEEFF"),Is.True,error);WorldEmitter(target,sound);
                Assert.That(AssignEnvironment(target,profile,out error),Is.True,error);
            }
            var members=new[]{first,second}.Select((id,i)=>new ConstructionMember{target=id,revision=editor.ObjectRevision(id),slot="piece_"+i}).ToArray();
            Assert.That(editor.CaptureConstruction(members,out var captured,out error),Is.True,error);
            var source=ConstructionModule.Definition(captured,"Portable pair");ProgramModuleLibrary.Validate(source);
            Assert.That(captured.blueprint.version,Is.EqualTo(4));Assert.That(editor.Read(first).environmentProfile,Is.EqualTo(profile),"Capture cannot mutate originals");
            foreach(var target in new[]{first,second})Assert.That(editor.DeleteObject(target,out error),Is.True,error);
            Assert.That(editor.EditAppearance(null,style,editor.AppearanceRevision(style),Array.Empty<string>(),out error),Is.True,error);
            Assert.That(editor.EditAudio(sound,editor.AudioRevision(sound),null,out error),Is.True,error);
            Assert.That(editor.EditEnvironment(null,profile,editor.EnvironmentRevision(profile),Array.Empty<string>(),out error),Is.True,error);
            // The destination already owns these module-local symbols with different values.
            string local=captured.blueprint.resources.appearances.Single().id;
            Assert.That(editor.EditAppearance(new RoomAppearance{id=local,name="Unrelated",style=new AppearanceStyle{tint="#FF0000"}},local,0,Array.Empty<string>(),out error),Is.True,error);
            Assert.That(editor.EditAudio(local,0,new RoomAudioDefinition{id=local,name="Unrelated",frequency=880},out error),Is.True,error);
            Assert.That(editor.EditEnvironment(new RoomEnvironmentProfile{id=local,name="Unrelated",realCollisions=true},local,0,Array.Empty<string>(),out error),Is.True,error);
            var args=(JObject)source["program"]["functions"][1]["body"][0]["arguments"].DeepClone();var executor=new RoomAgentExecutor(editor);
            var request=TemplateRequest(new JObject{["id"]="object.batch.create",["version"]=1,["arguments"]=args});
            Assert.That(executor.Execute(request,out error,out _),Is.True,error);
            var receipt=executor.Executions.Observe()["selected"];Assert.That((string)receipt["phase"],Is.EqualTo("completed"),receipt.ToString());
            var copies=((JArray)receipt["output"]["objectIds"]).Values<string>().ToArray();var a=editor.Read(copies[0]);var b=editor.Read(copies[1]);
            string copiedStyle=a.appearanceBindings.Single().appearanceId,copiedSound=a.audioEmitters.Single().source,copiedProfile=a.environmentProfile;
            Assert.That(copiedStyle,Is.EqualTo(b.appearanceBindings.Single().appearanceId).And.Not.EqualTo(local));Assert.That(copiedSound,Is.EqualTo(b.audioEmitters.Single().source).And.Not.EqualTo(local));Assert.That(copiedProfile,Is.EqualTo(b.environmentProfile).And.Not.EqualTo(local));
            Assert.That(a.appearanceBindings.Single().tint,Is.EqualTo("#BBEEFF"));Assert.That(editor.ReadAppearance(copiedStyle).style.opacity,Is.EqualTo(.4f));
            Assert.That(physics.IncludesRealRoom(editor.Find(copies[0])),Is.False);Assert.That(editor.ReadEnvironment(local).realCollisions,Is.True);
            Assert.That(editor.ReadAppearance(local).style.tint,Is.EqualTo("#FF0000"));Assert.That(editor.ReadAudio(local).frequency,Is.EqualTo(880));
            Assert.That(editor.Find(copies[0]).GetComponentsInChildren<Renderer>().First(r=>!r.GetComponent<PencilMarks>()).sharedMaterial.GetFloat("_SurfaceOpacity"),Is.EqualTo(.4f));
            Assert.That(copies.SelectMany(id=>editor.Find(id).GetComponentsInChildren<AudioSource>()).All(s=>!s.isPlaying),Is.True);
            var saved=new RoomStorage(directory).Load(out error);Assert.That(saved.Validate(out error),Is.True,error);Assert.That(saved.objects.Single(o=>o.id==copies[0]).environmentProfile,Is.EqualTo(copiedProfile));
            Assert.That(saved.appearances.Length,Is.EqualTo(2));Assert.That(saved.audioSources.Length,Is.EqualTo(2));Assert.That(saved.environmentProfiles.Length,Is.EqualTo(2));
            editor.Undo();Assert.That(copies.All(id=>!editor.Find(id)),Is.True);Assert.That(editor.ReadAppearance(copiedStyle),Is.Null);Assert.That(editor.ReadAudio(copiedSound),Is.Null);Assert.That(editor.ReadEnvironment(copiedProfile),Is.Null);
            Assert.That(executor.Execute(request,out error,out _),Is.True,error);Assert.That(editor.ReadAppearance(copiedStyle),Is.Null,"Receipt replay cannot recreate resources after Undo");
            editor.Redo();Assert.That(copies.All(id=>editor.Find(id)),Is.True);Assert.That(editor.ReadAppearance(copiedStyle),Is.Not.Null);
            Assert.That(editor.CreateBatch(CreationBatch.Read(args),out var another,out error),Is.True,error);
            Assert.That(editor.Read(another[0]).appearanceBindings.Single().appearanceId,Is.Not.EqualTo(copiedStyle));Assert.That(editor.Read(another[0]).audioEmitters.Single().source,Is.Not.EqualTo(copiedSound));Assert.That(editor.Read(another[0]).environmentProfile,Is.Not.EqualTo(copiedProfile));
        }
        [UnityTest] public IEnumerator PortableResourcesRespectWholeRoomCapacityWithoutPartialSave() {
            while(editor.EnvironmentProfiles().Length<RoomEnvironmentProfile.MaximumProfiles)NewEnvironment();
            var before=JsonUtility.ToJson(editor.Snapshot());Assert.That(editor.CreateBatch(PortableBatch(),out var ids,out var error),Is.False);Assert.That(ids,Is.Null);Assert.That(error,Does.Contain("16"));
            Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));var saved=new RoomStorage(directory).Load(out error);Assert.That(saved.appearances,Is.Empty);Assert.That(saved.audioSources,Is.Empty);yield return null;
        }
        [UnityTest] public IEnumerator FailedConstructionSaveLeavesNoObjectsOrDefinitions() {
            string before=JsonUtility.ToJson(editor.Snapshot()),obstacle=Path.Combine(directory,RoomStorage.FileName+".pending");Directory.CreateDirectory(obstacle);
            try {Assert.That(editor.CreateBatch(PortableBatch(),out _,out _),Is.False);Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));}
            finally {Directory.Delete(obstacle);}
            Assert.That(editor.CreateBatch(PortableBatch(),out var ids,out var error),Is.True,error);Assert.That(ids.Length,Is.EqualTo(2));
            Assert.That(editor.AudioSources().Length,Is.EqualTo(1));Assert.That(editor.EnvironmentProfiles().Length,Is.EqualTo(1));Assert.That(editor.Snapshot().appearances.Length,Is.EqualTo(1));yield return null;
        }
        [UnityTest] public IEnumerator MissingConstructionModelCannotLeakItsBundledDefinitions() {
            var args=(JObject)JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/construction-resources-contract.json")))[0]["arguments"];
            args["blueprint"]["pieces"][0]["source"]["prototype"]["geometry"]=new JObject{["kind"]="model",["modelHash"]=new string('f',64)};
            string before=JsonUtility.ToJson(editor.Snapshot());var capability=new BatchCreationCapability();
            Assert.That(capability.Start(new CapabilityContext(editor,animations),"missing-model",args,out var operation,out var error),Is.True,error);
            var state=Maestro.Quest.Rules.RuleActionState.Preparing;
            for(int i=0;i<180&&(state=operation.State(out error))==Maestro.Quest.Rules.RuleActionState.Preparing;i++)yield return null;
            Assert.That(state,Is.EqualTo(Maestro.Quest.Rules.RuleActionState.Failed));Assert.That(error,Does.Contain("missing or damaged"));Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator TemporaryConstructionResourcesDiscardTogetherAndKeepTogether() {
            Assert.That(editor.BeginTemporaryRoom(out var error),Is.True,error);while(editor.TemporarySavePending)yield return null;
            Assert.That(editor.CreateBatch(PortableBatch(),out var ids,out error),Is.True,error);string style=editor.Read(ids[0]).appearanceBindings[0].appearanceId;
            Assert.That(new RoomStorage(directory).Load(out error).appearances,Is.Empty,error);
            Assert.That(editor.DiscardTemporaryRoom(out error),Is.True,error);Assert.That(editor.ReadAppearance(style),Is.Null);Assert.That(editor.AudioSources(),Is.Empty);Assert.That(editor.EnvironmentProfiles(),Is.Empty);
            Assert.That(editor.BeginTemporaryRoom(out error),Is.True,error);while(editor.TemporarySavePending)yield return null;
            Assert.That(editor.CreateBatch(PortableBatch(),out ids,out error),Is.True,error);
            Assert.That(editor.KeepTemporaryRoom(out error),Is.True,error);while(editor.TemporarySavePending)yield return null;
            var saved=new RoomStorage(directory).Load(out error);Assert.That(saved.Validate(out error),Is.True,error);Assert.That(saved.appearances.Length,Is.EqualTo(1));Assert.That(saved.audioSources.Length,Is.EqualTo(1));Assert.That(saved.environmentProfiles.Length,Is.EqualTo(1));
            Assert.That(saved.objects.Single(o=>o.id==ids[0]).appearanceBindings.Single().appearanceId,Is.EqualTo(saved.appearances[0].id));
        }
    }
}
