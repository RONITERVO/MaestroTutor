// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using Maestro.Quest.Programs;
using Maestro.Quest.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace Maestro.Quest.Tests
{
    public sealed class CapabilityModuleTests
    {
        sealed class Operation : CapabilityOperation {
            public int Ticks,Stops;public bool Preparing=true,Preserved,FailFinish;
            public override float Seconds=>1;
            public override RuleActionState State(out string error) {error=null;return Preparing?RuleActionState.Preparing:RuleActionState.Ready;}
            public override void Tick() {Ticks++;Preparing=false;}
            public override void Stop(bool preserve) {Stops++;Preserved=preserve;}
            public override bool Complete(out string error) {error=FailFinish?"finish failed":null;return !FailFinish;}
            public override JObject Result=>new JObject {["value"]=Ticks};
        }
        sealed class Module : CapabilityModule {
            public readonly Operation Operation=new();public bool FailStart;
            public override string Id=>"test.custom.module";
            public override string Label=>"Custom module";
            public override string Duration=>"timed";
            public override BehaviourCatalog.Claim[] Claims(JObject arguments)=>new[]{new BehaviourCatalog.Claim("maestro","gaze")};
            public override JObject InputSchema=>CapabilitySchema.Object(new JObject());
            public override bool CanRun(CapabilityContext context,JObject arguments,out string error) {error=null;return true;}
            public override bool Start(CapabilityContext context,string runId,JObject arguments,out CapabilityOperation operation,out string error) {
                operation=Operation;error=FailStart?"partial start":null;return !FailStart;
            }
        }
        [Test] public void DirectModuleTakeoverCleansOnceAndCannotBecomeSuccessfulCompletion() {
            var module=new Module();var definition=new BehaviourCatalog.ActionDefinition(module);Assert.That(definition.TryCall(new JObject(),out var call,out _),Is.True);
            var host=new RoomRuleActions(null,null);Assert.That(host.Start("run",call,out _,out _),Is.True);
            Assert.That(host.Ownership.TryAcquire("hand","Your grip",Maestro.Quest.Interaction.RoomActorRole.Grab,call.Claims,null,out var grip,out _,preservePlacement:true),Is.True);
            Assert.That(host.State("run",out _),Is.EqualTo(RuleActionState.Failed));host.Tick();
            Assert.That(host.Complete("run",out var error),Is.False);Assert.That(error,Does.Contain("Your grip"));
            Assert.That(module.Operation.Stops,Is.EqualTo(1));Assert.That(module.Operation.Preserved,Is.True);Assert.That(module.Operation.Ticks,Is.Zero);
            Assert.That(host.TakeResult("run").Count,Is.Zero);host.Stop("run",false);Assert.That(module.Operation.Stops,Is.EqualTo(1));grip.Dispose();
        }
        [TestCase("complete")] [TestCase("cancel")] [TestCase("startFailure")] [TestCase("finishFailure")]
        public void ModuleWithoutEnumOwnsItsPreparationResultAndExactlyOnceCleanup(string ending)
        {
            var module=new Module {FailStart=ending=="startFailure"};module.Operation.FailFinish=ending=="finishFailure";
            var definition=new BehaviourCatalog.ActionDefinition(module);
            Assert.That(definition.TryCall(new JObject(),out var call,out _),Is.True);Assert.That(call.TryStep(out _,out _),Is.False);
            var host=new RoomRuleActions(null,null);
            Assert.That(host.Start("run",call,out float seconds,out _),Is.EqualTo(!module.FailStart));Assert.That(seconds,Is.EqualTo(1));
            Assert.That(host.Start("run",call,out _,out _),Is.False,"An active operation cannot be silently replaced");
            if(ending=="cancel"||ending=="startFailure")host.Stop("run",true);
            else {
                Assert.That(host.State("run",out _),Is.EqualTo(RuleActionState.Preparing));host.Tick();
                Assert.That(host.State("run",out _),Is.EqualTo(RuleActionState.Ready));
                Assert.That(host.Complete("run",out _),Is.EqualTo(ending=="complete"));
            }
            Assert.That(module.Operation.Stops,Is.EqualTo(1));Assert.That(module.Operation.Preserved,Is.EqualTo(ending=="cancel"||ending=="startFailure"));
            var result=host.TakeResult("run");Assert.That(result.Count,Is.EqualTo(ending=="complete"?1:0));
            if(ending=="complete")Assert.That((int)result["value"],Is.EqualTo(1));
            Assert.That(host.TakeResult("run").Count,Is.Zero);host.Stop("run",false);host.Tick();
            Assert.That(module.Operation.Stops,Is.EqualTo(1),"Terminal operations cannot be stopped or ticked again");
        }
    }
}
