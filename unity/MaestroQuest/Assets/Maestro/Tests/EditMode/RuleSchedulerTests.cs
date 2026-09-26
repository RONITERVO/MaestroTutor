// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Maestro.Quest.Rules;
using NUnit.Framework;
using UnityEngine;

namespace Maestro.Quest.Tests
{
    public sealed class RuleSchedulerTests
    {
        sealed class Actions : IRuleActions
        {
            public readonly List<string> Started = new();
            public readonly HashSet<string> Active = new();
            public int Stopped;
            public bool CanRun(RuleStep step,out string error) { error = null; return true; }
            public bool Start(string id,RuleStep step,out float seconds,out string error) { Active.Add(id); Started.Add(step.targetId); seconds = step.seconds == 0 ? 2 : step.seconds; error = null; return true; }
            public void Stop(string id,bool preserve) { if (Active.Remove(id)) Stopped++; }
        }
        sealed class PreparingActions : IRuleActions,IRuleReadiness
        {
            public RuleActionState Phase = RuleActionState.Preparing;
            public int Polls,Stops,Starts;
            public bool CanRun(RuleStep step,out string error) { error = null; return true; }
            public bool Start(string id,RuleStep step,out float seconds,out string error) { Starts++; seconds = 2; error = null; return true; }
            public void Stop(string id,bool preserve) { Stops++; }
            public RuleActionState State(string id,out string error) { Polls++; error = Phase == RuleActionState.Failed ? "Missing saved motion" : null; return Phase; }
        }
        [Test] public void PreparationReservesTheTargetButStartsDurationOnlyWhenReadyAndNeverResumesAfterCancellation()
        {
            var sequence = Sequence(); var other = Sequence(); other.interruption = RuleInterruption.Ignore;
            var actions = new PreparingActions(); var scheduler = new RuleScheduler(actions);
            var binding = Binding(sequence,RuleEventKind.Speaking); binding.stopOnExit = true;
            scheduler.Configure(new RuleDocument { sequences = new[] { sequence,other },bindings = new[] { binding } });
            Assert.That(scheduler.Trigger(sequence.id,0),Is.True); scheduler.Tick(4); Assert.That(scheduler.PreparingCount,Is.EqualTo(1));
            Assert.That(scheduler.Trigger(other.id,4),Is.False,"Loading reserves the same target");
            actions.Phase = RuleActionState.Ready; scheduler.Tick(5); scheduler.Tick(6.99f); Assert.That(scheduler.RunningCount,Is.EqualTo(1));
            scheduler.Tick(7.01f); Assert.That(scheduler.RunningCount,Is.Zero); Assert.That(actions.Stops,Is.EqualTo(1));
            actions.Phase = RuleActionState.Preparing; scheduler.Trigger(sequence.id,10); int polls = actions.Polls; scheduler.Tick(40);
            Assert.That(actions.Polls,Is.EqualTo(polls),"An expired load must not start before it is cancelled"); Assert.That(scheduler.RunningCount,Is.Zero);
            scheduler.SetActivity("idle",41); scheduler.SetActivity("speaking",42); Assert.That(scheduler.PreparingCount,Is.EqualTo(1));
            scheduler.SetActivity("idle",43); actions.Phase = RuleActionState.Ready; scheduler.Tick(44); Assert.That(scheduler.RunningCount,Is.Zero);
            actions.Phase = RuleActionState.Preparing; scheduler.Trigger(sequence.id,45); scheduler.Suspend(true); scheduler.Suspend(false); actions.Phase = RuleActionState.Ready; scheduler.Tick(46); Assert.That(scheduler.RunningCount,Is.Zero);
            actions.Phase = RuleActionState.Failed; Assert.That(scheduler.Trigger(sequence.id,50),Is.False); Assert.That(scheduler.LastError,Does.Contain("Missing"));
        }
        static RuleSequence Sequence(string target = "maestro") => new() { id = Guid.NewGuid().ToString("N"), name = "Test action", steps = new[] { new RuleStep { action = RuleActionKind.RecordedAnimation, targetId = target, seconds = 2 } } };
        static RuleBinding Binding(RuleSequence sequence,RuleEventKind kind) => new() { id = Guid.NewGuid().ToString("N"), sequenceId = sequence.id, trigger = kind, sourceId = "book" };
        [Test] public void SharedSequenceRespondsToManualStateAndObjectTriggersWithoutRepeatedSnapshotFiring()
        {
            var action = Sequence(); var fake = new Actions(); var scheduler = new RuleScheduler(fake);
            var tap = Binding(action,RuleEventKind.ItemTapped);
            scheduler.Configure(new RuleDocument { sequences = new[] { action },bindings = new[] { Binding(action,RuleEventKind.Speaking),tap } });
            scheduler.SetActivity("idle",0); scheduler.SetActivity("speaking",1); scheduler.SetActivity("speaking",1.1f);
            Assert.That(fake.Started.Count,Is.EqualTo(1));
            scheduler.Emit(RuleEventKind.ItemTapped,"maestro",2); Assert.That(fake.Started.Count,Is.EqualTo(1));
            scheduler.Emit(RuleEventKind.ItemTapped,"book",2); Assert.That(fake.Started.Count,Is.EqualTo(2));
            scheduler.Emit(RuleEventKind.ItemTapped,"book",2.1f); Assert.That(fake.Started.Count,Is.EqualTo(2),"Cooldown prevents repeated contact");
            Assert.That(scheduler.Trigger(action.id,2.2f),Is.True); Assert.That(fake.Started.Count,Is.EqualTo(3)); Assert.That(scheduler.RunningCount,Is.EqualTo(1));
        }
        [Test] public void QueueKeepsOneLatestRequestAndDisjointTargetsRunTogether()
        {
            var first = Sequence(); var queued = Sequence(); queued.interruption = RuleInterruption.QueueLatest;
            var other = Sequence("book"); var fake = new Actions(); var scheduler = new RuleScheduler(fake);
            scheduler.Configure(new RuleDocument { sequences = new[] { first,queued,other } });
            scheduler.Trigger(first.id,0); scheduler.Trigger(other.id,0);
            for (int i = 0; i < 20; i++) scheduler.Trigger(queued.id,.1f);
            Assert.That(scheduler.RunningCount,Is.EqualTo(2)); Assert.That(scheduler.QueuedCount,Is.EqualTo(1));
            scheduler.Tick(2.1f); Assert.That(scheduler.RunningCount,Is.EqualTo(1)); Assert.That(scheduler.QueuedCount,Is.Zero); Assert.That(fake.Started.Count,Is.EqualTo(3));
            scheduler.Suspend(true); Assert.That(fake.Active,Is.Empty); Assert.That(scheduler.Trigger(first.id,3),Is.False);
            scheduler.Suspend(false); scheduler.Tick(10); Assert.That(fake.Active,Is.Empty,"Resume does not replay interrupted work");
        }
        [Test] public void StateExitCancelsRunningAndQueuedWhileRulesAndIgnoreProtectsCurrentAction()
        {
            var first = Sequence(); first.repeat = true; first.interruption = RuleInterruption.Ignore;
            var second = Sequence(); second.interruption = RuleInterruption.QueueLatest;
            var a = Binding(first,RuleEventKind.Speaking); a.stopOnExit = true;
            var b = Binding(second,RuleEventKind.Speaking); b.stopOnExit = true;
            var fake = new Actions(); var scheduler = new RuleScheduler(fake);
            scheduler.Configure(new RuleDocument { sequences = new[] { first,second },bindings = new[] { a,b } });
            scheduler.SetActivity("idle",0); scheduler.SetActivity("speaking",1);
            Assert.That(scheduler.Trigger(first.id,1.1f),Is.False); Assert.That(scheduler.RunningCount,Is.EqualTo(1)); Assert.That(scheduler.QueuedCount,Is.EqualTo(1));
            scheduler.SetActivity("listening",1.2f); Assert.That(fake.Active,Is.Empty); Assert.That(scheduler.QueuedCount,Is.Zero);
            scheduler.Tick(100); Assert.That(fake.Active,Is.Empty);
            scheduler.SetActivity("speaking",102); Assert.That(scheduler.RunningCount,Is.EqualTo(1));
            scheduler.ForgetActivity(); Assert.That(fake.Active,Is.Empty); Assert.That(scheduler.QueuedCount,Is.Zero);
            scheduler.SetActivity("speaking",104); Assert.That(fake.Active,Is.Empty,"Browser resume is a baseline, not a fresh speaking event");
            scheduler.Trigger(first.id,105); scheduler.ForgetActivity(); Assert.That(scheduler.RunningCount,Is.EqualTo(1),"A manual action does not depend on browser activity");
        }
        [Test] public void SequentialActionsAdvanceOncePerTickAndEditingCanStopAnEntireSequence()
        {
            var sequence = Sequence(); sequence.repeat = true; sequence.steps = new[] { new RuleStep { action = RuleActionKind.RecordedAnimation,targetId = "book",seconds = 1 },new RuleStep { action = RuleActionKind.RecordedAnimation,targetId = "maestro",seconds = 1 } };
            var fake = new Actions(); var scheduler = new RuleScheduler(fake); scheduler.Configure(new RuleDocument { sequences = new[] { sequence } });
            scheduler.Trigger(sequence.id,0); scheduler.Tick(1000); Assert.That(fake.Started.Count,Is.EqualTo(2),"No catch-up loop after a stalled frame");
            Assert.That(fake.Started[1],Is.EqualTo("maestro")); scheduler.StopTarget("book",true); Assert.That(fake.Active,Is.Empty);
        }
        [Test] public void ValidatesReferencesAndBoundsAndRecoversRulesButtonsAndIndependentCopies()
        {
            string directory = Path.Combine(Path.GetTempPath(),"MaestroRules-"+Guid.NewGuid().ToString("N"));
            try
            {
                var sequence = Sequence(); var document = new RuleDocument { sequences = new[] { sequence },bindings = new[] { Binding(sequence,RuleEventKind.Speaking) },buttons = new[] { new RuleButtonData { id = Guid.NewGuid().ToString("N"),sequenceId = sequence.id,mount = ButtonMount.LeftController,position = new Vector3(-.12f,.08f,.06f) } } };
                Assert.That(document.Validate(out _),Is.True); var copy = document.Copy(); copy.sequences[0].steps[0].seconds = 9; Assert.That(document.sequences[0].steps[0].seconds,Is.EqualTo(2));
                var storage = new RuleStorage(directory); Assert.That(storage.Save(document,out _),Is.True); Assert.That(storage.Save(copy,out _),Is.True);
                File.WriteAllText(Path.Combine(directory,"rules.v2.json"),"broken");
                var recovered = storage.Load(out var message); StringAssert.Contains("backup",message); Assert.That(recovered.buttons[0].position,Is.EqualTo(document.buttons[0].position));
                Assert.That(recovered.sequences[0].steps[0].seconds,Is.EqualTo(2));
                copy.buttons[0].position = Vector3.one; Assert.That(copy.Validate(out _),Is.False);
                copy = document.Copy(); copy.bindings[0].sequenceId = Guid.NewGuid().ToString("N"); Assert.That(copy.Validate(out _),Is.False);
                copy = document.Copy(); copy.sequences[0].steps[0].seconds = float.NaN; Assert.That(copy.Validate(out _),Is.False);
                copy = document.Copy(); copy.bindings[0].trigger = (RuleEventKind)500; Assert.That(copy.Validate(out _),Is.False);
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory,true); }
        }
    }
}
