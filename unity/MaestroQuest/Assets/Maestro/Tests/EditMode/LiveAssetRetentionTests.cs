// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class LiveAssetRetentionTests
    {
        static readonly string Hash=new('a',64),Other=new('b',64);
        static JObject Local(string name,JToken value)=>new(){["name"]=name,["initial"]=value};
        static JObject Function(string name,JArray locals,params JObject[] body)=>new(){["name"]=name,["returns"]="void",["parameters"]=new JArray(),["locals"]=locals,["body"]=new JArray(body)};
        static JObject Source(params JObject[] functions)=>new(){["version"]=3,["entry"]="main",["resources"]=new JArray(),["state"]=new JArray(),["events"]=new JArray(new JObject{["name"]="user.asset",["type"]="text"}),["functions"]=new JArray(functions)};
        static JObject Wait(string id)=>new(){["id"]=id,["op"]="awaitEvent",["event"]="user.asset",["source"]="",["timeout"]=new JObject{["value"]=0},["received"]="received",["value"]="payload"};
        static JObject Sleep(string id)=>new(){["id"]=id,["op"]="sleep",["seconds"]=new JObject{["value"]=10}};
        static JArray EventLocals()=>new(Local("received",false),Local("payload",""));
        static BehaviourProgram Compile(JObject source){Assert.That(BehaviourProgram.TryParse(source.ToString(Formatting.None),out var value,out var error),Is.True,error);return value;}
        static void AssertRetains(ProgramMachine machine,bool expected,bool exempt=false)
        {Assert.That(machine.RetainsAssetIdentity(Hash,out var uncertain,exempt),Is.EqualTo(expected));Assert.That(uncertain,Is.False);}
        static RuleSequence Sequence(JObject source)=>new(){id=Guid.NewGuid().ToString("N"),name="Retained asset",program=source.ToString(Formatting.None)};
        sealed class Actions:IRuleActions,IRuleReadiness
        {
            public bool CanRun(CapabilityCall call,out string error){error=null;return true;}
            public bool Start(string id,CapabilityCall call,out float seconds,out string error){seconds=0;error=null;return true;}
            public void Stop(string id,bool preserve){}
            public RuleActionState State(string id,out string error){error=null;return RuleActionState.Preparing;}
        }
        static JObject ImportCall()=>new(){["id"]="image.import",["version"]=1,["arguments"]=new JObject{["operation"]="accept",["requestId"]=new string('c',32),["imageHash"]=Hash}};
        static JObject ImportOutput()=>new(){["requestId"]=new string('c',32),["imageHash"]=Hash,["appearanceId"]="",["revision"]=0,["temporary"]=false,
            ["library"]=new JObject{["ready"]=false,["error"]="Refresh to inspect saved files",["total"]=0,["next"]=-1,["entries"]=new JArray()}};

        [Test] public void CompiledAssetReferencesUseExactDecodedStringsIncludingEscapedLiterals()
        {
            var source=Source(Function("main",new JArray(Local("asset",Hash))));
            string escaped=source.ToString(Formatting.None).Replace(Hash,string.Concat(Enumerable.Repeat("\\u0061",64)));
            Assert.That(BehaviourProgram.TryParse(escaped,out var program,out var error),Is.True,error);
            Assert.That(program.ReferencesAssetIdentity(Hash),Is.True);Assert.That(program.ReferencesAssetIdentity(Other),Is.False);
            source["functions"][0]["locals"][0]["initial"]="prefix-"+Hash;
            Assert.That(Compile(source).ReferencesAssetIdentity(Hash),Is.False,"An arbitrary substring is not an exact identity");
        }
        [Test] public void CallerScopeSurvivesWhileOnlyTheCalleeScopeIsVisible()
        {
            var source=Source(Function("main",EventLocals(),Wait("receive"),new JObject{["id"]="call",["op"]="call",["function"]="helper",["args"]=new JArray()},
                new JObject{["id"]="clear",["op"]="set",["variable"]="payload",["value"]=new JObject{["value"]=""}}),Function("helper",new JArray(),Sleep("delay")));
            var program=Compile(source);Assert.That(program.ReferencesAssetIdentity(Hash),Is.False);
            var machine=new ProgramMachine(program,null);Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Waiting));machine.Resume(true,new ProgramValue(Hash));
            Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Waiting));Assert.That(machine.Function,Is.EqualTo("helper"));Assert.That(machine.Locals,Is.Empty);AssertRetains(machine,true);
            machine.Resume(false);Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Completed));AssertRetains(machine,false);
        }
        [Test] public void DynamicStateRetainsAnIdentityEvenAfterItsLocalIsCleared()
        {
            var source=Source(Function("main",EventLocals(),Wait("receive"),
                new JObject{["id"]="keep",["op"]="setState",["variable"]="saved",["value"]=new JObject{["var"]="payload"}},
                new JObject{["id"]="clear",["op"]="set",["variable"]="payload",["value"]=new JObject{["value"]=""}},Sleep("delay")));
            source["state"]=new JArray(Local("saved",""));var machine=new ProgramMachine(Compile(source),null);
            machine.Advance(out _);machine.Resume(true,new ProgramValue(Hash));Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Waiting));
            Assert.That(machine.Locals["payload"].Text,Is.Empty);AssertRetains(machine,true);
        }
        [Test] public void ParallelBranchValuesProtectAnImageUntilTheJoinReleasesTheBranches()
        {
            var source=Source(Function("main",new JArray(),new JObject{["id"]="parallel",["op"]="parallel",["branches"]=new JArray(
                new JObject{["function"]="worker",["args"]=new JArray()},new JObject{["function"]="worker",["args"]=new JArray()})}),
                Function("worker",EventLocals(),Wait("receive"),Sleep("delay")));
            source["parallelVersion"]=1;var machine=new ProgramMachine(Compile(source),null);Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Parallel));
            foreach(var branch in machine.Branches)Assert.That(branch.Advance(out _),Is.EqualTo(ProgramYield.Waiting));
            machine.Branches[0].Resume(true,new ProgramValue(Hash));machine.Branches[1].Resume(true,new ProgramValue("other"));
            foreach(var branch in machine.Branches)Assert.That(branch.Advance(out _),Is.EqualTo(ProgramYield.Waiting));AssertRetains(machine,true);
            foreach(var branch in machine.Branches){branch.Resume(false);Assert.That(branch.Advance(out _),Is.EqualTo(ProgramYield.Completed));}
            AssertRetains(machine,true);Assert.That(machine.CompleteParallel(out var error),Is.True,error);AssertRetains(machine,false);
        }
        [Test] public void QueuedEventsProtectIdentitiesBeforeAndAfterDeliveryWithoutPredictingFutureEvents()
        {
            var sequence=Sequence(Source(Function("main",EventLocals(),Wait("receive"),Sleep("delay"))));var scheduler=new RuleScheduler(new Actions());scheduler.Configure(new(){sequences=new[]{sequence}});
            Assert.That(scheduler.Trigger(sequence.id,0),Is.True,scheduler.LastError);
            Assert.That(scheduler.RetainsAssetIdentity(Hash,null,out var uncertain),Is.False);Assert.That(uncertain,Is.False);
            Assert.That(scheduler.Signal("user.asset",new ProgramValue(Hash),1,out var status),Is.True,status);Assert.That(scheduler.EventQueueCount,Is.EqualTo(1));
            Assert.That(scheduler.RetainsAssetIdentity(Hash,null,out uncertain),Is.True);Assert.That(uncertain,Is.False);
            scheduler.Tick(1);Assert.That(scheduler.EventQueueCount,Is.Zero);Assert.That(scheduler.RetainsAssetIdentity(Hash,null,out uncertain),Is.True);Assert.That(uncertain,Is.False);
            scheduler.StopAll();Assert.That(scheduler.RetainsAssetIdentity(Hash,null,out uncertain),Is.False);Assert.That(uncertain,Is.False);
        }
        [Test] public void ExemptionRemovesOnlyTheExactCurrentOneOffEnvelope()
        {
            var scheduler=new RuleScheduler(new Actions());Assert.That(scheduler.Invoke(ImportCall(),0,out var first,out var error),Is.True,error);
            Assert.That(scheduler.RetainsAssetIdentity(Hash,null,out var uncertain),Is.True);Assert.That(uncertain,Is.False);
            Assert.That(scheduler.RetainsAssetIdentity(Hash,first,out uncertain),Is.False);Assert.That(uncertain,Is.False);
            Assert.That(scheduler.Invoke(ImportCall(),1,out _,out error),Is.True,error);
            Assert.That(scheduler.RetainsAssetIdentity(Hash,first,out uncertain),Is.True,"A different active invocation still protects its image");Assert.That(uncertain,Is.False);
        }
        [Test] public void ExemptRunIdNeverExemptsASavedProgramLiteral()
        {
            var source=JObject.Parse(BehaviourProgram.FromInvocation(ImportCall()));var sequence=Sequence(source);var scheduler=new RuleScheduler(new Actions());scheduler.Configure(new(){sequences=new[]{sequence}});
            Assert.That(scheduler.Trigger(sequence.id,0),Is.True,scheduler.LastError);string run=scheduler.ObserveRuns().Single().id;
            Assert.That(scheduler.RetainsAssetIdentity(Hash,run,out var uncertain),Is.True);Assert.That(uncertain,Is.False);
        }
        [Test] public void OneOffExemptionKeepsAcceptedActionResults()
        {
            Assert.That(BehaviourProgram.TryInvocation(ImportCall(),out var program,out var error),Is.True,error);var machine=new ProgramMachine(program,null);
            Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Action));AssertRetains(machine,false,exempt:true);
            Assert.That(machine.CompleteAction(ImportOutput(),out error),Is.True,error);AssertRetains(machine,true,exempt:true);
        }
        [Test] public void UnreadableStoredProgramAndExhaustedTraversalAreUncertain()
        {
            var scan=new AssetIdentityRetention(Hash);RuleScheduler.ReadAssetDocument(scan,new RuleDocument{sequences=new[]{new RuleSequence{id=Guid.NewGuid().ToString("N"),name="damaged",program="invalid"}}});
            Assert.That(scan.Uncertain,Is.True);Assert.That(scan.Found,Is.False);
            var bounded=new AssetIdentityRetention(Hash,2);bounded.Json(new JArray("unrelated","unrelated",Hash));Assert.That(bounded.Uncertain,Is.True);Assert.That(bounded.Found,Is.False);
        }
        [Test] public void NestedRecordAndListValuesProtectExactIdentities()
        {
            var value=ProgramValue.Literal(new JObject{["assets"]=new JArray("other",Hash)});var scan=new AssetIdentityRetention(Hash);scan.Value(value);
            Assert.That(scan.Found,Is.True);Assert.That(scan.Uncertain,Is.False);
        }
        static string MemoryProgram=>new('c',32);
        static Dictionary<string,ProgramMemoryDocument.Cell> MemoryValues()=>new(){[new string('d',32)]=new("asset",new ProgramValue(Hash))};
        [Test] public void MemoryRetainsSavedTemporaryAndPinnedCandidatesUntilTheirOwnersReleaseThem()
        {
            string directory=Path.Combine(Path.GetTempPath(),"MaestroLiveReferences-"+Guid.NewGuid().ToString("N"));var memory=new ProgramMemoryStore(directory,new());
            try {
                memory.Initialization.GetAwaiter().GetResult();Assert.That(memory.Error,Is.Null);
                Assert.That(memory.RetainsLiveIdentity(Hash,out var uncertain),Is.False);Assert.That(uncertain,Is.False);
                Assert.That(memory.Write(memory.Snapshot().Revision,MemoryProgram,MemoryValues()).GetAwaiter().GetResult().Error,Is.Null);
                var saved=memory.Snapshot();memory.BeginTemporary();
                Assert.That(memory.Reset(memory.Snapshot().Revision,MemoryProgram).GetAwaiter().GetResult().Error,Is.Null);
                Assert.That(memory.Snapshot().Retains(Hash),Is.False);Assert.That(memory.RetainsLiveIdentity(Hash,out uncertain),Is.True,"The saved base survives a temporary reset");Assert.That(uncertain,Is.False);
                memory.EndTemporary();Assert.That(memory.Reset(memory.Snapshot().Revision,MemoryProgram).GetAwaiter().GetResult().Error,Is.Null);
                Assert.That(memory.RetainsLiveIdentity(Hash,out uncertain),Is.False,"Disk backups are separately covered by saved-file proof");Assert.That(uncertain,Is.False);
                using(memory.RetainSnapshot(saved)){Assert.That(memory.RetainsLiveIdentity(Hash,out uncertain),Is.True,"An in-flight publication may still own its captured candidate");Assert.That(uncertain,Is.False);}
                Assert.That(memory.RetainsLiveIdentity(Hash,out uncertain),Is.False);Assert.That(uncertain,Is.False);
                memory.BeginTemporary();Assert.That(memory.Write(memory.Snapshot().Revision,MemoryProgram,MemoryValues()).GetAwaiter().GetResult().Error,Is.Null);
                Assert.That(memory.RetainsLiveIdentity(Hash,out uncertain),Is.True);Assert.That(uncertain,Is.False);memory.EndTemporary();
                Assert.That(memory.RetainsLiveIdentity(Hash,out uncertain),Is.False);Assert.That(uncertain,Is.False);
            } finally {memory.Drain().GetAwaiter().GetResult();if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
        [Test] public void InflightMemoryCandidateMakesAbsenceUncertainWithoutWaitingForItsWriter()
        {
            string directory=Path.Combine(Path.GetTempPath(),"MaestroLiveReferences-"+Guid.NewGuid().ToString("N"));using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
            var memory=new ProgramMemoryStore(directory,new(),stage=>{if(stage=="written"){entered.Set();release.Wait();}});
            try {
                memory.Initialization.GetAwaiter().GetResult();var write=memory.Write(memory.Snapshot().Revision,MemoryProgram,MemoryValues());
                Assert.That(entered.Wait(TimeSpan.FromSeconds(5)),Is.True);
                Assert.That(memory.RetainsLiveIdentity(Hash,out var uncertain),Is.False,"The detached candidate has not published");Assert.That(uncertain,Is.True);
                release.Set();Assert.That(write.GetAwaiter().GetResult().Error,Is.Null);Assert.That(memory.RetainsLiveIdentity(Hash,out uncertain),Is.True);Assert.That(uncertain,Is.False);
            } finally {release.Set();memory.Drain().GetAwaiter().GetResult();if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
        [Test] public void UnreadableMemoryNeverBecomesKnownAbsence()
        {
            string directory=Path.Combine(Path.GetTempPath(),"MaestroLiveReferences-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,ProgramMemoryStore.FileName),"invalid");
            var memory=new ProgramMemoryStore(directory,new());
            try {memory.Initialization.GetAwaiter().GetResult();Assert.That(memory.Error,Is.Not.Null);Assert.That(memory.RetainsLiveIdentity(Hash,out var uncertain),Is.False);Assert.That(uncertain,Is.True);}
            finally {memory.Drain().GetAwaiter().GetResult();Directory.Delete(directory,true);}
        }
        [Test] public void ModulesRequireCompletedOwnerPublicationAndRetainIncludedDefinitions()
        {
            string directory=Path.Combine(Path.GetTempPath(),"MaestroLiveReferences-"+Guid.NewGuid().ToString("N"));
            var definition=ProgramModuleLibrary.Definition(Source(Function("main",new JArray(Local("asset",Hash)))).ToString(Formatting.None),"Retained asset",new[]{"main"});
            var modules=new ProgramModuleLibrary(directory,new());ProgramModuleLibrary included=null;
            try {
                Assert.That(modules.RetainsLiveIdentity(Hash,out var uncertain),Is.False);Assert.That(uncertain,Is.True,"A finished worker without owner publication is still unknown");
                modules.Flush();Assert.That(modules.RetainsLiveIdentity(Hash,out uncertain),Is.False);Assert.That(uncertain,Is.False);
                var write=modules.Publish(definition);Assert.That(modules.RetainsLiveIdentity(Hash,out uncertain),Is.False);Assert.That(uncertain,Is.True);
                modules.Flush();Assert.That(write.Error,Is.Null);Assert.That(modules.RetainsLiveIdentity(Hash,out uncertain),Is.True);Assert.That(uncertain,Is.False);
                included=new ProgramModuleLibrary(Path.Combine(directory,"included-only"),new(),new[]{definition.ToString(Formatting.None)});included.Flush();
                Assert.That(included.RetainsLiveIdentity(Hash,out uncertain),Is.True);Assert.That(uncertain,Is.False);
                modules.Retire().GetAwaiter().GetResult();Assert.That(modules.RetainsLiveIdentity(Hash,out uncertain),Is.True);Assert.That(uncertain,Is.True,"A retiring owner cannot prove absence");
            } finally {modules.Retire().GetAwaiter().GetResult();included?.Retire().GetAwaiter().GetResult();if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
    }
}
