// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Linq;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Maestro.Quest.Creation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class EventProgramTests
    {
        sealed class Actions:IRuleActions,IRuleReadiness
        {
            public int Starts,Stops;public RuleActionState Phase=RuleActionState.Ready;
            public bool CanRun(CapabilityCall step,out string error){error=null;return true;}
            public bool Start(string id,CapabilityCall invocation,out float seconds,out string error){ invocation.TryStep(out var step,out _);Starts++;seconds=1;error=null;return true;}
            public void Stop(string id,bool placement){Stops++;}
            public RuleActionState State(string id,out string error){error=null;return Phase;}
        }
        static JObject Source()=>JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-events.json")));
        static JArray Loop(JObject p)=>(JArray)p["functions"][0]["body"][0]["body"];
        static RuleSequence Sequence(JObject source=null)=>new() {id=Guid.NewGuid().ToString("N"),name="Reactive wave",program=(source??Source()).ToString(Newtonsoft.Json.Formatting.None)};
        static RuleScheduler Scheduler(Actions actions,params RuleSequence[] sequences){var result=new RuleScheduler(actions);result.Configure(new RuleDocument {sequences=sequences});return result;}
        static string State(RuleScheduler s,string name)=>s.ObserveRuns().Single().state.Single(x=>x.name==name).value;
        [Test] public void EventsPreserveStateReleaseIdleResourcesAndNeverReplay()
        {
            var actions=new Actions();var sequence=Sequence();var s=Scheduler(actions,sequence);
            s.Emit(RuleEventKind.ItemTapped,"book",0);
            Assert.That(s.Trigger(sequence.id,1),Is.True);Assert.That(s.TargetsBusy(new[]{"maestro"}),Is.False);
            s.Tick(2);Assert.That(actions.Starts,Is.Zero,"An event before subscription is never replayed");
            s.Emit(RuleEventKind.ItemTapped,"maestro",2);s.Tick(2);Assert.That(actions.Starts,Is.Zero,"Source filter");
            s.Emit(RuleEventKind.ItemTapped,"book",3);s.Tick(3);
            Assert.That(State(s,"count"),Is.EqualTo("1"));Assert.That(actions.Starts,Is.EqualTo(1));Assert.That(s.TargetsBusy(new[]{"maestro"}),Is.True);
            s.Tick(4.1f);Assert.That(s.ObserveRuns().Single().waiting,Is.True);Assert.That(s.TargetsBusy(new[]{"maestro"}),Is.False);
            s.Tick(4.5f);s.Emit(RuleEventKind.ItemTapped,"book",5);s.Tick(5);
            Assert.That(State(s,"count"),Is.EqualTo("2"));Assert.That(actions.Starts,Is.EqualTo(2));
            s.Suspend(true);Assert.That(s.RunningCount,Is.Zero);s.Suspend(false);s.Tick(20);Assert.That(actions.Starts,Is.EqualTo(2));
            Assert.That(s.Trigger(sequence.id,21),Is.True);Assert.That(State(s,"count"),Is.EqualTo("0"),"Explicit new run initializes state");
        }
        [Test] public void ReactiveLayersAcquireAtInvocationReleaseDuringWaitAndCannotStealAChannel()
        {
            var source=Source();Loop(source)[1]["then"][1]["capability"]="avatar.gesture.upperBody";
            var sequence=Sequence(source);var a=new Actions();var scheduler=Scheduler(a,sequence);
            var follow=JObject.Parse(@"{'id':'avatar.follow.user','version':1,'arguments':{'target':'maestro','seconds':10}}");
            var wave=JObject.Parse(@"{'id':'avatar.gesture.upperBody','version':1,'arguments':{'target':'maestro','gesture':'greeting','seconds':10}}");
            Assert.That(scheduler.Invoke(follow,0,out var walking,out _),Is.True);
            Assert.That(scheduler.Trigger(sequence.id,0),Is.True);
            scheduler.Emit(RuleEventKind.ItemTapped,"book",.1f);scheduler.Tick(.1f);
            Assert.That(State(scheduler,"count"),Is.EqualTo("1"));
            Assert.That(scheduler.RunningCount,Is.EqualTo(2));
            Assert.That(scheduler.Invoke(wave,.2f,out _,out _),Is.False,"The active event invocation owns its arms");
            scheduler.Tick(1.2f);Assert.That(scheduler.ObserveRuns().Single().waiting,Is.True);
            Assert.That(scheduler.Invoke(wave,1.2f,out var other,out _),Is.True,"The sleeping program released its channel");
            scheduler.Tick(1.5f);scheduler.Emit(RuleEventKind.ItemTapped,"book",1.6f);scheduler.Tick(1.6f);
            Assert.That(scheduler.Outcomes.Single().phase,Is.EqualTo("failed"),"An event may not silently preempt another owner");
            Assert.That((string)scheduler.Invocation(other)["phase"],Is.EqualTo("running"));
            // The synthetic action duration is one second, so the original follow
            // already completed; its receipt must remain terminal.
            Assert.That((string)scheduler.Invocation(walking)["phase"],Is.EqualTo("completed"));
            scheduler.StopAll();Assert.That(scheduler.TargetsBusy(new[]{"maestro"}),Is.False);
        }
        [Test] public void TimersSkipMissedTicksAndStopCancelsAllWaits()
        {
            var p=Source();Loop(p).Clear();
            Loop(p).Add(JObject.Parse(@"{'id':'delay','op':'sleep','seconds':{'value':0.5}}"));
            Loop(p).Add(JObject.Parse(@"{'id':'count','op':'setState','variable':'count','value':{'op':'add','args':[{'state':'count'},{'value':1}]}}"));
            var a=new Actions();var sequence=Sequence(p);var s=Scheduler(a,sequence);
            Assert.That(s.Trigger(sequence.id,0),Is.True);s.Tick(100);Assert.That(State(s,"count"),Is.EqualTo("1"));s.Tick(100);Assert.That(State(s,"count"),Is.EqualTo("1"));
            s.Tick(100.6f);Assert.That(State(s,"count"),Is.EqualTo("2"));Assert.That(a.Starts,Is.Zero);
            var call=JObject.Parse(@"{'id':'time.wait','version':1,'arguments':{'seconds':1}}");
            Assert.That(s.Invoke(call,101,out var other,out _),Is.True);
            Assert.That(s.StopSequence("missing"),Is.False);Assert.That(s.RunningCount,Is.EqualTo(2));
            Assert.That(s.StopSequence(sequence.id),Is.True);Assert.That(s.RunningCount,Is.EqualTo(1));Assert.That((string)s.Invocation(other)["phase"],Is.EqualTo("running"));
            s.StopAll();s.Tick(200);Assert.That(s.RunningCount,Is.Zero);Assert.That(s.EventQueueCount,Is.Zero);
            Assert.That(s.Trigger(sequence.id,201),Is.True);s.Configure(new RuleDocument {sequences=new[]{sequence}});s.Tick(500);Assert.That(s.RunningCount,Is.Zero,"Reload does not restart timers");
        }
        [Test] public void BoundedQueueDeliversOnceAndLateEventCannotWakeNextWait()
        {
            var p=Source();var loop=Loop(p);loop[1]["then"]=new JArray(loop[1]["then"][0].DeepClone());
            loop[0]["timeout"]["value"]=.5;
            var sequence=Sequence(p);var a=new Actions();var s=Scheduler(a,sequence);s.Trigger(sequence.id,0);
            for(int i=0;i<65;i++)s.Emit(RuleEventKind.ItemTapped,"book",.2f);
            Assert.That(s.EventQueueCount,Is.EqualTo(64));Assert.That(s.EventsDropped,Is.EqualTo(1));
            s.Tick(.3f);Assert.That(State(s,"count"),Is.EqualTo("1"));s.Tick(.4f);s.Tick(.5f);s.Tick(.6f);
            Assert.That(State(s,"count"),Is.EqualTo("1"),"Captured wait generation prevents delivery to a new wait");
            s.Emit(RuleEventKind.ItemTapped,"book",2);s.Tick(2);
            Assert.That(State(s,"count"),Is.EqualTo("1"),"Event emitted after deadline loses to timeout");
            Assert.That(s.ObserveRuns().Single().waiting,Is.True);Assert.That(s.EventQueueCount,Is.Zero);
        }
        [Test] public void CustomSignalsUseDeclaredTypesAndRealWireHydration()
        {
            var p=Source();var loop=Loop(p);loop[0]["event"]="user.wave";loop[0]["source"]="";p["functions"][0]["locals"][1]["initial"]=0;
            loop[1]["then"]=new JArray(JObject.Parse(@"{'id':'save','op':'setState','variable':'count','value':{'var':'payload'}}"));
            var sequence=Sequence(p);var s=Scheduler(new Actions(),sequence);s.Trigger(sequence.id,0);
            Assert.That(s.Signal("user.wave",new ProgramValue("bad"),1,out _),Is.False);
            Assert.That(s.Signal("user.missing",new ProgramValue(7d),1,out _),Is.False);
            var command=JObject.Parse(@"{'action':'rules','rule':{'action':'signal','revision':1,'eventName':'user.wave','value':7}}");
            var root=new JObject {["version"]=2,["commands"]=new JArray(command)};
            Assert.That(RoomControls.ValidWire(root.ToString()),Is.True);
            var request=JsonUtility.FromJson<RoomAgentRequest>(root.ToString());Assert.That(RoomAgentWire.PopulateStructured(request,root),Is.True);
            Assert.That(s.Signal(request.commands[0].rule.eventName,ProgramValue.Literal(request.commands[0].rule.value),1,out _),Is.True);s.Tick(1);
            Assert.That(State(s,"count"),Is.EqualTo("7"));command["rule"]["extra"]=true;Assert.That(RoomControls.ValidWire(root.ToString()),Is.False);
            s.Suspend(true);Assert.That(s.Signal("user.wave",new ProgramValue(9d),2,out _),Is.False);
        }
        [Test] public void WaitingProgramsDoNotPreemptOwnersAndLoadingCancellationStaysTerminal()
        {
            var a=new Actions();var sequence=Sequence();var s=Scheduler(a,sequence);s.Trigger(sequence.id,0);
            var call=JObject.Parse(@"{'id':'avatar.gesture.play','version':1,'arguments':{'target':'maestro','gesture':'greeting','seconds':1}}");
            Assert.That(s.Invoke(call,0,out var owner,out _),Is.True);
            s.Emit(RuleEventKind.ItemTapped,"book",.1f);s.Tick(.1f);
            Assert.That(s.Outcomes.Single().phase,Is.EqualTo("failed"));Assert.That((string)s.Invocation(owner)["phase"],Is.EqualTo("running"));
            s.StopAll();a.Phase=RuleActionState.Preparing;s.Trigger(sequence.id,1);s.Emit(RuleEventKind.ItemTapped,"book",1);s.Tick(1);
            Assert.That(s.PreparingCount,Is.EqualTo(1));s.StopTarget("maestro",true);a.Phase=RuleActionState.Ready;s.Tick(50);
            Assert.That(s.RunningCount,Is.Zero);Assert.That(s.Outcomes.Last().phase,Is.EqualTo("cancelled"));
        }
        [Test] public void CrossProgramEventCyclesAreBoundedAndExternalEventsStartANewChain()
        {
            JObject Ping(string hear,string send) {
                var p=Source();p["events"]=JArray.Parse(@"[{'name':'user.ping','type':'number'},{'name':'user.pong','type':'number'},{'name':'user.pang','type':'number'}]");
                p["functions"][0]["locals"][1]["initial"]=0;
                var loop=Loop(p);loop[0]["event"]=hear;loop[0]["source"]="";loop.RemoveAt(1);
                loop.Add(new JObject {["id"]="send",["op"]="emitEvent",["event"]=send,["value"]=new JObject {["value"]=1}});
                return p;
            }
            var first=Sequence(Ping("user.ping","user.pong"));var second=Sequence(Ping("user.pong","user.pang"));var third=Sequence(Ping("user.pang","user.ping"));var s=Scheduler(new Actions(),first,second,third);
            Assert.That(s.Trigger(first.id,0),Is.True);Assert.That(s.Trigger(second.id,0),Is.True);Assert.That(s.Trigger(third.id,0),Is.True);
            Assert.That(s.Signal("user.ping",new ProgramValue(1d),0,out _),Is.True);
            for(int i=1;i<100&&s.Outcomes.Length==0;i++)s.Tick(i*.01f);
            Assert.That(s.Outcomes.Single().phase,Is.EqualTo("failed"));StringAssert.Contains("chain limit",s.LastError);
            Assert.That(s.EventQueueCount,Is.Zero);Assert.That(s.RunningCount,Is.EqualTo(2),"Other waiting runs remain active");
            var survivor=s.ObserveRuns().First();string heard=survivor.sequenceId==first.id?"user.ping":survivor.sequenceId==second.id?"user.pong":"user.pang";
            Assert.That(s.Signal(heard,new ProgramValue(3d),2,out _),Is.True);s.Tick(2);
            Assert.That(s.Outcomes.Length,Is.EqualTo(1),"An external event starts a fresh causal budget");s.StopAll();
        }
        [Test] public void CompletedNativeActionsRenewReactiveBudgetsWithoutLosingState()
        {
            var p=Source();var loop=Loop(p);var increment=loop[1]["then"][0].DeepClone();var action=loop[1]["then"][1].DeepClone();
            loop.Clear();
            loop.Add(new JObject {["id"]="work",["op"]="repeat",["count"]=new JObject {["value"]=1000},["body"]=new JArray(increment)});
            loop.Add(action);
            var a=new Actions();var sequence=Sequence(p);var s=Scheduler(a,sequence);Assert.That(s.Trigger(sequence.id,0),Is.True);
            for(int i=0;i<10000&&a.Starts<15&&s.RunningCount>0;i++)s.Tick(i);
            Assert.That(a.Starts,Is.EqualTo(15));Assert.That(State(s,"count"),Is.EqualTo("15000"));Assert.That(s.Outcomes,Is.Empty,
                "Time-consuming native completions, like elapsed timers, renew the activation budget");
            Assert.That(s.StopSequence(sequence.id),Is.True);Assert.That(s.RunningCount,Is.Zero);
        }
        [Test] public void ParserRejectsAmbiguousEventTypesAndUnboundedComputingFails()
        {
            var p=Source();p["events"][0]["type"]="void";Assert.That(BehaviourProgram.TryParse(p.ToString(),out _,out _),Is.False);
            p=Source();p["version"]=2;Assert.That(BehaviourProgram.TryParse(p.ToString(),out _,out _),Is.False);
            p=Source();Loop(p)[0]["received"]="payload";Assert.That(BehaviourProgram.TryParse(p.ToString(),out _,out _),Is.False);
            var one=Sequence();p=Source();p["events"][0]["type"]="text";var two=Sequence(p);
            Assert.That(new RuleDocument {sequences=new[]{one,two}}.Validate(out var conflict),Is.False);StringAssert.Contains("types",conflict);
            p=Source();Loop(p).Clear();var endless=Sequence(p);var s=Scheduler(new Actions(),endless);Assert.That(s.Trigger(endless.id,0),Is.True);
            for(int i=0;i<2200&&s.RunningCount>0;i++)s.Tick(i*.01f);
            Assert.That(s.Outcomes.Single().phase,Is.EqualTo("failed"));StringAssert.Contains("budget",s.LastError);
        }
    }
}
