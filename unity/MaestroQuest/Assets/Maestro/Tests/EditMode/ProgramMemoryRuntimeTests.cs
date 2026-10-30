// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Maestro.Quest.Persistence;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class ProgramMemoryRuntimeTests
    {
        const string A="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",B="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",Cell="cccccccccccccccccccccccccccccccc";
        string directory;readonly List<ProgramMemoryStore> stores=new();
        sealed class Actions:IRuleActions {internal int Started;public bool CanRun(CapabilityCall c,out string error){error=null;return true;}public bool Start(string id,CapabilityCall c,out float seconds,out string error){Started++;seconds=c.Instant?0:.1f;error=null;return true;}public void Stop(string id,bool preserve){}}
        [SetUp]public void Setup(){directory=Path.Combine(Path.GetTempPath(),"Maestro-memory-runtime-"+Guid.NewGuid().ToString("N"));}
        [TearDown]public void Cleanup(){foreach(var store in stores)store.Drain().GetAwaiter().GetResult();stores.Clear();if(Directory.Exists(directory))Directory.Delete(directory,true);}
        ProgramMemoryStore Open(Action<string> fault=null){var store=new ProgramMemoryStore(directory,new(),fault);stores.Add(store);store.Initialization.GetAwaiter().GetResult();return store;}
        static JObject Source()=>JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-memory.json")));
        static BehaviourProgram Compile(JObject source){Assert.That(BehaviourProgram.TryParse(source.ToString(),out var program,out var error),Is.True,error);return program;}
        static RuleSequence Sequence(string id=A,JObject source=null)=>new(){id=id,name="Remembered counter",program=(source??Source()).ToString()};
        static RuleScheduler Scheduler(ProgramMemoryStore store,params RuleSequence[] sequences){var scheduler=new RuleScheduler(new Actions());scheduler.ConfigureMemory(store);scheduler.Configure(new(){sequences=sequences.Length>0?sequences:new[]{Sequence()}});return scheduler;}
        static double Value(ProgramMemoryStore store,string program=A){Assert.That(store.Snapshot().TryRead(program,Cell,ProgramType.Number,out var value),Is.True);return value.Number;}
        static void Save(ProgramMemoryStore store,RuleScheduler scheduler,float now){scheduler.Tick(now);store.Drain().GetAwaiter().GetResult();scheduler.Tick(now+.01f);scheduler.Tick(now+.02f);}
        [Test]public void ExplicitRestartLoadsCheckpointWhilePerRunValuesResetAndCopiesAreIsolated(){
            var store=Open();var scheduler=Scheduler(store,Sequence(),Sequence(B));Assert.That(scheduler.Trigger(A,0),Is.True,scheduler.LastError);Assert.That(scheduler.ObserveRuns().Single().status,Is.EqualTo("Saving remembered values"));
            Save(store,scheduler,0);Assert.That(Value(store),Is.EqualTo(1));scheduler.StopAll();Assert.That(scheduler.Trigger(A,2),Is.True);Save(store,scheduler,2);Assert.That(Value(store),Is.EqualTo(2));Assert.That(scheduler.ObserveRuns().Single().state.Single(x=>x.name=="perRun").value,Is.EqualTo("8"));
            scheduler.StopAll();var reopened=Open();var restarted=Scheduler(reopened,Sequence(),Sequence(B));Assert.That(restarted.RunningCount,Is.Zero);Assert.That(restarted.Trigger(B,4),Is.True);Save(reopened,restarted,4);Assert.That(Value(reopened,B),Is.EqualTo(1));Assert.That(Value(reopened),Is.EqualTo(2));
        }
        [Test]public void StopBeforeDispatchDiscardsUncheckpointedAssignments(){var store=Open();var scheduler=Scheduler(store);Assert.That(scheduler.Trigger(A,0),Is.True);scheduler.StopAll();scheduler.Tick(4);Assert.That(store.Snapshot().Programs,Is.Empty);Assert.That(File.Exists(Path.Combine(directory,ProgramMemoryStore.FileName)),Is.False);}
        [Test]public void StopAfterDispatchDrainsAcceptedSaveButNeverRunsFollowingBlocks(){
            using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();var store=Open(point=>{if(point=="before-publish"){entered.Set();if(!release.Wait(10000))throw new IOException("Test timed out");}});
            var source=Source();source["functions"][0]["body"][3]=new JObject{["id"]="effect",["op"]="invoke",["capability"]="time.wait",["version"]=1,["arguments"]=new JObject{["seconds"]=1},["bindings"]=new JObject()};
            var actions=new Actions();var scheduler=new RuleScheduler(actions);scheduler.ConfigureMemory(store);scheduler.Configure(new(){sequences=new[]{Sequence(source:source)}});
            try{Assert.That(scheduler.Trigger(A,0),Is.True);scheduler.Tick(0);Assert.That(entered.Wait(5000),Is.True);Assert.That(scheduler.Trigger(A,.1f),Is.False);Assert.That(scheduler.RunningCount,Is.EqualTo(1),"A rejected restart cannot cancel an existing run");scheduler.StopAll();Assert.That(scheduler.Outcomes.Last().status,Does.Contain("may still finish"));}
            finally{release.Set();store.Drain().GetAwaiter().GetResult();}
            scheduler.Tick(10);Assert.That(Value(store),Is.EqualTo(1));Assert.That(actions.Started,Is.Zero);Assert.That(scheduler.RunningCount,Is.Zero);
        }
        [Test]public void FailureStopsAtCheckpointAndDoesNotPublishCandidate(){var store=Open(point=>{if(point=="before-publish")throw new IOException("Disk rejected write");});var scheduler=Scheduler(store);Assert.That(scheduler.Trigger(A,0),Is.True);Save(store,scheduler,0);Assert.That(scheduler.Outcomes.Last().phase,Is.EqualTo("failed"));Assert.That(scheduler.LastError,Does.Contain("not confirmed"));Assert.That(store.Snapshot().Programs,Is.Empty);}
        [Test]public void DifferentProgramsMergeInFifoOrderAndCannotBypassGlobalRateLimit(){
            var store=Open();var scheduler=Scheduler(store,Sequence(),Sequence(B));Assert.That(scheduler.Trigger(A,0),Is.True);Assert.That(scheduler.Trigger(B,0),Is.True);Save(store,scheduler,0);
            Assert.That(Value(store),Is.EqualTo(1));Assert.That(store.Snapshot().Programs.ContainsKey(B),Is.False);scheduler.Tick(.99f);Assert.That(store.Pending,Is.False);Save(store,scheduler,1);Assert.That(Value(store,B),Is.EqualTo(1));Assert.That(Value(store),Is.EqualTo(1));
        }
        [Test]public void RenamingKeepsIdentityAndUndeclaredSavedCellsSurvive(){
            var store=Open();var scheduler=Scheduler(store);scheduler.Trigger(A,0);Save(store,scheduler,0);scheduler.StopAll();var source=Source();source["state"][0]["name"]="score";source["functions"][0]["body"][0]["variable"]="score";source["functions"][0]["body"][0]["value"]["args"][0]["state"]="score";
            scheduler.Configure(new(){sequences=new[]{Sequence(source:source)}});Assert.That(scheduler.Trigger(A,2),Is.True);Save(store,scheduler,2);Assert.That(Value(store),Is.EqualTo(2));Assert.That(store.Snapshot().Programs[A][Cell].Name,Is.EqualTo("score"));scheduler.StopAll();scheduler.Configure(new());Assert.That(Value(store),Is.EqualTo(2));
        }
        [Test]public void SavedTypeMismatchCorruptionAndTemporaryRoomsFailBeforeEffects(){
            var store=Open();store.Write(store.Snapshot().Revision,A,new Dictionary<string,ProgramMemoryDocument.Cell>{[Cell]=new("count",new ProgramValue("wrong type"))}).GetAwaiter().GetResult();var scheduler=Scheduler(store);Assert.That(scheduler.Trigger(A,0),Is.False);Assert.That(scheduler.LastError,Does.Contain("type differs"));
            store.Reset(store.Snapshot().Revision,A).GetAwaiter().GetResult();scheduler.ConfigureMemory(store,()=>"Temporary room");Assert.That(scheduler.Trigger(A,0),Is.False);Assert.That(scheduler.LastError,Is.EqualTo("Temporary room"));
            File.WriteAllText(Path.Combine(directory,ProgramMemoryStore.FileName),"broken");scheduler.ConfigureMemory(Open());Assert.That(scheduler.Trigger(A,0),Is.False);Assert.That(scheduler.LastError,Does.Contain("unavailable"));Assert.That(scheduler.RunningCount,Is.Zero);
        }
        [Test]public void ARememberedObjectIdentityDoesNotGrantActionAuthority(){
            var store=Open();store.Write(store.Snapshot().Revision,A,new Dictionary<string,ProgramMemoryDocument.Cell>{[Cell]=new("count",new ProgramValue(B))}).GetAwaiter().GetResult();
            var source=Source();source["state"][0]["initial"]=B;source["functions"][0]["body"]=new JArray(new JObject{["id"]="effect",["op"]="invoke",["capability"]="object.physics.stop",["version"]=1,["arguments"]=new JObject{["target"]=B},["bindings"]=new JObject{["target"]=new JObject{["state"]="count"}}});
            var actions=new Actions();var scheduler=new RuleScheduler(actions);scheduler.ConfigureMemory(store);scheduler.Configure(new(){sequences=new[]{Sequence(source:source)}});Assert.That(scheduler.Trigger(A,0),Is.False);Assert.That(scheduler.LastError,Does.Contain("declared or created"));Assert.That(actions.Started,Is.Zero);
        }
        [Test]public void PublicMachineCannotSilentlyIgnoreRememberedDeclarations(){Assert.Throws<ProgramFault>(()=>new ProgramMachine(Compile(Source()),null));}
        [Test]public void CheckpointsDoNotRenewInstructionBudget(){
            var source=Source();source["functions"][0]["body"]=new JArray(new JObject{["id"]="loop",["op"]="forever",["body"]=new JArray(new JObject{["id"]="save",["op"]="checkpoint"})});
            var machine=new ProgramMachine(Compile(source),null,new Dictionary<string,ProgramValue>());int writes=0;ProgramYield result;
            do{result=machine.Advance(out _);if(result==ProgramYield.Checkpoint){writes++;machine.CompleteCheckpoint();}}while(result!=ProgramYield.Failed&&writes<65537);
            Assert.That(result,Is.EqualTo(ProgramYield.Failed));Assert.That(machine.Error,Does.Contain("instruction budget"));Assert.That(machine.Activations,Is.Zero);
        }
        [Test]public void ChildCheckpointFailsBeforeAnyWrite(){
            var source=Source();source["parallelVersion"]=1;source["functions"][0]["body"]=new JArray(new JObject{["id"]="fork",["op"]="parallel",["branches"]=new JArray(new JObject{["function"]="child",["args"]=new JArray()},new JObject{["function"]="child",["args"]=new JArray()})});
            ((JArray)source["functions"]).Add(new JObject{["name"]="child",["returns"]="void",["parameters"]=new JArray(),["locals"]=new JArray(),["body"]=new JArray(new JObject{["id"]="save",["op"]="checkpoint"})});var store=Open();var scheduler=Scheduler(store,Sequence(source:source));Assert.That(scheduler.Trigger(A,0),Is.True);scheduler.Tick(0);Assert.That(scheduler.RunningCount,Is.Zero);Assert.That(scheduler.LastError,Does.Contain("parent"));Assert.That(store.Snapshot().Programs,Is.Empty);
        }
        [Test]public void MemoryValueTransportHasItsOwnBoundWithoutEnlargingOtherActionStrings(){
            var call=new JObject{["id"]="program.memory.edit",["version"]=1,["arguments"]=new JObject{["sessionId"]=new string('a',32),["kind"]="set",["programId"]=A,["variableId"]=Cell,["revision"]="initial",["rulesRevision"]=1,["valueJson"]="["+string.Join(",",Enumerable.Repeat("123456",32))+"]"}};
            Assert.That(Maestro.Quest.Creation.RoomCapabilityCatalog.ValidCall(call),Is.True);call["arguments"]["valueJson"]=new string('x',8193);Assert.That(Maestro.Quest.Creation.RoomCapabilityCatalog.ValidCall(call),Is.False);call["arguments"]["valueJson"]="1";call["arguments"]["programId"]=new string('x',129);Assert.That(Maestro.Quest.Creation.RoomCapabilityCatalog.ValidCall(call),Is.False);
            var source=Source();source["memoryVersion"]=new JValue(1d);Assert.That(BehaviourProgram.TryParse(source.ToString(),out _,out _),Is.True);
        }
        [Test]public void StrictExtensionChecksAndModulePublishingRejectAmbiguousMemory(){
            var source=Source();source.Remove("memoryVersion");Assert.That(BehaviourProgram.TryParse(source.ToString(),out _,out _),Is.False);source=Source();((JArray)source["state"]).Add(new JObject{["name"]="duplicate",["initial"]=1,["memory"]=Cell});Assert.That(BehaviourProgram.TryParse(source.ToString(),out _,out _),Is.False);
            source=Source();source["functions"][0]["body"][2]["extra"]=true;Assert.That(BehaviourProgram.TryParse(source.ToString(),out _,out _),Is.False);Assert.Throws<ProgramFault>(()=>ProgramModuleLibrary.Definition(Source().ToString(),"Counter",new[]{"main"}));
        }
        [Test]public void RetentionProtectsPrimaryAndBackupAndMarksUnknownEvidenceUncertain(){
            var store=Open();var cells=new Dictionary<string,ProgramMemoryDocument.Cell>{[Cell]=new("asset",new ProgramValue(B))};store.Write(store.Snapshot().Revision,A,cells).GetAwaiter().GetResult();store.Reset(store.Snapshot().Revision,A).GetAwaiter().GetResult();Assert.That(store.Retains(B,out var uncertain),Is.True);Assert.That(uncertain,Is.False);
            File.WriteAllText(Path.Combine(directory,"program-memory.v2.json"),"unknown");Assert.That(store.Retains(B,out uncertain),Is.False);Assert.That(uncertain,Is.True);
        }
    }
}
