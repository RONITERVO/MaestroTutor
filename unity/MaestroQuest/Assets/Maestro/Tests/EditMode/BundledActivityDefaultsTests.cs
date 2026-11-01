// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using System.Text;
using Maestro.Quest.Avatar;
using Maestro.Quest.Imports;
using Maestro.Quest.Persistence;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class BundledActivityDefaultsTests
    {
        string directory;byte[] model;JObject manifest;
        [SetUp]public void Setup(){directory=Path.Combine(Path.GetTempPath(),"MaestroDefaults-"+Guid.NewGuid().ToString("N"));model=ModelFixture.Mixamo();manifest=JObject.Parse(BundledMotionsFixture.Manifest(model));manifest["version"]=2;manifest["activities"]=new JArray(new JObject {["role"]=1,["choices"]=new JArray(new JObject {["motionId"]=manifest["catalogue"]["entries"][0]["id"].DeepClone(),["weight"]=1,["speed"]=1,["cooldown"]=3,["loop"]=false})});}
        [TearDown]public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        BundledMotions Pack()=>new(manifest.ToString(),(_,_)=>throw new InvalidOperationException("Metadata does not read payloads"));
        [Test]public void SeedUsesExactModelAndMotionIdentitiesAndReturnsIndependentCopies()
        {
            var pack=Pack();var seed=pack.DefaultActivities(pack.AvatarHash);Assert.That(seed.Valid(),Is.True);Assert.That(seed.avatars.Single().roles.Single().choices.Single().motionId,Is.EqualTo((string)manifest["catalogue"]["entries"][0]["id"]));
            Assert.That(pack.DefaultActivities(new string('f',64)).avatars,Is.Empty);seed.avatars[0].roles[0].choices[0].speed=2;Assert.That(pack.DefaultActivities(pack.AvatarHash).avatars[0].roles[0].choices[0].speed,Is.EqualTo(1));
            manifest["version"]=1;manifest.Remove("activities");Assert.That(Pack().DefaultActivities(pack.AvatarHash).avatars,Is.Empty);
        }
        [TestCase("unknownId")][TestCase("duplicateRole")][TestCase("unknownField")][TestCase("stringSpeed")][TestCase("emptyChoices")][TestCase("invalidWeight")][TestCase("shortClip")]
        public void InvalidPackDefaultsAreRejected(string problem)
        {
            var roles=(JArray)manifest["activities"];var choice=roles[0]["choices"][0];
            switch(problem){case "unknownId":choice["motionId"]=new string('f',32);break;case "duplicateRole":roles.Add(roles[0].DeepClone());break;case "unknownField":choice["script"]="anything";break;case "stringSpeed":choice["speed"]="1";break;case "emptyChoices":roles[0]["choices"]=new JArray();break;case "invalidWeight":choice["weight"]=0;break;case "shortClip":manifest["catalogue"]["entries"][0]["duration"]=.05;break;}
            Assert.Throws<ModelImportException>(()=>Pack());
        }
        [Test]public void FirstAssignmentIsDurableAndClearRemainsEmptyAfterPackageChanges()
        {
            var pack=Pack();var initial=pack.DefaultActivities(pack.AvatarHash);var profiles=new AvatarActivityProfiles(directory,initial:initial);
            Assert.That(profiles.Find(pack.AvatarHash),Is.Not.Null);Assert.That(profiles.CanUndo(pack.AvatarHash),Is.False);Assert.That(profiles.SavedMotion(initial.avatars[0].roles[0].choices[0].motionId,out var uncertain),Is.True);Assert.That(uncertain,Is.False);
            initial.avatars[0].roles[0].choices[0].speed=2;Assert.That(profiles.Find(pack.AvatarHash).roles[0].choices[0].speed,Is.EqualTo(1));
            Assert.That(profiles.Remove(pack.AvatarHash,TutorMotionRole.Listening,null,out var error),Is.True,error);Assert.That(profiles.Find(pack.AvatarHash),Is.Null);
            var reopened=new AvatarActivityProfiles(directory,initial:initial);Assert.That(reopened.Find(pack.AvatarHash),Is.Null,"An explicit empty saved profile is not a request to reseed");Assert.That(reopened.Notice,Is.Null);
        }
        [TestCase("avatar-activities.v2.json","{broken")][TestCase("avatar-activities.v3.json","{future")][TestCase("avatar-activities.v2.json.pending","{pending")][TestCase("avatar-activities.v2.json.unreadable","{unreadable")]
        public void DefaultsNeverReplaceSavedOrUnfinishedEvidence(string file,string content)
        {
            Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,file),content);var pack=Pack();var profiles=new AvatarActivityProfiles(directory,initial:pack.DefaultActivities(pack.AvatarHash));
            Assert.That(profiles.Snapshot().avatars,Is.Empty);Assert.That(File.ReadAllText(Path.Combine(directory,file)),Is.EqualTo(content));Assert.That(Directory.GetFiles(directory),Has.Length.EqualTo(1));
        }
        [Test]public void ExistingAssignmentsWinOverNewPackageSettings()
        {
            var pack=Pack();var seed=pack.DefaultActivities(pack.AvatarHash);var profiles=new AvatarActivityProfiles(directory,initial:seed);seed.avatars[0].roles[0].choices[0].speed=.5f;
            var reopened=new AvatarActivityProfiles(directory,initial:seed);Assert.That(reopened.Find(pack.AvatarHash).roles[0].choices[0].speed,Is.EqualTo(1));
        }
        [Test]public void FreshRecoveryCarriesTheSameDefaultsAndExactPortableMotions()
        {
            string package=Path.Combine(directory,"package");var included=BundledAvatarFixture.Write(package,model);var payloads=BundledMotionsFixture.Write(package,model);var pack=new BundledMotions(manifest.ToString(),(hash,_)=>payloads.Read(hash));
            var snapshot=WorkspaceDefaults.Snapshot(included,pack);var profiles=JsonUtility.FromJson<AvatarActivityDocument>(Encoding.UTF8.GetString(snapshot.Documents["avatar-activities.v2.json"]));
            Assert.That(profiles.Valid(),Is.True);Assert.That(profiles.avatars.Single().modelHash,Is.EqualTo(included.Hash));
            using var archive=new MemoryStream();var receipt=WorkspaceArchive.Write(archive,snapshot);Assert.That(receipt.Summary.MissingMotions,Is.Empty);
            Assert.That(WorkspaceDefaults.Snapshot(includedMotions:pack).Documents["avatar-activities.v2.json"],Is.EqualTo(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new AvatarActivityDocument()))));
        }
    }
}
