// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Imports;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class BundledAvatarRuntimeTests
    {
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
