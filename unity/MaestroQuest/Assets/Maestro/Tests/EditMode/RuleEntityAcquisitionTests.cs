// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Linq;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class RuleEntityAcquisitionTests
    {
        sealed class Demand:RuleEntityDemand
        {
            internal bool Ready,Failed;internal int Releases;
            internal override RuleActionState State(out string error){error=Failed?"Missing native content":null;return Failed?RuleActionState.Failed:Ready?RuleActionState.Ready:RuleActionState.Preparing;}
            public override void Dispose(){Releases++;}
        }
        sealed class Actions:IRuleActions,IRuleEntityAcquisition,IRuleCompletion
        {
            internal readonly Demand Demand=new();internal int Starts,Checks,Completions,Acquisitions;internal bool FailPreflight,FailStart;internal Func<bool> Admission;
            public bool CanRun(CapabilityCall call,out string error){Checks++;Assert.That(Demand.Ready,Is.True,"Preflight requires acquired native entities");error=FailPreflight?"Target no longer usable":null;return !FailPreflight;}
            public bool Start(string id,CapabilityCall call,out float seconds,out string error){Starts++;seconds=call.Instant?0:2;error=FailStart?"Effect rejected":null;return !FailStart;}
            RuleEntityDemand IRuleEntityAcquisition.Acquire(CapabilityCall call,Func<bool> admission){Acquisitions++;Admission=admission;Assert.That(admission(),Is.True);return Demand;}
            public bool Complete(string id,out string error){Completions++;error=null;return true;}
            public void Stop(string id,bool preserve){}
        }
        static JObject Move()=>new(){["id"]="object.position.set",["version"]=1,["arguments"]=new JObject{["target"]=new string('a',32),["x"]=0,["y"]=1,["z"]=0}};
        static JObject Motion()=>new(){["id"]="animation.play",["version"]=1,["arguments"]=new JObject{["target"]=new string('a',32),["source"]=new JObject{["kind"]="recording"},["channel"]="wholeTarget",["seconds"]=2,["loop"]=false}};
        [Test] public void InstantActionWaitsForEntitiesThenStartsAndCompletesExactlyOnce()
        {
            var actions=new Actions();var scheduler=new RuleScheduler(actions);Assert.That(scheduler.Invoke(Move(),0,out var id,out var error),Is.True,error);
            Assert.That(actions.Checks,Is.Zero);Assert.That(actions.Starts,Is.Zero);Assert.That(scheduler.PreparingCount,Is.EqualTo(1));
            Assert.That((string)scheduler.Invocation(id)["status"],Is.EqualTo("Loading required objects"));scheduler.Tick(20);Assert.That(actions.Starts,Is.Zero);
            actions.Demand.Ready=true;scheduler.Tick(21);scheduler.Tick(22);
            Assert.That((string)scheduler.Invocation(id)["phase"],Is.EqualTo("completed"));Assert.That(actions.Starts,Is.EqualTo(1));Assert.That(actions.Completions,Is.EqualTo(1));Assert.That(actions.Acquisitions,Is.EqualTo(1));Assert.That(actions.Demand.Releases,Is.EqualTo(1));Assert.That(actions.Admission(),Is.False);
        }
        [TestCase("cancel")] [TestCase("pause")] [TestCase("timeout")] [TestCase("failure")]
        public void PendingAcquisitionCannotRunAnEffectAfterTermination(string reason)
        {
            var actions=new Actions();var scheduler=new RuleScheduler(actions);scheduler.Invoke(Move(),0,out var id,out _);
            switch(reason){case "cancel":scheduler.CancelInvocation(id,out _);break;case "pause":scheduler.Suspend(true);scheduler.Suspend(false);break;case "timeout":scheduler.Tick(30);break;default:actions.Demand.Failed=true;scheduler.Tick(1);break;}
            Assert.That((string)scheduler.Invocation(id)["phase"],Is.EqualTo(reason is "cancel" or "pause"?"cancelled":"failed"));Assert.That(actions.Demand.Releases,Is.EqualTo(1));Assert.That(actions.Admission(),Is.False);
            actions.Demand.Ready=true;actions.Demand.Failed=false;scheduler.Tick(31);Assert.That(actions.Starts,Is.Zero);Assert.That(actions.Completions,Is.Zero);
        }
        [Test] public void RequestedPlaybackDurationStartsAfterEntityAcquisition()
        {
            var actions=new Actions();var scheduler=new RuleScheduler(actions);Assert.That(scheduler.Invoke(Motion(),0,out var id,out var error),Is.True,error);
            actions.Demand.Ready=true;scheduler.Tick(20);Assert.That(actions.Starts,Is.EqualTo(1));scheduler.Tick(21.9f);Assert.That(actions.Completions,Is.Zero);
            scheduler.Tick(22);Assert.That(actions.Completions,Is.EqualTo(1));Assert.That(actions.Demand.Releases,Is.EqualTo(1));
        }
        [TestCase(false)] [TestCase(true)] public void AdmissionIsRevalidatedAndFailedEffectsReleaseAcquisition(bool start)
        {
            var actions=new Actions{FailPreflight=!start,FailStart=start};var scheduler=new RuleScheduler(actions);scheduler.Invoke(Move(),0,out var id,out _);actions.Demand.Ready=true;scheduler.Tick(1);
            Assert.That((string)scheduler.Invocation(id)["phase"],Is.EqualTo("failed"));Assert.That(actions.Starts,Is.EqualTo(start?1:0));Assert.That(actions.Demand.Releases,Is.EqualTo(1));
        }
        [TestCase(2)] [TestCase(3)] public void SavedProgramsUseTheSameAcquisitionAndCancellation(int version)
        {
            var program=JObject.Parse(BehaviourProgram.FromInvocation(Move()));program["version"]=version;if(version==3){program["state"]=new JArray();program["events"]=new JArray();}
            var sequence=new RuleSequence{id=Guid.NewGuid().ToString("N"),name="Move saved object",program=program.ToString()};var actions=new Actions();var scheduler=new RuleScheduler(actions);scheduler.Configure(new RuleDocument{sequences=new[]{sequence}});
            Assert.That(scheduler.Trigger(sequence.id,0),Is.True,scheduler.LastError);Assert.That(scheduler.ObserveRuns().Single().status,Is.EqualTo("Loading required objects"));
            Assert.That(scheduler.StopSequence(sequence.id),Is.True);actions.Demand.Ready=true;scheduler.Tick(1);Assert.That(actions.Starts,Is.Zero);Assert.That(actions.Demand.Releases,Is.EqualTo(1));
        }
        [Test] public void ExplicitNativeDependenciesIncludeUnownedCatchProjectileAndSavedOnlyModulesStayEmpty()
        {
            var catcher=new CatchObjectCapability();var args=catcher.Example;var target=(string)args["target"];var holder=(string)args["holder"]["objectId"];
            Assert.That(catcher.NativeEntities(args),Is.EquivalentTo(new[]{target,holder}));Assert.That(catcher.Claims(args).Any(claim=>claim.Target==target),Is.False,"Action ownership is not a loading dependency list");
            Assert.That(new ModelLibraryCapability().NativeEntities(new JObject()),Is.Empty);
            Assert.That(new RegionAssignCapability().NativeEntities(new JObject()),Is.Empty);
        }
    }
}
