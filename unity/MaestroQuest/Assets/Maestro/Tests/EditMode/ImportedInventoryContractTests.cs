// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using Maestro.Quest.Imports;
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class ImportedInventoryContractTests
    {
        string directory;
        sealed class Actions:IRuleActions {
            internal int Starts;
            public bool CanRun(CapabilityCall call,out string error){error=null;return true;}
            public bool Start(string id,CapabilityCall call,out float seconds,out string error){Starts++;seconds=0;error=null;return true;}
            public void Stop(string id,bool preserve){}
        }
        [SetUp] public void Before()=>directory=Path.Combine(Path.GetTempPath(),"MaestroInventoryReceipts-"+Guid.NewGuid().ToString("N"));
        [TearDown] public void After(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        static JObject Call(string id)=>new(){["id"]=id,["version"]=1,["arguments"]=id=="model.library.inspect"?new JObject{["offset"]=0}:new JObject{["operation"]="refresh"}};
        static JObject Library()=>new(){["ready"]=true,["error"]="",["total"]=0,["next"]=-1,["entries"]=new JArray()};
        static JObject HistoricalOutput(string id)=>id=="model.library.inspect"?new JObject{["total"]=0,["offset"]=0,["entries"]=new JArray()}:
            new JObject{["requestId"]=new string('a',32),[id=="image.import"?"imageHash":"assetHash"]="",[id=="image.import"?"appearanceId":"sourceId"]="",["revision"]=0,["temporary"]=false,["library"]=Library()};
        static JObject Summary()=>ImportedInventoryObservation.Summary(new ImportedAssetInventory<int>(Array.Empty<int>(),true,true,0,null));
        static JObject OriginalBindingsProgram(string id)
        {
            var fields=id=="model.library.inspect"?JObject.Parse("{\"total\":\"number\",\"offset\":\"number\",\"entries\":{\"list\":{\"record\":{\"modelHash\":\"text\",\"name\":\"text\",\"bytes\":\"number\"}}}}"):
                JObject.Parse(id=="image.import"?
                "{\"requestId\":\"text\",\"imageHash\":\"text\",\"appearanceId\":\"text\",\"revision\":\"number\",\"temporary\":\"boolean\",\"library\":{\"record\":{\"ready\":\"boolean\",\"error\":\"text\",\"total\":\"number\",\"next\":\"number\",\"entries\":{\"list\":{\"record\":{\"id\":\"text\",\"name\":\"text\",\"width\":\"number\",\"height\":\"number\",\"format\":\"text\"}}}}}}":
                "{\"requestId\":\"text\",\"assetHash\":\"text\",\"sourceId\":\"text\",\"revision\":\"number\",\"temporary\":\"boolean\",\"library\":{\"record\":{\"ready\":\"boolean\",\"error\":\"text\",\"total\":\"number\",\"next\":\"number\",\"entries\":{\"list\":{\"record\":{\"id\":\"text\",\"name\":\"text\",\"seconds\":\"number\",\"sampleRate\":\"number\",\"channels\":\"number\"}}}}}}");
            var initial=HistoricalOutput(id);var locals=new JArray();var bindings=new JObject();
            foreach(var field in fields.Properties()){locals.Add(new JObject{["name"]=field.Name,["type"]=field.Value.DeepClone(),["initial"]=initial[field.Name].DeepClone()});bindings[field.Name]=field.Name;}
            return new JObject{["version"]=3,["dataVersion"]=1,["entry"]="main",["resources"]=new JArray(),["state"]=new JArray(),["events"]=new JArray(),["functions"]=new JArray(new JObject{
                ["name"]="main",["returns"]="void",["parameters"]=new JArray(),["locals"]=locals,
                ["body"]=new JArray(new JObject{["id"]="browse",["op"]="invoke",["capability"]=id,["version"]=1,["arguments"]=Call(id)["arguments"].DeepClone(),["bindings"]=new JObject(),["results"]=bindings})
            })};
        }
        [TestCase("model.library.inspect")] [TestCase("image.import")] [TestCase("audio.import")]
        public void HistoricalAndNewReceiptsReloadWithoutInventingCompletenessOrReplaying(string capability)
        {
            var call=Call(capability);var receipts=new InvocationReceipts(directory);
            foreach(bool current in new[]{false,true}){
                var output=HistoricalOutput(capability);if(current)output["inventory"]=Summary();
                Assert.That(CapabilityArguments.Validate(output,BehaviourCatalog.Action(capability).OutputSchema,out var error),Is.True,error);
                string id=receipts.NextId;Assert.That(receipts.Reserve(id,call,Array.Empty<string>(),out error),Is.True,error);
                var done=receipts.Find(id);done["phase"]="completed";done["status"]="Action completed";done["output"]=output.DeepClone();receipts.Update(done);
                receipts=new InvocationReceipts(directory);Assert.That(receipts.Error,Is.Null);Assert.That(JToken.DeepEquals(receipts.Find(id)["output"],output),Is.True);
                if(!current)Assert.That(receipts.Find(id)["output"]["inventory"],Is.Null,"Absence is historical unknown, never complete");
                var actions=new Actions();var scheduler=new RuleScheduler(actions,receipts);scheduler.Tick(1);
                Assert.That(actions.Starts,Is.Zero);Assert.That(scheduler.Invoke(call,2,out _,out _,id),Is.False,"A retained ID must not redispatch");
                Assert.That(actions.Starts,Is.Zero);Assert.That(JToken.DeepEquals(scheduler.Invocation(id)["output"],output),Is.True);
            }
        }
        [TestCase("model.library.inspect")] [TestCase("image.import")] [TestCase("audio.import")]
        public void OriginalVersionOneResultBindingsAcceptHistoricalAndNewOutputs(string capability)
        {
            Assert.That(BehaviourProgram.TryParse(OriginalBindingsProgram(capability).ToString(),out var program,out var error),Is.True,error);
            foreach(bool current in new[]{false,true}){
                var output=HistoricalOutput(capability);if(current)output["inventory"]=Summary();
                var machine=new ProgramMachine(program,null);Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Action),machine.Error);
                Assert.That(machine.CompleteAction(output,out error),Is.True,error);
                foreach(var field in HistoricalOutput(capability).Properties())Assert.That(machine.Locals[field.Name].Same(ProgramValue.Literal(field.Value,machine.Locals[field.Name].Type)),Is.True,field.Name);
                Assert.That(machine.Advance(out _),Is.EqualTo(ProgramYield.Completed),machine.Error);
            }
        }
        [Test] public void UninspectedSummaryIsUnavailableAndHasNoCompletenessClaim()
        {
            var summary=ImportedInventoryObservation.Summary<int>(null);
            Assert.That((bool)summary["ready"],Is.False);Assert.That((bool)summary["complete"],Is.False);
            Assert.That((bool)summary["enumerationComplete"],Is.False);Assert.That((int)summary["issueCount"],Is.Zero);
            Assert.That((string)summary["error"],Is.Not.Empty);Assert.That(CapabilityArguments.Validate(summary,ImportedInventoryObservation.Schema(),out var error),Is.True,error);
        }
    }
}
