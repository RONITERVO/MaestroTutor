// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Linq;
using Maestro.Quest.Avatar;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class ObjectAttentionMathTests
    {
        [Test]public void JointRestAxesAndMovingParentDefineTheSameBoundedDirection(){
            var root=new GameObject("actor");var parent=new GameObject("neck");var head=new GameObject("head");
            try{
                parent.transform.SetParent(root.transform,false);head.transform.SetParent(parent.transform,false);
                parent.transform.localRotation=Quaternion.Euler(17,34,-11);head.transform.localRotation=Quaternion.Euler(-52,75,19);
                var frame=new JointAttentionFrame(root.transform,head.transform);var correction=Quaternion.Inverse(root.transform.rotation)*head.transform.rotation;
                Assert.That(frame.Angles(head.transform.position+root.transform.forward*3,out var yaw,out var pitch,out var limited),Is.True);
                Assert.That(yaw,Is.EqualTo(0).Within(.001));Assert.That(pitch,Is.EqualTo(0).Within(.001));Assert.That(limited,Is.False);
                Assert.That(Quaternion.Angle(frame.Rotation(0,0),head.transform.rotation),Is.LessThan(.01));
                parent.transform.rotation=Quaternion.Euler(10,100,0)*parent.transform.rotation;
                var wanted=frame.Neutral*Quaternion.Euler(-20,40,0)*Vector3.forward;
                Assert.That(frame.Angles(head.transform.position+wanted*5,out yaw,out pitch,out limited),Is.True);
                Assert.That(yaw,Is.EqualTo(40).Within(.001));Assert.That(pitch,Is.EqualTo(-20).Within(.001));Assert.That(limited,Is.False);
                Assert.That(Vector3.Angle(frame.Rotation(yaw,pitch)*Quaternion.Inverse(correction)*Vector3.forward,wanted),Is.LessThan(.02));
                frame.Angles(head.transform.position+frame.Neutral*new Vector3(2,10,-1),out yaw,out pitch,out limited);
                Assert.That(yaw,Is.EqualTo(60));Assert.That(pitch,Is.EqualTo(-30));Assert.That(limited,Is.True);
                Assert.That(frame.Angles(head.transform.position,out _,out _,out _),Is.False);
            }finally{Object.DestroyImmediate(root);}
        }
        [Test]public void AttentionRefusesNonuniformAndReparentedJoints(){
            var root=new GameObject("actor");var head=new GameObject("head");
            try{
                head.transform.SetParent(root.transform,false);var frame=new JointAttentionFrame(root.transform,head.transform);
                Assert.That(frame.Valid,Is.True);root.transform.localScale=new Vector3(1,2,1);Assert.That(frame.Valid,Is.False);
                root.transform.localScale=Vector3.one;head.transform.SetParent(null);Assert.That(frame.Valid,Is.False);
            }finally{Object.DestroyImmediate(root);Object.DestroyImmediate(head);}
        }
        [Test]public void AttentionClaimsOnlyItsActorAndChosenPartWhileFocusIsARead(){
            var module=new ObjectAttentionCapability();var target=new string('a',32);
            var args=(JObject)module.Example.DeepClone();args["target"]=target;args["joint"]=new JObject{["kind"]="part",["part"]="Head"};args["focus"]=new JObject{["kind"]="object",["objectId"]="book"};
            Assert.That(BehaviourCatalog.TryCall(module.Id,1,args,out var call,out var error),Is.True,error);
            CollectionAssert.AreEquivalent(new[]{target,"book"},module.NativeEntities(args));
            CollectionAssert.AreEquivalent(new[]{"gaze","recipePart:Head"},call.Claims.Select(x=>x.Channel));
            Assert.That(call.Claims.All(x=>x.Target==target),Is.True);
            Assert.That(call.Claims.Any(x=>x.Conflicts(new BehaviourCatalog.Claim(target,"locomotion"))),Is.False);
            Assert.That(call.Claims.Any(x=>x.Conflicts(new BehaviourCatalog.Claim(target,"recipePart:Arm"))),Is.False);
            Assert.That(call.Claims.Any(x=>x.Conflicts(new BehaviourCatalog.Claim(target,"recipePart:Head"))),Is.True);
            Assert.That(call.Claims.Any(x=>x.Conflicts(new BehaviourCatalog.Claim(target,"wholeTarget"))),Is.True);
            args["focus"]["objectId"]=target;Assert.That(BehaviourCatalog.TryCall(module.Id,1,args,out _,out error),Is.False);Assert.That(error,Does.Contain("itself"));
            args["target"]="book";args["focus"]["objectId"]="maestro";Assert.That(BehaviourCatalog.TryCall(module.Id,1,args,out _,out error),Is.False);
        }
    }
}
