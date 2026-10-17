// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using Maestro.Quest.Creation;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class AcceptedRoomPrimaryTests
    {
        [Serializable] public sealed class Probe {public int version=1,value=1;}
        string directory,primary;VersionedRoomFile<Probe> file;Action<Probe> validate;
        VersionedRoomFile<Probe> Store()=>new(directory,"probe",8192,
            x=>{validate?.Invoke(x);return x.value>=0;},
            x=>new Probe{value=x.value},null,x=>x.version=1,version:1,
            cachePrimaryInspection:true,trackAcceptedPrimary:true);
        [SetUp] public void SetUp(){
            directory=Path.Combine(Path.GetTempPath(),"MaestroAcceptedPrimary-"+Guid.NewGuid().ToString("N"));
            primary=Path.Combine(directory,"probe.v1.json");file=Store();
            Assert.That(file.Save(new Probe(),out var error),Is.True,error);
            Assert.That(file.Save(new Probe(),out error),Is.True,error);
        }
        [TearDown] public void TearDown(){validate=null;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        void Outside(int value=9){var stamp=File.GetLastWriteTimeUtc(primary);File.WriteAllText(primary,File.ReadAllText(primary).Replace("\"value\":1","\"value\":"+value));File.SetLastWriteTimeUtc(primary,stamp);}
        VersionedRoomFile<Probe>.PreparedSave Prepare(){
            Assert.That(file.PrepareSave(new Probe{value=2},file.CaptureForPreparation(),out var prepared,out var error),Is.True,error);return prepared;
        }
        [Test] public void IndependentStoresCannotOverwriteTheSameLengthReplacement(){
            var other=Store();Assert.That(other.Load(out _).value,Is.EqualTo(1));
            Assert.That(other.Save(new Probe{value=9},out var error),Is.True,error);
            string accepted=File.ReadAllText(primary),backup=File.ReadAllText(primary+".backup");
            Assert.That(file.Save(new Probe{value=2},out error),Is.False);Assert.That(error,Does.Contain("outside"));
            Assert.That(file.ReadOnly,Is.True);Assert.That(File.ReadAllText(primary),Is.EqualTo(accepted));Assert.That(File.ReadAllText(primary+".backup"),Is.EqualTo(backup));
        }
        [Test] public void ChangesDuringLoadDoNotBindUnseenBytes(){
            validate=x=>{validate=null;Outside();};Assert.That(file.Load(out var error),Is.Null);Assert.That(error,Does.Contain("outside"));
            Assert.That(file.ReadOnly,Is.True);Assert.That(File.ReadAllText(primary),Does.Contain("\"value\":9"));
        }
        [Test] public void ExistingPrimaryMustBeLoadedBeforeTheFirstSave(){
            var other=Store();Assert.That(other.Save(new Probe{value=4},out var error),Is.False);Assert.That(error,Does.Contain("Load"));
            Assert.That(other.ReadOnly,Is.False);Assert.That(other.Load(out _).value,Is.EqualTo(1));Assert.That(other.Save(new Probe{value=4},out error),Is.True,error);
        }
        [Test] public void SameTimestampExternalEditCannotBecomeAStartupInspectionCacheHit(){
            Outside();Assert.That(file.Save(new Probe{value=2},out var error),Is.False);Assert.That(error,Does.Contain("outside"));
        }
        [Test] public void SuccessfulPreparedPublicationAdvancesTheOrdinarySaveBaseline(){
            using var prepared=Prepare();Assert.That(file.PublishPrepared(prepared,out var error),Is.True,error);
            Assert.That(file.Save(new Probe{value=3},out error),Is.True,error);
            Assert.That(file.Load(out _).value,Is.EqualTo(3));
        }
        [Test] public void NewerOwnSaveRejectsOldPreparationWithoutBlockingLaterEdits(){
            using var prepared=Prepare();Assert.That(file.Save(new Probe{value=3},out var error),Is.True,error);
            Assert.That(file.PublishPrepared(prepared,out error),Is.False);Assert.That(error,Is.Not.Empty);Assert.That(file.ReadOnly,Is.False);
            Assert.That(file.Save(new Probe{value=4},out error),Is.True,error);
        }
        [Test] public void CancelledPreparationDoesNotLetAutosaveOverwriteAnOutsideChange(){
            var prepared=Prepare();Outside();prepared.Dispose();
            Assert.That(file.Save(new Probe{value=3},out var error),Is.False);Assert.That(error,Does.Contain("outside"));
            Assert.That(File.ReadAllText(primary),Does.Contain("\"value\":9"));
        }
        [Test] public void NewPreparationCannotBlessAChangedPrimary(){
            Outside();Assert.Throws<IOException>(()=>file.CaptureForPreparation());
            Assert.That(file.ReadOnly,Is.True);Assert.That(file.Save(new Probe{value=3},out _),Is.False);
        }
        [Test] public void ExternalDeletionAndCreationAreAlsoConflicts(){
            File.Delete(primary);Assert.That(file.Save(new Probe(),out _),Is.False);Assert.That(File.Exists(primary),Is.False);
            var other=Store();File.Delete(primary+".backup");Assert.That(other.Load(out _),Is.Null);
            File.WriteAllText(primary,"{\"version\":1,\"value\":8}");
            Assert.That(other.Save(new Probe(),out _),Is.False);Assert.That(File.ReadAllText(primary),Does.Contain(":8"));
        }
        [Test] public void ChangeDuringCandidateValidationPreservesPrimaryAndBackup(){
            string backup=File.ReadAllText(primary+".backup");
            validate=x=>{if(x.value!=2)return;validate=null;Outside();};
            Assert.That(file.Save(new Probe{value=2},out var error),Is.False);Assert.That(error,Does.Contain("outside"));
            Assert.That(File.ReadAllText(primary),Does.Contain("\"value\":9"));Assert.That(File.ReadAllText(primary+".backup"),Is.EqualTo(backup));Assert.That(File.Exists(primary+".pending"),Is.False);
        }
        [Test] public void ForeignPendingFileSurvivesAndExplicitRemovalAllowsRetry(){
            string accepted=File.ReadAllText(primary),pending=primary+".pending";File.WriteAllText(pending,"another writer's unfinished work");
            Assert.That(file.Save(new Probe{value=2},out _),Is.False);Assert.That(File.ReadAllText(pending),Is.EqualTo("another writer's unfinished work"));
            Assert.That(File.ReadAllText(primary),Is.EqualTo(accepted));Assert.That(file.ReadOnly,Is.False);
            File.Delete(pending);Assert.That(file.Save(new Probe{value=2},out var error),Is.True,error);
        }
        [Test] public void ExplicitLoadCanRecoverAnEmptyPrimaryFromItsBackup(){
            File.WriteAllBytes(primary,Array.Empty<byte>());var restored=file.Load(out var message);
            Assert.That(restored,Is.Not.Null,message);Assert.That(message,Does.Contain("backup"));
            Assert.That(file.Save(restored,out var error),Is.True,error);Assert.That(File.ReadAllBytes(primary+".unreadable"),Is.Empty);
        }
    }
}
