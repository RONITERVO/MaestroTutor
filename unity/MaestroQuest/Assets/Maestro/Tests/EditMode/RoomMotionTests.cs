// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;

namespace Maestro.Quest.Tests
{
    public sealed class RoomMotionTests
    {
        static RoomDocument Room() => new() { version = 1, objects = new[] { new RoomObjectData { id = "book", kind = RoomObjectKind.Book },new RoomObjectData { id = "maestro", kind = RoomObjectKind.Maestro } } };
        static RoomMotion Clip() => new() { frames = new[] {
            new MotionFrame { joints = new[] { new JointPose { joint = PoseJoint.Head } } },
            new MotionFrame { time = 2, position = Vector3.right, rotation = Quaternion.Euler(0,90,0), joints = new[] { new JointPose { joint = PoseJoint.Head, rotation = Quaternion.Euler(0,60,0) } } }
        } };
        [Test]
        public void PlayableInterpolatesTransformsAndJointRotationsAndLoops()
        {
            var graph = PlayableGraph.Create("Motion test");
            try
            {
                MotionFrame sampled = null;
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var play = ScriptPlayable<RoomMotionPlayable>.Create(graph); play.GetBehaviour().Motion = Clip(); play.GetBehaviour().Apply = value => sampled = value;
                ScriptPlayableOutput.Create(graph,"Test output").SetSourcePlayable(play); graph.Play(); play.SetTime(1); graph.Evaluate(0);
                Assert.That(sampled.position.x,Is.EqualTo(.5f).Within(.001f));
                Assert.That(Quaternion.Angle(Quaternion.identity,sampled.joints[0].rotation),Is.EqualTo(30).Within(.01f));
                play.GetBehaviour().Motion.loop = true; play.SetTime(3); graph.Evaluate(0);
                Assert.That(sampled.position.x,Is.EqualTo(.5f).Within(.001f));
                play.GetBehaviour().Motion.loop = false; play.SetTime(3); graph.Evaluate(0);
                Assert.That(sampled.position.x,Is.EqualTo(1).Within(.001f));
            }
            finally { graph.Destroy(); }
        }
        [Test]
        public void RejectsMalformedChannelsTimesTransformsAndTotalBudget()
        {
            var clip = Clip(); Assert.That(clip.Validate(RoomObjectKind.Maestro),Is.True);
            Assert.That(clip.Validate(RoomObjectKind.Block),Is.False);
            clip.frames[1].joints[0].joint = PoseJoint.Chest; Assert.That(clip.Validate(RoomObjectKind.Maestro),Is.False);
            clip = Clip(); clip.frames[1].time = float.NaN; Assert.That(clip.Validate(RoomObjectKind.Maestro),Is.False);
            clip = Clip(); clip.frames[1].time = 0; Assert.That(clip.Validate(RoomObjectKind.Maestro),Is.False);
            clip = Clip(); clip.frames[1].rotation = default; Assert.That(clip.Validate(RoomObjectKind.Maestro),Is.False);
            clip = Clip(); clip.frames[1].joints = new[] { new JointPose { joint = PoseJoint.Head },new JointPose { joint = PoseJoint.Head } }; Assert.That(clip.Validate(RoomObjectKind.Maestro),Is.False);
            var room = Room();
            room.objects = room.objects.Concat(Enumerable.Range(0,5).Select(_ => new RoomObjectData { id = Guid.NewGuid().ToString("N"), kind = RoomObjectKind.Block,
                motion = new RoomMotion { frames = Enumerable.Range(0,301).Select(i => new MotionFrame { time = i*.1f }).ToArray() } })).ToArray();
            Assert.That(room.Validate(out var error),Is.False); StringAssert.Contains("animation limit",error);
        }
        [Test]
        public void PoseAndMotionSurviveSaveUndoAndCallerMutationWithoutFreezingOldRooms()
        {
            string directory = Path.Combine(Path.GetTempPath(),"MaestroMotionTests-"+Guid.NewGuid().ToString("N"));
            try
            {
                var room = Room(); var storage = new RoomStorage(directory);
                Directory.CreateDirectory(directory);
                const string legacyPose = "\"position\":{\"x\":0,\"y\":0,\"z\":0},\"rotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},\"scale\":1,\"color\":{\"r\":1,\"g\":1,\"b\":1,\"a\":1}";
                File.WriteAllText(Path.Combine(directory,"room.v1.json"),"{\"version\":1,\"objects\":[{\"id\":\"book\",\"kind\":0,"+legacyPose+"},{\"id\":\"maestro\",\"kind\":1,"+legacyPose+"}]}");
                Assert.That(storage.Load(out _),Is.Not.Null,"Pre-animation rooms remain readable without any new fields");
                Assert.That(storage.Save(room,out _),Is.True);
                var old = storage.Load(out _); Assert.That(old,Is.Not.Null);
                Assert.That(old.objects[1].joints,Is.Null); Assert.That(old.objects[1].motion,Is.Null);
                var journal = new RoomJournal(room); var item = journal.Read("maestro"); item.motion = Clip(); item.joints = item.motion.frames[1].joints;
                Assert.That(journal.Apply(new[] { item },Array.Empty<string>(),out _),Is.True);
                item.motion.frames[1].joints[0].rotation = Quaternion.Euler(0,5,0);
                Assert.That(Quaternion.Angle(Quaternion.identity,journal.Read("maestro").joints[0].rotation),Is.EqualTo(60).Within(.01f));
                journal.Undo(); Assert.That(journal.Read("maestro").motion,Is.Null); journal.Redo();
                Assert.That(storage.Save(journal.Snapshot(),out _),Is.True);
                var restored = storage.Load(out var message); Assert.That(restored,Is.Not.Null,message);
                var maestro = restored.objects.Single(x => x.id == "maestro");
                Assert.That(maestro.motion.frames.Length,Is.EqualTo(2)); Assert.That(maestro.joints[0].joint,Is.EqualTo(PoseJoint.Head));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory,true); }
        }
    }
}
