// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class RoomOwnershipTests
    {
        static BehaviourCatalog.Claim[] Claims(params string[] channels)=>channels.Select(x=>new BehaviourCatalog.Claim("maestro",x)).ToArray();
        static RoomOwnership.Lease Own(RoomOwnership room,string id,RoomActorRole role,string channel,Action<RoomOwnership.Interruption> stop=null,bool allowsGrab=false) {
            Assert.That(room.TryAcquire(id,id,role,Claims(channel),stop,out var lease,out var error,allowsGrab:allowsGrab),Is.True,error);return lease;
        }
        [Test] public void MultiChannelTakeoverIsAtomicAndEqualProgramsCannotSteal() {
            var room=new RoomOwnership();int cancelled=0;
            var ambient=Own(room,"ambient",RoomActorRole.Ambient,"gaze",_=>cancelled++);
            var hand=Own(room,"hand",RoomActorRole.Grab,"locomotion");
            Assert.That(room.TryAcquire("program","Program",RoomActorRole.Program,Claims("gaze","locomotion"),null,out _,out _),Is.False);
            Assert.That(ambient.Held,Is.True);Assert.That(hand.Held,Is.True);Assert.That(cancelled,Is.Zero);
            hand.Dispose();var program=Own(room,"program",RoomActorRole.Program,"gaze");
            Assert.That(cancelled,Is.EqualTo(1));Assert.That(ambient.Held,Is.False);
            Assert.That(room.TryAcquire("other","Other",RoomActorRole.Program,Claims("gaze"),null,out _,out _),Is.False);
            Assert.That(program.Held,Is.True);
        }
        [Test] public void IndependentChannelsContinueWhileReflexAndControlsPreemptOnlyTheirClaims() {
            var room=new RoomOwnership();int stopped=0;
            var arms=Own(room,"wave",RoomActorRole.Program,"upperBody");
            var walk=Own(room,"walk",RoomActorRole.Program,"locomotion",n=>{stopped++;Assert.That(n.Label,Is.EqualTo("reflex"));});
            var reflex=Own(room,"reflex",RoomActorRole.Reflex,"locomotion");
            Assert.That(walk.Held,Is.False);Assert.That(arms.Held,Is.True);Assert.That(stopped,Is.EqualTo(1));
            var control=Own(room,"stick",RoomActorRole.Control,"locomotion");Assert.That(reflex.Held,Is.False);
            control.Dispose();Assert.That(walk.Held,Is.False,"Releasing a higher owner never restarts old work");Assert.That(arms.Held,Is.True);
        }
        [Test] public void ExplicitControlReplacementAndStaleTokensCannotRemoveNewOwner() {
            var room=new RoomOwnership();var old=Own(room,"direct",RoomActorRole.Control,"gaze");
            Assert.That(room.TryAcquire("next","Next control",RoomActorRole.Control,Claims("gaze"),null,out _,out _),Is.False);
            Assert.That(room.TryAcquire("next","Next control",RoomActorRole.Control,Claims("gaze"),null,out var next,out _,replaceControl:true),Is.True);
            old.Dispose();next.Dispose();var replacement=Own(room,"direct",RoomActorRole.Control,"gaze");old.Dispose();
            Assert.That(replacement.Held,Is.True);Assert.That(room.Covers("direct",Claims("gaze")),Is.True);
        }
        [Test] public void RecordingCanCooperateWithGripsWithoutAllowingProgramsOrPreviewToWrite() {
            var room=new RoomOwnership();var recording=Own(room,"record",RoomActorRole.Control,"wholeTarget",allowsGrab:true);
            var grip=Own(room,"hand",RoomActorRole.Grab,"wholeTarget");Assert.That(recording.Held,Is.True);
            Assert.That(recording.SetAllowsGrab(false),Is.False,"Cannot switch into playback while the hand is driving the object");
            Assert.That(room.TryAcquire("program","Program",RoomActorRole.Program,Claims("upperBody"),null,out _,out _),Is.False);
            Assert.That(room.TryAcquire("preview","Preview",RoomActorRole.Control,Claims("wholeTarget"),null,out _,out _,replaceControl:true),Is.False);
            grip.Dispose();Assert.That(recording.SetAllowsGrab(false),Is.True);
            Own(room,"hand",RoomActorRole.Grab,"wholeTarget");Assert.That(recording.Held,Is.False);
        }
        [Test] public void TakeoverCallbacksObserveCompleteReservationAndCannotReenterAcquisition() {
            var room=new RoomOwnership();int stopped=0;
            Own(room,"program",RoomActorRole.Program,"gaze",notice=>{
                stopped++;Assert.That(notice.PreservePlacement,Is.True);Assert.That(room.Covers("hand",Claims("gaze")),Is.True);
                Assert.That(room.TryAcquire("reentry","Reentry",RoomActorRole.Control,Claims("upperBody"),null,out _,out _),Is.False);
            });
            Assert.That(room.TryAcquire("hand","Your grip",RoomActorRole.Grab,Claims("wholeTarget"),null,out var hand,out _,preservePlacement:true),Is.True);
            Assert.That(stopped,Is.EqualTo(1));Assert.That(hand.Held,Is.True);
        }
        [Test] public void PauseRevokesEveryLeaseAndResumeDoesNotReplayActors() {
            var room=new RoomOwnership();int stopped=0;
            Own(room,"one",RoomActorRole.Program,"gaze",n=>{stopped++;Assert.That(n.PreservePlacement,Is.False);room.Suspend(false);Assert.That(room.TryAcquire("restart","Restart",RoomActorRole.Program,Claims("gaze"),null,out _,out _),Is.False);});
            Own(room,"two",RoomActorRole.Program,"upperBody",_=>stopped++);room.Suspend(true);room.Suspend(true);
            Assert.That(stopped,Is.EqualTo(2));Assert.That(room.Suspended,Is.True);Assert.That(room.Observe().owners,Is.Empty);
            Assert.That(room.TryAcquire("later","Later",RoomActorRole.Program,Claims("gaze"),null,out _,out _),Is.False);
            room.Suspend(false);Assert.That(room.Observe().owners,Is.Empty);Own(room,"later",RoomActorRole.Program,"gaze");
        }
        [Test] public void FailedCleanupStillNotifiesOtherActorsAndPreventsFurtherEffects() {
            var room=new RoomOwnership();int stopped=0;
            Own(room,"broken",RoomActorRole.Program,"gaze",_=>throw new InvalidOperationException("test cleanup"));
            Own(room,"other",RoomActorRole.Program,"upperBody",_=>stopped++);
            Assert.That(room.TryAcquire("hand","Your grip",RoomActorRole.Grab,Claims("wholeTarget"),null,out _,out var error),Is.False);
            Assert.That(stopped,Is.EqualTo(1));Assert.That(error,Does.Contain("cleanup"));Assert.That(room.Observe().owners,Is.Empty);
            room.Suspend(false);Assert.That(room.TryAcquire("next","Next",RoomActorRole.Program,Claims("gaze"),null,out _,out _),Is.False);
        }
        sealed class BrokenCleanup : Maestro.Quest.Rules.IRuleActions {
            public bool CanRun(CapabilityCall call,out string error){error=null;return true;}
            public bool Start(string id,CapabilityCall call,out float seconds,out string error){seconds=1;error=null;return true;}
            public void Stop(string id,bool preserve)=>throw new InvalidOperationException("test operation cleanup");
        }
        [Test] public void SchedulerRemovesInterruptedRunEvenWhenItsNativeCleanupFails() {
            var scheduler=new Maestro.Quest.Rules.RuleScheduler(new BrokenCleanup());
            var call=Newtonsoft.Json.Linq.JObject.Parse("{\"id\":\"avatar.look.user\",\"version\":1,\"arguments\":{\"target\":\"maestro\",\"seconds\":1}}");
            Assert.That(scheduler.Invoke(call,0,out var run,out var error),Is.True,error);
            Assert.That(scheduler.Ownership.TryAcquire("hand","Your grip",RoomActorRole.Grab,Claims("wholeTarget"),null,out _,out _),Is.False);
            Assert.That(scheduler.RunningCount,Is.Zero);Assert.That((string)scheduler.Invocation(run)["phase"],Is.EqualTo("cancelled"));
            scheduler.Tick(10);Assert.That(scheduler.RunningCount,Is.Zero);Assert.That(scheduler.Ownership.Observe().owners,Is.Empty);
        }
        [Test] public void ObservationIsDetachedAndCapacityAllowsReplacingExistingOwners() {
            var room=new RoomOwnership();
            for(int i=0;i<RoomOwnership.MaximumOwners;i++)Own(room,"p"+i,RoomActorRole.Program,"channel"+i);
            var view=room.Observe();view.owners[0].claims[0].channel="changed";
            Assert.That(room.Covers("p0",Claims("channel0")),Is.True);
            var grip=Own(room,"hand",RoomActorRole.Grab,"wholeTarget");Assert.That(room.Observe().owners.Length,Is.EqualTo(1));Assert.That(grip.Held,Is.True);
        }
    }
}
