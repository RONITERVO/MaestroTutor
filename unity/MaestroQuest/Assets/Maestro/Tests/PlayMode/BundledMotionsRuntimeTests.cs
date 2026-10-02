// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Imports;
using Maestro.Quest.Avatar;
using Maestro.Quest.Book;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class BundledAvatarRuntimeTests
    {
        IEnumerator StateMotion(string state,string[] choices) {
            avatar.ObserveTutorState(new BookSnapshot {version=1,activity=state});float until=Time.realtimeSinceStartup+20;
            while(!choices.Contains(avatar.ActivityMotionId)&&Time.realtimeSinceStartup<until)yield return null;
            Assert.That(choices,Does.Contain(avatar.ActivityMotionId),avatar.ActivityMotionStatus);
        }
        [UnityTest]public IEnumerator ShippedStartingAssignmentsFollowTutorStatesAndClearSurvivesRestart()
        {
            var included=BundledAvatar.FromApplication();var pack=BundledMotions.FromApplication();Open(included,pack);yield return Loaded();
            float until=Time.realtimeSinceStartup+30;while(!editor.Motions.IncludedInitialization.IsCompleted&&Time.realtimeSinceStartup<until)yield return null;
            Assert.That(editor.Motions.IncludedInitialization.IsCompleted,Is.True);Assert.That(editor.Motions.Notice,Is.Null);
            var profile=editor.ActivityProfiles.Find(included.Hash);Assert.That(profile.roles,Has.Length.EqualTo(4));Assert.That(avatar.ActivityMotionId,Is.Null,"Assignments alone are not a fresh tutor activity");
            var origin=avatar.transform.position;
            foreach(var role in profile.roles) {
                var choices=role.choices.Select(x=>x.motionId).ToArray();yield return StateMotion(role.role.ToString().ToLowerInvariant(),choices);
                Assert.That(avatar.transform.position,Is.EqualTo(origin));
                Assert.That(editor.Motions.ResidentClipCount,Is.LessThanOrEqualTo(5),"The rest of the included library stays metadata-only");
            }
            avatar.ReducedMotion=true;yield return null;Assert.That(avatar.ActivityMotionId,Is.Null);avatar.ReducedMotion=false;
            Assert.That(editor.ActivityProfiles.Remove(included.Hash,TutorMotionRole.Speaking,null,out var error),Is.True,error);yield return null;Assert.That(avatar.ActivityMotionId,Is.Null);
            editor.SaveNow();UnityEngine.Object.Destroy(root);yield return null;Open(included,pack);yield return Loaded();
            Assert.That(editor.ActivityProfiles.Find(included.Hash).roles.Any(x=>x.role==TutorMotionRole.Speaking),Is.False);avatar.ObserveTutorState(new BookSnapshot {version=1,activity="speaking"});yield return new WaitForSeconds(.3f);Assert.That(avatar.ActivityMotionId,Is.Null);
        }
        [UnityTest]public IEnumerator ExistingRoomWithoutProfilesDoesNotAcquireNewDefaults()
        {
            string saved=Path.Combine(directory,"saved");Directory.CreateDirectory(saved);File.WriteAllText(Path.Combine(saved,"existing-content.txt"),"Existing workspace");
            var included=BundledAvatar.FromApplication();Open(included,BundledMotions.FromApplication());yield return Loaded();Assert.That(editor.ActivityProfiles.Find(included.Hash),Is.Null);Assert.That(File.Exists(Path.Combine(saved,"avatar-activities.v2.json")),Is.False);
        }
        [UnityTest]public IEnumerator ExistingLibraryAddsIncludedMotionsThroughSharedActionWithoutReplacingAvatarOrStartingPlayback()
        {
            var model=ModelFixture.Mixamo();var included=BundledAvatarFixture.Write(Path.Combine(directory,"package"),model);var motions=BundledMotionsFixture.Write(Path.Combine(directory,"package"),model);
            string library=Path.Combine(directory,"saved","motions");Directory.CreateDirectory(library);File.WriteAllText(Path.Combine(library,"motions.v2.json"),JsonConvert.SerializeObject(new MotionCatalogue()));
            Open(included,motions);yield return Loaded();Assert.That(editor.Motions.List(),Is.Empty);var before=Fact("motion.pack.included");Assert.That((int)before["counts"]["catalogued"],Is.Zero);
            var call=new JObject {["operation"]="start",["runId"]=actions.Observe()["nextRunId"].DeepClone(),["call"]=new JObject {["id"]="motion.pack.install",["version"]=1,["arguments"]=new JObject {["operation"]="add",["manifestHash"]=motions.Hash}}};
            Assert.That(actions.Execute(call,out var error),Is.True,error);yield return Completed();var receipt=actions.Observe();var after=Fact("motion.pack.included");Assert.That((int)after["counts"]["catalogued"],Is.EqualTo(1));Assert.That((int)receipt["selected"]["output"]["added"],Is.EqualTo(1));
            Assert.That(editor.Motions.ResidentClipCount,Is.Zero);Assert.That(avatar.IsImportedClipPlaying,Is.False);Assert.That(avatar.ModelHash,Is.EqualTo(included.Hash));Assert.That(actions.Execute(call,out error),Is.True,error);Assert.That(editor.Motions.List(),Has.Length.EqualTo(1));
            string capture=Environment.GetEnvironmentVariable("MAESTRO_INCLUDED_MOTIONS_EVIDENCE");if(!string.IsNullOrEmpty(capture)){Directory.CreateDirectory(capture);File.WriteAllText(Path.Combine(capture,"package.json"),new JObject {["before"]=before,["after"]=after,["receipt"]=receipt}.ToString());}
            var id=editor.Motions.List()[0].id;var load=editor.Motions.AcquireAsync(id,avatar.CustomModel.MotionRigHash);yield return new WaitUntil(()=>load.IsCompleted);Assert.That(load.Exception,Is.Null);Assert.That(avatar.PlayLibraryMotion(load.Result,false),Is.True);yield return null;avatar.StopImportedClip();
        }
        [UnityTest]public IEnumerator ShippedMotionCollectionLoadsMetadataOnceAndCompilesOnlyAnExplicitlyPlayedClip()
        {
            var pack=BundledMotions.FromApplication();Assert.That(pack,Is.Not.Null);Open(BundledAvatar.FromApplication(),pack);yield return Loaded();
            float until=Time.realtimeSinceStartup+30;while(!editor.Motions.IncludedInitialization.IsCompleted&&Time.realtimeSinceStartup<until)yield return null;
            Assert.That(editor.Motions.IncludedInitialization.IsCompleted,Is.True);Assert.That(editor.Motions.Notice,Is.Null);Assert.That(editor.Motions.List(),Has.Length.EqualTo(pack.Count));Assert.That(editor.Motions.ResidentClipCount,Is.Zero);Assert.That(avatar.IsImportedClipPlaying,Is.False);
            Assert.That(avatar.CustomModel.Instance.SkinnedMeshRenderers.All(x=>x.quality==SkinQuality.Bone4),Is.True,"Android quality must not discard the Meshy mesh's third and fourth influences");
            var entry=editor.Motions.List().First(x=>x.name=="Agree_Gesture");Assert.That(entry.rigHash,Is.EqualTo(avatar.CustomModel.MotionRigHash));var load=editor.Motions.AcquireAsync(entry.id,entry.rigHash);yield return new WaitUntil(()=>load.IsCompleted);Assert.That(load.Exception,Is.Null);Assert.That(editor.Motions.ResidentClipCount,Is.EqualTo(1));Assert.That(avatar.PlayLibraryMotion(load.Result,false),Is.True);yield return new WaitForSeconds(.05f);avatar.StopImportedClip();Assert.That(avatar.IsImportedClipPlaying,Is.False);
        }
    }
}
