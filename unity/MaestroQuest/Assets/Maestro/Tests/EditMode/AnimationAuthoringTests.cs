// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public class AnimationAuthoringTests
    {
        [Test] public void FramePatchesAreDetachedBoundedAndKeepExactChannels()
        {
            var current=new RoomMotion {loop=true,frames=Enumerable.Range(0,301).Select(i=>new MotionFrame {time=i*.1f}).ToArray()};var frame=new MotionFrame {time=1,position=Vector3.right};Assert.That(RoomMotionEdits.Put(current,new[]{frame},false,out var patched,out _),Is.True);Assert.That(patched.Validate(RoomObjectKind.Block),Is.True);frame.position=Vector3.left;Assert.That(current.frames[10].position,Is.EqualTo(Vector3.zero));Assert.That(patched.frames[10].position,Is.EqualTo(Vector3.right));
            Assert.That(RoomMotionEdits.Put(current,new[]{new MotionFrame {time=.05f}},false,out var overflow,out _),Is.True);Assert.That(overflow.Validate(RoomObjectKind.Block),Is.False);
            Assert.That(RoomMotionEdits.Remove(patched,new[]{0f,.1f},out var shortened,out _),Is.True);Assert.That(shortened.frames[0].time,Is.Zero);Assert.That(shortened.loop,Is.True);Assert.That(patched.frames.Length,Is.EqualTo(301));
        }
        [Test] public void AuthoringSchemaRejectsUnknownOrDuplicateJointsAndPartialSettings()
        {
            var definition=BehaviourCatalog.Action("animation.author");var args=new JObject {["operation"]="settings",["target"]="maestro",["revision"]=1};Assert.That(definition.TryCall(args,out _,out _),Is.False);
            args["operation"]="pose";args["joints"]=new JArray(new JObject {["joint"]="LeftWing",["rotation"]=JObject.Parse("{\"x\":0,\"y\":0,\"z\":0,\"w\":1}")});Assert.That(definition.TryCall(args,out _,out _),Is.False);
            args["joints"][0]["joint"]="Head";((JArray)args["joints"]).Add(args["joints"][0].DeepClone());Assert.That(definition.TryCall(args,out _,out _),Is.False);
            ((JArray)args["joints"]).RemoveAt(1);Assert.That(definition.TryCall(args,out var call,out _),Is.True);Assert.That(call.Resources,Is.EqualTo(new[]{"maestro"}));
        }
    }
}
