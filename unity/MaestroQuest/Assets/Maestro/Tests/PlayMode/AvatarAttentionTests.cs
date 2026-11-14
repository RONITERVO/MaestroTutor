// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.IO;
using Maestro.Quest.Avatar;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class AvatarSpatialTests {
        static JObject AttentionCall(JObject focus,float seconds=10)=>new(){["id"]="avatar.look",["version"]=1,["arguments"]=new JObject{["target"]="maestro",["focus"]=focus,["seconds"]=seconds}};
        static JObject ObjectFocus(string id="book")=>new(){["kind"]="object",["objectId"]=id};
        static JObject PointFocus(Vector3 point)=>new(){["kind"]="point",["point"]=new JObject{["x"]=point.x,["y"]=point.y,["z"]=point.z}};
        JObject AttentionFact(){
            Assert.That(BehaviourCatalog.TryRead("avatar.attention",1,new JObject{["target"]="maestro"},new BehaviourCatalog.FactContext(editor:editor),out var fact),Is.True);
            return JObject.FromObject(fact.Value);
        }
        [UnityTest] public IEnumerator AttentionTracksMovingObjectAtRenderedHeadAndNeverMovesEitherRoot(){
            Tutor();yield return null;viewer.transform.position=new(-3,1.6f,3);
            var book=editor.Find("book");book.transform.position=new(3,1.6f,3);Physics.SyncTransforms();
            var before=avatar.transform.position;var rotation=avatar.transform.rotation;
            var head=avatar.PoseRig.CanonicalBone(PoseJoint.Head);Quaternion shown=Quaternion.identity;
            var probe=avatar.gameObject.AddComponent<AvatarChannelProbe>();probe.Sample=()=>shown=head.rotation;
            var actions=new RoomRuleActions(editor,authoring);var scheduler=new RuleScheduler(actions);
            Assert.That(scheduler.Invoke(AttentionCall(ObjectFocus()),0,out var run,out var error),Is.True,error);
            try{
                yield return new WaitForSeconds(.65f);var right=shown;var first=AttentionFact();
                Assert.That((float)first["yaw"],Is.GreaterThan(35));Assert.That((bool)first["applied"],Is.True);Assert.That((string)first["objectId"],Is.EqualTo("book"));
                book.transform.position=new(-3,1.6f,3);Physics.SyncTransforms();yield return new WaitForSeconds(.8f);
                var second=AttentionFact();Assert.That((float)second["yaw"],Is.LessThan(-35));
                Assert.That(Quaternion.Angle(right,shown),Is.GreaterThan(65),"The rendered head must change, not just the status.");
                Assert.That((float)second["point"]["x"],Is.EqualTo(-3).Within(.001));Assert.That(avatar.transform.position,Is.EqualTo(before));Assert.That(avatar.transform.rotation,Is.EqualTo(rotation));
                Assert.That(book.transform.position,Is.EqualTo(new Vector3(-3,1.6f,3)));
                scheduler.Tick(11);var receipt=scheduler.Invocation(run);Assert.That((string)receipt["phase"],Is.EqualTo("completed"),receipt.ToString());
                Assert.That((float)receipt["output"]["yaw"],Is.LessThan(-35));Assert.That((bool)AttentionFact()["active"],Is.False);
                var path=Environment.GetEnvironmentVariable("MAESTRO_ATTENTION_EVIDENCE");
                if(!string.IsNullOrEmpty(path)){Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"rendered-object-attention.json"),new JObject{["first"]=first,["second"]=second,["renderedHeadDelta"]=Quaternion.Angle(right,shown),["receipt"]=receipt}.ToString());}
            }finally{scheduler.Suspend(true);}
        }
        [UnityTest] public IEnumerator AttentionObjectGripIsObservedButMaestroGripCancels(){
            Tutor();var scheduler=new RuleScheduler(new RoomRuleActions(editor,authoring));
            Assert.That(scheduler.Invoke(AttentionCall(ObjectFocus()),0,out var run,out var error),Is.True,error);
            try{
                scheduler.GrabTarget("book");Assert.That((string)scheduler.Invocation(run)["phase"],Is.EqualTo("running"));
                Assert.That(editor.Ownership.Covers(run,new[]{new BehaviourCatalog.Claim("book","gaze")}),Is.False);
                editor.Find("book").transform.position=new(2,1.5f,3);Physics.SyncTransforms();yield return new WaitForSeconds(.3f);
                Assert.That((float)AttentionFact()["yaw"],Is.GreaterThan(10));
                scheduler.GrabTarget("maestro");Assert.That((string)scheduler.Invocation(run)["phase"],Is.EqualTo("cancelled"));Assert.That(motion.Active,Is.False);
            }finally{scheduler.Suspend(true);}
        }
        [UnityTest] public IEnumerator AttentionMissingBoundsEndsAndDoesNotResume(){
            Tutor();var scheduler=new RuleScheduler(new RoomRuleActions(editor,authoring));
            Assert.That(scheduler.Invoke(AttentionCall(ObjectFocus()),0,out var run,out var error),Is.True,error);
            try{
                yield return new WaitForSeconds(.2f);var collider=editor.Find("book").GetComponent<BoxCollider>();collider.enabled=false;
                yield return null;yield return null;scheduler.Tick(.3f);
                Assert.That(motion.Active,Is.False);Assert.That((string)scheduler.Invocation(run)["phase"],Is.EqualTo("failed"));
                collider.enabled=true;yield return new WaitForSeconds(.2f);Assert.That(motion.Active,Is.False);Assert.That((string)AttentionFact()["focus"],Is.EqualTo("none"));
            }finally{scheduler.Suspend(true);}
        }
        [UnityTest] public IEnumerator AttentionDestroyedTargetCannotResumeAfterReplacement(){
            Tutor();var pinned=new AvatarAttentionTarget(editor,ObjectFocus());Assert.That(pinned.Read(editor,viewer.transform,out _,out var error),Is.True,error);
            UnityEngine.Object.Destroy(editor.Find("book").gameObject);yield return null;
            var replacement=new GameObject("book");replacement.transform.SetParent(root.transform,false);replacement.AddComponent<BoxCollider>();
            Assert.That(pinned.Read(editor,viewer.transform,out _,out error),Is.False);Assert.That(error,Does.Contain("unavailable"));
        }
        [UnityTest] public IEnumerator AttentionAuthoredPointMovesWithWorldAndReportsHeadLimits(){
            Tutor(true);yield return null;
            var focus=PointFocus(new Vector3(3,6,-3));var module=new AvatarAttentionCapability();var context=new CapabilityContext(editor,authoring);
            Assert.That(module.Start(context,"point",(JObject)AttentionCall(focus)["arguments"],out var operation,out var error),Is.True,error);
            try{
                yield return new WaitForSeconds(.5f);var first=AttentionFact();Assert.That((bool)first["limited"],Is.True);Assert.That((float)first["yaw"],Is.InRange(50,60));Assert.That((float)first["pitch"],Is.InRange(-30,-20));
                root.transform.SetPositionAndRotation(new Vector3(4,1,-2),Quaternion.Euler(0,65,0));root.transform.localScale=Vector3.one*.8f;
                yield return new WaitForSeconds(.4f);var second=AttentionFact();
                foreach(var axis in new[]{"x","y","z"})Assert.That((float)second["point"][axis],Is.EqualTo((float)focus["point"][axis]).Within(.001));
                Assert.That((bool)second["limited"],Is.True);Assert.That(operation.State(out error),Is.EqualTo(RuleActionState.Ready),error);
            }finally{operation.Stop(false);}
        }
        [UnityTest] public IEnumerator AttentionReducedMotionDoesNotApplyHeadOffsets(){
            Tutor();avatar.ReducedMotion=true;yield return null;
            var head=avatar.PoseRig.CanonicalBone(PoseJoint.Head);Quaternion shown=Quaternion.identity;
            var probe=avatar.gameObject.AddComponent<AvatarChannelProbe>();probe.Sample=()=>shown=head.rotation;yield return null;yield return null;var before=shown;
            var module=new AvatarAttentionCapability();Assert.That(module.Start(new CapabilityContext(editor,authoring),"reduced",(JObject)AttentionCall(PointFocus(new Vector3(3,2,2)))["arguments"],out var operation,out var error),Is.True,error);
            try{yield return new WaitForSeconds(.4f);Assert.That((bool)AttentionFact()["active"],Is.True);Assert.That((bool)AttentionFact()["applied"],Is.False);Assert.That(Quaternion.Angle(before,shown),Is.LessThan(.1f));}
            finally{operation.Stop(false);}
        }
        [UnityTest] public IEnumerator AttentionSharesUpperBodyAndTrackingLossTerminatesGaze(){
            Tutor();var scheduler=new RuleScheduler(new RoomRuleActions(editor,authoring));
            var wave=new JObject{["id"]="animation.play",["version"]=1,["arguments"]=new JObject{["target"]="maestro",["source"]=new JObject{["kind"]="gesture",["gesture"]="greeting"},["channel"]="upperBody",["seconds"]=10}};
            Assert.That(scheduler.Invoke(wave,0,out var arms,out var error),Is.True,error);
            Assert.That(scheduler.Invoke(AttentionCall(ObjectFocus()),0,out var gaze,out error),Is.True,error);
            try{
                yield return new WaitForSeconds(.3f);Assert.That(avatar.UpperBodyOwnedBy(arms),Is.True);Assert.That(motion.OwnedBy(gaze),Is.True);
                tracked=false;yield return null;yield return null;scheduler.Tick(.5f);Assert.That(motion.Active,Is.False);Assert.That(avatar.UpperBodyOwnedBy(arms),Is.True);
                tracked=true;yield return null;Assert.That(motion.Active,Is.False);
            }finally{scheduler.Suspend(true);}
        }
        [UnityTest] public IEnumerator AttentionReadinessAndFactsDoNotMutateAndResourceClaimsAreSeparate(){
            Tutor();var snapshot=JsonUtility.ToJson(editor.Snapshot());var module=new AvatarAttentionCapability();var args=(JObject)AttentionCall(ObjectFocus())["arguments"];
            Assert.That(BehaviourCatalog.TryCall(module.Id,1,args,out var call,out var error),Is.True,error);
            CollectionAssert.AreEquivalent(new[]{"maestro","book"},module.NativeEntities(args));
            Assert.That(call.Claims,Has.Length.EqualTo(1));Assert.That(call.Claims[0].Target,Is.EqualTo("maestro"));Assert.That(call.Claims[0].Channel,Is.EqualTo("gaze"));
            for(int i=0;i<3;i++){Assert.That(module.CanRun(new CapabilityContext(editor,authoring),args,out error),Is.True,error);Assert.That((bool)AttentionFact()["active"],Is.False);}
            Assert.That(JsonUtility.ToJson(editor.Snapshot()),Is.EqualTo(snapshot));Assert.That(motion.Active,Is.False);
            Assert.That(BehaviourCatalog.TryCall(module.Id,1,(JObject)AttentionCall(ObjectFocus("maestro"))["arguments"],out _,out error),Is.False);Assert.That(error,Does.Contain("itself"));
            yield return null;
        }
    }
    public sealed partial class RoomRulesTests {
        [UnityTest] public IEnumerator AttentionAdvertisesItsFeatureToConnectedClients(){
            Assert.That(RoomControls.Capabilities(editor),Does.Contain(AvatarAttentionCapability.Feature));yield return null;
        }
    }
}
