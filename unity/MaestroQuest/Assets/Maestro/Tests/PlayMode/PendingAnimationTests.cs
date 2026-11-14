// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.IO;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Maestro.Quest.Tests
{
    public sealed partial class RoomRulesTests
    {
        JObject AnimationTiming(string id)=>new(){["operation"]="settings",["target"]=id,["revision"]=editor.ObjectRevision(id),["duration"]=3,["loop"]=true};
        bool StartAnimation(JObject args,out CapabilityOperation operation,out string error)=>
            BehaviourCatalog.Action("animation.author").Module.Start(new CapabilityContext(editor,animations),"animation",args,out operation,out error);
        [UnityTest] public IEnumerator PendingAnimationCopiesQueuedIntentAndReturnsAcceptedMetadataAfterNewerEdit(){
            string id=editor.Identity(block);var args=AnimationTiming(id);
            Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation op=null;
            try{
                yield return Prepared(blocker);Assert.IsTrue(StartAnimation(args,out op,out error),error);
                Assert.AreEqual("completion",BehaviourCatalog.Action("animation.author").Module.Duration);
                Assert.AreEqual(RuleActionState.Preparing,op.State(out error),error);args["duration"]=8;args["loop"]=false;
                Assert.IsFalse(block.GetComponent<RigidRoomItem>().AnimationOwned);
                blocker.Dispose();yield return Drained(blocker);yield return GeometryReady(op);
                int accepted=editor.ObjectRevision(id);Assert.AreEqual(3,editor.Read(id).motion.Duration);
                // Delay the observer's Complete until another physical edit has succeeded.
                Assert.IsTrue(editor.SaveAnimation(id,null,null,false));Assert.Greater(editor.ObjectRevision(id),accepted);
                Assert.IsTrue(op.Complete(out error),error);var receipt=op.Result;
                Assert.AreEqual(accepted,(int)receipt["revision"]);Assert.AreEqual(3,(float)receipt["duration"]);Assert.IsTrue((bool)receipt["loop"]);
                receipt["frames"]=299;Assert.AreEqual(2,(int)op.Result["frames"]);Assert.IsNull(editor.Read(id).motion);
            }finally{op?.Stop(false);blocker.Dispose();}
        }
        [UnityTest] public IEnumerator PendingAnimationStaleWaiterAndCancelledWaiterCannotOverwritePhysicalEdit(){
            string id=editor.Identity(block);
            foreach(bool cancel in new[]{true,false}){
                Assert.IsTrue(editor.BeginRotation("book",Quaternion.identity,out var blocker,out var error),error);CapabilityOperation op=null;
                try{
                    yield return Prepared(blocker);Assert.IsTrue(StartAnimation(AnimationTiming(id),out op,out error),error);
                    if(cancel)op.Stop(false);blocker.Dispose();yield return Drained(blocker);
                    var motion=editor.Read(id).motion;motion.loop=!motion.loop;Assert.IsTrue(editor.SaveAnimation(id,motion,null,false));int revision=editor.ObjectRevision(id);
                    Assert.AreEqual(RuleActionState.Failed,op.State(out error));Assert.IsFalse(op.Complete(out _));Assert.AreEqual(revision,editor.ObjectRevision(id));
                    Assert.AreEqual(motion.loop,editor.Read(id).motion.loop);
                }finally{op?.Stop(false);blocker.Dispose();}
            }
        }
        [UnityTest] public IEnumerator PendingAnimationCancellationAndNewPhysicalPreviewKeepOriginalFrames(){
            string id=editor.Identity(block);
            foreach(bool preview in new[]{false,true}){
                var bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));int revision=editor.ObjectRevision(id);var motion=editor.Read(id).motion;motion.loop=!motion.loop;
                Assert.IsTrue(editor.BeginAnimation(id,revision,motion,null,false,out _,out var pending,out var error),error);
                try{
                    yield return Prepared(pending);
                    if(preview){editor.Select(block);animations.Play();Assert.IsTrue(animations.IsPlaying);Assert.IsFalse(pending.Poll(out _,out _));Assert.IsTrue(animations.IsPlaying);}
                    else pending.Dispose();
                    Assert.AreEqual(revision,editor.ObjectRevision(id));Assert.AreNotEqual(motion.loop,editor.Read(id).motion.loop);
                    Assert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));
                }finally{pending.Dispose();animations.Stop();}
                yield return Drained(pending);
            }
        }
        [UnityTest] public IEnumerator PendingAnimationContentAndUndoKeepCurrentPlacementAndMomentum(){
            string id=editor.Identity(block);
            Assert.IsTrue(editor.ConfigurePhysics(id,editor.ObjectRevision(id),new ObjectPhysicsSettings{mode="solid",shape="box",mass=.5f},out var error),error);
            physics.SetSurfaces(true,"Synthetic animation continuity");physics.StartPhysics();var body=block.GetComponent<Rigidbody>();
            var velocity=new Vector3(.2f,.1f,0);var spin=new Vector3(0,.3f,0);
            var motion=editor.Read(id).motion;motion.loop=true;
            Assert.IsTrue(editor.BeginAnimation(id,editor.ObjectRevision(id),motion,null,false,out _,out var pending,out error),error);
            try{
                Assert.IsFalse(block.GetComponent<RigidRoomItem>().AnimationOwned);yield return Prepared(pending);
                block.transform.localPosition+=Vector3.up*.2f;var live=block.transform.localPosition;body.linearVelocity=velocity;body.angularVelocity=spin;
                Assert.IsTrue(pending.Poll(out _,out error),error);Assert.IsTrue(pending.Accepted);
                Assert.AreEqual(live,block.transform.localPosition);Assert.AreEqual(velocity,body.linearVelocity);Assert.AreEqual(spin,body.angularVelocity);
                yield return Completed(pending);block.transform.localPosition+=Vector3.right*.1f;live=block.transform.localPosition;
                editor.Undo();Assert.AreEqual(live,block.transform.localPosition);Assert.IsFalse(editor.Read(id).motion.loop);
            }finally{pending.Dispose();}yield return Drained(pending);
        }
        [UnityTest] public IEnumerator PendingAnimationStopAfterAcceptancePublishesWithoutObserver(){
            string id=editor.Identity(block);var motion=editor.Read(id).motion;motion.loop=true;using var pause=new PublicationPause();
            Assert.IsTrue(editor.BeginAnimation(id,editor.ObjectRevision(id),motion,null,false,out _,out var pending,out var error,pause.Check),error);
            try{
                yield return Prepared(pending);Assert.IsTrue(pending.Poll(out var ready,out error),error);Assert.IsFalse(ready);Assert.IsTrue(pending.Accepted);
                pending.Dispose();pause.Release();yield return Drained(pending);Assert.IsTrue(pending.Published);Assert.IsFalse(pending.Complete(out _));
                Assert.IsTrue(new RoomStorage(directory).Load(out error).objects.Single(x=>x.id==id).motion.loop,error);editor.Undo();Assert.IsFalse(editor.Read(id).motion.loop);
            }finally{pause.Release();pending.Dispose();}
        }
        [UnityTest] public IEnumerator PendingAnimationPublicationFailureKeepsLiveEditUnconfirmedAndSavedBytes(){
            string id=editor.Identity(block);var motion=editor.Read(id).motion;motion.loop=true;var bytes=File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName));using var pause=new PublicationPause();
            Assert.IsTrue(editor.BeginAnimation(id,editor.ObjectRevision(id),motion,null,false,out _,out var pending,out var error,phase=>{if(phase=="publishing"){pause.Check(phase);throw new IOException("Injected animation publication failure");}}),error);
            try{
                yield return Prepared(pending);Assert.IsTrue(pending.Poll(out _,out error),error);Assert.IsTrue(pending.Accepted);pause.Release();yield return Rejected(pending);
                Assert.IsTrue(editor.Read(id).motion.loop);Assert.AreEqual(bytes,File.ReadAllBytes(Path.Combine(directory,RoomStorage.FileName)));
                Assert.IsFalse(editor.SaveAnimation(id,null,null,false));Assert.IsFalse(pending.Complete(out _));
            }finally{pause.Release();pending.Dispose();}yield return Drained(pending);
        }
    }
}
