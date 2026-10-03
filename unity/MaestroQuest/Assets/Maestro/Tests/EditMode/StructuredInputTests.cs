// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.IO;
using System.Linq;
using Maestro.Quest.Programs;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Maestro.Quest.Tests
{
    public sealed class StructuredInputTests
    {
        static JObject Source()=>JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Maestro/Tests/Fixtures/program-build-structure.json")));
        static ProgramMachine Machine(JObject source) {Assert.That(BehaviourProgram.TryParse(source.ToString(),out var program,out var error),Is.True,error);return new ProgramMachine(program,null);}
        static ProgramYield Advance(ProgramMachine machine,out CapabilityCall call) {
            ProgramYield phase;int ticks=0;do {phase=machine.Advance(out call);Assert.That(++ticks,Is.LessThan(20),machine.Error);}while(phase==ProgramYield.Yield);return phase;
        }
        static void Created(ProgramMachine machine) {
            Assert.That(Advance(machine,out var call),Is.EqualTo(ProgramYield.Action),machine.Error);Assert.That(call.Definition.Id,Is.EqualTo("object.batch.create"));
            Assert.That(machine.CompleteAction(new JObject {["objectIds"]=new JArray(Enumerable.Range(1,6).Select(i=>i.ToString("x32"))),["slots"]=new JArray(Enumerable.Range(1,6).Select(i=>"brick_"+i)),["temporary"]=false},out var error),Is.True,error);
        }
        [Test] public void CreatedListsBecomeExactStructureInputsWithoutAuthorizingTheirPlaceholders() {
            var machine=Machine(Source());Created(machine);
            Assert.That(Advance(machine,out var call),Is.EqualTo(ProgramYield.Action),machine.Error);Assert.That(call.Definition.Id,Is.EqualTo("structure.save"));
            Assert.That(call.Resources,Is.EquivalentTo(Enumerable.Range(1,6).Select(i=>i.ToString("x32"))));Assert.That((string)call.Arguments["source"]["members"][5]["slot"],Is.EqualTo("brick_6"));
            Assert.That(machine.CompleteAction(new JObject {["structureId"]=new string('a',32),["revision"]=int.MaxValue-1,["temporary"]=false},out var error),Is.True,error);
            Assert.That(Advance(machine,out call),Is.EqualTo(ProgramYield.Action),machine.Error);Assert.That(call.Definition.Id,Is.EqualTo("object.position.set"));Assert.That((string)call.Arguments["target"],Is.EqualTo(1.ToString("x32")));Assert.That(machine.CompleteAction(new JObject(),out error),Is.True,error);
            Assert.That(Advance(machine,out call),Is.EqualTo(ProgramYield.Action),machine.Error);Assert.That(call.Definition.Id,Is.EqualTo("structure.reset"));Assert.That((int)call.Arguments["revision"],Is.EqualTo(int.MaxValue-1));Assert.That(call.Resources,Has.Length.EqualTo(6));
            Assert.That(machine.CompleteAction(new JObject {["count"]=6,["temporary"]=false},out error),Is.True,error);Assert.That(Advance(machine,out _),Is.EqualTo(ProgramYield.Completed),machine.Error);
        }
        [TestCase("foreign")] [TestCase("duplicate")] [TestCase("empty")]
        public void ComputedListsAreRevalidatedAndEveryMemberNeedsAuthorityBeforeAnyEffect(string mode) {
            var source=Source();var body=(JArray)source["functions"][0]["body"];body.RemoveAt(1);
            source["functions"][0]["locals"][3]["initial"]=mode=="empty"?new JArray():new JArray(new JObject {["slot"]="left",["target"]=1.ToString("x32")},new JObject {["slot"]=mode=="duplicate"?"left":"right",["target"]=mode=="foreign"?new string('f',32):2.ToString("x32")});
            var machine=Machine(source);Created(machine);Assert.That(Advance(machine,out var call),Is.EqualTo(ProgramYield.Failed));Assert.That(call,Is.Null);if(mode=="foreign")Assert.That(machine.Error,Does.Contain("declared or created"));
        }
        [Test] public void WholeRecordBindingsRejectOverlappingChildrenAndStaticOrVariantInputs() {
            var source=Source();var create=(JObject)source["functions"][0]["body"][0];var bindings=(JObject)create["bindings"];
            bindings["position"]=new JObject {["value"]=create["arguments"]["position"].DeepClone()};
            Assert.That(BehaviourProgram.TryParse(source.ToString(),out _,out var error),Is.True,error);
            bindings["position.x"]=new JObject {["value"]=1};Assert.That(BehaviourProgram.TryParse(source.ToString(),out _,out _),Is.False);
            var capture=(JObject)source["functions"][0]["body"][2];var schema=BehaviourCatalog.Action("structure.save").InputSchema;var args=(JObject)capture["arguments"];
            Assert.That(CapabilitySchema.BindingType(schema,"source.kind",args),Is.Null);Assert.That(CapabilitySchema.BindingType(schema,"source",args),Is.Null);Assert.That(CapabilitySchema.BindingType(schema,"source.members.0.target",args),Is.Null);
            Assert.That(CapabilitySchema.SeparateBindings(JObject.Parse("{\"value\":{},\"value-other\":{},\"value.x\":{}}")),Is.False);
        }
    }
}
