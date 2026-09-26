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
        static RuleDocument Document() => new() { sequences=new[] { new RuleSequence { id=Guid.NewGuid().ToString("N"),name="Prop action",steps=new[] { new RuleStep { action=RuleActionKind.Gesture,seconds=1,propId=Guid.NewGuid().ToString("N") } } } } };
        [Test] public void PropBindingsRejectBadOffsetsTimingAndUnsupportedTargets()
        {
            var doc=Document(); var step=doc.sequences[0].steps[0]; Assert.That(doc.Validate(out _),Is.True);
            step.propOffset=Vector3.one; Assert.That(doc.Validate(out _),Is.False); step.propOffset=Vector3.zero;
            step.propReleaseAt=float.NaN; Assert.That(doc.Validate(out _),Is.False); step.propReleaseAt=0; Assert.That(doc.Validate(out _),Is.False); step.propReleaseAt=.5f;
            step.propRotation=new Quaternion(0,0,0,0); Assert.That(doc.Validate(out _),Is.False); step.propRotation=Quaternion.identity;
            step.action=RuleActionKind.Wait; Assert.That(doc.Validate(out _),Is.False); step.action=RuleActionKind.ImportedClip;
            step.targetId="book"; Assert.That(doc.Validate(out _),Is.False); step.targetId="maestro";
            step.propId="book"; Assert.That(doc.Validate(out _),Is.False); step.propId=Guid.NewGuid().ToString("N");
            doc.version=2; Assert.That(doc.Validate(out _),Is.False);
        }
        [Test] public void V2RulesMigrateWithoutEditingOriginalsAndNeverOverrideUnknownV3Files()
        {
            string directory=Path.Combine(Path.GetTempPath(),"MaestroProps-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            try
            {
                var doc=Document(); doc.version=2; doc.sequences[0].steps[0].propId=null;
                string old=JsonUtility.ToJson(doc); File.WriteAllText(Path.Combine(directory,"rules.v2.json"),old);
                var storage=new RuleStorage(directory); var next=storage.Load(out _); Assert.That(next.version,Is.EqualTo(3));
                next.sequences[0].steps[0].propId=Guid.NewGuid().ToString("N"); next.sequences[0].steps[0].propRelease=PropRelease.Throw;
                Assert.That(storage.Save(next,out var error),Is.True,error); Assert.That(File.ReadAllText(Path.Combine(directory,"rules.v2.json")),Is.EqualTo(old));
                Assert.That(new RuleStorage(directory).Load(out _).sequences[0].steps[0].propRelease,Is.EqualTo(PropRelease.Throw));
                File.WriteAllText(Path.Combine(directory,"rules.v3.json"),"{\"version\":4}");
                var future=new RuleStorage(directory); future.Load(out _); Assert.That(future.ReadOnly,Is.True); Assert.That(future.Save(next,out _),Is.False);
                Assert.That(File.ReadAllText(Path.Combine(directory,"rules.v3.json")),Is.EqualTo("{\"version\":4}"));
            }
            finally { Directory.Delete(directory,true); }
        }
    }
}
