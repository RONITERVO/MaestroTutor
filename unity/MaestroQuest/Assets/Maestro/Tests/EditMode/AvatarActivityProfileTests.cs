// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Imports;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Maestro.Quest.Tests
{
    public sealed class AvatarActivityProfileTests
    {
        [Test] public void StateProfilesRejectConflictingExcessiveOrNonfinitePreferences()
        {
            var choice=new TutorMotionChoice { motionId=Guid.NewGuid().ToString("N") };
            var profile=new AvatarActivityProfile { modelHash=new string('a',64),rigHash=new string('b',64),roles=new[] { new TutorRoleChoices { role=TutorMotionRole.Speaking,choices=new[] { choice } } } };
            var document=new AvatarActivityDocument { avatars=new[] { profile } }; Assert.That(document.Valid(),Is.True);
            choice.speed=float.NaN; Assert.That(document.Valid(),Is.False); choice.speed=1;
            choice.cooldown=61; Assert.That(document.Valid(),Is.False); choice.cooldown=0;
            choice.weight=0; Assert.That(document.Valid(),Is.False); choice.weight=1;
            profile.roles[0].choices=new[] { choice,choice.Copy() }; Assert.That(document.Valid(),Is.False);
            profile.roles[0].choices=new[] { choice }; profile.roles=new[] { profile.roles[0],profile.roles[0].Copy() }; Assert.That(document.Valid(),Is.False);
            profile.roles=profile.roles.Take(1).ToArray(); document.avatars=new[] { profile,profile.Copy() }; Assert.That(document.Valid(),Is.False);
        }
        [UnityTest] public IEnumerator AssignmentUndoIsPerAvatarAndKeepsReferencedMotionsForRecovery()
        {
            var directory=Path.Combine(Path.GetTempPath(),"MaestroActivityProfiles-"+Guid.NewGuid().ToString("N"));
            using var library=new MotionLibrary(Path.Combine(directory,"motions"));
            try
            {
                var load=library.ImportAsync("avatar.glb",ModelFixture.Mixamo()); while (!load.IsCompleted) yield return null; Assert.That(load.Exception,Is.Null);
                var motion=load.Result.Single(); var profiles=new AvatarActivityProfiles(directory); string a=new string('a',64),b=new string('b',64);
                var choice=new TutorMotionChoice { motionId=motion.id,weight=3,speed=.75f,cooldown=10 };
                Assert.That(profiles.Assign(a,motion.rigHash,TutorMotionRole.Speaking,choice,library,out var error),Is.True,error);
                Assert.That(profiles.Assign(b,motion.rigHash,TutorMotionRole.Listening,choice,library,out error),Is.True,error);
                Assert.That(profiles.Undo(a,out error),Is.True,error); Assert.That(profiles.Find(a),Is.Null); Assert.That(profiles.Find(b).roles.Single().role,Is.EqualTo(TutorMotionRole.Listening));
                Assert.That(profiles.ReferencedMotionIds,Does.Contain(motion.id)); Assert.That(profiles.Redo(a,out error),Is.True,error);
                var reloaded=new AvatarActivityProfiles(directory); Assert.That(reloaded.Find(a).roles[0].choices[0].weight,Is.EqualTo(3));
                var copy=profiles.Find(a); copy.roles[0].choices[0].weight=9; Assert.That(profiles.Find(a).roles[0].choices[0].weight,Is.EqualTo(3));
                Assert.That(profiles.Remove(a,TutorMotionRole.Speaking,motion.id,out error),Is.True,error); Assert.That(profiles.Undo(a,out error),Is.True,error);
                var file=Path.Combine(directory,"avatar-activities.v2.json"); File.WriteAllText(file,"{\"version\":7}");
                var newer=new AvatarActivityProfiles(directory); Assert.That(newer.ReadOnly,Is.True); Assert.That(newer.Assign(a,motion.rigHash,TutorMotionRole.Speaking,choice,library,out _),Is.False);
                Assert.That(File.ReadAllText(file),Is.EqualTo("{\"version\":7}"));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory,true); }
        }
    }
}
