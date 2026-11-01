// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests {
    public sealed partial class RoomRulesTests {
        (string ground,string id) RecordedInputStage(){
            string ground=AvailabilityTerrain(),id=PhysicsInputBall(new(4,.35f,4));var data=editor.Read(id);
            var motion=new RoomMotion{frames=new[]{new MotionFrame{position=new(4,.13f,4),scale=data.scale},new MotionFrame{time=1,position=new(4.2f,.13f,4),scale=data.scale}}};
            Assert.That(editor.SaveAnimation(id,motion,null,false),Is.True,editor.Status);
            Assert.That(editor.RetireNativeArea(NativeAreaFor(ground),out var error),Is.True,error);return(ground,id);
        }
        RecordedMotionOperation RecordedInputOperation(string id)=>new(new CapabilityContext(editor,animations),new JObject{["target"]=id,["seconds"]=1,["loop"]=false});
        JObject RecordedInputFact(string id){Assert.That(BehaviourCatalog.TryRead("object.recording.inputs",1,new JObject{["target"]=id},new BehaviourCatalog.FactContext(editor:editor),out var fact),Is.True);return JObject.FromObject(fact.Value);}
        [UnityTest]public IEnumerator RecordedInputsProgramWaitsBeforeFirstRootPoseAndDoesNotConsumePlayback(){
            var(ground,id)=RecordedInputStage();var item=editor.Find(id);var before=item.transform.position;var operation=RecordedInputOperation(id);
            try{
                Assert.That(operation.Begin(false,out var error),Is.True,error);Assert.That(operation.State(out error),Is.EqualTo(RuleActionState.Preparing),error);
                float remaining=operation.RemainingSeconds.Value;operation.Tick();Assert.That(item.transform.position,Is.EqualTo(before));Assert.That((string)RecordedInputFact(id)["state"],Is.EqualTo("pending"));
                yield return NavigationLoadingDone();Assert.That(operation.RemainingSeconds.Value,Is.EqualTo(remaining));Assert.That(editor.Find(ground),Is.Not.Null);
                operation.Tick();Assert.That(operation.State(out error),Is.EqualTo(RuleActionState.Ready),error);Assert.That(item.transform.position.y,Is.EqualTo(.13f).Within(.001f));Assert.That((bool)RecordedInputFact(id)["ready"],Is.True);
            }finally{operation.Stop(true);}
            Assert.That((string)RecordedInputFact(id)["state"],Is.EqualTo("inactive"));
        }
        [UnityTest]public IEnumerator RecordedInputsCancelCannotPublishAnOldRootOrFinishALoad(){
            var(ground,id)=RecordedInputStage();var operation=RecordedInputOperation(id);var before=editor.Find(id).transform.position;
            Assert.That(operation.Begin(false,out var error),Is.True,error);Assert.That(operation.State(out error),Is.EqualTo(RuleActionState.Preparing));Assert.That(operation.Complete(out _),Is.False);
            operation.Stop(true);yield return NavigationLoadingDone();Assert.That(editor.Find(ground),Is.Null);Assert.That(editor.Find(id).transform.position,Is.EqualTo(before));Assert.That(editor.Find(id).GetComponent<RigidRoomItem>().AnimationOwned,Is.False);
        }
        [UnityTest]public IEnumerator RecordedInputsPreviewUsesTheSamePendingOwnerAndResumesFromPausedTime(){
            var(ground,id)=RecordedInputStage();editor.Select(editor.Find(id));animations.Play();animations.SendMessage("Update");
            Assert.That(animations.IsPlaying,Is.True);Assert.That((string)RecordedInputFact(id)["state"],Is.EqualTo("pending"));Assert.That(animations.Status,Does.Contain("Preparing"));
            yield return NavigationLoadingDone();animations.SendMessage("Update");Assert.That(editor.Find(ground),Is.Not.Null);Assert.That(animations.IsPlaying,Is.True);Assert.That(editor.Find(id).transform.position.y,Is.EqualTo(.13f).Within(.001f));Assert.That((bool)RecordedInputFact(id)["ready"],Is.True);
            animations.Stop();Assert.That((string)RecordedInputFact(id)["state"],Is.EqualTo("inactive"));
        }
        [UnityTest]public IEnumerator RecordedInputsStoppingPreviewRetiresItsPendingLoad(){
            var(ground,id)=RecordedInputStage();editor.Select(editor.Find(id));animations.Play();animations.SendMessage("Update");
            Assert.That((string)RecordedInputFact(id)["state"],Is.EqualTo("pending"));animations.Stop();yield return NavigationLoadingDone();
            Assert.That(editor.Find(ground),Is.Null);Assert.That(animations.IsPlaying,Is.False);Assert.That((string)RecordedInputFact(id)["state"],Is.EqualTo("inactive"));Assert.That(editor.Find(id).Grab.enabled,Is.True);
        }
        [UnityTest]public IEnumerator RecordedInputsCumulativeBudgetStopsWithoutApplyingOrKeepingTheLoad(){
            var(ground,id)=RecordedInputStage();float now=0;var clock=new RoomPlaybackClock(()=>now);var item=editor.Find(id);var input=RoomRecordedMotionInputs.Begin(editor,item,clock,editor.Read(id).motion,()=>true);var pose=editor.Read(id).motion.frames[0];var before=item.transform.position;
            Assert.That(input.Prepare(pose,out var error),Is.EqualTo(RuleActionState.Preparing),error);now=31;Assert.That(input.Prepare(pose,out error),Is.EqualTo(RuleActionState.Failed));Assert.That(error,Does.Contain("budget"));Assert.That(item.transform.position,Is.EqualTo(before));
            input.End();yield return NavigationLoadingDone();Assert.That(editor.Find(ground),Is.Null);
        }
        [UnityTest]public IEnumerator RecordedInputsPrepareMiddleKeysAndScaleBeforeTheFirstFrame(){
            string ground=AvailabilityTerrain(),id=PhysicsInputBall(new(4,1,4));var motion=new RoomMotion{frames=new[]{
                new MotionFrame{position=new(4,1,4),scale=.4f},new MotionFrame{time=.5f,position=new(4,.14f,4),scale=.6f},new MotionFrame{time=1,position=new(4,1,4),scale=.4f}}};
            Assert.That(editor.SaveAnimation(id,motion,null,false),Is.True,editor.Status);Assert.That(editor.RetireNativeArea(NativeAreaFor(ground),out var error),Is.True,error);
            var operation=RecordedInputOperation(id);try{
                Assert.That(operation.Begin(false,out error),Is.True,error);Assert.That(operation.State(out error),Is.EqualTo(RuleActionState.Preparing),error);
                Assert.That(RecordedInputFact(id)["dependencies"].ToObject<string[]>(),Does.Contain(ground));Assert.That(editor.Find(id).transform.position.y,Is.EqualTo(1));
                yield return NavigationLoadingDone();Assert.That(operation.State(out error),Is.EqualTo(RuleActionState.Ready),error);
            }finally{operation.Stop(true);}
        }
        [UnityTest]public IEnumerator RecordedInputsPassiveReadsCannotStartPlaybackOrLoadSavedGeometry(){
            var(ground,id)=RecordedInputStage();int revision=editor.Revision;
            for(int i=0;i<3;i++){Assert.That((string)RecordedInputFact(id)["state"],Is.EqualTo("inactive"));yield return null;}
            Assert.That(editor.NativeActivationPending,Is.False);Assert.That(editor.Find(ground),Is.Null);Assert.That(editor.Revision,Is.EqualTo(revision));Assert.That(animations.IsPlaying,Is.False);
        }
    }
}
