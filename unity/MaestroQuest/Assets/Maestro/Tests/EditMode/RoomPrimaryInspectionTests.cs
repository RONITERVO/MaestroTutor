// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Maestro.Quest.Tests
{
    public sealed class RoomPrimaryInspectionTests
    {
        [Serializable] public sealed class Probe
        {
            public int version=1, value=1;
            public string tag="one";
        }
        string directory,primary;
        Action<Probe> onValidate;
        VersionedRoomFile<Probe> file;
        [SetUp] public void SetUp()
        {
            directory=Path.Combine(Path.GetTempPath(),"MaestroPrimaryInspection-"+Guid.NewGuid().ToString("N"));
            primary=Path.Combine(directory,"probe.v1.json");
            file=new VersionedRoomFile<Probe>(directory,"probe",8192,
                x=>{onValidate?.Invoke(x);return x.value>=0&&x.value<=5&&x.tag!=null;},
                x=>new Probe{version=x.version,value=x.value,tag=x.tag},null,x=>x.version=1,version:1,
                validWire:x=>x.Count==3&&x["version"]?.Type==JTokenType.Integer&&x["value"]?.Type==JTokenType.Integer&&x["tag"]?.Type==JTokenType.String,cachePrimaryInspection:true);
            Assert.That(file.Save(new Probe(),out var error),Is.True,error);
            Assert.That(file.Save(new Probe(),out error),Is.True,error);
        }
        [TearDown] public void TearDown(){onValidate=null;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        string ReplaceSameMetadata(string replacement)
        {
            var stamp=File.GetLastWriteTimeUtc(primary);int length=File.ReadAllText(primary).Length;
            Assert.That(replacement.Length,Is.LessThanOrEqualTo(length));
            replacement=replacement.PadRight(length,' ');
            File.WriteAllText(primary,replacement);File.SetLastWriteTimeUtc(primary,stamp);
            return replacement;
        }
        [Test] public void ChangedVersionWithSameLengthAndTimestampStillBlocksSaving()
        {
            var before=File.ReadAllText(primary+".backup");
            var newer=ReplaceSameMetadata(File.ReadAllText(primary).Replace("\"version\":1","\"version\":2"));
            Assert.That(file.Save(new Probe(),out _),Is.False);
            Assert.That(file.ReadOnly,Is.True);
            Assert.That(File.ReadAllText(primary),Is.EqualTo(newer));
            Assert.That(File.ReadAllText(primary+".backup"),Is.EqualTo(before));
            Assert.That(File.Exists(primary+".pending"),Is.False);
        }
        [Test] public void DuplicateFieldsWithSameMetadataArePreservedAsUnreadable()
        {
            string corrupt=ReplaceSameMetadata(File.ReadAllText(primary).Replace("\"tag\":\"one\"","\"value\":2"));
            Assert.That(file.Save(new Probe(),out var error),Is.True,error);
            Assert.That(File.ReadAllText(primary+".unreadable"),Is.EqualTo(corrupt));
            Assert.That(file.Load(out error)?.value,Is.EqualTo(1),error);
        }
        [Test] public void RepeatedInvalidContentsNeverBecomeAValidatedBackup()
        {
            string corrupt=File.ReadAllText(primary).Replace("\"value\":1","\"value\":9");
            string good=File.ReadAllText(primary+".backup");
            for(int i=0;i<2;i++) {
                ReplaceSameMetadata(corrupt);
                Assert.That(file.Save(new Probe(),out var error),Is.True,error);
                Assert.That(File.ReadAllText(primary+".unreadable"),Is.EqualTo(corrupt));
                Assert.That(File.ReadAllText(primary+".backup"),Is.EqualTo(good));
            }
        }
        [Test] public void ReplacementDuringCandidateValidationIsInspectedAgain()
        {
            string corrupt=File.ReadAllText(primary).Replace("\"value\":1","\"value\":9");
            onValidate=x=>{if(x.value!=2)return;onValidate=null;ReplaceSameMetadata(corrupt);};
            Assert.That(file.Save(new Probe{value=2},out var error),Is.True,error);
            Assert.That(File.ReadAllText(primary+".unreadable"),Is.EqualTo(corrupt));
            Assert.That(file.Load(out error)?.value,Is.EqualTo(2),error);
        }
        [Test] public void NewerVersionAppearingDuringPreparationPreservesPrimaryAndBackup()
        {
            string good=File.ReadAllText(primary+".backup");
            string newer=File.ReadAllText(primary).Replace("\"version\":1","\"version\":2");
            onValidate=x=>{if(x.value!=2)return;onValidate=null;ReplaceSameMetadata(newer);};
            Assert.That(file.Save(new Probe{value=2},out _),Is.False);
            Assert.That(file.ReadOnly,Is.True);
            Assert.That(File.ReadAllText(primary),Is.EqualTo(newer));
            Assert.That(File.ReadAllText(primary+".backup"),Is.EqualTo(good));
            Assert.That(File.Exists(primary+".pending"),Is.False);
        }
        [Test] public void StoresWithContextualValidationDoNotCacheByDefault()
        {
            bool needsNewerReader=false;
            var contextual=new VersionedRoomFile<Probe>(directory,"contextual",8192,
                x=>x.value>=0,x=>new Probe{version=x.version,value=x.value,tag=x.tag},null,x=>x.version=1,
                version:1,newerDocument:x=>needsNewerReader);
            Assert.That(contextual.Save(new Probe(),out var error),Is.True,error);
            Assert.That(contextual.Save(new Probe(),out error),Is.True,error);
            string path=Path.Combine(directory,"contextual.v1.json"),before=File.ReadAllText(path);
            needsNewerReader=true;
            Assert.That(contextual.Save(new Probe{value=2},out _),Is.False);
            Assert.That(contextual.ReadOnly,Is.True);
            Assert.That(File.ReadAllText(path),Is.EqualTo(before));
        }
    }
}
