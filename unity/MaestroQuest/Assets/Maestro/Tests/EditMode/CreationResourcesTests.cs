// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class CreationResourcesTests
    {
        static JArray Cases()=>JArray.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/construction-resources-contract.json")));
        [Test] public void PortableResourceBoundariesMatchWebValidation() {
            foreach(var c in Cases())Assert.That(BehaviourCatalog.TryCall("object.batch.create",1,(JObject)c["arguments"],out _,out var error),Is.EqualTo((bool)c["valid"]),(string)c["name"]+": "+error);
        }
        [Test] public void EveryInstanceOwnsFreshDefinitionsWhileKeepingInternalSharingAndSourceUnchanged() {
            var source=(JObject)Cases()[0]["arguments"];var batch=CreationBatch.Read(source);string before=JsonUtility.ToJson(batch);
            Assert.That(batch.Prepare(out var a,out var ar,out var error),Is.True,error);Assert.That(batch.Prepare(out var b,out var br,out error),Is.True,error);
            Assert.That(ar.appearances.Length,Is.EqualTo(1));Assert.That(ar.audioSources.Length,Is.EqualTo(1));Assert.That(ar.environmentProfiles.Length,Is.EqualTo(1));
            Assert.That(a.Select(x=>x.appearanceBindings[0].appearanceId).Distinct().Single(),Is.EqualTo(ar.appearances[0].id));
            Assert.That(a.Select(x=>x.audioEmitters[0].source).Distinct().Single(),Is.EqualTo(ar.audioSources[0].id));
            Assert.That(a.Select(x=>x.environmentProfile).Distinct().Single(),Is.EqualTo(ar.environmentProfiles[0].id));
            Assert.That(ar.appearances[0].id,Is.Not.EqualTo(br.appearances[0].id));Assert.That(ar.audioSources[0].id,Is.Not.EqualTo(br.audioSources[0].id));Assert.That(ar.environmentProfiles[0].id,Is.Not.EqualTo(br.environmentProfiles[0].id));
            ar.appearances[0].style.opacity=.7f;Assert.That(br.appearances[0].style.opacity,Is.EqualTo(.4f));Assert.That(JsonUtility.ToJson(batch),Is.EqualTo(before));
        }
        [Test] public void CaptureProducesDeterministicLocalSymbolsAndEditableModule() {
            var batch=CreationBatch.Read((JObject)Cases()[0]["arguments"]);Assert.That(batch.Prepare(out var objects,out var resources,out var error),Is.True,error);
            var document=new RoomDocument{objects=objects,appearances=resources.appearances,audioSources=resources.audioSources,environmentProfiles=resources.environmentProfiles};
            var local=CreationResources.Capture(objects,document);Assert.That(local.Validate(objects,out error),Is.True,error);
            Assert.That(local.appearances.Single().id,Is.EqualTo(new string('0',31)+"1"));Assert.That(local.appearances.Single().id,Is.Not.EqualTo(resources.appearances[0].id));
            batch.blueprint.resources=local;for(int i=0;i<objects.Length;i++)batch.blueprint.pieces[i].source.prototype=CreationPrototype.Capture(objects[i]);
            var module=ConstructionModule.Definition(batch,"Portable pieces");ProgramModuleLibrary.Validate(module);
            foreach(var id in resources.appearances.Select(x=>x.id).Concat(resources.audioSources.Select(x=>x.id)).Concat(resources.environmentProfiles.Select(x=>x.id)))Assert.That(module.ToString(),Does.Not.Contain(id));
        }
        [Test] public void NullPrototypeMembersAreRejectedWithoutThrowing() {
            var source=CreationBatch.Read((JObject)Cases()[0]["arguments"]).blueprint.pieces[0].source.prototype;
            source.appearanceBindings[0]=null;Assert.That(source.Validate(out var error),Is.False);Assert.That(error,Is.Not.Empty);
        }
    }
}
