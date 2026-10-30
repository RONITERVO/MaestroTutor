// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Interaction;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using NUnit.Framework;
namespace Maestro.Quest.Tests {
    public sealed partial class RuleSchedulerTests {
        sealed class ClockActions:IRuleActions,IRuleReadiness,IRuleTiming,IRuleCompletion {
            internal float Now;internal readonly RoomPlaybackClock Clock;internal int Finishes,Stops;
            internal ClockActions(){Clock=new RoomPlaybackClock(()=>Now);}
            public bool CanRun(CapabilityCall c,out string e){e=null;return true;}
            public bool Start(string id,CapabilityCall c,out float seconds,out string e){Clock.Start();seconds=1;e=null;return true;}
            public RuleActionState State(string id,out string e)=>!Clock.Valid(out e)?RuleActionState.Failed:Clock.Paused?RuleActionState.Preparing:RuleActionState.Ready;
            public float? RemainingSeconds(string id)=>Clock.Remaining(1);
            public bool Complete(string id,out string e){Finishes++;e=null;return true;}
            public void Stop(string id,bool preserve){Stops++;}
        }
        [Test] public void NativePlaybackWaitsMidActionWithoutRestartingElapsedTimeOrCompletingEarly(){
            var a=new ClockActions();var s=new RuleScheduler(a);Assert.That(s.Invoke(InvocationCall(),0,out var id,out _),Is.True);
            a.Now=.4f;s.Tick(a.Now);a.Clock.Pause("prop",true);a.Now=8;s.Tick(a.Now);
            Assert.That(s.PreparingCount,Is.EqualTo(1));Assert.That(a.Finishes,Is.Zero);Assert.That((string)s.Invocation(id)["phase"],Is.EqualTo("preparing"));
            a.Clock.Pause("prop",false);s.Tick(a.Now);a.Now=8.59f;s.Tick(a.Now);Assert.That(a.Finishes,Is.Zero);
            a.Now=8.61f;s.Tick(a.Now);Assert.That(a.Finishes,Is.EqualTo(1));Assert.That((string)s.Invocation(id)["phase"],Is.EqualTo("completed"));s.Tick(100);Assert.That(a.Finishes,Is.EqualTo(1));
        }
        [Test] public void NativePlaybackMultipleOwnersAndRepeatedWaitsShareOneBudget(){
            var a=new ClockActions();var s=new RuleScheduler(a);Assert.That(s.Invoke(InvocationCall(),0,out var id,out _),Is.True);
            a.Now=.2f;a.Clock.Pause("prop",true);a.Clock.Pause("root",true);a.Now=15;a.Clock.Pause("prop",false);s.Tick(a.Now);
            Assert.That(a.Clock.Paused,Is.True);a.Clock.Pause("root",false);a.Now=15.1f;s.Tick(a.Now);a.Clock.Pause("prop",true);
            a.Now=30.31f;s.Tick(a.Now);Assert.That((string)s.Invocation(id)["phase"],Is.EqualTo("failed"));Assert.That(s.LastError,Does.Contain("budget"));Assert.That(a.Finishes,Is.Zero);Assert.That(a.Stops,Is.EqualTo(1));
        }
        [Test] public void NativePlaybackCancelCannotResumeOnLatePreparation(){
            var a=new ClockActions();var s=new RuleScheduler(a);Assert.That(s.Invoke(InvocationCall(),0,out var id,out _),Is.True);
            a.Clock.Pause("prop",true);s.Tick(2);Assert.That(s.CancelInvocation(id,out _),Is.True);a.Now=4;a.Clock.Pause("prop",false);a.Now=8;s.Tick(a.Now);
            Assert.That(a.Finishes,Is.Zero);Assert.That(a.Stops,Is.EqualTo(1));Assert.That((string)s.Invocation(id)["phase"],Is.EqualTo("cancelled"));
        }
    }
}
