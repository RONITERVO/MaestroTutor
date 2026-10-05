// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Maestro.Quest.Creation;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.Profiling;
namespace Maestro.Quest.Tests
{
    public sealed class RoomPublicationTests
    {
        string directory;
        const string ObjectId="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        sealed class Actions : IRuleActions,IRuleResults
        {
            readonly HashSet<string> waits=new();
            public bool CanRun(CapabilityCall call,out string error){error=null;return true;}
            public bool Start(string id,CapabilityCall call,out float seconds,out string error){seconds=call.Instant?0:1;if(!call.Instant)waits.Add(id);error=null;return true;}
            public void Stop(string id,bool preserve){}
            public JObject TakeResult(string id)=>waits.Remove(id)?new JObject():new JObject {["objectId"]=ObjectId};
        }
        [SetUp] public void Before()=>directory=Path.Combine(Path.GetTempPath(),"MaestroPublication-"+Guid.NewGuid().ToString("N"));
        [TearDown] public void After(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        static JObject Call(int count)
        {
            var args=(JObject)BehaviourCatalog.Action("object.create").InputSchema["oneOf"].Single(v=>(string)v["examples"][0]["kind"]=="recipe")["examples"][0].DeepClone();
            var part=args["recipe"]["parts"][0];
            args["recipe"]["parts"]=new JArray(Enumerable.Range(0,count).Select(i=>{var p=(JObject)part.DeepClone();p["id"]="part_"+i;return p;}));
            args["recipe"]["tracks"]=new JArray();
            Assert.That(BehaviourCatalog.TryCall("object.create",1,args,out _,out var error),Is.True,error);
            return new JObject {["id"]="object.create",["version"]=1,["arguments"]=args};
        }
        RuleScheduler Scheduler(int parts,bool live,out string selected)
        {
            var path=Path.Combine(directory,Guid.NewGuid().ToString("N"));var receipts=new InvocationReceipts(path);var scheduler=new RuleScheduler(new Actions(),receipts);selected=null;
            for(int i=0;i<4;i++) {
                Assert.That(scheduler.Invoke(Call(parts),i*2,out selected,out var error,receipts.NextId),Is.True,error);
                scheduler.Tick(i*2+1.5f);
            }
            if(live)Assert.That(scheduler.Invoke(new JObject {["id"]="time.wait",["version"]=1,["arguments"]=new JObject {["seconds"]=1}},9,out _,out var waitError,receipts.NextId),Is.True,waitError);
            return live?scheduler:new RuleScheduler(new Actions(),new InvocationReceipts(path));
        }
        static long Allocations(Func<object> observe)
        {
            for(int i=0;i<8;i++)GC.KeepAlive(observe());
            using var recorder=new ProfilerRecorder(ProfilerCategory.Memory,"GC.Alloc",1,ProfilerRecorderOptions.WrapAroundWhenCapacityReached|ProfilerRecorderOptions.SumAllSamplesInFrame|ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            Assert.That(recorder.Valid,Is.True);recorder.Start();for(int i=0;i<16;i++)GC.KeepAlive(observe());recorder.Stop();
            Assert.That(recorder.Count,Is.GreaterThan(0),"Unsupported allocation counters must not pass silently");return recorder.GetSample(0).Count/16;
        }
        [TestCase(false)] [TestCase(true)] public void UnselectedReceiptsDoNotCopyHiddenRecipeArguments(bool live)
        {
            var small=Scheduler(1,live,out _);var detailed=Scheduler(32,live,out _);
            long first=Allocations(()=>small.ObserveInvocations(null)),second=Allocations(()=>detailed.ObserveInvocations(null));
            TestContext.WriteLine($"Receipt summary allocations ({(live?"live":"restarted")}): {first} (1 part), {second} (32 parts)");
            Assert.That(second-first,Is.LessThanOrEqualTo(2),"Summary publication must not copy omitted creation arguments");
        }
        [Test] public void SelectedCallsAndOutputsRemainExactDetachedAndTrackCompletionAndRestart()
        {
            var scheduler=Scheduler(4,true,out var id);var expected=scheduler.Invocation(id);var view=scheduler.ObserveInvocations(id);
            Assert.That(JToken.DeepEquals(view["selected"],expected),Is.True);Assert.That(view["running"].Count(),Is.EqualTo(1));Assert.That(view["outcomes"].Count(),Is.EqualTo(4));
            Assert.That(view["running"].Concat(view["outcomes"]).All(v=>v["call"]==null),Is.True);
            Assert.That((string)view["outcomes"][0]["output"]["objectId"],Is.EqualTo(ObjectId));
            view["selected"]["call"]["arguments"]["recipe"]["parts"][0]["id"]="outside_edit";view["running"][0]["status"]="outside_edit";view["outcomes"][0]["output"]["objectId"]="outside_edit";
            Assert.That(JToken.DeepEquals(scheduler.Invocation(id),expected),Is.True);Assert.That((string)scheduler.ObserveInvocations(null)["outcomes"][0]["output"]["objectId"],Is.EqualTo(ObjectId));
            var waiting=(string)view["running"][0]["id"];scheduler.Tick(11);Assert.That((string)scheduler.ObserveInvocations(waiting)["selected"]["phase"],Is.EqualTo("completed"));var completed=scheduler.ObserveInvocations(id);Assert.That((string)completed["selected"]["phase"],Is.EqualTo("completed"));Assert.That((string)completed["selected"]["output"]["objectId"],Is.EqualTo(ObjectId));Assert.That(completed["running"].Count(),Is.Zero);
            var restarted=new RuleScheduler(new Actions(),new InvocationReceipts(Directory.GetDirectories(directory).Single()));
            Assert.That(restarted.ObserveInvocations(id)["selected"].ToString(Formatting.None),Is.EqualTo(completed["selected"].ToString(Formatting.None)),"Restart preserves the exact published receipt JSON");Assert.That(restarted.ObserveInvocations(Guid.NewGuid().ToString("N"))["selected"].Type,Is.EqualTo(JTokenType.Null));
        }
        [Test] public void WireWritesStructuredPayloadsWithoutReparentingOrChangingValues()
        {
            var owner=JObject.Parse("{\"capture\":{\"text\":\"quotes \\\" slash \\\\ newline \\n Unicode \u00e4\",\"value\":null},\"catalog\":{\"values\":[true,2,0.25,{\"nested\":\"value\"}]},\"execution\":{\"selected\":null,\"running\":[]}}");
            var before=owner.ToString(Formatting.None);var state=new RoomAgentState {objects=Array.Empty<RoomAgentObject>(),capture=(JObject)owner["capture"],catalog=(JObject)owner["catalog"],execution=(JObject)owner["execution"],rules=new RuleView()};
            var wire=JObject.Parse(RoomAgentWire.Serialize(state));
            foreach(var name in new[]{"capture","catalog","execution"}){Assert.That(JToken.DeepEquals(wire[name],owner[name]),Is.True,name);Assert.That(owner[name].Parent,Is.SameAs(owner.Property(name)));}
            Assert.That(owner.ToString(Formatting.None),Is.EqualTo(before));Assert.That(wire["inspection"].Type,Is.EqualTo(JTokenType.Null));Assert.That(wire["rules"]["memory"].Type,Is.EqualTo(JTokenType.Null));Assert.That(wire.ContainsKey("temporaryRoom"),Is.False);
            state.capture=state.catalog=state.execution=null;wire=JObject.Parse(RoomAgentWire.Serialize(state));foreach(var name in new[]{"capture","catalog","execution"})Assert.That(wire[name].Type,Is.EqualTo(JTokenType.Null));
        }
    }
}
