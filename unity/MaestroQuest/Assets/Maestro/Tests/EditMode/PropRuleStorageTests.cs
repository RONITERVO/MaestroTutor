// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using Maestro.Quest.Rules;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class PropRuleStorageTests
    {
        static RuleDocument Document() => new() { sequences=new[] { new RuleSequence { id=Guid.NewGuid().ToString("N"),name="Prop action",program=Maestro.Quest.Programs.BehaviourProgram.FromSteps(new RuleStep { action=RuleActionKind.Gesture,seconds=1,propId=Guid.NewGuid().ToString("N") }) } } };
        [Test] public void PropBindingsRejectBadOffsetsTimingAndUnsupportedTargets()
        {
            var doc=Document();var step=doc.sequences[0].SimpleSteps()[0];Assert.That(doc.Validate(out _),Is.True);
            bool Valid()=>RuleDocument.ValidStep(step,out _);
            step.propOffset=Vector3.one;Assert.That(Valid(),Is.False);step.propOffset=Vector3.zero;
            step.propReleaseAt=float.NaN;Assert.That(Valid(),Is.False);step.propReleaseAt=0;Assert.That(Valid(),Is.False);step.propReleaseAt=.5f;
            step.propRotation=new Quaternion(0,0,0,0);Assert.That(Valid(),Is.False);step.propRotation=Quaternion.identity;
            step.action=RuleActionKind.Wait;Assert.That(Valid(),Is.False);step.action=RuleActionKind.ImportedClip;
            step.targetId="book";Assert.That(Valid(),Is.False);step.targetId="maestro";
            step.propId="book";Assert.That(Valid(),Is.False);step.propId=Guid.NewGuid().ToString("N");
            Assert.That(Valid(),Is.True);doc.version=3;Assert.That(doc.Validate(out _),Is.False);
        }
        [Test] public void RetiredDevelopmentRulesStayUntouchedAndCannotOverrideCanonicalPrograms()
        {
            string directory=Path.Combine(Path.GetTempPath(),"MaestroReset-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try {
                foreach(int version in new[] {1,2,3,4,5})File.WriteAllText(Path.Combine(directory,"rules.v"+version+".json"),"Retired development save "+version);
                File.WriteAllText(Path.Combine(directory,"behaviours.v1.json"),"Retired numeric program save");
                var storage=new RuleStorage(directory);var loaded=storage.Load(out _);Assert.That(loaded.sequences,Is.Empty);Assert.That(storage.ReadOnly,Is.False);
                Assert.That(File.ReadAllText(Path.Combine(directory,"behaviours.v1.json")),Is.EqualTo("Retired numeric program save"));
                var doc=Document();Assert.That(storage.Save(doc,out var error),Is.True,error);
                Assert.That(new RuleStorage(directory).Load(out _).sequences[0].program,Is.EqualTo(doc.sequences[0].program));
                foreach(int version in new[] {1,2,3,4,5})Assert.That(File.ReadAllText(Path.Combine(directory,"rules.v"+version+".json")),Is.EqualTo("Retired development save "+version));
                File.WriteAllText(Path.Combine(directory,"behaviours.v2.json"),"{\"version\":3}");
                storage=new RuleStorage(directory);storage.Load(out _);Assert.That(storage.ReadOnly,Is.True);Assert.That(storage.Save(doc,out _),Is.False);
            }finally {Directory.Delete(directory,true);}
        }
        [Test] public void PropReleaseAndBlockIdsSurviveBackupRecoveryWithoutLegacyStepFields()
        {
            string directory=Path.Combine(Path.GetTempPath(),"MaestroCanonicalProps-"+Guid.NewGuid().ToString("N"));
            try {
                var document=Document();var sequence=document.sequences[0];var steps=sequence.SimpleSteps();
                steps[0].propRelease=PropRelease.Throw;sequence.SetSimpleSteps(steps);var storage=new RuleStorage(directory);
                Assert.That(storage.Save(document,out _),Is.True);Assert.That(storage.Save(document,out _),Is.True);
                File.WriteAllText(Path.Combine(directory,"behaviours.v2.json"),"broken");
                var loaded=new RuleStorage(directory).Load(out var message);Assert.That(message,Does.Contain("backup"));
                var recovered=loaded.sequences[0].SimpleSteps()[0];Assert.That(recovered.propRelease,Is.EqualTo(PropRelease.Throw));Assert.That(recovered.id,Is.EqualTo(steps[0].id));
                Assert.That(Newtonsoft.Json.Linq.JObject.Parse(JsonUtility.ToJson(loaded))["sequences"][0]["steps"],Is.Null);
            }finally {Directory.Delete(directory,true);}
        }
    }
}
